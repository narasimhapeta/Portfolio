from fastapi import APIRouter
from claims_assistant.config import get_settings

router = APIRouter()

@router.get("/health")
async def health()-> dict[str, str]:
    settings = get_settings()
    return {"status": "ok", "app_env": settings.app_env}