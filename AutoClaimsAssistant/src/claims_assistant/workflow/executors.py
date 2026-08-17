# src/claims_assistant/workflow/executors.py
from __future__ import annotations

import datetime
from typing import Never

from agent_framework import Agent, Executor, WorkflowContext, handler

from claims_assistant.agents.adjuster_summary_agent import summarize_for_adjuster
from claims_assistant.agents.adjuster_summary_schema import (
    ClaimRecommendation,
    assemble_claim_recommendation,
)
from claims_assistant.agents.coverage_agent import determine_coverage
from claims_assistant.agents.extraction_agent import extract_fnol_facts
from claims_assistant.agents.fraud_agent import assess_fraud_risk
from claims_assistant.config import Settings
from claims_assistant.workflow.messages import (
    ClaimIntakeRequest,
    ClarificationRequest,
    CoverageOutcome,
    ExtractionResult,
    FraudOutcome,
)
from claims_assistant.workflow.supervisor import (
    identify_low_confidence_fields,
    identify_missing_required_fields,
)


class ExtractionExecutor(Executor):
    def __init__(self, agent: Agent, *, id: str = "extraction") -> None:
        super().__init__(id=id)
        self._agent = agent

    @handler
    async def run(
        self, message: ClaimIntakeRequest, ctx: WorkflowContext[ExtractionResult]
    ) -> None:
        extraction = await extract_fnol_facts(self._agent, message.narrative_text)
        await ctx.send_message(ExtractionResult(request=message, extraction=extraction))


class ClarificationExecutor(Executor):
    def __init__(self, *, id: str = "clarification") -> None:
        super().__init__(id=id)

    @handler
    async def run(
        self, message: ExtractionResult, ctx: WorkflowContext[Never, ClarificationRequest]
    ) -> None:
        low_confidence = identify_low_confidence_fields(message.extraction.confidence)
        missing = identify_missing_required_fields(message.extraction.facts)
        reasons = []
        if low_confidence:
            reasons.append(f"low-confidence fields: {', '.join(low_confidence)}")
        if missing:
            reasons.append(f"missing required fields: {', '.join(missing)}")
        await ctx.yield_output(
            ClarificationRequest(
                policy_number=message.request.policy_number,
                reason="; ".join(reasons),
                low_confidence_fields=low_confidence,
                missing_required_fields=missing,
                extraction=message.extraction,
            )
        )


class FanOutGateExecutor(Executor):
    """Trivial pass-through. Exists only because add_switch_case_edge_group's Case/Default
    targets are single-dispatch — the "sufficient confidence" branch needs to reach two
    executors (Coverage + Fraud-Risk), so it lands here first and this re-sends the same
    message via a separate add_fan_out_edges call (see graph.py)."""

    def __init__(self, *, id: str = "fan_out_gate") -> None:
        super().__init__(id=id)

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[ExtractionResult]) -> None:
        await ctx.send_message(message)


class CoverageExecutor(Executor):
    def __init__(self, agent: Agent, settings: Settings, *, id: str = "coverage") -> None:
        super().__init__(id=id)
        self._agent = agent
        self._settings = settings

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[CoverageOutcome]) -> None:
        determination = await determine_coverage(
            self._agent,
            self._settings,
            message.request.policy_number,
            message.request.narrative_text,
        )
        await ctx.send_message(
            CoverageOutcome(
                policy_number=message.request.policy_number, determination=determination
                )
        )


def _incident_date(incident_datetime: str) -> str:
    """Extract the YYYY-MM-DD date portion fraud_signals.compute_fraud_signals expects
    from FNOLFacts.incident_datetime. Raises a clear, immediately-diagnosable error if the
    extraction produced something that doesn't start with a parseable ISO date, instead of
    letting date.fromisoformat fail confusingly deep inside compute_fraud_signals."""
    candidate = incident_datetime[:10]
    try:
        datetime.date.fromisoformat(candidate)
    except ValueError as exc:
        raise ValueError(
            f"extraction produced a non-ISO-date incident_datetime: {incident_datetime!r}"
        ) from exc
    return candidate


class FraudRiskExecutor(Executor):
    def __init__(self, agent: Agent, settings: Settings, *, id: str = "fraud_risk") -> None:
        super().__init__(id=id)
        self._agent = agent
        self._settings = settings

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[FraudOutcome]) -> None:
        incident_date = _incident_date(message.extraction.facts.incident_datetime)
        assessment = await assess_fraud_risk(
            self._agent,
            self._settings,
            message.request.policy_number,
            message.request.vin,
            incident_date,
            message.request.narrative_text,
        )
        await ctx.send_message(
            FraudOutcome(policy_number=message.request.policy_number, assessment=assessment)
        )


class AdjusterSummaryExecutor(Executor):
    def __init__(self, agent: Agent, *, id: str = "adjuster_summary") -> None:
        super().__init__(id=id)
        self._agent = agent

    @handler
    async def run(
        self,
        message: list[CoverageOutcome | FraudOutcome],
        ctx: WorkflowContext[Never, ClaimRecommendation],
    ) -> None:
        coverage_outcome = next(m for m in message if isinstance(m, CoverageOutcome))
        fraud_outcome = next(m for m in message if isinstance(m, FraudOutcome))
        summary = await summarize_for_adjuster(
            self._agent,
            coverage_outcome.policy_number,
            coverage_outcome.determination,
            fraud_outcome.assessment,
        )
        await ctx.yield_output(
            assemble_claim_recommendation(
                coverage_outcome.policy_number,
                coverage_outcome.determination,
                fraud_outcome.assessment,
                summary,
            )
        )
