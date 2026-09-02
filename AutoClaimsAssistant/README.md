# AI-Powered Auto Insurance Claims Assistant

A multi-agent claims-intake pipeline for auto insurance FNOL (First Notice of Loss) reports, built on the Microsoft stack: **FastAPI**, **Microsoft Agent Framework** (graph-based workflow orchestration), **Azure AI Foundry**, **Azure AI Search**, **MCP**, **Azure Container Apps**, and **GitHub Actions**.

Given a raw FNOL narrative, the system extracts structured facts, determines policy coverage against real policy documents (RAG, with citations), scores fraud risk against real claims-history/vehicle data (via MCP tools), and produces a structured, human-reviewable recommendation. It never auto-approves or auto-denies a claim — a human adjuster always makes the final call.

Built as a portfolio/capstone project demonstrating production-grade agentic AI engineering practice: **architect → design → build → eval → test → deploy**, including a real Azure deployment with eval-gated CI/CD and a canary rollout — not a local-only simulation.

## Architecture

Sequential backbone with a parallel fan-out and one conditional handoff edge, coordinated by a supervisor:

```
FNOL text/police report
        │
        ▼
 ┌───────────────┐
 │ Extraction     │  → structured JSON facts + per-field confidence
 │ Agent          │
 └───────┬────────┘
         │
   supervisor checks confidence
         │
   ┌─────┴─────┐
   │           │
 low conf.   sufficient conf.
   │           │
   ▼           ▼ (parallel fan-out)
┌─────────┐ ┌──────────┐  ┌───────────────┐
│Clarify/ │ │ Coverage │  │ Fraud-Risk    │
│Escalate │ │ Agent    │  │ Agent         │
│(handoff)│ │ (RAG)    │  │ (MCP signals) │
└─────────┘ └────┬─────┘  └──────┬────────┘
                  └───────┬───────┘
                          ▼
                 ┌──────────────────┐
                 │ Adjuster-Summary  │
                 │ Agent             │
                 └────────┬──────────┘
                          ▼
              structured recommendation
              (coverage + fraud + rationale + citations)
                          │
                          ▼
                  human adjuster decides
```

- **Extraction Agent** — structured prompting + few-shot examples, converts unstructured FNOL text into a fixed Pydantic-validated JSON schema, with per-field confidence.
- **Supervisor** — a deterministic Python predicate (not an LLM call — it's a threshold check) that routes low-confidence/incomplete extractions to a clarification handoff instead of guessing, and otherwise fans out to Coverage and Fraud-Risk in parallel.
- **Coverage Agent** — RAG over Azure AI Search (policy document corpus) via `policy-db-mcp`, produces an approve/deny/needs-info determination with citations validated against the real retrieval set.
- **Fraud-Risk Agent** — calls `claims-history-mcp` and `vin-vehicle-mcp`, reasons over red-flag signals (claim timing, frequency, narrative inconsistencies, prior flags), produces a 0–100 risk score with a rationale tied to real tool-returned facts.
- **Adjuster-Summary Agent** — merges Coverage + Fraud-Risk into one structured recommendation packet, the pipeline's terminal output.

Full rationale for this topology and the model-tiering strategy (cheap model for high-volume structured tasks, strong model for grounded/high-stakes reasoning) is in the [design spec](docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md).

## Components

| Component | What it does |
|---|---|
| `claims_assistant.api` | FastAPI orchestrator — `POST /claims`, `GET /claims`, `GET /claims/{id}`, `POST /claims/{id}/documents`, `/health`, `/health/db` |
| `claims_assistant.workflow` | The Agent Framework graph wiring the four agents + supervisor handoff |
| `claims_assistant.agents` | The four agents and their Pydantic I/O schemas |
| `claims_assistant.mcp_servers` | Three MCP servers — `policy-db`, `claims-history`, `vin-vehicle` — each wrapping Postgres so agents call real data instead of hallucinating it |
| `claims_assistant.search` | Chunking, embedding, and indexing the policy corpus into Azure AI Search; retrieval for the Coverage Agent |
| `claims_assistant.storage` | Azure Blob Storage wiring for uploaded claim documents/photos |
| `claims_assistant.eval` | pytest-based eval harness — deterministic extraction scoring, LLM-as-judge for coverage grounding and fraud rationale, Pandas-aggregated scored reports against checked-in baselines |
| `claims_assistant.frontend` | Streamlit multi-page UI (password-gated): Submit FNOL, Claim Status, Upload Document, Claim History — a structured form-based UI, not a chat interface |

## Getting started (local)

Requires [uv](https://docs.astral.sh/uv/) and Docker.

```bash
cp .env.example .env   # fill in your Azure OpenAI / AI Search / Storage values
uv sync
docker-compose up
```

This brings up Postgres, the three MCP servers, the FastAPI API (`localhost:8000`), and the Streamlit frontend (`localhost:8501`, password from `FRONTEND_ACCESS_PASSWORD`). API docs are at `localhost:8000/docs`.

```bash
uv run pytest              # unit + integration tests (58 test files)
uv run ruff check .        # lint
uv run mypy src            # type check
python scripts/seed_db.py  # seed Postgres with synthetic policies/claims/vehicles
```

## Data & grounding

All data is synthetic — no real customer data or compliance certification is involved.

- **Policy corpus**: synthetic auto policy documents (liability-only, full coverage, comprehensive/collision, plus state-variant clauses), chunked/embedded into Azure AI Search for hybrid vector + keyword retrieval.
- **Postgres**: seeded policy, claims-history, and VIN/vehicle tables — the source of truth the three MCP servers query.
- Every coverage citation is validated against the real Azure AI Search retrieval set before being returned; every coverage/fraud claim in eval is checked for grounding (traceable to a real cited clause / real tool-returned fact), not just plausibility.

See [Section 5 of the design spec](docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md#5-data--grounding) for full detail.

## Evaluation

An eval suite gates every prompt/model change before it reaches production:

- **Extraction**: deterministic field-level exact/fuzzy match against gold JSON.
- **Coverage**: LLM-as-judge on correctness (approve/deny/needs-info vs. gold) plus a hallucination/grounding check.
- **Fraud-risk**: LLM-as-judge on rationale grounding (real tool data, not fabricated signals) plus tier accuracy.
- A distinct, stronger judge model is used where feasible to avoid a model favoring its own outputs.

Runs in CI on every PR; the build fails if aggregate scores drop below a checked-in baseline per agent. See [Section 6 of the design spec](docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md#6-evaluation-framework).

## Deployment

Deployed to Azure Container Apps as five container apps (API, three MCP servers, Streamlit frontend), scale-to-zero for demo cost. CI/CD (GitHub Actions, at the repo root under `.github/workflows/`) builds and pushes images on merge to `main`, deploys a canary revision (traffic-split), and promotes to 100% after manual approval.

Infrastructure is defined as Bicep (`iac/`):
- `iac/platform.bicep` — Azure OpenAI + Azure AI Search (documents the intended config; the actual deploy path is `scripts/iac/create-platform-manual.ps1`, since a real Bicep-driven deploy of these resources tripped Azure's real-time fraud protection — see the Phase 10 plan's Lessons Learned for the full story).
- `iac/app-infra-base.bicep` / `iac/app-infra-apps.bicep` — ACR, Log Analytics, Postgres Flexible Server, Storage, the Container Apps environment, and the five container apps. Fully Bicep-driven; proven to tear down and redeploy cleanly via the scripts in `scripts/iac/`.
- `iac/app-infra-base.bicep` also provisions Application Insights (workspace-based, linked to the same Log Analytics workspace) and a Bicep-defined Workbook dashboard. Structured logging, tracing, and metrics ship via OpenTelemetry (`azure-monitor-opentelemetry`), auto-instrumented across the API, all three MCP servers, and the Agent Framework orchestration graph itself — agent/tool spans nest under the originating HTTP request with no manual span-wrapping needed. Custom metrics (claim outcome, extraction confidence, fraud-risk score) are recorded through the same OpenTelemetry meter agent-framework's own native metrics use.

Secrets (Azure OpenAI/Search keys, Postgres password, Blob connection string) are never committed — they're Bicep `@secure()` parameters and GitHub Actions secrets, with CD authenticating to Azure via OIDC/federated credentials.

AKS was evaluated and deliberately not used at this scale; Container Apps + KEDA gives the same Kubernetes-based autoscaling story without a standing cluster cost. See [Section 7 of the design spec](docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md#7-deployment--cicd) for the full tradeoff writeup.

## Project docs

- [Design spec](docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md) — goals, architecture, data/grounding, eval framework, deployment, testing strategy.
- [Roadmap](docs/superpowers/plans/2026-08-10-roadmap.md) — phase-by-phase status.
- `docs/superpowers/plans/` — one detailed implementation plan per phase, each with a "Lessons Learned" section capturing real bugs and gotchas hit during execution (Postgres DSN encoding, Container Apps identity/RBAC timing, Azure Search indexing latency, PowerShell/CLI argument-marshaling pitfalls, and more).

## Status

| Phase | Status |
|---|---|
| 0 — Foundations | ✅ |
| 1 — Synthetic data generation | ✅ |
| 2 — MCP servers | ✅ |
| 3 — Extraction Agent | ✅ |
| 4 — Azure AI Search + Coverage Agent | ✅ |
| 5 — Fraud-Risk Agent | ✅ |
| 6 — Supervisor orchestration graph | ✅ |
| 7 — FastAPI orchestrator endpoints | ✅ |
| 8 — Eval framework | ✅ |
| 9 — Containerization & CI | ✅ |
| 10 — Azure deployment | ✅ |
| 11 — Web frontend | ✅ |
| 12 — Observability | ✅ |

## Non-goals

- Real customer data or regulatory compliance certification (synthetic data only).
- Autonomous final claims decisions — the system always produces a recommendation for a human adjuster.
- A chat-style conversational UI (the frontend is a structured, form-based multi-page app).
- A trained ML fraud classifier — fraud-risk scoring is LLM reasoning over real tool-sourced signals, which fits the agentic architecture and produces an explainable rationale.
