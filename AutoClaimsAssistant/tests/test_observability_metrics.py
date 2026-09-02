# tests/test_observability_metrics.py
from __future__ import annotations

from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import InMemoryMetricReader

from claims_assistant.observability_metrics import (
    record_claim_outcome,
    record_extraction_confidence,
    record_fraud_risk_score,
)


def _read_metric_names(reader: InMemoryMetricReader) -> set[str]:
    data = reader.get_metrics_data()
    names = set()
    if data is None:
        return names
    for rm in data.resource_metrics:
        for sm in rm.scope_metrics:
            for metric in sm.metrics:
                names.add(metric.name)
    return names


def test_record_claim_outcome_emits_a_counter():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_claim_outcome("completed", meter_provider=provider)

    assert "claims_assistant.claim.outcome" in _read_metric_names(reader)


def test_record_extraction_confidence_emits_a_histogram():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_extraction_confidence("injuries", 0.3, meter_provider=provider)

    assert "claims_assistant.extraction.confidence" in _read_metric_names(reader)


def test_record_fraud_risk_score_emits_a_histogram():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_fraud_risk_score(72, "high", meter_provider=provider)

    assert "claims_assistant.fraud.risk_score" in _read_metric_names(reader)
