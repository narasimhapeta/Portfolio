# src/claims_assistant/agents/coverage_agent.py
from __future__ import annotations

import sys

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.config import Settings
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.search.retrieval import RetrievedChunk, retrieve_policy_chunks

INSTRUCTIONS = """\
You are an insurance coverage-determination specialist. For each request you are given:
1. The policyholder's policy metadata (coverage tier, state, effective/expiration dates).
2. A set of retrieved clauses from that exact policy document, each labeled with a chunk_id.
3. The claim narrative describing what happened.

Determine whether the described loss is covered under the policy's own text.

Rules:
- Base your determination ONLY on the retrieved policy clauses provided. Do not use outside \
knowledge of insurance law or assume coverage that isn't stated in the clauses.
- "citations" must be chunk_ids copied verbatim from the retrieved clauses given to you. Never \
invent a chunk_id or cite a clause that was not provided.
- If the retrieved clauses clearly show the loss is covered, respond "approve" and cite the \
specific clause(s) that establish coverage.
- If the retrieved clauses clearly show the loss is excluded, or the policy tier doesn't include \
this type of coverage at all, respond "deny" and cite the specific clause(s) establishing that.
- If the retrieved clauses clearly show the loss is excluded with no conditions attached, or the \
policy tier doesn't include this type of coverage at all, respond "deny" and cite the specific \
clause(s) establishing that.
- If coverage depends on a fact the clauses reference but the claim narrative doesn't confirm or \
deny (for example, whether an optional endorsement was added), respond "needs_info" rather than \
guessing, and cite the clause(s) that raise the open question. The narrative not mentioning a \
conditional fact is NOT the same as the narrative establishing that fact is absent — treat it as \
unknown, not as a negative answer. Only respond "deny" for a conditional clause like this if the \
narrative affirmatively confirms the excluded condition (e.g. the policyholder states they do NOT \
have the endorsement); silence means "needs_info", not "deny".
"""

_POLICY_DB_SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.policy_db"],
)


def build_coverage_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_coverage_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_coverage_agent(settings: Settings) -> Agent:
    client = build_coverage_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


async def lookup_policy_by_number(policy_number: str) -> PolicyLookupResult:
    # Raises rather than returning a structured "lookup failed" output (spec §8 describes
    # the latter) — there's no API layer yet to translate this into a response; Phase 7
    # (FastAPI orchestrator endpoints) is where this becomes a caught, surfaced error
    # instead of a propagating exception.
    async with stdio_client(_POLICY_DB_SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": policy_number}
            )
    if result.is_error:
        raise ValueError(f"policy lookup failed for policy_number={policy_number!r}")
    assert result.structured_content is not None
    return PolicyLookupResult.model_validate(result.structured_content)


def _validate_citations(citations: list[str], retrieved: list[RetrievedChunk]) -> None:
    valid_ids = {chunk.chunk_id for chunk in retrieved}
    invalid = [c for c in citations if c not in valid_ids]
    if invalid:
        raise ValueError(f"coverage determination cited unknown chunk id(s): {invalid}")


def _build_prompt(
    policy: PolicyLookupResult, chunks: list[RetrievedChunk], claim_narrative: str
) -> str:
    clauses = "\n\n".join(
        f"[chunk_id: {c.chunk_id}] {c.section_title}\n{c.content}" for c in chunks
    )
    return (
        f"Policy metadata:\n"
        f"- Policy number: {policy.policy_number}\n"
        f"- Coverage tier: {policy.coverage_tier}\n"
        f"- State: {policy.state}\n"
        f"- Effective: {policy.effective_date} to {policy.expiration_date}\n\n"
        f"Retrieved policy clauses:\n{clauses}\n\n"
        f"Claim narrative:\n{claim_narrative}\n\n"
        f"Determine coverage."
    )


async def determine_coverage(
    agent: Agent, settings: Settings, policy_number: str, claim_narrative: str
) -> CoverageDetermination:
    policy = await lookup_policy_by_number(policy_number)
    chunks = await retrieve_policy_chunks(
        settings, form_id=policy.policy_form_id, query_text=claim_narrative
    )
    prompt = _build_prompt(policy, chunks, claim_narrative)
    response = await agent.run(
        prompt, options=ChatOptions(response_format=CoverageDetermination)
    )
    determination = response.value
    assert isinstance(determination, CoverageDetermination)
    _validate_citations(determination.citations, chunks)
    return determination
