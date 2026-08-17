# tests/test_mcp_vin_vehicle.py
import pytest

from claims_assistant.database import get_session_factory
from claims_assistant.mcp_servers.vin_vehicle import find_vehicle_by_vin

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_find_vehicle_by_vin_returns_seeded_vehicle(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, "1FTFW1ET5EF123461")

    assert vehicle is not None
    assert vehicle.make == "Ford"
    assert vehicle.model == "F-150"
    assert vehicle.policy_number == "POL-TX-0006"


@pytest.mark.asyncio
async def test_find_vehicle_by_vin_returns_none_when_missing(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, "0000000000000UNKN")

    assert vehicle is None
