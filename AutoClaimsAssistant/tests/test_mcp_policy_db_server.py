# tests/test_mcp_policy_db_server.py
import pytest
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-CA-0002"}
            )

    assert result.is_error is False
    assert result.structured_content is not None
    assert result.structured_content["coverage_tier"] == "full_coverage"
    assert result.structured_content["policy_form_id"] == "CA-FULL-COVERAGE"


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call_errors_for_unknown_policy(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True


@pytest.mark.asyncio
async def test_get_policy_by_vin_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("get_policy_by_vin", {"vin": "5YJ3E1EA7JF123457"})

    assert result.is_error is False
    assert result.structured_content["policy_number"] == "POL-CA-0002"
