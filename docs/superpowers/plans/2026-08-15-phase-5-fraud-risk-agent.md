# Phase 5: Fraud-Risk Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Build a Fraud-Risk Agent that reasons over real claims-history and vehicle signals — reached via `claims-history-mcp` and `vin-vehicle-mcp` (both from Phase 2), plus `policy-db-mcp`'s already-built `get_policy_by_number` (reused from Phase 4's Coverage Agent, needed here for the policy's `effective_date`) — and produces a 0–100 risk score, a low/medium/high tier, and a rationale whose red-flag claims are validated against deterministically computed ground truth before being returned.

**Architecture:** Same shape as Phase 4's Coverage Agent, adapted for a domain that has no fixed retrieval corpus to cite against. Three new modules under `src/claims_assistant/agents/`:

- **`fraud_signals.py`** — the domain logic, and the part that makes grounding enforceable. `compute_fraud_signals()` takes the three MCP lookup results (`PolicyLookupResult`, `ClaimsHistoryResult`, `VehicleLookupResult` — all already defined in Phase 2's `mcp_servers/` modules) plus the new claim's `incident_date`, and deterministically derives a `FraudSignals` dataclass: day-deltas (incident vs. policy effective date, incident vs. most recent prior claim) and a claim-amount-to-market-value ratio. `determine_actual_red_flags()` then maps those numbers through fixed thresholds into a `set[RedFlagCode]` — a small closed vocabulary (`recent_policy_inception`, `high_claim_frequency`, `prior_fraud_flag`, `clustered_recent_claims`, `prior_claim_near_vehicle_value`) that is spec §3.1's "claim timing vs. policy effective date, claim frequency, prior fraud flags" red flags turned into checkable booleans. This is pure, network-free, and fully unit-testable.
- **`fraud_schema.py`** — `FraudRiskAssessment` (`risk_score: int` 0–100, `risk_tier: Literal["low","medium","high"]`, `red_flags: list[RedFlagCode]`, `rationale: str`), the LLM's structured output contract. `red_flags` is typed against the same `RedFlagCode` literal `fraud_signals.py` defines, so Pydantic itself rejects any code outside the closed vocabulary at parse time.
- **`fraud_agent.py`** — wires it together: `build_fraud_agent()` (same `Agent` + `ChatOptions(response_format=...)` pattern Phase 3/4 verified), a shared `_call_mcp_tool()` helper plus the thin `lookup_claims_history()`/`lookup_vehicle_by_vin()` wrappers built on it (same stdio-client shape as Phase 4's `lookup_policy_by_number()`, which this module imports and reuses directly rather than writing a third copy of that shape), and `assess_fraud_risk()` — the orchestration function: run the three lookups, compute signals + actual red flags in Python, build a prompt that hands the LLM the real numbers *and* which flags are actually true, get back a `FraudRiskAssessment`, then **`_validate_assessment()`** raises if the model claimed a red flag that wasn't actually true (spec §3.1's "rationale tied to specific tool-returned facts", enforced the same way Phase 4's `_validate_citations()` enforced grounding — reject fabricated claims post-hoc rather than trusting the model) or if `risk_tier` doesn't match the `risk_score` band. Retrieval/computation happens in plain Python before the one LLM call, not as agentic tool-calling — same reason Phase 4 chose this shape: it's what makes the grounding check enforceable.

**Why `policy-db-mcp` too, not just the two servers named in the roadmap line:** spec §3.1's first red-flag signal is "claim timing **vs. policy effective date**" — `claims-history-mcp`'s `get_claims_history` returns claim dates and fraud flags but not the policy's own `effective_date` (confirmed by re-reading `mcp_servers/claims_history.py` and `mcp_servers/policy_db.py` while writing this plan). Only `policy-db-mcp` has that field. It's already built, already tested, and Phase 4's Coverage Agent already calls it the same way — this reuses that exact `lookup_policy_by_number()` function (imported from `claims_assistant.agents.coverage_agent`) rather than duplicating the stdio-client boilerplate a third time.

**Tech Stack:** No new dependencies this phase — same `agent_framework`/`agent_framework.openai` (`Agent`, `ChatOptions`, `OpenAIChatCompletionClient`) and `mcp` (`ClientSession`, `StdioServerParameters`, `stdio_client`) surface Phase 3/4 already verified, re-confirmed unchanged in this project's `.venv` while writing this plan (see Global Constraints). Pydantic v2, pytest + pytest-asyncio (`integration` marker for anything touching real Postgres via MCP or real Azure OpenAI).

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§3.1 Fraud-Risk Agent, §4 model tiering, §5.3 MCP servers)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All I/O-bound functions are `async def` (per Phase 0's async I/O constraint) — the two new MCP lookups follow the exact `stdio_client`/`ClientSession` pattern already verified in Phase 2/4.
- No new dependency additions this phase — nothing to `uv add`.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Tests that make real MCP-over-Postgres or Azure OpenAI calls are `pytest.mark.integration` (need `docker-compose up -d postgres`, seeded, and real `AZURE_OPENAI_*` values in `.env`). This phase does **not** need Azure AI Search — the Fraud-Risk Agent has no document corpus.
- **No new external-store ID/format is introduced this phase** — unlike Phase 4's chunk-ID scheme (which hit Azure Search's document-key charset constraint on first real write), `RedFlagCode` strings and `risk_tier` values are never written to Postgres, Azure Search, or any other external store; they only round-trip through the LLM's structured JSON output and this process's own Python. So there's no external-store charset/constraint to verify here — the equivalent risk this phase actually carries is whether GPT-5's structured-output plumbing enforces `list[Literal[...]]` the same way it already-verified-working `Literal[...]` (Coverage Agent's `determination` field) — flagged as a residual, lower-probability risk in Task 4, not a certainty like Phase 4's chunk-ID issue was.
- **Confirmed against the actually-installed packages in this project's `.venv` while writing this plan** (`uv run python -c "import importlib.metadata as m; ..."`), same discipline as every prior phase: `agent-framework-core==1.14.0`, `agent-framework-openai==1.13.0`, `mcp==2.0.0`, `openai==2.54.0` — **all unchanged since Phase 3/4**, so every `Agent`/`ChatOptions`/`OpenAIChatCompletionClient`/`ClientSession`/`StdioServerParameters`/`stdio_client` API surface Phase 4's plan already verified still applies as-is; no re-verification needed for those. `pydantic==2.13.4` (list-of-`Literal` field validation is standard Pydantic v2 behavior, not something this project needed to specially verify).
- **Live Azure OpenAI model catalog re-checked while writing this plan** (`az cognitiveservices account list-models --name claims-assistant-openai --resource-group claims-assistant-rg`): the catalog has moved again since Phase 4 (`gpt-5.4`, 2026-03-05, is still GA but no longer newest). Newest full-tier GA entries now are `gpt-5.5` (version `2026-04-24`) and three same-day `gpt-5.6-*` variants — `gpt-5.6-sol`, `gpt-5.6-luna`, `gpt-5.6-terra` (all version `2026-07-09`, all `GenerallyAvailable`, all `chatCompletion: true`). The catalog exposes no description/capability field distinguishing what "sol"/"luna"/"terra" mean (checked via `-o json`, `capabilities` blocks are otherwise identical to `gpt-5.5`'s) — unlike the catalog's other suffixed variants (`-chat`, `-codex`) where the suffix's purpose is at least self-explanatory, these three give no signal about which one is the plain full-tier chat model vs. some specialized variant. Rather than guess, **Task 1 deploys `gpt-5.5`** (version `2026-04-24`) — the newest GA full-tier model with unambiguous plain naming — as the current "GPT-5 (full)" match for spec §4's Fraud-Risk row. Re-check the catalog again if Task 1's deployment step fails, and re-examine the `gpt-5.6-*` variants (e.g. via Azure AI Foundry's portal, which may show human-readable descriptions the CLI doesn't) if `gpt-5.5` looks stale by execution time.
- Existing deployments on `claims-assistant-openai` confirmed via `az cognitiveservices account deployment list`: `extraction-agent`, `coverage-agent`, `policy-embeddings`. Task 1 adds a fourth, `fraud-risk-agent`, reusing the same resource (no new Azure resource needed — the working agreement has all `az` commands run by the user, not the assistant).

---

### Task 1: Azure deployment + config wiring

**Files:**
- Modify: `src/claims_assistant/config.py`
- Modify: `.env.example`
- Modify: `tests/test_config.py`

**Interfaces:**
- Consumes: nothing new (first task of the phase).
- Produces: `Settings.azure_openai_fraud_deployment: str` — consumed by Task 4's `build_fraud_chat_client()`.

- [x] **Step 1: Provision the Fraud-Risk Agent's chat deployment**

Reuses the existing `claims-assistant-openai` resource. `gpt-5.5` is the current unambiguous full-tier model per the live catalog check above (see Global Constraints for why the newer `gpt-5.6-*` variants were passed over).

```powershell
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name fraud-risk-agent --model-name gpt-5.5 --model-version "2026-04-24" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
```

If this fails with a capacity/quota error, retry with a lower `--sku-capacity` (e.g. `5`) — this is a demo workload, not production traffic.

- [x] **Step 2: Extend the config test**

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
    assert settings.azure_search_endpoint == "https://example.search.windows.net"
    assert settings.azure_search_api_key == "test-search-key"
    assert settings.azure_search_index_name == "test-policy-documents"


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [x] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'azure_openai_fraud_deployment'`

- [x] **Step 4: Add the new settings field**

In `src/claims_assistant/config.py`, add this field to the `Settings` class (after the existing `azure_openai_embedding_deployment` field):

```python
    azure_openai_fraud_deployment: str = ""
```

- [x] **Step 5: Run the test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [x] **Step 6: Document the new env var**

Add to `.env.example` (and your own `.env`, with your real deployment name from Step 1):

```env
AZURE_OPENAI_FRAUD_DEPLOYMENT=fraud-risk-agent
```

- [x] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 8: Commit**

```powershell
git add src/claims_assistant/config.py .env.example tests/test_config.py
git commit -m "feat: add Fraud-Risk Agent chat deployment config"
```

---

### Task 2: Deterministic fraud-signal computation

**Files:**
- Create: `src/claims_assistant/agents/fraud_signals.py`
- Test: `tests/test_fraud_signals.py`

**Interfaces:**
- Consumes: `PolicyLookupResult` (`mcp_servers/policy_db.py`), `ClaimsHistoryResult`/`ClaimSummary` (`mcp_servers/claims_history.py`), `VehicleLookupResult` (`mcp_servers/vin_vehicle.py`) — all already defined in Phase 2, used here only as plain Pydantic models, no MCP call in this task.
- Produces: `RedFlagCode` (`Literal[...]` type alias), `FraudSignals` dataclass, `compute_fraud_signals(policy: PolicyLookupResult, claims_history: ClaimsHistoryResult, vehicle: VehicleLookupResult, incident_date: str) -> FraudSignals`, `determine_actual_red_flags(signals: FraudSignals) -> set[RedFlagCode]`. Task 3's `fraud_schema.py` imports `RedFlagCode`; Task 4's `fraud_agent.py` imports everything.

- [x] **Step 1: Write the failing signal-computation tests**

These construct MCP result models directly (no network, no DB) — same style as Phase 4's pure chunking tests.

```python
# tests/test_fraud_signals.py
from claims_assistant.agents.fraud_signals import (
    compute_fraud_signals,
    determine_actual_red_flags,
)
from claims_assistant.mcp_servers.claims_history import ClaimSummary, ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

_POLICY = PolicyLookupResult(
    policy_number="POL-TEST-0001",
    policyholder_name="Test Person",
    state="TX",
    coverage_tier="comprehensive_collision",
    policy_form_id="TX-COMPREHENSIVE-COLLISION",
    effective_date="2025-07-15",
    expiration_date="2026-07-15",
    premium_monthly=198.40,
)
_VEHICLE = VehicleLookupResult(
    vin="TESTVIN0000000001",
    make="Ford",
    model="F-150",
    year=2017,
    market_value_usd=19750.0,
    policy_number="POL-TEST-0001",
)


def _claims_history(claims: list[ClaimSummary]) -> ClaimsHistoryResult:
    return ClaimsHistoryResult(
        policy_number="POL-TEST-0001",
        claim_count=len(claims),
        prior_fraud_flag_count=sum(1 for c in claims if c.fraud_flag),
        most_recent_claim_date=claims[0].claim_date if claims else None,
        claims=claims,
    )


def test_compute_fraud_signals_computes_day_deltas_and_ratio():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ]

    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2025-08-01"
    )

    assert signals.days_since_policy_effective == 17
    assert signals.days_since_most_recent_prior_claim == 12
    assert signals.highest_prior_claim_to_market_value_ratio == 1.0


def test_compute_fraud_signals_handles_no_prior_claims():
    signals = compute_fraud_signals(
        _POLICY, _claims_history([]), _VEHICLE, incident_date="2026-03-10"
    )

    assert signals.claim_count == 0
    assert signals.most_recent_prior_claim_date is None
    assert signals.days_since_most_recent_prior_claim is None
    assert signals.highest_prior_claim_amount_usd is None
    assert signals.highest_prior_claim_to_market_value_ratio is None


def test_determine_actual_red_flags_flags_recent_inception_and_prior_fraud():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2025-08-01"
    )

    flags = determine_actual_red_flags(signals)

    assert flags == {
        "recent_policy_inception",
        "prior_fraud_flag",
        "clustered_recent_claims",
        "prior_claim_near_vehicle_value",
    }


def test_determine_actual_red_flags_empty_for_clean_case():
    claims = [
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-11-01",
            claim_type="comprehensive",
            amount_usd=2100.0,
            status="approved",
            fraud_flag=False,
        )
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2026-03-10"
    )

    flags = determine_actual_red_flags(signals)

    assert flags == set()


def test_determine_actual_red_flags_flags_high_frequency():
    claims = [
        ClaimSummary(
            claim_id=f"CLM-{i}",
            claim_date="2025-09-01",
            claim_type="collision",
            amount_usd=1000.0,
            status="approved",
            fraud_flag=False,
        )
        for i in range(2)
    ]
    signals = compute_fraud_signals(
        _POLICY, _claims_history(claims), _VEHICLE, incident_date="2026-06-01"
    )

    flags = determine_actual_red_flags(signals)

    assert "high_claim_frequency" in flags
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_fraud_signals.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.fraud_signals'`

- [x] **Step 3: Write the signal computation module**

```python
# src/claims_assistant/agents/fraud_signals.py
from __future__ import annotations

import datetime
from dataclasses import dataclass
from typing import Literal

from claims_assistant.mcp_servers.claims_history import ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

RedFlagCode = Literal[
    "recent_policy_inception",
    "high_claim_frequency",
    "prior_fraud_flag",
    "clustered_recent_claims",
    "prior_claim_near_vehicle_value",
]

RECENT_POLICY_INCEPTION_DAYS = 30
CLUSTERED_CLAIMS_DAYS = 45
HIGH_FREQUENCY_CLAIM_COUNT = 2
NEAR_MARKET_VALUE_RATIO = 0.9


@dataclass(frozen=True)
class FraudSignals:
    policy_number: str
    incident_date: datetime.date
    policy_effective_date: datetime.date
    days_since_policy_effective: int
    claim_count: int
    prior_fraud_flag_count: int
    most_recent_prior_claim_date: datetime.date | None
    days_since_most_recent_prior_claim: int | None
    vehicle_make: str
    vehicle_model: str
    vehicle_year: int
    vehicle_market_value_usd: float
    highest_prior_claim_amount_usd: float | None
    highest_prior_claim_to_market_value_ratio: float | None


def compute_fraud_signals(
    policy: PolicyLookupResult,
    claims_history: ClaimsHistoryResult,
    vehicle: VehicleLookupResult,
    incident_date: str,
) -> FraudSignals:
    incident = datetime.date.fromisoformat(incident_date)
    effective = datetime.date.fromisoformat(policy.effective_date)
    most_recent = (
        datetime.date.fromisoformat(claims_history.most_recent_claim_date)
        if claims_history.most_recent_claim_date
        else None
    )
    # claims_history.claims aren't tied to a specific vehicle (ClaimHistory has no VIN),
    # so this assumes one vehicle per policy — true for all current seed data, but a future
    # multi-vehicle policy would need this signal recomputed per-vehicle.
    highest_amount = (
        max(c.amount_usd for c in claims_history.claims) if claims_history.claims else None
    )
    return FraudSignals(
        policy_number=policy.policy_number,
        incident_date=incident,
        policy_effective_date=effective,
        days_since_policy_effective=(incident - effective).days,
        claim_count=claims_history.claim_count,
        prior_fraud_flag_count=claims_history.prior_fraud_flag_count,
        most_recent_prior_claim_date=most_recent,
        days_since_most_recent_prior_claim=(
            (incident - most_recent).days if most_recent else None
        ),
        vehicle_make=vehicle.make,
        vehicle_model=vehicle.model,
        vehicle_year=vehicle.year,
        vehicle_market_value_usd=vehicle.market_value_usd,
        highest_prior_claim_amount_usd=highest_amount,
        highest_prior_claim_to_market_value_ratio=(
            highest_amount / vehicle.market_value_usd if highest_amount is not None else None
        ),
    )


def determine_actual_red_flags(signals: FraudSignals) -> set[RedFlagCode]:
    flags: set[RedFlagCode] = set()
    if 0 <= signals.days_since_policy_effective < RECENT_POLICY_INCEPTION_DAYS:
        flags.add("recent_policy_inception")
    if signals.claim_count >= HIGH_FREQUENCY_CLAIM_COUNT:
        flags.add("high_claim_frequency")
    if signals.prior_fraud_flag_count > 0:
        flags.add("prior_fraud_flag")
    if (
        signals.days_since_most_recent_prior_claim is not None
        and 0 <= signals.days_since_most_recent_prior_claim < CLUSTERED_CLAIMS_DAYS
    ):
        flags.add("clustered_recent_claims")
    if (
        signals.highest_prior_claim_to_market_value_ratio is not None
        and signals.highest_prior_claim_to_market_value_ratio >= NEAR_MARKET_VALUE_RATIO
    ):
        flags.add("prior_claim_near_vehicle_value")
    return flags
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_fraud_signals.py -v`
Expected: PASS (4 passed)

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/agents/fraud_signals.py tests/test_fraud_signals.py
git commit -m "feat: add deterministic fraud-signal computation"
```

---

### Task 3: Fraud-risk output schema

**Files:**
- Create: `src/claims_assistant/agents/fraud_schema.py`
- Test: `tests/test_fraud_schema.py`

**Interfaces:**
- Consumes: `RedFlagCode` (Task 2's `fraud_signals.py`).
- Produces: `FraudRiskAssessment` (`risk_score: int`, `risk_tier: Literal["low","medium","high"]`, `red_flags: list[RedFlagCode]`, `rationale: str`). Task 4's `fraud_agent.py` imports it.

- [x] **Step 1: Write the failing schema test**

```python
# tests/test_fraud_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.fraud_schema import FraudRiskAssessment


def test_fraud_risk_assessment_validates():
    assessment = FraudRiskAssessment(
        risk_score=80,
        risk_tier="high",
        red_flags=["prior_fraud_flag", "recent_policy_inception"],
        rationale=(
            "Prior fraud-flagged claim and a new claim filed 17 days after the "
            "policy started."
        ),
    )

    assert assessment.risk_score == 80
    assert assessment.red_flags == ["prior_fraud_flag", "recent_policy_inception"]


def test_fraud_risk_assessment_rejects_score_out_of_range():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=150, risk_tier="high", red_flags=[], rationale="invalid score"
        )


def test_fraud_risk_assessment_rejects_invalid_tier():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=50, risk_tier="severe", red_flags=[], rationale="invalid tier"
        )


def test_fraud_risk_assessment_rejects_invalid_red_flag_code():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=50,
            risk_tier="medium",
            red_flags=["not_a_real_flag"],
            rationale="invalid flag",
        )
```

- [x] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_fraud_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.fraud_schema'`

- [x] **Step 3: Write the schema**

```python
# src/claims_assistant/agents/fraud_schema.py
from __future__ import annotations

from typing import Literal

from pydantic import BaseModel, Field

from claims_assistant.agents.fraud_signals import RedFlagCode


class FraudRiskAssessment(BaseModel):
    risk_score: int = Field(ge=0, le=100)
    risk_tier: Literal["low", "medium", "high"]
    red_flags: list[RedFlagCode]
    rationale: str
```

- [x] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_fraud_schema.py -v`
Expected: PASS (4 passed)

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/agents/fraud_schema.py tests/test_fraud_schema.py
git commit -m "feat: add Fraud Risk Assessment output schema"
```

---

### Task 4: Fraud-Risk Agent

**Files:**
- Create: `src/claims_assistant/agents/fraud_agent.py`
- Test: `tests/test_fraud_agent_validation.py`
- Test: `tests/test_fraud_agent.py`

**Interfaces:**
- Consumes: `Agent`, `ChatOptions` (`agent_framework`); `OpenAIChatCompletionClient` (`agent_framework.openai`); `Settings` (`config.py`); `lookup_policy_by_number()` (Phase 4's `coverage_agent.py`, reused as-is); `FraudSignals`, `RedFlagCode`, `compute_fraud_signals()`, `determine_actual_red_flags()` (Task 2); `FraudRiskAssessment` (Task 3); `ClaimsHistoryResult` (`mcp_servers/claims_history.py`), `VehicleLookupResult` (`mcp_servers/vin_vehicle.py`), `PolicyLookupResult` (`mcp_servers/policy_db.py`); `ClientSession`, `StdioServerParameters`, `stdio_client` (`mcp`).
- Produces: `build_fraud_agent(settings: Settings) -> Agent`, `lookup_claims_history(policy_number: str) -> ClaimsHistoryResult`, `lookup_vehicle_by_vin(vin: str) -> VehicleLookupResult`, `async def assess_fraud_risk(agent: Agent, policy_number: str, vin: str, incident_date: str, claim_narrative: str) -> FraudRiskAssessment`. Not consumed further in this plan — Phase 6 (Supervisor orchestration graph) wires this into the fan-out alongside the Coverage Agent.

- [x] **Step 1: Write the failing validation unit tests**

These test `_expected_tier` and `_validate_assessment` in isolation — pure, no network — before the full agent flow in Step 3, same shape as Phase 4's Task 7 Step 5.

```python
# tests/test_fraud_agent_validation.py
import pytest

from claims_assistant.agents.fraud_agent import _expected_tier, _validate_assessment
from claims_assistant.agents.fraud_schema import FraudRiskAssessment
from claims_assistant.agents.fraud_signals import compute_fraud_signals
from claims_assistant.mcp_servers.claims_history import ClaimSummary, ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

_POLICY = PolicyLookupResult(
    policy_number="POL-TEST-0001",
    policyholder_name="Test Person",
    state="TX",
    coverage_tier="comprehensive_collision",
    policy_form_id="TX-COMPREHENSIVE-COLLISION",
    effective_date="2025-07-15",
    expiration_date="2026-07-15",
    premium_monthly=198.40,
)
_VEHICLE = VehicleLookupResult(
    vin="TESTVIN0000000001",
    make="Ford",
    model="F-150",
    year=2017,
    market_value_usd=19750.0,
    policy_number="POL-TEST-0001",
)
_CLAIMS_HISTORY = ClaimsHistoryResult(
    policy_number="POL-TEST-0001",
    claim_count=1,
    prior_fraud_flag_count=1,
    most_recent_claim_date="2025-07-20",
    claims=[
        ClaimSummary(
            claim_id="CLM-1",
            claim_date="2025-07-20",
            claim_type="theft",
            amount_usd=19750.0,
            status="pending",
            fraud_flag=True,
        )
    ],
)
_SIGNALS = compute_fraud_signals(_POLICY, _CLAIMS_HISTORY, _VEHICLE, incident_date="2025-08-01")


def test_expected_tier_boundaries():
    assert _expected_tier(0) == "low"
    assert _expected_tier(33) == "low"
    assert _expected_tier(34) == "medium"
    assert _expected_tier(66) == "medium"
    assert _expected_tier(67) == "high"
    assert _expected_tier(100) == "high"


def test_validate_assessment_passes_for_grounded_flags_and_consistent_tier():
    assessment = FraudRiskAssessment(
        risk_score=85,
        risk_tier="high",
        red_flags=["prior_fraud_flag", "recent_policy_inception"],
        rationale="grounded",
    )

    _validate_assessment(assessment, _SIGNALS)  # does not raise


def test_validate_assessment_raises_on_a_fabricated_red_flag():
    assessment = FraudRiskAssessment(
        risk_score=85,
        risk_tier="high",
        red_flags=["high_claim_frequency"],
        rationale="fabricated",
    )

    with pytest.raises(ValueError, match="high_claim_frequency"):
        _validate_assessment(assessment, _SIGNALS)


def test_validate_assessment_raises_on_tier_score_mismatch():
    assessment = FraudRiskAssessment(
        risk_score=90, risk_tier="low", red_flags=[], rationale="mismatched tier"
    )

    with pytest.raises(ValueError, match="risk_tier"):
        _validate_assessment(assessment, _SIGNALS)
```

- [x] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_fraud_agent_validation.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.fraud_agent'`

- [x] **Step 3: Write the Fraud-Risk Agent**

```python
# src/claims_assistant/agents/fraud_agent.py
from __future__ import annotations

import sys
from typing import Literal

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

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

INSTRUCTIONS = """\
You are an insurance fraud-risk analyst. For each request you are given:
1. The policyholder's policy metadata (coverage tier, state, effective date).
2. A structured summary of the policyholder's prior claims history.
3. The vehicle's decoded make/model/year/market value.
4. Deterministically computed red-flag signals — booleans already calculated from the \
above data and given to you as ground truth. Do not recompute or contradict them.
5. The new claim's incident date and narrative.

Assess this new claim's fraud risk.

Rules:
- "red_flags" must be chosen ONLY from the signals explicitly marked TRUE in the \
computed signals block. Never include a red flag code marked false — that would be \
fabricating a red flag not supported by the actual data.
- You may also weigh the narrative itself for internal inconsistencies or implausible \
details (for example, injuries described inconsistently, or a narrative that doesn't \
match the claimed loss type) — describe these in "rationale" ONLY, since they are not \
one of the tool-grounded red flag codes above.
- "risk_score" is 0-100. Use the number and severity of TRUE red flags, plus any \
narrative concerns, to set the score holistically — you are not computing a fixed \
formula, but more/stronger signals should push the score higher.
- "risk_tier" must be "low" for risk_score 0-33, "medium" for 34-66, "high" for 67-100.
- "rationale" should be a short, adjuster-readable explanation naming the specific \
computed numbers (e.g. days since policy effective, claim counts, dollar amounts) that \
justify the score, so an adjuster can verify each claim against the data you were given.
"""

_CLAIMS_HISTORY_SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.claims_history"],
)
_VIN_VEHICLE_SERVER_PARAMS = StdioServerParameters(
    command=sys.executable,
    args=["-m", "claims_assistant.mcp_servers.vin_vehicle"],
)

_ALL_RED_FLAG_CODES: tuple[RedFlagCode, ...] = (
    "recent_policy_inception",
    "high_claim_frequency",
    "prior_fraud_flag",
    "clustered_recent_claims",
    "prior_claim_near_vehicle_value",
)


def build_fraud_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_fraud_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_fraud_agent(settings: Settings) -> Agent:
    client = build_fraud_chat_client(settings)
    return Agent(client=client, instructions=INSTRUCTIONS)


async def _call_mcp_tool(
    server_params: StdioServerParameters, tool_name: str, arguments: dict[str, str]
) -> dict[str, object]:
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await session.call_tool(tool_name, arguments)
    if result.is_error:
        raise ValueError(f"{tool_name} failed for arguments={arguments!r}")
    assert result.structured_content is not None
    return result.structured_content


async def lookup_claims_history(policy_number: str) -> ClaimsHistoryResult:
    content = await _call_mcp_tool(
        _CLAIMS_HISTORY_SERVER_PARAMS, "get_claims_history", {"policy_number": policy_number}
    )
    return ClaimsHistoryResult.model_validate(content)


async def lookup_vehicle_by_vin(vin: str) -> VehicleLookupResult:
    content = await _call_mcp_tool(_VIN_VEHICLE_SERVER_PARAMS, "decode_vin", {"vin": vin})
    return VehicleLookupResult.model_validate(content)


def _expected_tier(risk_score: int) -> Literal["low", "medium", "high"]:
    if risk_score <= 33:
        return "low"
    if risk_score <= 66:
        return "medium"
    return "high"


def _validate_assessment(assessment: FraudRiskAssessment, signals: FraudSignals) -> None:
    actual_flags = determine_actual_red_flags(signals)
    fabricated = [f for f in assessment.red_flags if f not in actual_flags]
    if fabricated:
        raise ValueError(f"fraud assessment cited unsupported red flag(s): {fabricated}")
    expected_tier = _expected_tier(assessment.risk_score)
    if assessment.risk_tier != expected_tier:
        raise ValueError(
            f"risk_tier {assessment.risk_tier!r} inconsistent with risk_score "
            f"{assessment.risk_score} (expected {expected_tier!r})"
        )


def _build_prompt(
    policy: PolicyLookupResult,
    signals: FraudSignals,
    actual_red_flags: set[RedFlagCode],
    claim_narrative: str,
) -> str:
    flags_block = "\n".join(
        f"- {code}: {'TRUE' if code in actual_red_flags else 'false'}"
        for code in _ALL_RED_FLAG_CODES
    )
    return (
        f"Policy metadata:\n"
        f"- Policy number: {policy.policy_number}\n"
        f"- State: {policy.state}\n"
        f"- Coverage tier: {policy.coverage_tier}\n"
        f"- Policy effective date: {signals.policy_effective_date}\n\n"
        f"Claims history:\n"
        f"- Total prior claims: {signals.claim_count}\n"
        f"- Prior fraud-flagged claims: {signals.prior_fraud_flag_count}\n"
        f"- Most recent prior claim date: {signals.most_recent_prior_claim_date}\n"
        f"- Highest prior claim amount: {signals.highest_prior_claim_amount_usd}\n\n"
        f"Vehicle:\n"
        f"- {signals.vehicle_year} {signals.vehicle_make} {signals.vehicle_model}\n"
        f"- Market value: ${signals.vehicle_market_value_usd}\n\n"
        f"Computed red-flag signals (ground truth — only cite flags marked TRUE):\n"
        f"{flags_block}\n\n"
        f"New claim:\n"
        f"- Incident date: {signals.incident_date}\n"
        f"- Days since policy effective: {signals.days_since_policy_effective}\n"
        f"- Days since most recent prior claim: "
        f"{signals.days_since_most_recent_prior_claim}\n"
        f"- Narrative: {claim_narrative}\n\n"
        f"Assess this claim's fraud risk."
    )


async def assess_fraud_risk(
    agent: Agent,
    policy_number: str,
    vin: str,
    incident_date: str,
    claim_narrative: str,
) -> FraudRiskAssessment:
    policy = await lookup_policy_by_number(policy_number)
    claims_history = await lookup_claims_history(policy_number)
    vehicle = await lookup_vehicle_by_vin(vin)
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

- [x] **Step 4: Run the validation tests to verify they pass**

Run: `uv run pytest tests/test_fraud_agent_validation.py -v`
Expected: PASS (4 passed) — no network involved yet, since `_expected_tier`/`_validate_assessment` are pure functions.

- [x] **Step 5: Write the failing end-to-end integration tests**

Two cases, per the roadmap's success criteria — one clean, one flagged — using real seeded data (Phase 1's `seed_data.py`). **Clean:** `POL-CA-0003` (Priya Natarajan, comprehensive/collision, effective 2025-05-20) has exactly one prior claim (`CLM-0004`, 2025-11-01, $2,100, approved, not fraud-flagged) against a $24,300 Jeep Grand Cherokee — a new claim months later has no true red flags. **Flagged:** `POL-TX-0006` (Derek Owusu, comprehensive/collision, effective 2025-07-15) has one prior claim (`CLM-0007`, 2025-07-20, $19,750 theft, **fraud-flagged**) against a Ford F-150 whose market value is *also* exactly $19,750 — a new claim filed shortly after both the policy's start and that prior claim trips `recent_policy_inception`, `prior_fraud_flag`, `clustered_recent_claims`, and `prior_claim_near_vehicle_value` (verified via Task 2's `determine_actual_red_flags` against this exact data). Needs `docker-compose up -d postgres` (seeded) and real Azure OpenAI credentials for `AZURE_OPENAI_FRAUD_DEPLOYMENT`.

```python
# tests/test_fraud_agent.py
import pytest

from claims_assistant.agents.fraud_agent import assess_fraud_risk, build_fraud_agent
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_clean_claim_on_low_history_policy_is_low_risk(seeded_db):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
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
    seeded_db,
):
    settings = get_settings()
    agent = build_fraud_agent(settings)

    result = await assess_fraud_risk(
        agent,
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

- [x] **Step 6: Run the tests**

Run: `uv run pytest tests/test_fraud_agent.py -v`
Expected: PASS (2 passed). If the first test fails because the model included a red flag anyway, `assess_fraud_risk` would have raised `ValueError` from `_validate_assessment` before returning — that means the model fabricated a flag not supported by the computed signals, which is real prompt signal (strengthen the "ONLY from signals marked TRUE" rule in `INSTRUCTIONS`), not a bug in the test. If the second test fails on `risk_tier`/`risk_score`, the model likely under-weighted the four true red flags — same category of prompt-tuning signal Phase 4's Task 7 hit with `needs_info` vs `deny`; strengthen the scoring guidance in `INSTRUCTIONS` (e.g. explicitly stating four or more true red flags should reliably push into the "high" band) and re-run.

- [x] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 8: Commit**

```powershell
git add src/claims_assistant/agents/fraud_agent.py tests/test_fraud_agent_validation.py tests/test_fraud_agent.py
git commit -m "feat: add Fraud-Risk Agent with grounded red-flag validation"
```

---

## Definition of Done for Phase 5

- [x] `uv run pytest -v -m "not integration"` passes with no external services needed (config, fraud signals, fraud schema, fraud-agent validation unit tests).
- [x] With real `AZURE_OPENAI_*` values in `.env` (including `AZURE_OPENAI_FRAUD_DEPLOYMENT`) and `docker-compose up -d postgres` running (seeded), `uv run pytest -v -m integration` passes — including this phase's Fraud-Risk Agent integration tests, plus all prior phases' integration tests (Search/Coverage Agent tests still need `AZURE_SEARCH_*` values too, unchanged from Phase 4).
- [x] Given a policy + claim, `assess_fraud_risk()` returns a 0–100 score, a low/medium/high tier consistent with that score, and red flags that are all traceable to real MCP-tool-returned data — a fabricated red flag is rejected by `_validate_assessment` (roadmap Phase 5 success criteria; Task 4).
- [x] Both the "one clean, one flagged" test cases from Task 4 pass against real seeded data and a real Azure OpenAI deployment.
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [x] Roadmap doc's Phase 5 checkbox is checked off.
- [x] Everything above is committed.

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 6 (Supervisor orchestration graph) plan next — it depends on Phases 3, 4, and 5 all existing (per the roadmap's dependency notes), and will wire the Extraction Agent, Coverage Agent, and this phase's Fraud-Risk Agent into the sequential-backbone + parallel-fan-out + conditional-handoff graph spec §3.1 describes.

**Notes from execution:** Two real issues surfaced during the guided walkthrough, neither caught by the pre-execution plan review:

1. **`mypy --warn-return-any` flagged `_call_mcp_tool`'s return statement** (Task 4, Step 3): `result.structured_content` is typed `Any | None` by the `mcp` SDK, so `return result.structured_content` from a function declared `-> dict[str, object]` is "returning Any" under mypy's strict setting. Fixed with an explicit `cast(dict[str, object], result.structured_content)`. Same category of gap as Phase 4's `SearchFieldDataType.Collection(...)` mypy issue — a real SDK-typing mismatch, not a logic bug, only visible once mypy actually ran against the written code.
2. **Full-suite regression check surfaced two failures in Phase 4's `test_coverage_agent.py`**, unrelated to any file this phase touched (confirmed via `git diff` — `coverage_agent.py` wasn't modified by Task 1–4). Root-caused via `superpowers:systematic-debugging`:
   - The liability-only "deny" case failed once, then passed on an immediate rerun with no code change — LLM sampling non-determinism against a hard `assert ... == "deny"`, not a bug. Confirms these Phase 4 integration tests are exactly the kind of single-hard-assertion-against-a-probabilistic-model test that Phase 8's eval framework (statistical baselines, not exact-match asserts) is meant to replace.
   - The `needs_info`-vs-`deny` case failed identically twice in a row — real, not noise. Root cause: `coverage_agent.py`'s `INSTRUCTIONS` string has carried a **duplicate, unqualified "deny" bullet since Phase 4's original commit** (`git show a6d13f1` confirms it was already there when Phase 4's tests were passing) — an unqualified "loss is excluded → deny" rule sitting immediately before the correctly-scoped "excluded **with no conditions attached** → deny" rule that Phase 4's own prompt-tuning fix added. The redundant, unqualified copy diluted the `needs_info` carve-out for genuinely conditional exclusions (this test's delivery-use/commercial-endorsement scenario). Fixed by deleting the redundant unqualified bullet, keeping only the correctly-qualified one — committed separately (`fix: remove redundant unqualified deny bullet from Coverage Agent prompt`) since it's a Phase 4 file, not part of this phase's task list. All 3 Coverage Agent tests passed after the fix.
   - **Lesson for future phases:** a prompt edit applied mid-walkthrough (as Phase 4's "Notes from execution" #4 was) can leave stale/duplicate text behind if the edit was additive rather than a clean replace — worth explicitly diffing the final `INSTRUCTIONS` string against what the plan intended, not just re-running the tests it was meant to fix, since duplicate-but-non-contradictory text can pass by luck on one run and still be a real latent defect.

Full suite after Task 4 + the Coverage Agent fix: 55 passed (`-m "not integration"`), 32 passed (`-m integration`, needs `docker-compose up -d postgres` + real `AZURE_OPENAI_*`/`AZURE_SEARCH_*` in `.env`) — no regressions across Phases 0–5.
