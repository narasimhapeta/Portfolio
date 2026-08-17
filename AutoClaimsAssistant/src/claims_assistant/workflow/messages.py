# src/claims_assistant/workflow/messages.py
from __future__ import annotations

from pydantic import BaseModel

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.extraction_schema import FNOLExtraction
from claims_assistant.agents.fraud_schema import FraudRiskAssessment


class ClaimIntakeRequest(BaseModel):
    policy_number: str
    vin: str
    narrative_text: str


class ExtractionResult(BaseModel):
    request: ClaimIntakeRequest
    extraction: FNOLExtraction


class CoverageOutcome(BaseModel):
    policy_number: str
    determination: CoverageDetermination


class FraudOutcome(BaseModel):
    policy_number: str
    assessment: FraudRiskAssessment


class ClarificationRequest(BaseModel):
    policy_number: str
    reason: str
    low_confidence_fields: list[str]
    missing_required_fields: list[str]
    extraction: FNOLExtraction
