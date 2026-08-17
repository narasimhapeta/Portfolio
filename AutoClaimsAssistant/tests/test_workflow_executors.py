# tests/test_workflow_executors.py
import pytest

from claims_assistant.workflow.executors import _incident_date


def test_incident_date_extracts_date_portion_from_full_datetime():
    assert _incident_date("2026-03-12T07:45") == "2026-03-12"


def test_incident_date_accepts_bare_date():
    assert _incident_date("2026-03-12") == "2026-03-12"


def test_incident_date_raises_clear_error_for_unparseable_input():
    with pytest.raises(ValueError, match="non-ISO-date"):
        _incident_date("sometime last week, not sure exactly when")
