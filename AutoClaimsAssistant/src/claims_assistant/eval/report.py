# src/claims_assistant/eval/report.py
from __future__ import annotations

import pandas as pd

from claims_assistant.eval.results import EvalResult


def build_eval_report(results: list[EvalResult]) -> pd.DataFrame:
    return pd.DataFrame(
        [
            {
                "agent": r.agent,
                "fixture_id": r.fixture_id,
                "correctness_score": r.correctness_score,
                "grounding_score": r.grounding_score,
                "composite_score": r.composite_score,
                "primary_judge_grounded": r.primary_judge_grounded,
                "secondary_judge_grounded": r.secondary_judge_grounded,
                "judge_disagreement": r.judge_disagreement,
            }
            for r in results
        ]
    )


def summarize_by_agent(report: pd.DataFrame) -> pd.DataFrame:
    return report.groupby("agent")["composite_score"].mean().reset_index(name="mean_score")
