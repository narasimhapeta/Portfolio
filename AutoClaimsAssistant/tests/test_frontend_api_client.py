from __future__ import annotations

import httpx

from claims_assistant.frontend.api_client import ClaimsApiClient


def test_submit_claim_posts_to_claims_endpoint():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims"
        assert request.method == "POST"
        return httpx.Response(201, json={"id": "abc", "status": "completed"})

    client = ClaimsApiClient(
        base_url="http://test",
        transport=httpx.MockTransport(handler),
    )
    result = client.submit_claim(
        policy_number="POL-CA-0003", vin="1C4RJFBG5FC123458", narrative_text="hail damage"
    )
    assert result["status"] == "completed"


def test_get_claim_gets_by_id():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims/abc"
        return httpx.Response(200, json={"id": "abc", "status": "completed"})

    client = ClaimsApiClient(base_url="http://test", transport=httpx.MockTransport(handler))
    result = client.get_claim("abc")
    assert result["id"] == "abc"


def test_list_claims_passes_pagination_params():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims"
        assert request.url.params["limit"] == "10"
        assert request.url.params["offset"] == "5"
        return httpx.Response(200, json={"claims": []})

    client = ClaimsApiClient(base_url="http://test", transport=httpx.MockTransport(handler))
    result = client.list_claims(limit=10, offset=5)
    assert result == []
