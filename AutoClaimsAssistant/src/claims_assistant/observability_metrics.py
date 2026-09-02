# src/claims_assistant/observability_metrics.py
from __future__ import annotations

from agent_framework.observability import get_meter
from opentelemetry.metrics import Meter, MeterProvider


def _meter(meter_provider: MeterProvider | None) -> Meter:
    if meter_provider is not None:
        return meter_provider.get_meter("claims_assistant")
    return get_meter("claims_assistant")


def record_claim_outcome(status: str, *, meter_provider: MeterProvider | None = None) -> None:
    counter = _meter(meter_provider).create_counter(
        "claims_assistant.claim.outcome", description="Count of claim outcomes by status"
    )
    counter.add(1, {"status": status})


def record_extraction_confidence(
    field: str, confidence: float, *, meter_provider: MeterProvider | None = None
) -> None:
    histogram = _meter(meter_provider).create_histogram(
        "claims_assistant.extraction.confidence",
        description="Per-field extraction confidence scores",
    )
    histogram.record(confidence, {"field": field})


def record_fraud_risk_score(
    score: int, tier: str, *, meter_provider: MeterProvider | None = None
) -> None:
    histogram = _meter(meter_provider).create_histogram(
        "claims_assistant.fraud.risk_score", description="Fraud-risk scores by tier"
    )
    histogram.record(score, {"tier": tier})
