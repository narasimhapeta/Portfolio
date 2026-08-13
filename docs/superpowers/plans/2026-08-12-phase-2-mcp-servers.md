# Phase 2: MCP Servers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Stand up three independently runnable MCP servers — `policy-db-mcp`, `claims-history-mcp`, `vin-vehicle-mcp` — each wrapping one slice of the Postgres tables seeded in Phase 1 (`policies`, `vehicles`, `claims_history`) via real database queries, not the LLM, so that later agent phases call real systems instead of hallucinating facts.

**Architecture:** Each server is a single module under a new `src/claims_assistant/mcp_servers/` subpackage (module names use underscores — `policy_db.py`, `claims_history.py`, `vin_vehicle.py` — since Python can't import hyphenated names; each module's `MCPServer(...)` instance is still named with the hyphenated form from the spec, e.g. `"policy-db-mcp"`, which is what shows up in any MCP client/inspector). Each module has two layers written top-to-bottom in the same file: a thin **repo layer** (plain `async def` functions taking an `AsyncSession` and returning ORM objects or `None`, built on Phase 1's `get_session_factory()`/`models.py` — independently unit-testable without the MCP protocol in the loop) and a **tool layer** (`@mcp.tool()`-decorated `async def` functions that open a session, call the repo layer, and map the ORM object to a Pydantic result schema). A tool raises `ValueError` when a lookup misses; `MCPServer` converts that into a `CallToolResult` with `is_error=True` and the message as content — this is how "policy not found" / "VIN not found" gets surfaced explicitly per spec §8, instead of a tool silently returning nothing for an LLM to guess around. Every server runs over **stdio transport** (`mcp.run()` under `if __name__ == "__main__"`), started by hand as `uv run python -m claims_assistant.mcp_servers.<name>`; the automated protocol tests spawn the same module directly via `sys.executable -m ...` (skipping `uv run`'s per-invocation sync check, since the test process is already running inside the project's venv) rather than shelling out through `uv` again. HTTP/container-app transport is Phase 10's concern (spec §7), not this phase's.

Policy lookups are by **policy number or VIN** (spec §5.3) — modeled as two separate tools (`get_policy_by_number`, `get_policy_by_vin`) rather than one tool with two optional/nullable parameters, so the tool's input schema is unambiguous to a calling agent and neither repo function has to branch on "which identifier did I get". `get_claims_history` returns not just the raw claim rows but small pre-computed aggregates (`claim_count`, `prior_fraud_flag_count`, `most_recent_claim_date`) — these are exactly the "frequency, recency, prior fraud flags" signals spec §5.3 says `claims-history-mcp` must expose, and computing them once in the tool layer means the Phase 5 Fraud-Risk Agent doesn't have to re-derive them from a raw list in its own prompt/reasoning.

**Tech Stack:** `mcp[cli]` v2.0.0 (the official Model Context Protocol Python SDK — `mcp.server.MCPServer` for building servers; in this release line the old `mcp.server.fastmcp.FastMCP` class was renamed/relocated to `mcp.server.MCPServer`, and `CallToolResult`'s Python attributes are the snake_case `is_error` / `structured_content`, with `isError`/`structuredContent` only as the wire-protocol JSON aliases. `mcp.client.stdio.stdio_client` + `mcp.ClientSession` for the test-client-driven integration tests, `mcp dev` CLI for optional interactive Inspector checks), on top of Phase 1's SQLAlchemy 2.0 async models/session (`database.py`, `models.py`, `seed_data.py`), Pydantic v2 for tool result schemas, pytest + pytest-asyncio (`integration` marker, session-scoped event loop — both already configured from Phase 0/1).

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§5.3 MCP servers, §8 error handling)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All DB-touching functions are `async def` (per Phase 0's async I/O constraint).
- MCP servers wrap the Postgres tables directly — no LLM calls anywhere in this phase (spec §5.3, roadmap Phase 2).
- Tool functions never guess or return partial/plausible data on a miss — they `raise ValueError(...)` with a specific message so the caller sees an explicit failure, not silent bad data (spec §8).
- All DB access goes through Phase 1's `get_session_factory()` — no new engine/connection-pool logic.
- `MCPServer` tool docstrings are protocol metadata (the description an MCP client/agent sees when deciding whether to call the tool) — every `@mcp.tool()` function gets a real one-line docstring, not a generic or omitted one.
- Every dependency addition goes through `uv add`.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Integration tests (`pytest.mark.integration`) require `docker-compose up -d postgres` running first, same as Phase 1. They no longer require a separate manual seed step — the new `seeded_db` fixture (Task 2) calls `create_all_tables()` + `seed_database()` itself.
- **Confirmed against the actually-installed SDK** (`uv pip show mcp` → `2.0.0`, verified by reading `.venv/Lib/site-packages/mcp` directly during Task 1): the server-building class is `mcp.server.MCPServer` (constructor takes `name` as its first positional arg, same `.tool()` decorator, same `.run()` defaulting to stdio transport), not the pre-2.0 `mcp.server.fastmcp.FastMCP`. `ClientSession.call_tool(name, arguments)` (no extra kwargs) returns `types.CallToolResult`, whose Pydantic fields are `is_error: bool | None` and `structured_content: Any | None` — `isError`/`structuredContent` are only the JSON wire-protocol aliases, not the Python attribute names. If a future `uv sync` pulls a different `mcp` version and something below breaks, re-run this same inspection (`uv pip show mcp`, read `mcp/server/__init__.py` and the `CallToolResult` definition in the `mcp_types` package) rather than guessing.

---

### Task 1: MCP SDK dependency + subpackage scaffold

**Files:**
- Modify: `pyproject.toml`, `uv.lock` (via `uv add`)
- Create: `src/claims_assistant/mcp_servers/__init__.py`
- Test: `tests/test_mcp_setup.py`

**Interfaces:**
- Consumes: nothing (first task of the phase).
- Produces: the `mcp` package available for import (`MCPServer`, `ClientSession`, `StdioServerParameters`, `stdio_client`); the `claims_assistant.mcp_servers` subpackage that Tasks 2–4 add one module to each.

- [ ] **Step 1: Add the MCP SDK dependency**

Run (PowerShell):
```powershell
uv add "mcp[cli]"
```

- [ ] **Step 2: Create the subpackage**

Create `src/claims_assistant/mcp_servers/__init__.py` (empty file).

- [ ] **Step 3: Write a smoke test**

```python
# tests/test_mcp_setup.py
from mcp.server import MCPServer


def test_mcpserver_importable():
    server = MCPServer("smoke-test")

    assert server.name == "smoke-test"
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_mcp_setup.py -v`
Expected: PASS (1 passed)

- [ ] **Step 5: Commit**

```powershell
git add src/claims_assistant/mcp_servers/__init__.py tests/test_mcp_setup.py pyproject.toml uv.lock
git commit -m "feat: add MCP SDK dependency and mcp_servers subpackage"
```

---

### Task 2: `policy-db-mcp`

**Files:**
- Create: `tests/conftest.py`
- Create: `src/claims_assistant/mcp_servers/policy_db.py`
- Test: `tests/test_mcp_policy_db.py`
- Test: `tests/test_mcp_policy_db_server.py`

**Interfaces:**
- Consumes: `get_session_factory()`, `create_all_tables()` (Phase 1's `database.py`), `Policy`, `Vehicle` (Phase 1's `models.py`), `seed_database()` (Phase 1's `seed_data.py`).
- Produces: `tests/conftest.py`'s `seeded_db` fixture (reused by Tasks 3 and 4). `policy_db.py`'s `PolicyLookupResult` Pydantic model, `find_policy_by_number(session, policy_number) -> Policy | None`, `find_policy_by_vin(session, vin) -> Policy | None`, and the `get_policy_by_number` / `get_policy_by_vin` MCP tools exposed on a `MCPServer("policy-db-mcp")` instance named `mcp`.

- [ ] **Step 1: Add the shared seeded-database test fixture**

```python
# tests/conftest.py
import pytest_asyncio

from claims_assistant.database import create_all_tables
from claims_assistant.seed_data import seed_database


@pytest_asyncio.fixture
async def seeded_db() -> None:
    await create_all_tables()
    await seed_database()
```

- [ ] **Step 2: Write the failing repo-layer tests**

```python
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `docker-compose up -d postgres` then `uv run pytest tests/test_mcp_policy_db.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.mcp_servers.policy_db'`

- [ ] **Step 4: Write the repo layer**

```python
# src/claims_assistant/mcp_servers/policy_db.py
from __future__ import annotations

from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Policy, Vehicle


class PolicyLookupResult(BaseModel):
    policy_number: str
    policyholder_name: str
    state: str
    coverage_tier: str
    policy_form_id: str
    effective_date: str
    expiration_date: str
    premium_monthly: float


async def find_policy_by_number(session: AsyncSession, policy_number: str) -> Policy | None:
    result = await session.execute(select(Policy).where(Policy.policy_number == policy_number))
    return result.scalar_one_or_none()


async def find_policy_by_vin(session: AsyncSession, vin: str) -> Policy | None:
    result = await session.execute(select(Policy).join(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(policy: Policy) -> PolicyLookupResult:
    return PolicyLookupResult(
        policy_number=policy.policy_number,
        policyholder_name=policy.policyholder_name,
        state=policy.state,
        coverage_tier=policy.coverage_tier,
        policy_form_id=policy.policy_form_id,
        effective_date=policy.effective_date.isoformat(),
        expiration_date=policy.expiration_date.isoformat(),
        premium_monthly=policy.premium_monthly,
    )
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_policy_db.py -v`
Expected: PASS (3 passed)

- [ ] **Step 6: Write the failing protocol-layer tests**

```python
# tests/test_mcp_policy_db_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.policy_db"],
)


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-CA-0002"}
            )

    assert result.is_error is False
    assert result.structured_content is not None
    assert result.structured_content["coverage_tier"] == "full_coverage"
    assert result.structured_content["policy_form_id"] == "CA-FULL-COVERAGE"


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call_errors_for_unknown_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True


@pytest.mark.asyncio
async def test_get_policy_by_vin_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("get_policy_by_vin", {"vin": "5YJ3E1EA7JF123457"})

    assert result.is_error is False
    assert result.structured_content["policy_number"] == "POL-CA-0002"
```

- [ ] **Step 7: Run the tests to verify they fail**

Run: `uv run pytest tests/test_mcp_policy_db_server.py -v`
Expected: FAIL — the subprocess exits without ever speaking the MCP protocol (no `mcp` `MCPServer` instance / `__main__` entrypoint exists yet in `policy_db.py`), so `session.initialize()` raises/fails.

- [ ] **Step 8: Add the tool layer**

Replace the full contents of `src/claims_assistant/mcp_servers/policy_db.py` with:

```python
# src/claims_assistant/mcp_servers/policy_db.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Policy, Vehicle


class PolicyLookupResult(BaseModel):
    policy_number: str
    policyholder_name: str
    state: str
    coverage_tier: str
    policy_form_id: str
    effective_date: str
    expiration_date: str
    premium_monthly: float


async def find_policy_by_number(session: AsyncSession, policy_number: str) -> Policy | None:
    result = await session.execute(select(Policy).where(Policy.policy_number == policy_number))
    return result.scalar_one_or_none()


async def find_policy_by_vin(session: AsyncSession, vin: str) -> Policy | None:
    result = await session.execute(select(Policy).join(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(policy: Policy) -> PolicyLookupResult:
    return PolicyLookupResult(
        policy_number=policy.policy_number,
        policyholder_name=policy.policyholder_name,
        state=policy.state,
        coverage_tier=policy.coverage_tier,
        policy_form_id=policy.policy_form_id,
        effective_date=policy.effective_date.isoformat(),
        expiration_date=policy.expiration_date.isoformat(),
        premium_monthly=policy.premium_monthly,
    )


mcp = MCPServer("policy-db-mcp")


@mcp.tool()
async def get_policy_by_number(policy_number: str) -> PolicyLookupResult:
    """Look up a policy by its policy number. Raises if no such policy exists."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_number(session, policy_number)
    if policy is None:
        raise ValueError(f"no policy found for policy_number={policy_number!r}")
    return _to_result(policy)


@mcp.tool()
async def get_policy_by_vin(vin: str) -> PolicyLookupResult:
    """Look up the policy covering a given vehicle VIN. Raises if no such VIN exists."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        policy = await find_policy_by_vin(session, vin)
    if policy is None:
        raise ValueError(f"no policy found for vin={vin!r}")
    return _to_result(policy)


if __name__ == "__main__":
    mcp.run()
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_policy_db_server.py -v`
Expected: PASS (3 passed)

- [ ] **Step 10 (optional, manual double-check): interactive Inspector**

If you have Node.js installed:
Run: `uv run mcp dev src/claims_assistant/mcp_servers/policy_db.py`
This opens the MCP Inspector in your browser. Call `get_policy_by_number` with `policy_number = "POL-CA-0002"` and confirm you get back the same fields the test above asserts on. This step has no pass/fail assertion — it's a hands-on look at the same protocol the test already verified.

- [ ] **Step 11: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 12: Commit**

```powershell
git add tests/conftest.py src/claims_assistant/mcp_servers/policy_db.py tests/test_mcp_policy_db.py tests/test_mcp_policy_db_server.py
git commit -m "feat: add policy-db-mcp server"
```

---

### Task 3: `claims-history-mcp`

**Files:**
- Create: `src/claims_assistant/mcp_servers/claims_history.py`
- Test: `tests/test_mcp_claims_history.py`
- Test: `tests/test_mcp_claims_history_server.py`

**Interfaces:**
- Consumes: `get_session_factory()` (Phase 1's `database.py`), `ClaimHistory`, `Policy` (Phase 1's `models.py`), `seeded_db` fixture (Task 2's `tests/conftest.py`).
- Produces: `claims_history.py`'s `ClaimSummary` / `ClaimsHistoryResult` Pydantic models, `policy_exists(session, policy_number) -> bool`, `fetch_claims_for_policy(session, policy_number) -> list[ClaimHistory]`, and the `get_claims_history` MCP tool on a `MCPServer("claims-history-mcp")` instance named `mcp`.

- [ ] **Step 1: Write the failing repo-layer tests**

```python
# tests/test_mcp_claims_history.py
import pytest

from claims_assistant.database import get_session_factory
from claims_assistant.mcp_servers.claims_history import fetch_claims_for_policy, policy_exists

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_fetch_claims_for_policy_returns_flagged_history(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        claims = await fetch_claims_for_policy(session, "POL-CA-0002")

    assert len(claims) == 3
    assert sum(1 for c in claims if c.fraud_flag) == 1


@pytest.mark.asyncio
async def test_fetch_claims_for_policy_returns_empty_for_clean_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        claims = await fetch_claims_for_policy(session, "POL-CA-0001")

    assert claims == []


@pytest.mark.asyncio
async def test_policy_exists_is_false_for_unknown_policy(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        exists = await policy_exists(session, "POL-ZZ-9999")

    assert exists is False
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_mcp_claims_history.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.mcp_servers.claims_history'`

- [ ] **Step 3: Write the repo layer**

```python
# src/claims_assistant/mcp_servers/claims_history.py
from __future__ import annotations

from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import ClaimHistory, Policy


class ClaimSummary(BaseModel):
    claim_id: str
    claim_date: str
    claim_type: str
    amount_usd: float
    status: str
    fraud_flag: bool


class ClaimsHistoryResult(BaseModel):
    policy_number: str
    claim_count: int
    prior_fraud_flag_count: int
    most_recent_claim_date: str | None
    claims: list[ClaimSummary]


async def policy_exists(session: AsyncSession, policy_number: str) -> bool:
    result = await session.execute(
        select(Policy.policy_number).where(Policy.policy_number == policy_number)
    )
    return result.scalar_one_or_none() is not None


async def fetch_claims_for_policy(
    session: AsyncSession, policy_number: str
) -> list[ClaimHistory]:
    result = await session.execute(
        select(ClaimHistory)
        .where(ClaimHistory.policy_number == policy_number)
        .order_by(ClaimHistory.claim_date.desc())
    )
    return list(result.scalars().all())


def _to_result(policy_number: str, claims: list[ClaimHistory]) -> ClaimsHistoryResult:
    return ClaimsHistoryResult(
        policy_number=policy_number,
        claim_count=len(claims),
        prior_fraud_flag_count=sum(1 for c in claims if c.fraud_flag),
        most_recent_claim_date=claims[0].claim_date.isoformat() if claims else None,
        claims=[
            ClaimSummary(
                claim_id=c.claim_id,
                claim_date=c.claim_date.isoformat(),
                claim_type=c.claim_type,
                amount_usd=c.amount_usd,
                status=c.status,
                fraud_flag=c.fraud_flag,
            )
            for c in claims
        ],
    )
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_claims_history.py -v`
Expected: PASS (3 passed)

- [ ] **Step 5: Write the failing protocol-layer tests**

```python
# tests/test_mcp_claims_history_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.claims_history"],
)


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_flagged_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0002"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 3
    assert result.structured_content["prior_fraud_flag_count"] == 1


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_clean_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0001"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 0
    assert result.structured_content["most_recent_claim_date"] is None


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_errors_for_unknown_policy(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `uv run pytest tests/test_mcp_claims_history_server.py -v`
Expected: FAIL — no `MCPServer` instance / `__main__` entrypoint in `claims_history.py` yet.

- [ ] **Step 7: Add the tool layer**

Replace the full contents of `src/claims_assistant/mcp_servers/claims_history.py` with:

```python
# src/claims_assistant/mcp_servers/claims_history.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import ClaimHistory, Policy


class ClaimSummary(BaseModel):
    claim_id: str
    claim_date: str
    claim_type: str
    amount_usd: float
    status: str
    fraud_flag: bool


class ClaimsHistoryResult(BaseModel):
    policy_number: str
    claim_count: int
    prior_fraud_flag_count: int
    most_recent_claim_date: str | None
    claims: list[ClaimSummary]


async def policy_exists(session: AsyncSession, policy_number: str) -> bool:
    result = await session.execute(
        select(Policy.policy_number).where(Policy.policy_number == policy_number)
    )
    return result.scalar_one_or_none() is not None


async def fetch_claims_for_policy(
    session: AsyncSession, policy_number: str
) -> list[ClaimHistory]:
    result = await session.execute(
        select(ClaimHistory)
        .where(ClaimHistory.policy_number == policy_number)
        .order_by(ClaimHistory.claim_date.desc())
    )
    return list(result.scalars().all())


def _to_result(policy_number: str, claims: list[ClaimHistory]) -> ClaimsHistoryResult:
    return ClaimsHistoryResult(
        policy_number=policy_number,
        claim_count=len(claims),
        prior_fraud_flag_count=sum(1 for c in claims if c.fraud_flag),
        most_recent_claim_date=claims[0].claim_date.isoformat() if claims else None,
        claims=[
            ClaimSummary(
                claim_id=c.claim_id,
                claim_date=c.claim_date.isoformat(),
                claim_type=c.claim_type,
                amount_usd=c.amount_usd,
                status=c.status,
                fraud_flag=c.fraud_flag,
            )
            for c in claims
        ],
    )


mcp = MCPServer("claims-history-mcp")


@mcp.tool()
async def get_claims_history(policy_number: str) -> ClaimsHistoryResult:
    """Look up prior claims for a policy. Raises if the policy number doesn't exist."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        if not await policy_exists(session, policy_number):
            raise ValueError(f"no policy found for policy_number={policy_number!r}")
        claims = await fetch_claims_for_policy(session, policy_number)
    return _to_result(policy_number, claims)


if __name__ == "__main__":
    mcp.run()
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_claims_history_server.py -v`
Expected: PASS (3 passed)

- [ ] **Step 9 (optional, manual double-check): interactive Inspector**

If you have Node.js installed:
Run: `uv run mcp dev src/claims_assistant/mcp_servers/claims_history.py`
Call `get_claims_history` with `policy_number = "POL-CA-0002"` and confirm `claim_count = 3`, `prior_fraud_flag_count = 1`.

- [ ] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 11: Commit**

```powershell
git add src/claims_assistant/mcp_servers/claims_history.py tests/test_mcp_claims_history.py tests/test_mcp_claims_history_server.py
git commit -m "feat: add claims-history-mcp server"
```

---

### Task 4: `vin-vehicle-mcp`

**Files:**
- Create: `src/claims_assistant/mcp_servers/vin_vehicle.py`
- Test: `tests/test_mcp_vin_vehicle.py`
- Test: `tests/test_mcp_vin_vehicle_server.py`

**Interfaces:**
- Consumes: `get_session_factory()` (Phase 1's `database.py`), `Vehicle` (Phase 1's `models.py`), `seeded_db` fixture (Task 2's `tests/conftest.py`).
- Produces: `vin_vehicle.py`'s `VehicleLookupResult` Pydantic model, `find_vehicle_by_vin(session, vin) -> Vehicle | None`, and the `decode_vin` MCP tool on a `MCPServer("vin-vehicle-mcp")` instance named `mcp`.

- [ ] **Step 1: Write the failing repo-layer tests**

```python
# tests/test_mcp_vin_vehicle.py
import pytest

from claims_assistant.database import get_session_factory
from claims_assistant.mcp_servers.vin_vehicle import find_vehicle_by_vin

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_find_vehicle_by_vin_returns_seeded_vehicle(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, "1FTFW1ET5EF123461")

    assert vehicle is not None
    assert vehicle.make == "Ford"
    assert vehicle.model == "F-150"
    assert vehicle.policy_number == "POL-TX-0006"


@pytest.mark.asyncio
async def test_find_vehicle_by_vin_returns_none_when_missing(seeded_db):
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, "0000000000000UNKN")

    assert vehicle is None
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_mcp_vin_vehicle.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.mcp_servers.vin_vehicle'`

- [ ] **Step 3: Write the repo layer**

```python
# src/claims_assistant/mcp_servers/vin_vehicle.py
from __future__ import annotations

from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Vehicle


class VehicleLookupResult(BaseModel):
    vin: str
    make: str
    model: str
    year: int
    market_value_usd: float
    policy_number: str


async def find_vehicle_by_vin(session: AsyncSession, vin: str) -> Vehicle | None:
    result = await session.execute(select(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(vehicle: Vehicle) -> VehicleLookupResult:
    return VehicleLookupResult(
        vin=vehicle.vin,
        make=vehicle.make,
        model=vehicle.model,
        year=vehicle.year,
        market_value_usd=vehicle.market_value_usd,
        policy_number=vehicle.policy_number,
    )
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_vin_vehicle.py -v`
Expected: PASS (2 passed)

- [ ] **Step 5: Write the failing protocol-layer tests**

```python
# tests/test_mcp_vin_vehicle_server.py
import sys

import pytest
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

pytestmark = pytest.mark.integration

SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.vin_vehicle"],
)


@pytest.mark.asyncio
async def test_decode_vin_tool_call(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "1FTFW1ET5EF123461"})

    assert result.is_error is False
    assert result.structured_content["make"] == "Ford"
    assert result.structured_content["market_value_usd"] == 19750.00
    assert result.structured_content["policy_number"] == "POL-TX-0006"


@pytest.mark.asyncio
async def test_decode_vin_tool_call_errors_for_unknown_vin(seeded_db):
    async with stdio_client(SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "0000000000000UNKN"})

    assert result.is_error is True
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `uv run pytest tests/test_mcp_vin_vehicle_server.py -v`
Expected: FAIL — no `MCPServer` instance / `__main__` entrypoint in `vin_vehicle.py` yet.

- [ ] **Step 7: Add the tool layer**

Replace the full contents of `src/claims_assistant/mcp_servers/vin_vehicle.py` with:

```python
# src/claims_assistant/mcp_servers/vin_vehicle.py
from __future__ import annotations

from mcp.server import MCPServer
from pydantic import BaseModel
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from claims_assistant.database import get_session_factory
from claims_assistant.models import Vehicle


class VehicleLookupResult(BaseModel):
    vin: str
    make: str
    model: str
    year: int
    market_value_usd: float
    policy_number: str


async def find_vehicle_by_vin(session: AsyncSession, vin: str) -> Vehicle | None:
    result = await session.execute(select(Vehicle).where(Vehicle.vin == vin))
    return result.scalar_one_or_none()


def _to_result(vehicle: Vehicle) -> VehicleLookupResult:
    return VehicleLookupResult(
        vin=vehicle.vin,
        make=vehicle.make,
        model=vehicle.model,
        year=vehicle.year,
        market_value_usd=vehicle.market_value_usd,
        policy_number=vehicle.policy_number,
    )


mcp = MCPServer("vin-vehicle-mcp")


@mcp.tool()
async def decode_vin(vin: str) -> VehicleLookupResult:
    """Decode a VIN into make/model/year/market value. Raises if the VIN is unknown."""
    session_factory = get_session_factory()
    async with session_factory() as session:
        vehicle = await find_vehicle_by_vin(session, vin)
    if vehicle is None:
        raise ValueError(f"no vehicle found for vin={vin!r}")
    return _to_result(vehicle)


if __name__ == "__main__":
    mcp.run()
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `uv run pytest tests/test_mcp_vin_vehicle_server.py -v`
Expected: PASS (2 passed)

- [ ] **Step 9 (optional, manual double-check): interactive Inspector**

If you have Node.js installed:
Run: `uv run mcp dev src/claims_assistant/mcp_servers/vin_vehicle.py`
Call `decode_vin` with `vin = "1FTFW1ET5EF123461"` and confirm you get the Ford F-150 / `POL-TX-0006` result.

- [ ] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 11: Commit**

```powershell
git add src/claims_assistant/mcp_servers/vin_vehicle.py tests/test_mcp_vin_vehicle.py tests/test_mcp_vin_vehicle_server.py
git commit -m "feat: add vin-vehicle-mcp server"
```

---

## Definition of Done for Phase 2

- [x] `uv run pytest -v -m "not integration"` passes with no Postgres running (only the `mcp` import smoke test applies here; everything else in this phase is integration-marked since it needs seeded data).
- [x] `docker-compose up -d postgres` then `uv run pytest -v -m integration` passes — repo-layer and MCP-client protocol tests for all three servers, plus Phase 0/1's existing integration tests. (19 passed, 27.45s.)
- [x] Each server has been called at least once with a real MCP client against seeded data (the automated `stdio_client`/`ClientSession` tests above satisfy this; the optional `mcp dev` Inspector steps were also used as a hands-on double-check).
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [x] Roadmap doc's Phase 2 checkbox is checked off.
- [x] Everything above is committed.

**Note (implementation deviation from plan):** the plan assumed the `mcp` Python SDK's pre-2.0 API (`mcp.server.fastmcp.FastMCP`, camelCase `CallToolResult.isError`/`.structuredContent`). `uv add "mcp[cli]"` resolved the newest release, `2.0.0`, which restructured the SDK: the server-building class moved to `mcp.server.MCPServer`, and `CallToolResult`'s Python-side Pydantic fields are snake_case (`is_error`, `structured_content`), with the camelCase names surviving only as JSON wire-protocol aliases. Diagnosed by reading the installed package directly (`.venv/Lib/site-packages/mcp`) rather than guessing; the plan was corrected throughout before Task 1 completed, and Global Constraints records the confirmed API shape for future reference. Separately, `seeded_db` (Task 2) was this project's first `@pytest_asyncio.fixture` — its default per-test loop scope conflicted with `database.py`'s cached module-level `AsyncEngine`, causing cross-loop `RuntimeError`s on the second test onward. Fixed by adding `asyncio_default_fixture_loop_scope = "session"` to `pyproject.toml` alongside Phase 1's existing `asyncio_default_test_loop_scope = "session"`, so fixtures and tests share the one session-long event loop. See commits "fix: scope async fixtures to the session event loop, not just tests" and the three "feat: add \*-mcp server" commits.

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 3 (Extraction Agent) plan next — it will be the first agent, wired to the Microsoft Agent Framework, and will lean on Phase 1's `FNOLFacts` schema and eval fixtures rather than these MCP servers directly.
