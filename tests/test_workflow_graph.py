# tests/test_workflow_graph.py
import pytest
from agent_framework import Workflow

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.config import Settings, get_settings
from claims_assistant.workflow.graph import build_claim_intake_workflow, get_claim_intake_workflow
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest

_TEST_SETTINGS = Settings(
    azure_openai_endpoint="https://example.openai.azure.com",
    azure_openai_api_key="test-key",
    azure_openai_chat_deployment="test-extraction-deployment",
    azure_openai_coverage_deployment="test-coverage-deployment",
    azure_openai_fraud_deployment="test-fraud-deployment",
    azure_openai_adjuster_summary_deployment="test-adjuster-summary-deployment",
)


def test_build_claim_intake_workflow_builds_without_error():
    workflow = build_claim_intake_workflow(_TEST_SETTINGS)

    assert isinstance(workflow, Workflow)


@pytest.mark.integration
@pytest.mark.asyncio
async def test_workflow_produces_claim_recommendation_for_normal_claim(seeded_db):
    workflow = build_claim_intake_workflow(get_settings())
    request = ClaimIntakeRequest(
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
                narrative_text=(
            "On March 10, 2026, I (Priya Natarajan) discovered hail damage to my Jeep "
            "Grand Cherokee, which had been parked outside my home overnight during a "
            "storm in Fresno, CA. No one was hurt; I was not in the vehicle at the time."
        ),
    )

    result = await workflow.run(request)

    outputs = result.get_outputs()
    assert len(outputs) == 1
    assert isinstance(outputs[0], ClaimRecommendation)
    assert outputs[0].policy_number == "POL-CA-0003"
    assert outputs[0].coverage_determination in ("approve", "deny", "needs_info")
    assert outputs[0].fraud_risk_tier in ("low", "medium", "high")
    assert outputs[0].narrative_summary
    assert outputs[0].recommended_next_step


@pytest.mark.integration
@pytest.mark.asyncio
async def test_workflow_routes_low_confidence_extraction_to_clarification(seeded_db):
    workflow = build_claim_intake_workflow(get_settings())
    request = ClaimIntakeRequest(
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text=(
            "Something happened to my car at some point, not totally sure when or "
            "where, might have been another vehicle involved, might not have been. "
            "Not sure if anyone got hurt."
        ),
    )

    result = await workflow.run(request)

    outputs = result.get_outputs()
    assert len(outputs) == 1
    assert isinstance(outputs[0], ClarificationRequest)
    assert outputs[0].policy_number == "POL-CA-0003"
    assert outputs[0].reason

@pytest.mark.integration
def test_get_claim_intake_workflow_returns_a_fresh_instance_each_call():
    # agent_framework's Workflow is stateful and single-run-at-a-time by the SDK's own
    # contract (docstring: "To execute multiple independent runs, create separate
    # Workflow instances via WorkflowBuilder"; run() raises WorkflowException if called
    # while a prior run on the same instance is still active). get_claim_intake_workflow
    # must never cache/reuse a single Workflow across calls, or two overlapping
    # POST /claims requests (Phase 7) would race inside that guard.
    workflow_a = get_claim_intake_workflow()
    workflow_b = get_claim_intake_workflow()

    assert workflow_a is not workflow_b
