# src/claims_assistant/agents/extraction_schema.py
from __future__ import annotations

from pydantic import BaseModel, Field

from claims_assistant.fnol_schema import FNOLFacts


class FieldConfidence(BaseModel):
    """Per-field extraction confidence, one score per top-level FNOLFacts group (spec §5.4)."""

    incident_datetime: float = Field(ge=0.0, le=1.0)
    location: float = Field(ge=0.0, le=1.0)
    parties: float = Field(ge=0.0, le=1.0)
    vehicles: float = Field(ge=0.0, le=1.0)
    injuries: float = Field(ge=0.0, le=1.0)
    narrative_summary: float = Field(ge=0.0, le=1.0)


class FNOLExtraction(BaseModel):
    facts: FNOLFacts
    confidence: FieldConfidence
