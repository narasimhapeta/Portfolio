# Phase 9: Containerization & CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Make the roadmap's Phase 9 deliverable real: Dockerfiles per service, and a GitHub Actions pipeline (lint → test → eval gate) where a PR with a deliberately regressive prompt change fails CI specifically at the eval-gate step, while a clean PR passes every step.

**Architecture:** Two things have to happen before "add a GitHub Actions workflow" is even meaningful, and both were resolved by investigation during planning rather than assumed:

1. **This repo has no GitHub remote.** It needs to become a subdirectory (`AutoClaimsAssistant/`) of the user's existing `github.com/narasimhapeta/Portfolio` monorepo, merged via `git subtree` to preserve its ~50 commits of Phase 0–8 history, with path-scoped CI triggers so this project's pipeline doesn't fire on unrelated Portfolio projects (`AutoInsurance/`, `AutoInsuranceMind/`, `ClaimsService/`) and vice versa.
2. **The three MCP servers are stdio subprocesses today**, spawned in-process by `coverage_agent.py`/`fraud_agent.py` via `stdio_client(StdioServerParameters(...))`. Spec §7 describes "each MCP server as its own container app" — a real, independently-reachable network service, which stdio subprocesses fundamentally aren't. Directly verified against the installed `mcp==2.0.0` SDK: `MCPServer.run(transport="streamable-http", host=..., port=...)` and `mcp.client.streamable_http.streamable_http_client(url)` already exist and are structurally drop-in replacements for the stdio call pattern (`async with client(...) as (read, write): async with ClientSession(read, write) as session: ...`). So Phase 9 does the transport migration now — stdio → streamable-http — rather than deferring the "is this actually a container app" question to Phase 10.

Once both are true, containerization is comparatively mechanical: the project already has a working `Dockerfile` (Phase 0) that installs the full `src/` tree, so the three MCP servers don't need their own Dockerfiles — they reuse the same image with a different `command:` override in Docker Compose (DRY; still satisfies "each MCP server as its own container app" at the process/network level, which is what actually matters for spec §7's KEDA/scale-to-zero story). `docker-compose.yml` grows from Postgres+API to include all three MCP servers, for local dev parity with the eventual Container Apps topology. Tests get a new session-scoped `mcp_servers` pytest fixture that spawns and tears down the three servers as real subprocesses — this is what lets both local `pytest -m integration` and CI's `integration-test`/`eval-gate` jobs stay simple (no separate "start background services and wait for ports" step duplicated in the workflow YAML; the fixture the tests already need does it once).

**Tech Stack:** No new Python dependencies. Reuses the already-installed `mcp[cli]>=2.0.0` SDK's HTTP transport (verified directly, not assumed), the existing `agent_framework`/`pydantic`/`pytest` stack, and adds GitHub Actions (`actions/checkout@v4`, a small local composite action, GitHub-hosted `postgres` service containers) plus a `git subtree`-based repo merge.

**Design decisions resolved during planning (not guessed at):**
- **MCP transport — split into real HTTP services, verified against the installed SDK.** `uv run python -c "from mcp.server import MCPServer; import inspect; print(inspect.signature(MCPServer.run))"` shows `transport: Literal['stdio', 'sse', 'streamable-http'] = 'stdio'`, and `MCPServer.run_streamable_http_async` takes `host`, `port`, `streamable_http_path` (default `/mcp`) and runs a real `uvicorn.Server` under the hood. Client-side, `mcp.client.streamable_http.streamable_http_client(url) -> AsyncContextManager[(read_stream, write_stream)]` has the identical shape `stdio_client` already has, so every call site converts mechanically. `stateless_http=True` is used on all three servers — they're pure per-request DB lookups with no cross-call session state, so there's no reason to pay for sticky-session bookkeeping the client/server would otherwise negotiate.
- **Ports:** `policy-db-mcp` → 8101, `claims-history-mcp` → 8102, `vin-vehicle-mcp` → 8103 (arbitrary, just distinct from Postgres' 5432 and the API's 8000). All three bind `host="0.0.0.0"` — required for container-to-container reachability regardless of local-vs-container use, and harmless locally (still reachable via `localhost`).
- **One Dockerfile, not three.** The existing `Dockerfile` already `COPY`s the entire `src/` tree and installs all dependencies — an MCP server and the FastAPI app need the exact same image, differing only in the process that's actually run. Docker Compose's per-service `command:` override (Task 4) gets each MCP server running from the same `build: .` without duplicating ~15 lines of near-identical Dockerfile three times. This is a deliberate reading of the roadmap's "Dockerfiles per service" as "each service runs as its own container/process" rather than "each service has a literally distinct Dockerfile" — flag this during review if you want three literal Dockerfiles instead; nothing else in the plan depends on which way this goes.
- **A new `mcp_servers` pytest fixture, not a manual "start 3 terminals" step or a CI-YAML wait-loop.** Since streamable-http servers can't be spawned on-demand *inside* a test the way stdio subprocesses could, something has to start them before an integration test runs. A session-scoped fixture (`tests/conftest.py`, Task 2) that `subprocess.Popen`s all three and polls their ports keeps every integration test that needs MCP fully self-contained — `pytest -m integration` "just works" locally exactly as it did before this migration, and the CI workflow (Task 5) needs zero extra orchestration steps to start MCP servers, because the tests already bring their own.
- **CI eval-gate runs the real suite, every PR, with plain repo secrets.** Confirmed empirically in this session: `~5 min` wall-clock for the full 20-fixture suite (3 agents × 2 judges × 10 fixtures) — acceptable for a portfolio project's CI, and it's the only way to actually satisfy the roadmap's literal success criteria (a mocked/cached eval-gate can't catch a real prompt regression, since the whole point is the *real* judge model scoring the *real* weakened output). Secrets are plain GitHub Actions repo secrets, matching the code's existing key-based Azure auth (`AZURE_OPENAI_API_KEY`, `AZURE_SEARCH_API_KEY`) — no AAD/managed-identity code change. Spec §7's OIDC → federated-credentials mention is for the Container Apps *deploy* step (Phase 10, which needs an Azure identity to push images/update revisions), not for calling Azure OpenAI/Search as an API client from a test run; introducing OIDC here would require a code change (switching from API-key auth to `DefaultAzureCredential`) that's out of scope for "containerize and gate."
- **`integration-test` and `eval-gate` are separate CI jobs, `eval-gate` gated on `integration-test` passing.** This is what makes the roadmap's literal success criteria ("fails CI at the eval-gate step", not earlier) checkable rather than just asserted: `tests/test_coverage_agent.py`'s existing assertions only check `determination`/`citations` values, not rationale wording, so the same weakened-prompt regression Phase 8 already used to prove its own grounding check (`coverage_agent.py`'s "use general knowledge to fill gaps" edit) leaves `integration-test` green while `eval-gate`'s grounding-judge composite score drops below baseline — a real, reusable case, not a hypothetical one (Task 6 exercises exactly this).
- **Repo merge preserves history via `git subtree add`, not a fresh copy.** The user's call, made explicit before writing this plan: the 20+ Phase 0–8 commits are themselves part of what a portfolio review would want visible, not just the current file state.

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§7 Deployment & CI/CD — this phase's primary scope; §4 model tiering — no new deployments needed, reuses Phase 5/8's `coverage-agent`/`fraud-risk-agent`/`eval-judge-primary`/`eval-judge-secondary`; §10 working agreement — guided walkthrough, no direct code writes)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0). Every I/O-bound function is `async def`.
- **After Task 1, the project root moves.** All file paths in Tasks 2–6 below are given relative to the new location, `C:\Narasimha\Portfolio\Portfolio\AutoClaimsAssistant\`, prefixed `AutoClaimsAssistant/` in every "Files:" block. The old standalone `C:\Narasimha\AutoClaimsAssistant\` directory is not touched again after Task 1 and can be left as-is or removed later — not a decision this plan needs to make.
- No new Azure resource deployments this phase — reuses `extraction-agent`, `coverage-agent`, `policy-embeddings`, `fraud-risk-agent`, `adjuster-summary-agent`, `eval-judge-primary`, `eval-judge-secondary` exactly as Phase 3/4/5/8 provisioned them.
- **`mcp==2.0.0`'s streamable-http transport was verified directly against the installed package while writing this plan** (not assumed from docs): `MCPServer.run_streamable_http_async(host, port, streamable_http_path="/mcp", stateless_http, ...)` runs a real `uvicorn.Server`; `mcp.client.streamable_http.streamable_http_client(url)` is a drop-in `AsyncContextManager` replacement for `stdio_client`, yielding the same `(read_stream, write_stream)` tuple `ClientSession` already consumes.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Every test that calls a real agent, real MCP server, or touches real Postgres stays `pytest.mark.integration`, matching every prior phase's convention.
- Tasks 1–4 commit directly to `main` (no CI exists yet to gate them, matching Phase 8's own precedent of direct commits). Tasks 5–6 use a feature branch + PR, since exercising the PR/CI flow is the actual point of those tasks.
- No secrets are ever committed to the repo — Azure credentials reach CI exclusively via GitHub Actions repo secrets (Task 5), matching the existing `.env`/`.env.example` pattern already used for local dev.

---

### Task 1: Merge AutoClaimsAssistant into the Portfolio monorepo

**Files:** None (git history operation) — result: `C:\Narasimha\Portfolio\Portfolio\AutoClaimsAssistant\` contains the full project with history preserved.

**Interfaces:**
- Consumes: nothing.
- Produces: a new working root for every subsequent task in this plan.

- [x] **Step 1: Check Portfolio's current state before touching it**

```powershell
cd C:\Narasimha\Portfolio\Portfolio
git status
```

Expected: branch `main`, ahead of `origin/main` by 1 commit, with uncommitted modifications under `AutoInsurance/backend/` (unrelated project — confirmed by reading this directly while planning). The subtree merge below only touches a new `AutoClaimsAssistant/` path, so it won't conflict with these — but commit or stash them first (`git add -A && git commit -m "..."` or `git stash -u`) so the subtree-merge commit's diff stays scoped to just the new subtree, not mixed with unrelated in-progress work.

- [x] **Step 2: Add the standalone repo as a temporary remote and fetch it**

```powershell
git remote add auto-claims-assistant-old C:\Narasimha\AutoClaimsAssistant
git fetch auto-claims-assistant-old
```

Expected: fetch succeeds, pulls in the standalone repo's `master` branch and its ~50 commits as `auto-claims-assistant-old/master` without touching your working tree yet.

- [x] **Step 3: Subtree-merge it in as a new subdirectory, preserving history**

```powershell
git subtree add --prefix=AutoClaimsAssistant auto-claims-assistant-old master -m "chore: merge AutoClaimsAssistant into monorepo via git subtree, preserving history"
```

Expected: one new merge commit on `main`.

- [x] **Step 4: Verify the merge actually preserved history**

```powershell
git log --oneline -5
dir AutoClaimsAssistant
git log --oneline -- AutoClaimsAssistant/pyproject.toml
```

Expected: `AutoClaimsAssistant/` contains `src/`, `tests/`, `docs/`, `pyproject.toml`, `docker-compose.yml`, `Dockerfile`, `.env.example`, `.gitignore`; the last command lists multiple commits touching that file (real preserved history), not just the merge commit.

**Note from actual execution:** `git log --oneline -- <path> | wc -l`-style piped counts were unreliable in this environment (a pager/tty quirk under a piped shell, not a real problem) — undercounted both AutoClaimsAssistant's own commits (looked like 1, actually 71) and this repo's total (looked like 50, actually 139). The authoritative check is `git rev-list --count <ref>`, which never invokes a pager. Verified: Portfolio's own prior history (67 commits) + AutoClaimsAssistant's history (71 commits) + 1 merge commit = 139 total reachable from `HEAD`, with zero overlap between the two sides (`git rev-list --count <merge>^2 --not <merge>^1` = 71, i.e. all of AutoClaimsAssistant's history is genuinely unique). If you see a suspiciously low count from a piped `git log | wc -l` command during this walkthrough, re-check with `git rev-list --count` before assuming something's wrong.

- [x] **Step 5: Remove the temporary remote**

```powershell
git remote remove auto-claims-assistant-old
```

This is harmless — the fetched commits are already part of `main`'s history; this just removes the now-unneeded remote-tracking pointer.

- [x] **Step 6: Push**

```powershell
git push origin main
```

Note: this also pushes the pre-existing AutoInsurance commit from Step 1 if you haven't pushed it separately — confirm that's fine before pushing, or push in two steps if you'd rather keep them apart.

- [x] **Step 7: Set up the working environment in the new location**

`.venv/` and `.env` were never tracked by git (per `AutoClaimsAssistant/.gitignore`), so they don't come across in the merge — recreate them:

```powershell
cd C:\Narasimha\Portfolio\Portfolio\AutoClaimsAssistant
uv sync --group dev
copy C:\Narasimha\AutoClaimsAssistant\.env .env
```

- [x] **Step 8: Confirm the test suite still runs from the new location**

```powershell
uv run pytest -v -m "not integration"
```

Expected: same pass count it had in the old standalone repo — nothing about the merge should have changed behavior, only location.

**Actual result: 84 passed, 53 deselected.** Task 1 complete.

From here on, all work happens in `C:\Narasimha\Portfolio\Portfolio\AutoClaimsAssistant\`, not the old standalone directory.

---

### Task 2: MCP servers — switch to streamable-http transport

**Files:**
- Modify: `AutoClaimsAssistant/src/claims_assistant/config.py`
- Modify: `AutoClaimsAssistant/.env.example`
- Modify: `AutoClaimsAssistant/tests/test_config.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/mcp_servers/policy_db.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/mcp_servers/claims_history.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/mcp_servers/vin_vehicle.py`
- Modify: `AutoClaimsAssistant/tests/conftest.py`
- Modify: `AutoClaimsAssistant/tests/test_mcp_policy_db_server.py`
- Modify: `AutoClaimsAssistant/tests/test_mcp_claims_history_server.py`
- Modify: `AutoClaimsAssistant/tests/test_mcp_vin_vehicle_server.py`

**Interfaces:**
- Produces: `Settings.policy_db_mcp_url: str`, `Settings.claims_history_mcp_url: str`, `Settings.vin_vehicle_mcp_url: str` — consumed by Task 3's client-side migration. `mcp_servers` pytest fixture (session-scoped, `tests/conftest.py`) — consumed by every integration test touched in Task 3 and by CI (Task 5) with zero extra orchestration.

- [x] **Step 1: Extend the failing config test**

Add to `AutoClaimsAssistant/tests/test_config.py`'s `test_settings_reads_from_env`, right after the existing `AZURE_SEARCH_INDEX_NAME` `monkeypatch.setenv` line:

```python
    monkeypatch.setenv("POLICY_DB_MCP_URL", "http://policy-db-test:8101/mcp")
    monkeypatch.setenv("CLAIMS_HISTORY_MCP_URL", "http://claims-history-test:8102/mcp")
    monkeypatch.setenv("VIN_VEHICLE_MCP_URL", "http://vin-vehicle-test:8103/mcp")
```

And these assertions, right after the existing `azure_search_index_name` assertion:

```python
    assert settings.policy_db_mcp_url == "http://policy-db-test:8101/mcp"
    assert settings.claims_history_mcp_url == "http://claims-history-test:8102/mcp"
    assert settings.vin_vehicle_mcp_url == "http://vin-vehicle-test:8103/mcp"
```

- [x] **Step 2: Run to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'policy_db_mcp_url'`

- [x] **Step 3: Add the settings fields**

In `AutoClaimsAssistant/src/claims_assistant/config.py`, add these three lines right after the existing `azure_search_index_name: str = "policy-documents"` line:

```python
    policy_db_mcp_url: str = "http://localhost:8101/mcp"
    claims_history_mcp_url: str = "http://localhost:8102/mcp"
    vin_vehicle_mcp_url: str = "http://localhost:8103/mcp"
```

- [x] **Step 4: Update `.env.example`**

Add to `AutoClaimsAssistant/.env.example`, after the existing `AZURE_SEARCH_INDEX_NAME` line:

```
POLICY_DB_MCP_URL=http://localhost:8101/mcp
CLAIMS_HISTORY_MCP_URL=http://localhost:8102/mcp
VIN_VEHICLE_MCP_URL=http://localhost:8103/mcp
```

Also add the same three lines to your real `.env` — the defaults already match local dev, so this is mostly documentation, but keep `.env.example` and `.env` in sync per existing convention.

- [x] **Step 5: Run the tests to verify they pass, then lint/type-check**

Run: `uv run pytest tests/test_config.py -v` — Expected: PASS
Run: `uv run ruff check .` and `uv run mypy src` — Expected: both clean

- [x] **Step 6: Switch the three MCP servers to streamable-http**

In `AutoClaimsAssistant/src/claims_assistant/mcp_servers/policy_db.py`, replace the final two lines:

```python
if __name__ == "__main__":
    mcp.run()
```

with:

```python
if __name__ == "__main__":
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8101, stateless_http=True)
```

In `AutoClaimsAssistant/src/claims_assistant/mcp_servers/claims_history.py`, the same replacement, port 8102:

```python
if __name__ == "__main__":
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8102, stateless_http=True)
```

In `AutoClaimsAssistant/src/claims_assistant/mcp_servers/vin_vehicle.py`, port 8103:

```python
if __name__ == "__main__":
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8103, stateless_http=True)
```

- [x] **Step 7: Add the `mcp_servers` fixture**

Replace `AutoClaimsAssistant/tests/conftest.py` entirely with:

```python
# tests/conftest.py
import socket
import subprocess
import sys
import time
from collections.abc import Iterator

import pytest
import pytest_asyncio

from claims_assistant.database import create_all_tables
from claims_assistant.seed_data import seed_database


@pytest_asyncio.fixture
async def seeded_db() -> None:
    await create_all_tables()
    await seed_database()


_MCP_SERVER_MODULES = [
    "claims_assistant.mcp_servers.policy_db",
    "claims_assistant.mcp_servers.claims_history",
    "claims_assistant.mcp_servers.vin_vehicle",
]
_MCP_SERVER_PORTS = [8101, 8102, 8103]


def _wait_for_port(port: int, timeout: float = 10.0) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            if sock.connect_ex(("localhost", port)) == 0:
                return
        time.sleep(0.2)
    raise TimeoutError(f"MCP server on port {port} did not start within {timeout}s")


@pytest.fixture(scope="session")
def mcp_servers() -> Iterator[None]:
    processes = [
        subprocess.Popen([sys.executable, "-m", module]) for module in _MCP_SERVER_MODULES
    ]
    try:
        for port in _MCP_SERVER_PORTS:
            _wait_for_port(port)
        yield
    finally:
        for process in processes:
            process.terminate()
        for process in processes:
            process.wait(timeout=5)
```

- [x] **Step 8: Rewrite the three MCP server integration tests**

Replace `AutoClaimsAssistant/tests/test_mcp_policy_db_server.py` entirely:

```python
# tests/test_mcp_policy_db_server.py
import pytest
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_get_policy_by_number_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
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
async def test_get_policy_by_number_tool_call_errors_for_unknown_policy(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True


@pytest.mark.asyncio
async def test_get_policy_by_vin_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("get_policy_by_vin", {"vin": "5YJ3E1EA7JF123457"})

    assert result.is_error is False
    assert result.structured_content["policy_number"] == "POL-CA-0002"
```

Replace `AutoClaimsAssistant/tests/test_mcp_claims_history_server.py` entirely:

```python
# tests/test_mcp_claims_history_server.py
import pytest
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_flagged_policy(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.claims_history_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0002"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 3
    assert result.structured_content["prior_fraud_flag_count"] == 1


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_for_clean_policy(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.claims_history_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-CA-0001"}
            )

    assert result.is_error is False
    assert result.structured_content["claim_count"] == 0
    assert result.structured_content["most_recent_claim_date"] is None


@pytest.mark.asyncio
async def test_get_claims_history_tool_call_errors_for_unknown_policy(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.claims_history_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_claims_history", {"policy_number": "POL-ZZ-9999"}
            )

    assert result.is_error is True
```

Replace `AutoClaimsAssistant/tests/test_mcp_vin_vehicle_server.py` entirely:

```python
# tests/test_mcp_vin_vehicle_server.py
import pytest
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_decode_vin_tool_call(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.vin_vehicle_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "1FTFW1ET5EF123461"})

    assert result.is_error is False
    assert result.structured_content["make"] == "Ford"
    assert result.structured_content["market_value_usd"] == 19750.00
    assert result.structured_content["policy_number"] == "POL-TX-0006"


@pytest.mark.asyncio
async def test_decode_vin_tool_call_errors_for_unknown_vin(seeded_db, mcp_servers):
    settings = get_settings()
    async with streamable_http_client(settings.vin_vehicle_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool("decode_vin", {"vin": "0000000000000UNKN"})

    assert result.is_error is True
```

- [x] **Step 9: Run to verify they pass**

Run: `uv run pytest tests/test_mcp_policy_db_server.py tests/test_mcp_claims_history_server.py tests/test_mcp_vin_vehicle_server.py -v -m integration`
Expected: PASS (8 passed). The `mcp_servers` fixture starts all three servers once for the whole test session — first test in the run will take a couple seconds longer while they boot.

- [x] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 11: Commit**

```powershell
git add src/claims_assistant/config.py .env.example tests/test_config.py src/claims_assistant/mcp_servers/policy_db.py src/claims_assistant/mcp_servers/claims_history.py src/claims_assistant/mcp_servers/vin_vehicle.py tests/conftest.py tests/test_mcp_policy_db_server.py tests/test_mcp_claims_history_server.py tests/test_mcp_vin_vehicle_server.py
git commit -m "feat: switch MCP servers to streamable-http transport"
```

---

### Task 3: Thread settings through Coverage/Fraud MCP lookups

Every caller of the three lookup functions needs a way to reach the URLs Task 2 added. `determine_coverage` already threads `Settings` through; `assess_fraud_risk` didn't need to before (stdio subprocesses needed no config) and now does. This is one task, not several, because the signature change is only valid if every caller is updated in the same commit — a partial version wouldn't import.

**Files:**
- Modify: `AutoClaimsAssistant/src/claims_assistant/agents/coverage_agent.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/agents/fraud_agent.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/workflow/executors.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/workflow/graph.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/eval/coverage_eval.py`
- Modify: `AutoClaimsAssistant/src/claims_assistant/eval/fraud_eval.py`
- Modify: `AutoClaimsAssistant/tests/test_fraud_agent.py`
- Modify: `AutoClaimsAssistant/tests/test_coverage_agent.py`
- Modify: `AutoClaimsAssistant/tests/test_coverage_eval_runner.py`
- Modify: `AutoClaimsAssistant/tests/test_fraud_eval_runner.py`
- Modify: `AutoClaimsAssistant/tests/test_eval_suite.py`
- Modify: `AutoClaimsAssistant/tests/test_workflow_graph.py`

**Interfaces:**
- `lookup_policy_by_number(settings: Settings, policy_number: str) -> PolicyLookupResult` (was `(policy_number: str)`).
- `lookup_claims_history(settings: Settings, policy_number: str) -> ClaimsHistoryResult`, `lookup_vehicle_by_vin(settings: Settings, vin: str) -> VehicleLookupResult` (both gain a leading `settings` param).
- `assess_fraud_risk(agent: Agent, settings: Settings, policy_number: str, vin: str, incident_date: str, claim_narrative: str) -> FraudRiskAssessment` (gains `settings` as the 2nd positional param, matching `determine_coverage`'s existing `agent, settings, ...` shape).
- `FraudRiskExecutor.__init__(self, agent: Agent, settings: Settings, *, id: str = "fraud_risk")` (gains `settings`).
- `run_fraud_eval(fraud_agent: Agent, judge_primary: Agent, judge_secondary: Agent, settings: Settings, fixtures: list[FraudFixture]) -> list[EvalResult]` (gains `settings`, inserted to match `run_coverage_eval`'s existing param order).

- [x] **Step 1: `coverage_agent.py` — switch to streamable-http and thread settings**

Replace the top imports (drop `import sys`, `StdioServerParameters`, `stdio_client`; add `streamable_http_client`):

```python
# src/claims_assistant/agents/coverage_agent.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.config import Settings
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.search.retrieval import RetrievedChunk, retrieve_policy_chunks
```

Delete the `_POLICY_DB_SERVER_PARAMS = StdioServerParameters(...)` block entirely.

Replace `lookup_policy_by_number`:

```python
async def lookup_policy_by_number(settings: Settings, policy_number: str) -> PolicyLookupResult:
    # Raises rather than returning a structured "lookup failed" output (spec §8 describes
    # the latter) — there's no API layer yet to translate this into a response; Phase 7
    # (FastAPI orchestrator endpoints) is where this becomes a caught, surfaced error
    # instead of a propagating exception.
    async with streamable_http_client(settings.policy_db_mcp_url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": policy_number}
            )
    if result.is_error:
        raise ValueError(f"policy lookup failed for policy_number={policy_number!r}")
    assert result.structured_content is not None
    return PolicyLookupResult.model_validate(result.structured_content)
```

In `determine_coverage`, update the one call site:

```python
    policy = await lookup_policy_by_number(settings, policy_number)
```

- [x] **Step 2: `fraud_agent.py` — switch to streamable-http and thread settings**

Replace the top imports (drop `import sys`, `StdioServerParameters`, `stdio_client`; add `streamable_http_client`):

```python
# src/claims_assistant/agents/fraud_agent.py
from __future__ import annotations

from typing import Literal, cast

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

from claims_assistant.agents.coverage_agent import lookup_policy_by_number
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import (
    FraudSignals,
    RedFlagCode,
    compute_fraud_signals,
    determine_actual_red_flags,
)
from claims_assistant.config import Settings
from claims_assistant.mcp_servers.claims_history import ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult
```

Delete the `_CLAIMS_HISTORY_SERVER_PARAMS` and `_VIN_VEHICLE_SERVER_PARAMS` blocks entirely.

Replace `_call_mcp_tool` and the two lookup functions:

```python
async def _call_mcp_tool(
    url: str, tool_name: str, arguments: dict[str, str]
) -> dict[str, object]:
    async with streamable_http_client(url) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(tool_name, arguments)
    if result.is_error:
        raise ValueError(f"{tool_name} failed for arguments={arguments!r}")
    assert result.structured_content is not None
    return cast(dict[str, object], result.structured_content)


async def lookup_claims_history(settings: Settings, policy_number: str) -> ClaimsHistoryResult:
    content = await _call_mcp_tool(
        settings.claims_history_mcp_url,
        "get_claims_history",
        {"policy_number": policy_number},
    )
    return ClaimsHistoryResult.model_validate(content)


async def lookup_vehicle_by_vin(settings: Settings, vin: str) -> VehicleLookupResult:
    content = await _call_mcp_tool(settings.vin_vehicle_mcp_url, "decode_vin", {"vin": vin})
    return VehicleLookupResult.model_validate(content)
```

Update `assess_fraud_risk`'s signature and body:

```python
async def assess_fraud_risk(
    agent: Agent,
    settings: Settings,
    policy_number: str,
    vin: str,
    incident_date: str,
    claim_narrative: str,
) -> FraudRiskAssessment:
    policy = await lookup_policy_by_number(settings, policy_number)
    claims_history = await lookup_claims_history(settings, policy_number)
    vehicle = await lookup_vehicle_by_vin(settings, vin)
    signals = compute_fraud_signals(policy, claims_history, vehicle, incident_date)
    actual_flags = determine_actual_red_flags(signals)
    prompt = _build_prompt(policy, signals, actual_flags, claim_narrative)
    response = await agent.run(
        prompt, options=ChatOptions(response_format=FraudRiskAssessment)
    )
    assessment = response.value
    assert isinstance(assessment, FraudRiskAssessment)
    _validate_assessment(assessment, signals)
    return assessment
```

- [x] **Step 3: `executors.py` — `FraudRiskExecutor` gains `settings`**

Replace the `FraudRiskExecutor` class:

```python
class FraudRiskExecutor(Executor):
    def __init__(self, agent: Agent, settings: Settings, *, id: str = "fraud_risk") -> None:
        super().__init__(id=id)
        self._agent = agent
        self._settings = settings

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[FraudOutcome]) -> None:
        incident_date = _incident_date(message.extraction.facts.incident_datetime)
        assessment = await assess_fraud_risk(
            self._agent,
            self._settings,
            message.request.policy_number,
            message.request.vin,
            incident_date,
            message.request.narrative_text,
        )
        await ctx.send_message(
            FraudOutcome(policy_number=message.request.policy_number, assessment=assessment)
        )
```

- [x] **Step 4: `graph.py` — pass settings when constructing `FraudRiskExecutor`**

Update the one line in `build_claim_intake_workflow`:

```python
    fraud_risk = FraudRiskExecutor(build_fraud_agent(settings), settings)
```

- [x] **Step 5: `eval/coverage_eval.py` — update the one call site**

```python
        policy = await lookup_policy_by_number(settings, fixture.policy_number)
```

- [x] **Step 6: `eval/fraud_eval.py` — add settings param and thread it through**

Add the import:

```python
from claims_assistant.config import Settings
```

Update `run_fraud_eval`:

```python
async def run_fraud_eval(
    fraud_agent: Agent,
    judge_primary: Agent,
    judge_secondary: Agent,
    settings: Settings,
    fixtures: list[FraudFixture],
) -> list[EvalResult]:
    results = []
    for fixture in fixtures:
        assessment = await assess_fraud_risk(
            fraud_agent,
            settings,
            fixture.policy_number,
            fixture.vin,
            fixture.incident_date,
            fixture.claim_narrative,
        )
        tier_correct = float(assessment.risk_tier == fixture.gold_risk_tier)

        policy = await lookup_policy_by_number(settings, fixture.policy_number)
        claims_history = await lookup_claims_history(settings, fixture.policy_number)
        vehicle = await lookup_vehicle_by_vin(settings, fixture.vin)
```

(the rest of the function body is unchanged — only the first three lookup-related lines above change).

- [x] **Step 7: Update `tests/test_fraud_agent.py`**

Both test functions: add `mcp_servers` to the signature and `settings` as the 2nd positional arg to `assess_fraud_risk`.

```python
@pytest.mark.asyncio
async def test_clean_claim_on_low_history_policy_is_low_risk(seeded_db, mcp_servers):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
        settings,
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        incident_date="2026-03-10",
        claim_narrative=(
            "Hail damage to my Jeep Grand Cherokee while it was parked outside my "
            "home overnight during a storm."
        ),
    )

    assert result.risk_tier == "low"
    assert result.red_flags == []


@pytest.mark.asyncio
async def test_theft_claim_shortly_after_policy_start_with_prior_fraud_is_high_risk(
    seeded_db, mcp_servers
):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
        settings,
        policy_number="POL-TX-0006",
        vin="1FTFW1ET5EF123461",
        incident_date="2025-08-01",
        claim_narrative=(
            "My Ford F-150 was stolen overnight from a parking lot; I don't have "
            "any other details."
        ),
    )

    assert result.risk_tier == "high"
    assert result.risk_score >= 67
    assert "prior_fraud_flag" in result.red_flags
    assert "recent_policy_inception" in result.red_flags
    assert len(result.rationale) > 0
```

- [x] **Step 8: Add `mcp_servers` to the remaining tests that exercise real MCP calls**

`tests/test_coverage_agent.py` — add `mcp_servers` to all three test function signatures (no call-site changes needed; `determine_coverage`'s own signature didn't change):

```python
async def test_collision_claim_on_full_coverage_policy_is_approved(seeded_db, mcp_servers):
```
```python
async def test_collision_claim_on_liability_only_policy_is_denied(seeded_db, mcp_servers):
```
```python
async def test_delivery_use_collision_with_unstated_endorsement_needs_info(seeded_db, mcp_servers):
```

`tests/test_coverage_eval_runner.py` — add `mcp_servers`:

```python
async def test_run_coverage_eval_returns_one_result_per_fixture(seeded_db, mcp_servers):
```

`tests/test_fraud_eval_runner.py` — add `mcp_servers` and the new `settings` argument:

```python
@pytest.mark.asyncio
async def test_run_fraud_eval_returns_one_result_per_fixture(seeded_db, mcp_servers):
    settings = get_settings()
    fraud_agent = build_fraud_agent(settings)
    judge_primary = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )
    fixtures = load_fraud_fixtures()

    results = await run_fraud_eval(fraud_agent, judge_primary, judge_secondary, settings, fixtures)

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "fraud"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score in (0.0, 1.0)
        assert result.primary_judge_grounded is not None
        assert result.secondary_judge_grounded is not None
```

`tests/test_eval_suite.py` — add `mcp_servers` and update the `run_fraud_eval` call:

```python
async def test_eval_suite_produces_report_above_baseline(seeded_db, mcp_servers):
```

```python
    fraud_results = await run_fraud_eval(
        build_fraud_agent(settings), judge_primary, judge_secondary, settings, load_fraud_fixtures()
    )
```

`tests/test_workflow_graph.py` — add `mcp_servers` only to the one test that actually reaches Coverage+Fraud (the low-confidence test short-circuits to clarification before the fan-out, so it never calls MCP):

```python
async def test_workflow_produces_claim_recommendation_for_normal_claim(seeded_db, mcp_servers):
```

- [x] **Step 9: Run the full affected test set**

Run: `uv run pytest tests/test_coverage_agent.py tests/test_fraud_agent.py tests/test_coverage_eval_runner.py tests/test_fraud_eval_runner.py tests/test_workflow_graph.py tests/test_eval_suite.py -v -m integration`
Expected: all PASS, same counts as before this task (no behavior changed, only how MCP servers are reached).

- [x] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 11: Commit**

```powershell
git add src/claims_assistant/agents/coverage_agent.py src/claims_assistant/agents/fraud_agent.py src/claims_assistant/workflow/executors.py src/claims_assistant/workflow/graph.py src/claims_assistant/eval/coverage_eval.py src/claims_assistant/eval/fraud_eval.py tests/test_fraud_agent.py tests/test_coverage_agent.py tests/test_coverage_eval_runner.py tests/test_fraud_eval_runner.py tests/test_eval_suite.py tests/test_workflow_graph.py
git commit -m "feat: thread settings through Coverage/Fraud MCP lookups for streamable-http"
```

---

### Task 4: Extend `docker-compose.yml` to all services

**Files:**
- Modify: `AutoClaimsAssistant/docker-compose.yml`

**Interfaces:**
- Consumes: the same `Dockerfile` (unchanged) via per-service `command:` overrides.
- Produces: a full local stack (`postgres`, three MCP servers, `api`) matching the eventual Container Apps topology.

- [x] **Step 1: Replace `docker-compose.yml`**

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

  policy-db-mcp:
    build: .
    command: uv run python -m claims_assistant.mcp_servers.policy_db
    environment:
      POSTGRES_HOST: postgres
    ports:
      - "8101:8101"
    depends_on:
      postgres:
        condition: service_healthy

  claims-history-mcp:
    build: .
    command: uv run python -m claims_assistant.mcp_servers.claims_history
    environment:
      POSTGRES_HOST: postgres
    ports:
      - "8102:8102"
    depends_on:
      postgres:
        condition: service_healthy

  vin-vehicle-mcp:
    build: .
    command: uv run python -m claims_assistant.mcp_servers.vin_vehicle
    environment:
      POSTGRES_HOST: postgres
    ports:
      - "8103:8103"
    depends_on:
      postgres:
        condition: service_healthy

  api:
    build: .
    env_file: .env
    environment:
      POSTGRES_HOST: postgres
      POLICY_DB_MCP_URL: http://policy-db-mcp:8101/mcp
      CLAIMS_HISTORY_MCP_URL: http://claims-history-mcp:8102/mcp
      VIN_VEHICLE_MCP_URL: http://vin-vehicle-mcp:8103/mcp
    ports:
      - "8000:8000"
    depends_on:
      postgres:
        condition: service_healthy
      policy-db-mcp:
        condition: service_started
      claims-history-mcp:
        condition: service_started
      vin-vehicle-mcp:
        condition: service_started

volumes:
  pgdata:
```

- [x] **Step 2: Bring up the full stack and smoke-test it**

```powershell
docker-compose up -d --build
docker-compose ps
```

Expected: all five containers (`postgres`, three MCP servers, `api`) show `running`/`healthy`.

- [x] **Step 3: Manually exercise the API through the running stack (spec §9's manual-testing requirement)**

Open `http://localhost:8000/docs` and submit a `POST /claims` request through Swagger with a real policy number/VIN from the seeded data (e.g. `POL-CA-0003` / `1C4RJFBG5FC123458`) — confirms the containerized `api` service can actually reach the containerized MCP servers over the compose network, not just that each container starts.

Expected: `201` response with a structured recommendation, same shape as Phase 7's manual verification.

- [x] **Step 4: Tear down**

```powershell
docker-compose down
```

- [x] **Step 5: Commit**

```powershell
git add docker-compose.yml
git commit -m "feat: extend docker-compose to all services for local dev parity"
```

---

### Task 5: GitHub Actions pipeline — lint → test → eval gate

**Files:**
- Create: `.github/actions/setup-claims-assistant/action.yml` (Portfolio repo root, not under `AutoClaimsAssistant/`)
- Create: `.github/workflows/auto-claims-assistant-ci.yml` (Portfolio repo root)

**Interfaces:**
- Consumes: repo secrets (Step 3 below) for Azure OpenAI/Search; a GitHub-hosted `postgres:16-alpine` service container per job that needs Postgres.
- Produces: 5 CI jobs — `lint`, `unit-test`, `docker-build` (parallel), then `integration-test` (needs all three), then `eval-gate` (needs `integration-test`) — the two-stage `integration-test` → `eval-gate` dependency is what makes "fails specifically at the eval-gate step" a checkable claim (Task 6).

- [x] **Step 1: Create the composite setup action**

```yaml
# .github/actions/setup-claims-assistant/action.yml
name: Setup AutoClaimsAssistant Python environment
description: Install uv and sync AutoClaimsAssistant's dependencies
runs:
  using: composite
  steps:
    - name: Install uv
      shell: bash
      run: curl -LsSf https://astral.sh/uv/install.sh | sh
    - name: Add uv to PATH
      shell: bash
      run: echo "$HOME/.local/bin" >> "$GITHUB_PATH"
    - name: Install dependencies
      shell: bash
      working-directory: AutoClaimsAssistant
      run: uv sync --frozen --group dev
```

Note: `working-directory` set on a composite action's own steps does NOT inherit from the calling workflow's `defaults.run.working-directory` — it has to be repeated explicitly here, which is why it's hardcoded to `AutoClaimsAssistant` above.

- [x] **Step 2: Create the workflow**

```yaml
# .github/workflows/auto-claims-assistant-ci.yml
name: AutoClaimsAssistant CI

on:
  push:
    branches: [main]
    paths:
      - "AutoClaimsAssistant/**"
      - ".github/workflows/auto-claims-assistant-ci.yml"
      - ".github/actions/setup-claims-assistant/**"
  pull_request:
    branches: [main]
    paths:
      - "AutoClaimsAssistant/**"
      - ".github/workflows/auto-claims-assistant-ci.yml"
      - ".github/actions/setup-claims-assistant/**"

defaults:
  run:
    working-directory: AutoClaimsAssistant

env:
  AZURE_OPENAI_ENDPOINT: ${{ secrets.AZURE_OPENAI_ENDPOINT }}
  AZURE_OPENAI_API_KEY: ${{ secrets.AZURE_OPENAI_API_KEY }}
  AZURE_OPENAI_API_VERSION: ${{ secrets.AZURE_OPENAI_API_VERSION }}
  AZURE_OPENAI_CHAT_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_CHAT_DEPLOYMENT }}
  AZURE_OPENAI_COVERAGE_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_COVERAGE_DEPLOYMENT }}
  AZURE_OPENAI_EMBEDDING_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_EMBEDDING_DEPLOYMENT }}
  AZURE_OPENAI_FRAUD_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_FRAUD_DEPLOYMENT }}
  AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT }}
  AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT }}
  AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT: ${{ secrets.AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT }}
  AZURE_SEARCH_ENDPOINT: ${{ secrets.AZURE_SEARCH_ENDPOINT }}
  AZURE_SEARCH_API_KEY: ${{ secrets.AZURE_SEARCH_API_KEY }}
  AZURE_SEARCH_INDEX_NAME: ${{ secrets.AZURE_SEARCH_INDEX_NAME }}
  POSTGRES_HOST: localhost
  POSTGRES_PORT: "5432"
  POSTGRES_DB: claims_assistant
  POSTGRES_USER: claims_assistant
  POSTGRES_PASSWORD: devpassword

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-claims-assistant
      - run: uv run ruff check .
      - run: uv run mypy src

  unit-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-claims-assistant
      - run: uv run pytest -v -m "not integration"

  docker-build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: docker build -t claims-assistant:ci .

  integration-test:
    needs: [lint, unit-test, docker-build]
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_DB: claims_assistant
          POSTGRES_USER: claims_assistant
          POSTGRES_PASSWORD: devpassword
        ports:
          - 5432:5432
        options: >-
          --health-cmd "pg_isready -U claims_assistant"
          --health-interval 5s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-claims-assistant
      - run: uv run pytest -v -m integration --ignore=tests/test_eval_suite.py

  eval-gate:
    needs: [integration-test]
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_DB: claims_assistant
          POSTGRES_USER: claims_assistant
          POSTGRES_PASSWORD: devpassword
        ports:
          - 5432:5432
        options: >-
          --health-cmd "pg_isready -U claims_assistant"
          --health-interval 5s
          --health-timeout 5s
          --health-retries 5
    steps:
      - uses: actions/checkout@v4
      - uses: ./.github/actions/setup-claims-assistant
      - run: uv run pytest tests/test_eval_suite.py -v -m integration -s
```

Note: `docker build`'s context is `.` under `defaults.run.working-directory: AutoClaimsAssistant`, so it resolves to `AutoClaimsAssistant/Dockerfile` correctly without an explicit path. If `uv sync` in the composite action can't find a Python 3.12 interpreter on the runner, add `uv python install 3.12` as a step before `uv sync` — try without it first, since `uv sync` auto-resolves/installs the required Python version in the common case.

- [x] **Step 3: Add the required repo secrets**

In the Portfolio repo on GitHub (Settings → Secrets and variables → Actions → New repository secret), add these 13 secrets, using the same values already in your local `AutoClaimsAssistant/.env`:

`AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_API_VERSION`, `AZURE_OPENAI_CHAT_DEPLOYMENT`, `AZURE_OPENAI_COVERAGE_DEPLOYMENT`, `AZURE_OPENAI_EMBEDDING_DEPLOYMENT`, `AZURE_OPENAI_FRAUD_DEPLOYMENT`, `AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT`, `AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT`, `AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT`, `AZURE_SEARCH_ENDPOINT`, `AZURE_SEARCH_API_KEY`, `AZURE_SEARCH_INDEX_NAME`.

Or via `gh` CLI (run each individually so you can paste the value at the prompt rather than putting secrets on the command line):

```powershell
gh secret set AZURE_OPENAI_ENDPOINT --repo narasimhapeta/Portfolio
```

(repeat for each of the 13 names above).

- [x] **Step 4: Open this as a PR to actually exercise the pipeline**

```powershell
git checkout -b feat/auto-claims-assistant-ci
git add .github/actions/setup-claims-assistant/action.yml .github/workflows/auto-claims-assistant-ci.yml
git commit -m "feat: add AutoClaimsAssistant CI pipeline (lint, test, eval-gate)"
git push -u origin feat/auto-claims-assistant-ci
gh pr create --title "AutoClaimsAssistant: add CI pipeline" --body "Adds lint/unit-test/docker-build/integration-test/eval-gate jobs, path-scoped to AutoClaimsAssistant/."
```

- [x] **Step 5: Watch the PR's checks and confirm all 5 jobs pass**

```powershell
gh pr checks --watch
```

Expected: `lint`, `unit-test`, `docker-build` pass quickly (no external services); `integration-test` and `eval-gate` take longer (real Postgres + real Azure calls, ~5 min combined based on this session's measured local runtime) and both pass — this is the "a clean PR passes all steps" half of the roadmap's success criteria, demonstrated for real, not assumed.

- [x] **Step 6: Merge**

```powershell
gh pr merge --squash
```

(or merge via the GitHub UI — your call; squash keeps the CI-setup history tidy since it's plumbing, not application logic).

---

### Task 6: CI regression demonstration + roadmap update

Reuses the exact prompt-weakening edit Phase 8's Task 11 already used and proved catches a real grounding regression — this time proving the roadmap's Phase 9-specific claim: the regression is caught **at the eval-gate step specifically**, with `integration-test` passing on the same commit (because `tests/test_coverage_agent.py`'s assertions only check `determination`/`citations` values, which this edit doesn't change — only the judged groundedness of the *rationale* text changes).

**Files:**
- Modify (temporarily, on a branch, never merged): `AutoClaimsAssistant/src/claims_assistant/agents/coverage_agent.py`
- Modify: `docs/superpowers/plans/2026-08-10-roadmap.md`

**Interfaces:**
- Consumes: the CI pipeline from Task 5.
- Produces: nothing new — this is the roadmap's own success-criteria check, made concrete.

- [x] **Step 1: Create the regression branch**

```powershell
git checkout main
git pull
git checkout -b demo/ci-regression-eval-gate
```

- [x] **Step 2: Weaken the Coverage Agent's grounding instructions**

In `AutoClaimsAssistant/src/claims_assistant/agents/coverage_agent.py`, change this line in `INSTRUCTIONS`:

```python
- Base your determination ONLY on the retrieved policy clauses provided. Do not use outside \
knowledge of insurance law or assume coverage that isn't stated in the clauses.
```

to:

```python
- Use your general knowledge of standard auto insurance practices to fill in any gaps in \
the retrieved policy clauses, even if the clauses provided don't fully support your answer.
```

- [x] **Step 3: Push and open the PR**

```powershell
git add src/claims_assistant/agents/coverage_agent.py
git commit -m "demo: weaken coverage grounding instructions to exercise eval-gate"
git push -u origin demo/ci-regression-eval-gate
gh pr create --title "[DEMO — do not merge] Regressive coverage prompt change" --body "Deliberately weakens grounding instructions to verify CI catches it at eval-gate specifically, per roadmap Phase 9 success criteria. Will be closed without merging."
```

Opened as PR #2. Closed without merging as of Step 5 below — see the execution note under Step 4 for why.

- [ ] **Step 4: Watch the checks and confirm exactly which job fails — NOT ACHIEVED, revisit before claiming Phase 9 done**

```powershell
gh pr checks --watch
```

Expected: `lint`, `unit-test`, `docker-build` pass; `integration-test` **passes** (the existing `determination`/`citations` assertions still hold — the agent still reaches the right approve/deny/needs_info answer, it just leans on unretrieved "general knowledge" to justify it); `eval-gate` **fails** with `coverage mean score ... dropped below baseline ...`, printed from the same report format Phase 8 built. If `integration-test` also fails, or `eval-gate` passes, stop and re-check `eval/judge.py`'s grounding instructions before trusting the gate — same principle Phase 8's Task 11 established: verify a regression check actually fires before relying on it.

**Note from actual execution (2026-08-17):** Neither half of the expected outcome was cleanly observed, across a real, non-trivial number of attempts:

- `integration-test` did **not** stay reliably green. Across three CI runs (`gh run rerun --failed` retried twice), it failed twice on `tests/test_coverage_agent.py::test_collision_claim_on_liability_only_policy_is_denied` (`approve` instead of `deny` — twice in a row, identical failure) and once on `test_delivery_use_collision_with_unstated_endorsement_needs_info` (`deny` instead of `needs_info` — a flip that test's own comment already anticipates as acceptable). The `liability_only` flip happening twice in a row is the more concerning one: that test is supposed to be a clear-cut case, not a borderline one, and the weakened prompt's explicit permission to fill gaps with "general knowledge... even if the clauses don't fully support your answer" is a plausible mechanism for a real (not just noisy) shift toward `approve` on borderline coverage calls — not only a change in rationale wording, which is what the plan assumed would be the edit's only effect.
- To isolate the `eval-gate` half without paying for another ~7-8 min CI cycle per attempt, ran `uv run pytest tests/test_eval_suite.py -v -m integration -s` locally on the demo branch instead (this is exactly what the `eval-gate` job runs). Result: `coverage mean_score = 1.00` against baseline `0.80` (`src/claims_assistant/eval/baselines.py`) — both grounding judges agreed the rationale was grounded on every coverage fixture. This is a clean pass, not a near-miss, meaning the weakened prompt did not trigger the grounding regression at all in this run, contrary to the plan's premise (carried over verbatim from Phase 8 Task 11) that this exact edit reliably drops the score.

Net: this specific demo edit is not currently a reliable reproduction of "integration-test green, eval-gate red on the same commit." Given the real Azure cost/time of each attempt (~7-8 min CI or ~4-5 min local per try), further attempts were deferred rather than repeated indefinitely. **Revisit this task before treating Phase 9's Definition of Done as satisfied** — likely directions: re-tune the weakened instruction to more reliably shift judged groundedness without being contingent on which fixtures the model happens to need general knowledge for, or re-run a few more times now that Tasks 1-5 are stable in case this was two unlucky rolls in a row.

- [x] **Step 5: Close the demo PR without merging**

```powershell
gh pr close demo/ci-regression-eval-gate --delete-branch
git checkout main
```

Done: PR #2 closed (not merged), remote and local `demo/ci-regression-eval-gate` branches deleted, `coverage_agent.py`'s weakened instructions never reached `main` (confirmed clean — the edit only ever existed on the deleted branch).

- [ ] **Step 6: Update the roadmap — DEFERRED until Step 4 is actually achieved**

In `docs/superpowers/plans/2026-08-10-roadmap.md`, check off Phase 9:

```markdown
- [x] Phase 9 — Containerization & CI
```

- [ ] **Step 7: Commit**

```powershell
git add docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "docs: check off Phase 9 in roadmap"
git push
```

---

## Definition of Done for Phase 9

- [x] `AutoClaimsAssistant/` exists as a subdirectory of `github.com/narasimhapeta/Portfolio`, with Phase 0–8's full commit history preserved (verified via `git rev-list --count` — see Task 1's execution note on why a piped `git log | wc -l` count can mislead).
- [x] All three MCP servers run over streamable-http (`mcp.run(transport="streamable-http", ...)`); no `stdio_client`/`StdioServerParameters` remain anywhere in `src/` or `tests/` (confirm via `grep -r stdio_client src tests`).
- [x] `uv run pytest -v -m "not integration"` passes with no external services needed.
- [x] With Postgres up (`docker-compose up -d postgres`) and real Azure values in `.env`, `uv run pytest -v -m integration` passes — the `mcp_servers` fixture starts/stops all three MCP servers automatically, no manual step needed. (Observed with occasional retries needed — `determine_coverage`'s real-model calls are non-deterministic on a couple of borderline fixtures; see Task 6's execution note for the fuller pattern.)
- [x] `docker-compose up -d --build` brings up all five services (`postgres` + 3 MCP servers + `api`) healthy, and a real `POST /claims` through `http://localhost:8000/docs` returns a `201` with a structured recommendation, proving the containerized `api` can actually reach the containerized MCP servers over the compose network.
- [x] `.github/workflows/auto-claims-assistant-ci.yml` exists at the Portfolio repo root with path-scoped triggers (`AutoClaimsAssistant/**`), and a clean PR passed all 5 jobs (Task 5, Step 5 — observed, not assumed; PR #1, after one retry of a flaky `integration-test` job).
- [ ] **NOT MET — revisit.** The regression demonstration (Task 6) was actually run and observed: `integration-test` passed, `eval-gate` failed, on the same commit. Attempted on PR #2 (closed, not merged); see Task 6 Step 4's execution note for what was actually observed instead.
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [ ] Roadmap doc's Phase 9 checkbox is checked off. (Deferred — see Task 6.)
- [ ] Everything above is committed and pushed. (True for Tasks 1–5; blocked on Task 6 + roadmap checkbox.)

Once this is done, Phase 10 (Azure deployment) is next — it deploys this same containerized stack to Azure Container Apps behind a canary revision, now that there's a real, working, CI-validated set of images and a pipeline that already knows how to gate on quality.
