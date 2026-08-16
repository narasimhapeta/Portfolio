# tests/test_extraction_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.extraction_eval import run_extraction_eval
from claims_assistant.eval_fixtures import load_extraction_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_extraction_eval_returns_one_result_per_fixture():
    settings = get_settings()
    agent = build_extraction_agent(settings)
    fixtures = load_extraction_fixtures()

    results = await run_extraction_eval(agent, fixtures)

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "extraction"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score is None
        assert result.composite_score == result.correctness_score
        assert result.primary_judge_grounded is None
        assert result.judge_disagreement is False
