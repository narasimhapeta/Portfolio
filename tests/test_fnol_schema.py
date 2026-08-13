# tests/test_fnol_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def test_fnol_facts_valid_construction():
    facts = FNOLFacts(
        incident_datetime="2025-09-14T17:30",
        location="Elm Street, Sacramento, CA",
        parties=[
            Party(role="policyholder", name="Maria Gonzalez"),
            Party(role="other_driver", name="Kevin Ortiz", contact="916-555-0142"),
        ],
        vehicles=[
            VehicleInfo(role="policyholder_vehicle", description="Ford Focus"),
            VehicleInfo(role="other_vehicle", description="blue Honda Civic"),
        ],
        injuries=False,
        narrative_summary="Policyholder rear-ended another vehicle stopped at a red light.",
    )

    assert facts.injuries is False
    assert facts.parties[0].role == "policyholder"
    assert facts.vehicles[1].vin is None


def test_fnol_facts_requires_narrative_summary():
    with pytest.raises(ValidationError):
        FNOLFacts(
            incident_datetime="2025-09-14T17:30",
            location="Elm Street, Sacramento, CA",
            parties=[],
            vehicles=[],
            injuries=False,
        )
