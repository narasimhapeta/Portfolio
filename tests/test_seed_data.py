# tests/test_seed_data.py
import pytest
from sqlalchemy import select

from claims_assistant.database import create_all_tables, get_session_factory
from claims_assistant.models import ClaimHistory, Policy
from claims_assistant.seed_data import seed_database

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_seed_database_populates_expected_rows():
    await create_all_tables()
    counts = await seed_database()

    assert counts == {"policies": 9, "vehicles": 9, "claims_history": 10}

    session_factory = get_session_factory()
    async with session_factory() as session:
        result = await session.execute(
            select(Policy).where(Policy.policy_number == "POL-CA-0002")
        )
        policy = result.scalar_one()
        assert policy.coverage_tier == "full_coverage"
        assert policy.policy_form_id == "CA-FULL-COVERAGE"

        result = await session.execute(
            select(ClaimHistory).where(ClaimHistory.policy_number == "POL-CA-0002")
        )
        claims = result.scalars().all()
        assert len(claims) == 3
        assert any(c.fraud_flag for c in claims)

        result = await session.execute(
            select(ClaimHistory).where(ClaimHistory.policy_number == "POL-CA-0001")
        )
        assert result.scalars().all() == []
