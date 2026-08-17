# tests/test_supervisor.py
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.supervisor import (
    CONFIDENCE_THRESHOLD,
    identify_low_confidence_fields,
    identify_missing_required_fields,
    is_extraction_sufficient,
)

_SUFFICIENT_CONFIDENCE = FieldConfidence(
    incident_datetime=0.95,
    location=0.9,
    parties=0.85,
    vehicles=0.85,
    injuries=0.8,
    narrative_summary=0.9,
)
_COMPLETE_FACTS = FNOLFacts(
    incident_datetime="2026-07-09T17:15",
    location="Elm Street, Columbus, OH",
    parties=[Party(role="policyholder", name="Harold Bennett")],
    vehicles=[VehicleInfo(role="policyholder_vehicle", description="Chevrolet Equinox")],
    injuries=False,
    narrative_summary="Rear-ended while stopped for a pedestrian.",
)


def test_identify_low_confidence_fields_returns_empty_when_all_above_threshold():
    assert identify_low_confidence_fields(_SUFFICIENT_CONFIDENCE) == []


def test_identify_low_confidence_fields_flags_fields_below_threshold():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"injuries": 0.4, "location": 0.5})

    flagged = identify_low_confidence_fields(confidence)

    assert set(flagged) == {"injuries", "location"}


def test_identify_low_confidence_fields_boundary_is_not_flagged():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"injuries": CONFIDENCE_THRESHOLD})

    assert identify_low_confidence_fields(confidence) == []


def test_identify_missing_required_fields_returns_empty_when_complete():
    assert identify_missing_required_fields(_COMPLETE_FACTS) == []


def test_identify_missing_required_fields_flags_empty_parties_and_vehicles():
    facts = _COMPLETE_FACTS.model_copy(update={"parties": [], "vehicles": []})

    missing = identify_missing_required_fields(facts)

    assert set(missing) == {"parties", "vehicles"}


def test_is_extraction_sufficient_true_for_complete_high_confidence_extraction():
    extraction = FNOLExtraction(facts=_COMPLETE_FACTS, confidence=_SUFFICIENT_CONFIDENCE)

    assert is_extraction_sufficient(extraction) is True


def test_is_extraction_sufficient_false_for_low_confidence():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"narrative_summary": 0.2})
    extraction = FNOLExtraction(facts=_COMPLETE_FACTS, confidence=confidence)

    assert is_extraction_sufficient(extraction) is False


def test_is_extraction_sufficient_false_for_missing_required_fields():
    facts = _COMPLETE_FACTS.model_copy(update={"vehicles": []})
    extraction = FNOLExtraction(facts=facts, confidence=_SUFFICIENT_CONFIDENCE)

    assert is_extraction_sufficient(extraction) is False
