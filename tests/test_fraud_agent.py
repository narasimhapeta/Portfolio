# tests/test_fraud_agent.py
import pytest

from claims_assistant.agents.fraud_agent import assess_fraud_risk, build_fraud_agent
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_clean_claim_on_low_history_policy_is_low_risk(seeded_db):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        incident_date="2026-03-10",
        claim_narrative=(
            "Hail damage to my Jeep Grand Cherokee while it was parked outside my "
            "home overnight during a storm."
        ),
    )

    assert result.risk_tier == "low"
    assert result.red_flags == []


@pytest.mark.asyncio
async def test_theft_claim_shortly_after_policy_start_with_prior_fraud_is_high_risk(
    seeded_db,
):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
        policy_number="POL-TX-0006",
        vin="1FTFW1ET5EF123461",
        incident_date="2025-08-01",
        claim_narrative=(
            "My Ford F-150 was stolen overnight from a parking lot; I don't have "
            "any other details."
        ),
    )

    assert result.risk_tier == "high"
    assert result.risk_score >= 67
    assert "prior_fraud_flag" in result.red_flags
    assert "recent_policy_inception" in result.red_flags
    assert len(result.rationale) > 0
