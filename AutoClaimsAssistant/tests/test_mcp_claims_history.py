# tests/test_mcp_claims_history.py
import pytest

from claims_assistant.database import get_session_factory
from claims_assistant.mcp_servers.claims_history import fetch_claims_for_policy, policy_exists

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_fetch_claims_for_policy_returns_flagged_history(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        claims = await fetch_claims_for_policy(session, "POL-CA-0002")

    assert len(claims) == 3
    assert sum(1 for c in claims if c.fraud_flag) == 1


@pytest.mark.asyncio
async def test_fetch_claims_for_policy_returns_empty_for_clean_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        claims = await fetch_claims_for_policy(session, "POL-CA-0001")

    assert claims == []


@pytest.mark.asyncio
async def test_policy_exists_is_false_for_unknown_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        exists = await policy_exists(session, "POL-ZZ-9999")

    assert exists is False
