# Phase 6: Supervisor Orchestration Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Wire the Extraction Agent (Phase 3), Coverage Agent (Phase 4), and Fraud-Risk Agent (Phase 5) into a Microsoft Agent Framework `Workflow` graph — a sequential backbone with a confidence-based conditional branch and a parallel fan-out — and add a new Adjuster-Summary Agent (GPT-5-mini) that merges Coverage + Fraud-Risk output into one structured recommendation. A deliberately low-confidence FNOL routes to a clarification path instead of the recommendation path (spec §3.1, roadmap Phase 6 success criteria).

**Architecture:** A new `src/claims_assistant/workflow/` package holds the graph-specific plumbing — message types that flow between nodes (`messages.py`), the deterministic confidence-routing logic (`supervisor.py`), the `Executor` subclasses that wrap each phase's existing orchestration function (`executors.py`), and the graph builder itself (`graph.py`). No agent's own module changes: `extraction_agent.extract_fnol_facts`, `coverage_agent.determine_coverage`, and `fraud_agent.assess_fraud_risk` are imported and called as-is from inside thin `Executor` wrappers — the graph layer never talks to `agent_framework.Agent` instances directly (see Global Constraints on why raw `Agent` nodes were rejected). The **Supervisor is not an LLM agent**: per spec §3.1 its job is "inspects extraction confidence/completeness," which is a threshold check over numbers the Extraction Agent already computed — implemented as a plain Python predicate (`supervisor.is_extraction_sufficient`) that IS the condition function of the graph's own conditional-branch primitive (`Case`), not a separate node. This was a deliberate deviation from spec §4's model-tiering table (which lists a "Supervisor/orchestrator: GPT-5-mini" row) — confirmed with the project owner before writing this plan — because it matches every other agent's established pattern in this project (compute ground truth deterministically in Python; only call an LLM where real reasoning is needed) and spec §3.2's own rationale for choosing this topology ("deterministic and testable for the common path"). It also means the "low-confidence routes to clarification" test case is a reliable non-flaky unit test at the routing-logic level, with the real end-to-end behavior (does the *actual* Extraction Agent produce low confidence for an ambiguous narrative) still verified separately by an integration test.

The new Adjuster-Summary Agent's LLM call is prose-only: it returns `narrative_summary` and `recommended_next_step` (free text), never re-stating `coverage_determination`/`fraud_risk_tier`/citations/red-flags — those are already-validated structured facts from Coverage/Fraud and are assembled into the terminal `ClaimRecommendation` in plain Python (`assemble_claim_recommendation()`), never re-derived by this LLM call. This is why, unlike Coverage's `_validate_citations` and Fraud's `_validate_assessment`, the Adjuster-Summary Agent needs no post-hoc grounding-validation function for fabrication: there is no closed-vocabulary claim (a citation ID, a red-flag code) it could fabricate — it only writes free-text prose around facts it never touches. A narrower, related risk remains unmechanized on purpose: nothing in Phase 6 checks that `recommended_next_step`'s prose doesn't *contradict* `coverage_determination`/`fraud_risk_tier` (e.g. reading like an approval when the determination is `"deny"`). `INSTRUCTIONS` (Task 4) explicitly tells the model to stay consistent, but unlike a citation ID or a red-flag code, "does this sentence semantically contradict that field" isn't checkable with a plain equality/membership test the way `_validate_citations`/`_validate_assessment` are — it's the same class of judgment call spec §6 already assigns to Phase 8's LLM-as-judge (which is how Coverage's hallucination check and Fraud's rationale-groundedness check are specified to work, not a hand-rolled Phase 4/5 validator either). Treated as a known residual risk carried forward to Phase 8, not a Phase 6 gap to close now.

**Tech Stack:** `agent-framework-core==1.14.0` (unchanged from Phase 3/4/5, re-confirmed below) — specifically its `_workflows` submodule (`WorkflowBuilder`, `Executor`, `WorkflowContext`, `handler`, `Case`, `Default`, `Workflow`), none of which were used before this phase and all of which are verified against the actual installed source in Global Constraints, not trained knowledge. `agent_framework.openai.OpenAIChatCompletionClient` (unchanged). Pydantic v2 for all new message/schema types (matches the rest of the codebase — no new dataclasses introduced, keeping `FraudSignals` in Phase 5 the sole dataclass exception). `typing.Never` (stdlib, Python 3.12) for terminal-node `WorkflowContext` annotations — used instead of `typing_extensions.Never` (what the framework's own source and docstrings use) because this project targets Python ≥3.12 where `typing.Never` is already stdlib; no new dependency needed either way. No new PyPI dependency this phase: `agent_framework_orchestrations` (which hosts `HandoffBuilder`/`ConcurrentBuilder`/`SequentialBuilder`) is confirmed **not installed** in this project's `.venv`, and is not needed — `WorkflowBuilder.add_switch_case_edge_group` (the conditional branch) and `add_fan_out_edges`/`add_fan_in_edges` (the parallel fan-out/merge) are core `agent-framework-core` primitives that cover everything spec §3.1's diagram needs.

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§3.1 orchestration graph, §3.2 topology rationale, §4 model tiering, §8 error handling)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All I/O-bound functions are `async def` (per Phase 0's async I/O constraint) — every `Executor.@handler` method in this phase is `async def`, matching the framework's own convention.
- No new dependency additions this phase — nothing to `uv add`. (`agent_framework_orchestrations` was considered and explicitly rejected — see Architecture and the API-verification notes below.)
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Tests that make real MCP-over-Postgres, Azure OpenAI, or Azure AI Search calls are `pytest.mark.integration` (need `docker-compose up -d postgres`, seeded, and real `AZURE_OPENAI_*`/`AZURE_SEARCH_*` values in `.env` — this phase's end-to-end graph tests need *all* of them, since the graph now spans every prior phase's agent).
- **Confirmed against the actually-installed packages in this project's `.venv` while writing this plan**: `agent-framework-core==1.14.0`, `agent-framework-openai==1.13.0`, `mcp==2.0.0`, `openai==2.54.0`, `pydantic==2.13.4` — **all unchanged since Phase 3/4/5**, so every previously-verified `Agent`/`ChatOptions`/`OpenAIChatCompletionClient`/MCP client surface still applies as-is.
- **`agent_framework`'s workflow/graph API verified directly against installed source this phase** (first phase to use it — nothing here was in Phase 3/4/5's verified surface). Read in full: `_workflows/_workflow_builder.py`, `_workflows/_executor.py`, `_workflows/_workflow_context.py`, `_workflows/_workflow.py`, `_workflows/_edge.py` (`Case`/`Default`/`SwitchCaseEdgeGroup` internals), plus spot-checked `_agents.py`/`_types.py` for `AgentResponse`. Key findings that shape this plan (each independently re-verified by direct source read, not just trusted from a research pass):
  - `WorkflowBuilder(start_executor=...)` — `start_executor` is a **required keyword-only constructor argument**; there is no public `set_start_executor()` added after the fact. The first executor of the graph must be known at `WorkflowBuilder(...)` construction time.
  - `add_edge(source, target, condition=None)`, `add_fan_out_edges(source, targets)`, `add_fan_in_edges(sources, target)`, `add_switch_case_edge_group(source, cases: Sequence[Case | Default])`, `.build() -> Workflow` are all real, all on `WorkflowBuilder`, all confirmed present with these exact signatures.
  - **`add_switch_case_edge_group`'s `Case.condition` is a synchronous `Callable[[Any], bool]`**, called un-awaited (verified directly in `_edge.py`'s `SwitchCaseEdgeGroup.selection_func`: `if case.condition(message): return [case.target_id]`, itself called without `await` in the edge runner) — this is *narrower* than `add_edge`'s own `condition` parameter, which explicitly supports `bool | Awaitable[bool]`. An `async def` condition passed to `Case` would silently never be awaited (a truthy coroutine object, not a bug that raises). This project's `Case` condition (Task 5) is a plain sync function for exactly this reason. The same `selection_func` also wraps each `case.condition(message)` call in `try/except Exception: logger.warning(...)` and falls through to the next case on any exception — since this project's only case is followed immediately by `Default`, an exception in the confidence-check condition would silently route to the "sufficient confidence" path rather than clarification or a hard failure. `is_extraction_sufficient` only reads already-Pydantic-validated fields and can't raise for a well-formed `ExtractionResult`, so this is safe as written, but it's a real framework behavior worth knowing about before anyone touches that condition function later (flagged inline on the `Case(...)` call in Task 5's `graph.py`).
  - Cases are evaluated **in list order**, first match wins, and a bare `Default` entry always matches (no condition check) — so the list must be `[Case(...), Default(...)]`, never the reverse, and a `Case`-only "low confidence" branch plus one `Default` "sufficient confidence" branch is exactly the shape `add_switch_case_edge_group` requires (confirmed: it accepts as few as one `Case` plus the required `Default`).
  - `add_switch_case_edge_group`'s `Case`/`Default` targets are **single-dispatch** — a `Case`/`Default` can point at exactly one executor, never a list. Since the "sufficient confidence" branch needs to reach **two** executors (Coverage + Fraud-Risk) in parallel, the `Default` target is a trivial pass-through gate executor (`FanOutGateExecutor`, Task 5) that immediately re-sends the same message via a separate `add_fan_out_edges` call — this is the one indirection the graph's own type system forces, not an arbitrary design choice.
  - `Executor` subclasses register message handlers via the `@handler` decorator (`from agent_framework import Executor, WorkflowContext, handler`) on an `async def method(self, message: InT, ctx: WorkflowContext[OutT]) -> None` — input type is introspected from `message`'s annotation, and what the handler is allowed to do (`ctx.send_message`, `ctx.yield_output`, both, or neither) is introspected from `WorkflowContext`'s generic parameters: `WorkflowContext[OutT]` = send only, `WorkflowContext[Never, OutT]` = yield only (terminal node), `WorkflowContext[OutT, YieldT]` = both. `ctx.send_message(message, target_id=None)` and `ctx.yield_output(output)` are both real, both confirmed by direct read of `_workflow_context.py`.
  - `WorkflowBuilder.add_fan_in_edges(sources, target)` delivers the target executor a **`list[T]`** aggregated from all sources, not one call per source — the fan-in target's handler must be annotated to accept that list type (`list[CoverageOutcome | FraudOutcome]` here), and cannot assume source order.
  - `Workflow.run(message)` (non-streaming, the default) returns an **awaitable** (`result = await workflow.run(initial_message)`); `result.get_outputs() -> list[Any]` collects every `yield_output` call's payload where `type == 'output'` (the default for every `yield_output` call unless `output_from`/`intermediate_output_from` is explicitly set on `WorkflowBuilder` — this plan doesn't set either, so every `yield_output` is an `'output'` event). Confirmed via the docstring's own worked example (`events = await workflow.run("hello"); events.get_outputs()`) and by reading `WorkflowRunResult.get_outputs()`'s implementation directly.
  - `WorkflowBuilder` **does** accept a raw `agent_framework.Agent` instance directly (auto-wrapped into an `AgentExecutor` via the `SupportsAgentRun` protocol) — but this plan deliberately does **not** use that path. An agent-wrapped node's output is always an `AgentExecutorResponse` (not a raw Pydantic value), chaining agent-wrapped nodes has a documented gotcha (a downstream custom executor that emits a plain `str` instead of `AgentExecutorResponse.with_text(...)` silently drops conversation history because it flips which handler fires), and extracting a structured value still means reaching into `agent_executor_response.agent_response.value` — strictly more surface area than calling the phase's own already-tested `extract_fnol_facts`/`determine_coverage`/`assess_fraud_risk` functions directly from inside a thin `Executor.@handler`, which is what this plan does instead (Task 5).
  - `agent_framework.orchestrations` (`HandoffBuilder`, `ConcurrentBuilder`, `SequentialBuilder`, etc.) is a lazy-import shim over a **separate, not-installed** package (`agent_framework_orchestrations`; `import agent_framework_orchestrations` raises `ModuleNotFoundError` in this venv). `HandoffBuilder` in particular is a multi-agent/human conversational-handoff primitive, not a deterministic confidence-threshold branch — confirmed as the wrong tool for spec §3.1's "conditional handoff edge" by reading its re-exported name list (`HandoffAgentUserRequest`, `HandoffSentEvent`, etc., all pointing at an interactive conversation-transfer pattern). `add_switch_case_edge_group`, already part of installed `agent-framework-core`, is the correct and sufficient mechanism.
- **Live Azure OpenAI model catalog re-checked while writing this plan** (`az cognitiveservices account list-models --name claims-assistant-openai --resource-group claims-assistant-rg`): unchanged at the mini tier since Phase 3 — `gpt-5.4-mini` (version `2026-03-17`, `GenerallyAvailable`, unambiguous plain naming) is still the newest mini-tier model, and is in fact the exact model already deployed for `extraction-agent` (confirmed via `az cognitiveservices account deployment show --deployment-name extraction-agent`). The full-tier catalog has moved again since Phase 5 (newest GA full-tier entries are still `gpt-5.5`/2026-04-24 and the still-unlabeled `gpt-5.6-luna`/`-sol`/`-terra` trio from Phase 5's check) but this phase doesn't need a new full-tier deployment. Task 1 deploys `gpt-5.4-mini` (`2026-03-17`) for the Adjuster-Summary Agent — same model, same rationale, as the existing `extraction-agent` deployment.
- Existing deployments on `claims-assistant-openai` confirmed via `az cognitiveservices account deployment list`: `extraction-agent`, `coverage-agent`, `policy-embeddings`, `fraud-risk-agent`. Task 1 adds a fifth, `adjuster-summary-agent`, reusing the same resource (no new Azure resource — all `az` commands run by the user, not the assistant, per the working agreement).
- **No Supervisor Azure deployment this phase** — see Architecture for the rationale (confirmed with the project owner: deterministic Python, not an LLM call).
- **MCP/lookup failures still propagate as raised exceptions, same as Phase 4/5** (spec §8's "surfaces this explicitly ... rather than the LLM guessing" requirement) — `CoverageExecutor`/`FraudRiskExecutor` call `determine_coverage`/`assess_fraud_risk` as-is, and neither catches the `ValueError` those functions raise on a failed policy/VIN/claims-history lookup. Per `coverage_agent.py`'s own existing comment, turning that into a caught, API-surfaced error is explicitly Phase 7's job (the FastAPI orchestrator layer), not this phase's — an uncaught exception during `workflow.run()` failing the whole run is the correct interim behavior, not a gap in this plan.

---

### Task 1: Azure deployment + config wiring for the Adjuster-Summary Agent

**Files:**
- Modify: `src/claims_assistant/config.py`
- Modify: `.env.example`
- Modify: `tests/test_config.py`

**Interfaces:**
- Consumes: nothing new (first task of the phase).
- Produces: `Settings.azure_openai_adjuster_summary_deployment: str` — consumed by Task 4's `build_adjuster_summary_chat_client()`.

- [ ] **Step 1: Provision the Adjuster-Summary Agent's chat deployment**

Reuses the existing `claims-assistant-openai` resource. `gpt-5.4-mini` (version `2026-03-17`) is the current unambiguous mini-tier model per the live catalog check above — the same model already deployed for `extraction-agent`.

```powershell
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name adjuster-summary-agent --model-name gpt-5.4-mini --model-version "2026-03-17" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
```

If this fails with a capacity/quota error, retry with a lower `--sku-capacity` (e.g. `5`) — this is a demo workload, not production traffic.

- [ ] **Step 2: Extend the config test**

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
    monkeypatch.setenv("AZURE_OPENAI_FRAUD_DEPLOYMENT", "test-fraud-deployment")
    monkeypatch.setenv(
        "AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT", "test-adjuster-summary-deployment"
    )
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
    assert settings.azure_openai_fraud_deployment == "test-fraud-deployment"
    assert settings.azure_openai_adjuster_summary_deployment == "test-adjuster-summary-deployment"
    assert settings.azure_search_endpoint == "https://example.search.windows.net"
    assert settings.azure_search_api_key == "test-search-key"
    assert settings.azure_search_index_name == "test-policy-documents"


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'azure_openai_adjuster_summary_deployment'`

- [ ] **Step 4: Add the new settings field**

In `src/claims_assistant/config.py`, add this field to the `Settings` class (after the existing `azure_openai_fraud_deployment` field):

```python
    azure_openai_adjuster_summary_deployment: str = ""
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [ ] **Step 6: Document the new env var**

Add to `.env.example` (and your own `.env`, with your real deployment name from Step 1):

```env
AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT=adjuster-summary-agent
```

- [ ] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 8: Commit**

```powershell
git add src/claims_assistant/config.py .env.example tests/test_config.py
git commit -m "feat: add Adjuster-Summary Agent chat deployment config"
```

---

### Task 2: Deterministic supervisor confidence-routing logic

**Files:**
- Create: `src/claims_assistant/workflow/__init__.py`
- Create: `src/claims_assistant/workflow/supervisor.py`
- Test: `tests/test_supervisor.py`

**Interfaces:**
- Consumes: `FieldConfidence`, `FNOLExtraction` (`agents/extraction_schema.py`); `FNOLFacts` (`fnol_schema.py`) — all already defined, used here only as plain Pydantic models, no LLM/network call in this task.
- Produces: `CONFIDENCE_THRESHOLD: float`, `identify_low_confidence_fields(confidence: FieldConfidence, threshold: float = CONFIDENCE_THRESHOLD) -> list[str]`, `identify_missing_required_fields(facts: FNOLFacts) -> list[str]`, `is_extraction_sufficient(extraction: FNOLExtraction, threshold: float = CONFIDENCE_THRESHOLD) -> bool`. Task 3's `ClarificationRequest` construction and Task 5's `ClarificationExecutor`/graph wiring both import from here.

- [ ] **Step 1: Write the failing supervisor tests**

Pure, no network — same style as Phase 5's `fraud_signals.py` tests.

```python
# tests/test_supervisor.py
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.supervisor import (
    CONFIDENCE_THRESHOLD,
    identify_low_confidence_fields,
    identify_missing_required_fields,
    is_extraction_sufficient,
)

_SUFFICIENT_CONFIDENCE = FieldConfidence(
    incident_datetime=0.95,
    location=0.9,
    parties=0.85,
    vehicles=0.85,
    injuries=0.8,
    narrative_summary=0.9,
)
_COMPLETE_FACTS = FNOLFacts(
    incident_datetime="2026-07-09T17:15",
    location="Elm Street, Columbus, OH",
    parties=[Party(role="policyholder", name="Harold Bennett")],
    vehicles=[VehicleInfo(role="policyholder_vehicle", description="Chevrolet Equinox")],
    injuries=False,
    narrative_summary="Rear-ended while stopped for a pedestrian.",
)


def test_identify_low_confidence_fields_returns_empty_when_all_above_threshold():
    assert identify_low_confidence_fields(_SUFFICIENT_CONFIDENCE) == []


def test_identify_low_confidence_fields_flags_fields_below_threshold():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"injuries": 0.4, "location": 0.5})

    flagged = identify_low_confidence_fields(confidence)

    assert set(flagged) == {"injuries", "location"}


def test_identify_low_confidence_fields_boundary_is_not_flagged():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"injuries": CONFIDENCE_THRESHOLD})

    assert identify_low_confidence_fields(confidence) == []


def test_identify_missing_required_fields_returns_empty_when_complete():
    assert identify_missing_required_fields(_COMPLETE_FACTS) == []


def test_identify_missing_required_fields_flags_empty_parties_and_vehicles():
    facts = _COMPLETE_FACTS.model_copy(update={"parties": [], "vehicles": []})

    missing = identify_missing_required_fields(facts)

    assert set(missing) == {"parties", "vehicles"}


def test_is_extraction_sufficient_true_for_complete_high_confidence_extraction():
    extraction = FNOLExtraction(facts=_COMPLETE_FACTS, confidence=_SUFFICIENT_CONFIDENCE)

    assert is_extraction_sufficient(extraction) is True


def test_is_extraction_sufficient_false_for_low_confidence():
    confidence = _SUFFICIENT_CONFIDENCE.model_copy(update={"narrative_summary": 0.2})
    extraction = FNOLExtraction(facts=_COMPLETE_FACTS, confidence=confidence)

    assert is_extraction_sufficient(extraction) is False


def test_is_extraction_sufficient_false_for_missing_required_fields():
    facts = _COMPLETE_FACTS.model_copy(update={"vehicles": []})
    extraction = FNOLExtraction(facts=facts, confidence=_SUFFICIENT_CONFIDENCE)

    assert is_extraction_sufficient(extraction) is False
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_supervisor.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.workflow'`

- [ ] **Step 3: Create the `workflow` package and write the supervisor module**

```python
# src/claims_assistant/workflow/__init__.py
```

(empty file — package marker)

```python
# src/claims_assistant/workflow/supervisor.py
from __future__ import annotations

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts

# spec §3.1: "supervisor checks confidence" / "Below threshold or missing required fields
# -> handoff". This is deterministic Python, not an LLM call (see Phase 6 plan's Architecture
# section for why) — it's the condition function of the graph's switch-case branch (Task 5),
# not a separate node.
CONFIDENCE_THRESHOLD = 0.7


def identify_low_confidence_fields(
    confidence: FieldConfidence, threshold: float = CONFIDENCE_THRESHOLD
) -> list[str]:
    return [
        field_name
        for field_name in FieldConfidence.model_fields
        if getattr(confidence, field_name) < threshold
    ]


def identify_missing_required_fields(facts: FNOLFacts) -> list[str]:
    # Only list-valued fields can be "missing" in a way confidence scores don't already
    # cover — location/incident_datetime/narrative_summary are always non-empty strings
    # once FNOLFacts validates, so a genuinely thin answer there shows up as low
    # confidence instead (identify_low_confidence_fields), not as an absent field here.
    missing: list[str] = []
    if not facts.parties:
        missing.append("parties")
    if not facts.vehicles:
        missing.append("vehicles")
    return missing


def is_extraction_sufficient(
    extraction: FNOLExtraction, threshold: float = CONFIDENCE_THRESHOLD
) -> bool:
    return (
        not identify_low_confidence_fields(extraction.confidence, threshold)
        and not identify_missing_required_fields(extraction.facts)
    )
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_supervisor.py -v`
Expected: PASS (8 passed)

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/workflow/__init__.py src/claims_assistant/workflow/supervisor.py tests/test_supervisor.py
git commit -m "feat: add deterministic supervisor confidence-routing logic"
```

---

### Task 3: Workflow message types

**Files:**
- Create: `src/claims_assistant/workflow/messages.py`
- Test: `tests/test_workflow_messages.py`

**Interfaces:**
- Consumes: `FNOLExtraction` (`agents/extraction_schema.py`); `CoverageDetermination` (`agents/coverage_schema.py`); `FraudRiskAssessment` (`agents/fraud_schema.py`) — all already defined.
- Produces: `ClaimIntakeRequest` (`policy_number: str`, `vin: str`, `narrative_text: str`), `ExtractionResult` (`request: ClaimIntakeRequest`, `extraction: FNOLExtraction`), `CoverageOutcome` (`policy_number: str`, `determination: CoverageDetermination`), `FraudOutcome` (`policy_number: str`, `assessment: FraudRiskAssessment`), `ClarificationRequest` (`policy_number: str`, `reason: str`, `low_confidence_fields: list[str]`, `missing_required_fields: list[str]`, `extraction: FNOLExtraction`). Task 5's `executors.py` and `graph.py` both import all five; Task 6's end-to-end tests construct `ClaimIntakeRequest` and assert on `ClarificationRequest`/`ClaimRecommendation` (Task 4).

- [ ] **Step 1: Write the failing message-type tests**

```python
# tests/test_workflow_messages.py
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo
from claims_assistant.workflow.messages import (
    ClaimIntakeRequest,
    ClarificationRequest,
    CoverageOutcome,
    ExtractionResult,
    FraudOutcome,
)

_FACTS = FNOLFacts(
    incident_datetime="2026-07-09T17:15",
    location="Elm Street, Columbus, OH",
    parties=[Party(role="policyholder", name="Harold Bennett")],
    vehicles=[VehicleInfo(role="policyholder_vehicle", description="Chevrolet Equinox")],
    injuries=False,
    narrative_summary="Rear-ended while stopped for a pedestrian.",
)
_CONFIDENCE = FieldConfidence(
    incident_datetime=0.95, location=0.9, parties=0.85, vehicles=0.85, injuries=0.8,
    narrative_summary=0.9,
)


def test_claim_intake_request_holds_policy_vin_and_narrative():
    request = ClaimIntakeRequest(
        policy_number="POL-OH-0001", vin="1GNSKBKC5FR123456", narrative_text="..."
    )

    assert request.policy_number == "POL-OH-0001"
    assert request.vin == "1GNSKBKC5FR123456"


def test_extraction_result_wraps_request_and_extraction():
    request = ClaimIntakeRequest(policy_number="POL-OH-0001", vin="VIN1", narrative_text="...")
    extraction = FNOLExtraction(facts=_FACTS, confidence=_CONFIDENCE)

    result = ExtractionResult(request=request, extraction=extraction)

    assert result.request.policy_number == "POL-OH-0001"
    assert result.extraction.facts.location == "Elm Street, Columbus, OH"


def test_coverage_outcome_wraps_determination():
    determination = CoverageDetermination(determination="approve", rationale="...", citations=["c1"])

    outcome = CoverageOutcome(policy_number="POL-OH-0001", determination=determination)

    assert outcome.determination.determination == "approve"


def test_fraud_outcome_wraps_assessment():
    assessment = FraudRiskAssessment(
        risk_score=10, risk_tier="low", red_flags=[], rationale="clean"
    )

    outcome = FraudOutcome(policy_number="POL-OH-0001", assessment=assessment)

    assert outcome.assessment.risk_tier == "low"


def test_clarification_request_carries_reason_and_extraction():
    extraction = FNOLExtraction(facts=_FACTS, confidence=_CONFIDENCE)

    request = ClarificationRequest(
        policy_number="POL-OH-0001",
        reason="low-confidence fields: injuries",
        low_confidence_fields=["injuries"],
        missing_required_fields=[],
        extraction=extraction,
    )

    assert request.low_confidence_fields == ["injuries"]
    assert request.extraction.facts.location == "Elm Street, Columbus, OH"
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_workflow_messages.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.workflow.messages'`

- [ ] **Step 3: Write the message types**

```python
# src/claims_assistant/workflow/messages.py
from __future__ import annotations

from pydantic import BaseModel

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.extraction_schema import FNOLExtraction
from claims_assistant.agents.fraud_schema import FraudRiskAssessment


class ClaimIntakeRequest(BaseModel):
    policy_number: str
    vin: str
    narrative_text: str


class ExtractionResult(BaseModel):
    request: ClaimIntakeRequest
    extraction: FNOLExtraction


class CoverageOutcome(BaseModel):
    policy_number: str
    determination: CoverageDetermination


class FraudOutcome(BaseModel):
    policy_number: str
    assessment: FraudRiskAssessment


class ClarificationRequest(BaseModel):
    policy_number: str
    reason: str
    low_confidence_fields: list[str]
    missing_required_fields: list[str]
    extraction: FNOLExtraction
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_workflow_messages.py -v`
Expected: PASS (5 passed)

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/workflow/messages.py tests/test_workflow_messages.py
git commit -m "feat: add workflow graph message types"
```

---

### Task 4: Adjuster-Summary Agent

**Files:**
- Create: `src/claims_assistant/agents/adjuster_summary_schema.py`
- Create: `src/claims_assistant/agents/adjuster_summary_agent.py`
- Test: `tests/test_adjuster_summary_schema.py`
- Test: `tests/test_adjuster_summary_agent.py`

**Interfaces:**
- Consumes: `Agent`, `ChatOptions` (`agent_framework`); `OpenAIChatCompletionClient` (`agent_framework.openai`); `Settings` (`config.py`); `CoverageDetermination` (`agents/coverage_schema.py`); `FraudRiskAssessment`, `RedFlagCode` (`agents/fraud_schema.py`, `agents/fraud_signals.py`).
- Produces: `AdjusterSummary` (LLM output: `narrative_summary: str`, `recommended_next_step: str`), `ClaimRecommendation` (terminal assembled object: `policy_number`, `coverage_determination`, `coverage_rationale`, `coverage_citations`, `fraud_risk_score`, `fraud_risk_tier`, `fraud_red_flags`, `fraud_rationale`, `narrative_summary`, `recommended_next_step`), `build_adjuster_summary_chat_client(settings) -> OpenAIChatCompletionClient`, `build_adjuster_summary_agent(settings) -> Agent`, `async def summarize_for_adjuster(agent, policy_number, coverage, fraud) -> AdjusterSummary`, `assemble_claim_recommendation(policy_number, coverage, fraud, summary) -> ClaimRecommendation`. Task 5's `AdjusterSummaryExecutor` imports `build_adjuster_summary_agent`, `summarize_for_adjuster`, and `assemble_claim_recommendation`.

- [ ] **Step 1: Write the failing schema tests**

```python
# tests/test_adjuster_summary_schema.py
from claims_assistant.agents.adjuster_summary_schema import AdjusterSummary, assemble_claim_recommendation
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment


def test_adjuster_summary_holds_narrative_and_next_step():
    summary = AdjusterSummary(
        narrative_summary="Rear-end collision, no injuries, coverage clear.",
        recommended_next_step="Approve and close.",
    )

    assert summary.recommended_next_step == "Approve and close."


def test_assemble_claim_recommendation_passes_through_coverage_and_fraud_facts():
    coverage = CoverageDetermination(
        determination="approve", rationale="clause X covers this", citations=["c1", "c2"]
    )
    fraud = FraudRiskAssessment(
        risk_score=15, risk_tier="low", red_flags=[], rationale="no red flags present"
    )
    summary = AdjusterSummary(
        narrative_summary="Clean claim, low risk, covered.",
        recommended_next_step="Approve and close.",
    )

    recommendation = assemble_claim_recommendation("POL-OH-0001", coverage, fraud, summary)

    assert recommendation.policy_number == "POL-OH-0001"
    assert recommendation.coverage_determination == "approve"
    assert recommendation.coverage_rationale == "clause X covers this"
    assert recommendation.coverage_citations == ["c1", "c2"]
    assert recommendation.fraud_risk_score == 15
    assert recommendation.fraud_risk_tier == "low"
    assert recommendation.fraud_red_flags == []
    assert recommendation.fraud_rationale == "no red flags present"
    assert recommendation.narrative_summary == "Clean claim, low risk, covered."
    assert recommendation.recommended_next_step == "Approve and close."
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_adjuster_summary_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.adjuster_summary_schema'`

- [ ] **Step 3: Write the schema module**

```python
# src/claims_assistant/agents/adjuster_summary_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel

from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import RedFlagCode


class AdjusterSummary(BaseModel):
    """LLM output: prose only. It never restates determination/tier/citations/red-flags —
    those are already-validated facts from Coverage/Fraud, assembled in Python by
    assemble_claim_recommendation(), not re-derived here."""

    narrative_summary: str
    recommended_next_step: str


class ClaimRecommendation(BaseModel):
    policy_number: str
    coverage_determination: Literal["approve", "deny", "needs_info"]
    coverage_rationale: str
    coverage_citations: list[str]
    fraud_risk_score: int
    fraud_risk_tier: Literal["low", "medium", "high"]
    fraud_red_flags: list[RedFlagCode]
    fraud_rationale: str
    narrative_summary: str
    recommended_next_step: str


def assemble_claim_recommendation(
    policy_number: str,
    coverage: CoverageDetermination,
    fraud: FraudRiskAssessment,
    summary: AdjusterSummary,
) -> ClaimRecommendation:
    return ClaimRecommendation(
        policy_number=policy_number,
        coverage_determination=coverage.determination,
        coverage_rationale=coverage.rationale,
        coverage_citations=coverage.citations,
        fraud_risk_score=fraud.risk_score,
        fraud_risk_tier=fraud.risk_tier,
        fraud_red_flags=fraud.red_flags,
        fraud_rationale=fraud.rationale,
        narrative_summary=summary.narrative_summary,
        recommended_next_step=summary.recommended_next_step,
    )
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_adjuster_summary_schema.py -v`
Expected: PASS (2 passed)

- [ ] **Step 5: Write the failing agent integration test**

Needs real Azure OpenAI credentials for `AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT`. No MCP/Postgres/Search needed — this agent only reasons over already-computed `CoverageDetermination`/`FraudRiskAssessment` objects built by hand.

```python
# tests/test_adjuster_summary_agent.py
import pytest

from claims_assistant.agents.adjuster_summary_agent import (
    build_adjuster_summary_agent,
    summarize_for_adjuster,
)
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_summarize_for_adjuster_produces_nonempty_narrative_and_next_step():
    settings = get_settings()
    agent = build_adjuster_summary_agent(settings)
    coverage = CoverageDetermination(
        determination="approve",
        rationale="Comprehensive coverage clause explicitly covers hail damage.",
        citations=["POL-CA-0003-chunk-04"],
    )
    fraud = FraudRiskAssessment(
        risk_score=8,
        risk_tier="low",
        red_flags=[],
        rationale="No prior claims, policy in force well over a year, no red flags present.",
    )

    summary = await summarize_for_adjuster(agent, "POL-CA-0003", coverage, fraud)

    assert summary.narrative_summary
    assert summary.recommended_next_step
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `uv run pytest tests/test_adjuster_summary_agent.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.adjuster_summary_agent'`

- [ ] **Step 7: Write the Adjuster-Summary Agent**

```python
# src/claims_assistant/agents/adjuster_summary_agent.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.agents.adjuster_summary_schema import AdjusterSummary
from claims_assistant.agents.coverage_schema import CoverageDetermination
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.config import Settings

INSTRUCTIONS = """\
You are writing a short briefing for a human insurance adjuster who is about to make a \
final decision on a claim. You are given:
1. The policy number.
2. A coverage determination (approve/deny/needs_info) with its rationale and citations, \
already decided by a coverage-specialist process — do not second-guess or restate it.
3. A fraud-risk assessment (score/tier/red flags) with its rationale, already computed by \
a fraud-specialist process — do not second-guess or restate it.

Write:
- "narrative_summary": a short paragraph (2-4 sentences) synthesizing the coverage and \
fraud findings into a single readable briefing, written for someone who has not seen the \
underlying data. Reference the concrete reasons given (e.g. which clause, which red flags \
or their absence), but do not invent new facts.
- "recommended_next_step": one short, concrete, actionable sentence for what the adjuster \
should do next (e.g. "Approve and close, citing the comprehensive damage clause." or \
"Request additional documentation regarding the vehicle's ownership history before \
approving." or "Escalate to fraud investigation before any payout decision."). This is \
advisory only — you are not making the final claims decision, the human adjuster is.

Both fields must stay consistent with the coverage determination and fraud tier you were \
given — never recommend an action that contradicts them (for example, do not recommend \
approval if the coverage determination is "deny", and do not describe a "high" fraud tier \
as low-risk). If the determination is "needs_info", your recommended next step should be \
about resolving that open question, not approving or denying outright.
"""


def build_adjuster_summary_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_adjuster_summary_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_adjuster_summary_agent(settings: Settings) -> Agent:
    client = build_adjuster_summary_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


def _build_prompt(
    policy_number: str, coverage: CoverageDetermination, fraud: FraudRiskAssessment
) -> str:
    return (
        f"Policy number: {policy_number}\n\n"
        f"Coverage determination: {coverage.determination}\n"
        f"Coverage rationale: {coverage.rationale}\n"
        f"Coverage citations: {', '.join(coverage.citations) or 'none'}\n\n"
        f"Fraud risk score: {fraud.risk_score}\n"
        f"Fraud risk tier: {fraud.risk_tier}\n"
        f"Fraud red flags: {', '.join(fraud.red_flags) or 'none'}\n"
        f"Fraud rationale: {fraud.rationale}\n\n"
        f"Write the adjuster briefing."
    )


async def summarize_for_adjuster(
    agent: Agent,
    policy_number: str,
    coverage: CoverageDetermination,
    fraud: FraudRiskAssessment,
) -> AdjusterSummary:
    prompt = _build_prompt(policy_number, coverage, fraud)
    response = await agent.run(prompt, options=ChatOptions(response_format=AdjusterSummary))
    summary = response.value
    assert isinstance(summary, AdjusterSummary)
    return summary
```

- [ ] **Step 8: Run the test**

Run: `uv run pytest tests/test_adjuster_summary_agent.py -v`
Expected: PASS (1 passed). If `narrative_summary`/`recommended_next_step` come back present but low-quality (e.g. the model restates raw field names instead of writing prose), that's a prompt-tuning signal for `INSTRUCTIONS`, not a test bug — strengthen the "written for someone who has not seen the underlying data" framing and re-run, same category of iteration Phase 4/5 both needed.

- [ ] **Step 9: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 10: Commit**

```powershell
git add src/claims_assistant/agents/adjuster_summary_schema.py src/claims_assistant/agents/adjuster_summary_agent.py tests/test_adjuster_summary_schema.py tests/test_adjuster_summary_agent.py
git commit -m "feat: add Adjuster-Summary Agent"
```

---

### Task 5: Workflow graph — executors and builder

**Files:**
- Create: `src/claims_assistant/workflow/executors.py`
- Create: `src/claims_assistant/workflow/graph.py`
- Test: `tests/test_workflow_executors.py` (pure unit test for the `_incident_date` helper)
- Test: `tests/test_workflow_graph.py` (structural test only in this task — end-to-end tests are Task 6)

**Interfaces:**
- Consumes: `Agent` (`agent_framework`); `extract_fnol_facts`, `build_extraction_agent` (`agents/extraction_agent.py`); `determine_coverage`, `build_coverage_agent` (`agents/coverage_agent.py`); `assess_fraud_risk`, `build_fraud_agent` (`agents/fraud_agent.py`); `summarize_for_adjuster`, `assemble_claim_recommendation`, `build_adjuster_summary_agent` (Task 4); `ClaimRecommendation` (Task 4); `is_extraction_sufficient`, `identify_low_confidence_fields`, `identify_missing_required_fields` (Task 2); `ClaimIntakeRequest`, `ExtractionResult`, `CoverageOutcome`, `FraudOutcome`, `ClarificationRequest` (Task 3); `Settings` (`config.py`).
- Produces: `ExtractionExecutor`, `ClarificationExecutor`, `FanOutGateExecutor`, `CoverageExecutor`, `FraudRiskExecutor`, `AdjusterSummaryExecutor` (all `Executor` subclasses); `_incident_date(incident_datetime: str) -> str` (module-private helper, not consumed outside this file); `build_claim_intake_workflow(settings: Settings) -> Workflow`. Task 6's end-to-end tests import `build_claim_intake_workflow` and `ClaimIntakeRequest` (Task 3) and run the built `Workflow`.

- [ ] **Step 1: Write the failing structural test**

This builds the graph with fake-but-nonempty `Settings` (no real Azure/network call happens at build time — `OpenAIChatCompletionClient` construction and `WorkflowBuilder.build()`'s validation are both local) and only checks the graph assembles without a wiring error (duplicate IDs, type mismatch between adjacent nodes, missing start executor, etc.) — the real behavioral correctness is Task 6's job.

```python
# tests/test_workflow_graph.py
from agent_framework import Workflow

from claims_assistant.config import Settings
from claims_assistant.workflow.graph import build_claim_intake_workflow

_TEST_SETTINGS = Settings(
    azure_openai_endpoint="https://example.openai.azure.com",
    azure_openai_api_key="test-key",
    azure_openai_chat_deployment="test-extraction-deployment",
    azure_openai_coverage_deployment="test-coverage-deployment",
    azure_openai_fraud_deployment="test-fraud-deployment",
    azure_openai_adjuster_summary_deployment="test-adjuster-summary-deployment",
)


def test_build_claim_intake_workflow_builds_without_error():
    workflow = build_claim_intake_workflow(_TEST_SETTINGS)

    assert isinstance(workflow, Workflow)
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_workflow_graph.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.workflow.graph'`

- [ ] **Step 3: Write the failing incident-date helper test**

`FNOLFacts.incident_datetime` (`fnol_schema.py`) is a bare `str` with no format constraint — `"YYYY-MM-DDTHH:MM"` is only a convention from the few-shot examples, not schema-enforced. Phase 5's own tests always hand-built `incident_date` as a clean ISO string; Phase 6 is the first place it comes from a real LLM extraction, which could in principle produce something that doesn't start with a parseable date (especially for a deliberately date-vague narrative like Task 6's clarification-routing test case, if the model ever assigns that field surprisingly high confidence anyway). Rather than let that surface as a confusing `ValueError` deep inside `fraud_signals.compute_fraud_signals`'s own `date.fromisoformat` call, a small helper in `executors.py` raises a clear, immediately-diagnosable error right where extraction output meets fraud-agent input.

```python
# tests/test_workflow_executors.py
import pytest

from claims_assistant.workflow.executors import _incident_date


def test_incident_date_extracts_date_portion_from_full_datetime():
    assert _incident_date("2026-03-12T07:45") == "2026-03-12"


def test_incident_date_accepts_bare_date():
    assert _incident_date("2026-03-12") == "2026-03-12"


def test_incident_date_raises_clear_error_for_unparseable_input():
    with pytest.raises(ValueError, match="non-ISO-date"):
        _incident_date("sometime last week, not sure exactly when")
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `uv run pytest tests/test_workflow_executors.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.workflow.executors'`

- [ ] **Step 5: Write the executors**

```python
# src/claims_assistant/workflow/executors.py
from __future__ import annotations

import datetime
from typing import Never

from agent_framework import Agent, Executor, WorkflowContext, handler

from claims_assistant.agents.adjuster_summary_agent import summarize_for_adjuster
from claims_assistant.agents.adjuster_summary_schema import (
    ClaimRecommendation,
    assemble_claim_recommendation,
)
from claims_assistant.agents.coverage_agent import determine_coverage
from claims_assistant.agents.extraction_agent import extract_fnol_facts
from claims_assistant.agents.fraud_agent import assess_fraud_risk
from claims_assistant.config import Settings
from claims_assistant.workflow.messages import (
    ClaimIntakeRequest,
    ClarificationRequest,
    CoverageOutcome,
    ExtractionResult,
    FraudOutcome,
)
from claims_assistant.workflow.supervisor import (
    identify_low_confidence_fields,
    identify_missing_required_fields,
)


class ExtractionExecutor(Executor):
    def __init__(self, agent: Agent, *, id: str = "extraction") -> None:
        super().__init__(id=id)
        self._agent = agent

    @handler
    async def run(
        self, message: ClaimIntakeRequest, ctx: WorkflowContext[ExtractionResult]
    ) -> None:
        extraction = await extract_fnol_facts(self._agent, message.narrative_text)
        await ctx.send_message(ExtractionResult(request=message, extraction=extraction))


class ClarificationExecutor(Executor):
    def __init__(self, *, id: str = "clarification") -> None:
        super().__init__(id=id)

    @handler
    async def run(
        self, message: ExtractionResult, ctx: WorkflowContext[Never, ClarificationRequest]
    ) -> None:
        low_confidence = identify_low_confidence_fields(message.extraction.confidence)
        missing = identify_missing_required_fields(message.extraction.facts)
        reasons = []
        if low_confidence:
            reasons.append(f"low-confidence fields: {', '.join(low_confidence)}")
        if missing:
            reasons.append(f"missing required fields: {', '.join(missing)}")
        await ctx.yield_output(
            ClarificationRequest(
                policy_number=message.request.policy_number,
                reason="; ".join(reasons),
                low_confidence_fields=low_confidence,
                missing_required_fields=missing,
                extraction=message.extraction,
            )
        )


class FanOutGateExecutor(Executor):
    """Trivial pass-through. Exists only because add_switch_case_edge_group's Case/Default
    targets are single-dispatch — the "sufficient confidence" branch needs to reach two
    executors (Coverage + Fraud-Risk), so it lands here first and this re-sends the same
    message via a separate add_fan_out_edges call (see graph.py)."""

    def __init__(self, *, id: str = "fan_out_gate") -> None:
        super().__init__(id=id)

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[ExtractionResult]) -> None:
        await ctx.send_message(message)


class CoverageExecutor(Executor):
    def __init__(self, agent: Agent, settings: Settings, *, id: str = "coverage") -> None:
        super().__init__(id=id)
        self._agent = agent
        self._settings = settings

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[CoverageOutcome]) -> None:
        determination = await determine_coverage(
            self._agent,
            self._settings,
            message.request.policy_number,
            message.request.narrative_text,
        )
        await ctx.send_message(
            CoverageOutcome(policy_number=message.request.policy_number, determination=determination)
        )


def _incident_date(incident_datetime: str) -> str:
    """Extract the YYYY-MM-DD date portion fraud_signals.compute_fraud_signals expects
    from FNOLFacts.incident_datetime. Raises a clear, immediately-diagnosable error if the
    extraction produced something that doesn't start with a parseable ISO date, instead of
    letting date.fromisoformat fail confusingly deep inside compute_fraud_signals."""
    candidate = incident_datetime[:10]
    try:
        datetime.date.fromisoformat(candidate)
    except ValueError as exc:
        raise ValueError(
            f"extraction produced a non-ISO-date incident_datetime: {incident_datetime!r}"
        ) from exc
    return candidate


class FraudRiskExecutor(Executor):
    def __init__(self, agent: Agent, *, id: str = "fraud_risk") -> None:
        super().__init__(id=id)
        self._agent = agent

    @handler
    async def run(self, message: ExtractionResult, ctx: WorkflowContext[FraudOutcome]) -> None:
        incident_date = _incident_date(message.extraction.facts.incident_datetime)
        assessment = await assess_fraud_risk(
            self._agent,
            message.request.policy_number,
            message.request.vin,
            incident_date,
            message.request.narrative_text,
        )
        await ctx.send_message(
            FraudOutcome(policy_number=message.request.policy_number, assessment=assessment)
        )


class AdjusterSummaryExecutor(Executor):
    def __init__(self, agent: Agent, *, id: str = "adjuster_summary") -> None:
        super().__init__(id=id)
        self._agent = agent

    @handler
    async def run(
        self,
        message: list[CoverageOutcome | FraudOutcome],
        ctx: WorkflowContext[Never, ClaimRecommendation],
    ) -> None:
        coverage_outcome = next(m for m in message if isinstance(m, CoverageOutcome))
        fraud_outcome = next(m for m in message if isinstance(m, FraudOutcome))
        summary = await summarize_for_adjuster(
            self._agent,
            coverage_outcome.policy_number,
            coverage_outcome.determination,
            fraud_outcome.assessment,
        )
        await ctx.yield_output(
            assemble_claim_recommendation(
                coverage_outcome.policy_number,
                coverage_outcome.determination,
                fraud_outcome.assessment,
                summary,
            )
        )
```

- [ ] **Step 6: Run the incident-date helper tests to verify they pass**

Run: `uv run pytest tests/test_workflow_executors.py -v`
Expected: PASS (3 passed)

- [ ] **Step 7: Write the graph builder**

`Case.condition` receives the raw message the source executor sent — for the `extraction` node that's `ExtractionResult` (not the `FNOLExtraction` nested inside it), so the condition reaches through `result.extraction` before calling `is_extraction_sufficient`, which takes an `FNOLExtraction`.

```python
# src/claims_assistant/workflow/graph.py
from __future__ import annotations

from agent_framework import Case, Default, Workflow, WorkflowBuilder

from claims_assistant.agents.adjuster_summary_agent import build_adjuster_summary_agent
from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import Settings
from claims_assistant.workflow.executors import (
    AdjusterSummaryExecutor,
    ClarificationExecutor,
    CoverageExecutor,
    ExtractionExecutor,
    FanOutGateExecutor,
    FraudRiskExecutor,
)
from claims_assistant.workflow.supervisor import is_extraction_sufficient


def build_claim_intake_workflow(settings: Settings) -> Workflow:
    extraction = ExtractionExecutor(build_extraction_agent(settings))
    clarification = ClarificationExecutor()
    fan_out_gate = FanOutGateExecutor()
    coverage = CoverageExecutor(build_coverage_agent(settings), settings)
    fraud_risk = FraudRiskExecutor(build_fraud_agent(settings))
    adjuster_summary = AdjusterSummaryExecutor(build_adjuster_summary_agent(settings))

    return (
        WorkflowBuilder(start_executor=extraction)
        .add_switch_case_edge_group(
            extraction,
            [
                # NOTE: SwitchCaseEdgeGroup catches any exception this condition raises and
                # falls through toward Default (see Global Constraints) -- an exception here
                # would silently route to the "sufficient confidence" happy path instead of
                # clarification, the opposite of spec §8's fail-explicit intent. Safe today
                # because is_extraction_sufficient only reads already-Pydantic-validated
                # FieldConfidence/FNOLFacts fields and can't raise for a well-formed
                # ExtractionResult -- keep it that way if this condition is ever touched.
                Case(
                    condition=lambda result: not is_extraction_sufficient(result.extraction),
                    target=clarification,
                ),
                Default(target=fan_out_gate),
            ],
        )
        .add_fan_out_edges(fan_out_gate, [coverage, fraud_risk])
        .add_fan_in_edges([coverage, fraud_risk], adjuster_summary)
        .build()
    )
```

- [ ] **Step 8: Run the structural test**

Run: `uv run pytest tests/test_workflow_graph.py -v`
Expected: PASS (1 passed). `WorkflowBuilder.build()`'s type-compatibility validation was traced end-to-end against the real installed source while writing this plan for exactly this graph's `list[CoverageOutcome | FraudOutcome]` fan-in case, and it resolves correctly under the bare `@handler` introspection used above — this should just pass. If it doesn't (a type-compatibility error meaning two adjacent nodes' declared input/output types don't line up), re-check each `@handler`'s `message`/`WorkflowContext[...]` annotations against the Interfaces list above field-by-field; the documented fallback is explicit typing on `AdjusterSummaryExecutor.run` — `@handler(input=list[CoverageOutcome | FraudOutcome], workflow_output=ClaimRecommendation)` in place of the bare `@handler` — both modes are real (see Global Constraints).

- [ ] **Step 9: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 10: Commit**

```powershell
git add src/claims_assistant/workflow/executors.py src/claims_assistant/workflow/graph.py tests/test_workflow_executors.py tests/test_workflow_graph.py
git commit -m "feat: add claim-intake workflow graph (executors + builder)"
```

---

### Task 6: End-to-end graph tests

**Files:**
- Modify: `tests/test_workflow_graph.py`
- Modify: `docs/superpowers/plans/2026-08-10-roadmap.md`

**Interfaces:**
- Consumes: `build_claim_intake_workflow` (Task 5); `ClaimIntakeRequest` (Task 3); `ClaimRecommendation` (Task 4); `ClarificationRequest` (Task 3); `Workflow.run()` / `WorkflowRunResult.get_outputs()` (`agent_framework`).
- Produces: nothing new — this is the roadmap's own success-criteria check, not a new interface for a later phase. Phase 7 (FastAPI orchestrator endpoints) is what actually calls `build_claim_intake_workflow` from an API route.

- [ ] **Step 1: Write the failing end-to-end tests**

Two cases, per the roadmap's success criteria — a normal claim that produces a merged `ClaimRecommendation`, and a deliberately ambiguous narrative that should route to `ClarificationRequest` instead. Uses real seeded data (`POL-CA-0003` / Priya Natarajan's Jeep, the same clean fixture Phase 5 used) plus real Azure OpenAI and Azure AI Search credentials — the full graph spans Extraction, Coverage (needs Search), and Fraud-Risk (needs Postgres via MCP).

Replace the entire contents of `tests/test_workflow_graph.py` with the following — this is the full file (Task 5's structural test plus the two new end-to-end tests), not an addition to merge by hand:

```python
# tests/test_workflow_graph.py
import pytest
from agent_framework import Workflow

from claims_assistant.agents.adjuster_summary_schema import ClaimRecommendation
from claims_assistant.config import Settings, get_settings
from claims_assistant.workflow.graph import build_claim_intake_workflow
from claims_assistant.workflow.messages import ClaimIntakeRequest, ClarificationRequest

_TEST_SETTINGS = Settings(
    azure_openai_endpoint="https://example.openai.azure.com",
    azure_openai_api_key="test-key",
    azure_openai_chat_deployment="test-extraction-deployment",
    azure_openai_coverage_deployment="test-coverage-deployment",
    azure_openai_fraud_deployment="test-fraud-deployment",
    azure_openai_adjuster_summary_deployment="test-adjuster-summary-deployment",
)


def test_build_claim_intake_workflow_builds_without_error():
    workflow = build_claim_intake_workflow(_TEST_SETTINGS)

    assert isinstance(workflow, Workflow)


@pytest.mark.integration
@pytest.mark.asyncio
async def test_workflow_produces_claim_recommendation_for_normal_claim(seeded_db):
    workflow = build_claim_intake_workflow(get_settings())
    request = ClaimIntakeRequest(
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text=(
            "On March 10, 2026, I (Priya Natarajan) discovered hail damage to my Jeep "
            "Grand Cherokee, which had been parked outside my home overnight during a "
            "storm in Fresno, CA. No one was hurt; I was not in the vehicle at the time."
        ),
    )

    result = await workflow.run(request)

    outputs = result.get_outputs()
    assert len(outputs) == 1
    assert isinstance(outputs[0], ClaimRecommendation)
    assert outputs[0].policy_number == "POL-CA-0003"
    assert outputs[0].coverage_determination in ("approve", "deny", "needs_info")
    assert outputs[0].fraud_risk_tier in ("low", "medium", "high")
    assert outputs[0].narrative_summary
    assert outputs[0].recommended_next_step


@pytest.mark.integration
@pytest.mark.asyncio
async def test_workflow_routes_low_confidence_extraction_to_clarification(seeded_db):
    workflow = build_claim_intake_workflow(get_settings())
    request = ClaimIntakeRequest(
        policy_number="POL-CA-0003",
        vin="1C4RJFBG5FC123458",
        narrative_text=(
            "Something happened to my car at some point, not totally sure when or "
            "where, might have been another vehicle involved, might not have been. "
            "Not sure if anyone got hurt."
        ),
    )

    result = await workflow.run(request)

    outputs = result.get_outputs()
    assert len(outputs) == 1
    assert isinstance(outputs[0], ClarificationRequest)
    assert outputs[0].policy_number == "POL-CA-0003"
    assert outputs[0].reason
```

- [ ] **Step 2: Run the tests**

Run: `uv run pytest tests/test_workflow_graph.py -v -m integration`
Expected: PASS (2 passed, plus the existing structural test still passing under `-m "not integration"`).

If the first test fails on `coverage_determination`/`fraud_risk_tier` being an unexpected value: check whether it's LLM sampling non-determinism against a soft assertion (the test only asserts a valid Literal value, not a specific one, so this shouldn't happen — if it's a hard crash instead, check `_validate_citations`/`_validate_assessment` inside Coverage/Fraud's own functions first, since those raise on real grounding violations, not this phase's new code).

If the second test fails because the real Extraction Agent assigned high confidence to the deliberately vague narrative (so the workflow produced a `ClaimRecommendation` instead of routing to clarification): this is real prompt-tuning signal, not a test bug — per this project's established pattern (Phase 4/5 both needed at least one iteration here), strengthen `extraction_agent.py`'s `INSTRUCTIONS_TEMPLATE` to more explicitly call out that vague/uncertain narratives (no clear date, ambiguous number of parties, hedged injury language) should produce confidence scores below roughly 0.5–0.6 on the affected fields, and/or make the test narrative even more explicitly ambiguous (e.g. remove any parseable date entirely) before concluding the routing logic itself (Task 2, already unit-tested and deterministic) is at fault.

- [ ] **Step 3: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 4: Update the roadmap**

In `docs/superpowers/plans/2026-08-10-roadmap.md`, check off Phase 6:

```markdown
- [x] Phase 6 — Supervisor orchestration graph
```

- [ ] **Step 5: Commit**

```powershell
git add tests/test_workflow_graph.py docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "test: add end-to-end claim-intake workflow graph tests"
```

---

## Definition of Done for Phase 6

- [ ] `uv run pytest -v -m "not integration"` passes with no external services needed (config, supervisor, workflow messages, adjuster-summary schema, workflow-graph structural-build tests, plus all prior phases' unit tests unchanged).
- [ ] With real `AZURE_OPENAI_*`, `AZURE_SEARCH_*` values in `.env` (including `AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT`) and `docker-compose up -d postgres` running (seeded), `uv run pytest -v -m integration` passes — including this phase's Adjuster-Summary Agent test and both end-to-end workflow-graph tests, plus all prior phases' integration tests (no regressions).
- [ ] A normal FNOL narrative run through `build_claim_intake_workflow(...).run(...)` produces a single `ClaimRecommendation` output merging Coverage + Fraud-Risk (roadmap Phase 6 success criteria, Task 6).
- [ ] A deliberately low-confidence/ambiguous FNOL narrative routes to `ClarificationRequest` instead (roadmap Phase 6 success criteria, Task 6) — demonstrating the graph's conditional-handoff mode, not just the sequential/parallel path.
- [ ] `is_extraction_sufficient`'s routing decision itself is covered by fast, deterministic unit tests independent of LLM sampling (Task 2) — the end-to-end clarification-routing test (Task 6) additionally verifies the real Extraction Agent's confidence calibration on an ambiguous input, a separate concern from the routing logic's own correctness.
- [ ] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [ ] Roadmap doc's Phase 6 checkbox is checked off.
- [ ] Everything above is committed.

Once this is done, we write the Phase 7 (FastAPI orchestrator endpoints) plan next — it depends on Phase 6 existing (per the roadmap's dependency notes) and wires `build_claim_intake_workflow()` behind `POST /claims` / `GET /claims/{id}`.
