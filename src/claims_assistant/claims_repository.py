# src/claims_assistant/claims_repository.py
from __future__ import annotations

import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.models import Claim
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest


async def create_completed_claim(
    session: AsyncSession, request: ClaimIntakeRequest, recommendation: ClaimRecommendation
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="completed",
        recommendation=recommendation.model_dump(mode="json"),
    )
    async with session.begin():
        session.add(claim)
    return claim


async def create_clarification_claim(
    session: AsyncSession, request: ClaimIntakeRequest, clarification: ClarificationRequest
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="needs_clarification",
        clarification=clarification.model_dump(mode="json"),
    )
    async with session.begin():
        session.add(claim)
    return claim


async def create_failed_claim(
    session: AsyncSession, request: ClaimIntakeRequest, error_message: str
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="failed",
        error_message=error_message,
    )
    async with session.begin():
        session.add(claim)
    return claim


async def get_claim_by_id(session: AsyncSession, claim_id: uuid.UUID) -> Claim | None:
    return await session.get(Claim, claim_id)
