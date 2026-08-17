# src/claims_assistant/workflow/graph.py
from __future__ import annotations

from agent_framework import Case, Default, Workflow, WorkflowBuilder

from claims_assistant.agents.adjuster_summary_agent import build_adjuster_summary_agent
from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import Settings, get_settings
from claims_assistant.workflow.executors import (
    AdjusterSummaryExecutor,
    ClarificationExecutor,
    CoverageExecutor,
    ExtractionExecutor,
    FanOutGateExecutor,
    FraudRiskExecutor,
)
from claims_assistant.workflow.supervisor import is_extraction_sufficient


def build_claim_intake_workflow(settings: Settings) -> Workflow:
    extraction = ExtractionExecutor(build_extraction_agent(settings))
    clarification = ClarificationExecutor()
    fan_out_gate = FanOutGateExecutor()
    coverage = CoverageExecutor(build_coverage_agent(settings), settings)
    fraud_risk = FraudRiskExecutor(build_fraud_agent(settings), settings)
    adjuster_summary = AdjusterSummaryExecutor(build_adjuster_summary_agent(settings))

    return (
        WorkflowBuilder(start_executor=extraction)
        .add_switch_case_edge_group(
            extraction,
            [
                # NOTE: SwitchCaseEdgeGroup catches any exception this condition raises and
                # falls through toward Default (see Global Constraints) -- an exception here
                # would silently route to the "sufficient confidence" happy path instead of
                # clarification, the opposite of spec §8's fail-explicit intent. Safe today
                # because is_extraction_sufficient only reads already-Pydantic-validated
                # FieldConfidence/FNOLFacts fields and can't raise for a well-formed
                # ExtractionResult -- keep it that way if this condition is ever touched.
                Case(
                    condition=lambda result: not is_extraction_sufficient(result.extraction),
                    target=clarification,
                ),
                Default(target=fan_out_gate),
            ],
        )
        .add_fan_out_edges(fan_out_gate, [coverage, fraud_risk])
        .add_fan_in_edges([coverage, fraud_risk], adjuster_summary)
        .build()
    )

def get_claim_intake_workflow() -> Workflow:
    return build_claim_intake_workflow(get_settings())
