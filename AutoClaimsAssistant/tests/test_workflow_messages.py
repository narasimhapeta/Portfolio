# tests/test_workflow_messages.py
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.messages import (
    ClaimIntakeRequest,
    ClarificationRequest,
    CoverageOutcome,
    ExtractionResult,
    FraudOutcome,
)

_FACTS = FNOLFacts(
    incident_datetime="2026-07-09T17:15",
    location="Elm Street, Columbus, OH",
    parties=[Party(role="policyholder", name="Harold Bennett")],
    vehicles=[VehicleInfo(role="policyholder_vehicle", description="Chevrolet Equinox")],
    injuries=False,
    narrative_summary="Rear-ended while stopped for a pedestrian.",
)
_CONFIDENCE = FieldConfidence(
    incident_datetime=0.95, location=0.9, parties=0.85, vehicles=0.85, injuries=0.8,
    narrative_summary=0.9,
)


def test_claim_intake_request_holds_policy_vin_and_narrative():
    request = ClaimIntakeRequest(
        policy_number="POL-OH-0001", vin="1GNSKBKC5FR123456", narrative_text="..."
    )

    assert request.policy_number == "POL-OH-0001"
    assert request.vin == "1GNSKBKC5FR123456"


def test_extraction_result_wraps_request_and_extraction():
    request = ClaimIntakeRequest(policy_number="POL-OH-0001", vin="VIN1", narrative_text="...")
    extraction = FNOLExtraction(facts=_FACTS, confidence=_CONFIDENCE)

    result = ExtractionResult(request=request, extraction=extraction)

    assert result.request.policy_number == "POL-OH-0001"
    assert result.extraction.facts.location == "Elm Street, Columbus, OH"


def test_coverage_outcome_wraps_determination():
    determination = CoverageDetermination(
        determination="approve", rationale="...", citations=["c1"]
        )

    outcome = CoverageOutcome(policy_number="POL-OH-0001", determination=determination)

    assert outcome.determination.determination == "approve"


def test_fraud_outcome_wraps_assessment():
    assessment = FraudRiskAssessment(
        risk_score=10, risk_tier="low", red_flags=[], rationale="clean"
    )

    outcome = FraudOutcome(policy_number="POL-OH-0001", assessment=assessment)

    assert outcome.assessment.risk_tier == "low"


def test_clarification_request_carries_reason_and_extraction():
    extraction = FNOLExtraction(facts=_FACTS, confidence=_CONFIDENCE)

    request = ClarificationRequest(
        policy_number="POL-OH-0001",
        reason="low-confidence fields: injuries",
        low_confidence_fields=["injuries"],
        missing_required_fields=[],
        extraction=extraction,
    )

    assert request.low_confidence_fields == ["injuries"]
    assert request.extraction.facts.location == "Elm Street, Columbus, OH"
