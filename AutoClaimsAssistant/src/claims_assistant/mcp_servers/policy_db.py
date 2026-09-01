# src/claims_assistant/mcp_servers/policy_db.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Policy, Vehicle
from claims_assistant.observability import configure_observability


class PolicyLookupResult(BaseModel):
    policy_number: str
    policyholder_name: str
    state: str
    coverage_tier: str
    policy_form_id: str
    effective_date: str
    expiration_date: str
    premium_monthly: float


async def find_policy_by_number(session: AsyncSession, policy_number: str) -> Policy | None:
    result = await session.execute(select(Policy).where(Policy.policy_number == policy_number))
    return result.scalar_one_or_none()


async def find_policy_by_vin(session: AsyncSession, vin: str) -> Policy | None:
    result = await session.execute(select(Policy).join(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(policy: Policy) -> PolicyLookupResult:
    return PolicyLookupResult(
        policy_number=policy.policy_number,
        policyholder_name=policy.policyholder_name,
        state=policy.state,
        coverage_tier=policy.coverage_tier,
        policy_form_id=policy.policy_form_id,
        effective_date=policy.effective_date.isoformat(),
        expiration_date=policy.expiration_date.isoformat(),
        premium_monthly=policy.premium_monthly,
    )


mcp = MCPServer("policy-db-mcp")


@mcp.tool()
async def get_policy_by_number(policy_number: str) -> PolicyLookupResult:
    """Look up a policy by its policy number. Raises if no such policy exists."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_number(session, policy_number)
    if policy is None:
        raise ValueError(f"no policy found for policy_number={policy_number!r}")
    return _to_result(policy)


@mcp.tool()
async def get_policy_by_vin(vin: str) -> PolicyLookupResult:
    """Look up the policy covering a given vehicle VIN. Raises if no such VIN exists."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_vin(session, vin)
    if policy is None:
        raise ValueError(f"no policy found for vin={vin!r}")
    return _to_result(policy)


if __name__ == "__main__":
    configure_observability(service_name="policy-db-mcp")
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8101, stateless_http=True)

