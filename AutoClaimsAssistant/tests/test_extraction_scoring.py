# tests/test_extraction_scoring.py
from claims_assistant.agents.extraction_scoring import score_extraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def _facts(**overrides: object) -> FNOLFacts:
    defaults: dict[str, object] = {
        "incident_datetime": "2026-02-03T21:00",
        "location": "4th Ave, Brooklyn, NY",
        "parties": [Party(role="policyholder", name="Linda Park")],
        "vehicles": [VehicleInfo(role="policyholder_vehicle", description="Toyota Corolla")],
        "injuries": False,
        "narrative_summary": "Parked car struck by a hit-and-run driver.",
    }
    defaults.update(overrides)
    return FNOLFacts(**defaults)  # type: ignore[arg-type]


def test_identical_facts_score_one():
    facts = _facts()

    assert score_extraction(facts, facts) == 1.0


def test_location_match_is_case_insensitive():
    predicted = _facts(location="4TH AVE, BROOKLYN, NY")
    gold = _facts(location="4th Ave, Brooklyn, NY")

    assert score_extraction(predicted, gold) == 1.0


def test_mismatched_injuries_lowers_score():
    predicted = _facts(injuries=True, injury_description="minor bruise")
    gold = _facts(injuries=False)

    assert score_extraction(predicted, gold) == 0.8  # 4 of 5 checks match


def test_completely_different_facts_score_zero():
    predicted = _facts(
        incident_datetime="2025-01-01T00:00",
        location="Nowhere",
        parties=[Party(role="other_driver", name="Nobody")],
        vehicles=[VehicleInfo(role="other_vehicle", description="unknown")],
        injuries=True,
    )
    gold = _facts()

    assert score_extraction(predicted, gold) == 0.0
