# src/claims_assistant/workflow/supervisor.py
from __future__ import annotations

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts

# spec §3.1: "supervisor checks confidence" / "Below threshold or missing required fields
# -> handoff". This is deterministic Python, not an LLM call (see Phase 6 plan's Architecture
# section for why) — it's the condition function of the graph's switch-case branch (Task 5),
# not a separate node.
CONFIDENCE_THRESHOLD = 0.7


def identify_low_confidence_fields(
    confidence: FieldConfidence, threshold: float = CONFIDENCE_THRESHOLD
) -> list[str]:
    return [
        field_name
        for field_name in FieldConfidence.model_fields
        if getattr(confidence, field_name) < threshold
    ]


def identify_missing_required_fields(facts: FNOLFacts) -> list[str]:
    # Only list-valued fields can be "missing" in a way confidence scores don't already
    # cover — location/incident_datetime/narrative_summary are always non-empty strings
    # once FNOLFacts validates, so a genuinely thin answer there shows up as low
    # confidence instead (identify_low_confidence_fields), not as an absent field here.
    missing: list[str] = []
    if not facts.parties:
        missing.append("parties")
    if not facts.vehicles:
        missing.append("vehicles")
    return missing


def is_extraction_sufficient(
    extraction: FNOLExtraction, threshold: float = CONFIDENCE_THRESHOLD
) -> bool:
    return (
        not identify_low_confidence_fields(extraction.confidence, threshold)
        and not identify_missing_required_fields(extraction.facts)
    )
