# src/claims_assistant/api/claims_schema.py
from __future__ import annotations

import datetime
import uuid
from typing import Literal, cast

from pydantic import BaseModel

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.models import Claim
from claims_assistant.workflow.messages import ClarificationRequest

ClaimStatus = Literal["completed", "needs_clarification", "failed"]


class ClaimResponse(BaseModel):
    id: uuid.UUID
    policy_number: str
    vin: str
    narrative_text: str
    status: ClaimStatus
    created_at: datetime.datetime
    recommendation: ClaimRecommendation | None = None
    clarification: ClarificationRequest | None = None
    error: str | None = None
    document_urls: list[str] | None = None



def claim_response_from_model(claim: Claim) -> ClaimResponse:
    return ClaimResponse(
        id=claim.id,
        policy_number=claim.policy_number,
        vin=claim.vin,
        narrative_text=claim.narrative_text,
        status=cast(ClaimStatus, claim.status),
        created_at=claim.created_at,
        recommendation=(
            ClaimRecommendation.model_validate(claim.recommendation)
            if claim.recommendation is not None
            else None
        ),
        clarification=(
            ClarificationRequest.model_validate(claim.clarification)
            if claim.clarification is not None
            else None
        ),
        error=claim.error_message,
        document_urls=claim.document_urls,

    )


class ClaimListResponse(BaseModel):
    claims: list[ClaimResponse]
