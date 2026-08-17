# tests/test_mcp_vin_vehicle_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.vin_vehicle"],
)


@pytest.mark.asyncio
async def test_decode_vin_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "1FTFW1ET5EF123461"})

    assert result.is_error is False
    assert result.structured_content["make"] == "Ford"
    assert result.structured_content["market_value_usd"] == 19750.00
    assert result.structured_content["policy_number"] == "POL-TX-0006"


@pytest.mark.asyncio
async def test_decode_vin_tool_call_errors_for_unknown_vin(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "0000000000000UNKN"})

    assert result.is_error is True
