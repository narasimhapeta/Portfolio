# src/claims_assistant/eval_fixtures.py
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

from pydantic import BaseModel

from claims_assistant.agents.fraud_signals import RedFlagCode
from claims_assistant.fnol_schema import FNOLFacts

FIXTURES_ROOT = Path(__file__).resolve().parents[2] / "data" / "eval_fixtures"
EXTRACTION_FIXTURES_DIR = FIXTURES_ROOT / "extraction"
COVERAGE_FIXTURES_DIR = FIXTURES_ROOT / "coverage"
FRAUD_FIXTURES_DIR = FIXTURES_ROOT / "fraud"

@dataclass(frozen=True)
class ExtractionFixture:
    fixture_id: str
    narrative_text: str
    gold: FNOLFacts


def load_extraction_fixtures() -> list[ExtractionFixture]:
    fixtures = []
    for txt_path in sorted(EXTRACTION_FIXTURES_DIR.glob("*.txt")):

        fixture_id = txt_path.stem
        json_path = txt_path.with_suffix(".json")
        narrative_text = txt_path.read_text(encoding="utf-8").strip()
        gold_data = json.loads(json_path.read_text(encoding="utf-8"))
        gold = FNOLFacts.model_validate(gold_data)
        fixtures.append(ExtractionFixture(fixture_id, narrative_text, gold))
    return fixtures


class _CoverageFixtureData(BaseModel):
    policy_number: str
    claim_narrative: str
    gold_determination: Literal["approve", "deny", "needs_info"]
    gold_citation: str


@dataclass(frozen=True)
class CoverageFixture:
    fixture_id: str
    policy_number: str
    claim_narrative: str
    gold_determination: Literal["approve", "deny", "needs_info"]
    gold_citation: str


def load_coverage_fixtures() -> list[CoverageFixture]:
    fixtures = []
    for json_path in sorted(COVERAGE_FIXTURES_DIR.glob("*.json")):
        data = _CoverageFixtureData.model_validate_json(json_path.read_text(encoding="utf-8"))
        fixtures.append(
            CoverageFixture(
                fixture_id=json_path.stem,
                policy_number=data.policy_number,
                claim_narrative=data.claim_narrative,
                gold_determination=data.gold_determination,
                gold_citation=data.gold_citation,
            )
        )
    return fixtures


class _FraudFixtureData(BaseModel):
    policy_number: str
    vin: str
    incident_date: str
    claim_narrative: str
    gold_risk_tier: Literal["low", "medium", "high"]
    gold_red_flags: list[RedFlagCode]


@dataclass(frozen=True)
class FraudFixture:
    fixture_id: str
    policy_number: str
    vin: str
    incident_date: str
    claim_narrative: str
    gold_risk_tier: Literal["low", "medium", "high"]
    gold_red_flags: list[RedFlagCode]


def load_fraud_fixtures() -> list[FraudFixture]:
    fixtures = []
    for json_path in sorted(FRAUD_FIXTURES_DIR.glob("*.json")):
        data = _FraudFixtureData.model_validate_json(json_path.read_text(encoding="utf-8"))
        fixtures.append(
            FraudFixture(
                fixture_id=json_path.stem,
                policy_number=data.policy_number,
                vin=data.vin,
                incident_date=data.incident_date,
                claim_narrative=data.claim_narrative,
                gold_risk_tier=data.gold_risk_tier,
                gold_red_flags=data.gold_red_flags,
            )
        )
    return fixtures
