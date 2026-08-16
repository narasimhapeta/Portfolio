# tests/test_fraud_signals.py
from claims_assistant.agents.fraud_signals import (
    compute_fraud_signals,
    determine_actual_red_flags,
)
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


def _claims_history(claims: list[ClaimSummary]) -> ClaimsHistoryResult:
    return ClaimsHistoryResult(
        policy_number="POL-TEST-0001",
        claim_count=len(claims),
        prior_fraud_flag_count=sum(1 for c in claims if c.fraud_flag),
        most_recent_claim_date=claims[0].claim_date if claims else None,
        claims=claims,
    )


def test_compute_fraud_signals_computes_day_deltas_and_ratio():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ]

    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2025-08-01"
    )

    assert signals.days_since_policy_effective == 17
    assert signals.days_since_most_recent_prior_claim == 12
    assert signals.highest_prior_claim_to_market_value_ratio == 1.0


def test_compute_fraud_signals_handles_no_prior_claims():
    signals = compute_fraud_signals(
        _POLICY, _claims_history([]), _VEHICLE, incident_date="2026-03-10"
    )

    assert signals.claim_count == 0
    assert signals.most_recent_prior_claim_date is None
    assert signals.days_since_most_recent_prior_claim is None
    assert signals.highest_prior_claim_amount_usd is None
    assert signals.highest_prior_claim_to_market_value_ratio is None


def test_determine_actual_red_flags_flags_recent_inception_and_prior_fraud():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2025-08-01"
    )

    flags = determine_actual_red_flags(signals)

    assert flags == {
        "recent_policy_inception",
        "prior_fraud_flag",
        "clustered_recent_claims",
        "prior_claim_near_vehicle_value",
    }


def test_determine_actual_red_flags_empty_for_clean_case():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-11-01",
            claim_type="comprehensive",
            amount_usd=2100.0,
            status="approved",
            fraud_flag=False,
        )
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2026-03-10"
    )

    flags = determine_actual_red_flags(signals)

    assert flags == set()


def test_determine_actual_red_flags_flags_high_frequency():
    claims = [
        ClaimSummary(
            claim_id=f"CLM-{i}",
            claim_date="2025-09-01",
            claim_type="collision",
            amount_usd=1000.0,
            status="approved",
            fraud_flag=False,
        )
        for i in range(2)
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2026-06-01"
    )

    flags = determine_actual_red_flags(signals)

    assert "high_claim_frequency" in flags
