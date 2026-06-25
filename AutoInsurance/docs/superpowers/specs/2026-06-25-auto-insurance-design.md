# Auto Insurance Platform — Architecture & Design Spec

**Date**: 2026-06-25  
**Status**: Approved  
**Scope**: Quote & Buy Application + Customer Service Portal

---

## Table of Contents

1. [Overview](#1-overview)
2. [Decisions Log](#2-decisions-log)
3. [System Context](#3-system-context)
4. [Frontend Architecture](#4-frontend-architecture)
5. [Backend Services](#5-backend-services)
6. [Data Model](#6-data-model)
7. [Key User Flows](#7-key-user-flows)
8. [Deployment & Infrastructure](#8-deployment--infrastructure)
9. [Cross-Cutting Concerns](#9-cross-cutting-concerns)

---

## 1. Overview

A full-stack auto insurance platform consisting of two user-facing applications and five backend microservices:

| Application | Purpose |
|---|---|
| **Quote & Buy App** | Anonymous multi-step wizard to quote, bind, and pay for an auto insurance policy |
| **Customer Service Portal (CSP)** | Authenticated portal for policyholders to manage their policy, payments, documents, and claims |

**Core stack**:
- Frontend: React 18 + Redux Toolkit (both apps)
- Backend: .NET 10 Web APIs with Clean Architecture
- Database: SQL Server (single shared database)
- Identity: Azure AD B2C (dual-mode: mock for local dev, real B2C for cloud)
- Cloud target: Azure (Container Apps, Azure SQL, Azure Blob, Azure Communication Services)
- Initial deployment: Docker Compose

---

## 2. Decisions Log

| Decision | Choice | Rationale |
|---|---|---|
| API architecture | Ocelot API Gateway + 5 domain APIs | Single entry point, JWT validated once, routes by path prefix, portable to Azure APIM |
| Cloud | Azure | Aligns with existing portfolio (Cosmos DB, Event Grid, Blob) |
| Identity | Azure AD B2C + mock mode | Managed identity; mock mode enables offline local development |
| Payment | Mock `IPaymentProvider` abstraction | Real provider (Stripe) swappable via single DI binding |
| Document generation | QuestPDF → Azure Blob + Azure Communication Services email | PDF + in-portal download + email at key events |
| Premium rating | Flat mock rates in `CoverageTypes` table | Extensible to rule-based or external rating engine later |
| Redux persistence | `redux-persist` (AES-256 encrypted) + server-side auto-save | Client cache + server source of truth; PII never in plaintext in browser |

---

## 3. System Context

### Actors

| Actor | Description |
|---|---|
| **Prospect** | Anonymous user completing a quote; no login required |
| **Policyholder** | Prospect who completed payment and created a CSP account |
| **System** | Automated events: payment confirmation, document generation, renewal reminders |

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│  Browser                                                        │
│  ┌──────────────────────┐   ┌──────────────────────────────┐   │
│  │  Quote & Buy App     │   │  Customer Service Portal     │   │
│  │  React 18 + Redux    │   │  React 18 + Redux            │   │
│  │  localhost:3000      │   │  localhost:3001              │   │
│  └──────────┬───────────┘   └───────────────┬──────────────┘   │
└─────────────┼─────────────────────────────────┼────────────────┘
              │  HTTPS (single base URL)         │
              ▼                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│  Ocelot API Gateway  :8080                                      │
│  • JWT validation (Azure AD B2C or mock)                        │
│  • Path-based routing                                           │
│  • Correlation ID injection, request logging                    │
└────┬──────────┬──────────┬──────────┬──────────┬───────────────┘
     │          │          │          │          │
     ▼          ▼          ▼          ▼          ▼
  Quote &   Customer   Payment   Document    Claims
  Buy API   Svc API    API       Gen API     API
  :5001     :5002      :5003     :5004       :5005
     │          │          │          │          │
     └──────────┴──────────┴──────────┴──────────┘
                          │
              ┌───────────▼────────────┐
              │  SQL Server            │
              │  (single shared DB)    │
              └────────────────────────┘

  ┌───────────────────────────────────────────┐
  │  Azure Services (after Docker phase)      │
  │  • Azure AD B2C       (identity)          │
  │  • Azure Blob Storage (documents, claims) │
  │  • Azure Comm Svc     (email)             │
  │  • Azurite            (local Blob emul.)  │
  └───────────────────────────────────────────┘
```

### Repository Structure

```
AutoInsurance/
├── frontend/
│   ├── quote-buy-app/              # React 18 + Redux Toolkit
│   └── customer-service-app/       # React 18 + Redux Toolkit
├── backend/
│   ├── AutoInsurance.Gateway/      # Ocelot API Gateway
│   ├── AutoInsurance.QuoteBuy/     # Quote & Buy API
│   ├── AutoInsurance.CustomerService/
│   ├── AutoInsurance.Payment/
│   ├── AutoInsurance.DocumentGeneration/
│   └── AutoInsurance.Claims/
├── database/
│   └── migrations/                 # Shared EF Core migrations
├── docker-compose.yml
└── docs/
    └── superpowers/specs/
```

---

## 4. Frontend Architecture

### Applications

| | Quote & Buy App | Customer Service Portal |
|---|---|---|
| Port | 3000 | 3001 |
| Auth | Anonymous → B2C signup after payment | B2C login required on entry |
| State | Quote wizard state + robust persistence | Policy/claim data loaded from API |

---

### Quote & Buy App — Routes

| Route | Component | Notes |
|---|---|---|
| `/` | `PersonalInfoPage` | Step 1 — creates Quote record on Next |
| `/quote/drivers` | `DriversPage` | Step 2 — Primary driver required |
| `/quote/vehicles` | `VehiclesPage` | Step 3 |
| `/quote/coverages` | `CoveragesPage` | Step 4 — triggers premium calculation |
| `/quote/review` | `QuoteReviewPage` | Step 5 — full summary + bind action |
| `/quote/payment` | `PaymentPage` | Step 6 — mock payment form |
| `/quote/confirmation` | `ConfirmationPage` | Post-payment — prompts account creation |
| `/resume` | `QuoteResumePage` | Quote# + ZIP code resume entry |

---

### Redux State Shape — Quote & Buy

```typescript
{
  quote: {
    quoteId: string | null,
    quoteNumber: string | null,
    sessionToken: string | null,
    currentStep: 1 | 2 | 3 | 4 | 5 | 6,
    isDirty: boolean,
    lastSyncedAt: string | null,            // ISO timestamp

    personalInfo: {
      firstName: string,
      lastName: string,
      dateOfBirth: string,
      email: string,
      phone: string,
      address: {
        street: string,
        city: string,
        state: string,
        zip: string
      }
    },

    drivers: Array<{
      id: string,
      driverType: 'Primary' | 'Secondary' | 'Occasional',
      firstName: string,
      lastName: string,
      dateOfBirth: string,
      licenseNumber: string,
      licenseState: string
    }>,

    vehicles: Array<{
      id: string,
      year: number,
      make: string,
      model: string,
      vin: string,
      primaryUse: 'Commute' | 'Pleasure' | 'Business'
    }>,

    coverages: Array<{
      coverageTypeId: number,
      code: string,
      limitOption: string,
      deductible: number,
      annualPremium: number
    }>,

    premium: {
      totalAnnual: number,
      totalMonthly: number,
      breakdown: Array<{ code: string, amount: number }>
    } | null,

    bindQuoteId: string | null
  },

  ui: {
    stepStatus: Record<1 | 2 | 3 | 4 | 5 | 6, 'untouched' | 'valid' | 'invalid'>,
    loading: boolean,
    error: string | null
  }
}
```

---

### Redux Persistence Layer — Dual-Mode Strategy

Quote state is persisted in two layers simultaneously to guarantee no data loss:

```
On every step navigation (Next / Back):
  1. saveStepThunk() dispatched
     ├─ PATCH /api/quote/{id}/draft  → server auto-save (non-blocking)
     └─ redux-persist writes quote slice to localStorage
        encrypted with AES-256
        key = SHA256(quoteId + zipCode)  ← derived client-side at runtime

On app load / tab reopen:
  1. redux-persist rehydrates from encrypted localStorage
  2. If quoteId present → GET /api/quote/{id}/draft to verify still valid
  3. Server state wins on conflict (source of truth)
  4. If server returns 404/410 → clear localStorage, show resume page

Security properties:
  • PII never stored in plaintext in the browser
  • localStorage unreadable without quoteId + ZIP (not stored in localStorage)
  • SessionToken stored server-side as SHA256 hash only
  • 24-hour expiry enforced server-side
```

**Library**: `redux-persist` + `redux-persist-transform-encrypt`

---

### Customer Service Portal — Routes

| Route | Component |
|---|---|
| `/login` | Redirects to Azure AD B2C |
| `/dashboard` | `DashboardPage` |
| `/policies` | `PoliciesPage` |
| `/policies/:id/card` | `InsuranceCardPage` — view + download PDF |
| `/policies/:id/payments` | `PaymentsPage` — pay + billing schedule |
| `/policies/:id/renew` | `RenewalPage` |
| `/policies/:id/coverages` | `ChangeCoveragePage` |
| `/claims/new` | `NewClaimPage` — FNOL + photo upload |
| `/claims/:id` | `ClaimDetailPage` |

CSP Redux state is standard async thunks — no complex persistence needed (all data lives on server).

---

## 5. Backend Services

### Clean Architecture Structure (all APIs)

```
AutoInsurance.{ServiceName}/
├── API/                          # Controllers, Middleware, Program.cs, DI setup
├── Application/                  # Commands, Queries, DTOs, Interfaces (MediatR)
│   ├── Commands/
│   ├── Queries/
│   └── Interfaces/               # IRepository<T>, IUnitOfWork, IPaymentProvider, etc.
├── Domain/                       # Entities, Value Objects, Domain Events
│   └── Entities/
├── Infrastructure/               # EF Core, Repositories, external service clients
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── Repositories/
│   │   └── UnitOfWork.cs
│   └── Services/                 # BlobService, EmailService, MockPaymentProvider
└── Tests/
    ├── Unit/
    └── Integration/
```

**Shared patterns across all APIs**:
- **Repository pattern**: `IRepository<T>` — `GetById`, `GetAll`, `Add`, `Update`, `Delete`
- **Unit of Work**: `IUnitOfWork` wraps `AppDbContext.SaveChangesAsync()` — all writes go through it
- **MediatR**: Commands and Queries dispatched via MediatR; controllers are thin dispatch points only
- **FluentValidation**: All command/query inputs validated in Application layer before reaching domain
- **Result pattern**: `Result<T>` return type from Application layer — no exceptions for business rule failures

---

### 1. Quote & Buy API (:5001)

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/quote` | Create quote, assign QuoteId + QuoteNumber |
| PATCH | `/api/quote/{id}/drivers` | Save drivers step |
| PATCH | `/api/quote/{id}/vehicles` | Save vehicles step |
| PATCH | `/api/quote/{id}/coverages` | Save coverages, calculate mock premium |
| GET | `/api/quote/{id}/review` | Return full quote + premium breakdown |
| POST | `/api/quote/{id}/bind` | Bind quote → create Policy record |
| POST | `/api/quote/resume` | Validate QuoteNumber + ZIP → return draft state |
| PATCH | `/api/quote/{id}/draft` | Auto-save full draft JSON (non-blocking) |

**Domain entities**: `Quote`, `Driver`, `Vehicle`, `QuoteCoverage`, `QuoteDraft`  
**Auth**: `POST /api/quote` and `POST /api/quote/resume` are anonymous. All other endpoints require valid `sessionToken` header.

---

### 2. Customer Service API (:5002)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/policies` | List policies for authenticated user |
| GET | `/api/policies/{id}` | Policy detail |
| GET | `/api/policies/{id}/documents` | List documents |
| PUT | `/api/policies/{id}/coverages` | Request coverage change (creates Endorsement) |
| POST | `/api/policies/{id}/renew` | Trigger renewal |
| GET | `/api/account` | Account profile |
| POST | `/api/account/link` | Link B2C objectId to policy after payment |

**Domain entities**: `Policy`, `PolicyDriver`, `PolicyVehicle`, `PolicyCoverage`, `Endorsement`, `RenewalRequest`, `UserAccount`

---

### 3. Payment API (:5003)

Implements `IPaymentProvider` abstraction — `MockPaymentProvider` wired by default, replaceable via DI.

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/payments/initiate` | Start payment → returns mock paymentIntentId |
| POST | `/api/payments/confirm` | Confirm → activates Policy + triggers document generation |
| GET | `/api/payments/{policyId}/history` | Payment history |
| POST | `/api/payments/{policyId}/schedule` | Set billing frequency (Monthly/Quarterly/Yearly) |

**Mock provider**: `/confirm` always succeeds with generated `transactionId`. Configurable failure mode via `appsettings` for testing.  
**Domain entities**: `PaymentTransaction`, `BillingSchedule`

---

### 4. Document Generation API (:5004)

Triggered internally (by Payment API on confirm) and by CSP download requests.

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/documents/generate` | Generate PDF (InsuranceCard or DeclarationPage) |
| GET | `/api/documents/{policyId}` | List documents for policy |
| GET | `/api/documents/{id}/download` | Return time-limited Azure Blob SAS URL |

**Flow**: QuestPDF renders template → upload to Azure Blob (or Azurite locally) → store `Documents` record → Azure Communication Services sends email with download link.  
**Domain entities**: `Document` (id, policyId, type, blobUrl, generatedAt)

---

### 5. Claims API (:5005)

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/claims` | Submit FNOL |
| GET | `/api/claims/{policyId}` | List claims for policy |
| GET | `/api/claims/{id}` | Claim detail + status |
| POST | `/api/claims/{id}/documents` | Upload incident/damage photos → Azure Blob |
| GET | `/api/claims/{id}/documents` | List uploaded claim documents |

**Domain entities**: `Claim`, `ClaimDocument`

---

### 6. Ocelot Gateway (:8080)

Routes by path prefix, strips the upstream prefix before forwarding to downstream APIs, validates Azure AD B2C JWT (or accepts mock claims in dev):

```json
{
  "Routes": [
    { "UpstreamPathTemplate": "/api/quote/{everything}",     "DownstreamPathTemplate": "/api/quote/{everything}",     "DownstreamPort": 5001 },
    { "UpstreamPathTemplate": "/api/csp/{everything}",       "DownstreamPathTemplate": "/api/{everything}",           "DownstreamPort": 5002 },
    { "UpstreamPathTemplate": "/api/payments/{everything}",  "DownstreamPathTemplate": "/api/payments/{everything}",  "DownstreamPort": 5003 },
    { "UpstreamPathTemplate": "/api/documents/{everything}", "DownstreamPathTemplate": "/api/documents/{everything}", "DownstreamPort": 5004 },
    { "UpstreamPathTemplate": "/api/claims/{everything}",    "DownstreamPathTemplate": "/api/claims/{everything}",    "DownstreamPort": 5005 }
  ]
}
```

The `/api/csp/` prefix is gateway-only routing. The Customer Service API internally uses `/api/policies` and `/api/account` — the gateway strips the `csp` segment before forwarding.

**Anonymous routes** (no JWT required): `POST /api/quote`, `POST /api/quote/resume`.  
`POST /api/csp/account/link` requires the B2C JWT issued immediately after signup.  
All other routes require a valid Bearer token.

---

## 6. Data Model

Single SQL Server database. Each API accesses only its own tables via its own `AppDbContext`.

### Schema

#### Quote & Buy Tables

```sql
Quotes
  Id                  UNIQUEIDENTIFIER  PK
  QuoteNumber         VARCHAR(20)       UNIQUE  -- QT-2026-001234
  Status              VARCHAR(20)       -- Draft | Review | Bound | Expired
  ZipCode             VARCHAR(10)
  SessionTokenHash    VARCHAR(64)       -- SHA256 of (quoteNumber + zipCode)
  SessionTokenExpiry  DATETIME
  CreatedAt           DATETIME
  UpdatedAt           DATETIME

QuoteDrafts
  QuoteId             UNIQUEIDENTIFIER  FK → Quotes.Id
  StepReached         INT               -- 1–6
  DraftStateJson      NVARCHAR(MAX)     -- full Redux quote slice serialized as JSON
  UpdatedAt           DATETIME

Drivers
  Id                  UNIQUEIDENTIFIER  PK
  QuoteId             UNIQUEIDENTIFIER  FK → Quotes.Id
  DriverType          VARCHAR(20)       -- Primary | Secondary | Occasional
  FirstName           VARCHAR(100)
  LastName            VARCHAR(100)
  DateOfBirth         DATE
  LicenseNumber       VARCHAR(50)
  LicenseState        VARCHAR(2)

Vehicles
  Id                  UNIQUEIDENTIFIER  PK
  QuoteId             UNIQUEIDENTIFIER  FK → Quotes.Id
  Year                INT
  Make                VARCHAR(50)
  Model               VARCHAR(50)
  VIN                 VARCHAR(17)
  PrimaryUse          VARCHAR(20)       -- Commute | Pleasure | Business

CoverageTypes                           -- lookup / mock rate table
  Id                  INT               PK
  Code                VARCHAR(30)       -- BODILY_INJURY | PROPERTY_DAMAGE | COMPREHENSIVE | COLLISION | UNINSURED
  Description         VARCHAR(100)
  MockAnnualRate      DECIMAL(10,2)

QuoteCoverages
  QuoteId             UNIQUEIDENTIFIER  FK → Quotes.Id
  CoverageTypeId      INT               FK → CoverageTypes.Id
  LimitOption         VARCHAR(50)       -- e.g. "100/300" for BI
  Deductible          DECIMAL(10,2)
  AnnualPremium       DECIMAL(10,2)
```

#### Policy Tables

```sql
Policies
  Id                  UNIQUEIDENTIFIER  PK
  QuoteId             UNIQUEIDENTIFIER  FK → Quotes.Id
  PolicyNumber        VARCHAR(20)       UNIQUE  -- PL-2026-001234
  Status              VARCHAR(20)       -- Active | Cancelled | Expired | PendingRenewal
  EffectiveDate       DATE
  ExpirationDate      DATE
  TotalAnnualPremium  DECIMAL(10,2)
  CreatedAt           DATETIME

PolicyDrivers                           -- copied from Drivers at bind time
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  DriverType          VARCHAR(20)
  FirstName, LastName, DateOfBirth, LicenseNumber, LicenseState

PolicyVehicles                          -- copied from Vehicles at bind time
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  Year, Make, Model, VIN, PrimaryUse

PolicyCoverages                         -- copied from QuoteCoverages at bind time
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  CoverageTypeId      INT               FK → CoverageTypes.Id
  LimitOption, Deductible, AnnualPremium

Endorsements
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  Type                VARCHAR(30)       -- CoverageChange | VehicleAdd | DriverAdd
  RequestedAt         DATETIME
  EffectiveDate       DATE
  Status              VARCHAR(20)       -- Pending | Applied
  ChangeJson          NVARCHAR(MAX)

RenewalRequests
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  RequestedAt         DATETIME
  NewEffectiveDate    DATE
  Status              VARCHAR(20)       -- Pending | Confirmed | Declined

UserAccounts
  Id                  UNIQUEIDENTIFIER  PK
  B2CObjectId         VARCHAR(100)      UNIQUE  -- Azure AD B2C sub claim
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  Email               VARCHAR(200)
  CreatedAt           DATETIME
```

#### Payment, Document & Claims Tables

```sql
PaymentTransactions
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  Amount              DECIMAL(10,2)
  TransactionRef      VARCHAR(100)      -- mock provider reference
  Status              VARCHAR(20)       -- Pending | Success | Failed
  PaidAt              DATETIME

BillingSchedules
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id  UNIQUE
  Frequency           VARCHAR(20)       -- Monthly | Quarterly | Yearly
  NextDueDate         DATE
  UpdatedAt           DATETIME

Documents
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  Type                VARCHAR(30)       -- InsuranceCard | DeclarationPage
  BlobUrl             VARCHAR(500)
  GeneratedAt         DATETIME

Claims
  Id                  UNIQUEIDENTIFIER  PK
  PolicyId            UNIQUEIDENTIFIER  FK → Policies.Id
  IncidentDate        DATE
  Description         NVARCHAR(1000)
  Status              VARCHAR(20)       -- Submitted | UnderReview | Approved | Denied | Closed
  CreatedAt           DATETIME

ClaimDocuments
  Id                  UNIQUEIDENTIFIER  PK
  ClaimId             UNIQUEIDENTIFIER  FK → Claims.Id
  Type                VARCHAR(30)       -- IncidentPhoto | DamagePhoto | Other
  BlobUrl             VARCHAR(500)
  UploadedAt          DATETIME
```

---

## 7. Key User Flows

### Flow 1 — Quote Wizard (Happy Path)

```
Step 1 — Personal Info
  → POST /api/quote → returns quoteId, quoteNumber
  → Redux stores quoteId; sessionToken derived; PATCH /api/quote/{id}/draft

Step 2 — Drivers
  → PATCH /api/quote/{id}/drivers + /draft

Step 3 — Vehicles
  → PATCH /api/quote/{id}/vehicles + /draft

Step 4 — Coverages
  → PATCH /api/quote/{id}/coverages
  → API sums MockAnnualRate × selections → returns PremiumBreakdown
  → Redux stores premium

Step 5 — Review & Bind
  → POST /api/quote/{id}/bind
  → Creates Policy record, Quote.Status = Bound
  → Returns bindQuoteId

Step 6 — Payment
  → POST /api/payments/initiate
  → POST /api/payments/confirm
  → Payment API: Policy.Status = Active
                 → POST /api/documents/generate (insurance card)
                 → ACS sends email with card PDF
  → Navigate to /quote/confirmation
```

---

### Flow 2 — Quote Resume

```
User visits /resume, enters QuoteNumber + ZipCode
  → POST /api/quote/resume { quoteNumber, zipCode }
  → API: SHA256(quoteNumber + zipCode) matches SessionTokenHash
         SessionTokenExpiry > now
  → Returns { quoteId, sessionToken, draftStateJson, stepReached }
  → Frontend: hydrateQuote(draftState) dispatched
              redux-persist re-encrypts to localStorage
              Navigate to stepReached route

Expiry handling:
  → 410 Gone if SessionTokenExpiry passed
  → Frontend shows "Your quote has expired — start a new one"
```

---

### Flow 3 — Account Creation After Payment

```
Confirmation page:
  "Your policy PL-2026-001234 is active."
  [Create Account] → Redirect to Azure AD B2C sign-up
                     (email pre-filled from personalInfo)
  → B2C: user sets password, completes signup, returns JWT
  → POST /api/csp/account/link { b2cObjectId, policyId, email }
  → UserAccounts record created
  → Redirect to CSP /dashboard
```

---

### Flow 4 — CSP Login

```
User visits CSP app → not authenticated
  → Redirect to Azure AD B2C login
  → B2C returns JWT → stored in Redux auth slice
  → GET /api/csp/account → load profile + policyId
  → GET /api/policies → load policy list
  → Render /dashboard
```

---

### Flow 5 — Claim Submission

```
User navigates to /claims/new
  → fills FNOL (incident date, description, vehicles involved)
  → POST /api/claims → returns claimId, Claim.Status = Submitted

  → uploads photos (multipart form)
  → POST /api/claims/{id}/documents (one request per file)
  → Claims API: streams to Azure Blob (private container)
                stores ClaimDocuments record with BlobUrl

  → Confirmation shows claim number + Submitted status
```

---

### Flow 6 — Payment Scheduling (CSP)

```
User navigates to /policies/{id}/payments
  → Sees current Frequency + NextDueDate
  → Selects new frequency
  → POST /api/payments/{policyId}/schedule { frequency }
  → BillingSchedule updated, NextDueDate recalculated
```

---

## 8. Deployment & Infrastructure

### Phase 1 — Docker Compose

All services as Docker containers, single `docker-compose.yml`:

```
gateway           :8080   Ocelot
quote-buy-api     :5001   .NET 10
customer-svc-api  :5002   .NET 10
payment-api       :5003   .NET 10
document-api      :5004   .NET 10
claims-api        :5005   .NET 10
quote-buy-app     :3000   React (nginx)
customer-svc-app  :3001   React (nginx)
sqlserver         :1433   SQL Server 2022
azurite           :10000  Azure Blob emulator
```

**Startup order**: `sqlserver` health-check passes → all APIs start (EF migrations run on startup) → gateway → React apps.

**Single command**: `docker-compose up --build`

#### Docker Image Strategy

```
.NET APIs:   mcr.microsoft.com/dotnet/aspnet:10.0 (runtime)
             mcr.microsoft.com/dotnet/sdk:10.0 (build stage)
             Multi-stage build → ~100MB final image

React apps:  node:20-alpine (build stage: npm run build)
             nginx:alpine (serve /dist)
             ~25MB final image
```

---

### Phase 2 — Azure Deployment

```
React apps        → Azure Static Web Apps
Ocelot Gateway    → Azure Container Apps
5 .NET APIs       → Azure Container Apps (one app per service)
SQL Server        → Azure SQL Database
Blob Storage      → Azure Blob Storage (replaces Azurite)
Email             → Azure Communication Services
Identity          → Azure AD B2C
Container images  → Azure Container Registry
```

**Azure Container Apps** is preferred over AKS — fully managed, scales to zero, no Kubernetes cluster to maintain.

---

### Environment Configuration

| Config | Docker Local | Azure |
|---|---|---|
| `Auth__Mode` | `mock` | `b2c` |
| `BlobStorage__Endpoint` | `http://azurite:10000` | Azure Blob connection string |
| `Email__Mode` | `log` (stdout) | `send` (ACS) |
| `Payment__Mode` | `mock` | `mock` (until real provider) |
| `ConnectionStrings__Default` | Docker SQL Server | Azure SQL |

All config injected via environment variables — zero code changes between phases.

---

### CI/CD (GitHub Actions)

```
Push to main
  → dotnet build + dotnet test (all 5 APIs)
  → npm run build (both React apps)
  → Docker build + push to Azure Container Registry
  → az containerapp update (deploy to Azure Container Apps)
```

---

## 9. Cross-Cutting Concerns

### Authentication — Dual Mode

| Mode | When | Behavior |
|---|---|---|
| `mock` | Local Docker dev | Gateway middleware injects hardcoded `ClaimsPrincipal`; skips JWT validation |
| `b2c` | Integration testing + Azure | Gateway validates JWT against Azure AD B2C JWKS endpoint |

Toggle: `AUTH_MODE=mock|b2c` environment variable. Mock mode is clearly flagged dev-only and blocked in production build via `ASPNETCORE_ENVIRONMENT` check.

**B2C with local Docker**: B2C is cloud-hosted; browser redirects to `login.microsoftonline.com`. Docker containers only validate the returned JWT — one outbound HTTPS call to Microsoft's JWKS endpoint on startup. No B2C container needed locally.

---

### Error Handling

- Application layer returns `Result<T>` (never throws for business rule failures)
- Controllers map `Result<T>` to appropriate HTTP status codes
- Global exception middleware in each API catches unhandled exceptions → logs + returns `500` with correlation ID
- Gateway adds `X-Correlation-Id` header to every request for distributed tracing

---

### Logging

- Structured logging via `Microsoft.Extensions.Logging` + Serilog
- Local: console sink (Docker logs)
- Azure: Application Insights sink
- All requests logged at Gateway with correlation ID, downstream service, and response time

---

### Security

- JWT validated at gateway only — downstream APIs trust gateway-forwarded claims headers
- PII in Redux localStorage encrypted with AES-256; key derived from `quoteId + zipCode`
- Azure Blob documents served via time-limited SAS URLs (not public)
- Claim photo uploads stored in private Blob container; access only via SAS URL
- `SessionTokenHash` stored as SHA256 — never the raw token

---

### Testing Strategy

| Layer | Type | Tool |
|---|---|---|
| Domain + Application | Unit tests | xUnit + Moq |
| Repositories | Integration tests | EF Core InMemory or TestContainers |
| API endpoints | Integration tests | `WebApplicationFactory<T>` |
| React components | Unit tests | Vitest + React Testing Library |
| Redux slices/thunks | Unit tests | Vitest |

---

*Spec approved 2026-06-25. Next step: implementation plan via writing-plans.*
