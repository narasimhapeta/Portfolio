# Phase 0: Foundations — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Stand up a runnable project skeleton — FastAPI app with health checks, config management, Postgres wired via Docker Compose, and lint/typecheck/test tooling — so every later phase has a working foundation to build on.

**Architecture:** A single Python package (`claims_assistant`) using an app-factory pattern for FastAPI, Pydantic Settings for config (reads from `.env`), and a `docker-compose.yml` that runs Postgres alongside the API container for local dev. No business logic yet — this phase only proves the scaffolding works end-to-end.

**Tech Stack:** Python 3.12, `uv` (dependency management + lockfile), FastAPI + Uvicorn, Pydantic v2 / pydantic-settings, `asyncpg` (raw async Postgres driver — SQLAlchemy models come in Phase 1 when there's real schema to define), `ruff` (lint + format), `mypy` (type check), `pytest` + `pytest-asyncio`, Docker + Docker Compose.

## Global Constraints

- Python 3.12 minimum (per spec's async/typed-interfaces requirement).
- All source lives under `src/claims_assistant/` (src-layout, avoids import ambiguity).
- All functions that do I/O (DB, HTTP) are `async def` — matches the spec's "async I/O" requirement throughout.
- No business logic in this phase — health/connectivity checks only.
- Every task ends with tests passing before moving to the next task.

---

### Task 1: Project scaffolding & dependency management

**Files:**
- Create: `pyproject.toml`
- Create: `.gitignore`
- Create: `.env.example`
- Create: `src/claims_assistant/__init__.py`
- Create: `tests/__init__.py`

**Interfaces:**
- Produces: a `uv`-managed project named `claims-assistant`, importable as `claims_assistant`, with `fastapi`, `uvicorn`, `pydantic-settings`, `asyncpg` as runtime deps and `pytest`, `pytest-asyncio`, `httpx`, `ruff`, `mypy` as dev deps.

- [ ] **Step 1: Initialize the project with uv**

Run (PowerShell, from `c:\Narasimha\AutoClaimsAssistant`):
```powershell
uv init --package --name claims-assistant --python 3.12
```
This scaffolds a `pyproject.toml` and `src/claims_assistant/`. If `uv` isn't installed: `winget install --id=astral-sh.uv -e`.

- [ ] **Step 2: Add runtime and dev dependencies**

```powershell
uv add fastapi "uvicorn[standard]" "pydantic-settings>=2" asyncpg
uv add --dev pytest pytest-asyncio httpx ruff mypy
```

- [ ] **Step 3: Create `.env.example`**

```env
# .env.example — copy to .env and fill in for local dev
APP_ENV=local
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=claims_assistant
POSTGRES_USER=claims_assistant
POSTGRES_PASSWORD=devpassword
```

- [ ] **Step 4: Create `.gitignore`**

```gitignore
.venv/
__pycache__/
*.pyc
.env
.pytest_cache/
.mypy_cache/
.ruff_cache/
dist/
```

- [ ] **Step 5: Create empty test package marker**

```powershell
New-Item -ItemType Directory -Force tests | Out-Null
New-Item -ItemType File -Force tests/__init__.py | Out-Null
```

- [ ] **Step 6: Verify the environment resolves**

Run: `uv sync`
Expected: completes with no errors, creates `.venv/` and `uv.lock`.

- [ ] **Step 7: Commit**

```powershell
git add pyproject.toml uv.lock .gitignore .env.example src tests
git commit -m "chore: scaffold project with uv, FastAPI, Postgres deps"
```
(Skip this step if you haven't run `git init` yet — do that first: `git init`.)

---

### Task 2: Config module (Pydantic Settings)

**Files:**
- Create: `src/claims_assistant/config.py`
- Test: `tests/test_config.py`

**Interfaces:**
- Consumes: environment variables / `.env` file (Task 1's `.env.example` defines the shape).
- Produces: `Settings` class (pydantic-settings `BaseSettings`) and `get_settings()` — a cached factory function other modules import to read config. Fields: `app_env: str`, `postgres_host: str`, `postgres_port: int`, `postgres_db: str`, `postgres_user: str`, `postgres_password: str`, plus computed property `postgres_dsn: str`.

- [ ] **Step 1: Write the failing test**

```python
# tests/test_config.py
import os

from claims_assistant.config import Settings, get_settings


def test_settings_reads_from_env(monkeypatch):
    monkeypatch.setenv("APP_ENV", "test")
    monkeypatch.setenv("POSTGRES_HOST", "db.example")
    monkeypatch.setenv("POSTGRES_PORT", "5433")
    monkeypatch.setenv("POSTGRES_DB", "testdb")
    monkeypatch.setenv("POSTGRES_USER", "testuser")
    monkeypatch.setenv("POSTGRES_PASSWORD", "testpass")

    settings = Settings()

    assert settings.app_env == "test"
    assert settings.postgres_host == "db.example"
    assert settings.postgres_port == 5433
    assert settings.postgres_dsn == (
        "postgresql://testuser:testpass@db.example:5433/testdb"
    )


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.config'`

- [ ] **Step 3: Write the implementation**

```python
# src/claims_assistant/config.py
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    app_env: str = "local"
    postgres_host: str = "localhost"
    postgres_port: int = 5432
    postgres_db: str = "claims_assistant"
    postgres_user: str = "claims_assistant"
    postgres_password: str = "devpassword"

    @property
    def postgres_dsn(self) -> str:
        return (
            f"postgresql://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


@lru_cache
def get_settings() -> Settings:
    return Settings()
```

- [ ] **Step 4: Run test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [ ] **Step 5: Commit**

```powershell
git add src/claims_assistant/config.py tests/test_config.py
git commit -m "feat: add Settings config module"
```

---

### Task 3: FastAPI app factory + `/health` endpoint

**Files:**
- Create: `src/claims_assistant/main.py`
- Create: `src/claims_assistant/api/__init__.py`
- Create: `src/claims_assistant/api/health.py`
- Test: `tests/test_health.py`

**Interfaces:**
- Consumes: `get_settings()` from Task 2.
- Produces: `create_app() -> FastAPI` factory in `main.py` (importable by Uvicorn as `claims_assistant.main:app` via a module-level `app = create_app()`), and an `APIRouter` in `api/health.py` mounted at `/health` returning `{"status": "ok", "app_env": <str>}`.

- [ ] **Step 1: Write the failing test**

```python
# tests/test_health.py
from fastapi.testclient import TestClient

from claims_assistant.main import create_app


def test_health_returns_ok():
    client = TestClient(create_app())

    response = client.get("/health")

    assert response.status_code == 200
    assert response.json()["status"] == "ok"
```

- [ ] **Step 2: Run test to verify it fails**

Run: `uv run pytest tests/test_health.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.main'`

- [ ] **Step 3: Write the health router**

```python
# src/claims_assistant/api/health.py
from fastapi import APIRouter

from claims_assistant.config import get_settings

router = APIRouter()


@router.get("/health")
async def health() -> dict[str, str]:
    settings = get_settings()
    return {"status": "ok", "app_env": settings.app_env}
```

- [ ] **Step 4: Write the app factory**

```python
# src/claims_assistant/main.py
from fastapi import FastAPI

from claims_assistant.api.health import router as health_router


def create_app() -> FastAPI:
    app = FastAPI(title="Auto Claims Assistant")
    app.include_router(health_router)
    return app


app = create_app()
```

- [ ] **Step 5: Create the `api` package marker**

```python
# src/claims_assistant/api/__init__.py
```

- [ ] **Step 6: Run test to verify it passes**

Run: `uv run pytest tests/test_health.py -v`
Expected: PASS (1 passed)

- [ ] **Step 7: Run the server manually and verify by hand**

Run: `uv run uvicorn claims_assistant.main:app --reload`
Then in a second terminal: `curl http://127.0.0.1:8000/health`
Expected: `{"status":"ok","app_env":"local"}`. Stop the server (Ctrl+C) once confirmed.

- [ ] **Step 8: Commit**

```powershell
git add src/claims_assistant/main.py src/claims_assistant/api tests/test_health.py
git commit -m "feat: add FastAPI app factory and /health endpoint"
```

---

### Task 4: Docker Compose with Postgres + API container

**Files:**
- Create: `Dockerfile`
- Create: `docker-compose.yml`
- Create: `.dockerignore`

**Interfaces:**
- Consumes: `pyproject.toml`/`uv.lock` (Task 1), `main.py` (Task 3), `.env` (git-ignored, user-created from `.env.example`).
- Produces: an `api` service reachable at `localhost:8000` and a `postgres` service reachable at `localhost:5432`, on a shared Docker network, both started via `docker-compose up`.

- [ ] **Step 1: Write the Dockerfile**

```dockerfile
# Dockerfile
FROM python:3.12-slim

COPY --from=ghcr.io/astral-sh/uv:latest /uv /uvx /bin/

WORKDIR /app
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-install-project

COPY src ./src
RUN uv sync --frozen

EXPOSE 8000
CMD ["uv", "run", "uvicorn", "claims_assistant.main:app", "--host", "0.0.0.0", "--port", "8000"]
```

- [ ] **Step 2: Write `.dockerignore`**

```
.venv/
__pycache__/
.pytest_cache/
.mypy_cache/
.ruff_cache/
tests/
.env
.git/
docs/
```

- [ ] **Step 3: Write `docker-compose.yml`**

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: claims_assistant
      POSTGRES_USER: claims_assistant
      POSTGRES_PASSWORD: devpassword
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U claims_assistant"]
      interval: 5s
      timeout: 5s
      retries: 5

  api:
    build: .
    env_file: .env
    environment:
      POSTGRES_HOST: postgres
    ports:
      - "8000:8000"
    depends_on:
      postgres:
        condition: service_healthy

volumes:
  pgdata:
```

- [ ] **Step 4: Create your local `.env` from the example**

```powershell
Copy-Item .env.example .env
```

- [ ] **Step 5: Build and start the stack**

Run: `docker-compose up --build`
Expected: `postgres` logs "database system is ready to accept connections", `api` logs Uvicorn started on `0.0.0.0:8000`.

- [ ] **Step 6: Verify from the host**

In a second terminal: `curl http://127.0.0.1:8000/health`
Expected: `{"status":"ok","app_env":"local"}`. Then `docker-compose down` to stop.

- [ ] **Step 7: Commit**

```powershell
git add Dockerfile docker-compose.yml .dockerignore .env.example
git commit -m "chore: add Docker Compose stack for API and Postgres"
```

---

### Task 5: DB connectivity check (`/health/db`)

**Files:**
- Modify: `src/claims_assistant/api/health.py`
- Create: `src/claims_assistant/db.py`
- Test: `tests/test_health_db.py`

**Interfaces:**
- Consumes: `get_settings().postgres_dsn` (Task 2), `asyncpg` (Task 1 dependency).
- Produces: `get_connection_pool() -> asyncpg.Pool` (module-level cached pool in `db.py`), and `GET /health/db` returning `{"status": "ok", "db": "reachable"}` or a 503 with `{"status": "error", "db": "unreachable"}` if the query fails.

This task's test is an **integration test** — it requires the real Postgres from Task 4 running (`docker-compose up postgres` or `docker-compose up`), since faking `asyncpg` here would only prove the mock works, not that the wiring is real.

- [ ] **Step 1: Write the failing integration test**

```python
# tests/test_health_db.py
import pytest
from fastapi.testclient import TestClient

from claims_assistant.main import create_app

pytestmark = pytest.mark.integration


def test_health_db_returns_ok_when_postgres_reachable():
    client = TestClient(create_app())

    response = client.get("/health/db")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "db": "reachable"}
```

- [ ] **Step 2: Register the `integration` marker**

```toml
# pyproject.toml — add under [tool.pytest.ini_options]
[tool.pytest.ini_options]
markers = [
    "integration: requires external services (e.g. Postgres) to be running",
]
```

- [ ] **Step 3: Start Postgres and run test to verify it fails**

Run: `docker-compose up -d postgres` then `uv run pytest tests/test_health_db.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.db'`

- [ ] **Step 4: Write the connection pool module**

```python
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
```

- [ ] **Step 5: Add the `/health/db` route**

```python
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
```

- [ ] **Step 6: Run test to verify it passes**

Ensure `.env` has `POSTGRES_HOST=localhost` (Postgres port is published to the host by Task 4's compose file), then:
Run: `uv run pytest tests/test_health_db.py -v`
Expected: PASS (1 passed). Then `docker-compose down`.

- [ ] **Step 7: Commit**

```powershell
git add src/claims_assistant/db.py src/claims_assistant/api/health.py tests/test_health_db.py pyproject.toml
git commit -m "feat: add Postgres connectivity check at /health/db"
```

---

### Task 6: Lint + type-check tooling

**Files:**
- Modify: `pyproject.toml`

**Interfaces:**
- Produces: `[tool.ruff]` and `[tool.mypy]` config sections; both tools runnable via `uv run ruff check .` / `uv run mypy src`.

- [ ] **Step 1: Add ruff config**

```toml
# pyproject.toml
[tool.ruff]
line-length = 100
target-version = "py312"

[tool.ruff.lint]
select = ["E", "F", "I", "UP", "B"]
```

- [ ] **Step 2: Add mypy config**

```toml
# pyproject.toml
[tool.mypy]
python_version = "3.12"
strict = true
disallow_untyped_defs = true
```

- [ ] **Step 3: Run ruff and fix anything it flags**

Run: `uv run ruff check .`
Expected: eventually "All checks passed!" — fix any import-order or unused-import issues it reports first.

- [ ] **Step 4: Run mypy and fix anything it flags**

Run: `uv run mypy src`
Expected: eventually "Success: no issues found". If `asyncpg` has no type stubs, add under `[[tool.mypy.overrides]]`:
```toml
[[tool.mypy.overrides]]
module = "asyncpg.*"
ignore_missing_imports = true
```

- [ ] **Step 5: Run the full test suite once more (excluding integration tests, no Postgres needed)**

Run: `uv run pytest -v -m "not integration"`
Expected: PASS (3 passed: `test_settings_reads_from_env`, `test_get_settings_is_cached`, `test_health_returns_ok`)

- [ ] **Step 6: Commit**

```powershell
git add pyproject.toml
git commit -m "chore: configure ruff and mypy"
```

---

## Definition of Done for Phase 0

- [ ] `uv run pytest -v -m "not integration"` passes with no Postgres running.
- [ ] `docker-compose up --build` brings up both services; `curl http://127.0.0.1:8000/health` and `.../health/db` both return 200.
- [ ] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [ ] Roadmap doc's Phase 0 checkbox is checked off.
- [ ] Everything above is committed.

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 1 (synthetic data generation) plan next.
