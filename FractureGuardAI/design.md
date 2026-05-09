# FractureGuard AI — Implementation Plan (Main Coordinator)

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Each phase has its own design file — link to each is below.

**Goal:** Build a real-time predictive maintenance platform for fracking sites — a .NET 9 Semantic Kernel orchestrator, a Python 3.12 ML predictor, a Node.js 22 real-time notifier, and an Angular 19 dashboard, all wired together locally via Docker Compose.

**Architecture:** Hybrid sync/async. Fast queries (sensor snapshot + RAG + LLM explanation) stream back to the Angular chat panel in < 1 s. ML predictions publish to RabbitMQ, run in the Python service, and push the completed report to the dashboard via Socket.io when done.

**Tech Stack:** .NET 9 · Semantic Kernel 1.21.x · Python 3.12 · FastAPI 0.115.x · Scikit-learn 1.5.x · Node.js 22 LTS · Socket.io 4 · Angular 19 · MSAL 3 · RabbitMQ (dev) · Cosmos DB Emulator (dev)

---

## Phase Overview & Execution Order

```
Phase 0 — Scaffolding
    │
    ├──────────────────────────────────┐
    │                                  │
Phase 1          Phase 2          Phase 3          Phase 4
Python           Node.js          .NET API         Angular
Predictor        Notifier         (Orchestrator)   Dashboard
    │                │                │                │
    └────────────────┴────────────────┴────────────────┘
                                      │
                                 Phase 5
                                 Integration
```

**Phase 0 must complete first.** Phases 1–4 are independent and can be worked in parallel. Phase 5 requires all prior phases.

---

## Phase Files

| Phase | File | Goal | Tasks | Can parallel? |
|---|---|---|---|---|
| **0** | [design-phase-0-scaffolding.md](design-phase-0-scaffolding.md) | Monorepo skeleton, `.env`, Docker Compose | 1 | No — must go first |
| **1** | [design-phase-1-python-predictor.md](design-phase-1-python-predictor.md) | Python ML microservice (RandomForest + RabbitMQ consumer) | 3 | Yes (after Phase 0) |
| **2** | [design-phase-2-nodejs-notifier.md](design-phase-2-nodejs-notifier.md) | Node.js real-time notifier (sensor simulator + Socket.io) | 2 | Yes (after Phase 0) |
| **3** | [design-phase-3-dotnet-api.md](design-phase-3-dotnet-api.md) | .NET 9 Semantic Kernel orchestrator + ChatController | 5 | Yes (after Phase 0) |
| **4** | [design-phase-4-angular-dashboard.md](design-phase-4-angular-dashboard.md) | Angular 19 dashboard (monitoring, chat, alerts) | 4 | Yes (after Phase 0) |
| **5** | [design-phase-5-integration.md](design-phase-5-integration.md) | Docker Compose end-to-end smoke test | 1 | No — must go last |

**Total tasks: 16** (1 + 3 + 2 + 5 + 4 + 1)

---

## Full Repository Structure

```
FractureGuardAI/
├── docker-compose.yml
├── .env.example
├── .gitignore
│
├── FractureGuard.Api/                    .NET 9 orchestrator
│   ├── Controllers/ChatController.cs     POST /api/chat (SSE), GET /api/chat/{id}
│   ├── Plugins/
│   │   ├── SensorPlugin.cs               Fetches live sensor snapshot
│   │   ├── RAGPlugin.cs                  Vector search on safety manuals
│   │   ├── PredictionPlugin.cs           Role-gated ML job publisher
│   │   └── ReportPlugin.cs              LLM risk report generator
│   ├── Services/
│   │   ├── AnalysisJobService.cs         RabbitMQ publisher → analysis-requests
│   │   ├── AnalysisResultConsumer.cs     BackgroundService consuming analysis-results
│   │   └── NotifierService.cs            HTTP POST to Node.js /notify
│   ├── Infrastructure/
│   │   ├── CosmosDbService.cs            Chat session persistence
│   │   └── VectorSearchService.cs        Azure AI Search / FAISS
│   └── Models/                           Shared record types
│
├── FractureGuard.Api.Tests/              xUnit + Moq + FluentAssertions
│
├── fractureguard-predictor/              Python 3.12 ML service
│   ├── app/
│   │   ├── main.py                       FastAPI + /health endpoint
│   │   ├── consumer.py                   RabbitMQ consumer loop
│   │   ├── predictor.py                  RandomForestClassifier inference
│   │   ├── features.py                   Sensor → numpy feature array
│   │   └── models.py                     Pydantic schemas
│   ├── scripts/train_model.py            Synthetic data + RF training
│   └── tests/                            pytest
│
├── fractureguard-notifier/               Node.js 22 LTS notifier
│   └── src/
│       ├── index.js                      Express + Socket.io server
│       ├── sensorSimulator.js            Synthetic sensor readings
│       └── roomManager.js               Session-to-socket room mapping
│
└── fractureguard-dashboard/             Angular 19 dashboard
    └── src/app/
        ├── features/
        │   ├── monitoring/               KPI cards, live chart, alert banner
        │   ├── chat/                     SSE streaming chat panel
        │   └── reports/                  Historical reports list
        └── services/
            ├── telemetry.service.ts      Socket.io sensor stream → signal
            ├── chat.service.ts           fetch + SSE → streaming signal
            └── alert.service.ts         Socket.io alert:report → signal
```

---

## End-to-End Data Flow (Summary)

```
Engineer types: "What is the risk of a screen-out in the next hour?"

FAST PATH (< 1 second, sync):
  Angular ──POST /api/chat──► .NET API
  .NET: SensorPlugin ──GET──► Node.js → sensor snapshot
  .NET: RAGPlugin ──search──► Azure AI Search → safety protocols
  .NET: LLM call → streams "Simulation submitted, analysing..."
  Angular: renders streamed tokens in chat panel

HEAVY PATH (async, 5–15 seconds):
  .NET: PredictionPlugin ──publish──► RabbitMQ: analysis-requests
  Python: consumes message → RandomForest → risk_pct: 85%
  Python ──publish──► RabbitMQ: analysis-results
  .NET: AnalysisResultConsumer picks up result
  .NET: ReportPlugin → LLM generates technical report
  .NET ──POST /notify──► Node.js
  Node.js ──Socket.io emit──► Angular: alert banner appears
```

---

## Azure vs. Local Dev Mapping

| Azure Service | Local Substitute | Config key |
|---|---|---|
| Azure Service Bus | RabbitMQ 3.13 | `RABBITMQ_HOST` |
| Cosmos DB | Cosmos DB Linux Emulator | `COSMOS_ENDPOINT` |
| Azure AI Search | FAISS + local embeddings | `AZURE_SEARCH_ENDPOINT` |
| Azure OpenAI (GPT-4o) | Ollama (llama3) | `OLLAMA_ENDPOINT` |
| Azure Entra ID | Hardcoded dev JWT | `DEV_JWT_SECRET` |

---

## Tech Stack Versions

| Service | Technology | Version |
|---|---|---|
| FractureGuard.Api | .NET / ASP.NET Core | **9.0** |
| | Semantic Kernel | **1.21.x** |
| fractureguard-predictor | Python | **3.12** |
| | FastAPI | **0.115.x** |
| | Scikit-learn | **1.5.x** |
| fractureguard-notifier | Node.js | **22 LTS** |
| | Socket.io | **4.x** |
| fractureguard-dashboard | Angular CLI | **19.x** |
| | MSAL Angular | **3.x** |
| Infrastructure | Docker Compose | **27+** |
| | RabbitMQ | **3.13** |

---

## Recommended Execution Strategy

**Subagent-Driven (recommended):** Dispatch one subagent per phase file. Phases 1–4 can run simultaneously as four parallel subagents after Phase 0 completes. Phase 5 runs after all four finish.

**Inline Execution:** Work through each phase file sequentially in this session using `superpowers:executing-plans`.

---

*Document version: 2026-05-07*  
*Concept reference: [concept.md](concept.md)*
