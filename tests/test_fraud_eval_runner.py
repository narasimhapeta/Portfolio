# tests/test_fraud_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.fraud_eval import run_fraud_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval_fixtures import load_fraud_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_fraud_eval_returns_one_result_per_fixture(seeded_db):
    settings = get_settings()
    fraud_agent = build_fraud_agent(settings)
    judge_primary = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )
    fixtures = load_fraud_fixtures()

    results = await run_fraud_eval(fraud_agent, judge_primary, judge_secondary, fixtures)

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "fraud"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score in (0.0, 1.0)
        assert result.primary_judge_grounded is not None
        assert result.secondary_judge_grounded is not None
