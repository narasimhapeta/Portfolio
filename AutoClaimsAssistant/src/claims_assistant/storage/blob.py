# src/claims_assistant/storage/blob.py
from __future__ import annotations

import uuid

from azure.storage.blob.aio import BlobServiceClient

from claims_assistant.config import Settings


async def upload_claim_document(
    settings: Settings,
    claim_id: uuid.UUID,
    filename: str,
    content: bytes,
    content_type: str,
) -> str:
    blob_name = f"{claim_id}/{filename}"
    async with BlobServiceClient.from_connection_string(
        settings.azure_storage_connection_string
    ) as service_client:
        container_client = service_client.get_container_client(
            settings.azure_storage_container_name
        )
        blob_client = container_client.get_blob_client(blob_name)
        await blob_client.upload_blob(content, overwrite=True, content_type=content_type)
        return blob_client.url
