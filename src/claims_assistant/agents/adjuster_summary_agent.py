# src/claims_assistant/agents/adjuster_summary_agent.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.agents.adjuster_summary_schema import AdjusterSummary
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.config import Settings

INSTRUCTIONS = """\
You are writing a short briefing for a human insurance adjuster who is about to make a \
final decision on a claim. You are given:
1. The policy number.
2. A coverage determination (approve/deny/needs_info) with its rationale and citations, \
already decided by a coverage-specialist process — do not second-guess or restate it.
3. A fraud-risk assessment (score/tier/red flags) with its rationale, already computed by \
a fraud-specialist process — do not second-guess or restate it.

Write:
- "narrative_summary": a short paragraph (2-4 sentences) synthesizing the coverage and \
fraud findings into a single readable briefing, written for someone who has not seen the \
underlying data. Reference the concrete reasons given (e.g. which clause, which red flags \
or their absence), but do not invent new facts.
- "recommended_next_step": one short, concrete, actionable sentence for what the adjuster \
should do next (e.g. "Approve and close, citing the comprehensive damage clause." or \
"Request additional documentation regarding the vehicle's ownership history before \
approving." or "Escalate to fraud investigation before any payout decision."). This is \
advisory only — you are not making the final claims decision, the human adjuster is.

Both fields must stay consistent with the coverage determination and fraud tier you were \
given — never recommend an action that contradicts them (for example, do not recommend \
approval if the coverage determination is "deny", and do not describe a "high" fraud tier \
as low-risk). If the determination is "needs_info", your recommended next step should be \
about resolving that open question, not approving or denying outright.
"""


def build_adjuster_summary_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_adjuster_summary_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_adjuster_summary_agent(settings: Settings) -> Agent:
    client = build_adjuster_summary_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


def _build_prompt(
    policy_number: str, coverage: CoverageDetermination, fraud: FraudRiskAssessment
) -> str:
    return (
        f"Policy number: {policy_number}\n\n"
        f"Coverage determination: {coverage.determination}\n"
        f"Coverage rationale: {coverage.rationale}\n"
        f"Coverage citations: {', '.join(coverage.citations) or 'none'}\n\n"
        f"Fraud risk score: {fraud.risk_score}\n"
        f"Fraud risk tier: {fraud.risk_tier}\n"
        f"Fraud red flags: {', '.join(fraud.red_flags) or 'none'}\n"
        f"Fraud rationale: {fraud.rationale}\n\n"
        f"Write the adjuster briefing."
    )


async def summarize_for_adjuster(
    agent: Agent,
    policy_number: str,
    coverage: CoverageDetermination,
    fraud: FraudRiskAssessment,
) -> AdjusterSummary:
    prompt = _build_prompt(policy_number, coverage, fraud)
    response = await agent.run(prompt, options=ChatOptions(response_format=AdjusterSummary))
    summary = response.value
    assert isinstance(summary, AdjusterSummary)
    return summary
