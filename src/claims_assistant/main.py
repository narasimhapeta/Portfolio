from fastapi import FastAPI
from claims_assistant.api.health import router as health_router


def create_app() -> FastAPI:
    app = FastAPI(title="Claims Assistant")
    app.include_router(health_router)
    return app

app = create_app()