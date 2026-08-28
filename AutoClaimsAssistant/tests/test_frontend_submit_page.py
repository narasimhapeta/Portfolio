from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_submit_page_shows_recommendation_after_successful_submit():
    at = AppTest.from_file("../src/claims_assistant/frontend/pages/submit.py")
    with patch("claims_assistant.frontend.api_client.ClaimsApiClient") as mock_client_cls:

        mock_client = MagicMock()
        mock_client.submit_claim.return_value = {
            "id": "abc-123",
            "status": "completed",
            "recommendation": {"coverage_determination": "approve"},
        }
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="policy_number").set_value("POL-CA-0003")
        at.text_input(key="vin").set_value("1C4RJFBG5FC123458")
        at.text_area(key="narrative_text").set_value("Hail damage overnight.")
        at.button(key="submit_button").click().run()

        mock_client.submit_claim.assert_called_once_with(
            policy_number="POL-CA-0003",
            vin="1C4RJFBG5FC123458",
            narrative_text="Hail damage overnight.",
        )
        assert not at.exception
