# src/claims_assistant/agents/adjuster_summary_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import RedFlagCode


class AdjusterSummary(BaseModel):
    """LLM output: prose only. It never restates determination/tier/citations/red-flags —
    those are already-validated facts from Coverage/Fraud, assembled in Python by
    assemble_claim_recommendation(), not re-derived here."""

    narrative_summary: str
    recommended_next_step: str


class ClaimRecommendation(BaseModel):
    policy_number: str
    coverage_determination: Literal["approve", "deny", "needs_info"]
    coverage_rationale: str
    coverage_citations: list[str]
    fraud_risk_score: int
    fraud_risk_tier: Literal["low", "medium", "high"]
    fraud_red_flags: list[RedFlagCode]
    fraud_rationale: str
    narrative_summary: str
    recommended_next_step: str


def assemble_claim_recommendation(
    policy_number: str,
    coverage: CoverageDetermination,
    fraud: FraudRiskAssessment,
    summary: AdjusterSummary,
) -> ClaimRecommendation:
    return ClaimRecommendation(
        policy_number=policy_number,
        coverage_determination=coverage.determination,
        coverage_rationale=coverage.rationale,
        coverage_citations=coverage.citations,
        fraud_risk_score=fraud.risk_score,
        fraud_risk_tier=fraud.risk_tier,
        fraud_red_flags=fraud.red_flags,
        fraud_rationale=fraud.rationale,
        narrative_summary=summary.narrative_summary,
        recommended_next_step=summary.recommended_next_step,
    )
