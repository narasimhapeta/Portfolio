# tests/test_blob_storage.py
import uuid

import pytest

from claims_assistant.config import get_settings
from claims_assistant.storage.blob import upload_claim_document

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_upload_claim_document_returns_retrievable_url():
    settings = get_settings()
    claim_id = uuid.uuid4()

    url = await upload_claim_document(
        settings, claim_id, "photo.jpg", b"fake-jpeg-bytes", "image/jpeg"
    )

    assert url.startswith("https://claimsassistantstorage.blob.core.windows.net/claim-documents/")
    assert str(claim_id) in url
    assert url.endswith("photo.jpg")
