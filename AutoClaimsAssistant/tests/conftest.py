# tests/conftest.py
import pytest_asyncio

from claims_assistant.database import create_all_tables
from claims_assistant.seed_data import seed_database


@pytest_asyncio.fixture
async def seeded_db() -> None:
    await create_all_tables()
    await seed_database()
