# Auto Insurance Platform — Plan 2: Quote & Buy API

**Goal:** Fully implement the Quote & Buy API (`AutoInsurance.QuoteBuy`) with clean architecture — MediatR commands/queries, repository + unit of work, FluentValidation, and the QuoteController wiring all 8 endpoints. Deliver a test project with unit tests for all command/query handlers.

**Base:** Plan 1 foundation is complete. `AutoInsurance.QuoteBuy` is scaffolded with `QuoteBuyDbContext` and a `/health` endpoint.

**Tech Stack:** .NET 10, MediatR 12, FluentValidation 11, xUnit, FluentAssertions, Moq

---

## File Map (additions/changes to `AutoInsurance.QuoteBuy/`)

```
AutoInsurance.QuoteBuy/
├── Application/
│   ├── Commands/
│   │   ├── CreateQuote/
│   │   │   ├── CreateQuoteCommand.cs
│   │   │   ├── CreateQuoteCommandHandler.cs
│   │   │   └── CreateQuoteCommandValidator.cs
│   │   ├── SaveDrivers/
│   │   │   ├── SaveDriversCommand.cs
│   │   │   └── SaveDriversCommandHandler.cs
│   │   ├── SaveVehicles/
│   │   │   ├── SaveVehiclesCommand.cs
│   │   │   └── SaveVehiclesCommandHandler.cs
│   │   ├── SaveCoverages/
│   │   │   ├── SaveCoveragesCommand.cs
│   │   │   └── SaveCoveragesCommandHandler.cs
│   │   ├── BindQuote/
│   │   │   ├── BindQuoteCommand.cs
│   │   │   └── BindQuoteCommandHandler.cs
│   │   └── AutoSaveDraft/
│   │       ├── AutoSaveDraftCommand.cs
│   │       └── AutoSaveDraftCommandHandler.cs
│   ├── Queries/
│   │   ├── GetQuoteReview/
│   │   │   ├── GetQuoteReviewQuery.cs
│   │   │   └── GetQuoteReviewQueryHandler.cs
│   │   └── ResumeQuote/
│   │       ├── ResumeQuoteQuery.cs
│   │       └── ResumeQuoteQueryHandler.cs
│   └── DTOs/
│       ├── QuoteDto.cs
│       ├── DriverDto.cs
│       ├── VehicleDto.cs
│       └── CoverageDto.cs
├── Controllers/
│   └── QuoteController.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── QuoteBuyDbContext.cs        (update — add Policy tables for bind)
│   │   ├── Repositories/
│   │   │   └── QuoteRepository.cs
│   │   └── UnitOfWork.cs
│   └── Services/
│       └── QuoteNumberGenerator.cs
└── Program.cs                          (update — register MediatR, validators, repos)

AutoInsurance.QuoteBuy.Tests/
├── AutoInsurance.QuoteBuy.Tests.csproj
├── Commands/
│   ├── CreateQuoteCommandHandlerTests.cs
│   ├── SaveDriversCommandHandlerTests.cs
│   ├── SaveCoveragesCommandHandlerTests.cs
│   └── BindQuoteCommandHandlerTests.cs
└── Queries/
    ├── GetQuoteReviewQueryHandlerTests.cs
    └── ResumeQuoteQueryHandlerTests.cs
```

---

## Global Constraints

- Controllers are thin: validate → dispatch MediatR → map to HTTP response
- All handlers return `Result<T>` — no exceptions for business failures
- `IUnitOfWork.SaveChangesAsync()` called once per command handler
- SessionToken = SHA256(quoteNumber + zipCode), stored as hash, 24-hour expiry
- Mock premium: `annualPremium = coverageType.MockAnnualRate` (flat, per coverage)
- Bind quote: creates Policy + PolicyDriver/Vehicle/Coverage records inside QuoteBuyDbContext (same DB)
- No test runs until all code is generated; run once at the end

---

## Task 1: Packages + Repository + Unit of Work

**Packages to add to `AutoInsurance.QuoteBuy`:**
- `MediatR` 12.*
- `FluentValidation.AspNetCore` 11.*
- `Microsoft.AspNetCore.Diagnostics.HealthChecks` (already via EF check)

**Files:**
- Create: `Infrastructure/Persistence/Repositories/QuoteRepository.cs`
- Create: `Infrastructure/Persistence/UnitOfWork.cs`
- Create: `Infrastructure/Services/QuoteNumberGenerator.cs`
- Update: `QuoteBuyDbContext.cs` — add Policy tables
- Update: `Program.cs` — register everything

---

## Task 2: CreateQuote Command

### POST /api/quote
**Flow:** PersonalInfo form submitted → create `Quote` + `QuoteDraft` (stores step 1 JSON) → return `quoteId`, `quoteNumber`, `zipCode`.

**SessionToken:** `SHA256(quoteNumber + zipCode)` computed server-side and stored as hash. Client uses `quoteId + zipCode` as AES-256 key for localStorage.

---

## Task 3: SaveDrivers / SaveVehicles / SaveCoverages Commands

### PATCH /api/quote/{id}/drivers
Replace all drivers for the quote (delete existing, insert new).

### PATCH /api/quote/{id}/vehicles
Replace all vehicles for the quote.

### PATCH /api/quote/{id}/coverages
Replace coverages; compute `annualPremium = coverageType.MockAnnualRate`; advance quote status to `Review`.

---

## Task 4: Queries + BindQuote Command

### GET /api/quote/{id}/review
Returns full quote data + premium total.

### POST /api/quote/resume
Validate QuoteNumber + ZIP → recompute `SHA256(quoteNumber + zipCode)` → match against stored hash + expiry → return draft state JSON.

### POST /api/quote/{id}/bind
Validate status = `Review` → create `Policy` + child records → set `Quote.Status = Bound` → return `policyId`.

---

## Task 5: AutoSaveDraft + Controller + Program.cs

### PATCH /api/quote/{id}/draft
Non-blocking: upsert `QuoteDraft.DraftStateJson`. Always returns 204.

### QuoteController
Thin controller: extract from HttpContext, dispatch to MediatR, return appropriate status codes.

---

## Task 6: Test Project + Unit Tests + Run + Commit

Create `AutoInsurance.QuoteBuy.Tests` xUnit project. Write unit tests for all handlers (mocking `IQuoteRepository` and `IUnitOfWork` via Moq). Run all tests. Commit.
