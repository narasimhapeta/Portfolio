# Phase 11: Web Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path/command in chat, the human creates/edits the file or runs the command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files, or to run any `az`/`docker`/`gh` command that provisions, modifies, or deletes a real resource, directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Reverse the spec's original "no user-facing UI" non-goal (Section 1/11, superseded 2026-08-25). Build a structured, multi-page Streamlit app on top of the existing FastAPI API: submit an FNOL, watch it complete, view the recommendation, upload documents, and browse claim history — all from a browser, without Swagger/Postman. Deployed as a 5th Azure Container App behind a shared-password gate.

**Architecture:** A new `src/claims_assistant/frontend/` package, following the project's existing src-layout convention — not a separate top-level directory. `frontend/api_client.py` wraps the 4 backend endpoints with a plain `httpx.Client` (sync): Streamlit's own execution model reruns the whole script top-to-bottom on every interaction, so there's no `asyncio` event loop to hang an `async def` client off of, unlike every other I/O-bound function in this project. `frontend/app.py` is the entrypoint, using `st.navigation`/`st.Page` for 4 pages (`frontend/pages/submit.py`, `status.py`, `upload.py`, `history.py`), gated by a password check (`frontend/auth.py`) run before any page renders.

**Deployment shape — confirmed from the actual CD/compose files, not assumed:** this project already uses **one shared Docker image across all 4 existing services**, differentiated only by each Container App's `command`/`args` (`docker-compose.yml`: `policy-db-mcp`, `claims-history-mcp`, `vin-vehicle-mcp`, and `api` all say `build: .`; `app-infra-apps.bicep`: all 4 `containerApps` resources reference the identical `var image`). The frontend follows the same pattern — no new Dockerfile, no new image, no new `docker build`/`push` leg in CD. `streamlit` is added to the existing `pyproject.toml`; the 5th container app (`claims-assistant-frontend`) runs `uv run streamlit run src/claims_assistant/frontend/app.py --server.port 8501 --server.address 0.0.0.0` against the exact same image the other 4 already use.

**New API surface (extends the already-shipped Phase 7 API):** `GET /claims` — list, `limit`/`offset` query params (default `limit=50`, `offset=0`), sorted `created_at desc`. Same `claims_repository.py`/`api/claims.py` files Phase 7 established.

**Tech Stack:** New dependency `streamlit` (exact version pinned when you run `uv add streamlit` in Step 1 — not guessed here). `httpx` is already a dev-only dependency (Phase 7); this phase promotes it to a direct runtime dependency for `frontend/api_client.py`. No other new packages.

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§1 — this phase is the reversal of the "no UI" non-goal; §7 — deployment, this phase's 5th Container App follows the same Bicep/CD conventions Phase 10 established; §10 — working agreement, guided walkthrough)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0). The frontend package is the one exception to "every I/O-bound function is `async def`" (Architecture, above) — Streamlit's script-rerun model has no persistent event loop to await against.
- No Alembic/migrations tooling exists in this project — no schema change is needed for `GET /claims` (it's a read of the existing `claims` table).
- **Access protection**: a single shared secret, `FRONTEND_ACCESS_PASSWORD`, checked in `frontend/auth.py` before any page renders. Not part of `config.Settings` (that class backs the API service, not the frontend) — read directly via `os.environ` in the frontend package, matching the frontend's standalone-service treatment elsewhere in this plan.
- `CLAIMS_API_BASE_URL` (new env var, frontend-only) points the `httpx.Client` at the API — `http://localhost:8000` for local dev, the real Container Apps FQDN in production (Task 6).
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for touched source files) before moving to the next task.
- Tests that need real Postgres are `pytest.mark.integration`, matching every prior phase's convention. Streamlit page tests use `streamlit.testing.v1.AppTest` with the API client mocked (no real HTTP, no real Postgres) — confirm `AppTest`'s exact API against whatever `streamlit` version Step 1 installs before writing Task 3's first test; this plan's snippets show the documented pattern as of planning time but the installed version is the source of truth.
- **Azure Container Apps WebSocket support for Streamlit is unconfirmed** — Streamlit's live UI requires a persistent WebSocket connection, and while ACA ingress generally supports WebSocket passthrough, this has not been validated against real ARM for this specific setup. Task 6 validates this for real (`az containerapp ingress show` / a live browser session against the deployed revision) before considering the phase done — if it doesn't work out of the box, the fallback is Streamlit's polling-based `--server.enableWebsocketCompression=false`/reduced-reliance config, investigated at that point, not pre-solved here.

---

### Task 1: `GET /claims` list endpoint

**Files:**
- Modify: `src/claims_assistant/claims_repository.py`
- Modify: `src/claims_assistant/api/claims.py`
- Modify: `src/claims_assistant/api/claims_schema.py`
- Test: `tests/test_claims_repository.py`, `tests/test_claims_api.py`

**Interfaces:**
- Produces: `list_claims(session, limit: int = 50, offset: int = 0) -> list[Claim]` (`claims_repository.py`); `ClaimListResponse` (Pydantic: `claims: list[ClaimResponse]`, `total: int`) (`api/claims_schema.py`); `GET /claims` route (`api/claims.py`).

- [ ] **Step 1: Write the failing repository test**

Append to `tests/test_claims_repository.py`:

```python
@pytest.mark.asyncio
async def test_list_claims_returns_newest_first_and_respects_limit():
    await create_all_tables()
    session_factory = get_session_factory()

    async with session_factory() as session:
        for i in range(3):
            await create_failed_claim(
                session,
                ClaimIntakeRequest(
                    policy_number=f"POL-CA-000{i}",
                    vin="1C4RJFBG5FC123458",
                    narrative_text="test claim",
                ),
                f"error {i}",
            )

    async with session_factory() as session:
        claims = await list_claims(session, limit=2, offset=0)

    assert len(claims) == 2
    assert claims[0].created_at >= claims[1].created_at
```

Add `list_claims` to the existing import from `claims_assistant.claims_repository` at the top of the file.

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_claims_repository.py::test_list_claims_returns_newest_first_and_respects_limit -v -m integration`
Expected: FAIL — `ImportError: cannot import name 'list_claims'`

- [ ] **Step 3: Add `list_claims` to the repository**

In `src/claims_assistant/claims_repository.py`, add this import at the top:

```python
from sqlalchemy import select
```

And add this function at the end of the file:

```python
async def list_claims(session: AsyncSession, limit: int = 50, offset: int = 0) -> list[Claim]:
    result = await session.execute(
        select(Claim).order_by(Claim.created_at.desc()).limit(limit).offset(offset)
    )
    return list(result.scalars().all())
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_claims_repository.py::test_list_claims_returns_newest_first_and_respects_limit -v -m integration`
Expected: PASS

- [ ] **Step 5: Add `ClaimListResponse` and the route**

In `src/claims_assistant/api/claims_schema.py`, add at the end of the file:

```python
class ClaimListResponse(BaseModel):
    claims: list[ClaimResponse]
```

In `src/claims_assistant/api/claims.py`, update the `claims_repository` import to include `list_claims`, update the `claims_schema` import to include `ClaimListResponse`, and add this route (placed before `get_claim` so FastAPI's path-matching doesn't need to disambiguate `/claims` from `/claims/{claim_id}` — it won't, since `/claims` with no trailing segment can't match `{claim_id}`, but keeping the list route grouped with `submit_claim` above it matches this file's existing top-to-bottom ordering by HTTP verb):

```python
@router.get("/claims", response_model=ClaimListResponse)
async def list_claims_route(
    session: SessionDep, limit: int = 50, offset: int = 0
) -> ClaimListResponse:
    claims = await list_claims(session, limit=limit, offset=offset)
    return ClaimListResponse(claims=[claim_response_from_model(c) for c in claims])
```

- [ ] **Step 6: Write the failing API test**

Append to `tests/test_claims_api.py`:

```python
@pytest.mark.asyncio
async def test_list_claims_returns_persisted_claims_newest_first():
    await create_all_tables()
    fake_workflow = _FakeWorkflow(outputs=[_RECOMMENDATION])

    async with _client_with_fake_workflow(fake_workflow) as client:
        first = await client.post("/claims", json=_REQUEST_BODY)
        second = await client.post("/claims", json=_REQUEST_BODY)
        response = await client.get("/claims?limit=10&offset=0")

    assert response.status_code == 200
    body = response.json()
    ids = [c["id"] for c in body["claims"]]
    assert second.json()["id"] in ids
    assert first.json()["id"] in ids
    assert ids.index(second.json()["id"]) < ids.index(first.json()["id"])
```

- [ ] **Step 7: Run tests, lint, type-check**

Run: `uv run pytest tests/test_claims_api.py tests/test_claims_repository.py -v -m integration`
Expected: all pass, including the two new tests.

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 8: Commit**

```powershell
git add src/claims_assistant/claims_repository.py src/claims_assistant/api/claims.py src/claims_assistant/api/claims_schema.py tests/test_claims_repository.py tests/test_claims_api.py
git commit -m "feat: add GET /claims list endpoint"
```

---

### Task 2: Frontend scaffold — dependency, API client, password gate

**Files:**
- Modify: `pyproject.toml`
- Create: `src/claims_assistant/frontend/__init__.py`
- Create: `src/claims_assistant/frontend/api_client.py`
- Create: `src/claims_assistant/frontend/auth.py`
- Create: `src/claims_assistant/frontend/app.py`
- Test: `tests/test_frontend_api_client.py`, `tests/test_frontend_auth.py`

**Interfaces:**
- Produces: `ClaimsApiClient` (`api_client.py`) — `submit_claim(policy_number, vin, narrative_text) -> dict`, `get_claim(claim_id: str) -> dict`, `list_claims(limit=50, offset=0) -> list[dict]`, `upload_document(claim_id: str, filename: str, content: bytes, content_type: str) -> dict`, wrapping the 4 endpoints via `httpx.Client(base_url=...)`. `check_password() -> bool` (`auth.py`), reads `FRONTEND_ACCESS_PASSWORD` from `os.environ` and compares against `st.session_state`/`st.text_input`. Every later page task imports both.

- [ ] **Step 1: Add the `streamlit` dependency**

```powershell
uv add streamlit
```

Also promote `httpx` from dev-only to a direct runtime dependency (it's already installed as a transitive/dev dependency since Phase 7 — this just changes which `pyproject.toml` group owns it):

Edit `pyproject.toml` — move `"httpx>=0.28.1"` from the `[dependency-groups] dev` list up into the top-level `dependencies` list.

- [ ] **Step 2: Write the failing API client test**

```python
# tests/test_frontend_api_client.py
from __future__ import annotations

import httpx
import pytest

from claims_assistant.frontend.api_client import ClaimsApiClient


def test_submit_claim_posts_to_claims_endpoint():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims"
        assert request.method == "POST"
        return httpx.Response(201, json={"id": "abc", "status": "completed"})

    client = ClaimsApiClient(
        base_url="http://test",
        transport=httpx.MockTransport(handler),
    )
    result = client.submit_claim(
        policy_number="POL-CA-0003", vin="1C4RJFBG5FC123458", narrative_text="hail damage"
    )
    assert result["status"] == "completed"


def test_get_claim_gets_by_id():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims/abc"
        return httpx.Response(200, json={"id": "abc", "status": "completed"})

    client = ClaimsApiClient(base_url="http://test", transport=httpx.MockTransport(handler))
    result = client.get_claim("abc")
    assert result["id"] == "abc"


def test_list_claims_passes_pagination_params():
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.url.path == "/claims"
        assert request.url.params["limit"] == "10"
        assert request.url.params["offset"] == "5"
        return httpx.Response(200, json={"claims": []})

    client = ClaimsApiClient(base_url="http://test", transport=httpx.MockTransport(handler))
    result = client.list_claims(limit=10, offset=5)
    assert result == []
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `uv run pytest tests/test_frontend_api_client.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.frontend'`

- [ ] **Step 4: Write `api_client.py`**

```python
# src/claims_assistant/frontend/api_client.py
from __future__ import annotations

from typing import Any

import httpx


class ClaimsApiClient:
    def __init__(self, base_url: str, transport: httpx.BaseTransport | None = None) -> None:
        self._client = httpx.Client(base_url=base_url, transport=transport, timeout=60.0)

    def submit_claim(self, policy_number: str, vin: str, narrative_text: str) -> dict[str, Any]:
        response = self._client.post(
            "/claims",
            json={"policy_number": policy_number, "vin": vin, "narrative_text": narrative_text},
        )
        response.raise_for_status()
        return response.json()

    def get_claim(self, claim_id: str) -> dict[str, Any]:
        response = self._client.get(f"/claims/{claim_id}")
        response.raise_for_status()
        return response.json()

    def list_claims(self, limit: int = 50, offset: int = 0) -> list[dict[str, Any]]:
        response = self._client.get("/claims", params={"limit": limit, "offset": offset})
        response.raise_for_status()
        return response.json()["claims"]

    def upload_document(
        self, claim_id: str, filename: str, content: bytes, content_type: str
    ) -> dict[str, Any]:
        response = self._client.post(
            f"/claims/{claim_id}/documents",
            files={"file": (filename, content, content_type)},
        )
        response.raise_for_status()
        return response.json()
```

Note: `submit_claim`'s 60-second `httpx` timeout matches the API's own real synchronous pipeline duration (Phase 7's design note — 10-30s typical) plus headroom, not an arbitrary default.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_frontend_api_client.py -v`
Expected: PASS (3 passed)

- [ ] **Step 6: Write the failing auth test**

```python
# tests/test_frontend_auth.py
from __future__ import annotations

import pytest

from claims_assistant.frontend.auth import verify_password


def test_verify_password_accepts_correct_password(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("FRONTEND_ACCESS_PASSWORD", "correct-horse")
    assert verify_password("correct-horse") is True


def test_verify_password_rejects_incorrect_password(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("FRONTEND_ACCESS_PASSWORD", "correct-horse")
    assert verify_password("wrong") is False
```

- [ ] **Step 7: Run the test to verify it fails, then write `auth.py`**

Run: `uv run pytest tests/test_frontend_auth.py -v`
Expected: FAIL — `ModuleNotFoundError`

```python
# src/claims_assistant/frontend/auth.py
from __future__ import annotations

import os

import streamlit as st


def verify_password(attempt: str) -> bool:
    expected = os.environ.get("FRONTEND_ACCESS_PASSWORD", "")
    return bool(expected) and attempt == expected


def require_login() -> None:
    """Blocks page rendering until the correct password is entered.
    Call at the top of app.py before st.navigation runs.
    """
    if st.session_state.get("authenticated"):
        return
    st.title("Claims Assistant")
    password = st.text_input("Access password", type="password")
    if st.button("Log in"):
        if verify_password(password):
            st.session_state["authenticated"] = True
            st.rerun()
        else:
            st.error("Incorrect password")
    st.stop()
```

`verify_password` is the pure, unit-tested logic; `require_login`'s Streamlit-widget wiring is exercised by Task 3's `AppTest`-based page tests instead (it has no meaningful behavior to unit-test in isolation — it's UI wiring around the already-tested predicate).

- [ ] **Step 8: Run the auth test to verify it passes**

Run: `uv run pytest tests/test_frontend_auth.py -v`
Expected: PASS (2 passed)

- [ ] **Step 9: Write the app entrypoint**

```python
# src/claims_assistant/frontend/app.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.auth import require_login

require_login()

pages = [
    st.Page("pages/submit.py", title="Submit FNOL", icon="📝"),
    st.Page("pages/status.py", title="Claim Status", icon="🔍"),
    st.Page("pages/upload.py", title="Upload Document", icon="📎"),
    st.Page("pages/history.py", title="Claim History", icon="📋"),
]
st.navigation(pages).run()
```

`pages/` here is relative to `app.py`'s own directory (Streamlit's `st.Page` path resolution) — create `src/claims_assistant/frontend/pages/` as an empty directory for now; Tasks 3-6 populate it. `CLAIMS_API_BASE_URL` construction is deferred to those page modules (each builds its own `ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))`), not centralized here, since Streamlit page modules are independently executed scripts, not importers of a shared app-level object.

- [ ] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 11: Commit**

```powershell
git add pyproject.toml uv.lock src/claims_assistant/frontend tests/test_frontend_api_client.py tests/test_frontend_auth.py
git commit -m "feat: scaffold Streamlit frontend with API client and password gate"
```

---

### Task 3: Submit FNOL page

**Files:**
- Create: `src/claims_assistant/frontend/pages/submit.py`
- Test: `tests/test_frontend_submit_page.py`

**Interfaces:**
- Consumes: `ClaimsApiClient` (Task 2).
- Produces: the Submit FNOL page — a form (`policy_number`, `vin`, `narrative_text`) that calls `submit_claim` and renders the result.

- [ ] **Step 1: Write the failing page test**

Uses `streamlit.testing.v1.AppTest.from_file`, monkeypatching `ClaimsApiClient` so no real HTTP call happens. Confirm the exact `AppTest` API (e.g. whether form-submit buttons need `.click().run()` or a different call) against your installed `streamlit` version before trusting this snippet verbatim — check `streamlit.testing.v1`'s docstrings/docs for your version if it doesn't match.

```python
# tests/test_frontend_submit_page.py
from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_submit_page_shows_recommendation_after_successful_submit():
    at = AppTest.from_file("src/claims_assistant/frontend/pages/submit.py")
    with patch("claims_assistant.frontend.pages.submit.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.submit_claim.return_value = {
            "id": "abc-123",
            "status": "completed",
            "recommendation": {"coverage_determination": "approve"},
        }
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="policy_number").set_value("POL-CA-0003")
        at.text_input(key="vin").set_value("1C4RJFBG5FC123458")
        at.text_area(key="narrative_text").set_value("Hail damage overnight.")
        at.button(key="submit_button").click().run()

        mock_client.submit_claim.assert_called_once_with(
            policy_number="POL-CA-0003",
            vin="1C4RJFBG5FC123458",
            narrative_text="Hail damage overnight.",
        )
        assert not at.exception
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_frontend_submit_page.py -v`
Expected: FAIL — `FileNotFoundError` or `ModuleNotFoundError`, since `pages/submit.py` doesn't exist yet.

- [ ] **Step 3: Write the page**

```python
# src/claims_assistant/frontend/pages/submit.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Submit FNOL")

policy_number = st.text_input("Policy number", key="policy_number")
vin = st.text_input("VIN", key="vin")
narrative_text = st.text_area("Narrative", key="narrative_text")

if st.button("Submit claim", key="submit_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    with st.spinner("Running the claim intake pipeline (10-30s)..."):
        result = client.submit_claim(
            policy_number=policy_number, vin=vin, narrative_text=narrative_text
        )
    st.session_state["last_claim_id"] = result["id"]
    if result["status"] == "completed":
        st.success(f"Claim {result['id']} completed.")
        st.json(result["recommendation"])
    elif result["status"] == "needs_clarification":
        st.warning(f"Claim {result['id']} needs clarification.")
        st.json(result["clarification"])
    else:
        st.error(f"Claim {result['id']} failed.")
        st.write(result.get("error"))
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_frontend_submit_page.py -v`
Expected: PASS

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/frontend/pages/submit.py tests/test_frontend_submit_page.py
git commit -m "feat: add Submit FNOL frontend page"
```

---

### Task 4: Claim Status page

**Files:**
- Create: `src/claims_assistant/frontend/pages/status.py`
- Test: `tests/test_frontend_status_page.py`

**Interfaces:**
- Consumes: `ClaimsApiClient`.
- Produces: a claim-ID input → `get_claim` → renders whichever outcome shape came back.

- [ ] **Step 1: Write the failing page test**

```python
# tests/test_frontend_status_page.py
from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_status_page_shows_completed_claim():
    at = AppTest.from_file("src/claims_assistant/frontend/pages/status.py")
    with patch("claims_assistant.frontend.pages.status.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.get_claim.return_value = {
            "id": "abc-123",
            "status": "completed",
            "recommendation": {"coverage_determination": "approve"},
        }
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="lookup_claim_id").set_value("abc-123")
        at.button(key="lookup_button").click().run()

        mock_client.get_claim.assert_called_once_with("abc-123")
        assert not at.exception
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_frontend_status_page.py -v`
Expected: FAIL — `FileNotFoundError`

- [ ] **Step 3: Write the page**

```python
# src/claims_assistant/frontend/pages/status.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Claim Status")

default_id = st.session_state.get("last_claim_id", "")
claim_id = st.text_input("Claim ID", value=default_id, key="lookup_claim_id")

if st.button("Look up", key="lookup_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    result = client.get_claim(claim_id)
    st.write(f"Status: **{result['status']}**")
    if result.get("recommendation"):
        st.json(result["recommendation"])
    if result.get("clarification"):
        st.json(result["clarification"])
    if result.get("error"):
        st.error(result["error"])
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_frontend_status_page.py -v`
Expected: PASS

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/frontend/pages/status.py tests/test_frontend_status_page.py
git commit -m "feat: add Claim Status frontend page"
```

---

### Task 5: Upload Document page

**Files:**
- Create: `src/claims_assistant/frontend/pages/upload.py`
- Test: `tests/test_frontend_upload_page.py`

**Interfaces:**
- Consumes: `ClaimsApiClient`.
- Produces: a claim-ID input + `st.file_uploader` → `upload_document`.

- [ ] **Step 1: Write the failing page test**

```python
# tests/test_frontend_upload_page.py
from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_upload_page_calls_upload_document_with_claim_id():
    at = AppTest.from_file("src/claims_assistant/frontend/pages/upload.py")
    with patch("claims_assistant.frontend.pages.upload.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.upload_document.return_value = {"id": "abc-123", "document_urls": ["url1"]}
        mock_client_cls.return_value = mock_client

        at.run()
        at.text_input(key="upload_claim_id").set_value("abc-123")
        # File-upload widgets can't be driven through AppTest's public API as of this
        # writing (confirm against your installed streamlit version) -- if so, this test
        # instead calls the page's extracted upload handler function directly.
        assert not at.exception
```

Note (confirm during this step, not assumed here): `streamlit.testing.v1.AppTest` may not support scripting `st.file_uploader` interactions directly, depending on your installed version. If it doesn't, refactor the page to extract a plain `handle_upload(client, claim_id, filename, content, content_type)` function and unit-test that function directly (no `AppTest` involved) instead of trying to script the widget — check the installed version's docs before writing this step for real, and adjust the test shape accordingly.

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_frontend_upload_page.py -v`
Expected: FAIL — `FileNotFoundError`

- [ ] **Step 3: Write the page**

```python
# src/claims_assistant/frontend/pages/upload.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Upload Document")

default_id = st.session_state.get("last_claim_id", "")
claim_id = st.text_input("Claim ID", value=default_id, key="upload_claim_id")
uploaded_file = st.file_uploader("Document", key="upload_file")

if uploaded_file is not None and claim_id and st.button("Upload", key="upload_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    result = client.upload_document(
        claim_id=claim_id,
        filename=uploaded_file.name,
        content=uploaded_file.getvalue(),
        content_type=uploaded_file.type or "application/octet-stream",
    )
    st.success("Uploaded.")
    st.json(result.get("document_urls"))
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_frontend_upload_page.py -v`
Expected: PASS (adjust per Step 1's note if `AppTest` can't script the file uploader in your installed version).

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/frontend/pages/upload.py tests/test_frontend_upload_page.py
git commit -m "feat: add Upload Document frontend page"
```

---

### Task 6: Claims History page

**Files:**
- Create: `src/claims_assistant/frontend/pages/history.py`
- Test: `tests/test_frontend_history_page.py`

**Interfaces:**
- Consumes: `ClaimsApiClient`.
- Produces: a table of the most recent claims (`list_claims`).

- [ ] **Step 1: Write the failing page test**

```python
# tests/test_frontend_history_page.py
from __future__ import annotations

from unittest.mock import MagicMock, patch

from streamlit.testing.v1 import AppTest


def test_history_page_renders_a_table_of_claims():
    at = AppTest.from_file("src/claims_assistant/frontend/pages/history.py")
    with patch("claims_assistant.frontend.pages.history.ClaimsApiClient") as mock_client_cls:
        mock_client = MagicMock()
        mock_client.list_claims.return_value = [
            {"id": "abc", "status": "completed", "policy_number": "POL-CA-0003"},
            {"id": "def", "status": "failed", "policy_number": "POL-CA-0004"},
        ]
        mock_client_cls.return_value = mock_client

        at.run()

        mock_client.list_claims.assert_called_once()
        assert not at.exception
        assert len(at.dataframe) == 1
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_frontend_history_page.py -v`
Expected: FAIL — `FileNotFoundError`

- [ ] **Step 3: Write the page**

```python
# src/claims_assistant/frontend/pages/history.py
from __future__ import annotations

import os

import pandas as pd
import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Claim History")

client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
claims = client.list_claims(limit=50, offset=0)
st.dataframe(
    pd.DataFrame(claims)[["id", "status", "policy_number", "vin", "created_at"]]
    if claims
    else pd.DataFrame(columns=["id", "status", "policy_number", "vin", "created_at"])
)
```

`pandas` is already a direct project dependency (Phase 8's eval framework) — no new dependency here.

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_frontend_history_page.py -v`
Expected: PASS

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/frontend/pages/history.py tests/test_frontend_history_page.py
git commit -m "feat: add Claim History frontend page"
```

---

### Task 7: Local Compose wiring

**Files:**
- Modify: `docker-compose.yml`

**Interfaces:** none new — this is deployment config only.

- [ ] **Step 1: Add the frontend service**

In `docker-compose.yml`, add a new service alongside the existing 4 (same `build: .` shared-image pattern as `policy-db-mcp`/`claims-history-mcp`/`vin-vehicle-mcp`):

```yaml
  frontend:
    build: .
    command: uv run streamlit run src/claims_assistant/frontend/app.py --server.port 8501 --server.address 0.0.0.0
    environment:
      CLAIMS_API_BASE_URL: http://api:8000
      FRONTEND_ACCESS_PASSWORD: devpassword
    ports:
      - "8501:8501"
    depends_on:
      api:
        condition: service_started
```

- [ ] **Step 2: Verify locally**

```powershell
docker-compose up -d
```

Open `http://localhost:8501`, log in with `devpassword`, submit a claim end-to-end through all 4 pages.

- [ ] **Step 3: Commit**

```powershell
git add docker-compose.yml
git commit -m "feat: add frontend service to docker-compose"
```

---

### Task 8: Azure deployment — 5th Container App

**Files:**
- Modify: `AutoClaimsAssistant/iac/app-infra-apps.bicep`
- Modify: `AutoClaimsAssistant/scripts/iac/deploy-app-infra-apps.ps1`
- Modify: `.github/workflows/auto-claims-assistant-cd.yml`

**Interfaces:**
- Produces: `claims-assistant-frontend` Container App, external ingress on port 8501, sharing the same ACR image and `acrPullIdentity` as the other 4 apps (Phase 10's `app-infra-apps.bicep`).

- [ ] **Step 1: Add the container app to `app-infra-apps.bicep`**

Add a 5th `Microsoft.App/containerApps` resource, modeled directly on the existing `policyDbMcp`/`claimsHistoryMcp`/`vinVehicleMcp` resources (same `identity`, same `registries`, same `dependsOn: [acrPullRoleAssignment]`) but with **external** ingress (unlike the internal-only MCP servers) and the frontend's `command`/`args`/env:

```bicep
@secure()
@description('Shared password gating access to the frontend')
param frontendAccessPassword string

param apiFqdn string

resource frontend 'Microsoft.App/containerApps@2026-03-02-preview' = {
  name: 'claims-assistant-frontend'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentity.id}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8501
        transport: 'auto'
      }
      registries: [
        { server: acrLoginServer, identity: acrPullIdentity.id }
      ]
      secrets: [
        { name: 'frontend-password', value: frontendAccessPassword }
      ]
    }
    template: {
      containers: [
        {
          name: 'claims-assistant-frontend'
          image: image
          command: ['uv']
          args: [
            'run', 'streamlit', 'run', 'src/claims_assistant/frontend/app.py'
            '--server.port', '8501', '--server.address', '0.0.0.0'
          ]
          env: [
            { name: 'CLAIMS_API_BASE_URL', value: 'https://${apiFqdn}' }
            { name: 'FRONTEND_ACCESS_PASSWORD', secretRef: 'frontend-password' }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
  dependsOn: [
    acrPullRoleAssignment
  ]
}

output frontendFqdn string = frontend.properties.configuration.ingress.fqdn
```

`transport: 'auto'` is ACA's setting for WebSocket-capable HTTP ingress — **confirm this is correct against `az containerapp ingress` docs for your CLI version before running this**, since Streamlit's live reactivity depends on it and this hasn't been validated against real ARM yet (Global Constraints).

`apiFqdn` is passed in as a parameter here (not read via an `existing` resource lookup) because it's an output of this same template's own `api` resource — reference it directly as `api.properties.configuration.ingress.fqdn` instead of a new parameter if that's simpler once you're editing the real file.

- [ ] **Step 2: Lint-check locally**

```powershell
az bicep build --file iac/app-infra-apps.bicep --stdout | Out-Null
```

Expected: no errors.

- [ ] **Step 3: Update the apps-deploy script**

In `scripts/iac/deploy-app-infra-apps.ps1`, add a prompt for the frontend password and pass it through to the `az deployment group create` call alongside the existing parameters (`postgresAdminPassword`, `openAiKey`, `searchKey`, etc.) — follow the existing `Read-Host -AsSecureString` pattern already in that script for the other secrets.

- [ ] **Step 4: Deploy for real**

```powershell
./scripts/iac/deploy-app-infra-apps.ps1
```

Expected: 5 container apps provisioned/updated (the existing 4 plus `claims-assistant-frontend`), output includes `frontendFqdn`.

- [ ] **Step 5: Verify WebSocket/live behavior for real**

Open the `frontendFqdn` URL in a browser. Confirm the app loads, the password gate works, and interacting with a widget (e.g. typing into the policy number field) updates the page without a full reload — if it doesn't, or the page hangs, this is the real WebSocket-support check Global Constraints flagged as unconfirmed; investigate `az containerapp ingress show --name claims-assistant-frontend` and Streamlit's server flags before considering this task done.

- [ ] **Step 6: Extend the CD workflow**

In `.github/workflows/auto-claims-assistant-cd.yml`, add a `deploy-frontend` job after `build-and-push` (parallel to `deploy-canary`, not chained through the API's canary/promote gate — the frontend is a stateless UI layer with no correctness-critical rollout risk, so it deploys straight to 100% on every push, unlike the API):

```yaml
  deploy-frontend:
    needs: build-and-push
    runs-on: ubuntu-latest
    steps:
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - run: |
          az containerapp update \
            --name claims-assistant-frontend \
            --resource-group claims-assistant-rg \
            --image claimsassistantacr.azurecr.io/claims-assistant:${{ needs.build-and-push.outputs.image-tag }}
```

- [ ] **Step 7: Update the roadmap**

In `docs/superpowers/plans/2026-08-10-roadmap.md`:

```markdown
- [x] Phase 11 — Web frontend
```

- [ ] **Step 8: Commit**

```powershell
git add AutoClaimsAssistant/iac/app-infra-apps.bicep AutoClaimsAssistant/scripts/iac/deploy-app-infra-apps.ps1 .github/workflows/auto-claims-assistant-cd.yml AutoClaimsAssistant/docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "feat: deploy frontend as 5th Container App"
```

---

## Definition of Done for Phase 11

- [ ] `uv run pytest -v -m "not integration"` passes — including all new frontend page tests (Tasks 2-6), no regressions.
- [ ] `uv run pytest -v -m integration` passes — including the new `GET /claims` tests (Task 1), no regressions.
- [ ] `uv run ruff check .` and `uv run mypy src` both clean.
- [ ] Locally, via `docker-compose up`, a full browser session at `http://localhost:8501` can log in, submit a claim, watch it complete, view the recommendation, upload a document, and see it in the claims history table.
- [ ] Deployed to Azure, the same flow works end-to-end against the real `frontendFqdn` URL, including a confirmed-working live/WebSocket UI (Task 8 Step 5).
- [ ] Roadmap's Phase 11 checkbox is checked off.
- [ ] Everything above is committed.

## Lessons Learned (2026-08-27 – 2026-08-28 execution session)

This session also absorbed an unplanned detour: the Azure app-infra layer had been torn down before Task 8 began, so most of the real bugs below surfaced while diagnosing a from-scratch redeploy rather than the frontend code itself.

### Environment/tooling gotchas (local dev)

1. **A native Windows `postgres.exe` service was already bound to port 5432, silently intercepting local Docker Postgres connections.** `docker-compose.yml` and `.env` agreed on `devpassword`, but `uv run pytest -m integration` failed with `asyncpg.exceptions.InvalidPasswordError` regardless — the classic symptom of a stale-volume password mismatch, except the volume was fine. `netstat -ano | findstr :5432` showed *two* PIDs `LISTENING` on the port; `tasklist` resolved one to `postgres.exe` (a Windows Service, unrelated to this project) and the other to `com.docker.backend.exe`. App connections were landing on the native service, which had no idea about `claims_assistant`/`devpassword`. Fix: remapped Docker's host port to `5433` (`docker-compose.yml`'s `ports: - "5433:5432"`, `.env`'s `POSTGRES_PORT=5433`) rather than touching the native service, which might be used by something else on the machine. **Lesson: an `InvalidPasswordError`/auth failure against local Postgres with credentials that visibly match isn't always a stale-volume issue — check `netstat` for a second listener on the port before assuming the app's own config is wrong.**
2. **Fixing #1 broke `docker-compose`'s own `api` service internally, via `env_file: .env`.** The `api` service loads the host `.env` wholesale (`env_file: .env`), so the `POSTGRES_PORT=5433` fix for the *host-side* native-service collision leaked into the container, which tried to reach `postgres:5433` on the Docker-internal network — but the `postgres` container only ever listens on its own internal `5432` regardless of the host port mapping. Result: `ConnectionRefusedError: [Errno 111]` inside the `api` container, a completely different failure mode from #1 despite looking similar. Fix: added an explicit `POSTGRES_PORT: "5432"` under the `api` service's `environment:` block, which takes precedence over `env_file` values. **Lesson: a host-side fix applied via `.env` isn't automatically safe once `env_file:` re-injects that same `.env` into a container — internal Docker-network addressing and host-side addressing are different address spaces and can need different values for the same variable.**

### Real bugs found in test code

3. **`streamlit.testing.v1.AppTest.from_file`'s relative-path resolution is relative to the *calling test file's directory*, not the repo root or CWD.** `AppTest.from_file("src/claims_assistant/frontend/pages/submit.py")` called from `tests/test_frontend_submit_page.py` raised `FileNotFoundError` looking for `tests/src/claims_assistant/...` — confirmed directly from the library's own docstring and the error's resolved path. Fix: write the path relative to `tests/`, i.e. `"../src/claims_assistant/frontend/pages/submit.py"`. **Lesson: this is real, version-specific `AppTest` behavior (`streamlit==1.62.0`) the plan doc had explicitly flagged as unconfirmed — worth checking a library's actual behavior via the first real failure's error message before assuming a snippet is portable.**
4. **Streamlit's full top-to-bottom script rerun on every widget interaction silently undoes `mock.patch` when the patch targets the page's imported name instead of the class's source module.** Task 3's page test patched `claims_assistant.frontend.pages.submit.ClaimsApiClient` (the page's `from ... import ClaimsApiClient` binding) and passed on the initial `at.run()`, but hung for 3+ seconds and raised `RuntimeError: AppTest script run timed out` on `at.button(...).click().run()`. Root cause: Streamlit reruns the *entire script*, including its import statements, on every interaction — the re-executed `from claims_assistant.frontend.api_client import ClaimsApiClient` line re-fetches the real, unpatched class from its source module on every rerun, clobbering the patch. The real client then made a genuine `httpx` call to `http://localhost:8000` (nothing listening), which on this Windows machine stalled a few seconds (IPv6 `::1` attempted before IPv4 fallback) before failing — long enough to blow past `AppTest`'s 3s default timeout and produce a confusing `RuntimeError` instead of a clean assertion failure. Confirmed by ruling out a live server on port 8000 (`netstat`/`docker ps` both empty) before landing on the rerun/import theory, then confirming the fix worked. Fix: patch the class at its **source module** (`claims_assistant.frontend.api_client.ClaimsApiClient`), not the page's imported reference — imports re-resolve from the source module's current attribute on every re-execution, so patching the source survives every rerun. Applied to all four page tests (Tasks 3-6). **Lesson: the usual "patch where it's used, not where it's defined" mock guidance assumes the import runs once — it inverts for any test harness (like Streamlit's) that re-executes the importing module's top level repeatedly.**
5. **A test's own mock data didn't match the real API schema, which would have caused a `KeyError` once real data flowed through.** Task 6's plan snippet mocked `list_claims` returning dicts with only `id`/`status`/`policy_number`, but `history.py`'s `pd.DataFrame(claims)[["id", "status", "policy_number", "vin", "created_at"]]` unconditionally selects all 5 columns — `vin`/`created_at` are always present on a real `ClaimResponse` (`api/claims_schema.py`), so the mock was unrealistic, not the page. Caught by reading the real schema before running the test, rather than after a confusing `KeyError` in CI. **Lesson: when a plan doc's test snippet mocks a shape resembling a real Pydantic response model, cross-check the mock against the model's actual fields before trusting the snippet verbatim — a plan written before a schema exists (or without re-reading it) can drift.**

### Real bugs found in deployment / infra

6. **The frontend's `httpx` client timeout (60s) was too short for a real, cold-start-inclusive pipeline run on Azure.** Every Container App in this project (all 4 pre-existing services plus the new frontend) deploys with `minReplicas: 0`. Locally nothing ever scales to zero, so this cost was invisible until the first real Azure submission. Live and historical `az containerapp logs show` output for the API confirmed the actual pipeline completed successfully server-side (`"Workflow completed after 4 supersteps"`) — it just took **83 seconds** end-to-end, with clearly visible ~20-30s gaps before each MCP server's first response in a burst (consistent with cold starts), well past the client's 60s timeout. This was diagnosed, not guessed: ruled out a genuine hang by comparing wall-clock timestamps between the client's `ReadTimeout` and the server's own completion log line before touching any code. Fix: raised `ClaimsApiClient`'s `httpx.Client` timeout from `60.0` to `180.0`. **Lesson: a `ReadTimeout` on a request that a request-side proxy log shows as `200 OK`/completed further downstream is a client-timeout tuning problem, not a functional bug — confirm via server-side logs and timestamps before assuming the pipeline itself is broken, especially with `minReplicas: 0` anywhere in the path.**
7. **Tearing down and redeploying the Storage account rotates its access keys, but nothing else knows to update.** After redeploying `app-infra-base.bicep` (new `claimsassistantstorage` account, new keys) and `app-infra-apps.bicep` (which picks up the fresh connection string via `az storage account show-connection-string` at deploy time), CI's `integration-test` job still failed with an Azure `ClientAuthenticationError`/"invalid shared key" on both blob-storage tests. Root cause: the GitHub Actions secret `AZURE_STORAGE_CONNECTION_STRING` — read directly by CI, entirely separate from the Bicep-deployed container's env — still held the connection string from before the teardown. This also blocked `eval-gate` and, downstream, the entire CD pipeline (`deploy-frontend` never got a chance to run) until fixed. Fix: refreshed the secret with `az storage account show-connection-string` + `gh secret set AZURE_STORAGE_CONNECTION_STRING`, then `gh run rerun --failed`. **Lesson: same root category as Phase 10's issue #16 (a credential/config with more than one place that needs to know about it) — recreating any Azure resource whose *keys* (not just endpoint) are referenced by a GitHub secret requires refreshing that secret explicitly; Bicep redeploying the container's own env var does not touch CI's independent copy.**

### What worked well, worth repeating

- **Checking `netstat`/`docker ps`/`tasklist` for what's *actually* listening before trusting an error message's stated cause** — both the Postgres port collision (#1) and the httpx-timeout diagnosis (#6) were resolved by looking at real process/log state instead of guessing from the exception type alone.
- **Comparing client-side failure timestamps against server-side completion timestamps in real logs before assuming a "timeout" means "hang."** The Azure cold-start latency (#6) would have been easy to mis-diagnose as a stuck pipeline; the server's own `"Workflow completed"` log line, timestamped, proved otherwise.
- **Redeploying only the specific Azure resource needed (Storage account, or a single Container App's image) instead of a full stack redeploy** when only that piece was missing or stale — kept the Task 8 detour's blast radius small once the platform layer (OpenAI + Search) was confirmed to have survived the teardown.
