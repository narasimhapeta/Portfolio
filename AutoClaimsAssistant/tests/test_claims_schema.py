# tests/test_claims_schema.py
from __future__ import annotations

import datetime
import uuid

from claims_assistant.api.claims_schema import claim_response_from_model
from claims_assistant.models import Claim

_NOW = datetime.datetime.now(datetime.UTC)


def test_claim_response_from_model_maps_completed_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text="Hail damage.",
        status="completed",
        created_at=_NOW,
        recommendation={
            "policy_number": "POL-CA-0003",
            "coverage_determination": "approve",
            "coverage_rationale": "clause X covers this",
            "coverage_citations": ["c1"],
            "fraud_risk_score": 10,
            "fraud_risk_tier": "low",
            "fraud_red_flags": [],
            "fraud_rationale": "clean",
            "narrative_summary": "Hail damage, covered, low risk.",
            "recommended_next_step": "Approve and close.",
        },
        clarification=None,
        error_message=None,
    )

    response = claim_response_from_model(claim)

    assert response.status == "completed"
    assert response.recommendation is not None
    assert response.recommendation.coverage_determination == "approve"
    assert response.clarification is None
    assert response.error is None


def test_claim_response_from_model_maps_clarification_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text="Something happened, not sure when.",
        status="needs_clarification",
        created_at=_NOW,
        recommendation=None,
        clarification={
            "policy_number": "POL-CA-0003",
            "reason": "low-confidence fields: injuries",
            "low_confidence_fields": ["injuries"],
            "missing_required_fields": [],
            "extraction": {
                "facts": {
                    "incident_datetime": "2026-07-09T17:15",
                    "location": "Elm Street, Columbus, OH",
                    "parties": [{"role": "policyholder", "name": "Priya Natarajan"}],
                    "vehicles": [
                        {"role": "policyholder_vehicle", "description": "Jeep Grand Cherokee"}
                    ],
                    "injuries": False,
                    "narrative_summary": "Hail damage.",
                },
                "confidence": {
                    "incident_datetime": 0.9,
                    "location": 0.9,
                    "parties": 0.9,
                    "vehicles": 0.9,
                    "injuries": 0.3,
                    "narrative_summary": 0.9,
                },
            },
        },
        error_message=None,
    )

    response = claim_response_from_model(claim)

    assert response.status == "needs_clarification"
    assert response.recommendation is None
    assert response.clarification is not None
    assert response.clarification.reason == "low-confidence fields: injuries"


def test_claim_response_from_model_maps_failed_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-ZZ-9999",
        vin="UNKNOWNVIN0000001",
        narrative_text="...",
        status="failed",
        created_at=_NOW,
        recommendation=None,
        clarification=None,
        error_message="policy lookup failed for policy_number='POL-ZZ-9999'",
    )

    response = claim_response_from_model(claim)

    assert response.status == "failed"
    assert response.recommendation is None
    assert response.clarification is None
    assert response.error == "policy lookup failed for policy_number='POL-ZZ-9999'"
