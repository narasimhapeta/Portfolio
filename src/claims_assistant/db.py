# src/claims_assistant/db.py
import asyncpg

from claims_assistant.config import get_settings

_pool: asyncpg.Pool | None = None


async def get_connection_pool() -> asyncpg.Pool:
    global _pool
    if _pool is None:
        settings = get_settings()
        _pool = await asyncpg.create_pool(dsn=settings.postgres_dsn)
    return _pool
