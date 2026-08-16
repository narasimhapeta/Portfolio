# tests/test_eval_report.py
from __future__ import annotations

from claims_assistant.eval.report import build_eval_report, summarize_by_agent
from claims_assistant.eval.results import EvalResult

_RESULTS = [
    EvalResult(
        agent="extraction",
        fixture_id="fnol_001",
        correctness_score=1.0,
        grounding_score=None,
        composite_score=1.0,
        primary_judge_grounded=None,
        secondary_judge_grounded=None,
        judge_disagreement=False,
    ),
    EvalResult(
        agent="coverage",
        fixture_id="cov_001",
        correctness_score=1.0,
        grounding_score=1.0,
        composite_score=1.0,
        primary_judge_grounded=True,
        secondary_judge_grounded=True,
        judge_disagreement=False,
    ),
    EvalResult(
        agent="coverage",
        fixture_id="cov_002",
        correctness_score=0.0,
        grounding_score=1.0,
        composite_score=0.5,
        primary_judge_grounded=True,
        secondary_judge_grounded=False,
        judge_disagreement=True,
    ),
]


def test_build_eval_report_has_one_row_per_result():
    report = build_eval_report(_RESULTS)

    assert len(report) == 3
    assert list(report.columns) == [
        "agent",
        "fixture_id",
        "correctness_score",
        "grounding_score",
        "composite_score",
        "primary_judge_grounded",
        "secondary_judge_grounded",
        "judge_disagreement",
    ]


def test_summarize_by_agent_averages_composite_score_per_agent():
    report = build_eval_report(_RESULTS)

    summary = summarize_by_agent(report)

    assert list(summary.columns) == ["agent", "mean_score"]
    coverage_row = summary[summary["agent"] == "coverage"].iloc[0]
    assert coverage_row["mean_score"] == 0.75
    extraction_row = summary[summary["agent"] == "extraction"].iloc[0]
    assert extraction_row["mean_score"] == 1.0
