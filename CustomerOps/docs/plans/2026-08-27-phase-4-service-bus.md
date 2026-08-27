# Service Bus Emulator (Async Operation Submission) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Naming note:** This is the 4th plan doc in this project's sequential numbering (`phase-1-foundation`, `phase-2-customer-apis`, `phase-3-redis`, this one). `docs/architecture.md` §8 calls this same body of work "Phase 3 — Service Bus Emulator" because its numbering starts counting after folding foundation+CRUD into one phase. The two numbering schemes diverged after Phase 1; this file's name follows the project's actual chronological plan-doc sequence, not architecture.md's phase labels.

**Goal:** Add `POST /api/v1/operations` (create + publish to a Service Bus emulator queue, return `202 Accepted`) and `GET /api/v1/operations/{id}` (poll status), plus a minimal in-process consumer that proves the full `API → Service Bus emulator → Consumer → SQL` pipe works end-to-end by flipping a submitted operation to `Processing`.

**Architecture:** Mirrors the existing Customer vertical slice exactly (Domain entity → Application service behind an interface → EF Core repository → controller action) with one new piece: an `IOperationPublisher` abstraction wrapping `Azure.Messaging.ServiceBus`, and a `BackgroundService` consumer (`OperationProcessor`) hosted inside the existing API process — no separate worker project this phase. Simulated progress (0/25/50/75/100%), retries, dead-lettering, and `Idempotency-Key` handling are explicitly deferred to later phases (Background Worker / Testing+Resiliency).

**Tech Stack:** ASP.NET Core (.NET 10), EF Core 10 / SQL Server, `Azure.Messaging.ServiceBus` 7.20.2, official `mcr.microsoft.com/azure-messaging/servicebus-emulator` Docker image (+ its required `azure-sql-edge` companion container), `Testcontainers.ServiceBus` 4.14.0 for integration tests, xUnit 2.9.3.

**Spec:** `docs/architecture.md` (§4, §5, §7, §8), `CustomerOps/CLAUDE.md` §12/§13/§23 (§23 idempotency explicitly deferred — see brainstorming decision below), plus the in-chat design approved in this session (no separate spec file, per this project's established convention of going straight from brainstorming to a plan doc).

## Global Constraints

- Target framework: `net10.0` everywhere (matches every existing project).
- `Azure.Messaging.ServiceBus` version `7.20.2` (latest stable as of 2026-08-27).
- `Testcontainers.ServiceBus` version `4.14.0` (matches the `Testcontainers.MsSql`/`Testcontainers.Redis` version already pinned in this repo).
- Single Service Bus queue named `operations`. No topics/subscriptions this phase.
- Consumer scope is minimal: on message receipt, load the `Operation`, call `MarkProcessing()`, save, complete the message. No simulated progress, no retry/backoff policy, no dead-letter handling — that's Phase 5 (Background Worker) / Phase 6 (Testing + Resiliency) territory.
- No `Idempotency-Key` header handling this phase — deferred to the Testing + Resiliency phase per architecture.md §8, confirmed in brainstorming.
- Operation publish failures **propagate** as exceptions (unlike Redis, which degrades gracefully) — a `202` must mean the message is actually queued, not just that a DB row was written. This is a deliberate asymmetry from the Redis cache-aside pattern and should be called out at the phase's interview checkpoint.
- Consumer runs as an `IHostedService` (`BackgroundService`) inside `CustomerPortal.Api` — not a separate worker project — per CLAUDE.md §12.
- Follow existing repo conventions throughout: primary-constructor DI, `private set` + static factory method on domain entities, `required init` DTOs/requests, `AsNoTracking()` for read-only EF queries (not needed here — no list/search endpoint for Operations), one shared `CustomerOperationsController`.

---

## File Structure

```
src/CustomerPortal.Domain/
  OperationStatus.cs                     (create)
  Operation.cs                           (create)

src/CustomerPortal.Application/Operations/
  OperationDto.cs                        (create)
  SubmitOperationRequest.cs              (create)
  SubmitOperationRequestValidator.cs     (create)
  IOperationRepository.cs                (create)
  IOperationPublisher.cs                 (create)
  OperationNotFoundException.cs          (create)
  OperationService.cs                    (create)

src/CustomerPortal.Infrastructure/Persistence/
  CustomerPortalDbContext.cs             (modify)
  OperationRepository.cs                 (create)
  Migrations/...AddOperations...         (generate via EF tooling)

src/CustomerPortal.Infrastructure/Messaging/
  ServiceBusOptions.cs                   (create)
  OperationMessage.cs                    (create)
  ServiceBusOperationPublisher.cs        (create)
  OperationProcessor.cs                  (create)

src/CustomerPortal.Api/
  Controllers/CustomerOperationsController.cs   (modify)
  ErrorHandling/CustomerApiExceptionHandler.cs  (modify)
  Program.cs                             (modify)
  appsettings.Development.json           (modify)
  CustomerPortal.Api.csproj              (modify)

src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj  (modify)

servicebus/
  Config.json                            (create) -- shared by docker-compose and both test projects

docker-compose.yml                       (modify)

tests/CustomerPortal.UnitTests/
  Domain/OperationTests.cs               (create)
  TestDoubles/InMemoryOperationRepository.cs  (create)
  TestDoubles/FakeOperationPublisher.cs  (create)
  TestDoubles/ThrowingOperationPublisher.cs   (create)
  Operations/OperationServiceTests.cs    (create)

tests/CustomerPortal.IntegrationTests/
  CustomerPortal.IntegrationTests.csproj (modify)
  OperationRepositoryTests.cs            (create)
  ServiceBusFixture.cs                   (create)
  ServiceBusMessagingTests.cs            (create)
  ServiceBusOperationPublisherTests.cs   (create)

tests/CustomerPortal.ApiTests/
  CustomerPortal.ApiTests.csproj         (modify)
  CustomerApiFactory.cs                  (modify)
  OperationEndpointsTests.cs             (create)
```

---

### Task 1: Domain — Operation entity and OperationStatus enum

**Files:**
- Create: `src/CustomerPortal.Domain/OperationStatus.cs`
- Create: `src/CustomerPortal.Domain/Operation.cs`
- Test: `tests/CustomerPortal.UnitTests/Domain/OperationTests.cs`

**Interfaces:**
- Produces: `Operation.Submit(string type) : Operation`, `Operation.MarkProcessing() : void`, properties `Id`, `Type`, `Status` (`OperationStatus`), `Progress` (int), `Message` (string), `CreatedAt`, `UpdatedAt` (all `DateTime`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/CustomerPortal.UnitTests/Domain/OperationTests.cs
using CustomerPortal.Domain;
using Xunit;

namespace CustomerPortal.UnitTests.Domain;

public class OperationTests
{
    [Fact]
    public void Submit_SetsPropertiesAndSubmittedStatus()
    {
        var operation = Operation.Submit("document-processing");

        Assert.NotEqual(Guid.Empty, operation.Id);
        Assert.Equal("document-processing", operation.Type);
        Assert.Equal(OperationStatus.Submitted, operation.Status);
        Assert.Equal(0, operation.Progress);
        Assert.Equal("Operation submitted", operation.Message);
        Assert.Equal(operation.CreatedAt, operation.UpdatedAt);
    }

    [Fact]
    public void MarkProcessing_SetsStatusToProcessingAndAdvancesUpdatedAt()
    {
        var operation = Operation.Submit("document-processing");
        var originalUpdatedAt = operation.UpdatedAt;

        operation.MarkProcessing();

        Assert.Equal(OperationStatus.Processing, operation.Status);
        Assert.Equal("Processing", operation.Message);
        Assert.True(operation.UpdatedAt >= originalUpdatedAt);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.UnitTests --filter FullyQualifiedName~OperationTests`
Expected: FAIL to compile — `Operation` and `OperationStatus` don't exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/CustomerPortal.Domain/OperationStatus.cs
namespace CustomerPortal.Domain;

public enum OperationStatus
{
    Submitted,
    Processing,
    Completed,
    Failed
}
```

```csharp
// src/CustomerPortal.Domain/Operation.cs
namespace CustomerPortal.Domain;

public class Operation
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public OperationStatus Status { get; private set; }
    public int Progress { get; private set; }
    public string Message { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Operation()
    {
    }

    public static Operation Submit(string type)
    {
        var now = DateTime.UtcNow;
        return new Operation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Status = OperationStatus.Submitted,
            Progress = 0,
            Message = "Operation submitted",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkProcessing()
    {
        Status = OperationStatus.Processing;
        Message = "Processing";
        UpdatedAt = DateTime.UtcNow;
    }
}
```

`Completed`/`Failed` are declared now (they're part of the documented state machine in CLAUDE.md §14) but nothing produces them yet — that's Phase 5 (Background Worker). No `MarkCompleted`/`MarkFailed` methods yet; adding unused methods now would be dead code.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.UnitTests --filter FullyQualifiedName~OperationTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/CustomerPortal.Domain/OperationStatus.cs src/CustomerPortal.Domain/Operation.cs tests/CustomerPortal.UnitTests/Domain/OperationTests.cs
git commit -m "feat: add Operation domain entity"
```

---

### Task 2: Application — Operation contracts and OperationService

**Files:**
- Create: `src/CustomerPortal.Application/Operations/OperationDto.cs`
- Create: `src/CustomerPortal.Application/Operations/SubmitOperationRequest.cs`
- Create: `src/CustomerPortal.Application/Operations/SubmitOperationRequestValidator.cs`
- Create: `src/CustomerPortal.Application/Operations/IOperationRepository.cs`
- Create: `src/CustomerPortal.Application/Operations/IOperationPublisher.cs`
- Create: `src/CustomerPortal.Application/Operations/OperationNotFoundException.cs`
- Create: `src/CustomerPortal.Application/Operations/OperationService.cs`
- Create: `tests/CustomerPortal.UnitTests/TestDoubles/InMemoryOperationRepository.cs`
- Create: `tests/CustomerPortal.UnitTests/TestDoubles/FakeOperationPublisher.cs`
- Create: `tests/CustomerPortal.UnitTests/TestDoubles/ThrowingOperationPublisher.cs`
- Test: `tests/CustomerPortal.UnitTests/Operations/OperationServiceTests.cs`

**Interfaces:**
- Consumes: `Operation.Submit(string)`, `Operation.MarkProcessing()` from Task 1.
- Produces: `OperationService(IOperationRepository, IOperationPublisher, IValidator<SubmitOperationRequest>)` with `SubmitAsync(SubmitOperationRequest, CancellationToken) : Task<OperationDto>` and `GetByIdAsync(Guid, CancellationToken) : Task<OperationDto>`. `IOperationRepository.GetByIdAsync/AddAsync/UpdateAsync`, `IOperationPublisher.PublishAsync(Guid, CancellationToken) : Task`. These interface shapes are what Task 3 (repository) and Task 6 (publisher) implement.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/CustomerPortal.UnitTests/TestDoubles/InMemoryOperationRepository.cs
using CustomerPortal.Application.Operations;
using CustomerPortal.Domain;

namespace CustomerPortal.UnitTests.TestDoubles;

public class InMemoryOperationRepository : IOperationRepository
{
    private readonly List<Operation> _operations = new();

    public Task<Operation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_operations.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(Operation operation, CancellationToken ct)
    {
        _operations.Add(operation);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Operation operation, CancellationToken ct) => Task.CompletedTask;
}
```

```csharp
// tests/CustomerPortal.UnitTests/TestDoubles/FakeOperationPublisher.cs
using CustomerPortal.Application.Operations;

namespace CustomerPortal.UnitTests.TestDoubles;

public class FakeOperationPublisher : IOperationPublisher
{
    public List<Guid> PublishedOperationIds { get; } = new();

    public Task PublishAsync(Guid operationId, CancellationToken ct)
    {
        PublishedOperationIds.Add(operationId);
        return Task.CompletedTask;
    }
}
```

```csharp
// tests/CustomerPortal.UnitTests/TestDoubles/ThrowingOperationPublisher.cs
using CustomerPortal.Application.Operations;

namespace CustomerPortal.UnitTests.TestDoubles;

public class ThrowingOperationPublisher : IOperationPublisher
{
    public Task PublishAsync(Guid operationId, CancellationToken ct) =>
        throw new InvalidOperationException("Service Bus publish failed");
}
```

```csharp
// tests/CustomerPortal.UnitTests/Operations/OperationServiceTests.cs
using CustomerPortal.Application.Operations;
using CustomerPortal.UnitTests.TestDoubles;
using FluentValidation;
using Xunit;

namespace CustomerPortal.UnitTests.Operations;

public class OperationServiceTests
{
    private readonly InMemoryOperationRepository _repository = new();
    private readonly FakeOperationPublisher _publisher = new();
    private readonly OperationService _service;

    public OperationServiceTests()
    {
        _service = new OperationService(_repository, _publisher, new SubmitOperationRequestValidator());
    }

    private static SubmitOperationRequest ValidRequest() => new() { Type = "document-processing" };

    [Fact]
    public async Task SubmitAsync_WithValidRequest_ReturnsSubmittedOperation()
    {
        var result = await _service.SubmitAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal("document-processing", result.Type);
        Assert.Equal("Submitted", result.Status);
        Assert.Equal(0, result.Progress);
    }

    [Fact]
    public async Task SubmitAsync_PublishesTheOperationId()
    {
        var result = await _service.SubmitAsync(ValidRequest(), CancellationToken.None);

        Assert.Contains(result.Id, _publisher.PublishedOperationIds);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidRequest_ThrowsValidationException()
    {
        var request = new SubmitOperationRequest { Type = "" };

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAsync_WhenPublisherThrows_PropagatesTheException()
    {
        var service = new OperationService(_repository, new ThrowingOperationPublisher(), new SubmitOperationRequestValidator());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitAsync(ValidRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ThrowsOperationNotFoundException()
    {
        await Assert.ThrowsAsync<OperationNotFoundException>(
            () => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.UnitTests --filter FullyQualifiedName~OperationServiceTests`
Expected: FAIL to compile — none of the Application types exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/CustomerPortal.Application/Operations/OperationDto.cs
namespace CustomerPortal.Application.Operations;

public class OperationDto
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Status { get; init; }
    public required int Progress { get; init; }
    public required string Message { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
```

```csharp
// src/CustomerPortal.Application/Operations/SubmitOperationRequest.cs
namespace CustomerPortal.Application.Operations;

public class SubmitOperationRequest
{
    public required string Type { get; init; }
}
```

```csharp
// src/CustomerPortal.Application/Operations/SubmitOperationRequestValidator.cs
using FluentValidation;

namespace CustomerPortal.Application.Operations;

public class SubmitOperationRequestValidator : AbstractValidator<SubmitOperationRequest>
{
    public SubmitOperationRequestValidator()
    {
        RuleFor(x => x.Type).NotEmpty().MaximumLength(50);
    }
}
```

```csharp
// src/CustomerPortal.Application/Operations/IOperationRepository.cs
using CustomerPortal.Domain;

namespace CustomerPortal.Application.Operations;

public interface IOperationRepository
{
    Task<Operation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Operation operation, CancellationToken ct);
    Task UpdateAsync(Operation operation, CancellationToken ct);
}
```

```csharp
// src/CustomerPortal.Application/Operations/IOperationPublisher.cs
namespace CustomerPortal.Application.Operations;

public interface IOperationPublisher
{
    Task PublishAsync(Guid operationId, CancellationToken ct);
}
```

```csharp
// src/CustomerPortal.Application/Operations/OperationNotFoundException.cs
namespace CustomerPortal.Application.Operations;

public class OperationNotFoundException(Guid id) : Exception($"Operation '{id}' was not found.");
```

```csharp
// src/CustomerPortal.Application/Operations/OperationService.cs
using CustomerPortal.Domain;
using FluentValidation;

namespace CustomerPortal.Application.Operations;

public class OperationService(
    IOperationRepository repository,
    IOperationPublisher publisher,
    IValidator<SubmitOperationRequest> validator)
{
    public async Task<OperationDto> SubmitAsync(SubmitOperationRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var operation = Operation.Submit(request.Type);
        await repository.AddAsync(operation, ct);
        await publisher.PublishAsync(operation.Id, ct);
        return ToDto(operation);
    }

    public async Task<OperationDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var operation = await repository.GetByIdAsync(id, ct) ?? throw new OperationNotFoundException(id);
        return ToDto(operation);
    }

    private static OperationDto ToDto(Operation o) => new()
    {
        Id = o.Id,
        Type = o.Type,
        Status = o.Status.ToString(),
        Progress = o.Progress,
        Message = o.Message,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt
    };
}
```

Note the deliberate ordering in `SubmitAsync`: `AddAsync` (persist) happens before `PublishAsync` (queue). If publish fails, the row exists but is never picked up by the consumer — an orphaned `Submitted` operation, not a lost one. The exception propagates to the controller (which will surface as a 500), not swallowed — this is the interview-relevant contrast with Redis's graceful degradation, since here Service Bus is the source of truth for "will this work happen," not a cache.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.UnitTests --filter FullyQualifiedName~OperationServiceTests`
Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add src/CustomerPortal.Application/Operations tests/CustomerPortal.UnitTests/TestDoubles/InMemoryOperationRepository.cs tests/CustomerPortal.UnitTests/TestDoubles/FakeOperationPublisher.cs tests/CustomerPortal.UnitTests/TestDoubles/ThrowingOperationPublisher.cs tests/CustomerPortal.UnitTests/Operations/OperationServiceTests.cs
git commit -m "feat: add OperationService application layer"
```

---

### Task 3: Infrastructure — EF Core Operations table and repository

**Files:**
- Modify: `src/CustomerPortal.Infrastructure/Persistence/CustomerPortalDbContext.cs`
- Create: `src/CustomerPortal.Infrastructure/Persistence/OperationRepository.cs`
- Generate: EF migration under `src/CustomerPortal.Infrastructure/Persistence/Migrations/`
- Test: `tests/CustomerPortal.IntegrationTests/OperationRepositoryTests.cs`

**Interfaces:**
- Consumes: `Operation` (Task 1), `IOperationRepository` (Task 2), existing `SqlServerFixture` (`tests/CustomerPortal.IntegrationTests/SqlServerFixture.cs`, already in the repo from Phase 2 — exposes `CreateContext() : CustomerPortalDbContext`).
- Produces: `OperationRepository(CustomerPortalDbContext) : IOperationRepository`, `CustomerPortalDbContext.Operations : DbSet<Operation>`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/CustomerPortal.IntegrationTests/OperationRepositoryTests.cs
using CustomerPortal.Domain;
using CustomerPortal.Infrastructure.Persistence;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(SqlServerCollection))]
public class OperationRepositoryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsPersistedOperation()
    {
        await using var context = fixture.CreateContext();
        var repository = new OperationRepository(context);
        var operation = Operation.Submit("document-processing");

        await repository.AddAsync(operation, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new OperationRepository(readContext).GetByIdAsync(operation.Id, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal("document-processing", fetched!.Type);
        Assert.Equal(OperationStatus.Submitted, fetched.Status);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = fixture.CreateContext();
        var repository = new OperationRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsStatusChange()
    {
        await using var context = fixture.CreateContext();
        var repository = new OperationRepository(context);
        var operation = Operation.Submit("document-processing");
        await repository.AddAsync(operation, CancellationToken.None);

        operation.MarkProcessing();
        await repository.UpdateAsync(operation, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new OperationRepository(readContext).GetByIdAsync(operation.Id, CancellationToken.None);
        Assert.Equal(OperationStatus.Processing, fetched!.Status);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~OperationRepositoryTests`
Expected: FAIL to compile — `OperationRepository` doesn't exist and `Operations` table isn't mapped.

- [ ] **Step 3: Write minimal implementation**

Modify `src/CustomerPortal.Infrastructure/Persistence/CustomerPortalDbContext.cs` — add the `Operations` DbSet and its mapping:

```csharp
// src/CustomerPortal.Infrastructure/Persistence/CustomerPortalDbContext.cs (full file after edit)
using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure.Persistence;

public class CustomerPortalDbContext(DbContextOptions<CustomerPortalDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Operation> Operations => Set<Operation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.LastName).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.Phone).HasMaxLength(30).IsRequired();
            entity.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(c => c.Email);
            entity.HasIndex(c => c.LastName);
        });

        modelBuilder.Entity<Operation>(entity =>
        {
            entity.ToTable("Operations");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).HasMaxLength(50).IsRequired();
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(o => o.Message).HasMaxLength(500).IsRequired();
        });
    }
}
```

```csharp
// src/CustomerPortal.Infrastructure/Persistence/OperationRepository.cs
using CustomerPortal.Application.Operations;
using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure.Persistence;

public class OperationRepository(CustomerPortalDbContext context) : IOperationRepository
{
    public async Task<Operation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Operations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task AddAsync(Operation operation, CancellationToken ct)
    {
        context.Operations.Add(operation);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Operation operation, CancellationToken ct) =>
        await context.SaveChangesAsync(ct);
}
```

Generate the migration (the existing `CustomerPortalDbContextFactory` design-time factory already provides `DbContextOptions<CustomerPortalDbContext>`, so this should work without the Phase 2 DI-resolution gotcha):

```bash
dotnet ef migrations add AddOperations --project src/CustomerPortal.Infrastructure --startup-project src/CustomerPortal.Api
```

Expected: a new file pair under `src/CustomerPortal.Infrastructure/Persistence/Migrations/` (e.g. `<timestamp>_AddOperations.cs` + `.Designer.cs`) creating the `Operations` table, and `CustomerPortalDbContextModelSnapshot.cs` updated to include it.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~OperationRepositoryTests`
Expected: PASS (3 tests) — `SqlServerFixture.InitializeAsync` calls `context.Database.MigrateAsync()`, which will apply the new migration against the Testcontainers SQL instance automatically.

- [ ] **Step 5: Commit**

```bash
git add src/CustomerPortal.Infrastructure/Persistence tests/CustomerPortal.IntegrationTests/OperationRepositoryTests.cs
git commit -m "feat: persist Operation entities via EF Core"
```

---

### Task 4: Service Bus emulator — docker-compose and Config.json

**Files:**
- Modify: `docker-compose.yml`
- Create: `servicebus/Config.json`

**Interfaces:**
- Produces: a running `servicebus-emulator`-equivalent container reachable at `localhost:5672` (AMQP) and `localhost:5300` (HTTP health/admin), backed by a companion `sqledge` container, with a single queue named `operations` pre-provisioned. Task 5 depends on `servicebus/Config.json` existing at this path.

- [ ] **Step 1: Write the config file**

```json
// servicebus/Config.json
{
  "UserConfig": {
    "Namespaces": [
      {
        "Name": "sbemulatorns",
        "Queues": [
          {
            "Name": "operations",
            "Properties": {
              "DeadLetteringOnMessageExpiration": false,
              "DefaultMessageTimeToLive": "PT1H",
              "DuplicateDetectionHistoryTimeWindow": "PT20S",
              "ForwardDeadLetteredMessagesTo": "",
              "ForwardTo": "",
              "LockDuration": "PT1M",
              "MaxDeliveryCount": 3,
              "RequiresDuplicateDetection": false,
              "RequiresSession": false
            }
          }
        ],
        "Topics": []
      }
    ],
    "Logging": {
      "Type": "File"
    }
  }
}
```

- [ ] **Step 2: Add the emulator and its SQL Edge dependency to docker-compose.yml**

```yaml
# docker-compose.yml (full file after edit)
services:
  sql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "LocalDev!2026"
    ports:
      - "1433:1433"
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
  sqledge:
    image: mcr.microsoft.com/azure-sql-edge:latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "LocalDev!2026"
  servicebus:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    pull_policy: always
    volumes:
      - ./servicebus/Config.json:/ServiceBus_Emulator/ConfigFiles/Config.json
    ports:
      - "5672:5672"
      - "5300:5300"
    environment:
      SQL_SERVER: sqledge
      MSSQL_SA_PASSWORD: "LocalDev!2026"
      ACCEPT_EULA: "Y"
      SQL_WAIT_INTERVAL: "0"
    depends_on:
      - sqledge
```

`servicebus` reaches `sqledge` by service name over docker-compose's default bridge network — the same implicit networking `sql`/`redis` already rely on, no explicit `networks:` block needed.

- [ ] **Step 3: Start it and verify manually**

Run:
```bash
docker compose up -d sqledge servicebus
```
Wait about 30-60 seconds for `sqledge` to initialize (the emulator polls it via `SQL_WAIT_INTERVAL`), then:
```bash
curl http://localhost:5300/health
```
Expected: HTTP 200 with a healthy status body. Then check logs if it's not healthy yet:
```bash
docker compose logs servicebus
```
Expected line: `Emulator Service is Successfully Up!`

- [ ] **Step 4: Tear down**

```bash
docker compose down
```

(Leave it down — Task 5's Testcontainers-based tests start their own isolated instance and don't need this one running. You can bring it back up later for manual/browser testing of the full app.)

- [ ] **Step 5: Commit**

```bash
git add docker-compose.yml servicebus/Config.json
git commit -m "feat: add Service Bus emulator to local docker-compose stack"
```

---

### Task 5: Testcontainers.ServiceBus wiring smoke test

**Files:**
- Modify: `tests/CustomerPortal.IntegrationTests/CustomerPortal.IntegrationTests.csproj`
- Create: `tests/CustomerPortal.IntegrationTests/ServiceBusFixture.cs`
- Create: `tests/CustomerPortal.IntegrationTests/ServiceBusMessagingTests.cs`

**Interfaces:**
- Consumes: `servicebus/Config.json` (Task 4).
- Produces: `ServiceBusFixture.CreateClient() : ServiceBusClient`, reused by Task 6's publisher test and by `CustomerApiFactory` in Task 6.

- [ ] **Step 1: Add package references and copy the shared Config.json into the test output**

```xml
<!-- tests/CustomerPortal.IntegrationTests/CustomerPortal.IntegrationTests.csproj -->
<!-- add inside the existing <ItemGroup> that has the other PackageReference entries -->
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />
<PackageReference Include="Testcontainers.ServiceBus" Version="4.14.0" />
```

```xml
<!-- add as a new ItemGroup in the same csproj -->
<ItemGroup>
  <None Include="..\..\servicebus\Config.json" Link="Config.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing test**

```csharp
// tests/CustomerPortal.IntegrationTests/ServiceBusFixture.cs
using Azure.Messaging.ServiceBus;
using Testcontainers.ServiceBus;
using Xunit;

namespace CustomerPortal.IntegrationTests;

public class ServiceBusFixture : IAsyncLifetime
{
    private readonly ServiceBusContainer _container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
        .WithAcceptLicenseAgreement(true)
        .WithConfig(Path.Combine(AppContext.BaseDirectory, "Config.json"))
        .Build();

    public ServiceBusClient CreateClient() => new(_container.GetConnectionString());

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(ServiceBusCollection))]
public class ServiceBusCollection : ICollectionFixture<ServiceBusFixture>;
```

```csharp
// tests/CustomerPortal.IntegrationTests/ServiceBusMessagingTests.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(ServiceBusCollection))]
public class ServiceBusMessagingTests(ServiceBusFixture fixture)
{
    [Fact]
    public async Task SendMessage_ThenReceive_DeliversTheMessage()
    {
        await using var client = fixture.CreateClient();
        var operationId = Guid.NewGuid();
        var sender = client.CreateSender("operations");
        await sender.SendMessageAsync(new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(new { OperationId = operationId })));

        var receiver = client.CreateReceiver("operations");
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        Assert.NotNull(received);
        var payload = JsonSerializer.Deserialize<JsonElement>(received!.Body);
        Assert.Equal(operationId, payload.GetProperty("OperationId").GetGuid());
        await receiver.CompleteMessageAsync(received);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~ServiceBusMessagingTests`
Expected: FAIL to compile initially (packages not restored) — run `dotnet restore` first if needed, then it should compile but this is also the first real run against the actual emulator image, so treat any container-startup failure here as the thing this task exists to catch (per the brainstorming design note flagging Testcontainers.ServiceBus as less mature than the Redis/MsSql modules). If `Build()`/`StartAsync()` fails, that's a real finding — investigate before proceeding (likely candidates: Docker Desktop resource limits, or a config validation error surfaced in `docker compose logs`-equivalent Testcontainers output).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~ServiceBusMessagingTests`
Expected: PASS (1 test). This may take longer than the Redis/SQL tests (the emulator + its SQL Edge dependency both need to start and the emulator waits on SQL Edge).

- [ ] **Step 5: Commit**

```bash
git add tests/CustomerPortal.IntegrationTests/CustomerPortal.IntegrationTests.csproj tests/CustomerPortal.IntegrationTests/ServiceBusFixture.cs tests/CustomerPortal.IntegrationTests/ServiceBusMessagingTests.cs
git commit -m "test: verify Testcontainers.ServiceBus wiring against the emulator"
```

---

### Task 6: ServiceBusOperationPublisher, DI wiring, and CustomerApiFactory

**Files:**
- Create: `src/CustomerPortal.Infrastructure/Messaging/ServiceBusOptions.cs`
- Create: `src/CustomerPortal.Infrastructure/Messaging/OperationMessage.cs`
- Create: `src/CustomerPortal.Infrastructure/Messaging/ServiceBusOperationPublisher.cs`
- Modify: `src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj`
- Modify: `src/CustomerPortal.Api/CustomerPortal.Api.csproj`
- Modify: `src/CustomerPortal.Api/Program.cs`
- Modify: `src/CustomerPortal.Api/appsettings.Development.json`
- Modify: `tests/CustomerPortal.ApiTests/CustomerPortal.ApiTests.csproj`
- Modify: `tests/CustomerPortal.ApiTests/CustomerApiFactory.cs`
- Test: `tests/CustomerPortal.IntegrationTests/ServiceBusOperationPublisherTests.cs`

**Interfaces:**
- Consumes: `IOperationPublisher` (Task 2), `ServiceBusFixture` (Task 5).
- Produces: `ServiceBusOperationPublisher(ServiceBusClient, IOptions<ServiceBusOptions>) : IOperationPublisher`, config keys `ConnectionStrings:ServiceBus` and `ServiceBus:QueueName`. Task 7 consumes these DI registrations to build `OperationService` end-to-end in the API.

- [ ] **Step 1: Add package references**

```xml
<!-- src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj -->
<!-- add inside the existing ItemGroup with Microsoft.EntityFrameworkCore.SqlServer -->
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.11" />
```

```xml
<!-- src/CustomerPortal.Api/CustomerPortal.Api.csproj -->
<!-- add inside the existing ItemGroup with the other PackageReference entries -->
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />
```

```xml
<!-- tests/CustomerPortal.ApiTests/CustomerPortal.ApiTests.csproj -->
<!-- add inside the existing ItemGroup with the other PackageReference entries -->
<PackageReference Include="Azure.Messaging.ServiceBus" Version="7.20.2" />
<PackageReference Include="Testcontainers.ServiceBus" Version="4.14.0" />
```

```xml
<!-- tests/CustomerPortal.ApiTests/CustomerPortal.ApiTests.csproj -->
<!-- add as a new ItemGroup -->
<ItemGroup>
  <None Include="..\..\servicebus\Config.json" Link="Config.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing test**

```csharp
// tests/CustomerPortal.IntegrationTests/ServiceBusOperationPublisherTests.cs
using System.Text.Json;
using CustomerPortal.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(ServiceBusCollection))]
public class ServiceBusOperationPublisherTests(ServiceBusFixture fixture)
{
    [Fact]
    public async Task PublishAsync_SendsAMessageContainingTheOperationId()
    {
        await using var client = fixture.CreateClient();
        var options = Options.Create(new ServiceBusOptions { QueueName = "operations" });
        await using var publisher = new ServiceBusOperationPublisher(client, options);
        var operationId = Guid.NewGuid();

        await publisher.PublishAsync(operationId, CancellationToken.None);

        var receiver = client.CreateReceiver("operations");
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        Assert.NotNull(received);
        var payload = JsonSerializer.Deserialize<OperationMessage>(received!.Body.ToArray());
        Assert.Equal(operationId, payload!.OperationId);
        await receiver.CompleteMessageAsync(received);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~ServiceBusOperationPublisherTests`
Expected: FAIL to compile — `ServiceBusOptions`, `OperationMessage`, `ServiceBusOperationPublisher` don't exist yet.

- [ ] **Step 4: Write minimal implementation**

```csharp
// src/CustomerPortal.Infrastructure/Messaging/ServiceBusOptions.cs
namespace CustomerPortal.Infrastructure.Messaging;

public class ServiceBusOptions
{
    public required string QueueName { get; init; }
}
```

```csharp
// src/CustomerPortal.Infrastructure/Messaging/OperationMessage.cs
namespace CustomerPortal.Infrastructure.Messaging;

public class OperationMessage
{
    public required Guid OperationId { get; init; }
}
```

```csharp
// src/CustomerPortal.Infrastructure/Messaging/ServiceBusOperationPublisher.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CustomerPortal.Application.Operations;
using Microsoft.Extensions.Options;

namespace CustomerPortal.Infrastructure.Messaging;

public class ServiceBusOperationPublisher(ServiceBusClient client, IOptions<ServiceBusOptions> options)
    : IOperationPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender = client.CreateSender(options.Value.QueueName);

    public async Task PublishAsync(Guid operationId, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new OperationMessage { OperationId = operationId });
        var message = new ServiceBusMessage(payload) { ContentType = "application/json" };
        await _sender.SendMessageAsync(message, ct);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();
}
```

Wire it into `Program.cs` — add these lines after the existing `AddStackExchangeRedisCache` block (around what is currently line 20) and before the `AddScoped<ICustomerRepository, ...>` line:

```csharp
// src/CustomerPortal.Api/Program.cs -- add these usings at the top alongside the existing ones
using Azure.Messaging.ServiceBus;
using CustomerPortal.Application.Operations;
using CustomerPortal.Infrastructure.Messaging;
```

```csharp
// src/CustomerPortal.Api/Program.cs -- add after the AddStackExchangeRedisCache block
builder.Services.AddSingleton(new ServiceBusClient(builder.Configuration.GetConnectionString("ServiceBus")));
builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));
builder.Services.AddSingleton<IOperationPublisher, ServiceBusOperationPublisher>();
```

Note: `SubmitOperationRequestValidator` does **not** need a separate `AddValidatorsFromAssemblyContaining` call — the existing `builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>()` already scans the whole `CustomerPortal.Application` assembly and will pick it up automatically.

Add the connection string and queue name to `appsettings.Development.json`:

```json
// src/CustomerPortal.Api/appsettings.Development.json (full file after edit)
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  },
  "ConnectionStrings": {
    "CustomerPortal": "Server=localhost,1433;Database=CustomerPortal;User Id=sa;Password=LocalDev!2026;TrustServerCertificate=True;",
     "Redis": "localhost:6379",
     "ServiceBus": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
  },
  "ServiceBus": {
    "QueueName": "operations"
  }
}
```

Update `CustomerApiFactory` to run its own Service Bus emulator container and override the `ServiceBusClient` registration, keeping every existing ApiTests test (health check, customer CRUD, Redis cache) green:

```csharp
// tests/CustomerPortal.ApiTests/CustomerApiFactory.cs (full file after edit)
using Azure.Messaging.ServiceBus;
using CustomerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Testcontainers.ServiceBus;
using Xunit;

namespace CustomerPortal.ApiTests;

public class CustomerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder().Build();
    private readonly ServiceBusContainer _serviceBusContainer = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
        .WithAcceptLicenseAgreement(true)
        .WithConfig(Path.Combine(AppContext.BaseDirectory, "Config.json"))
        .Build();

    public string RedisConnectionString => _redisContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerPortalDbContext>>();
            services.AddDbContext<CustomerPortalDbContext>(options =>
                options.UseSqlServer(_sqlContainer.GetConnectionString()));

            services.RemoveAll<IDistributedCache>();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _redisContainer.GetConnectionString();
            });

            services.RemoveAll<ServiceBusClient>();
            services.AddSingleton(new ServiceBusClient(_serviceBusContainer.GetConnectionString()));
        });
    }

    public Task InitializeAsync() =>
        Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync(), _serviceBusContainer.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await _serviceBusContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(CustomerApiCollection))]
public class CustomerApiCollection : ICollectionFixture<CustomerApiFactory>;
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.IntegrationTests --filter FullyQualifiedName~ServiceBusOperationPublisherTests`
Expected: PASS (1 test)

Then run the full existing ApiTests suite to confirm `CustomerApiFactory`'s new Service Bus container doesn't break anything already passing:

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: PASS (all existing health/customer/cache tests still green — this is the verification that the factory change is safe, since there's no Operations controller yet to test directly in this task).

- [ ] **Step 6: Commit**

```bash
git add src/CustomerPortal.Infrastructure/Messaging src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj src/CustomerPortal.Api/CustomerPortal.Api.csproj src/CustomerPortal.Api/Program.cs src/CustomerPortal.Api/appsettings.Development.json tests/CustomerPortal.ApiTests/CustomerPortal.ApiTests.csproj tests/CustomerPortal.ApiTests/CustomerApiFactory.cs tests/CustomerPortal.IntegrationTests/ServiceBusOperationPublisherTests.cs
git commit -m "feat: publish operation messages to the Service Bus emulator"
```

---

### Task 7: Controller endpoints — POST/GET /api/v1/operations

**Files:**
- Modify: `src/CustomerPortal.Api/Controllers/CustomerOperationsController.cs`
- Modify: `src/CustomerPortal.Api/ErrorHandling/CustomerApiExceptionHandler.cs`
- Modify: `src/CustomerPortal.Api/Program.cs`
- Test: `tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs`

**Interfaces:**
- Consumes: `OperationService` (Task 2), `OperationRepository` (Task 3), `ServiceBusOperationPublisher` DI wiring (Task 6).
- Produces: `POST /api/v1/operations` (202 + `Location` header + body), `GET /api/v1/operations/{id}` (200 or 404). Task 8 adds the third test to `OperationEndpointsTests.cs` once the consumer exists.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using CustomerPortal.Application.Operations;
using Xunit;

namespace CustomerPortal.ApiTests;

[Collection(nameof(CustomerApiCollection))]
public class OperationEndpointsTests(CustomerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SubmitOperation_Returns202WithLocationHeaderAndSubmittedStatus()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequest { Type = "document-processing" });
        var body = await response.Content.ReadFromJsonAsync<OperationDto>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("Submitted", body!.Status);
    }

    [Fact]
    public async Task GetOperationById_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/operations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.ApiTests --filter FullyQualifiedName~OperationEndpointsTests`
Expected: FAIL — routes don't exist yet (404 for the POST too, or a DI resolution error since `OperationService` isn't registered).

- [ ] **Step 3: Write minimal implementation**

Register the remaining Application/Infrastructure pieces in `Program.cs` — add these lines next to the existing `AddScoped<ICustomerRepository, CustomerRepository>()` / `AddScoped<CustomerService>()` lines:

```csharp
// src/CustomerPortal.Api/Program.cs -- add alongside the existing customer registrations
builder.Services.AddScoped<IOperationRepository, OperationRepository>();
builder.Services.AddScoped<OperationService>();
```

(`OperationRepository` lives in `CustomerPortal.Infrastructure.Persistence`, already `using`'d in `Program.cs`; `IOperationRepository`/`OperationService` are in `CustomerPortal.Application.Operations`, already `using`'d from Task 6.)

Extend the controller:

```csharp
// src/CustomerPortal.Api/Controllers/CustomerOperationsController.cs (full file after edit)
using Asp.Versioning;
using CustomerPortal.Application.Common;
using CustomerPortal.Application.Customers;
using CustomerPortal.Application.Operations;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public class CustomerOperationsController(CustomerService customerService, OperationService operationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerDto>>> List(
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await customerService.ListAsync(pageNumber, pageSize, ct));

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<CustomerDto>>> Search(
        [FromQuery] string query, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await customerService.SearchAsync(query, pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await customerService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customerService.CreateAsync(request, ct);
        return Created($"/api/v1/customers/{created.Id}", created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct)
        => Ok(await customerService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await customerService.DeactivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("~/api/v{version:apiVersion}/operations")]
    public async Task<ActionResult<OperationDto>> SubmitOperation(SubmitOperationRequest request, CancellationToken ct)
    {
        var created = await operationService.SubmitAsync(request, ct);
        return Accepted($"/api/v1/operations/{created.Id}", created);
    }

    [HttpGet("~/api/v{version:apiVersion}/operations/{id:guid}")]
    public async Task<ActionResult<OperationDto>> GetOperationById(Guid id, CancellationToken ct)
        => Ok(await operationService.GetByIdAsync(id, ct));
}
```

The `~/` prefix on the two new routes overrides the controller-level `[Route("api/v{version:apiVersion}/customers")]` so they resolve under `/api/v1/operations` instead of nesting under `/customers` — the same single-controller-multiple-resource-groups shape CLAUDE.md §8 describes. Response bodies use hardcoded `/api/v1/...` location strings, matching the existing `Create` action above rather than `CreatedAtAction`-style versioned link generation (which isn't configured for URL-segment reversal in this project).

Extend the exception handler:

```csharp
// src/CustomerPortal.Api/ErrorHandling/CustomerApiExceptionHandler.cs (full file after edit)
using CustomerPortal.Application.Customers;
using CustomerPortal.Application.Operations;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace CustomerPortal.Api.ErrorHandling;

public class CustomerApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case ValidationException validationException:
                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
                return true;

            case CustomerNotFoundException notFoundException:
                await Results.Problem(
                    title: "Customer not found",
                    detail: notFoundException.Message,
                    statusCode: StatusCodes.Status404NotFound
                ).ExecuteAsync(httpContext);
                return true;

            case OperationNotFoundException operationNotFoundException:
                await Results.Problem(
                    title: "Operation not found",
                    detail: operationNotFoundException.Message,
                    statusCode: StatusCodes.Status404NotFound
                ).ExecuteAsync(httpContext);
                return true;

            default:
                return false;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.ApiTests --filter FullyQualifiedName~OperationEndpointsTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/CustomerPortal.Api/Controllers/CustomerOperationsController.cs src/CustomerPortal.Api/ErrorHandling/CustomerApiExceptionHandler.cs src/CustomerPortal.Api/Program.cs tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs
git commit -m "feat: add POST/GET /api/v1/operations endpoints"
```

---

### Task 8: OperationProcessor — minimal consumer closing the loop

**Files:**
- Create: `src/CustomerPortal.Infrastructure/Messaging/OperationProcessor.cs`
- Modify: `src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj`
- Modify: `src/CustomerPortal.Api/Program.cs`
- Modify: `tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs`

**Interfaces:**
- Consumes: `IOperationRepository` (Task 3, resolved per-message via a DI scope), `OperationMessage` (Task 6), `ServiceBusClient`/`ServiceBusOptions` (Task 6).
- Produces: a running background consumer — this is the last piece; nothing downstream depends on it within this phase.

- [ ] **Step 1: Write the failing test**

Add this test to the existing `tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs` (append inside the class, after `GetOperationById_WithUnknownId_Returns404`):

```csharp
    [Fact]
    public async Task SubmitOperation_IsEventuallyProcessedByTheConsumer()
    {
        var submitResponse = await _client.PostAsJsonAsync("/api/v1/operations", new SubmitOperationRequest { Type = "document-processing" });
        var submitted = await submitResponse.Content.ReadFromJsonAsync<OperationDto>();

        OperationDto? latest = null;
        for (var i = 0; i < 20; i++)
        {
            var getResponse = await _client.GetAsync($"/api/v1/operations/{submitted!.Id}");
            latest = await getResponse.Content.ReadFromJsonAsync<OperationDto>();
            if (latest!.Status == "Processing")
            {
                break;
            }
            await Task.Delay(500);
        }

        Assert.Equal("Processing", latest!.Status);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CustomerPortal.ApiTests --filter FullyQualifiedName~SubmitOperation_IsEventuallyProcessedByTheConsumer`
Expected: FAIL — the operation stays `Submitted` for the full 10-second poll window (~20 x 500ms) since nothing consumes the queue yet.

- [ ] **Step 3: Write minimal implementation**

`BackgroundService` lives in `Microsoft.Extensions.Hosting.Abstractions`, which isn't yet referenced by the Infrastructure project (it currently only pulls in `Microsoft.EntityFrameworkCore.SqlServer`, which doesn't bring this transitively) — add it first:

```xml
<!-- src/CustomerPortal.Infrastructure/CustomerPortal.Infrastructure.csproj -->
<!-- add inside the existing ItemGroup with Microsoft.EntityFrameworkCore.SqlServer -->
<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.11" />
```

```csharp
// src/CustomerPortal.Infrastructure/Messaging/OperationProcessor.cs
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CustomerPortal.Application.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CustomerPortal.Infrastructure.Messaging;

public class OperationProcessor(
    ServiceBusClient client,
    IOptions<ServiceBusOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<OperationProcessor> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = client.CreateProcessor(options.Value.QueueName, new ServiceBusProcessorOptions());
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
        await _processor.StartProcessingAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var payload = JsonSerializer.Deserialize<OperationMessage>(args.Message.Body.ToArray())
            ?? throw new InvalidOperationException("Received an operation message with no payload.");

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var operation = await repository.GetByIdAsync(payload.OperationId, args.CancellationToken);
        if (operation is not null)
        {
            operation.MarkProcessing();
            await repository.UpdateAsync(operation, args.CancellationToken);
        }

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus processor error in {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }
}
```

`StartAsync`/`StopAsync` are overridden directly (rather than doing the work in `ExecuteAsync`) because `ServiceBusProcessor` manages its own message pump internally once `StartProcessingAsync` returns — there's no long-running loop for `ExecuteAsync` to await. A scope is created per message (not per processor lifetime) so `IOperationRepository`'s scoped `CustomerPortalDbContext` dependency is resolved correctly — mirrors how `CustomerApiFactory`'s DI overrides already respect the `DbContext`'s scoped lifetime.

Register the hosted service in `Program.cs`, after the `AddScoped<OperationService>()` line added in Task 7:

```csharp
// src/CustomerPortal.Api/Program.cs -- add after AddScoped<OperationService>();
builder.Services.AddHostedService<OperationProcessor>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/CustomerPortal.ApiTests --filter FullyQualifiedName~OperationEndpointsTests`
Expected: PASS (3 tests)

Then run the full solution test suite to confirm nothing regressed:

Run: `dotnet test`
Expected: PASS, all tests across `CustomerPortal.UnitTests`, `CustomerPortal.IntegrationTests`, `CustomerPortal.ApiTests`.

- [ ] **Step 5: Commit**

```bash
git add src/CustomerPortal.Infrastructure/Messaging/OperationProcessor.cs src/CustomerPortal.Api/Program.cs tests/CustomerPortal.ApiTests/OperationEndpointsTests.cs
git commit -m "feat: add minimal Service Bus consumer proving API -> emulator -> DB pipe"
```

---

## After This Plan

Per this project's convention (see `docs/plans/2026-08-21-phase-2-customer-apis.md` and `2026-08-21-phase-3-redis.md`), once all 8 tasks pass and are committed:

1. Manually verify with the app running: `dotnet run --project src/CustomerPortal.Api` against `docker compose up -d` (all four containers), `POST /api/v1/operations`, then `GET /api/v1/operations/{id}` a few times and watch it flip from `Submitted` to `Processing`.
2. Run the CLAUDE.md §46 interview checkpoint (Service Bus / async messaging questions) before moving to the next phase.
3. Append a **Lessons Learned** section to this plan doc (debugging findings + interview Q&A) and commit it — per the standing convention.
