# src/claims_assistant/eval/results.py
from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

AgentName = Literal["extraction", "coverage", "fraud"]


@dataclass(frozen=True)
class EvalResult:
    agent: AgentName
    fixture_id: str
    correctness_score: float
    grounding_score: float | None
    composite_score: float
    primary_judge_grounded: bool | None
    secondary_judge_grounded: bool | None
    judge_disagreement: bool


def compute_composite_score(correctness: float, grounding: float | None) -> float:
    scores = [correctness] if grounding is None else [correctness, grounding]
    return sum(scores) / len(scores)
