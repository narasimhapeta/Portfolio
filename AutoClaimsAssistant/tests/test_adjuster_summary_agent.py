# tests/test_adjuster_summary_agent.py
import pytest

from claims_assistant.agents.adjuster_summary_agent import (
    build_adjuster_summary_agent,
    summarize_for_adjuster,
)
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_summarize_for_adjuster_produces_nonempty_narrative_and_next_step():
    settings = get_settings()
    agent = build_adjuster_summary_agent(settings)
    coverage = CoverageDetermination(
        determination="approve",
        rationale="Comprehensive coverage clause explicitly covers hail damage.",
        citations=["POL-CA-0003-chunk-04"],
    )
    fraud = FraudRiskAssessment(
        risk_score=8,
        risk_tier="low",
        red_flags=[],
        rationale="No prior claims, policy in force well over a year, no red flags present.",
    )

    summary = await summarize_for_adjuster(agent, "POL-CA-0003", coverage, fraud)

    assert summary.narrative_summary
    assert summary.recommended_next_step
