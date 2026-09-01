# tests/test_observability.py
from __future__ import annotations

from unittest.mock import patch

from claims_assistant.observability import configure_observability


def test_configure_observability_noops_when_connection_string_unset(monkeypatch):
    monkeypatch.delenv("APPLICATIONINSIGHTS_CONNECTION_STRING", raising=False)
    with patch("claims_assistant.observability.configure_azure_monitor") as mock_configure:
        configure_observability()
    mock_configure.assert_not_called()


def test_configure_observability_calls_configure_azure_monitor_when_connection_string_set(
    monkeypatch,
):
    monkeypatch.setenv("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=fake")
    with patch("claims_assistant.observability.configure_azure_monitor") as mock_configure:
        configure_observability()
    mock_configure.assert_called_once()
