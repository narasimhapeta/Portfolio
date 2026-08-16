# tests/test_coverage_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.coverage_eval import run_coverage_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval_fixtures import load_coverage_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_coverage_eval_returns_one_result_per_fixture(seeded_db):
    settings = get_settings()
    coverage_agent = build_coverage_agent(settings)
    judge_primary = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )
    fixtures = load_coverage_fixtures()

    results = await run_coverage_eval(
        coverage_agent, judge_primary, judge_secondary, settings, fixtures
    )

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "coverage"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score in (0.0, 1.0)
        assert result.primary_judge_grounded is not None
        assert result.secondary_judge_grounded is not None
        assert isinstance(result.judge_disagreement, bool)
