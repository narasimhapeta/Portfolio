# src/claims_assistant/api/claims.py
from __future__ import annotations

import uuid
from typing import Annotated

from agent_framework import Workflow
from fastapi import APIRouter, Depends, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.api.claims_schema import ClaimResponse, claim_response_from_model
from claims_assistant.claims_repository import (
    add_document_url,
    create_clarification_claim,
    create_completed_claim,
    create_failed_claim,
    get_claim_by_id,
)
from claims_assistant.config import Settings, get_settings
from claims_assistant.database import get_db_session
from claims_assistant.storage.blob import upload_claim_document
from claims_assistant.workflow.graph import get_claim_intake_workflow
from claims_assistant.workflow.messages import ClaimIntakeRequest

SettingsDep = Annotated[Settings, Depends(get_settings)]


router = APIRouter()

WorkflowDep = Annotated[Workflow, Depends(get_claim_intake_workflow)]
SessionDep = Annotated[AsyncSession, Depends(get_db_session)]


@router.post(
    "/claims",
    status_code=201,
    response_model=ClaimResponse,
    responses={
        502: {"model": ClaimResponse, "description": "Claim intake pipeline failed"},
    },
)
async def submit_claim(
    intake: ClaimIntakeRequest, workflow: WorkflowDep, session: SessionDep
) -> ClaimResponse | JSONResponse:
    try:
        result = await workflow.run(intake)
    except Exception as exc:
        claim = await create_failed_claim(session, intake, str(exc))
        return JSONResponse(
            status_code=502,
            content=claim_response_from_model(claim).model_dump(mode="json"),
        )

    outputs = result.get_outputs()
    # Phase 6's graph always yields exactly one terminal output (either branch ends in a
    # single ctx.yield_output call) -- this is a defensive check against a graph-wiring
    # regression, not an expected runtime failure mode, so it stays outside the try/except
    # above: an operational MCP/lookup failure gets persisted as a `failed` claim (spec
    # §8), but a wiring bug that produced zero/multiple outputs is a genuine server defect
    # and should surface as a loud, diagnosable error instead of a misleading claim record.
    assert len(outputs) == 1, f"expected exactly one terminal workflow output, got {len(outputs)}"
    outcome = outputs[0]
    if isinstance(outcome, ClaimRecommendation):
        claim = await create_completed_claim(session, intake, outcome)
    else:
        claim = await create_clarification_claim(session, intake, outcome)
    return claim_response_from_model(claim)


@router.get("/claims/{claim_id}", response_model=ClaimResponse)
async def get_claim(claim_id: uuid.UUID, session: SessionDep) -> ClaimResponse:
    claim = await get_claim_by_id(session, claim_id)
    if claim is None:
        raise HTTPException(status_code=404, detail=f"claim {claim_id} not found")
    return claim_response_from_model(claim)


@router.post("/claims/{claim_id}/documents", response_model=ClaimResponse)
async def upload_document(
    claim_id: uuid.UUID, file: UploadFile, session: SessionDep, settings: SettingsDep
) -> ClaimResponse:
    claim = await get_claim_by_id(session, claim_id)
    if claim is None:
        raise HTTPException(status_code=404, detail=f"claim {claim_id} not found")
    content = await file.read()
    filename = file.filename or "document"
    url = await upload_claim_document(
        settings, claim_id, filename, content, file.content_type or "application/octet-stream"
    )
    claim = await add_document_url(session, claim, url)
    return claim_response_from_model(claim)
