# tests/test_mcp_policy_db_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.policy_db"],
)


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
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
async def test_get_policy_by_number_tool_call_errors_for_unknown_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True


@pytest.mark.asyncio
async def test_get_policy_by_vin_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("get_policy_by_vin", {"vin": "5YJ3E1EA7JF123457"})

    assert result.is_error is False
    assert result.structured_content["policy_number"] == "POL-CA-0002"
