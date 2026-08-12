# tests/test_database.py
import pytest
from sqlalchemy import text

from claims_assistant.database import create_all_tables, get_engine

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_create_all_tables_creates_expected_tables():
    await create_all_tables()

    engine = get_engine()
    async with engine.connect() as conn:
        result = await conn.execute(
            text(
                "SELECT table_name FROM information_schema.tables "
                "WHERE table_schema = 'public'"
            )
        )
        table_names = {row[0] for row in result}

    assert {"policies", "vehicles", "claims_history"}.issubset(table_names)
