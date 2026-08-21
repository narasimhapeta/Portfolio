# CustomerOps Platform — Architecture

**Status:** Approved 2026-08-20. This document is the design spec that implementation plans in `docs/plans/` argue from.

## 1. What This Is

A Customer Operations & Document Processing capstone built to demonstrate enterprise .NET + Azure architecture. Business functionality is intentionally simple (customer CRUD + one simulated async document-processing operation) so the interesting work is in the plumbing: caching, queue-based load leveling, idempotency, resiliency, observability, and an IaC-driven path to Azure.

## 2. Business Requirements (minimal, by design)

- View / search / create / update / deactivate customers
- Submit a simulated document-processing operation, poll its status (`Submitted → Processing → Completed/Failed`)
- No auth, no SignalR, no real AI processing in the initial phases — see §6 Deferred.

## 3. Final Target Architecture (end state)

```
React SPA
   |
   v
CDN / Front Door
   |
   v
Azure API Management
   |
   v
AKS (.NET API pods)
   |
   +--> Redis
   +--> Azure SQL
   +--> Service Bus --> Worker --> Notification --> SignalR --> React

Cross-cutting: ACR, Key Vault, App Insights, Log Analytics, Managed Identity
Provisioned entirely via Bicep.
```

## 4. Local Development Architecture (where we start, and stay for many phases)

```
React (Vite) --> .NET Web API
                    |
                    +--> SQL Server (container)
                    +--> Redis (container)
                    +--> Service Bus Emulator (container) --> local Consumer
```

No Azure resources exist during local development. All infrastructure dependencies are reached through configuration/DI so that swapping the Service Bus emulator for real Azure Service Bus (or local Redis for Azure Cache for Redis, etc.) is a config change, not a code change. Environments: `Development`, `Test`, `Production`.

## 5. Backend Structure

Pragmatic Clean Architecture, one Web API, one primary controller (`CustomerOperationsController`), not multiple microservices:

```
src/
  CustomerPortal.Api             Controllers, middleware, DI, configuration
  CustomerPortal.Application     Application services, DTOs, interfaces, workflows
  CustomerPortal.Domain          Entities, domain rules
  CustomerPortal.Infrastructure  EF Core, Redis, Service Bus, external deps
```

Endpoints (v1):

```
GET    /api/v1/customers
GET    /api/v1/customers/{id}
GET    /api/v1/customers/search
POST   /api/v1/customers
PUT    /api/v1/customers/{id}
DELETE /api/v1/customers/{id}

POST   /api/v1/operations
GET    /api/v1/operations/{operationId}
```

## 6. Frontend Structure

Thin React + TypeScript + Vite app, one primary page (`CustomerOperationsPage`) covering customer search/list/create/edit/deactivate and document-operation submit/status. Server state via TanStack Query. No Redux, no design system.

## 7. Technology Choices

| Concern | Local | Azure |
|---|---|---|
| Backend | ASP.NET Core (.NET 10) | same, on AKS |
| Data | SQL Server (container) | Azure SQL |
| Cache | Redis (container) | Azure Cache for Redis |
| Messaging | Azure Service Bus Emulator | Azure Service Bus |
| IaC | — | Bicep only, no manual portal provisioning |
| CI/CD | — | GitHub Actions |
| Frontend | React + TS + Vite + TanStack Query | same, served via CDN/Front Door |

## 8. Development Phases

1. React + .NET + SQL (foundation, CRUD)
2. Redis (cache-aside)
3. Service Bus Emulator (async operation submission)
4. Background Worker (simulated processing, status updates)
5. Testing + Resiliency (unit/integration tests, Polly, idempotency)
6. Observability (logging, correlation IDs, OpenTelemetry, health checks)
7. Docker (multi-stage build, full local container stack)
8. Kubernetes Concepts (local manifests; not necessarily a live cluster)
9. Bicep (author + validate, no deploy)
10. Azure Infrastructure (deploy via Bicep, `what-if` reviewed first)
11. APIM (routing, versioning, throttling, CORS)
12. CI/CD (build/test/scan/push/deploy pipeline)
13. Authentication (Entra ID, OAuth/OIDC, JWT, RBAC)
14. SignalR (replace polling with push)

Kubernetes/AKS is deliberately pushed later than in the original draft — concepts before a live cluster, cluster before Azure deployment.

## 9. Deferred (explicitly, not accidentally)

Authentication (Entra ID/OAuth/JWT/RBAC) until Phase 13, SignalR until Phase 14, real document intelligence/AI (optional, post-core), AKS as a live target until Phase 8+, microservice decomposition (never, unless a real use case emerges).

## 10. Repository Structure

```
CustomerOps/
├── src/
│   ├── CustomerPortal.Api/
│   ├── CustomerPortal.Application/
│   ├── CustomerPortal.Domain/
│   └── CustomerPortal.Infrastructure/
├── frontend/customer-portal/
├── tests/
│   ├── CustomerPortal.UnitTests/
│   ├── CustomerPortal.IntegrationTests/
│   └── CustomerPortal.ApiTests/
├── infra/{main.bicep, modules/, parameters/}
├── k8s/
├── pipelines/
├── docs/{architecture.md, deployment.md, scalability.md, security.md, decisions/, plans/}
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## 11. Toolchain (verified on dev machine, 2026-08-20)

- .NET SDK 10.0.302
- Node v24.18.0 / npm 11.16.0
