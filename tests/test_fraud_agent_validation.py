# tests/test_fraud_agent_validation.py
import pytest

from claims_assistant.agents.fraud_agent import _expected_tier, _validate_assessment
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import compute_fraud_signals
from claims_assistant.mcp_servers.claims_history import ClaimsHistoryResult, ClaimSummary
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

_POLICY = PolicyLookupResult(
    policy_number="POL-TEST-0001",
    policyholder_name="Test Person",
    state="TX",
    coverage_tier="comprehensive_collision",
    policy_form_id="TX-COMPREHENSIVE-COLLISION",
    effective_date="2025-07-15",
    expiration_date="2026-07-15",
    premium_monthly=198.40,
)
_VEHICLE = VehicleLookupResult(
    vin="TESTVIN0000000001",
    make="Ford",
    model="F-150",
    year=2017,
    market_value_usd=19750.0,
    policy_number="POL-TEST-0001",
)
_CLAIMS_HISTORY = ClaimsHistoryResult(
    policy_number="POL-TEST-0001",
    claim_count=1,
    prior_fraud_flag_count=1,
    most_recent_claim_date="2025-07-20",
    claims=[
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ],
)
_SIGNALS = compute_fraud_signals(_POLICY, _CLAIMS_HISTORY, _VEHICLE, incident_date="2025-08-01")


def test_expected_tier_boundaries():
    assert _expected_tier(0) == "low"
    assert _expected_tier(33) == "low"
    assert _expected_tier(34) == "medium"
    assert _expected_tier(66) == "medium"
    assert _expected_tier(67) == "high"
    assert _expected_tier(100) == "high"


def test_validate_assessment_passes_for_grounded_flags_and_consistent_tier():
    assessment = FraudRiskAssessment(
        risk_score=85,
        risk_tier="high",
        red_flags=["prior_fraud_flag", "recent_policy_inception"],
        rationale="grounded",
    )

    _validate_assessment(assessment, _SIGNALS)  # does not raise


def test_validate_assessment_raises_on_a_fabricated_red_flag():
    assessment = FraudRiskAssessment(
        risk_score=85,
        risk_tier="high",
        red_flags=["high_claim_frequency"],
        rationale="fabricated",
    )

    with pytest.raises(ValueError, match="high_claim_frequency"):
        _validate_assessment(assessment, _SIGNALS)


def test_validate_assessment_raises_on_tier_score_mismatch():
    assessment = FraudRiskAssessment(
        risk_score=90, risk_tier="low", red_flags=[], rationale="mismatched tier"
    )

    with pytest.raises(ValueError, match="risk_tier"):
        _validate_assessment(assessment, _SIGNALS)
