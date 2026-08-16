# tests/test_claims_api.py
from __future__ import annotations

import uuid

import pytest
from httpx import ASGITransport, AsyncClient

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.database import create_all_tables
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.main import create_app
from claims_assistant.workflow.graph import get_claim_intake_workflow
from claims_assistant.workflow.messages import ClarificationRequest

pytestmark = pytest.mark.integration

_REQUEST_BODY = {
    "policy_number": "POL-CA-0003",
    "vin": "1C4RJFBG5FC123458",
    "narrative_text": "Hail damage to my Jeep overnight during a storm.",
}

_RECOMMENDATION = ClaimRecommendation(
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

_CLARIFICATION = ClarificationRequest(
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


class _FakeWorkflowResult:
    def __init__(self, outputs: list[object]) -> None:
        self._outputs = outputs

    def get_outputs(self) -> list[object]:
        return self._outputs


class _FakeWorkflow:
    def __init__(
        self, outputs: list[object] | None = None, error: Exception | None = None
    ) -> None:
        self._outputs = outputs or []
        self._error = error

    async def run(self, message: object) -> _FakeWorkflowResult:
        if self._error is not None:
            raise self._error
        return _FakeWorkflowResult(self._outputs)


def _client_with_fake_workflow(fake_workflow: _FakeWorkflow) -> AsyncClient:
    app = create_app()
    app.dependency_overrides[get_claim_intake_workflow] = lambda: fake_workflow
    return AsyncClient(transport=ASGITransport(app=app), base_url="http://test")


@pytest.mark.asyncio
async def test_post_claims_returns_201_with_recommendation_for_completed_outcome():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(outputs=[_RECOMMENDATION])

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "completed"
    assert body["recommendation"]["coverage_determination"] == "approve"
    assert body["clarification"] is None
    assert body["error"] is None
    uuid.UUID(body["id"])


@pytest.mark.asyncio
async def test_post_claims_returns_201_with_clarification_for_clarification_outcome():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(outputs=[_CLARIFICATION])

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "needs_clarification"
    assert body["recommendation"] is None
    assert body["clarification"]["reason"] == "low-confidence fields: injuries"


@pytest.mark.asyncio
async def test_post_claims_returns_502_and_persists_failed_claim_when_workflow_raises():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(
        error=ValueError("policy lookup failed for policy_number='POL-CA-0003'")
    )

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)
        assert response.status_code == 502
        body = response.json()
        assert body["status"] == "failed"
        assert "policy lookup failed" in body["error"]

        get_response = await client.get(f"/claims/{body['id']}")

    assert get_response.status_code == 200
    assert get_response.json()["status"] == "failed"


@pytest.mark.asyncio
async def test_get_claims_returns_404_for_unknown_id():
    await create_all_tables()
    fake_workflow = _FakeWorkflow()

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.get(f"/claims/{uuid.uuid4()}")

    assert response.status_code == 404


@pytest.mark.asyncio
async def test_post_claims_full_pipeline_returns_recommendation_via_real_http_request(
    seeded_db,
):
    app = create_app()
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post(
            "/claims",
            json={
                "policy_number": "POL-CA-0003",
                "vin": "1C4RJFBG5FC123458",
                "narrative_text": (
                    "On March 10, 2026, I (Priya Natarajan) discovered hail damage to my "
                    "Jeep Grand Cherokee, which had been parked outside my home overnight "
                    "during a storm in Fresno, CA. No one was hurt; I was not in the "
                    "vehicle at the time."
                ),
            },
        )
        assert response.status_code == 201
        body = response.json()
        assert body["status"] == "completed"
        assert body["recommendation"]["coverage_determination"] in (
            "approve",
            "deny",
            "needs_info",
        )
        assert body["recommendation"]["fraud_risk_tier"] in ("low", "medium", "high")
        assert body["recommendation"]["narrative_summary"]
        assert body["recommendation"]["recommended_next_step"]

        get_response = await client.get(f"/claims/{body['id']}")

    assert get_response.status_code == 200
    assert get_response.json()["status"] == "completed"
    assert get_response.json()["recommendation"] == body["recommendation"]


@pytest.mark.asyncio
async def test_post_claims_routes_low_confidence_extraction_to_clarification_via_real_http(
    seeded_db,
):
    app = create_app()
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post(
            "/claims",
            json={
                "policy_number": "POL-CA-0003",
                "vin": "1C4RJFBG5FC123458",
                "narrative_text": (
                    "Something happened to my car at some point, not totally sure when or "
                    "where, might have been another vehicle involved, might not have been. "
                    "Not sure if anyone got hurt."
                ),
            },
        )

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "needs_clarification"
    assert body["recommendation"] is None
    assert body["clarification"]["reason"]
