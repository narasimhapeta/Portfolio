# Phase 4: Azure AI Search Indexing + Coverage Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [x]`) syntax for tracking progress across the walkthrough.

**Goal:** Index the synthetic 9-document policy corpus (`policy_documents.py`) into Azure AI Search as hybrid-searchable (vector + keyword) chunks, then build a Coverage Agent that grounds its approve/deny/needs-info determination in real retrieved chunks from the policyholder's own policy document (looked up via `policy-db-mcp`), with every citation validated against the actual retrieval set before being returned.

**Architecture:** A new `src/claims_assistant/search/` subpackage owns everything Azure-AI-Search-specific: `chunking.py` splits each rendered policy document into one chunk per `## Section` (7 per document × 9 documents = 63 chunks — verified against the real `render_policy_document()` output while writing this plan), `index_schema.py` defines the hybrid vector+keyword index schema, `clients.py` builds the async `SearchIndexClient`/`SearchClient`, `embeddings.py` wraps `AsyncAzureOpenAI` for embedding generation, `indexer.py` is the one-shot pipeline that chunks the whole corpus, embeds it, and uploads it, and `retrieval.py` does the actual hybrid query (vector + keyword, filtered to one policy's `form_id`) that the Coverage Agent depends on. The Coverage Agent itself lives in `src/claims_assistant/agents/` (alongside Phase 3's Extraction Agent): `coverage_schema.py` defines the structured `CoverageDetermination` output, `coverage_agent.py` wires an `agent_framework.Agent` (same `Agent` + `ChatOptions(response_format=...)` pattern Phase 3 verified) and orchestrates the full flow — **retrieval happens in plain Python before the LLM call, not as agentic tool-calling** — `lookup_policy_by_number()` calls the real `policy-db-mcp` server over stdio (same pattern as `tests/test_mcp_policy_db_server.py`) to get the policyholder's `policy_form_id`, `retrieve_policy_chunks()` (Search) fetches the top-k relevant chunks filtered to that exact document, both get folded into one prompt, and the LLM's structured response's `citations` are validated post-hoc against the real retrieved `chunk_id`s — raising if the model cites something that wasn't actually retrieved. This is a direct implementation of spec §8's requirement: *"the Coverage Agent's prompt requires citing a real retrieved chunk ID, and the API layer validates the cited chunk ID actually exists in the retrieval set before returning it."* Doing retrieval deterministically in code (rather than exposing Search/MCP as LLM-callable tools) keeps the grounding check enforceable and sidesteps unverified `agent_framework` tool-calling surface area this phase doesn't need.

**Tech Stack:** `azure-search-documents` (confirmed **12.0.0** against a scratch venv while writing this plan — see Global Constraints), on top of Phase 3's `agent_framework`/`agent_framework.openai` wiring (`Agent`, `ChatOptions`, `OpenAIChatCompletionClient`, already at `agent-framework-core==1.14.0`/`agent-framework-openai==1.13.0` in this project's `.venv`, unchanged since Phase 3), `openai`'s `AsyncAzureOpenAI` (already installed transitively at **2.54.0** — used directly, not through `agent_framework`, since embeddings aren't part of that SDK's surface) for embedding generation, Phase 1's `policy_documents.py` (corpus) and Phase 2's `policy-db-mcp` (`mcp.ClientSession` + `stdio_client`, same pattern as `tests/test_mcp_policy_db_server.py`), Pydantic v2, pytest + pytest-asyncio (`integration` marker — this phase's integration tests need real Azure AI Search + Azure OpenAI credentials, and Task 7's also need `docker-compose up -d postgres` since they go through `policy-db-mcp`).

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§3.1 Coverage Agent, §4 model tiering, §5.2 Azure AI Search, §5.3 MCP servers, §8 citation-validity requirement)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All I/O-bound functions are `async def` (per Phase 0's async I/O constraint) — this phase uses the **async** variants of the Search SDK (`azure.search.documents.aio.SearchClient`, `azure.search.documents.indexes.aio.SearchIndexClient`), confirmed to exist with matching signatures to their sync counterparts.
- Every dependency addition goes through `uv add`.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Tests that make real Azure AI Search / Azure OpenAI / MCP-over-Postgres calls are `pytest.mark.integration` (this phase's equivalent of prior phases' "requires external services" precondition) — Task 7's tests additionally require `docker-compose up -d postgres` with the DB seeded, since `lookup_policy_by_number()` goes through `policy-db-mcp`.
- **No Azure AI Search resource exists yet** (confirmed: only `claims-assistant-openai` is provisioned, per [[project_auto_claims_assistant]] memory / `az cognitiveservices account deployment list` showing only the `extraction-agent` deployment). Task 1 provisions a new Azure AI Search resource **and** two new deployments on the *existing* `claims-assistant-openai` resource (reuse, not a second OpenAI resource) — per the working agreement, all `az` commands are run by the user themselves, not by the assistant.
- **Confirmed against the actually-installed/live systems while writing this plan** — do not trust trained/web knowledge of the Azure AI Search Python SDK's exact API surface, same lesson as Phase 2's `mcp` and Phase 3's `agent_framework` surprises:
  - Installed via a scratch venv: `azure-search-documents==12.0.0`. Verify again against whatever `uv add azure-search-documents` actually resolves in this project's `.venv` in Task 1, Step 1 — if it differs, re-run the inspection commands below rather than trusting this list.
  - **`azure-search-documents`'s async `.aio` clients require `aiohttp` as a separate dependency.** `SearchIndexClient`/`SearchClient` build an async transport pipeline at construction time — even before any network call — and default to `AioHttpTransport`, which hard-imports `aiohttp`. Neither `azure-search-documents` nor any existing project dependency pulls it in transitively (confirmed: none of `fastapi`, `uvicorn[standard]`, `mcp[cli]`, `sqlalchemy[asyncio]`, `agent-framework-core`/`-openai` depend on it). Without it, constructing either async client raises `ModuleNotFoundError: No module named 'aiohttp'` — confirmed directly in the scratch venv. Task 1, Step 4 installs it alongside `azure-search-documents`.
  - Index/field building: `from azure.search.documents.indexes.models import SearchIndex, SimpleField, SearchableField, SearchField, SearchFieldDataType, VectorSearch, VectorSearchProfile, HnswAlgorithmConfiguration`. `SimpleField(*, name, type, key=False, filterable=False, sortable=False, facetable=False, ...)` and `SearchableField(*, name, filterable=False, ...)` are plain factory **functions** (not classes) that both return a `SearchField` — this matches trained knowledge of the pre-12.0 API and still holds in 12.0.0.
  - **`SearchFieldDataType.Collection(...)` needs `# type: ignore[operator]`.** It's attached to the enum via runtime monkey-patching (`SearchFieldDataType.Collection = staticmethod(...)` inside the SDK's own `_patch.py`, which itself carries a `# type: ignore`) rather than being a real classmethod mypy can see — `mypy --strict` reports `"Enum" not callable [operator]` on any call site without the ignore comment. Verified by running `mypy --strict` against a sample file both with and without the ignore.
  - Vector field config: a vector `SearchField` needs `type=SearchFieldDataType.Collection(SearchFieldDataType.SINGLE)`, `searchable=True`, `vector_search_dimensions=<int>`, `vector_search_profile_name=<str>` (verified these exact keyword names by reading the SDK's `SearchField` class docstring). `VectorSearch(profiles=[VectorSearchProfile(name=..., algorithm_configuration_name=...)], algorithms=[HnswAlgorithmConfiguration(name=...)])` — field names confirmed by reading the SDK source directly (`_models.py`), not assumed from memory.
  - Clients: `SearchIndexClient(endpoint: str, credential)` and `SearchClient(endpoint: str, index_name: str, credential)`, both from `.aio` submodules for async use, credential is `azure.core.credentials.AzureKeyCredential(api_key)` (this project uses API-key auth throughout, not Azure AD — no `azure-identity` dependency needed).
  - Hybrid search: `SearchClient.search(search_text: str | None, *, vector_queries: list[VectorQuery] | None, filter: str | None, select: list[str] | None, top: int | None, ...)` — passing both `search_text` (keyword) and `vector_queries` (vector) in the same call **is** hybrid search; there's no separate "hybrid mode" flag. `VectorizedQuery(vector: list[float], k_nearest_neighbors: int, fields: str)` (from `azure.search.documents.models`) is how you pass a raw embedding vector into `vector_queries`.
  - `search()` returns an `AsyncSearchItemPaged[dict]` — iterate with `async for result in results:`; each `result` is a plain `dict` of the selected fields plus a `"@search.score"` key. `await results.get_count()` (itself a coroutine, despite what its type hint on the outer class suggests — confirmed via `inspect.iscoroutinefunction`) returns the total match count when `include_total_count=True` was passed to `search()`.
  - `SearchIndexClient.create_or_update_index(index: SearchIndex)` and `.get_index(name: str)` are both coroutines on the async client.
  - Embeddings: **not** part of `agent_framework` — use `openai.AsyncAzureOpenAI(azure_endpoint=..., api_key=..., api_version=...)` directly (already installed transitively at `2.54.0` via `agent-framework-openai`), and call `await client.embeddings.create(model=<deployment_name>, input=<str | list[str]>)` → `response.data[i].embedding: list[float]`. Pass the deployment name as `model=` per-call (same explicit-`Settings`-driven style Phase 3 used for chat), not via a client-level `azure_deployment=`.
  - If a future `uv sync`/`uv add` pulls a different `azure-search-documents` version and something above breaks, re-run this same inspection (`uv pip show azure-search-documents` — or `python -c "import importlib.metadata; print(importlib.metadata.version('azure-search-documents'))"` since this project's `uv`-managed venv has no `pip`, read `azure/search/documents/indexes/models/_models.py` for field names, `_patch.py` for the `SimpleField`/`SearchableField`/`Collection` monkey-patches) rather than guessing.
- **Live Azure OpenAI model catalog re-checked while writing this plan** (`az cognitiveservices account list-models --name claims-assistant-openai --resource-group claims-assistant-rg`), per the same discipline Phase 3 applied: the catalog now includes `gpt-5.4` (non-mini, version `2026-03-05`, GA, `GlobalStandard`/`DataZoneStandard`/others) as the current full-tier match for spec §4's "GPT-5 (full)" row — this is what Task 1 deploys for the Coverage Agent's chat model, alongside the already-deployed `gpt-5.4-mini` (Phase 3, unrelated to this phase). Embedding models `text-embedding-ada-002`, `text-embedding-3-small`, and `text-embedding-3-large` are all GA and available on this resource; `text-embedding-3-small` (version `1`, 1536 dimensions, `Standard`/`GlobalStandard`/`DataZoneStandard` SKUs) is deployed in Task 1 — smallest/cheapest embedding model, and 1536 dimensions is plenty for a 63-chunk corpus. Re-check the catalog again if Task 1's deployment step fails with a model-not-found error — the catalog moves fast.

---

### Task 1: Azure resource provisioning, dependency, config

**Files:**
- Modify: `pyproject.toml`, `uv.lock` (via `uv add`)
- Modify: `src/claims_assistant/config.py`
- Modify: `.env.example`
- Modify: `tests/test_config.py`
- Create: `src/claims_assistant/search/__init__.py`
- Test: `tests/test_search_setup.py`

**Interfaces:**
- Consumes: nothing new (first task of the phase).
- Produces: `azure.search.documents` importable; `Settings.azure_openai_coverage_deployment`, `.azure_openai_embedding_deployment`, `.azure_search_endpoint`, `.azure_search_api_key`, `.azure_search_index_name: str` fields; the `claims_assistant.search` subpackage Tasks 2–6 add modules to.

- [x] **Step 1: Provision the Azure AI Search resource**

Run (PowerShell) — `claims-assistant-rg` resource group, `free` SKU (cost-conscious per spec §7, and the 63-chunk corpus is nowhere near its 50MB/3-index limits), region **`centralus`** (not `eastus2` — `eastus2` returned `InsufficientResourcesAvailable` for the free SKU at execution time; the Search service doesn't need to be co-located with `claims-assistant-openai`, cross-region only affects latency for a demo workload like this). If the name is taken (Search service names are globally unique), pick a different suffix and use it consistently below.

```powershell
az search service create --name claims-assistant-search --resource-group claims-assistant-rg --sku free --location centralus
az search admin-key show --service-name claims-assistant-search --resource-group claims-assistant-rg --query primaryKey -o tsv
```

Note the endpoint is `https://claims-assistant-search.search.windows.net` (your service name + `.search.windows.net`) and save the printed admin key — both go in `.env` in Step 6.

- [x] **Step 2: Add the Coverage Agent's chat deployment**

Reuses the existing `claims-assistant-openai` resource (per the working agreement — no second OpenAI resource). `gpt-5.4` is the current full-tier model per the live catalog check above.

```powershell
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name coverage-agent --model-name gpt-5.4 --model-version "2026-03-05" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
```

If this fails with a capacity/quota error, retry with a lower `--sku-capacity` (e.g. `5`) — this is a demo workload, not production traffic.

- [x] **Step 3: Add the embedding deployment**

```powershell
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name policy-embeddings --model-name text-embedding-3-small --model-version "1" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
```

- [x] **Step 4: Add the Azure AI Search SDK dependency**

```powershell
uv add azure-search-documents aiohttp
```

`aiohttp` is required alongside `azure-search-documents` — the async `.aio` clients used throughout this phase default to an `aiohttp`-based transport and fail to construct without it (see Global Constraints).

Then re-verify the Global Constraints' SDK claims against whatever version this actually resolves (`python -c "import importlib.metadata; print(importlib.metadata.version('azure-search-documents'))"` in the project `.venv`) — proceed only once confirmed, same as Phase 3's Task 1 re-check.

- [x] **Step 5: Extend the config test**

Replace `test_settings_reads_from_env` in `tests/test_config.py` with:

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
    monkeypatch.setenv("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com")
    monkeypatch.setenv("AZURE_OPENAI_API_KEY", "test-key")
    monkeypatch.setenv("AZURE_OPENAI_CHAT_DEPLOYMENT", "test-deployment")
    monkeypatch.setenv("AZURE_OPENAI_API_VERSION", "2024-12-01-preview")
    monkeypatch.setenv("AZURE_OPENAI_COVERAGE_DEPLOYMENT", "test-coverage-deployment")
    monkeypatch.setenv("AZURE_OPENAI_EMBEDDING_DEPLOYMENT", "test-embedding-deployment")
    monkeypatch.setenv("AZURE_SEARCH_ENDPOINT", "https://example.search.windows.net")
    monkeypatch.setenv("AZURE_SEARCH_API_KEY", "test-search-key")
    monkeypatch.setenv("AZURE_SEARCH_INDEX_NAME", "test-policy-documents")

    settings = Settings()

    assert settings.app_env == "test"
    assert settings.postgres_host == "db.example"
    assert settings.postgres_port == 5433
    assert settings.postgres_dsn == (
        "postgresql://testuser:testpass@db.example:5433/testdb"
    )
    assert settings.postgres_async_dsn == (
        "postgresql+asyncpg://testuser:testpass@db.example:5433/testdb"
    )
    assert settings.azure_openai_endpoint == "https://example.openai.azure.com"
    assert settings.azure_openai_api_key == "test-key"
    assert settings.azure_openai_chat_deployment == "test-deployment"
    assert settings.azure_openai_api_version == "2024-12-01-preview"
    assert settings.azure_openai_coverage_deployment == "test-coverage-deployment"
    assert settings.azure_openai_embedding_deployment == "test-embedding-deployment"
    assert settings.azure_search_endpoint == "https://example.search.windows.net"
    assert settings.azure_search_api_key == "test-search-key"
    assert settings.azure_search_index_name == "test-policy-documents"


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [x] **Step 6: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'azure_openai_coverage_deployment'`

- [x] **Step 7: Add the new settings fields**

In `src/claims_assistant/config.py`, add these fields to the `Settings` class (after the existing `azure_openai_api_version` field):

```python
    azure_openai_coverage_deployment: str = ""
    azure_openai_embedding_deployment: str = ""
    azure_search_endpoint: str = ""
    azure_search_api_key: str = ""
    azure_search_index_name: str = "policy-documents"
```

- [x] **Step 8: Run the test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [x] **Step 9: Document the new env vars**

Add to `.env.example` (and your own `.env`, with your real values from Steps 1–3):

```env
AZURE_OPENAI_COVERAGE_DEPLOYMENT=coverage-agent
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=policy-embeddings
AZURE_SEARCH_ENDPOINT=https://your-search-service.search.windows.net
AZURE_SEARCH_API_KEY=your-search-admin-key
AZURE_SEARCH_INDEX_NAME=policy-documents
```

- [x] **Step 10: Create the subpackage**

Create `src/claims_assistant/search/__init__.py` (empty file).

- [x] **Step 11: Write a smoke test (no network call)**

```python
# tests/test_search_setup.py
from azure.core.credentials import AzureKeyCredential
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.aio import SearchIndexClient
from azure.search.documents.indexes.models import SearchFieldDataType, SimpleField


def test_search_index_client_constructs_without_network_call():
    client = SearchIndexClient(
        "https://example.search.windows.net", AzureKeyCredential("test-key")
    )

    assert isinstance(client, SearchIndexClient)


def test_search_client_constructs_without_network_call():
    client = SearchClient(
        "https://example.search.windows.net", "policy-documents", AzureKeyCredential("test-key")
    )

    assert isinstance(client, SearchClient)


def test_simple_field_builds_a_search_field():
    field = SimpleField(name="chunk_id", type=SearchFieldDataType.STRING, key=True)

    assert field.name == "chunk_id"
    assert field.key is True
```

- [x] **Step 12: Run the test to verify it passes**

Run: `uv run pytest tests/test_search_setup.py -v`
Expected: PASS (3 passed) — no network call is made; these only exercise object construction.

- [x] **Step 13: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 14: Commit**

```powershell
git add pyproject.toml uv.lock src/claims_assistant/config.py src/claims_assistant/search/__init__.py .env.example tests/test_config.py tests/test_search_setup.py
git commit -m "feat: add Azure AI Search dependency and config"
```

---

### Task 2: Policy document chunking

**Files:**
- Create: `src/claims_assistant/search/chunking.py`
- Test: `tests/test_chunking.py`

**Interfaces:**
- Consumes: `render_policy_document()` (Phase 1's `policy_documents.py`) — used only in the test, to chunk real rendered output.
- Produces: `chunking.py`'s `PolicyChunk` dataclass (`chunk_id`, `form_id`, `state`, `tier`, `section_title`, `content`) and `chunk_policy_document(form_id: str, state: str, tier: str, document_text: str) -> list[PolicyChunk]`. Task 5's `indexer.py` and Task 6's `retrieval.py` (via `RetrievedChunk`, a separate but field-compatible model) depend on this shape.

- [x] **Step 1: Write the failing chunking test**

This test runs `chunk_policy_document` against the *real* `render_policy_document("CA", "full_coverage")` output — verified while writing this plan to produce exactly 7 chunks with these exact titles and this exact `chunk_id` scheme.

```python
# tests/test_chunking.py
from claims_assistant.policy_documents import render_policy_document
from claims_assistant.search.chunking import chunk_policy_document


def test_chunk_policy_document_produces_one_chunk_per_section():
    text = render_policy_document("CA", "full_coverage")

    chunks = chunk_policy_document("CA-FULL-COVERAGE", "CA", "full_coverage", text)

    assert [c.section_title for c in chunks] == [
        "Section 1. Definitions",
        "Section 2. Liability Coverage",
        "Section 3. Physical Damage Coverage",
        "Section 4. Exclusions",
        "Section 5. Claims Filing Procedures",
        "Section 6. State-Specific Endorsement",
        "Summary",
    ]


def test_chunk_ids_are_deterministic_and_namespaced_by_form_id():
    text = render_policy_document("CA", "full_coverage")

    chunks = chunk_policy_document("CA-FULL-COVERAGE", "CA", "full_coverage", text)

    assert chunks[2].chunk_id == "CA-FULL-COVERAGE_section-3-physical-damage-coverage"
    assert all(c.chunk_id.startswith("CA-FULL-COVERAGE_") for c in chunks)
    assert len({c.chunk_id for c in chunks}) == 7  # all unique


def test_chunk_content_stays_scoped_to_its_own_section():
    text = render_policy_document("CA", "liability_only")

    chunks = chunk_policy_document("CA-LIABILITY-ONLY", "CA", "liability_only", text)

    physical_damage_chunk = next(
        c for c in chunks if c.section_title == "Section 3. Physical Damage Coverage"
    )
    assert "does NOT include Collision or Comprehensive" in physical_damage_chunk.content
    other_chunks = [c for c in chunks if c is not physical_damage_chunk]
    assert all("does NOT include Collision" not in c.content for c in other_chunks)


def test_chunk_metadata_fields_are_populated():
    text = render_policy_document("TX", "comprehensive_collision")

    chunks = chunk_policy_document(
        "TX-COMPREHENSIVE-COLLISION", "TX", "comprehensive_collision", text
    )

    assert all(c.form_id == "TX-COMPREHENSIVE-COLLISION" for c in chunks)
    assert all(c.state == "TX" for c in chunks)
    assert all(c.tier == "comprehensive_collision" for c in chunks)
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_chunking.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.search.chunking'`

- [x] **Step 3: Write the chunker**

```python
# src/claims_assistant/search/chunking.py
from __future__ import annotations

import re
from dataclasses import dataclass

_SECTION_BOUNDARY = re.compile(r"\n(?=## )")


@dataclass(frozen=True)
class PolicyChunk:
    chunk_id: str
    form_id: str
    state: str
    tier: str
    section_title: str
    content: str


def _slugify_section_title(section_title: str) -> str:
    return section_title.lower().replace(". ", "-").replace(" ", "-").replace(".", "")


def chunk_policy_document(
    form_id: str, state: str, tier: str, document_text: str
) -> list[PolicyChunk]:
    """Split a rendered policy document into one chunk per `## Section` heading.

    The document's leading title/metadata block (before the first `## ` heading) is
    dropped — `state`/`tier`/`form_id` are already carried as index fields, so it adds
    no new information and isn't a citable clause.
    """
    chunks = []
    for raw_section in _SECTION_BOUNDARY.split(document_text):
        section = raw_section.strip()
        if not section.startswith("## "):
            continue
        title_line, _, _ = section.partition("\n")
        section_title = title_line.removeprefix("## ").strip()
        chunk_id = f"{form_id}_{_slugify_section_title(section_title)}"
        chunks.append(
            PolicyChunk(
                chunk_id=chunk_id,
                form_id=form_id,
                state=state,
                tier=tier,
                section_title=section_title,
                content=section,
            )
        )
    return chunks
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_chunking.py -v`
Expected: PASS (4 passed)

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/search/chunking.py tests/test_chunking.py
git commit -m "feat: add policy document chunker"
```

---

### Task 3: Index schema and Search clients

**Files:**
- Create: `src/claims_assistant/search/index_schema.py`
- Create: `src/claims_assistant/search/clients.py`
- Test: `tests/test_index_schema.py`
- Test: `tests/test_search_clients.py`

**Interfaces:**
- Consumes: `Settings` (Task 1's `config.py`).
- Produces: `index_schema.py`'s `build_policy_index(index_name: str) -> SearchIndex`. `clients.py`'s `build_search_index_client(settings: Settings) -> SearchIndexClient` and `build_search_client(settings: Settings) -> SearchClient` (both async clients). Task 5's `indexer.py` and Task 6's `retrieval.py` import all three.

- [x] **Step 1: Write the failing index schema test**

```python
# tests/test_index_schema.py
from claims_assistant.search.index_schema import build_policy_index


def test_build_policy_index_has_expected_fields():
    index = build_policy_index("policy-documents")

    field_names = {f.name for f in index.fields}
    assert field_names == {
        "chunk_id",
        "form_id",
        "state",
        "tier",
        "section_title",
        "content",
        "content_vector",
    }


def test_chunk_id_is_the_key_field():
    index = build_policy_index("policy-documents")

    key_fields = [f for f in index.fields if f.key]
    assert len(key_fields) == 1
    assert key_fields[0].name == "chunk_id"


def test_content_vector_field_is_configured_for_vector_search():
    index = build_policy_index("policy-documents")

    vector_field = next(f for f in index.fields if f.name == "content_vector")
    assert vector_field.vector_search_dimensions == 1536
    assert vector_field.vector_search_profile_name is not None
    assert index.vector_search is not None
    profile_names = {p.name for p in index.vector_search.profiles}
    assert vector_field.vector_search_profile_name in profile_names


def test_form_id_state_tier_are_filterable():
    index = build_policy_index("policy-documents")

    filterable = {f.name for f in index.fields if f.filterable}
    assert {"form_id", "state", "tier"} <= filterable
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_index_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.search.index_schema'`

- [x] **Step 3: Write the index schema**

```python
# src/claims_assistant/search/index_schema.py
from __future__ import annotations

from azure.search.documents.indexes.models import (
    HnswAlgorithmConfiguration,
    SearchableField,
    SearchField,
    SearchFieldDataType,
    SearchIndex,
    SimpleField,
    VectorSearch,
    VectorSearchProfile,
)

VECTOR_DIMENSIONS = 1536
HNSW_ALGORITHM_NAME = "policy-hnsw"
VECTOR_PROFILE_NAME = "policy-vector-profile"


def build_policy_index(index_name: str) -> SearchIndex:
    fields = [
        SimpleField(name="chunk_id", type=SearchFieldDataType.STRING, key=True),
        SimpleField(name="form_id", type=SearchFieldDataType.STRING, filterable=True),
        SimpleField(
            name="state", type=SearchFieldDataType.STRING, filterable=True, facetable=True
        ),
        SimpleField(
            name="tier", type=SearchFieldDataType.STRING, filterable=True, facetable=True
        ),
        SearchableField(name="section_title"),
        SearchableField(name="content"),
        SearchField(
            name="content_vector",
            type=SearchFieldDataType.Collection(SearchFieldDataType.SINGLE),  # type: ignore[operator]
            searchable=True,
            vector_search_dimensions=VECTOR_DIMENSIONS,
            vector_search_profile_name=VECTOR_PROFILE_NAME,
        ),
    ]
    vector_search = VectorSearch(
        profiles=[
            VectorSearchProfile(
                name=VECTOR_PROFILE_NAME, algorithm_configuration_name=HNSW_ALGORITHM_NAME
            )
        ],
        algorithms=[HnswAlgorithmConfiguration(name=HNSW_ALGORITHM_NAME)],
    )
    return SearchIndex(name=index_name, fields=fields, vector_search=vector_search)
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_index_schema.py -v`
Expected: PASS (4 passed)

- [x] **Step 5: Write the client builders**

```python
# src/claims_assistant/search/clients.py
from __future__ import annotations

from azure.core.credentials import AzureKeyCredential
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.aio import SearchIndexClient

from claims_assistant.config import Settings


def build_search_index_client(settings: Settings) -> SearchIndexClient:
    return SearchIndexClient(
        settings.azure_search_endpoint, AzureKeyCredential(settings.azure_search_api_key)
    )


def build_search_client(settings: Settings) -> SearchClient:
    return SearchClient(
        settings.azure_search_endpoint,
        settings.azure_search_index_name,
        AzureKeyCredential(settings.azure_search_api_key),
    )
```

- [x] **Step 6: Write the failing integration test**

This test needs real Azure AI Search credentials in `.env` (Task 1). It actually creates/updates the index on your real Search service.

```python
# tests/test_search_clients.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.clients import build_search_index_client
from claims_assistant.search.index_schema import build_policy_index

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_create_or_update_index_round_trips_the_schema():
    settings = get_settings()
    index = build_policy_index(settings.azure_search_index_name)

    async with build_search_index_client(settings) as index_client:
        created = await index_client.create_or_update_index(index)
        fetched = await index_client.get_index(settings.azure_search_index_name)

    assert created.name == settings.azure_search_index_name
    fetched_field_names = {f.name for f in fetched.fields}
    assert fetched_field_names == {
        "chunk_id",
        "form_id",
        "state",
        "tier",
        "section_title",
        "content",
        "content_vector",
    }
```

- [x] **Step 7: Run the test to verify it passes**

Run: `uv run pytest tests/test_search_clients.py -v`
Expected: PASS (1 passed). If it fails with an authentication or 403 error, double-check `.env`'s `AZURE_SEARCH_ENDPOINT`/`AZURE_SEARCH_API_KEY` match the service and admin key from Task 1, Step 1.

- [x] **Step 8: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 9: Commit**

```powershell
git add src/claims_assistant/search/index_schema.py src/claims_assistant/search/clients.py tests/test_index_schema.py tests/test_search_clients.py
git commit -m "feat: add Azure AI Search index schema and client builders"
```

---

### Task 4: Embedding generation

**Files:**
- Create: `src/claims_assistant/search/embeddings.py`
- Test: `tests/test_embeddings.py`

**Interfaces:**
- Consumes: `Settings` (Task 1's `config.py`).
- Produces: `embeddings.py`'s `build_embedding_client(settings: Settings) -> AsyncAzureOpenAI` and `async def embed_texts(client: AsyncAzureOpenAI, model: str, texts: list[str]) -> list[list[float]]`. Task 5's `indexer.py` and Task 6's `retrieval.py` import both.

- [x] **Step 1: Write the failing integration test**

This test needs real Azure OpenAI credentials in `.env`, including `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` (Task 1).

```python
# tests/test_embeddings.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.embeddings import build_embedding_client, embed_texts

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_embed_texts_returns_one_vector_per_input():
    settings = get_settings()
    client = build_embedding_client(settings)

    vectors = await embed_texts(
        client, settings.azure_openai_embedding_deployment, ["hello world", "goodbye world"]
    )

    assert len(vectors) == 2
    assert len(vectors[0]) == 1536
    assert len(vectors[1]) == 1536
    assert vectors[0] != vectors[1]
```

- [x] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_embeddings.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.search.embeddings'`

- [x] **Step 3: Write the embedding client**

```python
# src/claims_assistant/search/embeddings.py
from __future__ import annotations

from openai import AsyncAzureOpenAI

from claims_assistant.config import Settings


def build_embedding_client(settings: Settings) -> AsyncAzureOpenAI:
    return AsyncAzureOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


async def embed_texts(client: AsyncAzureOpenAI, model: str, texts: list[str]) -> list[list[float]]:
    response = await client.embeddings.create(model=model, input=texts)
    return [item.embedding for item in response.data]
```

- [x] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_embeddings.py -v`
Expected: PASS (1 passed). If it fails with a 404, double-check `AZURE_OPENAI_EMBEDDING_DEPLOYMENT` matches the deployment name from Task 1, Step 3 exactly.

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/search/embeddings.py tests/test_embeddings.py
git commit -m "feat: add embedding generation via AsyncAzureOpenAI"
```

---

### Task 5: Corpus indexing pipeline

**Files:**
- Create: `src/claims_assistant/search/indexer.py`
- Test: `tests/test_indexer.py`

**Interfaces:**
- Consumes: `STATE_MINIMUMS`, `TIER_TEXT`, `render_policy_document()` (Phase 1's `policy_documents.py`); `chunk_policy_document()` (Task 2); `build_policy_index()` (Task 3's `index_schema.py`); `build_search_index_client()`, `build_search_client()` (Task 3's `clients.py`); `build_embedding_client()`, `embed_texts()` (Task 4); `Settings` (Task 1).
- Produces: `indexer.py`'s `async def index_policy_corpus(settings: Settings) -> int` (returns number of chunks indexed). Not consumed by later tasks in this plan — it's the one-shot pipeline you run to populate the index that Task 6's retrieval and Task 7's Coverage Agent then query.

- [x] **Step 1: Write the pipeline**

Unlike most tasks in this plan, there's no small unit to TDD here first — this task *is* the integration test (it exercises Tasks 2–4 together against real Azure services and has no meaningful pure-Python subset to isolate). Write the implementation directly, then verify it via the integration test in Step 2.

```python
# src/claims_assistant/search/indexer.py
from __future__ import annotations

from claims_assistant.config import Settings
from claims_assistant.policy_documents import STATE_MINIMUMS, TIER_TEXT, render_policy_document
from claims_assistant.search.chunking import PolicyChunk, chunk_policy_document
from claims_assistant.search.clients import build_search_client, build_search_index_client
from claims_assistant.search.embeddings import build_embedding_client, embed_texts
from claims_assistant.search.index_schema import build_policy_index


def _chunk_full_corpus() -> list[PolicyChunk]:
    chunks = []
    for state in STATE_MINIMUMS:
        for tier in TIER_TEXT:
            form_id = f"{state}-{tier.upper().replace('_', '-')}"
            document_text = render_policy_document(state, tier)
            chunks.extend(chunk_policy_document(form_id, state, tier, document_text))
    return chunks


async def index_policy_corpus(settings: Settings) -> int:
    chunks = _chunk_full_corpus()

    async with build_search_index_client(settings) as index_client:
        await index_client.create_or_update_index(
            build_policy_index(settings.azure_search_index_name)
        )

    async with build_embedding_client(settings) as embedding_client:
        vectors = await embed_texts(
            embedding_client,
            settings.azure_openai_embedding_deployment,
            [chunk.content for chunk in chunks],
        )

    documents = [
        {
            "chunk_id": chunk.chunk_id,
            "form_id": chunk.form_id,
            "state": chunk.state,
            "tier": chunk.tier,
            "section_title": chunk.section_title,
            "content": chunk.content,
            "content_vector": vector,
        }
        for chunk, vector in zip(chunks, vectors, strict=True)
    ]

    async with build_search_client(settings) as search_client:
        await search_client.upload_documents(documents=documents)

    return len(documents)
```

- [x] **Step 2: Write the failing integration test**

This test needs real Azure AI Search + Azure OpenAI credentials in `.env`, and makes 63 embedding calls' worth of tokens in one batched request plus one document upload.

```python
# tests/test_indexer.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.clients import build_search_client
from claims_assistant.search.indexer import index_policy_corpus

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_index_policy_corpus_indexes_all_63_chunks():
    settings = get_settings()

    indexed_count = await index_policy_corpus(settings)

    assert indexed_count == 63

    async with build_search_client(settings) as search_client:
        results = await search_client.search(search_text="*", include_total_count=True, top=1)
        total = await results.get_count()

    assert total == 63
```

- [x] **Step 3: Run the test**

Run: `uv run pytest tests/test_indexer.py -v`
Expected: PASS (1 passed). If `indexed_count` isn't 63, re-check Task 2's chunker against all 9 forms (not just the one it was tested against) — `9 states×tiers × 7 sections = 63` is the expected count.

- [x] **Step 4: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 5: Commit**

```powershell
git add src/claims_assistant/search/indexer.py tests/test_indexer.py
git commit -m "feat: add policy corpus indexing pipeline"
```

---

### Task 6: Hybrid retrieval

**Files:**
- Create: `src/claims_assistant/search/retrieval.py`
- Test: `tests/test_retrieval.py`

**Interfaces:**
- Consumes: `build_search_client()` (Task 3's `clients.py`); `build_embedding_client()`, `embed_texts()` (Task 4's `embeddings.py`); `Settings` (Task 1).
- Produces: `retrieval.py`'s `RetrievedChunk` Pydantic model (`chunk_id`, `form_id`, `section_title`, `content`, `score`) and `async def retrieve_policy_chunks(settings: Settings, form_id: str, query_text: str, top: int = 4) -> list[RetrievedChunk]`. Task 7's `coverage_agent.py` imports both.

**Precondition:** Task 5's `index_policy_corpus()` must have been run against your Search service at least once — this task queries real indexed data.

- [x] **Step 1: Write the failing integration test**

Queries the real index for CA's liability-only policy — its Section 3 chunk explicitly states it does *not* cover collision, which is exactly the clause a "does my policy cover this collision?" query should surface.

```python
# tests/test_retrieval.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.retrieval import retrieve_policy_chunks

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_is_scoped_to_the_requested_form_id():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert len(results) > 0
    assert all(r.form_id == "CA-LIABILITY-ONLY" for r in results)


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_surfaces_the_relevant_clause():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert any("does NOT include Collision" in r.content for r in results)


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_does_not_leak_other_documents():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert all(r.chunk_id.startswith("CA-LIABILITY-ONLY_") for r in results)
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_retrieval.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.search.retrieval'`

- [x] **Step 3: Write the retrieval function**

```python
# src/claims_assistant/search/retrieval.py
from __future__ import annotations

from azure.search.documents.models import VectorizedQuery
from pydantic import BaseModel

from claims_assistant.config import Settings
from claims_assistant.search.clients import build_search_client
from claims_assistant.search.embeddings import build_embedding_client, embed_texts


class RetrievedChunk(BaseModel):
    chunk_id: str
    form_id: str
    section_title: str
    content: str
    score: float


async def retrieve_policy_chunks(
    settings: Settings, form_id: str, query_text: str, top: int = 4
) -> list[RetrievedChunk]:
    async with build_embedding_client(settings) as embedding_client:
        vectors = await embed_texts(
            embedding_client, settings.azure_openai_embedding_deployment, [query_text]
        )
    vector_query = VectorizedQuery(
        vector=vectors[0], k_nearest_neighbors=top, fields="content_vector"
    )

    async with build_search_client(settings) as search_client:
        results = await search_client.search(
            search_text=query_text,
            vector_queries=[vector_query],
            filter=f"form_id eq '{form_id}'",
            select=["chunk_id", "form_id", "section_title", "content"],
            top=top,
        )
        return [
            RetrievedChunk(
                chunk_id=result["chunk_id"],
                form_id=result["form_id"],
                section_title=result["section_title"],
                content=result["content"],
                score=result["@search.score"],
            )
            async for result in results
        ]
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_retrieval.py -v`
Expected: PASS (3 passed)

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/search/retrieval.py tests/test_retrieval.py
git commit -m "feat: add hybrid vector+keyword retrieval scoped to one policy document"
```

---

### Task 7: Coverage schema and Coverage Agent

**Files:**
- Create: `src/claims_assistant/agents/coverage_schema.py`
- Create: `src/claims_assistant/agents/coverage_agent.py`
- Test: `tests/test_coverage_schema.py`
- Test: `tests/test_coverage_agent_citation_validation.py`
- Test: `tests/test_coverage_agent.py`

**Interfaces:**
- Consumes: `Agent`, `ChatOptions` (`agent_framework`); `OpenAIChatCompletionClient` (`agent_framework.openai`); `Settings`, `get_settings()` (`config.py`); `RetrievedChunk`, `retrieve_policy_chunks()` (Task 6's `retrieval.py`); `PolicyLookupResult` (Phase 2's `mcp_servers/policy_db.py`); `ClientSession`, `StdioServerParameters`, `stdio_client` (`mcp`).
- Produces: `coverage_schema.py`'s `CoverageDetermination` (`determination: Literal["approve", "deny", "needs_info"]`, `rationale: str`, `citations: list[str]`). `coverage_agent.py`'s `build_coverage_agent(settings: Settings) -> Agent`, `async def lookup_policy_by_number(policy_number: str) -> PolicyLookupResult`, `async def determine_coverage(agent: Agent, settings: Settings, policy_number: str, claim_narrative: str) -> CoverageDetermination`. Not consumed further in this plan — Phase 6 (Supervisor orchestration graph) wires this into the fan-out alongside the Fraud-Risk Agent.

- [x] **Step 1: Write the failing schema test**

```python
# tests/test_coverage_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.coverage_schema import CoverageDetermination


def test_coverage_determination_validates():
    determination = CoverageDetermination(
        determination="approve",
        rationale="Collision coverage applies per Section 3.1.",
        citations=["CA-FULL-COVERAGE_section-3-physical-damage-coverage"],
    )

    assert determination.determination == "approve"
    assert determination.citations == ["CA-FULL-COVERAGE_section-3-physical-damage-coverage"]


def test_coverage_determination_rejects_invalid_determination_value():
    with pytest.raises(ValidationError):
        CoverageDetermination(determination="maybe", rationale="unclear", citations=[])
```

- [x] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_coverage_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.coverage_schema'`

- [x] **Step 3: Write the schema**

```python
# src/claims_assistant/agents/coverage_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel


class CoverageDetermination(BaseModel):
    determination: Literal["approve", "deny", "needs_info"]
    rationale: str
    citations: list[str]
```

- [x] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_coverage_schema.py -v`
Expected: PASS (2 passed)

- [x] **Step 5: Write the failing citation-validation unit test**

This tests the pure, no-network citation-grounding check (spec §8) in isolation, before it's wired into the full agent flow in Step 7.

```python
# tests/test_coverage_agent_citation_validation.py
import pytest

from claims_assistant.agents.coverage_agent import _validate_citations
from claims_assistant.search.retrieval import RetrievedChunk

_RETRIEVED = [
    RetrievedChunk(
        chunk_id="CA-FULL-COVERAGE_section-3-physical-damage-coverage",
        form_id="CA-FULL-COVERAGE",
        section_title="Section 3. Physical Damage Coverage",
        content="...",
        score=1.5,
    )
]


def test_validate_citations_passes_when_all_citations_were_retrieved():
    _validate_citations(
        ["CA-FULL-COVERAGE_section-3-physical-damage-coverage"], _RETRIEVED
    )  # does not raise


def test_validate_citations_raises_on_a_fabricated_chunk_id():
    with pytest.raises(ValueError, match="section-99-does-not-exist"):
        _validate_citations(["CA-FULL-COVERAGE_section-99-does-not-exist"], _RETRIEVED)
```

- [x] **Step 6: Run the test to verify it fails**

Run: `uv run pytest tests/test_coverage_agent_citation_validation.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.coverage_agent'`

- [x] **Step 7: Write the Coverage Agent**

```python
# src/claims_assistant/agents/coverage_agent.py
from __future__ import annotations

import sys

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.config import Settings
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.search.retrieval import RetrievedChunk, retrieve_policy_chunks

INSTRUCTIONS = """\
You are an insurance coverage-determination specialist. For each request you are given:
1. The policyholder's policy metadata (coverage tier, state, effective/expiration dates).
2. A set of retrieved clauses from that exact policy document, each labeled with a chunk_id.
3. The claim narrative describing what happened.

Determine whether the described loss is covered under the policy's own text.

Rules:
- Base your determination ONLY on the retrieved policy clauses provided. Do not use outside \
knowledge of insurance law or assume coverage that isn't stated in the clauses.
- "citations" must be chunk_ids copied verbatim from the retrieved clauses given to you. Never \
invent a chunk_id or cite a clause that was not provided.
- If the retrieved clauses clearly show the loss is covered, respond "approve" and cite the \
specific clause(s) that establish coverage.
- If the retrieved clauses clearly show the loss is excluded with no conditions attached, or the \
policy tier doesn't include this type of coverage at all, respond "deny" and cite the specific \
clause(s) establishing that.
- If coverage depends on a fact the clauses reference but the claim narrative doesn't confirm or \
deny (for example, whether an optional endorsement was added), respond "needs_info" rather than \
guessing, and cite the clause(s) that raise the open question. The narrative not mentioning a \
conditional fact is NOT the same as the narrative establishing that fact is absent — treat it as \
unknown, not as a negative answer. Only respond "deny" for a conditional clause like this if the \
narrative affirmatively confirms the excluded condition (e.g. the policyholder states they do NOT \
have the endorsement); silence means "needs_info", not "deny".
- rationale should be a short, adjuster-readable explanation that reflects what the cited clauses \
actually say.
"""

_POLICY_DB_SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.policy_db"],
)


def build_coverage_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_coverage_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_coverage_agent(settings: Settings) -> Agent:
    client = build_coverage_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


async def lookup_policy_by_number(policy_number: str) -> PolicyLookupResult:
    # Raises rather than returning a structured "lookup failed" output (spec §8 describes
    # the latter) — there's no API layer yet to translate this into a response; Phase 7
    # (FastAPI orchestrator endpoints) is where this becomes a caught, surfaced error
    # instead of a propagating exception.
    async with stdio_client(_POLICY_DB_SERVER_PARAMS) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(
                "get_policy_by_number", {"policy_number": policy_number}
            )
    if result.is_error:
        raise ValueError(f"policy lookup failed for policy_number={policy_number!r}")
    assert result.structured_content is not None
    return PolicyLookupResult.model_validate(result.structured_content)


def _validate_citations(citations: list[str], retrieved: list[RetrievedChunk]) -> None:
    valid_ids = {chunk.chunk_id for chunk in retrieved}
    invalid = [c for c in citations if c not in valid_ids]
    if invalid:
        raise ValueError(f"coverage determination cited unknown chunk id(s): {invalid}")


def _build_prompt(
    policy: PolicyLookupResult, chunks: list[RetrievedChunk], claim_narrative: str
) -> str:
    clauses = "\n\n".join(
        f"[chunk_id: {c.chunk_id}] {c.section_title}\n{c.content}" for c in chunks
    )
    return (
        f"Policy metadata:\n"
        f"- Policy number: {policy.policy_number}\n"
        f"- Coverage tier: {policy.coverage_tier}\n"
        f"- State: {policy.state}\n"
        f"- Effective: {policy.effective_date} to {policy.expiration_date}\n\n"
        f"Retrieved policy clauses:\n{clauses}\n\n"
        f"Claim narrative:\n{claim_narrative}\n\n"
        f"Determine coverage."
    )


async def determine_coverage(
    agent: Agent, settings: Settings, policy_number: str, claim_narrative: str
) -> CoverageDetermination:
    policy = await lookup_policy_by_number(policy_number)
    chunks = await retrieve_policy_chunks(
        settings, form_id=policy.policy_form_id, query_text=claim_narrative
    )
    prompt = _build_prompt(policy, chunks, claim_narrative)
    response = await agent.run(
        prompt, options=ChatOptions(response_format=CoverageDetermination)
    )
    determination = response.value
    assert isinstance(determination, CoverageDetermination)
    _validate_citations(determination.citations, chunks)
    return determination
```

- [x] **Step 8: Run the citation-validation test to verify it passes**

Run: `uv run pytest tests/test_coverage_agent_citation_validation.py -v`
Expected: PASS (2 passed) — no network involved yet, since `_validate_citations` is a pure function.

- [x] **Step 9: Write the failing end-to-end integration tests**

These three cases use real seeded policies (Phase 1's `seed_data.py`): `POL-CA-0002` (full coverage) for an approve case and a needs-info case, `POL-CA-0001` (liability only) for a deny case. Needs `docker-compose up -d postgres` (seeded), a populated Search index (Task 5), and real Azure OpenAI credentials for both the coverage chat deployment and the embedding deployment.

```python
# tests/test_coverage_agent.py
import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent, determine_coverage
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_collision_claim_on_full_coverage_policy_is_approved(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0002",
        claim_narrative=(
            "I rear-ended another car while driving to work in my Tesla Model 3; my front "
            "bumper is damaged."
        ),
    )

    assert result.determination == "approve"
    assert len(result.citations) > 0
    assert all(c.startswith("CA-FULL-COVERAGE_") for c in result.citations)


@pytest.mark.asyncio
async def test_collision_claim_on_liability_only_policy_is_denied(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0001",
        claim_narrative=(
            "I rear-ended another car while driving to work in my Ford Focus; my front "
            "bumper is damaged."
        ),
    )

    assert result.determination == "deny"
    assert "CA-LIABILITY-ONLY_section-3-physical-damage-coverage" in result.citations


@pytest.mark.asyncio
async def test_delivery_use_collision_with_unstated_endorsement_needs_info(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0002",
        claim_narrative=(
            "I had just dropped off a food delivery order for a local restaurant's delivery "
            "app when another driver rear-ended me at a stoplight, denting my rear bumper. "
            "This was the first time I've ever done a delivery run in this car."
        ),
    )

    # First-cut assertion, same spirit as Phase 3's eval-fixture floor: this exercises a
    # genuinely conditional clause (Section 4.1's delivery-use exclusion "unless a
    # commercial-use endorsement has been added") where the narrative never confirms or
    # denies the endorsement. If the model instead returns "deny", that's real prompt-tuning
    # signal for Phase 8's eval harness, not necessarily a bug in this test.
    assert result.determination == "needs_info"
    assert len(result.citations) > 0
```

- [x] **Step 10: Run the tests**

Run: `uv run pytest tests/test_coverage_agent.py -v`
Expected: PASS (3 passed). If the third test fails because the model returned `"deny"` instead of `"needs_info"`, read `INSTRUCTIONS`' needs-info rule and consider strengthening the language (e.g. explicitly naming "an endorsement the narrative doesn't mention" as a needs-info trigger) — this is real prompt signal, the same way Phase 3's Task 4 treated a below-floor score as signal about the extraction prompt, not the fixture.

- [x] **Step 11: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 12: Commit**

```powershell
git add src/claims_assistant/agents/coverage_schema.py src/claims_assistant/agents/coverage_agent.py tests/test_coverage_schema.py tests/test_coverage_agent_citation_validation.py tests/test_coverage_agent.py
git commit -m "feat: add Coverage Agent with grounded citation validation"
```

---

## Definition of Done for Phase 4

- [x] `uv run pytest -v -m "not integration"` passes with no external services needed (config, chunking, index schema, coverage schema, citation-validation unit tests).
- [x] With real `AZURE_OPENAI_*`/`AZURE_SEARCH_*` values in `.env` and `docker-compose up -d postgres` running (seeded), `uv run pytest -v -m integration` passes — including all of this phase's Search/embedding/indexer/retrieval/Coverage Agent integration tests, plus all prior phases' integration tests.
- [x] The Azure AI Search index contains all 63 chunks from the 9-document policy corpus (Task 5).
- [x] Given a claim + policy, `determine_coverage()` returns approve/deny/needs-info with a citation that traces to a real indexed chunk — and a fabricated citation is rejected by `_validate_citations` (roadmap Phase 4 success criteria; Task 7).
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [x] Roadmap doc's Phase 4 checkbox is checked off.
- [x] Everything above is committed.

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 5 (Fraud-Risk Agent) plan next — it's independent of this phase's Coverage Agent (per the roadmap's dependency notes: both are independent once Phase 1 + relevant Phase 2 MCP servers exist) but will reuse this phase's `agent_framework` wiring patterns and the `claims-history-mcp`/`vin-vehicle-mcp` servers from Phase 2 the same way this phase reused `policy-db-mcp`.

**Notes from execution:** Four real issues surfaced during the guided walkthrough, none caught by the pre-execution plan review (which verified SDK/API claims and static logic but didn't exercise a live Search index end-to-end):

1. **`eastus2` had no capacity for the `free` Search SKU** at provisioning time (`InsufficientResourcesAvailable`). Reprovisioned in `centralus` instead — the Search service doesn't need to be co-located with `claims-assistant-openai`, cross-region only affects latency for a demo workload. Worth trying a different region first (not necessarily retrying the same one) if this recurs in a future phase needing a new resource.
2. **Azure AI Search document keys reject `::`** — only letters, digits, `_`, `-`, `=` are allowed (`InvalidDocumentKey`, surfaced only once real documents were uploaded in Task 5's `upload_documents()` call, since Task 2's unit tests never touched the live index). The `chunk_id` scheme was changed from `{form_id}::{slug}` to `{form_id}_{slug}` (e.g. `CA-FULL-COVERAGE_section-3-physical-damage-coverage`) and propagated through `chunking.py` and every test/prompt example that hardcoded the old separator (Tasks 2, 6, 7). **Lesson for future phases: a schema/format claim isn't fully verified by unit tests and mypy/ruff alone if it's meant to satisfy an external system's constraints (here, Azure Search's document-key charset) — those need at least one real round-trip through the live system before the format is treated as settled**, the same category of gap as Phase 2/3's SDK-surface lesson but one level up (data format vs. API surface).
3. A **line-length lint failure** in `tests/test_chunking.py`'s last test (103 > 100 chars) — a small snippet bug that slipped through both authoring and review, fixed by wrapping the call.
4. **`needs_info` vs `deny` prompt-tuning**, exactly as flagged as a risk while writing the plan: the model initially treated the claim narrative's silence about a commercial-use endorsement as evidence the endorsement was absent, and returned `"deny"` instead of `"needs_info"` for Task 7's third test case. Fixed by strengthening `INSTRUCTIONS`' needs-info rule to explicitly state that narrative silence about a conditional fact means "unknown," not "confirmed absent," and that `"deny"` on a conditional clause requires the narrative to *affirmatively* confirm the excluded condition. All 3 coverage-determination test cases passed after this change. Full suite after Task 7: 42 passed (`-m "not integration"`), 30 passed (`-m integration`, needs `docker-compose up -d postgres` + real `AZURE_OPENAI_*`/`AZURE_SEARCH_*` in `.env`) — no regressions across Phases 0–4.
