# tests/test_extraction_eval_fixtures.py
import pytest

from claims_assistant.agents.extraction_agent import build_extraction_agent, extract_fnol_facts
from claims_assistant.agents.extraction_scoring import score_extraction
from claims_assistant.config import get_settings
from claims_assistant.eval_fixtures import load_extraction_fixtures

pytestmark = pytest.mark.integration

FIRST_CUT_SCORE_FLOOR = 0.7


@pytest.mark.asyncio
async def test_extraction_passes_first_cut_of_eval_fixtures():
    agent = build_extraction_agent(get_settings())
    fixtures = load_extraction_fixtures()

    results = []
    for fixture in fixtures:
        extraction = await extract_fnol_facts(agent, fixture.narrative_text)
        score = score_extraction(extraction.facts, fixture.gold)
        results.append((fixture.fixture_id, score))

    mean_score = sum(score for _, score in results) / len(results)

    assert mean_score >= FIRST_CUT_SCORE_FLOOR, (
        f"mean extraction score {mean_score:.2f} below first-cut floor "
        f"{FIRST_CUT_SCORE_FLOOR}; per-fixture scores={results}"
    )
