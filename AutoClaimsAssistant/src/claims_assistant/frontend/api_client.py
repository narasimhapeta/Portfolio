# src/claims_assistant/frontend/api_client.py
from __future__ import annotations

from typing import Any, cast

import httpx


class ClaimsApiClient:
    def __init__(self, base_url: str, transport: httpx.BaseTransport | None = None) -> None:
        self._client = httpx.Client(base_url=base_url, transport=transport, timeout=180.0)

    def submit_claim(self, policy_number: str, vin: str, narrative_text: str) -> dict[str, Any]:
        response = self._client.post(
            "/claims",
            json={"policy_number": policy_number, "vin": vin, "narrative_text": narrative_text},
        )
        response.raise_for_status()
        return cast(dict[str, Any], response.json())


    def get_claim(self, claim_id: str) -> dict[str, Any]:
        response = self._client.get(f"/claims/{claim_id}")
        response.raise_for_status()
        return cast(dict[str, Any], response.json())


    def list_claims(self, limit: int = 50, offset: int = 0) -> list[dict[str, Any]]:
        response = self._client.get("/claims", params={"limit": limit, "offset": offset})
        response.raise_for_status()
        return cast(list[dict[str, Any]], response.json()["claims"])


    def upload_document(
        self, claim_id: str, filename: str, content: bytes, content_type: str
    ) -> dict[str, Any]:
        response = self._client.post(
            f"/claims/{claim_id}/documents",
            files={"file": (filename, content, content_type)},
        )
        response.raise_for_status()
        return cast(dict[str, Any], response.json())

