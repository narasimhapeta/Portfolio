# src/claims_assistant/eval/judge_schema.py
from __future__ import annotations

from pydantic import BaseModel


class GroundingJudgment(BaseModel):
    grounded: bool
    reasoning: str
