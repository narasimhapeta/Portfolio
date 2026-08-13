# src/claims_assistant/eval_fixtures.py
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from claims_assistant.fnol_schema import FNOLFacts

FIXTURES_DIR = Path(__file__).resolve().parents[2] / "data" / "eval_fixtures" / "extraction"


@dataclass(frozen=True)
class ExtractionFixture:
    fixture_id: str
    narrative_text: str
    gold: FNOLFacts


def load_extraction_fixtures() -> list[ExtractionFixture]:
    fixtures = []
    for txt_path in sorted(FIXTURES_DIR.glob("*.txt")):
        fixture_id = txt_path.stem
        json_path = txt_path.with_suffix(".json")
        narrative_text = txt_path.read_text(encoding="utf-8").strip()
        gold_data = json.loads(json_path.read_text(encoding="utf-8"))
        gold = FNOLFacts.model_validate(gold_data)
        fixtures.append(ExtractionFixture(fixture_id, narrative_text, gold))
    return fixtures
