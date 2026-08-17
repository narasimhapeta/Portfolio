# tests/test_mcp_vin_vehicle_server.py
import pytest
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_decode_vin_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.vin_vehicle_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "1FTFW1ET5EF123461"})

    assert result.is_error is False
    assert result.structured_content["make"] == "Ford"
    assert result.structured_content["market_value_usd"] == 19750.00
    assert result.structured_content["policy_number"] == "POL-TX-0006"


@pytest.mark.asyncio
async def test_decode_vin_tool_call_errors_for_unknown_vin(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.vin_vehicle_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "0000000000000UNKN"})

    assert result.is_error is True
