# tests/test_workflow_graph.py
from agent_framework import Workflow

from claims_assistant.config import Settings
from claims_assistant.workflow.graph import build_claim_intake_workflow

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
