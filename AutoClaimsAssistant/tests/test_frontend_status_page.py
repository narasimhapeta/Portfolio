from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_status_page_shows_completed_claim():
    at = AppTest.from_file("../src/claims_assistant/frontend/pages/status.py")
    with patch("claims_assistant.frontend.api_client.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.get_claim.return_value = {
            "id": "abc-123",
            "status": "completed",
            "recommendation": {"coverage_determination": "approve"},
        }
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="lookup_claim_id").set_value("abc-123")
        at.button(key="lookup_button").click().run()

        mock_client.get_claim.assert_called_once_with("abc-123")
        assert not at.exception
