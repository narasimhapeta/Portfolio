from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_history_page_renders_a_table_of_claims():
    at = AppTest.from_file("../src/claims_assistant/frontend/pages/history.py")
    with patch("claims_assistant.frontend.api_client.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.list_claims.return_value = [
        {
            "id": "abc",
            "status": "completed",
            "policy_number": "POL-CA-0003",
            "vin": "1C4RJFBG5FC123458",
            "created_at": "2026-08-27T10:00:00Z",
        },
        {
            "id": "def",
            "status": "failed",
            "policy_number": "POL-CA-0004",
            "vin": "1C4RJFBG5FC123459",
            "created_at": "2026-08-27T11:00:00Z",
        },
    ]

        mock_client_cls.return_value = mock_client

        at.run()

        mock_client.list_claims.assert_called_once()
        assert not at.exception
        assert len(at.dataframe) == 1
