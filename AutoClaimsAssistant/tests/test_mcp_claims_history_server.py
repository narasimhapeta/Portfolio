# tests/test_mcp_claims_history_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.claims_history"],
)


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_flagged_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0002"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 3
    assert result.structured_content["prior_fraud_flag_count"] == 1


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_clean_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0001"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 0
    assert result.structured_content["most_recent_claim_date"] is None


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_errors_for_unknown_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True
