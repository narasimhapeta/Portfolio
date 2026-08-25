# Phase 12: Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path/command in chat, the human creates/edits the file or runs the command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files, or to run any `az`/`docker`/`gh` command that provisions, modifies, or deletes a real resource, directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Structured logging, distributed tracing, and metrics across the FastAPI API, the 3 MCP servers, and the Agent Framework orchestration graph (spec §3.1), shipped to Azure Application Insights, with a real Bicep-defined dashboard — not just log lines.

**Architecture:** Application Insights, workspace-based and linked to the `claims-assistant-logs` Log Analytics workspace already provisioned in `app-infra-base.bicep` (Phase 10) — added to that same template, same teardown/redeploy lifecycle (cheap at rest, no state worth preserving, unlike Postgres/OpenAI's `platform.bicep` tier). Instrumentation goes in via the `azure-monitor-opentelemetry` distro's `configure_azure_monitor()`, called once at each service's startup — it auto-instruments FastAPI, outbound `httpx`, and SQLAlchemy/`asyncpg`, and bridges stdlib `logging` into Application Insights' `traces` table.

**A concrete finding from reading the installed `agent-framework-core==1.14.0` package directly (not assumed):** `agent_framework.observability` already ships built-in, on-by-default OpenTelemetry instrumentation — `get_tracer()`/`get_meter()`, and native GenAI semantic-convention spans per agent invocation (model, tokens, duration, tool calls) plus workflow-graph spans (`workflow.run`, `executor.process`, `edge_group.process`, `message.send`). OpenTelemetry's global-provider model means whichever exporter setup runs first in a process "wins" for that whole process — so `configure_azure_monitor()`, called before any agent/workflow code runs, should make the orchestration graph's tracing flow into Application Insights automatically, with **no manual span-wrapping needed around the 4 agents or the workflow graph itself**. Task 4 verifies this for real rather than assuming it holds exactly as read.

**Custom telemetry beyond auto-instrumentation** (things auto-instrumentation can't infer, since they're business-outcome data, not HTTP/DB call shape): claim outcome counts (completed/needs_clarification/failed), extraction per-field confidence, and fraud-risk score/tier — recorded via `agent_framework.observability.get_meter()` (the same meter agent_framework's own native metrics already use, for one consistent metric namespace rather than a second parallel one) at the natural choke points: `api/claims.py`'s `submit_claim` (outcome), `workflow/executors.py`'s `ExtractionExecutor`/`FraudRiskExecutor` (confidence/fraud score).

**Local dev stays opt-in:** telemetry export only activates when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set — unset (the local-dev default), `configure_azure_monitor()` is simply never called, and the app behaves exactly as it does today.

**Tech Stack:** New dependency `azure-monitor-opentelemetry` (pulls in `opentelemetry-sdk` and the FastAPI/httpx/SQLAlchemy auto-instrumentors as transitive deps — `agent-framework-core` currently ships only `opentelemetry-api`, confirmed by inspecting the installed `.venv`, not the full SDK). Exact pinned version is whatever `uv add azure-monitor-opentelemetry` resolves in Task 2 Step 1 — not guessed here.

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§1 — added scope, see the Phase 12 goal note; §3.1 — the orchestration graph this phase traces; §7 — deployment, extends `app-infra-base.bicep`'s existing Log Analytics workspace from Phase 10)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- **No new automated eval-style test suite for telemetry itself** — there's no ground truth to assert scores against (unlike Phase 8's extraction/coverage/fraud evals). Verification is: (a) instrumentation doesn't break the existing test suite, (b) unit tests on the custom-metric recording calls themselves (via OTel's `InMemoryMetricReader` test utility), (c) manual confirmation the live dashboard shows real data after exercising the deployed API — matching this project's existing convention for infra-shaped work (Phase 9/10).
- `APPLICATIONINSIGHTS_CONNECTION_STRING` is read directly via `os.environ` at each service's startup (API's `main.py`, each MCP server's `if __name__ == "__main__":` block) — **not** added to `config.Settings`, since instrumentation setup has to run before the app object is constructed, ahead of where `Settings`/`get_settings()` would normally be consulted.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for touched source files) before moving to the next task.
- **`transport: 'auto'` / ACA WebSocket and streaming behavior is not this phase's concern** (that's Phase 11's frontend) — this phase's Container Apps changes are limited to `app-infra-base.bicep` (Application Insights + Workbook resources), not `app-infra-apps.bicep`'s ingress config.
- No secrets committed to the repo — `APPLICATIONINSIGHTS_CONNECTION_STRING` is a Bicep `@secure()` output/parameter wired the same way Phase 10 wired `azureOpenAiApiKey`/`azureSearchApiKey`: a Container Apps `secretRef`, not a plaintext env value.

---

### Task 1: Application Insights in `app-infra-base.bicep`

**Files:**
- Modify: `AutoClaimsAssistant/iac/app-infra-base.bicep`
- Modify: `AutoClaimsAssistant/scripts/iac/deploy-app-infra-base.ps1`

**Interfaces:**
- Produces: `appInsightsConnectionString` output (Task 3/4/6 consume it via the apps-deploy script).

- [ ] **Step 1: Add the Application Insights resource**

In `iac/app-infra-base.bicep`, add this resource after the existing `logAnalytics` resource (linking to it via `workspaceResourceId`, the workspace-based Application Insights pattern):

```bicep
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'claims-assistant-insights'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}
```

Confirm the exact `Microsoft.Insights/components` API version against `az provider show --namespace Microsoft.Insights` for your subscription before deploying — `2020-02-02` is a commonly-used stable version as of planning time, not verified live against your subscription the way Phase 10's API versions were.

Add to the outputs at the end of the file:

```bicep
output appInsightsConnectionString string = appInsights.properties.ConnectionString
```

- [ ] **Step 2: Lint-check locally**

```powershell
az bicep build --file iac/app-infra-base.bicep --stdout | Out-Null
```

Expected: no errors.

- [ ] **Step 3: Validate against real ARM (non-mutating)**

```powershell
az deployment group validate --resource-group claims-assistant-rg --template-file iac/app-infra-base.bicep --query "properties.{provisioningState:provisioningState, error:error}" -o json
```

Expected: `{"error": null, "provisioningState": "Succeeded"}`. (This validates the whole `app-infra-base.bicep` file, including the resources Phase 10 already deployed — expected to be a no-op against them.)

- [ ] **Step 4: Deploy for real**

```powershell
./scripts/iac/deploy-app-infra-base.ps1
```

This re-runs the whole base-layer deploy (idempotent — Phase 10's existing resources are no-ops, only `appInsights` is newly created). Confirm the output includes `appInsightsConnectionString`.

- [ ] **Step 5: Update local `.env` and GitHub secrets**

Add `APPLICATIONINSIGHTS_CONNECTION_STRING` to your local `.env` (optional locally — leave unset if you don't want local telemetry export) and to GitHub secrets:

```powershell
az deployment group show --resource-group claims-assistant-rg --name app-infra-base --query "properties.outputs.appInsightsConnectionString.value" -o tsv | gh secret set APPLICATIONINSIGHTS_CONNECTION_STRING --repo narasimhapeta/Portfolio
```

- [ ] **Step 6: Commit**

```powershell
git add iac/app-infra-base.bicep scripts/iac/deploy-app-infra-base.ps1
git commit -m "feat: add Application Insights to app-infra-base"
```

---

### Task 2: Wire OpenTelemetry into the FastAPI API

**Files:**
- Modify: `pyproject.toml`
- Modify: `src/claims_assistant/main.py`
- Test: `tests/test_observability.py`

**Interfaces:**
- Produces: `configure_observability() -> None` (`src/claims_assistant/observability.py`, new file) — called once at API/MCP-server startup, no-ops when `APPLICATIONINSIGHTS_CONNECTION_STRING` is unset.

- [ ] **Step 1: Add the dependency**

```powershell
uv add azure-monitor-opentelemetry
```

- [ ] **Step 2: Write the failing test**

```python
# tests/test_observability.py
from __future__ import annotations

from unittest.mock import patch

from claims_assistant.observability import configure_observability


def test_configure_observability_noops_when_connection_string_unset(monkeypatch):
    monkeypatch.delenv("APPLICATIONINSIGHTS_CONNECTION_STRING", raising=False)
    with patch("claims_assistant.observability.configure_azure_monitor") as mock_configure:
        configure_observability()
    mock_configure.assert_not_called()


def test_configure_observability_calls_configure_azure_monitor_when_connection_string_set(
    monkeypatch,
):
    monkeypatch.setenv("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=fake")
    with patch("claims_assistant.observability.configure_azure_monitor") as mock_configure:
        configure_observability()
    mock_configure.assert_called_once()
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_observability.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.observability'`

- [ ] **Step 4: Write `observability.py`**

```python
# src/claims_assistant/observability.py
from __future__ import annotations

import os

from azure.monitor.opentelemetry import configure_azure_monitor


def configure_observability(service_name: str = "claims-assistant") -> None:
    """Wires OpenTelemetry -> Azure Monitor for this process, if configured.

    No-ops when APPLICATIONINSIGHTS_CONNECTION_STRING is unset (local dev default) --
    call this before constructing the FastAPI app / MCP server / running the workflow
    graph, since it sets the process-wide OTel providers agent_framework's own
    get_tracer()/get_meter() calls read from (see plan Architecture).
    """
    connection_string = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
    if not connection_string:
        return
    configure_azure_monitor(connection_string=connection_string, logger_name="claims_assistant")
```

Confirm `configure_azure_monitor`'s exact keyword arguments against whatever version `uv add` resolved (`service_name`/resource attributes may need to be set via the `OTEL_SERVICE_NAME` env var instead of a direct kwarg, depending on the installed version's API) — adjust before trusting this snippet verbatim.

- [ ] **Step 5: Run the test to verify it passes**

Run: `uv run pytest tests/test_observability.py -v`
Expected: PASS (2 passed)

- [ ] **Step 6: Call it from `main.py`**

```python
# src/claims_assistant/main.py
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
```

`configure_observability()` runs at import time, before `create_app()` — this is deliberate: `azure-monitor-opentelemetry`'s FastAPI auto-instrumentor needs to be active before the `FastAPI()` instance is constructed to correctly patch request handling.

- [ ] **Step 7: Run the full non-integration suite to confirm no regressions**

Run: `uv run pytest -v -m "not integration"`
Expected: all pass, same count as before plus this task's 2 new tests.

- [ ] **Step 8: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 9: Commit**

```powershell
git add pyproject.toml uv.lock src/claims_assistant/observability.py src/claims_assistant/main.py tests/test_observability.py
git commit -m "feat: wire OpenTelemetry/Azure Monitor into the API"
```

---

### Task 3: Wire the same instrumentation into the 3 MCP servers

**Files:**
- Modify: `src/claims_assistant/mcp_servers/policy_db.py`
- Modify: `src/claims_assistant/mcp_servers/claims_history.py`
- Modify: `src/claims_assistant/mcp_servers/vin_vehicle.py`

**Interfaces:** none new — reuses `configure_observability` from Task 2.

- [ ] **Step 1: Add the call to each server's entrypoint**

In each of the 3 files, add the import and call right before the existing `if __name__ == "__main__":` block runs `mcp.run(...)` — e.g. in `policy_db.py`:

```python
from claims_assistant.observability import configure_observability

if __name__ == "__main__":
    configure_observability(service_name="policy-db-mcp")
    mcp.run(transport="streamable-http", host="0.0.0.0", port=8101, stateless_http=True)
```

Repeat for `claims_history.py` (`service_name="claims-history-mcp"`, its own existing port) and `vin_vehicle.py` (`service_name="vin-vehicle-mcp"`, its own existing port). Distinct `service_name` per server so Application Insights' Application Map can distinguish the 3 MCP services from each other and from the API.

- [ ] **Step 2: Manual smoke test**

With `docker-compose up -d`, confirm all 3 MCP servers and the API still start cleanly (`docker-compose logs` shows no import errors from the new `configure_observability` call).

- [ ] **Step 3: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 4: Commit**

```powershell
git add src/claims_assistant/mcp_servers/policy_db.py src/claims_assistant/mcp_servers/claims_history.py src/claims_assistant/mcp_servers/vin_vehicle.py
git commit -m "feat: wire observability into the 3 MCP servers"
```

---

### Task 4: Custom metrics — claim outcomes, extraction confidence, fraud score

**Files:**
- Modify: `src/claims_assistant/api/claims.py`
- Modify: `src/claims_assistant/workflow/executors.py`
- Test: `tests/test_observability_metrics.py`

**Interfaces:**
- Produces: metric-recording calls at 3 choke points, using `agent_framework.observability.get_meter()` (the same meter agent_framework's own native GenAI/workflow metrics already publish through, for one consistent metric namespace).

- [ ] **Step 1: Write the failing metrics test**

Uses OpenTelemetry's own `InMemoryMetricReader` test utility to assert real instrument calls happened, not mocks of application code — this is the standard OTel testing pattern, not something specific to this project.

```python
# tests/test_observability_metrics.py
from __future__ import annotations

from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import InMemoryMetricReader

from claims_assistant.observability_metrics import (
    record_claim_outcome,
    record_extraction_confidence,
    record_fraud_risk_score,
)


def _read_metric_names(reader: InMemoryMetricReader) -> set[str]:
    data = reader.get_metrics_data()
    names = set()
    if data is None:
        return names
    for rm in data.resource_metrics:
        for sm in rm.scope_metrics:
            for metric in sm.metrics:
                names.add(metric.name)
    return names


def test_record_claim_outcome_emits_a_counter():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_claim_outcome("completed", meter_provider=provider)

    assert "claims_assistant.claim.outcome" in _read_metric_names(reader)


def test_record_extraction_confidence_emits_a_histogram():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_extraction_confidence("injuries", 0.3, meter_provider=provider)

    assert "claims_assistant.extraction.confidence" in _read_metric_names(reader)


def test_record_fraud_risk_score_emits_a_histogram():
    reader = InMemoryMetricReader()
    provider = MeterProvider(metric_readers=[reader])

    record_fraud_risk_score(72, "high", meter_provider=provider)

    assert "claims_assistant.fraud.risk_score" in _read_metric_names(reader)
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_observability_metrics.py -v`
Expected: FAIL — `ModuleNotFoundError`

- [ ] **Step 3: Write `observability_metrics.py`**

```python
# src/claims_assistant/observability_metrics.py
from __future__ import annotations

from opentelemetry.metrics import Meter, MeterProvider

from agent_framework.observability import get_meter


def _meter(meter_provider: MeterProvider | None) -> Meter:
    if meter_provider is not None:
        return meter_provider.get_meter("claims_assistant")
    return get_meter("claims_assistant")


def record_claim_outcome(status: str, *, meter_provider: MeterProvider | None = None) -> None:
    counter = _meter(meter_provider).create_counter(
        "claims_assistant.claim.outcome", description="Count of claim outcomes by status"
    )
    counter.add(1, {"status": status})


def record_extraction_confidence(
    field: str, confidence: float, *, meter_provider: MeterProvider | None = None
) -> None:
    histogram = _meter(meter_provider).create_histogram(
        "claims_assistant.extraction.confidence",
        description="Per-field extraction confidence scores",
    )
    histogram.record(confidence, {"field": field})


def record_fraud_risk_score(
    score: int, tier: str, *, meter_provider: MeterProvider | None = None
) -> None:
    histogram = _meter(meter_provider).create_histogram(
        "claims_assistant.fraud.risk_score", description="Fraud-risk scores by tier"
    )
    histogram.record(score, {"tier": tier})
```

The `meter_provider` parameter exists solely so Step 1's tests can inject an `InMemoryMetricReader`-backed provider instead of the process-global one — production call sites (Steps 4-5) never pass it, so they use whatever `configure_observability()` (Task 2) set as the global `MeterProvider`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_observability_metrics.py -v`
Expected: PASS (3 passed)

- [ ] **Step 5: Record claim outcome in `api/claims.py`**

In `submit_claim` (Phase 7), after determining `claim.status`, add:

```python
from claims_assistant.observability_metrics import record_claim_outcome

# ... inside submit_claim, right before each `return`:
record_claim_outcome(claim.status)
```

Add this call at all 3 return points in `submit_claim` (the `except Exception` branch's `failed` claim, and both the `completed`/`needs_clarification` branches at the end).

- [ ] **Step 6: Record extraction confidence and fraud score in `workflow/executors.py`**

In `ExtractionExecutor.run`, after `extraction = await extract_fnol_facts(...)`:

```python
from claims_assistant.observability_metrics import (
    record_extraction_confidence,
    record_fraud_risk_score,
)

# in ExtractionExecutor.run, after extraction is computed:
for field, confidence in extraction.confidence.model_dump().items():
    record_extraction_confidence(field, confidence)
```

In `FraudRiskExecutor.run`, after `assessment = await assess_fraud_risk(...)`:

```python
record_fraud_risk_score(assessment.fraud_risk_score, assessment.fraud_risk_tier)
```

Confirm `FieldConfidence.model_dump()`'s exact field names and `FraudAssessment`'s exact attribute names against `agents/extraction_schema.py`/`agents/fraud_schema.py` before writing this for real — this plan infers them from Phase 3/5's plans and Phase 7's test fixtures, not a fresh read of those schema files.

- [ ] **Step 7: Run the full non-integration and integration suites**

Run: `uv run pytest -v -m "not integration"` then `uv run pytest -v -m integration`
Expected: all pass, no regressions, plus this task's 3 new unit tests.

- [ ] **Step 8: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 9: Commit**

```powershell
git add src/claims_assistant/observability_metrics.py src/claims_assistant/api/claims.py src/claims_assistant/workflow/executors.py tests/test_observability_metrics.py
git commit -m "feat: add custom claim-outcome/extraction-confidence/fraud-score metrics"
```

---

### Task 5: Verify end-to-end tracing against the real deployment

**Files:** none — this task is verification only, per this project's "manual/API-level demo testing" convention (spec §9).

- [ ] **Step 1: Deploy the updated images**

Push the changes from Tasks 2-4 through the normal CD path (or manually via `deploy-app-infra-apps.ps1`), so the API and 3 MCP servers are running the instrumented code with `APPLICATIONINSIGHTS_CONNECTION_STRING` wired in (Task 6 handles the actual secret wiring into the container apps — do this step after Task 6 if working through the tasks in order, or come back to it).

- [ ] **Step 2: Exercise the deployed API**

Submit a real claim via Swagger or the Phase 11 frontend (`POST /claims`), including at least one case that reaches the clarification path (a deliberately ambiguous narrative, same fixtures Phase 6/7 used).

- [ ] **Step 3: Confirm real data in Application Insights**

In the Azure Portal, open the `claims-assistant-insights` resource:
- **Application Map**: confirm the API, and ideally the 3 MCP servers, appear as connected nodes.
- **Transaction Search**: find the `POST /claims` request by timestamp, open its end-to-end transaction detail, and confirm you can see the extraction/coverage/fraud-risk/adjuster-summary agent calls as nested spans underneath it (this is the concrete verification of the "native workflow tracing" claim in this plan's Architecture section — if the agent-level spans don't show up nested under the HTTP request, the parent-child span linkage needs investigating before calling this task done, not assumed to be automatic).
- **Metrics**: query for `claims_assistant.claim.outcome`, `claims_assistant.extraction.confidence`, `claims_assistant.fraud.risk_score` and confirm real recorded values from Step 2's submission.

- [ ] **Step 4: Note findings**

If the nested-span linkage doesn't hold (e.g. agent-framework's spans show up as a separate, unlinked trace rather than nested under the FastAPI request span), record what was actually observed in this plan's Lessons Learned section — this is exactly the kind of "verified against real behavior, not assumed" finding every prior phase's plan documents when reality diverges from the pre-execution design.

---

### Task 6: Bicep-defined Workbook dashboard

**Files:**
- Modify: `AutoClaimsAssistant/iac/app-infra-base.bicep`
- Modify: `AutoClaimsAssistant/scripts/iac/deploy-app-infra-apps.ps1` (wire `appInsightsConnectionString` into the `-apps` deploy)
- Modify: `AutoClaimsAssistant/iac/app-infra-apps.bicep` (accept and pass through the connection string to all 4 existing container apps' env)

**Interfaces:**
- Produces: a `Microsoft.Insights/workbooks` resource in `app-infra-base.bicep`; `APPLICATIONINSIGHTS_CONNECTION_STRING` env var on the API and 3 MCP server container apps.

- [ ] **Step 1: Add the Workbook resource**

In `iac/app-infra-base.bicep`, add after the `appInsights` resource (Task 1):

```bicep
resource dashboard 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: guid('claims-assistant-dashboard', resourceGroup().id)
  location: location
  kind: 'shared'
  properties: {
    displayName: 'Claims Assistant Dashboard'
    category: 'workbook'
    sourceId: appInsights.id
    serializedData: string({
      version: 'Notebook/1.0'
      items: [
        {
          type: 3
          content: {
            version: 'KqlItem/1.0'
            query: 'requests | summarize RequestCount=count(), FailureRate=100.0*countif(success==false)/count(), AvgDuration=avg(duration) by bin(timestamp, 5m) | order by timestamp asc'
            size: 0
            title: 'API request rate / failure rate / latency'
          }
        }
        {
          type: 3
          content: {
            version: 'KqlItem/1.0'
            query: 'customMetrics | where name == "claims_assistant.claim.outcome" | summarize Count=sum(valueSum) by tostring(customDimensions["status"]), bin(timestamp, 1h)'
            size: 0
            title: 'Claim outcome distribution over time'
          }
        }
        {
          type: 3
          content: {
            version: 'KqlItem/1.0'
            query: 'dependencies | where type == "InProc" or name contains "invoke_agent" | summarize AvgDuration=avg(duration) by name | order by AvgDuration desc'
            size: 0
            title: 'Per-agent latency breakdown'
          }
        }
      ]
    })
  }
}
```

Confirm the exact query semantics of each KQL panel against real ingested data (Task 5) before trusting these verbatim — in particular, whether agent-invocation spans land in the `dependencies` table under a queryable `name` filter like `invoke_agent` depends on exactly how `agent_framework`'s GenAI spans get mapped by the Azure Monitor exporter, which Task 5 is what actually confirms. Adjust the third panel's query based on what Task 5 Step 3 actually found in Transaction Search.

- [ ] **Step 2: Lint-check and validate**

```powershell
az bicep build --file iac/app-infra-base.bicep --stdout | Out-Null
az deployment group validate --resource-group claims-assistant-rg --template-file iac/app-infra-base.bicep --query "properties.{provisioningState:provisioningState, error:error}" -o json
```

Expected: no errors, `provisioningState: Succeeded`.

- [ ] **Step 3: Wire the connection string into `app-infra-apps.bicep`**

Add a new `@secure() param appInsightsConnectionString string` and, for each of the 4 existing container apps (`policyDbMcp`, `claimsHistoryMcp`, `vinVehicleMcp`, `api`), add a `secrets` entry and an `env` entry:

```bicep
{ name: 'app-insights-connection-string', value: appInsightsConnectionString }
```

```bicep
{ name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', secretRef: 'app-insights-connection-string' }
```

- [ ] **Step 4: Deploy for real**

```powershell
az deployment group create --resource-group claims-assistant-rg --template-file iac/app-infra-base.bicep --query "properties.outputs" -o json
```

```powershell
./scripts/iac/deploy-app-infra-apps.ps1
```

(Update the apps-deploy script to prompt for/pass `appInsightsConnectionString`, following the existing pattern for `postgresAdminPassword`/`azureOpenAiApiKey` etc.)

- [ ] **Step 5: Confirm the dashboard renders**

In the Azure Portal, open the `claims-assistant-insights` resource's Workbooks blade, confirm "Claims Assistant Dashboard" appears and its 3 panels render real data from Task 5's test submission (re-run Task 5 Step 2 first if no data shows yet).

- [ ] **Step 6: Update the roadmap**

In `docs/superpowers/plans/2026-08-10-roadmap.md`:

```markdown
- [x] Phase 12 — Observability
```

- [ ] **Step 7: Commit**

```powershell
git add AutoClaimsAssistant/iac/app-infra-base.bicep AutoClaimsAssistant/iac/app-infra-apps.bicep AutoClaimsAssistant/scripts/iac/deploy-app-infra-apps.ps1 AutoClaimsAssistant/docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "feat: add Bicep-defined Application Insights Workbook dashboard"
```

---

## Definition of Done for Phase 12

- [ ] `uv run pytest -v -m "not integration"` passes — including `test_observability.py` and `test_observability_metrics.py`, no regressions.
- [ ] `uv run pytest -v -m integration` passes, no regressions.
- [ ] `uv run ruff check .` and `uv run mypy src` both clean.
- [ ] Application Insights (`claims-assistant-insights`) is deployed, linked to the existing Log Analytics workspace, via `app-infra-base.bicep`.
- [ ] The API and all 3 MCP servers are instrumented (`configure_observability()`), with `APPLICATIONINSIGHTS_CONNECTION_STRING` wired as a Container Apps secret, not plaintext.
- [ ] After exercising the deployed API (Task 5), Application Map and Transaction Search show real, connected trace data, including the orchestration graph's per-agent spans nested under the originating HTTP request — confirmed for real, not assumed from reading the SDK source (Task 5's actual finding is recorded in Lessons Learned either way).
- [ ] The 3 custom metrics (claim outcome, extraction confidence, fraud-risk score) show real recorded values in Application Insights after Task 5's submission.
- [ ] The Bicep-defined Workbook dashboard renders and its panels show real data.
- [ ] Local dev without `APPLICATIONINSIGHTS_CONNECTION_STRING` set behaves unchanged from before this phase.
- [ ] Roadmap's Phase 12 checkbox is checked off.
- [ ] Everything above is committed.

## Lessons Learned

To be appended as real bugs/gotchas come up during execution, per this project's Phase 9/10 convention — Task 5 Step 4 in particular is expected to produce at least one entry here, since the nested-span-linkage claim in this plan's Architecture section is inferred from reading SDK source, not yet confirmed against a real Application Insights ingestion.
