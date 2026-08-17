# tests/test_adjuster_summary_schema.py
from claims_assistant.agents.adjuster_summary_schema import (
    AdjusterSummary,
    assemble_claim_recommendation,
)
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment


def test_adjuster_summary_holds_narrative_and_next_step():
    summary = AdjusterSummary(
        narrative_summary="Rear-end collision, no injuries, coverage clear.",
        recommended_next_step="Approve and close.",
    )

    assert summary.recommended_next_step == "Approve and close."


def test_assemble_claim_recommendation_passes_through_coverage_and_fraud_facts():
    coverage = CoverageDetermination(
        determination="approve", rationale="clause X covers this", citations=["c1", "c2"]
    )
    fraud = FraudRiskAssessment(
        risk_score=15, risk_tier="low", red_flags=[], rationale="no red flags present"
    )
    summary = AdjusterSummary(
        narrative_summary="Clean claim, low risk, covered.",
        recommended_next_step="Approve and close.",
    )

    recommendation = assemble_claim_recommendation("POL-OH-0001", coverage, fraud, summary)

    assert recommendation.policy_number == "POL-OH-0001"
    assert recommendation.coverage_determination == "approve"
    assert recommendation.coverage_rationale == "clause X covers this"
    assert recommendation.coverage_citations == ["c1", "c2"]
    assert recommendation.fraud_risk_score == 15
    assert recommendation.fraud_risk_tier == "low"
    assert recommendation.fraud_red_flags == []
    assert recommendation.fraud_rationale == "no red flags present"
    assert recommendation.narrative_summary == "Clean claim, low risk, covered."
    assert recommendation.recommended_next_step == "Approve and close."
