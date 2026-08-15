# tests/test_coverage_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.coverage_schema import CoverageDetermination


def test_coverage_determination_validates():
    determination = CoverageDetermination(
        determination="approve",
        rationale="Collision coverage applies per Section 3.1.",
        citations=["CA-FULL-COVERAGE_section-3-physical-damage-coverage"],
    )

    assert determination.determination == "approve"
    assert determination.citations == ["CA-FULL-COVERAGE_section-3-physical-damage-coverage"]


def test_coverage_determination_rejects_invalid_determination_value():
    with pytest.raises(ValidationError):
        CoverageDetermination(determination="maybe", rationale="unclear", citations=[])
