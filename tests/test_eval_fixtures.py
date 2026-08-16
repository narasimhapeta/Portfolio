# tests/test_eval_fixtures.py
from claims_assistant.eval_fixtures import load_extraction_fixtures
from claims_assistant.fnol_schema import FNOLFacts


def test_load_extraction_fixtures_returns_all_fixtures():
    fixtures = load_extraction_fixtures()

    assert len(fixtures) == 10
    ids = {f.fixture_id for f in fixtures}
    assert len(ids) == 10
    for fixture in fixtures:
        assert fixture.narrative_text
        assert isinstance(fixture.gold, FNOLFacts)


def test_hit_and_run_fixture_has_no_named_other_driver():
    fixtures = {f.fixture_id: f for f in load_extraction_fixtures()}
    fixture = fixtures["fnol_003_hit_and_run"]

    assert fixture.gold.injuries is False
    assert all(p.role != "other_driver" for p in fixture.gold.parties)


def test_ambiguous_injury_fixture_marks_injuries_true():
    fixtures = {f.fixture_id: f for f in load_extraction_fixtures()}
    fixture = fixtures["fnol_005_ambiguous_injury"]

    assert fixture.gold.injuries is True
    assert fixture.gold.injury_description is not None

def test_load_coverage_fixtures_returns_all_fixtures():
    from claims_assistant.eval_fixtures import load_coverage_fixtures

    fixtures = load_coverage_fixtures()

    assert len(fixtures) == 5
    ids = {f.fixture_id for f in fixtures}
    assert len(ids) == 5
    determinations = {f.gold_determination for f in fixtures}
    assert determinations == {"approve", "deny", "needs_info"}
    for fixture in fixtures:
        assert fixture.policy_number
        assert fixture.claim_narrative
        assert fixture.gold_citation

