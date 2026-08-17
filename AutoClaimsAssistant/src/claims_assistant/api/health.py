# src/claims_assistant/api/health.py
from fastapi import APIRouter, Response

from claims_assistant.config import get_settings
from claims_assistant.db import get_connection_pool

router = APIRouter()


@router.get("/health")
async def health() -> dict[str, str]:
    settings = get_settings()
    return {"status": "ok", "app_env": settings.app_env}


@router.get("/health/db")
async def health_db(response: Response) -> dict[str, str]:
    try:
        pool = await get_connection_pool()
        async with pool.acquire() as conn:
            await conn.fetchval("SELECT 1")
        return {"status": "ok", "db": "reachable"}
    except OSError:
        response.status_code = 503
        return {"status": "error", "db": "unreachable"}
