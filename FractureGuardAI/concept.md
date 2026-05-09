# FractureGuard AI — Concept Document

> **A real-time predictive maintenance platform for hydraulic fracturing sites.**  
> Engineers converse with an AI that autonomously fetches live sensor telemetry, runs an ML risk model, retrieves safety protocol context, and delivers a generated technical report — all within a single chat turn.

---

## Table of Contents

1. [Problem Statement](#1-problem-statement)
2. [Solution Overview](#2-solution-overview)
3. [System Architecture](#3-system-architecture)
4. [Service Breakdown](#4-service-breakdown)
5. [End-to-End Data Flow](#5-end-to-end-data-flow)
6. [Infrastructure & Deployment](#6-infrastructure--deployment)
7. [Security Model](#7-security-model)
8. [Tech Stack Summary](#8-tech-stack-summary)

---

## 1. Problem Statement

A hydraulic fracturing ("fracking") site runs hundreds of sensors simultaneously — measuring wellbore pressure, slurry flow rate, surface vibration, pump temperature, and more. Operators must monitor all of these in real time and make split-second decisions to avoid costly or dangerous failures, the most critical being a **screen-out**: a clogged well caused by proppant bridging that can halt operations and damage equipment.

Today, operators either watch dashboards reactively or wait for engineers to manually run analysis scripts. There is no system that:

- Answers natural-language questions about current risk levels
- Autonomously fetches live data, runs predictive models, and explains the result in plain English
- Pushes live alerts to operators the moment a risk threshold is crossed

**FractureGuard AI** closes that gap.

---

## 2. Solution Overview

FractureGuard AI is a **polyglot microservices platform** built across four technology stacks, each chosen for what it does best:

| Service | Stack | Why |
|---|---|---|
| Orchestrator & API Gateway | .NET Core + Semantic Kernel | Agentic workflow management, enterprise auth, RAG integration |
| ML Predictor | Python + Scikit-learn | Numerical computing, ML ecosystem, model training |
| Real-Time Notifier | Node.js + Socket.io | Non-blocking I/O, WebSocket fan-out to thousands of clients |
| Operator Dashboard | Angular + MSAL.js | Structured SPA, strong typing, enterprise auth flows |

The system uses a **hybrid sync/async execution model**:

- **Fast path (sync, < 1 s):** Sensor snapshots, RAG-retrieved safety context, and LLM explanations are handled inline and streamed directly to the chat panel.
- **Heavy path (async):** ML simulations are expensive. When a prediction is required, the orchestrator publishes a job to a message queue, immediately acknowledges the request, and pushes the completed report to the dashboard via WebSocket when the Python predictor finishes — decoupling user experience from compute time.

---

## 3. System Architecture

```
┌─────────────────────────────────────────────────────┐
│                  Angular Dashboard                   │
│   Monitoring-First · MSAL Auth · Socket.io Client   │
└────────────────┬──────────────────┬─────────────────┘
                 │ WebSocket        │ REST / SSE
                 ▼                  ▼
┌───────────────────────┐  ┌────────────────────────────────┐
│   Node.js Notifier    │  │    FractureGuard.Api (.NET)     │
│  Express + Socket.io  │  │  ★ Semantic Kernel Orchestrator │
│  Sensor stream sim    │◄─┤  SensorPlugin · RAGPlugin       │
│  Live alert broadcast │  │  PredictionPlugin · ReportPlugin│
└───────────────────────┘  │  Cosmos DB · Entra ID auth      │
                           └──────────┬──────────────────────┘
                                      │ publish / consume
                                      ▼
                        ┌─────────────────────────────┐
                        │  Azure Service Bus           │
                        │  (RabbitMQ in local dev)     │
                        │  Queue: analysis-requests    │
                        │  Queue: analysis-results     │
                        └──────────┬──────────────────┘
                                   │
                                   ▼
                  ┌────────────────────────────────────┐
                  │   Python Fracture Predictor         │
                  │   FastAPI · Scikit-learn RF Model   │
                  │   Features: pressure · flow rate    │
                  │            vibration · temperature  │
                  │   Output: risk_pct · factors · conf │
                  └────────────────────────────────────┘

  Shared Azure Services (prod) / Local Emulators (dev):
  ┌────────────────┐ ┌────────────┐ ┌─────────────┐ ┌──────────────┐
  │ Azure AI Search│ │ Cosmos DB  │ │ Azure Entra │ │Container Apps│
  │ Safety RAG     │ │ Chat hist. │ │ ID auth     │ │ Prod hosting │
  └────────────────┘ └────────────┘ └─────────────┘ └──────────────┘

  Legend:
  ──── Sync (fast path)    ╌╌╌╌ Async Service Bus    ════ WebSocket
```

---

## 4. Service Breakdown

### 4.1 FractureGuard.Api — .NET Core Orchestrator

**Role:** Primary API gateway and agentic workflow engine.

**Key technologies:**
- ASP.NET Core Web API
- **Semantic Kernel** — manages the agentic loop and Plugin invocation
- **Microsoft.Identity.Web** — validates Entra ID JWT Bearer tokens
- **Azure AI Search SDK** — vector search over indexed safety manuals (RAG)
- **Azure Service Bus SDK** — publishes/consumes ML job messages
- **Azure Cosmos DB SDK** — persists per-session chat history

**Semantic Kernel Plugins:**

| Plugin | Responsibility |
|---|---|
| `SensorPlugin` | Sync HTTP call to Node.js; returns latest sensor readings snapshot |
| `RAGPlugin` | Vector search on Azure AI Search; returns ranked safety protocol chunks |
| `PredictionPlugin` | Publishes sensor payload to `analysis-requests` queue; role-gated to `SiteEngineer` |
| `ReportPlugin` | Calls LLM with ML output + RAG context; formats and saves the final technical report |

**Why .NET + Semantic Kernel:**  
Semantic Kernel provides a production-grade abstraction for building LLM-powered agents in C#, including native Plugin composition, memory connectors, and process orchestration. It integrates directly with Azure OpenAI and Azure AI Search, making it the natural fit for an enterprise-grade agentic system on Azure.

---

### 4.2 Fracture Predictor — Python Microservice

**Role:** Heavy-duty numerical prediction and ML inference.

**Key technologies:**
- **FastAPI** — lightweight async web framework (also exposes a `/health` endpoint for container readiness probes)
- **Scikit-learn RandomForestClassifier** — trained on synthetic fracking sensor data
- **Azure Service Bus SDK (Python)** — consumes `analysis-requests`, publishes to `analysis-results`
- **Pandas / NumPy** — feature engineering on raw sensor readings

**Input (from Service Bus message):**
```json
{
  "session_id": "abc-123",
  "sensor_snapshot": {
    "pressure_psi": 847,
    "pressure_trend_pct": 12.3,
    "flow_rate_bpm": 12.4,
    "flow_rate_variance": 0.8,
    "vibration_g": 2.3,
    "temperature_c": 42
  }
}
```

**Output (to Service Bus reply queue):**
```json
{
  "session_id": "abc-123",
  "risk_pct": 85,
  "contributing_factors": ["pressure_trend", "vibration_amplitude"],
  "confidence": 0.91
}
```

**Why Python:**  
The ML ecosystem (Scikit-learn, TensorFlow, Pandas) is unmatched in Python. Isolating prediction into its own service also means the model can be retrained and redeployed independently of the .NET orchestrator — a clean separation of concerns.

---

### 4.3 Real-Time Notifier — Node.js Service

**Role:** Real-time telemetry streaming and live alert fan-out.

**Key technologies:**
- **Express** — HTTP server for the webhook endpoint and health check
- **Socket.io** — WebSocket server managing per-engineer rooms
- **`@azure/service-bus`** — optional direct Service Bus subscription (alternative to webhook mode)

**Two responsibilities:**

1. **Sensor stream simulator:** Emits configurable synthetic sensor readings (pressure, flow rate, vibration, temperature) to all connected Angular clients at high frequency, modeling realistic fracking site noise. In production this would be replaced by an OPC-UA or SCADA data adapter.

2. **Alert broadcaster:** Accepts a POST `/notify` from the .NET API (when a prediction report is ready) and pushes the payload to the correct engineer's Socket.io room. The Angular dashboard renders this as a live alert banner without a page reload.

**Why Node.js:**  
Node's non-blocking event loop handles thousands of concurrent WebSocket connections and high-frequency sensor pings without the thread-per-connection overhead of synchronous servers. It is the right tool for this specific job.

---

### 4.4 Angular Dashboard — Operator Frontend

**Role:** Operator-facing monitoring and AI interaction interface.

**Key technologies:**
- Angular 17+ standalone components
- **MSAL.js (`@azure/msal-angular`)** — Entra ID auth with silent token renewal
- **ApexCharts (or Chart.js)** — real-time time-series charts for sensor data
- **Socket.io client** — live sensor feed and alert banner updates

**Layout (Monitoring-First):**
- **Default view:** Full-width sensor telemetry panel — KPI cards (current PSI, flow rate, vibration, temperature) + live time-series charts per sensor. At-risk readings are highlighted in red automatically via Socket.io events.
- **Chat panel:** Collapsible right-side panel. Engineer types a natural-language question; the response streams in from the .NET API via Server-Sent Events. When an async report completes, it appears inline in the chat thread alongside a live alert banner in the monitoring panel.
- **Reports tab:** Historical list of AI-generated risk reports, fetched from Cosmos DB.

---

## 5. End-to-End Data Flow

**Scenario:** Engineer asks *"What is the risk of a screen-out in the next hour?"*

```
Step 1 — Auth & Routing
  Angular  ──POST /api/chat + Bearer token──►  FractureGuard.Api
  .NET validates Entra ID JWT → confirms SiteEngineer role

Step 2 — Fast Path (sync, immediate response)
  SK invokes SensorPlugin  ──HTTP──►  Node.js  → sensor snapshot
  SK invokes RAGPlugin  ──vector search──►  Azure AI Search → safety chunks
  SK calls LLM (Azure OpenAI) with: snapshot + safety context + question
  LLM streams: "Current pressure is 847 PSI, trending up 12.3%.
                Running full screen-out simulation — alerting you when done."
  Angular renders streamed response in chat panel.

Step 3 — Heavy Path trigger (async)
  SK invokes PredictionPlugin (SiteEngineer role confirmed)
  Publishes to Service Bus queue: analysis-requests
    payload: { session_id, sensor_snapshot }
  Angular chat shows spinner: "Analyzing..."

Step 4 — Python Prediction
  Fracture Predictor consumes analysis-requests message
  Runs RandomForestClassifier on sensor features
  Publishes to Service Bus queue: analysis-results
    payload: { session_id, risk_pct: 85, factors: [...], confidence: 0.91 }

Step 5 — Report Generation
  .NET Service Bus consumer picks up analysis-results
  ReportPlugin calls LLM with ML output + RAG context:
    "Screen-out probability: 85% (confidence 91%).
     Primary drivers: pressure trending 12% above threshold in zone 3,
     vibration amplitude at 2.3g.
     Recommended action: reduce flow rate by 15% immediately,
     monitor zone 3 pressure over next 20 minutes."
  Report saved to Cosmos DB (partitioned by userId + sessionId).

Step 6 — Live Alert
  .NET POSTs completed report to Node.js /notify
  Socket.io pushes alert to engineer's room
  Angular: alert banner appears in monitoring panel
            report appended to chat thread
            at-risk sensor cards highlighted red
```

---

## 6. Infrastructure & Deployment

### Local Development

All services run via a single `docker compose up`. No Azure account required.

| Azure Service | Local Substitute | Notes |
|---|---|---|
| Azure Service Bus | **RabbitMQ** | AMQP-compatible; same SDK abstractions apply |
| Cosmos DB | **Cosmos DB Linux Emulator** | Identical API surface |
| Azure AI Search | **FAISS + local embedding model** | `text-embedding-3-small` via Ollama or Azure OpenAI |
| Azure Entra ID | **Hardcoded dev JWT** | Token with `SiteEngineer` role claim; excluded from image via `.dockerignore` |
| Azure OpenAI | **Ollama (Llama 3 / Mistral)** | Swapped via `IKernelBuilder` config |

Sensor data is fully simulated by the Node.js service. No real hardware dependency.

### Production (Azure)

| Component | Azure Service |
|---|---|
| All 4 services | Azure Container Apps (serverless, scales to zero) |
| Container registry | Azure Container Registry |
| Message queues | Azure Service Bus Standard |
| Chat history | Azure Cosmos DB Serverless |
| RAG index | Azure AI Search Basic |
| LLM | Azure OpenAI (GPT-4o) |
| Auth | Azure Entra ID (app registration + role assignments) |
| Secrets | Azure Key Vault + managed identity (no env-var secrets) |

### CI/CD

GitHub Actions pipeline per service:
1. `build` — restore/install dependencies, compile
2. `test` — unit + integration tests
3. `docker-build` — build and push image to Azure Container Registry
4. `deploy` — update Azure Container App revision

---

## 7. Security Model

### Authentication & Authorization

Azure Entra ID is the single identity provider. Two application roles are defined in the app manifest:

| Role | Permissions |
|---|---|
| `SiteOperator` | View dashboard, ask free-form questions (fast path only) |
| `SiteEngineer` | All of the above + trigger ML simulations (heavy path, high-cost) |

The `PredictionPlugin` checks the `roles` claim in the validated JWT before publishing to Service Bus. An unauthorized call returns `HTTP 403` before any AI computation is initiated.

### Service-to-Service Security

- **Python Predictor** has no public HTTP surface — exclusively event-driven via Service Bus. Access requires a Shared Access Signature scoped to the `analysis-requests` queue.
- **Node.js Notifier** accepts webhook POSTs only from the .NET API's managed identity (production) or a shared secret header (local dev).
- **No service** stores its own credentials — all connection strings and API keys are retrieved from Azure Key Vault at startup via managed identity.

### Data Security

- Cosmos DB chat history is **partitioned by `userId`** — engineers cannot access each other's sessions.
- Raw sensor readings are **not persisted** beyond the current chat turn. Only LLM-generated reports are stored.
- Azure AI Search indexes contain **only public safety manuals** — no proprietary operational data.

### Local Dev Security

- The dev JWT is generated locally and is **never committed** (`.gitignore` + `.dockerignore`).
- A `.env.example` documents all required secrets; `.env` files are gitignored.

---

## 8. Tech Stack Summary

| Layer | Technology | Version |
|---|---|---|
| Orchestrator | ASP.NET Core, Semantic Kernel | **.NET 9.0**, SK 1.x (latest stable) |
| LLM | Azure OpenAI (GPT-4o) / Ollama (local) | GPT-4o |
| RAG Store | Azure AI Search / FAISS (local) | API version 2024-05 |
| Chat History | Azure Cosmos DB (NoSQL) | SDK v3 |
| ML Predictor | Python, FastAPI, Scikit-learn | **Python 3.12**, FastAPI 0.115.x, sklearn 1.5.x |
| Message Bus | Azure Service Bus / RabbitMQ (local) | AMQP 1.0 |
| Real-Time | Node.js, Express, Socket.io | **Node.js 22 LTS**, Socket.io 4 |
| Frontend | Angular, MSAL.js, ApexCharts | **Angular 19**, MSAL 3 |
| Auth | Azure Entra ID | OAuth 2.0 / OIDC |
| Containerization | Docker, Docker Compose | Docker 27+ |
| Prod Hosting | Azure Container Apps | Consumption plan |
| CI/CD | GitHub Actions + Azure Container Registry | — |
| Secrets | Azure Key Vault + Managed Identity | — |

---

*Document version: 2026-05-07*  
*Status: Concept approved — ready for implementation planning*
