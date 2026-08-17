# src/claims_assistant/mcp_servers/claims_history.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import ClaimHistory, Policy


class ClaimSummary(BaseModel):
    claim_id: str
    claim_date: str
    claim_type: str
    amount_usd: float
    status: str
    fraud_flag: bool


class ClaimsHistoryResult(BaseModel):
    policy_number: str
    claim_count: int
    prior_fraud_flag_count: int
    most_recent_claim_date: str | None
    claims: list[ClaimSummary]


async def policy_exists(session: AsyncSession, policy_number: str) -> bool:
    result = await session.execute(
        select(Policy.policy_number).where(Policy.policy_number == policy_number)
    )
    return result.scalar_one_or_none() is not None


async def fetch_claims_for_policy(
    session: AsyncSession, policy_number: str
) -> list[ClaimHistory]:
    result = await session.execute(
        select(ClaimHistory)
        .where(ClaimHistory.policy_number == policy_number)
        .order_by(ClaimHistory.claim_date.desc())
    )
    return list(result.scalars().all())


def _to_result(policy_number: str, claims: list[ClaimHistory]) -> ClaimsHistoryResult:
    return ClaimsHistoryResult(
        policy_number=policy_number,
        claim_count=len(claims),
        prior_fraud_flag_count=sum(1 for c in claims if c.fraud_flag),
        most_recent_claim_date=claims[0].claim_date.isoformat() if claims else None,
        claims=[
            ClaimSummary(
                claim_id=c.claim_id,
                claim_date=c.claim_date.isoformat(),
                claim_type=c.claim_type,
                amount_usd=c.amount_usd,
                status=c.status,
                fraud_flag=c.fraud_flag,
            )
            for c in claims
        ],
    )


mcp = MCPServer("claims-history-mcp")


@mcp.tool()
async def get_claims_history(policy_number: str) -> ClaimsHistoryResult:
    """Look up prior claims for a policy. Raises if the policy number doesn't exist."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        if not await policy_exists(session, policy_number):
            raise ValueError(f"no policy found for policy_number={policy_number!r}")
        claims = await fetch_claims_for_policy(session, policy_number)
    return _to_result(policy_number, claims)


if __name__ == "__main__":
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8102, stateless_http=True)
