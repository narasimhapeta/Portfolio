# Claims Service — Concept & Design Document

**Date:** 2026-05-11  
**Status:** Approved  
**Author:** Narasimha Peta

---

## 1. Overview

The Claims Service is a .NET 9 microservice that manages the full insurance claim lifecycle — from first notice of loss (FNOL) through photo upload, AI-assisted damage assessment, adjuster assignment, and final payout status tracking. It is part of a broader insurance portfolio and demonstrates real event-driven architecture on Azure using Event Grid, Blob Storage, Azure Functions, and Cosmos DB.

---

## 2. Goals

- Demonstrate real Azure service integration: Blob Storage, Event Grid, Azure Functions, Cosmos DB, Key Vault
- Implement the complete FNOL → review → decision → payment status workflow
- Show production-grade event-driven architecture: Blob upload → Event Grid push → Function → Cosmos DB update
- Provide a clean REST API secured with JWT bearer tokens
- Deploy both the Web API and Function App to Azure

---

## 3. Architecture

### 3.1 Solution Structure

```
ClaimsService/
├── ClaimsService.sln
├── ClaimsService.Api/                  # .NET 9 Web API
│   ├── Controllers/
│   │   ├── ClaimsController.cs
│   │   └── AdjustersController.cs
│   ├── Models/
│   │   ├── Claim.cs
│   │   └── Adjuster.cs
│   ├── Services/
│   │   ├── ClaimService.cs
│   │   └── BlobUploadService.cs
│   ├── Repositories/
│   │   └── ClaimRepository.cs
│   ├── Program.cs
│   └── appsettings.json
│
└── ClaimsService.Functions/            # Azure Functions v4 isolated worker
    ├── ClaimProcessingFunction.cs      # Event Grid trigger
    ├── Program.cs
    └── local.settings.json
```

### 3.2 Azure Services

| Service | Role |
|---------|------|
| **Azure Blob Storage** | Stores accident photos uploaded by customers; emits `BlobCreated` events |
| **Azure Event Grid** | System Topic on Storage Account routes `BlobCreated` events to the Function App |
| **Azure Functions (Event Grid trigger)** | Receives pushed events from Event Grid, runs mock AI processing, updates Cosmos DB |
| **Azure Cosmos DB** | Stores claim and adjuster documents (NoSQL, JSON) |
| **Azure Key Vault** | Stores all secrets (connection strings, JWT secret); accessed via Managed Identity — no credentials in config |

### 3.3 Event-Driven Flow

```
Customer
  │
  ├─ POST /api/claims/fnol
  │     → API creates Claim (status: FNOL) in Cosmos DB
  │
  ├─ POST /api/claims/{id}/photos/upload-url
  │     → API generates SAS URL (5-min expiry) pointing to Blob Storage
  │
  ├─ [Client uploads photo directly to Blob Storage using SAS URL]
  │
  │   Azure Blob Storage emits Microsoft.Storage.BlobCreated event
  │     → Event Grid System Topic receives the event
  │     → Event Grid pushes event to ClaimProcessingFunction (HTTPS endpoint)
  │     → Function extracts claimId from blob URL in event payload
  │     → Mock AI runs (2s delay, returns fixed damage score 72)
  │     → Claim status updated to "UnderReview" in Cosmos DB
  │     → damageScore written to Claim document
  │
Admin
  ├─ PUT /api/claims/{id}/assign      → assigns adjuster
  └─ PUT /api/claims/{id}/status      → advances to Approved / Rejected / Paid
```

### 3.4 Event Grid Integration Detail

Event Grid requires a **publicly accessible HTTPS endpoint** on the Function App. This is why the Function must be deployed to Azure before the Event Grid subscription can be wired up. The setup order is:

1. Deploy Function App to Azure
2. Create Event Grid System Topic on the Storage Account
3. Create Event Grid Subscription: filter on `Microsoft.Storage.BlobCreated`, endpoint = Function App's Event Grid trigger URL
4. Event Grid performs a one-time **endpoint validation handshake** with the Function automatically

The `ClaimProcessingFunction` receives an `EventGridEvent` object. The blob URL in `event.Data["url"]` contains the path `claims/{claimId}/{filename}` from which the `claimId` is extracted.

---

## 4. Data Model

### 4.1 Cosmos DB — `ClaimsDb` database

**Container: `claims`** (partition key: `/customerId`)

```json
{
  "id": "claim-uuid",
  "customerId": "cust-uuid",
  "policyNumber": "POL-123456",
  "status": "FNOL",
  "incidentDate": "2026-05-10T14:00:00Z",
  "incidentDescription": "Rear-end collision on I-95",
  "photosBlobPaths": ["claims/claim-uuid/photo1.jpg"],
  "damageScore": null,
  "adjusterId": null,
  "createdAt": "2026-05-11T09:00:00Z",
  "updatedAt": "2026-05-11T09:00:00Z"
}
```

**Container: `adjusters`** (partition key: `/id`) — seeded at startup

```json
{
  "id": "adj-uuid",
  "name": "Jane Smith",
  "email": "jane.smith@insurer.com",
  "isAvailable": true
}
```

### 4.2 Claim Status Lifecycle

```
FNOL
  └─ UnderReview   (set automatically by Azure Function after photo processed)
        ├─ Approved  (set by admin)
        │     └─ Paid  (set by admin)
        └─ Rejected  (set by admin)
```

Invalid transitions are rejected with `400 Bad Request`.

---

## 5. API Endpoints

**Base URL:** `https://<app-service-name>.azurewebsites.net/api` (deployed) / `https://localhost:7001/api` (local)
**Auth:** JWT Bearer required on all endpoints. Role claim: `admin` or `customer`.

### Claims

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| `POST` | `/api/claims/fnol` | customer | Submit FNOL. Body: `policyNumber`, `incidentDate`, `incidentDescription`. Returns created claim. |
| `GET` | `/api/claims/{id}` | customer, admin | Get claim by ID. Customer can only access their own claims (validated via `customerId` from JWT `sub`). |
| `GET` | `/api/claims` | admin | List all claims. Optional query param: `?status=FNOL\|UnderReview\|...` |
| `POST` | `/api/claims/{id}/photos/upload-url` | customer | Returns a time-limited SAS URL for direct photo upload to Blob Storage. |
| `PUT` | `/api/claims/{id}/assign` | admin | Assign an adjuster. Body: `{ "adjusterId": "adj-uuid" }` |
| `PUT` | `/api/claims/{id}/status` | admin | Advance status. Body: `{ "status": "Approved\|Rejected\|Paid" }` |

### Adjusters

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| `GET` | `/api/adjusters` | admin | List all adjusters. |

### Azure Function (internal, not HTTP)

| Trigger | Description |
|---------|-------------|
| Event Grid trigger | Receives `Microsoft.Storage.BlobCreated` events pushed by Event Grid. Extracts `claimId` from blob URL, runs mock AI (score 72, 2s delay), updates claim `damageScore` and status to `UnderReview` in Cosmos DB. |

---

## 6. Authentication & Authorization

- **JWT Bearer** via `Microsoft.AspNetCore.Authentication.JwtBearer`
- Token payload includes `sub` (customerId), `role` (`admin` or `customer`)
- Shared secret validation (HS256) — suitable for portfolio/demo use
- Admin-only endpoints protected with `[Authorize(Roles = "admin")]`
- Customer endpoints validate that `sub` matches the claim's `customerId`

---

## 7. Configuration

### `ClaimsService.Api/appsettings.json`

```json
{
  "Azure": {
    "CosmosDb": {
      "ConnectionString": "<your-cosmos-connection-string>",
      "DatabaseName": "ClaimsDb"
    },
    "BlobStorage": {
      "ConnectionString": "<your-storage-connection-string>",
      "ContainerName": "claims"
    }
  },
  "Jwt": {
    "Secret": "<your-jwt-secret-min-32-chars>",
    "Issuer": "ClaimsService",
    "Audience": "ClaimsService",
    "ExpiryMinutes": 60
  }
}
```

### `ClaimsService.Functions/local.settings.json`

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "<your-storage-connection-string>",
    "CosmosDbConnection": "<your-cosmos-connection-string>",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

---

## 8. Error Handling

- All error responses use RFC 7807 `ProblemDetails` (built into .NET 9 via `AddProblemDetails()`)
- Invalid claim status transitions → `400 Bad Request`
- Claim not found → `404 Not Found`
- Customer accessing another customer's claim → `403 Forbidden`
- Missing/invalid JWT → `401 Unauthorized`

---

## 9. Key Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Event Grid | Real System Topic + push to Function | Production-grade event routing; blob trigger would only poll |
| AI processing | Mock (fixed score, 2s delay) | Shows the event-driven pipeline without requiring a Vision API subscription |
| Photo upload | SAS URL (direct to Blob) | Keeps binary file traffic off the API; industry-standard pattern |
| Adjuster assignment | Manual via API | Keeps scope focused; avoids complex scheduling logic |
| Payout | Status tracking only | Scope is claim lifecycle management, not payment processing |
| Database | Cosmos DB (NoSQL) | Matches Azure tech stack; JSON documents map naturally to claim records |
| Functions runtime | Isolated worker (.NET 9) | Modern approach, matches API's runtime |
| Deployment | Azure App Service (API) + Azure Function App | Both deployed to Azure so Event Grid can reach the Function's HTTPS endpoint |
| Secret management | Azure Key Vault + Managed Identity + Key Vault References | No credentials in code or config; secrets resolved automatically by Azure at runtime |

---

## 10. Azure Resources to Create

Create the following in Azure Portal before wiring up configuration:

### Step 1 — Core Infrastructure
1. **Resource Group** — e.g., `rg-claims-service`
2. **Azure Cosmos DB account** — API: NoSQL; create database `ClaimsDb` with:
   - Container `claims` (partition key: `/customerId`)
   - Container `adjusters` (partition key: `/id`)
3. **Azure Storage Account** — create Blob container named `claims` (private access)
4. **Azure Key Vault** — e.g., `kv-claims-service`; store the following secrets:
   - `CosmosDbConnection` — Cosmos DB connection string
   - `BlobStorageConnection` — Storage Account connection string
   - `JwtSecret` — JWT signing secret (min 32 chars)

### Step 2 — Compute
5. **Azure App Service Plan** — e.g., B1 (Basic), Linux or Windows
6. **Azure App Service (Web API)** — .NET 9, linked to App Service Plan
   - Enable **System-assigned Managed Identity**
   - Grant Managed Identity **Key Vault Secrets User** role on `kv-claims-service`
7. **Azure Function App** — runtime: .NET 9 isolated, linked to the Storage Account
   - Enable **System-assigned Managed Identity**
   - Grant Managed Identity **Key Vault Secrets User** role on `kv-claims-service`

### Step 3 — App Settings (Key Vault References)
Set App Service and Function App Application Settings using Key Vault References — Azure resolves these automatically at runtime with no code changes:

```
# App Service settings
Azure__CosmosDb__ConnectionString  = @Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=CosmosDbConnection)
Azure__BlobStorage__ConnectionString = @Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=BlobStorageConnection)
Jwt__Secret                        = @Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=JwtSecret)
Azure__CosmosDb__DatabaseName      = ClaimsDb
Azure__BlobStorage__ContainerName  = claims
Jwt__Issuer                        = ClaimsService
Jwt__Audience                      = ClaimsService
Jwt__ExpiryMinutes                 = 60

# Function App settings
CosmosDbConnection    = @Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=CosmosDbConnection)
CosmosDbDatabaseName  = ClaimsDb
AzureWebJobsStorage   = @Microsoft.KeyVault(VaultName=kv-claims-service;SecretName=BlobStorageConnection)
```

### Step 4 — Event Grid (after Function App is deployed)
8. **Event Grid System Topic** — created on the Storage Account (type: `Microsoft.Storage.StorageAccounts`)
9. **Event Grid Subscription** — on the System Topic:
   - Filter event type: `Microsoft.Storage.BlobCreated`
   - Filter subject begins with: `/blobServices/default/containers/claims/`
   - Endpoint type: Azure Function
   - Endpoint: `ClaimProcessingFunction` in your Function App

---

## 11. Deployment

### Web API — Azure App Service

```bash
# Publish
dotnet publish ClaimsService.Api -c Release -o ./publish/api

# Deploy via Azure CLI
az webapp deploy --resource-group rg-claims-service \
  --name <app-service-name> \
  --src-path ./publish/api
```

Set App Settings in Azure Portal (or via CLI) to match `appsettings.json` values:
- `Azure__CosmosDb__ConnectionString`
- `Azure__BlobStorage__ConnectionString`
- `Jwt__Secret`

### Function App — Azure Functions

```bash
# Publish
dotnet publish ClaimsService.Functions -c Release -o ./publish/functions

# Deploy via Azure Functions Core Tools
func azure functionapp publish <function-app-name> --dotnet-isolated
```

Set Application Settings in Azure Portal:
- `AzureWebJobsStorage`
- `CosmosDbConnection`

### Deployment Order

```
1. Create Key Vault → store CosmosDbConnection, BlobStorageConnection, JwtSecret
2. Deploy Web API → App Service → enable Managed Identity → grant Key Vault Secrets User
3. Deploy Function App → enable Managed Identity → grant Key Vault Secrets User
4. Set App Service + Function App settings using Key Vault References
5. Create Event Grid System Topic on Storage Account
6. Create Event Grid Subscription pointing to ClaimProcessingFunction
7. Seed adjusters data via API (POST /api/claims/fnol triggers seeding on startup)
```
