# tests/test_mcp_policy_db.py
import pytest

from claims_assistant.database import get_session_factory
from claims_assistant.mcp_servers.policy_db import find_policy_by_number, find_policy_by_vin

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_find_policy_by_number_returns_seeded_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_number(session, "POL-CA-0002")

    assert policy is not None
    assert policy.coverage_tier == "full_coverage"
    assert policy.policy_form_id == "CA-FULL-COVERAGE"


@pytest.mark.asyncio
async def test_find_policy_by_number_returns_none_when_missing(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_number(session, "POL-ZZ-9999")

    assert policy is None


@pytest.mark.asyncio
async def test_find_policy_by_vin_returns_owning_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_vin(session, "5YJ3E1EA7JF123457")

    assert policy is not None
    assert policy.policy_number == "POL-CA-0002"
