# Phase 7: FastAPI Orchestrator Endpoints Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Expose Phase 6's `build_claim_intake_workflow()` graph over HTTP: `POST /claims` runs the full pipeline synchronously and persists the outcome, `GET /claims/{id}` reads a persisted outcome back. A real HTTP request through Swagger/Postman runs the full pipeline and returns the structured recommendation (roadmap Phase 7 success criteria).

**Architecture:** POST /claims awaits `workflow.run(intake)` directly in the request/response cycle (confirmed with the project owner: synchronous, not background-task/polling — see rationale below) and persists whichever terminal outcome the graph produced — a `ClaimRecommendation` or a `ClarificationRequest` — to a new `claims` Postgres table, keyed by a server-generated UUID. GET /claims/{id} is a pure read of that table; it never re-runs the pipeline. A new `src/claims_assistant/claims_repository.py` owns the three outcome-shaped INSERT functions plus the one SELECT; a new `src/claims_assistant/api/claims_schema.py` owns the API-facing `ClaimResponse` envelope and the `Claim` ORM row → `ClaimResponse` mapping function; a new `src/claims_assistant/api/claims.py` owns the two routes and nothing else (HTTP concerns only — status codes, dependency wiring, translating a raised exception into a 502). `workflow/graph.py` gains one new function, `get_claim_intake_workflow()`, a thin wrapper around the already-existing `build_claim_intake_workflow(settings)` used as a FastAPI dependency — **not cached**. `agent_framework`'s installed `Workflow` class docstring states "Workflow instances contain states and states are preserved across calls to `run`. To execute multiple independent runs, create separate Workflow instances via WorkflowBuilder," and `Workflow.run()` enforces this at runtime (`if self._is_run_active(): raise WorkflowException("Workflow is already running; concurrent runs are not allowed on the same instance.")`, verified directly in `_workflows/_workflow.py`) — a cached singleton reused across two overlapping `POST /claims` requests (trivially likely given this endpoint's 10-30s synchronous duration) would make the second request's `workflow.run()` raise, which the broad `except Exception` in `submit_claim` (Task 3) would then silently mis-persist as a `failed` claim with a misleading error message unrelated to the actual submitted claim. Building fresh per request is cheap and correct instead: Phase 6 already established that constructing the graph (`Agent`/`OpenAIChatCompletionClient`/executor construction, `WorkflowBuilder.build()`) does no eager network I/O, so there's no real cost to rebuilding it per request, and this is literally what the SDK's own docstring recommends.

**Why synchronous, not background-task+polling:** the roadmap's own success criteria reads "a real HTTP request... runs the full pipeline and returns the structured recommendation" — one request, one response. A background-task/job-queue pattern would add a `processing` status, a task runner, and a polling contract that nothing in the spec or roadmap asks for, for a demo-scale capstone where a 10-30s synchronous response over HTTP is unremarkable (no proxy/gateway timeout exists in this stack — `uvicorn` has no default request timeout). GET /claims/{id} still earns its place as a separate endpoint under this design: it's a pure persisted-read, letting an adjuster (or Postman) re-fetch a past result without re-running three LLM calls. Confirmed with the project owner before writing this plan.

**Tech Stack:** `fastapi==0.141.1`, `sqlalchemy[asyncio]==2.0.52` (`asyncpg` driver), `pydantic==2.13.4` — all already-installed, unchanged since Phase 0. `agent-framework-core==1.14.0` (unchanged since Phase 6). No new dependency this phase — `httpx>=0.28.1` (already a dev dependency, used by `fastapi.testclient.TestClient` internally) is used directly for its `AsyncClient`/`ASGITransport`, for a reason verified empirically below, not previously needed in this project.

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§3.1 orchestration graph — this phase is what finally calls it from an API route; §8 error handling — this is where MCP-lookup-failure `ValueError`s stop propagating uncaught; §9 testing — "FastAPI endpoint contracts" tested — as `pytest.mark.integration` tests against real Postgres via a fake workflow double, per this phase's Global Constraints, not literal zero-external-service unit tests, since route logic here is inseparable from DB I/O — and "manual/API-level demo testing... via Swagger/Postman")

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0). Every I/O-bound function is `async def`.
- No new dependency additions this phase — nothing to `uv add`.
- No Alembic/migrations tooling exists in this project (confirmed: no `alembic/`, `migrations/` directory anywhere in the repo). New tables are picked up automatically by `Base.metadata.create_all` — the same mechanism `database.create_all_tables()` and `scripts/seed_db.py` already use for `policies`/`vehicles`/`claims_history`. Adding `Claim` to `models.py` needs no separate migration step; re-running `uv run python scripts/seed_db.py` against your dev Postgres creates the new `claims` table alongside the existing three.
- **Confirmed against the actually-installed packages in this project's `.venv` while writing this plan**: `fastapi==0.141.1`, `sqlalchemy==2.0.52`, `pydantic==2.13.4`, `agent-framework-core==1.14.0`, `asyncpg==0.31.0`, `httpx==0.28.1` — `fastapi`/`sqlalchemy`/`pydantic` all unchanged since Phase 0; `agent-framework-core` unchanged since Phase 6.
- **`Mapped[uuid.UUID]` behavior verified directly** (probed against this exact installed SQLAlchemy against both a compiled `CREATE TABLE` and a real flush): a plain `Mapped[uuid.UUID]` column with no explicit `sqlalchemy.Uuid` annotation compiles to a native Postgres `UUID` column — no extra import needed. `Mapped[dict[str, Any] | None]` combined with an explicit `mapped_column(JSONB)` (`from sqlalchemy.dialects.postgresql import JSONB`) works for nullable JSON columns; nullability itself is correctly auto-inferred from the `X | None` in the type annotation alone (verified: a bare `Mapped[str | None] = mapped_column()` compiles `NULL`-able, `Mapped[str]` compiles `NOT NULL`, no explicit `nullable=` needed either way). Python-side column defaults (`default=uuid.uuid4`, `default=lambda: datetime.datetime.now(datetime.UTC)`) do **not** populate the attribute at object construction time (`Claim(status="completed").id` is `None` right after `__init__`) — they populate at `flush()` time. Combined with this project's existing `expire_on_commit=False` (already set in `database.get_session_factory()`), a `Claim` row's `id`/`created_at` are correctly readable on the same Python object immediately after `session.begin()`'s block exits (which flushes+commits) — no explicit `session.refresh()` needed anywhere in this plan.
- **A real timezone bug was found and reproduced while writing this plan, against real Postgres**: a `Mapped[datetime.datetime]` column with no explicit `DateTime(timezone=True)` compiles to `TIMESTAMP WITHOUT TIME ZONE`; inserting a Python-side default of `datetime.datetime.now(datetime.UTC)` (timezone-*aware*) into that column fails at INSERT time with `asyncpg.exceptions.DataError: ... can't subtract offset-naive and offset-aware datetimes` — reproduced directly against this project's real Postgres, not a hypothetical. Fix, verified working end-to-end (including the real INSERT/round-trip): use `mapped_column(DateTime(timezone=True), default=lambda: datetime.datetime.now(datetime.UTC))` (`from sqlalchemy import DateTime`), making the column `TIMESTAMPTZ` — this is what Task 1's `Claim.created_at` uses.
- **A real event-loop hazard was found and reproduced while writing this plan, and shapes every test in Task 3/4**: this project's DB access is built on two independent module-level singletons — `db.py`'s raw `asyncpg.Pool` (used only by `/health/db`) and `database.py`'s SQLAlchemy `AsyncEngine`/`async_sessionmaker` (used by everything else, including this phase's new code). Both singletons are created lazily on first use and, once created, are bound to whichever `asyncio` event loop was running at that moment. `fastapi.testclient.TestClient` (used by the existing `test_health.py`/`test_health_db.py`) runs the ASGI app through its own internal `anyio` portal thread/loop, **not** the loop `pytest-asyncio` gives async fixtures (this project sets `asyncio_default_test_loop_scope = "session"`, one shared loop for all `pytest-asyncio` async tests/fixtures in a run). Reproduced directly against this project's real `database.py`: a `pytest_asyncio` fixture that calls `create_all_tables()` (touching the SQLAlchemy engine on the pytest-asyncio session loop), followed in the same test session by a **sync** `TestClient` call to a route that also touches that engine, fails with `sqlalchemy.exc.InterfaceError: cannot perform operation: another operation is in progress` (or, for the raw asyncpg pool case, `RuntimeError: ...Future... attached to a different loop`) — a real, reproducible cross-event-loop bug, not a hypothetical. The existing `test_health.py`/`test_health_db.py` happen to be safe only because neither one is ever combined, in the same pytest session, with a separate `pytest_asyncio` async fixture that touches the same singleton first — that's incidental, not a guarantee, and this phase's new tests deliberately do NOT repeat that combination. **Fix, verified working**: use `httpx.AsyncClient(transport=ASGITransport(app=app), base_url="http://test")` inside an `@pytest.mark.asyncio async def` test function instead of the sync `TestClient` — this keeps the fixture and every HTTP call on the exact same `pytest-asyncio` event loop, and was confirmed to pass cleanly (twice, across two separate test functions sharing the module-level cached engine) where the `TestClient` equivalent failed. Every new test in Task 3/4 that combines a DB-touching fixture/setup with an HTTP call to the app uses this pattern, not `TestClient`.
- **`ruff`'s `B008` (`select = ["E", "F", "I", "UP", "B"]` in `pyproject.toml`) flags FastAPI's common `param: T = Depends(...)` default-argument pattern** — confirmed directly (a minimal probe route using that exact form fails `ruff check` with `B008 Do not perform function call Depends in argument defaults`). This is the first phase to use `Depends` at all (Phase 0's `health.py` doesn't). Fix, verified clean under both `ruff check` and `mypy --strict`: FastAPI's `Annotated[T, Depends(...)]` form (`from typing import Annotated`), used throughout `api/claims.py` instead of the bare-default form.
- **`agent_framework.Workflow.run`/`WorkflowRunResult.get_outputs` signatures verified directly against installed source** (`_workflows/_workflow.py`) for this phase specifically because `api/claims.py` is the first non-test file in this project to call `workflow.run(...)` under `mypy --strict` (Phase 6 only ever called it from `tests/`, which aren't strict-checked): `run(message: Any | None = None, *, stream: bool = False, ...)` is `@overload`ed on `stream`; the default (`stream=False`, used here) resolves to `-> Awaitable[WorkflowRunResult]`, so `result = await workflow.run(intake)` types cleanly. `WorkflowRunResult.get_outputs(self) -> list[Any]` — indexing `[0]` and `isinstance()`-narrowing the result is unrestricted (`Any`) and passes `mypy --strict` as written in Task 3.
- **`Workflow` instances are stateful and single-run-at-a-time by the SDK's own documented contract** — verified directly in `_workflows/_workflow.py`: the class docstring says "To execute multiple independent runs, create separate Workflow instances via WorkflowBuilder," and `run()` raises `WorkflowException("Workflow is already running; concurrent runs are not allowed on the same instance.")` if called while a prior run on the same instance hasn't finished (`_is_run_active()`, backed by a weakref cleared only when the run's stream is fully consumed or GC'd). This is why `get_claim_intake_workflow()` (Task 3) builds a fresh `Workflow` per call rather than caching one — see Architecture above.
- **`sqlalchemy.ext.asyncio.AsyncSession.get(entity, ident) -> _O | None`** (generic on `entity`'s class) verified directly against installed source — `session.get(Claim, claim_id)` types as `Claim | None`, used in `claims_repository.get_claim_by_id`.
- **`claims.policy_number`/`claims.vin` are deliberately not foreign keys** to `policies.policy_number`/`vehicles.vin`. Spec §8's "MCP tool call failure (e.g. policy not found)" case must still be persistable as a `failed` claim row with whatever `policy_number` the client submitted, even when that `policy_number` never resolves to a real `Policy` row (that's exactly what "policy not found" means) — an FK constraint would make that INSERT fail, defeating the point.
- **MCP/lookup-failure and validation `ValueError`s, deliberately left uncaught through Phase 6 per that phase's own plan** (`coverage_agent.lookup_policy_by_number`'s "Phase 7 (FastAPI orchestrator endpoints) is where this becomes a caught, surfaced error instead of a propagating exception" comment; same class of `ValueError` from `coverage_agent._validate_citations`, `fraud_agent._call_mcp_tool`/`_validate_assessment`, and Phase 6's own `workflow.executors._incident_date`) — this phase catches all of them, generically, at the single `await workflow.run(...)` call site in `submit_claim` (Task 3), and turns any of them into a persisted `failed` claim row plus an explicit `502` response (spec §8's "surfaces this explicitly... rather than the LLM guessing"). A single broad `except Exception` (not a narrower `except ValueError`) is used deliberately: distinguishing "policy not found" from "citation fabrication caught by our own grounding check" from "MCP server unreachable" would need new custom exception types across three already-shipped agent modules, which is out of scope for this phase and not something the spec asks for — spec §8 treats "MCP tool call failure" as one coarse-grained category to surface explicitly, not a set of distinguishable HTTP error codes.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Tests that need real Postgres (via `docker-compose up -d postgres`) are `pytest.mark.integration`, matching every prior phase's convention — this phase has no unit-testable (zero-external-services) tests beyond the pure `ClaimResponse` mapping function (Task 2), since everything else in this phase's job is either DB I/O or HTTP-wraps-DB-I/O.

---

### Task 1: `Claim` model + `claims_repository.py`

**Files:**
- Modify: `src/claims_assistant/models.py`
- Create: `src/claims_assistant/claims_repository.py`
- Test: `tests/test_claims_repository.py`

**Interfaces:**
- Consumes: `Base` (`models.py`, already defined); `ClaimIntakeRequest`, `ClarificationRequest` (`workflow/messages.py`, already defined); `ClaimRecommendation` (`agents/adjuster_summary_schema.py`, already defined); `get_session_factory` (`database.py`, already defined).
- Produces: `Claim` ORM model (`models.py`) with columns `id: uuid.UUID`, `policy_number: str`, `vin: str`, `narrative_text: str`, `status: str`, `created_at: datetime.datetime`, `recommendation: dict[str, Any] | None`, `clarification: dict[str, Any] | None`, `error_message: str | None`; `create_completed_claim(session, request, recommendation) -> Claim`, `create_clarification_claim(session, request, clarification) -> Claim`, `create_failed_claim(session, request, error_message) -> Claim`, `get_claim_by_id(session, claim_id: uuid.UUID) -> Claim | None` (`claims_repository.py`). Task 2's `claim_response_from_model` consumes `Claim`. Task 3's route handlers import and call all four repository functions.

- [ ] **Step 1: Write the failing repository tests**

Needs real Postgres (`docker-compose up -d postgres`). No `seeded_db` fixture needed — these tests only need the `claims` table to exist, not the seeded policy/vehicle/claims-history fixture data, so each test calls `create_all_tables()` directly (idempotent, safe to call every test).

```python
# tests/test_claims_repository.py
from __future__ import annotations

import uuid

import pytest

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.claims_repository import (
    create_clarification_claim,
    create_completed_claim,
    create_failed_claim,
    get_claim_by_id,
)
from claims_assistant.database import create_all_tables, get_session_factory
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest

pytestmark = pytest.mark.integration

_REQUEST = ClaimIntakeRequest(
    policy_number="POL-CA-0003",
    vin="1C4RJFBG5FC123458",
    narrative_text="Hail damage to my Jeep overnight during a storm.",
)


@pytest.mark.asyncio
async def test_create_completed_claim_persists_and_round_trips():
    await create_all_tables()
    recommendation = ClaimRecommendation(
        policy_number="POL-CA-0003",
        coverage_determination="approve",
        coverage_rationale="clause X covers this",
        coverage_citations=["c1"],
        fraud_risk_score=10,
        fraud_risk_tier="low",
        fraud_red_flags=[],
        fraud_rationale="clean",
        narrative_summary="Hail damage, covered, low risk.",
        recommended_next_step="Approve and close.",
    )
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_completed_claim(session, _REQUEST, recommendation)

    assert claim.id is not None
    assert claim.status == "completed"
    assert claim.policy_number == "POL-CA-0003"
    assert claim.recommendation == recommendation.model_dump(mode="json")
    assert claim.clarification is None
    assert claim.error_message is None

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, claim.id)

    assert fetched is not None
    assert fetched.status == "completed"
    assert fetched.recommendation is not None
    assert fetched.recommendation["coverage_determination"] == "approve"


@pytest.mark.asyncio
async def test_create_clarification_claim_persists_and_round_trips():
    await create_all_tables()
    clarification = ClarificationRequest(
        policy_number="POL-CA-0003",
        reason="low-confidence fields: injuries",
        low_confidence_fields=["injuries"],
        missing_required_fields=[],
        extraction=FNOLExtraction(
            facts=FNOLFacts(
                incident_datetime="2026-07-09T17:15",
                location="Elm Street, Columbus, OH",
                parties=[Party(role="policyholder", name="Priya Natarajan")],
                vehicles=[
                    VehicleInfo(role="policyholder_vehicle", description="Jeep Grand Cherokee")
                ],
                injuries=False,
                narrative_summary="Hail damage.",
            ),
            confidence=FieldConfidence(
                incident_datetime=0.9,
                location=0.9,
                parties=0.9,
                vehicles=0.9,
                injuries=0.3,
                narrative_summary=0.9,
            ),
        ),
    )
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_clarification_claim(session, _REQUEST, clarification)

    assert claim.status == "needs_clarification"
    assert claim.recommendation is None
    assert claim.clarification is not None
    assert claim.clarification["reason"] == "low-confidence fields: injuries"

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, claim.id)

    assert fetched is not None
    assert fetched.clarification is not None
    assert fetched.clarification["low_confidence_fields"] == ["injuries"]


@pytest.mark.asyncio
async def test_create_failed_claim_persists_error_message():
    await create_all_tables()
    session_factory = get_session_factory()

    async with session_factory() as session:
        claim = await create_failed_claim(
            session, _REQUEST, "policy lookup failed for policy_number='POL-CA-0003'"
        )

    assert claim.status == "failed"
    assert claim.recommendation is None
    assert claim.clarification is None
    assert claim.error_message == "policy lookup failed for policy_number='POL-CA-0003'"


@pytest.mark.asyncio
async def test_get_claim_by_id_returns_none_for_unknown_id():
    await create_all_tables()
    session_factory = get_session_factory()

    async with session_factory() as session:
        fetched = await get_claim_by_id(session, uuid.uuid4())

    assert fetched is None
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_claims_repository.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.claims_repository'`

- [ ] **Step 3: Add the `Claim` model**

In `src/claims_assistant/models.py`, add these imports at the top (after the existing `import datetime`):

```python
import uuid
from typing import Any
```

Change the existing `from sqlalchemy import ForeignKey` line to also import `DateTime`:

```python
from sqlalchemy import DateTime, ForeignKey
```

And add this import alongside it:

```python
from sqlalchemy.dialects.postgresql import JSONB
```

Then add this class at the end of the file (after `ClaimHistory`):

```python
class Claim(Base):
    """A persisted claim-intake pipeline run. status is one of: completed,
    needs_clarification, failed. policy_number/vin are deliberately not foreign keys to
    policies/vehicles: a claim can legitimately fail because its policy_number never
    resolved via policy-db-mcp (spec §8), and that row must still be insertable.
    """

    __tablename__ = "claims"

    id: Mapped[uuid.UUID] = mapped_column(primary_key=True, default=uuid.uuid4)
    policy_number: Mapped[str]
    vin: Mapped[str]
    narrative_text: Mapped[str]
    status: Mapped[str]
    created_at: Mapped[datetime.datetime] = mapped_column(
        DateTime(timezone=True), default=lambda: datetime.datetime.now(datetime.UTC)
    )
    recommendation: Mapped[dict[str, Any] | None] = mapped_column(JSONB)
    clarification: Mapped[dict[str, Any] | None] = mapped_column(JSONB)
    error_message: Mapped[str | None] = mapped_column()
```

- [ ] **Step 4: Write the repository module**

```python
# src/claims_assistant/claims_repository.py
from __future__ import annotations

import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.models import Claim
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest


async def create_completed_claim(
    session: AsyncSession, request: ClaimIntakeRequest, recommendation: ClaimRecommendation
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="completed",
        recommendation=recommendation.model_dump(mode="json"),
    )
    async with session.begin():
        session.add(claim)
    return claim


async def create_clarification_claim(
    session: AsyncSession, request: ClaimIntakeRequest, clarification: ClarificationRequest
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="needs_clarification",
        clarification=clarification.model_dump(mode="json"),
    )
    async with session.begin():
        session.add(claim)
    return claim


async def create_failed_claim(
    session: AsyncSession, request: ClaimIntakeRequest, error_message: str
) -> Claim:
    claim = Claim(
        policy_number=request.policy_number,
        vin=request.vin,
        narrative_text=request.narrative_text,
        status="failed",
        error_message=error_message,
    )
    async with session.begin():
        session.add(claim)
    return claim


async def get_claim_by_id(session: AsyncSession, claim_id: uuid.UUID) -> Claim | None:
    return await session.get(Claim, claim_id)
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_claims_repository.py -v -m integration`
Expected: PASS (4 passed)

- [ ] **Step 6: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 7: Commit**

```powershell
git add src/claims_assistant/models.py src/claims_assistant/claims_repository.py tests/test_claims_repository.py
git commit -m "feat: add Claim model and claims repository"
```

---

### Task 2: API response schema

**Files:**
- Create: `src/claims_assistant/api/claims_schema.py`
- Test: `tests/test_claims_schema.py`

**Interfaces:**
- Consumes: `Claim` (`models.py`, Task 1); `ClaimRecommendation` (`agents/adjuster_summary_schema.py`); `ClarificationRequest` (`workflow/messages.py`).
- Produces: `ClaimStatus` (`Literal["completed", "needs_clarification", "failed"]`), `ClaimResponse` (Pydantic: `id: uuid.UUID`, `policy_number: str`, `vin: str`, `narrative_text: str`, `status: ClaimStatus`, `created_at: datetime.datetime`, `recommendation: ClaimRecommendation | None`, `clarification: ClarificationRequest | None`, `error: str | None`), `claim_response_from_model(claim: Claim) -> ClaimResponse`. Task 3's `api/claims.py` imports `ClaimResponse` and `claim_response_from_model`.

This is the only pure unit test in this phase — no DB, no network. A `Claim` ORM instance can be constructed directly with kwargs without a session or engine (confirmed: SQLAlchemy declarative model `__init__` only sets attributes, no I/O).

- [ ] **Step 1: Write the failing schema tests**

```python
# tests/test_claims_schema.py
from __future__ import annotations

import datetime
import uuid

from claims_assistant.api.claims_schema import claim_response_from_model
from claims_assistant.models import Claim

_NOW = datetime.datetime.now(datetime.UTC)


def test_claim_response_from_model_maps_completed_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text="Hail damage.",
        status="completed",
        created_at=_NOW,
        recommendation={
            "policy_number": "POL-CA-0003",
            "coverage_determination": "approve",
            "coverage_rationale": "clause X covers this",
            "coverage_citations": ["c1"],
            "fraud_risk_score": 10,
            "fraud_risk_tier": "low",
            "fraud_red_flags": [],
            "fraud_rationale": "clean",
            "narrative_summary": "Hail damage, covered, low risk.",
            "recommended_next_step": "Approve and close.",
        },
        clarification=None,
        error_message=None,
    )

    response = claim_response_from_model(claim)

    assert response.status == "completed"
    assert response.recommendation is not None
    assert response.recommendation.coverage_determination == "approve"
    assert response.clarification is None
    assert response.error is None


def test_claim_response_from_model_maps_clarification_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text="Something happened, not sure when.",
        status="needs_clarification",
        created_at=_NOW,
        recommendation=None,
        clarification={
            "policy_number": "POL-CA-0003",
            "reason": "low-confidence fields: injuries",
            "low_confidence_fields": ["injuries"],
            "missing_required_fields": [],
            "extraction": {
                "facts": {
                    "incident_datetime": "2026-07-09T17:15",
                    "location": "Elm Street, Columbus, OH",
                    "parties": [{"role": "policyholder", "name": "Priya Natarajan"}],
                    "vehicles": [
                        {"role": "policyholder_vehicle", "description": "Jeep Grand Cherokee"}
                    ],
                    "injuries": False,
                    "narrative_summary": "Hail damage.",
                },
                "confidence": {
                    "incident_datetime": 0.9,
                    "location": 0.9,
                    "parties": 0.9,
                    "vehicles": 0.9,
                    "injuries": 0.3,
                    "narrative_summary": 0.9,
                },
            },
        },
        error_message=None,
    )

    response = claim_response_from_model(claim)

    assert response.status == "needs_clarification"
    assert response.recommendation is None
    assert response.clarification is not None
    assert response.clarification.reason == "low-confidence fields: injuries"


def test_claim_response_from_model_maps_failed_claim():
    claim = Claim(
        id=uuid.uuid4(),
        policy_number="POL-ZZ-9999",
        vin="UNKNOWNVIN0000001",
        narrative_text="...",
        status="failed",
        created_at=_NOW,
        recommendation=None,
        clarification=None,
        error_message="policy lookup failed for policy_number='POL-ZZ-9999'",
    )

    response = claim_response_from_model(claim)

    assert response.status == "failed"
    assert response.recommendation is None
    assert response.clarification is None
    assert response.error == "policy lookup failed for policy_number='POL-ZZ-9999'"
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_claims_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.api.claims_schema'`

- [ ] **Step 3: Write the schema module**

```python
# src/claims_assistant/api/claims_schema.py
from __future__ import annotations

import datetime
import uuid
from typing import Literal, cast

from pydantic import BaseModel

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.models import Claim
from claims_assistant.workflow.messages import ClarificationRequest

ClaimStatus = Literal["completed", "needs_clarification", "failed"]


class ClaimResponse(BaseModel):
    id: uuid.UUID
    policy_number: str
    vin: str
    narrative_text: str
    status: ClaimStatus
    created_at: datetime.datetime
    recommendation: ClaimRecommendation | None = None
    clarification: ClarificationRequest | None = None
    error: str | None = None


def claim_response_from_model(claim: Claim) -> ClaimResponse:
    return ClaimResponse(
        id=claim.id,
        policy_number=claim.policy_number,
        vin=claim.vin,
        narrative_text=claim.narrative_text,
        status=cast(ClaimStatus, claim.status),
        created_at=claim.created_at,
        recommendation=(
            ClaimRecommendation.model_validate(claim.recommendation)
            if claim.recommendation is not None
            else None
        ),
        clarification=(
            ClarificationRequest.model_validate(claim.clarification)
            if claim.clarification is not None
            else None
        ),
        error=claim.error_message,
    )
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_claims_schema.py -v`
Expected: PASS (3 passed)

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/api/claims_schema.py tests/test_claims_schema.py
git commit -m "feat: add ClaimResponse API schema"
```

---

### Task 3: `POST /claims` / `GET /claims/{id}` routes

**Files:**
- Modify: `src/claims_assistant/database.py`
- Modify: `src/claims_assistant/workflow/graph.py`
- Modify: `tests/test_workflow_graph.py`
- Create: `src/claims_assistant/api/claims.py`
- Modify: `src/claims_assistant/main.py`
- Test: `tests/test_claims_api.py`

**Interfaces:**
- Consumes: `get_session_factory` (`database.py`); `build_claim_intake_workflow` (`workflow/graph.py`, Phase 6); `get_settings` (`config.py`); `create_completed_claim`, `create_clarification_claim`, `create_failed_claim`, `get_claim_by_id` (`claims_repository.py`, Task 1); `ClaimResponse`, `claim_response_from_model` (`api/claims_schema.py`, Task 2); `ClaimIntakeRequest` (`workflow/messages.py`); `ClaimRecommendation` (`agents/adjuster_summary_schema.py`).
- Produces: `get_db_session() -> AsyncIterator[AsyncSession]` (`database.py`) — a FastAPI dependency; `get_claim_intake_workflow() -> Workflow` (`workflow/graph.py`) — a FastAPI dependency that builds a **fresh** `Workflow` per call, deliberately not cached (see Architecture and Global Constraints — `Workflow` instances are single-run-at-a-time by the SDK's own contract); `router: APIRouter` (`api/claims.py`) with `POST /claims` and `GET /claims/{claim_id}`. Task 4 imports `get_claim_intake_workflow` (to leave un-overridden, exercising the real one) and reuses this task's `tests/test_claims_api.py` file, appending to it.

- [ ] **Step 1: Write the failing route-contract tests**

These tests use a **fake** workflow (no real Azure OpenAI/MCP/Search calls) injected via `app.dependency_overrides`, and real Postgres for persistence. They use `httpx.AsyncClient` + `ASGITransport` inside `@pytest.mark.asyncio` tests, **not** the sync `TestClient` — see Global Constraints for why that combination is required here.

```python
# tests/test_claims_api.py
from __future__ import annotations

import uuid

import pytest
from httpx import ASGITransport, AsyncClient

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.database import create_all_tables
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.main import create_app
from claims_assistant.workflow.graph import get_claim_intake_workflow
from claims_assistant.workflow.messages import ClarificationRequest

pytestmark = pytest.mark.integration

_REQUEST_BODY = {
    "policy_number": "POL-CA-0003",
    "vin": "1C4RJFBG5FC123458",
    "narrative_text": "Hail damage to my Jeep overnight during a storm.",
}

_RECOMMENDATION = ClaimRecommendation(
    policy_number="POL-CA-0003",
    coverage_determination="approve",
    coverage_rationale="clause X covers this",
    coverage_citations=["c1"],
    fraud_risk_score=10,
    fraud_risk_tier="low",
    fraud_red_flags=[],
    fraud_rationale="clean",
    narrative_summary="Hail damage, covered, low risk.",
    recommended_next_step="Approve and close.",
)

_CLARIFICATION = ClarificationRequest(
    policy_number="POL-CA-0003",
    reason="low-confidence fields: injuries",
    low_confidence_fields=["injuries"],
    missing_required_fields=[],
    extraction=FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-07-09T17:15",
            location="Elm Street, Columbus, OH",
            parties=[Party(role="policyholder", name="Priya Natarajan")],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Jeep Grand Cherokee")
            ],
            injuries=False,
            narrative_summary="Hail damage.",
        ),
        confidence=FieldConfidence(
            incident_datetime=0.9,
            location=0.9,
            parties=0.9,
            vehicles=0.9,
            injuries=0.3,
            narrative_summary=0.9,
        ),
    ),
)


class _FakeWorkflowResult:
    def __init__(self, outputs: list[object]) -> None:
        self._outputs = outputs

    def get_outputs(self) -> list[object]:
        return self._outputs


class _FakeWorkflow:
    def __init__(
        self, outputs: list[object] | None = None, error: Exception | None = None
    ) -> None:
        self._outputs = outputs or []
        self._error = error

    async def run(self, message: object) -> _FakeWorkflowResult:
        if self._error is not None:
            raise self._error
        return _FakeWorkflowResult(self._outputs)


def _client_with_fake_workflow(fake_workflow: _FakeWorkflow) -> AsyncClient:
    app = create_app()
    app.dependency_overrides[get_claim_intake_workflow] = lambda: fake_workflow
    return AsyncClient(transport=ASGITransport(app=app), base_url="http://test")


@pytest.mark.asyncio
async def test_post_claims_returns_201_with_recommendation_for_completed_outcome():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(outputs=[_RECOMMENDATION])

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "completed"
    assert body["recommendation"]["coverage_determination"] == "approve"
    assert body["clarification"] is None
    assert body["error"] is None
    uuid.UUID(body["id"])


@pytest.mark.asyncio
async def test_post_claims_returns_201_with_clarification_for_clarification_outcome():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(outputs=[_CLARIFICATION])

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "needs_clarification"
    assert body["recommendation"] is None
    assert body["clarification"]["reason"] == "low-confidence fields: injuries"


@pytest.mark.asyncio
async def test_post_claims_returns_502_and_persists_failed_claim_when_workflow_raises():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(
        error=ValueError("policy lookup failed for policy_number='POL-CA-0003'")
    )

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.post("/claims", json=_REQUEST_BODY)
        assert response.status_code == 502
        body = response.json()
        assert body["status"] == "failed"
        assert "policy lookup failed" in body["error"]

        get_response = await client.get(f"/claims/{body['id']}")

    assert get_response.status_code == 200
    assert get_response.json()["status"] == "failed"


@pytest.mark.asyncio
async def test_get_claims_returns_404_for_unknown_id():
    await create_all_tables()
    fake_workflow = _FakeWorkflow()

    async with _client_with_fake_workflow(fake_workflow) as client:
        response = await client.get(f"/claims/{uuid.uuid4()}")

    assert response.status_code == 404
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_claims_api.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.api.claims'` (or similar, for `get_claim_intake_workflow` not existing yet)

- [ ] **Step 3: Add the DB session dependency**

In `src/claims_assistant/database.py`, add this import at the top:

```python
from collections.abc import AsyncIterator
```

And add this function at the end of the file:

```python
async def get_db_session() -> AsyncIterator[AsyncSession]:
    session_factory = get_session_factory()
    async with session_factory() as session:
        yield session
```

- [ ] **Step 4: Add the workflow dependency factory**

**Deliberately not cached.** `agent_framework`'s `Workflow` is stateful and single-run-at-a-time by the SDK's own documented contract (Global Constraints) — reusing one cached instance across two overlapping `POST /claims` requests would make the second request's `workflow.run()` raise `WorkflowException`, which `submit_claim` (Step 5) would then mis-persist as a `failed` claim. Building fresh per call is cheap (Phase 6 already established graph construction does no eager network I/O) and matches the SDK's own recommendation.

In `src/claims_assistant/workflow/graph.py`, change the config import line from:

```python
from claims_assistant.config import Settings
```

to:

```python
from claims_assistant.config import Settings, get_settings
```

And add this function at the end of the file:

```python
def get_claim_intake_workflow() -> Workflow:
    return build_claim_intake_workflow(get_settings())
```

Add a direct regression test for the "not cached" property to `tests/test_workflow_graph.py`. This deliberately does **not** go through `app.dependency_overrides` in `test_claims_api.py`'s HTTP-level tests — a dependency override replaces `get_claim_intake_workflow` entirely regardless of whether the real function caches, so an HTTP-level "two concurrent requests" test would pass whether or not this function is cached and give false confidence. Testing the function directly is what actually verifies it.

Change the import line:

```python
from claims_assistant.workflow.graph import build_claim_intake_workflow
```

to:

```python
from claims_assistant.workflow.graph import build_claim_intake_workflow, get_claim_intake_workflow
```

And add this test after `test_build_claim_intake_workflow_builds_without_error`:

```python
@pytest.mark.integration
def test_get_claim_intake_workflow_returns_a_fresh_instance_each_call():
    # agent_framework's Workflow is stateful and single-run-at-a-time by the SDK's own
    # contract (docstring: "To execute multiple independent runs, create separate
    # Workflow instances via WorkflowBuilder"; run() raises WorkflowException if called
    # while a prior run on the same instance is still active). get_claim_intake_workflow
    # must never cache/reuse a single Workflow across calls, or two overlapping
    # POST /claims requests (Phase 7) would race inside that guard.
    workflow_a = get_claim_intake_workflow()
    workflow_b = get_claim_intake_workflow()

    assert workflow_a is not workflow_b
```

Marked `pytest.mark.integration` because `get_claim_intake_workflow()` calls the real `get_settings()` (reads `.env`), matching this file's own existing convention (`test_workflow_produces_claim_recommendation_for_normal_claim` below does the same for the same reason) — no network call actually happens, but the dependency on real `.env` values is what earns the marker here.

Run: `uv run pytest tests/test_workflow_graph.py::test_get_claim_intake_workflow_returns_a_fresh_instance_each_call -v -m integration`
Expected: PASS (1 passed)

- [ ] **Step 5: Write the routes**

```python
# src/claims_assistant/api/claims.py
from __future__ import annotations

import uuid
from typing import Annotated

from agent_framework import Workflow
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import JSONResponse
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.api.claims_schema import ClaimResponse, claim_response_from_model
from claims_assistant.claims_repository import (
    create_clarification_claim,
    create_completed_claim,
    create_failed_claim,
    get_claim_by_id,
)
from claims_assistant.database import get_db_session
from claims_assistant.workflow.graph import get_claim_intake_workflow
from claims_assistant.workflow.messages import ClaimIntakeRequest

router = APIRouter()

WorkflowDep = Annotated[Workflow, Depends(get_claim_intake_workflow)]
SessionDep = Annotated[AsyncSession, Depends(get_db_session)]


@router.post(
    "/claims",
    status_code=201,
    response_model=ClaimResponse,
    responses={
        502: {"model": ClaimResponse, "description": "Claim intake pipeline failed"},
    },
)
async def submit_claim(
    intake: ClaimIntakeRequest, workflow: WorkflowDep, session: SessionDep
) -> ClaimResponse | JSONResponse:
    try:
        result = await workflow.run(intake)
    except Exception as exc:
        claim = await create_failed_claim(session, intake, str(exc))
        return JSONResponse(
            status_code=502,
            content=claim_response_from_model(claim).model_dump(mode="json"),
        )

    outputs = result.get_outputs()
    # Phase 6's graph always yields exactly one terminal output (either branch ends in a
    # single ctx.yield_output call) -- this is a defensive check against a graph-wiring
    # regression, not an expected runtime failure mode, so it stays outside the try/except
    # above: an operational MCP/lookup failure gets persisted as a `failed` claim (spec
    # §8), but a wiring bug that produced zero/multiple outputs is a genuine server defect
    # and should surface as a loud, diagnosable error instead of a misleading claim record.
    assert len(outputs) == 1, f"expected exactly one terminal workflow output, got {len(outputs)}"
    outcome = outputs[0]
    if isinstance(outcome, ClaimRecommendation):
        claim = await create_completed_claim(session, intake, outcome)
    else:
        claim = await create_clarification_claim(session, intake, outcome)
    return claim_response_from_model(claim)


@router.get("/claims/{claim_id}", response_model=ClaimResponse)
async def get_claim(claim_id: uuid.UUID, session: SessionDep) -> ClaimResponse:
    claim = await get_claim_by_id(session, claim_id)
    if claim is None:
        raise HTTPException(status_code=404, detail=f"claim {claim_id} not found")
    return claim_response_from_model(claim)
```

- [ ] **Step 6: Wire the router into the app**

Replace the full contents of `src/claims_assistant/main.py`:

```python
from fastapi import FastAPI

from claims_assistant.api.claims import router as claims_router
from claims_assistant.api.health import router as health_router


def create_app() -> FastAPI:
    app = FastAPI(title="Claims Assistant")
    app.include_router(health_router)
    app.include_router(claims_router)
    return app


app = create_app()
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `uv run pytest tests/test_claims_api.py -v -m integration`
Expected: PASS (4 passed)

Also re-run the full non-integration and integration suites to confirm no regressions from the `main.py`/`database.py`/`workflow/graph.py` edits:

Run: `uv run pytest -v -m "not integration"`
Expected: all pass, same count as before this task plus this task's non-DB-touching additions (none — every new test this task added needs Postgres).

Run: `uv run pytest -v -m integration`
Expected: all pass, including Task 1/2/3's new tests, no regressions in Phases 0-6's integration tests.

- [ ] **Step 8: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 9: Commit**

```powershell
git add src/claims_assistant/database.py src/claims_assistant/workflow/graph.py src/claims_assistant/api/claims.py src/claims_assistant/main.py tests/test_claims_api.py
git commit -m "feat: add POST /claims and GET /claims/{id} endpoints"
```

---

### Task 4: Real end-to-end test + manual Swagger/Postman verification

**Files:**
- Modify: `tests/test_claims_api.py`
- Modify: `docs/superpowers/plans/2026-08-10-roadmap.md`

**Interfaces:**
- Consumes: `create_app` (`main.py`); `get_claim_intake_workflow` (`workflow/graph.py`, Task 3, used un-overridden here — the real one); `seeded_db` fixture (`tests/conftest.py`, already defined).
- Produces: nothing new — this is the roadmap's own success-criteria check.

- [ ] **Step 1: Write the failing end-to-end tests**

These use the **real** `get_claim_intake_workflow()` (no dependency override) — real Azure OpenAI, real Azure AI Search, real MCP-over-Postgres — plus the `seeded_db` fixture (this time the seeded policy/vehicle/claims-history data is actually needed, since the real Coverage/Fraud agents look up `POL-CA-0003`). Reuses the exact two narratives Phase 6's own end-to-end tests already validated (a named-policyholder narrative for the happy path, an unnamed/ambiguous one for the clarification path — Phase 6's execution notes confirmed an *unnamed* policyholder reliably triggers low `parties` confidence in this system, so intentionally not naming one here for the second case).

Append these two tests to the end of `tests/test_claims_api.py` (the fake-workflow tests from Task 3 stay as-is above them):

```python
@pytest.mark.asyncio
async def test_post_claims_full_pipeline_returns_recommendation_via_real_http_request(
    seeded_db,
):
    app = create_app()
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post(
            "/claims",
            json={
                "policy_number": "POL-CA-0003",
                "vin": "1C4RJFBG5FC123458",
                "narrative_text": (
                    "On March 10, 2026, I (Priya Natarajan) discovered hail damage to my "
                    "Jeep Grand Cherokee, which had been parked outside my home overnight "
                    "during a storm in Fresno, CA. No one was hurt; I was not in the "
                    "vehicle at the time."
                ),
            },
        )
        assert response.status_code == 201
        body = response.json()
        assert body["status"] == "completed"
        assert body["recommendation"]["coverage_determination"] in (
            "approve",
            "deny",
            "needs_info",
        )
        assert body["recommendation"]["fraud_risk_tier"] in ("low", "medium", "high")
        assert body["recommendation"]["narrative_summary"]
        assert body["recommendation"]["recommended_next_step"]

        get_response = await client.get(f"/claims/{body['id']}")

    assert get_response.status_code == 200
    assert get_response.json()["status"] == "completed"
    assert get_response.json()["recommendation"] == body["recommendation"]


@pytest.mark.asyncio
async def test_post_claims_routes_low_confidence_extraction_to_clarification_via_real_http(
    seeded_db,
):
    app = create_app()
    async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
        response = await client.post(
            "/claims",
            json={
                "policy_number": "POL-CA-0003",
                "vin": "1C4RJFBG5FC123458",
                "narrative_text": (
                    "Something happened to my car at some point, not totally sure when or "
                    "where, might have been another vehicle involved, might not have been. "
                    "Not sure if anyone got hurt."
                ),
            },
        )

    assert response.status_code == 201
    body = response.json()
    assert body["status"] == "needs_clarification"
    assert body["recommendation"] is None
    assert body["clarification"]["reason"]
```

No new imports needed — `create_app`, `AsyncClient`, `ASGITransport`, and `pytest` are all already imported at the top of `tests/test_claims_api.py` from Task 3.

- [ ] **Step 2: Run the tests**

Needs `docker-compose up -d postgres` (seeded automatically by the `seeded_db` fixture) and real `AZURE_OPENAI_*`/`AZURE_SEARCH_*` values in `.env`.

Run: `uv run pytest tests/test_claims_api.py -v -m integration`
Expected: PASS (6 passed — Task 3's 4 fake-workflow tests plus these 2 real end-to-end tests).

If the first test fails on `coverage_determination`/`fraud_risk_tier` being an unexpected shape: this is the same graph Phase 6 already validated end-to-end without the HTTP layer — check whether the failure is actually happening inside `workflow.run()` (a Phase 6 concern) versus in the new route/persistence code around it (this phase's concern) before changing anything.

If the second test fails because the real Extraction Agent assigned high confidence to the ambiguous narrative: this is the identical fixture Phase 6's own Task 6 already validated reliably triggers clarification in this system — if it doesn't reproduce, re-check nothing about the narrative text was altered in copying it here before suspecting the routing logic.

- [ ] **Step 3: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 4: Manual verification via Swagger**

Per spec §9 ("Manual/API-level demo testing... exercised via the OpenAPI/Swagger docs and Postman collection before considering any milestone 'done'"):

1. Ensure your dev Postgres has the new `claims` table: `uv run python scripts/seed_db.py`.
2. Start the app: `uv run uvicorn claims_assistant.main:app --reload`.
3. Open `http://127.0.0.1:8000/docs`.
4. Expand `POST /claims`, click "Try it out", submit a body like:
   ```json
   {
     "policy_number": "POL-CA-0003",
     "vin": "1C4RJFBG5FC123458",
     "narrative_text": "On March 10, 2026, I (Priya Natarajan) discovered hail damage to my Jeep Grand Cherokee, which had been parked outside my home overnight during a storm in Fresno, CA. No one was hurt; I was not in the vehicle at the time."
   }
   ```
5. Confirm a `201` response with a populated `recommendation` and a real `id` (wait ~10-30s — this is the real pipeline).
6. Copy that `id`, expand `GET /claims/{claim_id}`, paste it in, execute, confirm the same recommendation comes back instantly (no re-run).
7. Submit a deliberately ambiguous narrative to `POST /claims` (e.g. the clarification-path text from Step 1's second test) and confirm `status: "needs_clarification"` with a populated `clarification` object instead.

- [ ] **Step 5: Update the roadmap**

In `docs/superpowers/plans/2026-08-10-roadmap.md`, check off Phase 7:

```markdown
- [x] Phase 7 — FastAPI orchestrator endpoints
```

- [ ] **Step 6: Commit**

```powershell
git add tests/test_claims_api.py docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "test: add end-to-end POST/GET /claims HTTP tests"
```

---

## Definition of Done for Phase 7

- [ ] `uv run pytest -v -m "not integration"` passes with no external services needed (`test_claims_schema.py` plus all prior phases' unit tests, unchanged).
- [ ] With real `AZURE_OPENAI_*`, `AZURE_SEARCH_*` values in `.env` and `docker-compose up -d postgres` running (seeded), `uv run pytest -v -m integration` passes — including this phase's `test_claims_repository.py` (4 tests), `test_claims_api.py` (6 tests), and the new `test_workflow_graph.py` addition (1 test), plus all prior phases' integration tests (no regressions).
- [ ] A real HTTP `POST /claims` request (verified via Swagger, Task 4 Step 4) runs the full pipeline and returns a structured `ClaimRecommendation` (roadmap Phase 7 success criteria).
- [ ] `GET /claims/{id}` reads a previously-persisted claim back without re-running the pipeline.
- [ ] A deliberately low-confidence narrative posted to `POST /claims` returns `status: "needs_clarification"` with the `ClarificationRequest` payload, demonstrating the graph's conditional-handoff path is reachable over HTTP too.
- [ ] An MCP-lookup-failure (or any other operational exception `workflow.run()` raises) is caught once, at the API layer, persisted as a `failed` claim, and surfaced as an explicit `502` — no operational failure reaches the client as an opaque, unpersisted `500` (spec §8). (A malformed graph producing zero/multiple terminal outputs is a distinct, structurally-unreachable-today failure mode that intentionally surfaces as a diagnosable `AssertionError`/500 instead of a misleading persisted claim — see Task 3 Step 5's inline rationale.)
- [ ] `get_claim_intake_workflow()` returns a fresh `Workflow` instance on every call (`test_get_claim_intake_workflow_returns_a_fresh_instance_each_call`, Task 3, added to `test_workflow_graph.py`) — regression coverage for the SDK's single-run-per-instance contract, verified to actually fail if `@lru_cache` is reintroduced (confirmed by deliberately reintroducing it during this plan's own verification pass and watching the test catch it) — an equivalent test driven through `app.dependency_overrides` at the HTTP layer was considered and rejected because overriding the dependency bypasses any caching on the real function entirely, which would pass regardless of whether the underlying bug exists.
- [ ] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [ ] Roadmap doc's Phase 7 checkbox is checked off.
- [ ] Everything above is committed.

Once this is done, we write the Phase 8 (Eval framework) plan next — it can build on the fixtures from Phase 1 and doesn't strictly need Phase 7, but per the roadmap, Phase 7 is the natural checkpoint since the system is now demoable end-to-end over HTTP.
