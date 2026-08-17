# tests/test_extraction_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def _sample_facts() -> FNOLFacts:
    return FNOLFacts(
        incident_datetime="2026-02-03T21:00",
        location="4th Ave, Brooklyn, NY",
        parties=[Party(role="policyholder", name="Linda Park")],
        vehicles=[VehicleInfo(role="policyholder_vehicle", description="Toyota Corolla")],
        injuries=False,
        narrative_summary="Parked car struck by a hit-and-run driver.",
    )


def test_fnol_extraction_validates_with_facts_and_confidence():
    extraction = FNOLExtraction(
        facts=_sample_facts(),
        confidence=FieldConfidence(
            incident_datetime=0.9,
            location=0.85,
            parties=0.8,
            vehicles=0.8,
            injuries=0.95,
            narrative_summary=0.9,
        ),
    )

    assert extraction.facts.location == "4th Ave, Brooklyn, NY"
    assert extraction.confidence.injuries == 0.95


def test_field_confidence_rejects_out_of_range_values():
    with pytest.raises(ValidationError):
        FieldConfidence(
            incident_datetime=1.5,
            location=0.5,
            parties=0.5,
            vehicles=0.5,
            injuries=0.5,
            narrative_summary=0.5,
        )
