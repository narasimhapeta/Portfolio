# src/claims_assistant/agents/coverage_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel


class CoverageDetermination(BaseModel):
    determination: Literal["approve", "deny", "needs_info"]
    rationale: str
    citations: list[str]
