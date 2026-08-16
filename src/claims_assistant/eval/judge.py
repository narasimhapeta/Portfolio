# src/claims_assistant/eval/judge.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.config import Settings
from claims_assistant.eval.judge_schema import GroundingJudgment

INSTRUCTIONS = """\
You are an evaluation judge for an insurance claims-assistant system. You are given a \
CLAIM -- a short piece of reasoning text an agent produced -- and EVIDENCE -- the source \
material the agent was supposed to base that reasoning on.

Decide whether every factual assertion in the CLAIM is actually supported by the EVIDENCE. \
This is a grounding check, not a correctness check: you are not judging whether the \
agent's ultimate decision (e.g. approve/deny, or a fraud risk tier) was the right call. \
You are judging only whether the stated reasoning is faithful to the evidence given.

Set "grounded" to true only if every specific factual claim in the CLAIM text traces back \
to something actually stated in the EVIDENCE. Set it to false if the CLAIM asserts \
anything -- a number, a clause, a fact -- that the EVIDENCE does not support, or that \
contradicts the EVIDENCE. "reasoning" should briefly explain your verdict, quoting the \
specific part of the CLAIM that is or isn't supported.
"""


def build_judge_agent(settings: Settings, deployment: str) -> Agent:
    client = OpenAIChatCompletionClient(
        model=deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )
    return Agent(client=client, instructions=INSTRUCTIONS)


def _build_prompt(claim_text: str, evidence_text: str) -> str:
    return (
        f"CLAIM:\n{claim_text}\n\n"
        f"EVIDENCE:\n{evidence_text}\n\n"
        f"Judge whether the CLAIM is grounded in the EVIDENCE."
    )


async def judge_grounding(agent: Agent, claim_text: str, evidence_text: str) -> GroundingJudgment:
    prompt = _build_prompt(claim_text, evidence_text)
    response = await agent.run(prompt, options=ChatOptions(response_format=GroundingJudgment))
    judgment = response.value
    assert isinstance(judgment, GroundingJudgment)
    return judgment
