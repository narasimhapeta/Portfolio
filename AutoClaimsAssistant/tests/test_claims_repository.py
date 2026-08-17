# tests/test_claims_repository.py
from __future__ import annotations

import uuid

import pytest

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.claims_repository import (
    create_clarification_claim,
    create_completed_claim,
    create_failed_claim,
    get_claim_by_id,
)
from claims_assistant.database import create_all_tables, get_session_factory
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest

pytestmark = pytest.mark.integration

_REQUEST = ClaimIntakeRequest(
    policy_number="POL-CA-0003",
    vin="1C4RJFBG5FC123458",
    narrative_text="Hail damage to my Jeep overnight during a storm.",
)


@pytest.mark.asyncio
async def test_create_completed_claim_persists_and_round_trips():
    await create_all_tables()
    recommendation = ClaimRecommendation(
        policy_number="POL-CA-0003",
        coverage_determination="approve",
        coverage_rationale="clause X covers this",
        coverage_citations=["c1"],
        fraud_risk_score=10,
        fraud_risk_tier="low",
        fraud_red_flags=[],
        fraud_rationale="clean",
        narrative_summary="Hail damage, covered, low risk.",
        recommended_next_step="Approve and close.",
    )
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_completed_claim(session, _REQUEST, recommendation)

    assert claim.id is not None
    assert claim.status == "completed"
    assert claim.policy_number == "POL-CA-0003"
    assert claim.recommendation == recommendation.model_dump(mode="json")
    assert claim.clarification is None
    assert claim.error_message is None

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, claim.id)

    assert fetched is not None
    assert fetched.status == "completed"
    assert fetched.recommendation is not None
    assert fetched.recommendation["coverage_determination"] == "approve"


@pytest.mark.asyncio
async def test_create_clarification_claim_persists_and_round_trips():
    await create_all_tables()
    clarification = ClarificationRequest(
        policy_number="POL-CA-0003",
        reason="low-confidence fields: injuries",
        low_confidence_fields=["injuries"],
        missing_required_fields=[],
        extraction=FNOLExtraction(
            facts=FNOLFacts(
                incident_datetime="2026-07-09T17:15",
                location="Elm Street, Columbus, OH",
                parties=[Party(role="policyholder", name="Priya Natarajan")],
                vehicles=[
                    VehicleInfo(role="policyholder_vehicle", description="Jeep Grand Cherokee")
                ],
                injuries=False,
                narrative_summary="Hail damage.",
            ),
            confidence=FieldConfidence(
                incident_datetime=0.9,
                location=0.9,
                parties=0.9,
                vehicles=0.9,
                injuries=0.3,
                narrative_summary=0.9,
            ),
        ),
    )
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_clarification_claim(session, _REQUEST, clarification)

    assert claim.status == "needs_clarification"
    assert claim.recommendation is None
    assert claim.clarification is not None
    assert claim.clarification["reason"] == "low-confidence fields: injuries"

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, claim.id)

    assert fetched is not None
    assert fetched.clarification is not None
    assert fetched.clarification["low_confidence_fields"] == ["injuries"]


@pytest.mark.asyncio
async def test_create_failed_claim_persists_error_message():
    await create_all_tables()
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_failed_claim(
            session, _REQUEST, "policy lookup failed for policy_number='POL-CA-0003'"
        )

    assert claim.status == "failed"
    assert claim.recommendation is None
    assert claim.clarification is None
    assert claim.error_message == "policy lookup failed for policy_number='POL-CA-0003'"


@pytest.mark.asyncio
async def test_get_claim_by_id_returns_none_for_unknown_id():
    await create_all_tables()
    session_factory = get_session_factory()

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, uuid.uuid4())

    assert fetched is None
