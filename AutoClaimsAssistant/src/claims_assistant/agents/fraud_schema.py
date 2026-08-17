# src/claims_assistant/agents/fraud_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field

from claims_assistant.agents.fraud_signals import RedFlagCode


class FraudRiskAssessment(BaseModel):
    risk_score: int = Field(ge=0, le=100)
    risk_tier: Literal["low", "medium", "high"]
    red_flags: list[RedFlagCode]
    rationale: str
