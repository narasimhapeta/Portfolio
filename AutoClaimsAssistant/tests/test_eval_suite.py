# tests/test_eval_suite.py
from __future__ import annotations

import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.baselines import BASELINES
from claims_assistant.eval.coverage_eval import run_coverage_eval
from claims_assistant.eval.extraction_eval import run_extraction_eval
from claims_assistant.eval.fraud_eval import run_fraud_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval.report import build_eval_report, summarize_by_agent
from claims_assistant.eval_fixtures import (
    load_coverage_fixtures,
    load_extraction_fixtures,
    load_fraud_fixtures,
)

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_eval_suite_produces_report_above_baseline(seeded_db):
    settings = get_settings()
    judge_primary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_primary_deployment
    )
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )

    extraction_results = await run_extraction_eval(
        build_extraction_agent(settings), load_extraction_fixtures()
    )
    coverage_results = await run_coverage_eval(
        build_coverage_agent(settings),
        judge_primary,
        judge_secondary,
        settings,
        load_coverage_fixtures(),
    )
    fraud_results = await run_fraud_eval(
        build_fraud_agent(settings), judge_primary, judge_secondary, load_fraud_fixtures()
    )

    report = build_eval_report(extraction_results + coverage_results + fraud_results)
    summary = summarize_by_agent(report)
    print("\n" + summary.to_string(index=False))
    disagreements = report[report["judge_disagreement"]]
    if len(disagreements):
        print("\nJudge disagreements:\n" + disagreements.to_string(index=False))

    for agent, baseline in BASELINES.items():
        mean_score = summary.loc[summary["agent"] == agent, "mean_score"].iloc[0]
        assert mean_score >= baseline, (
            f"{agent} mean score {mean_score:.2f} dropped below baseline {baseline}"
        )
