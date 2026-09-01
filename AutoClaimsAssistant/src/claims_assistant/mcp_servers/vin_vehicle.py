# src/claims_assistant/mcp_servers/vin_vehicle.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Vehicle
from claims_assistant.observability import configure_observability


class VehicleLookupResult(BaseModel):
    vin: str
    make: str
    model: str
    year: int
    market_value_usd: float
    policy_number: str


async def find_vehicle_by_vin(session: AsyncSession, vin: str) -> Vehicle | None:
    result = await session.execute(select(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(vehicle: Vehicle) -> VehicleLookupResult:
    return VehicleLookupResult(
        vin=vehicle.vin,
        make=vehicle.make,
        model=vehicle.model,
        year=vehicle.year,
        market_value_usd=vehicle.market_value_usd,
        policy_number=vehicle.policy_number,
    )


mcp = MCPServer("vin-vehicle-mcp")


@mcp.tool()
async def decode_vin(vin: str) -> VehicleLookupResult:
    """Decode a VIN into make/model/year/market value. Raises if the VIN is unknown."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, vin)
    if vehicle is None:
        raise ValueError(f"no vehicle found for vin={vin!r}")
    return _to_result(vehicle)


if __name__ == "__main__":
    configure_observability(service_name="vin-vehicle-mcp")
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8103, stateless_http=True)

