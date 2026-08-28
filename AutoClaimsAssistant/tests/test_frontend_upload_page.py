from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_upload_page_calls_upload_document_with_claim_id():
    at = AppTest.from_file("../src/claims_assistant/frontend/pages/upload.py")
    with patch("claims_assistant.frontend.api_client.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.upload_document.return_value = {"id": "abc-123", "document_urls": ["url1"]}
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="upload_claim_id").set_value("abc-123")
        assert not at.exception
