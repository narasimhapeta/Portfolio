# tests/test_pandas_setup.py
from __future__ import annotations

import pandas as pd


def test_dataframe_constructs_from_list_of_dicts():
    rows: list[dict[str, object]] = [
        {"agent": "coverage", "composite_score": 0.9},
        {"agent": "coverage", "composite_score": 0.8},
        {"agent": "fraud", "composite_score": 1.0},
    ]

    df = pd.DataFrame(rows)

    assert list(df.columns) == ["agent", "composite_score"]
    assert len(df) == 3


def test_groupby_mean_reset_index_produces_named_summary_column():
    df = pd.DataFrame(
        [
            {"agent": "coverage", "composite_score": 0.9},
            {"agent": "coverage", "composite_score": 0.7},
        ]
    )

    summary = df.groupby("agent")["composite_score"].mean().reset_index(name="mean_score")

    assert list(summary.columns) == ["agent", "mean_score"]
    assert summary["mean_score"].iloc[0] == 0.8


def test_to_string_returns_a_str():
    df = pd.DataFrame([{"agent": "coverage", "composite_score": 0.9}])

    text = df.to_string(index=False)

    assert isinstance(text, str)
    assert "coverage" in text
