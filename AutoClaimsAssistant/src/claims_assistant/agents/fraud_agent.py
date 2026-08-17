# src/claims_assistant/agents/fraud_agent.py
from __future__ import annotations

from typing import Literal, cast

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.agents.coverage_agent import lookup_policy_by_number
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import (
    FraudSignals,
    RedFlagCode,
    compute_fraud_signals,
    determine_actual_red_flags,
)
from claims_assistant.config import Settings
from claims_assistant.mcp_servers.claims_history import ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

INSTRUCTIONS = """\
You are an insurance fraud-risk analyst. For each request you are given:
1. The policyholder's policy metadata (coverage tier, state, effective date).
2. A structured summary of the policyholder's prior claims history.
3. The vehicle's decoded make/model/year/market value.
4. Deterministically computed red-flag signals — booleans already calculated from the \
above data and given to you as ground truth. Do not recompute or contradict them.
5. The new claim's incident date and narrative.

Assess this new claim's fraud risk.

Rules:
- "red_flags" must be chosen ONLY from the signals explicitly marked TRUE in the \
computed signals block. Never include a red flag code marked false — that would be \
fabricating a red flag not supported by the actual data.
- You may also weigh the narrative itself for internal inconsistencies or implausible \
details (for example, injuries described inconsistently, or a narrative that doesn't \
match the claimed loss type) — describe these in "rationale" ONLY, since they are not \
one of the tool-grounded red flag codes above.
- "risk_score" is 0-100. Use the number and severity of TRUE red flags, plus any \
narrative concerns, to set the score holistically — you are not computing a fixed \
formula, but more/stronger signals should push the score higher.
- "risk_tier" must be "low" for risk_score 0-33, "medium" for 34-66, "high" for 67-100.
- "rationale" should be a short, adjuster-readable explanation naming the specific \
computed numbers (e.g. days since policy effective, claim counts, dollar amounts) that \
justify the score, so an adjuster can verify each claim against the data you were given.
"""

_ALL_RED_FLAG_CODES: tuple[RedFlagCode, ...] = (
    "recent_policy_inception",
    "high_claim_frequency",
    "prior_fraud_flag",
    "clustered_recent_claims",
    "prior_claim_near_vehicle_value",
)


def build_fraud_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_fraud_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_fraud_agent(settings: Settings) -> Agent:
    client = build_fraud_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


async def _call_mcp_tool(
    url: str, tool_name: str, arguments: dict[str, str]
) -> dict[str, object]:
    async with streamable_http_client(url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(tool_name, arguments)
    if result.is_error:
        raise ValueError(f"{tool_name} failed for arguments={arguments!r}")
    assert result.structured_content is not None
    return cast(dict[str, object], result.structured_content)


async def lookup_claims_history(settings: Settings, policy_number: str) -> ClaimsHistoryResult:
    content = await _call_mcp_tool(
        settings.claims_history_mcp_url,
        "get_claims_history",
        {"policy_number": policy_number},
    )
    return ClaimsHistoryResult.model_validate(content)


async def lookup_vehicle_by_vin(settings: Settings, vin: str) -> VehicleLookupResult:
    content = await _call_mcp_tool(settings.vin_vehicle_mcp_url, "decode_vin", {"vin": vin})
    return VehicleLookupResult.model_validate(content)


def _expected_tier(risk_score: int) -> Literal["low", "medium", "high"]:
    if risk_score <= 33:
        return "low"
    if risk_score <= 66:
        return "medium"
    return "high"


def _validate_assessment(assessment: FraudRiskAssessment, signals: FraudSignals) -> None:
    actual_flags = determine_actual_red_flags(signals)
    fabricated = [f for f in assessment.red_flags if f not in actual_flags]
    if fabricated:
        raise ValueError(f"fraud assessment cited unsupported red flag(s): {fabricated}")
    expected_tier = _expected_tier(assessment.risk_score)
    if assessment.risk_tier != expected_tier:
        raise ValueError(
            f"risk_tier {assessment.risk_tier!r} inconsistent with risk_score "
            f"{assessment.risk_score} (expected {expected_tier!r})"
        )


def _build_prompt(
    policy: PolicyLookupResult,
    signals: FraudSignals,
    actual_red_flags: set[RedFlagCode],
    claim_narrative: str,
) -> str:
    flags_block = "\n".join(
        f"- {code}: {'TRUE' if code in actual_red_flags else 'false'}"
        for code in _ALL_RED_FLAG_CODES
    )
    return (
        f"Policy metadata:\n"
        f"- Policy number: {policy.policy_number}\n"
        f"- State: {policy.state}\n"
        f"- Coverage tier: {policy.coverage_tier}\n"
        f"- Policy effective date: {signals.policy_effective_date}\n\n"
        f"Claims history:\n"
        f"- Total prior claims: {signals.claim_count}\n"
        f"- Prior fraud-flagged claims: {signals.prior_fraud_flag_count}\n"
        f"- Most recent prior claim date: {signals.most_recent_prior_claim_date}\n"
        f"- Highest prior claim amount: {signals.highest_prior_claim_amount_usd}\n\n"
        f"Vehicle:\n"
        f"- {signals.vehicle_year} {signals.vehicle_make} {signals.vehicle_model}\n"
        f"- Market value: ${signals.vehicle_market_value_usd}\n\n"
        f"Computed red-flag signals (ground truth — only cite flags marked TRUE):\n"
        f"{flags_block}\n\n"
        f"New claim:\n"
        f"- Incident date: {signals.incident_date}\n"
        f"- Days since policy effective: {signals.days_since_policy_effective}\n"
        f"- Days since most recent prior claim: "
        f"{signals.days_since_most_recent_prior_claim}\n"
        f"- Narrative: {claim_narrative}\n\n"
        f"Assess this claim's fraud risk."
    )


async def assess_fraud_risk(
    agent: Agent,
    settings: Settings,
    policy_number: str,
    vin: str,
    incident_date: str,
    claim_narrative: str,
) -> FraudRiskAssessment:
    policy = await lookup_policy_by_number(settings, policy_number)
    claims_history = await lookup_claims_history(settings, policy_number)
    vehicle = await lookup_vehicle_by_vin(settings, vin)
    signals = compute_fraud_signals(policy, claims_history, vehicle, incident_date)
    actual_flags = determine_actual_red_flags(signals)
    prompt = _build_prompt(policy, signals, actual_flags, claim_narrative)
    response = await agent.run(
        prompt, options=ChatOptions(response_format=FraudRiskAssessment)
    )
    assessment = response.value
    assert isinstance(assessment, FraudRiskAssessment)
    _validate_assessment(assessment, signals)
    return assessment
