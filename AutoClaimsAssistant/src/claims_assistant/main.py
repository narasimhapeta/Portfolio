from fastapi import FastAPI

from claims_assistant.api.claims import router as claims_router
from claims_assistant.api.health import router as health_router
from claims_assistant.observability import configure_observability

configure_observability()


def create_app() -> FastAPI:
    app = FastAPI(title="Claims Assistant")
    app.include_router(health_router)
    app.include_router(claims_router)
    return app


app = create_app()
