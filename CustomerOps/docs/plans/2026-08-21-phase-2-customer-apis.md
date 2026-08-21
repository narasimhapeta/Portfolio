# CustomerOps Phase 2 — Customer APIs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **For this project specifically:** `CustomerOps/CLAUDE.md` §40/§41 requires guiding the human through each step interactively (small snippets, human runs commands, human writes code) rather than an agent autonomously completing tasks. Treat this plan as the reference the human works from during that walkthrough, not as a batch-execution script.

**Goal:** Build the `/api/v1/customers` CRUD + search endpoints (EF Core, SQL Server, DTOs, FluentValidation, pagination, ProblemDetails, API versioning) so `React → API → Database` works for customer data, per architecture.md §5/§8 Phase 1 (backend half).

**Architecture:** Clean Architecture across the four existing projects — `Customer` entity and rules in Domain; DTOs, `ICustomerRepository`, FluentValidation validators, and `CustomerService` in Application; EF Core `DbContext` + `CustomerRepository` in Infrastructure; versioned controller + centralized exception handling in Api. SQL Server runs locally via a dependency-only `docker-compose.yml`; EF Core migrations auto-apply on Development startup.

**Tech Stack:** .NET 10 (net10.0), EF Core (SqlServer provider), FluentValidation, Asp.Versioning.Mvc, Testcontainers.MsSql (integration/API tests), xUnit.

**Spec:** [../architecture.md](../architecture.md)

## Global Constraints

- .NET SDK 10.0.302, target framework `net10.0`
- Layering: Domain has no project references; Application references Domain only; Infrastructure references Application + Domain; Api references Application + Infrastructure — no layer is skipped or reversed
- `Customer.Id` is `Guid`, generated in `Customer.Create`, never client-supplied
- Validation lives in `CustomerService` via FluentValidation (`IValidator<T>.ValidateAsync`), not `[ApiController]` auto-validation or DataAnnotations
- `DELETE /api/v1/customers/{id}` is a soft delete — it calls `Customer.Deactivate()`, never a SQL `DELETE`
- Reads that don't mutate (`ListAsync`, `SearchAsync`) use `AsNoTracking()`; `GetByIdAsync` stays tracked because Update/Deactivate flows reuse it
- Pagination: `pageNumber` clamped to `>= 1`, `pageSize` clamped to `[1, 100]`, clamping happens in `CustomerService`
- SQL Server for local dev runs via `docker-compose.yml` at the repo root (dependency only — the app itself isn't containerized until Phase 7)
- Integration and API tests use Testcontainers.MsSql against a real SQL Server engine — no EF InMemory provider, no repository mocking framework
- No Redis, Service Bus, authentication, or SignalR in this plan — deferred per architecture.md §9
- Docker Desktop (or an equivalent Docker daemon) must be running before Task 3/Task 4 tests — Testcontainers needs it

---

### Task 1: Domain — Customer Entity

**Files:**
- Create: `CustomerOps/tests/CustomerPortal.UnitTests/` (scaffold via `dotnet new xunit`)
- Create: `src/CustomerPortal.Domain/CustomerStatus.cs`
- Create: `src/CustomerPortal.Domain/Customer.cs`
- Test: `tests/CustomerPortal.UnitTests/Domain/CustomerTests.cs`

**Interfaces:**
- Produces: `Customer.Create(string firstName, string lastName, string email, string phone) -> Customer`; instance methods `Update(string firstName, string lastName, string email, string phone)` and `Deactivate()`; properties `Id (Guid)`, `FirstName`, `LastName`, `Email`, `Phone`, `Status (CustomerStatus)`, `CreatedAt (DateTime)`, `UpdatedAt (DateTime)`. Task 2's `CustomerService` and Task 3's EF configuration depend on these exact names.

- [ ] **Step 1: Scaffold the UnitTests project**

Run from `CustomerOps/`:

```bash
dotnet new xunit -n CustomerPortal.UnitTests -o tests/CustomerPortal.UnitTests
dotnet sln add tests/CustomerPortal.UnitTests/CustomerPortal.UnitTests.csproj
dotnet add tests/CustomerPortal.UnitTests reference src/CustomerPortal.Domain
rm tests/CustomerPortal.UnitTests/UnitTest1.cs
```

- [ ] **Step 2: Write the failing Domain tests**

Create `tests/CustomerPortal.UnitTests/Domain/CustomerTests.cs`:

```csharp
using CustomerPortal.Domain;
using Xunit;

namespace CustomerPortal.UnitTests.Domain;

public class CustomerTests
{
    [Fact]
    public void Create_SetsPropertiesAndActiveStatus()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Ada", customer.FirstName);
        Assert.Equal("Lovelace", customer.LastName);
        Assert.Equal("ada@example.com", customer.Email);
        Assert.Equal("555-0100", customer.Phone);
        Assert.Equal(CustomerStatus.Active, customer.Status);
        Assert.Equal(customer.CreatedAt, customer.UpdatedAt);
    }

    [Fact]
    public void Update_ChangesFieldsAndAdvancesUpdatedAt()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");
        var originalUpdatedAt = customer.UpdatedAt;

        customer.Update("Grace", "Hopper", "grace@example.com", "555-0200");

        Assert.Equal("Grace", customer.FirstName);
        Assert.Equal("Hopper", customer.LastName);
        Assert.Equal("grace@example.com", customer.Email);
        Assert.Equal("555-0200", customer.Phone);
        Assert.True(customer.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_SetsStatusInactive()
    {
        var customer = Customer.Create("Ada", "Lovelace", "ada@example.com", "555-0100");

        customer.Deactivate();

        Assert.Equal(CustomerStatus.Inactive, customer.Status);
    }
}
```

- [ ] **Step 3: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: FAIL to build — `Customer` and `CustomerStatus` don't exist yet.

- [ ] **Step 4: Implement CustomerStatus and Customer**

Create `src/CustomerPortal.Domain/CustomerStatus.cs`:

```csharp
namespace CustomerPortal.Domain;

public enum CustomerStatus
{
    Active,
    Inactive
}
```

Create `src/CustomerPortal.Domain/Customer.cs`:

```csharp
namespace CustomerPortal.Domain;

public class Customer
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public CustomerStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Customer()
    {
    }

    public static Customer Create(string firstName, string lastName, string email, string phone)
    {
        var now = DateTime.UtcNow;
        return new Customer
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone,
            Status = CustomerStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string firstName, string lastName, string email, string phone)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = CustomerStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }
}
```

The private parameterless constructor exists for EF Core (Task 3) to materialize entities via reflection; application code must go through `Create`.

- [ ] **Step 5: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: PASS (3 tests)

- [ ] **Step 6: Commit**

```bash
git add tests/CustomerPortal.UnitTests src/CustomerPortal.Domain
git commit -m "feat: add Customer domain entity"
```

---

### Task 2: Application — DTOs, Repository Interface, Validation, CustomerService

**Files:**
- Create: `src/CustomerPortal.Application/Common/PagedResult.cs`
- Create: `src/CustomerPortal.Application/Customers/CustomerDto.cs`
- Create: `src/CustomerPortal.Application/Customers/CreateCustomerRequest.cs`
- Create: `src/CustomerPortal.Application/Customers/UpdateCustomerRequest.cs`
- Create: `src/CustomerPortal.Application/Customers/ICustomerRepository.cs`
- Create: `src/CustomerPortal.Application/Customers/CustomerNotFoundException.cs`
- Create: `src/CustomerPortal.Application/Customers/CreateCustomerRequestValidator.cs`
- Create: `src/CustomerPortal.Application/Customers/UpdateCustomerRequestValidator.cs`
- Create: `src/CustomerPortal.Application/Customers/CustomerService.cs`
- Create: `tests/CustomerPortal.UnitTests/TestDoubles/InMemoryCustomerRepository.cs`
- Test: `tests/CustomerPortal.UnitTests/Customers/CreateCustomerRequestValidatorTests.cs`
- Test: `tests/CustomerPortal.UnitTests/Customers/CustomerServiceTests.cs`

**Interfaces:**
- Consumes: `Customer.Create/Update/Deactivate` and its properties from Task 1.
- Produces: `ICustomerRepository` (`GetByIdAsync`, `ListAsync`, `SearchAsync`, `AddAsync`, `UpdateAsync`), `CustomerService` (`GetByIdAsync`, `ListAsync`, `SearchAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync`), `CustomerDto`, `PagedResult<T>`, `CreateCustomerRequest`, `UpdateCustomerRequest`, `CustomerNotFoundException`. Task 3's `CustomerRepository` implements `ICustomerRepository`; Task 4's controller and exception handler depend on `CustomerService`'s exact method signatures and on `FluentValidation.ValidationException`/`CustomerNotFoundException` being the exceptions thrown on failure.

- [ ] **Step 1: Add FluentValidation and wire up the UnitTests project**

Run from `CustomerOps/`:

```bash
dotnet add src/CustomerPortal.Application package FluentValidation
dotnet add tests/CustomerPortal.UnitTests reference src/CustomerPortal.Application
```

- [ ] **Step 2: Write the failing test double and tests**

Create `tests/CustomerPortal.UnitTests/TestDoubles/InMemoryCustomerRepository.cs`:

```csharp
using CustomerPortal.Application.Customers;
using CustomerPortal.Domain;

namespace CustomerPortal.UnitTests.TestDoubles;

public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new();

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_customers.FirstOrDefault(c => c.Id == id));

    public Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var ordered = _customers.OrderBy(c => c.LastName).ToList();
        var page = ordered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(((IReadOnlyList<Customer>)page, ordered.Count));
    }

    public Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var matches = _customers
            .Where(c => c.FirstName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.LastName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.Email.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.LastName)
            .ToList();
        var page = matches.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(((IReadOnlyList<Customer>)page, matches.Count));
    }

    public Task AddAsync(Customer customer, CancellationToken ct)
    {
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer customer, CancellationToken ct) => Task.CompletedTask;
}
```

Create `tests/CustomerPortal.UnitTests/Customers/CreateCustomerRequestValidatorTests.cs`:

```csharp
using CustomerPortal.Application.Customers;
using Xunit;

namespace CustomerPortal.UnitTests.Customers;

public class CreateCustomerRequestValidatorTests
{
    private readonly CreateCustomerRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_HasNoErrors()
    {
        var request = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMissingFirstName_HasError()
    {
        var request = new CreateCustomerRequest { FirstName = "", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerRequest.FirstName));
    }

    [Fact]
    public void Validate_WithInvalidEmail_HasError()
    {
        var request = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "not-an-email", Phone = "555-0100" };

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateCustomerRequest.Email));
    }
}
```

Create `tests/CustomerPortal.UnitTests/Customers/CustomerServiceTests.cs`:

```csharp
using CustomerPortal.Application.Customers;
using CustomerPortal.UnitTests.TestDoubles;
using FluentValidation;
using Xunit;

namespace CustomerPortal.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly CustomerService _service = new(
        new InMemoryCustomerRepository(),
        new CreateCustomerRequestValidator(),
        new UpdateCustomerRequestValidator());

    private static CreateCustomerRequest ValidCreateRequest() => new()
    {
        FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100"
    };

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsCreatedCustomer()
    {
        var result = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.Equal("Ada", result.FirstName);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ThrowsValidationException()
    {
        var request = new CreateCustomerRequest { FirstName = "", LastName = "Lovelace", Email = "ada@example.com", Phone = "555-0100" };

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ThrowsCustomerNotFoundException()
    {
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => _service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_UpdatesFields()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        var updated = await _service.UpdateAsync(
            created.Id,
            new UpdateCustomerRequest { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com", Phone = "555-0200" },
            CancellationToken.None);

        Assert.Equal("Grace", updated.FirstName);
        Assert.Equal("Hopper", updated.LastName);
    }

    [Fact]
    public async Task DeactivateAsync_SetsStatusToInactive()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        await _service.DeactivateAsync(created.Id, CancellationToken.None);
        var fetched = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal("Inactive", fetched.Status);
    }

    [Fact]
    public async Task ListAsync_ReturnsPagedResults()
    {
        for (var i = 0; i < 5; i++)
        {
            await _service.CreateAsync(
                new CreateCustomerRequest { FirstName = $"First{i}", LastName = $"Last{i}", Email = $"user{i}@example.com", Phone = "555-0000" },
                CancellationToken.None);
        }

        var page1 = await _service.ListAsync(pageNumber: 1, pageSize: 2, CancellationToken.None);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
    }

    [Fact]
    public async Task ListAsync_WithPageSizeAboveMax_ClampsToMax()
    {
        var result = await _service.ListAsync(pageNumber: 1, pageSize: 500, CancellationToken.None);

        Assert.Equal(100, result.PageSize);
    }
}
```

- [ ] **Step 3: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: FAIL to build — `PagedResult<T>`, `CustomerDto`, `CreateCustomerRequest`, `UpdateCustomerRequest`, `ICustomerRepository`, `CustomerNotFoundException`, the validators, and `CustomerService` don't exist yet.

- [ ] **Step 4: Implement the Application layer types**

Create `src/CustomerPortal.Application/Common/PagedResult.cs`:

```csharp
namespace CustomerPortal.Application.Common;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
```

Create `src/CustomerPortal.Application/Customers/CustomerDto.cs`:

```csharp
namespace CustomerPortal.Application.Customers;

public class CustomerDto
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
```

Create `src/CustomerPortal.Application/Customers/CreateCustomerRequest.cs`:

```csharp
namespace CustomerPortal.Application.Customers;

public class CreateCustomerRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
```

Create `src/CustomerPortal.Application/Customers/UpdateCustomerRequest.cs`:

```csharp
namespace CustomerPortal.Application.Customers;

public class UpdateCustomerRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
}
```

Create `src/CustomerPortal.Application/Customers/ICustomerRepository.cs`:

```csharp
using CustomerPortal.Domain;

namespace CustomerPortal.Application.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(int pageNumber, int pageSize, CancellationToken ct);
    Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
    Task UpdateAsync(Customer customer, CancellationToken ct);
}
```

Create `src/CustomerPortal.Application/Customers/CustomerNotFoundException.cs`:

```csharp
namespace CustomerPortal.Application.Customers;

public class CustomerNotFoundException(Guid id) : Exception($"Customer '{id}' was not found.");
```

Create `src/CustomerPortal.Application/Customers/CreateCustomerRequestValidator.cs`:

```csharp
using FluentValidation;

namespace CustomerPortal.Application.Customers;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}
```

Create `src/CustomerPortal.Application/Customers/UpdateCustomerRequestValidator.cs`:

```csharp
using FluentValidation;

namespace CustomerPortal.Application.Customers;

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(30);
    }
}
```

Create `src/CustomerPortal.Application/Customers/CustomerService.cs`:

```csharp
using CustomerPortal.Application.Common;
using CustomerPortal.Domain;
using FluentValidation;

namespace CustomerPortal.Application.Customers;

public class CustomerService(
    ICustomerRepository repository,
    IValidator<CreateCustomerRequest> createValidator,
    IValidator<UpdateCustomerRequest> updateValidator)
{
    private const int MaxPageSize = 100;

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        return ToDto(customer);
    }

    public async Task<PagedResult<CustomerDto>> ListAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var (normalizedPageNumber, normalizedPageSize) = NormalizePaging(pageNumber, pageSize);
        var (items, total) = await repository.ListAsync(normalizedPageNumber, normalizedPageSize, ct);
        return ToPagedResult(items, total, normalizedPageNumber, normalizedPageSize);
    }

    public async Task<PagedResult<CustomerDto>> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var (normalizedPageNumber, normalizedPageSize) = NormalizePaging(pageNumber, pageSize);
        var (items, total) = await repository.SearchAsync(query, normalizedPageNumber, normalizedPageSize, ct);
        return ToPagedResult(items, total, normalizedPageNumber, normalizedPageSize);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var customer = Customer.Create(request.FirstName, request.LastName, request.Email, request.Phone);
        await repository.AddAsync(customer, ct);
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var validation = await updateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        customer.Update(request.FirstName, request.LastName, request.Email, request.Phone);
        await repository.UpdateAsync(customer, ct);
        return ToDto(customer);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        customer.Deactivate();
        await repository.UpdateAsync(customer, ct);
    }

    private static (int PageNumber, int PageSize) NormalizePaging(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, MaxPageSize));

    private static PagedResult<CustomerDto> ToPagedResult(IReadOnlyList<Customer> items, int total, int pageNumber, int pageSize) =>
        new()
        {
            Items = items.Select(ToDto).ToList(),
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

    private static CustomerDto ToDto(Customer c) => new()
    {
        Id = c.Id,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Email = c.Email,
        Phone = c.Phone,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
```

- [ ] **Step 5: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: PASS (all Domain + Application tests)

- [ ] **Step 6: Commit**

```bash
git add src/CustomerPortal.Application tests/CustomerPortal.UnitTests
git commit -m "feat: add Customer application layer (DTOs, validation, CustomerService)"
```

---

### Task 3: Infrastructure — EF Core, SQL Server, CustomerRepository

**Files:**
- Create: `CustomerOps/docker-compose.yml`
- Modify: `src/CustomerPortal.Api/appsettings.Development.json`
- Create: `src/CustomerPortal.Infrastructure/Persistence/CustomerPortalDbContext.cs`
- Create: `src/CustomerPortal.Infrastructure/Persistence/CustomerRepository.cs`
- Create: `src/CustomerPortal.Infrastructure/Persistence/Migrations/` (generated by `dotnet ef migrations add`)
- Create: `CustomerOps/tests/CustomerPortal.IntegrationTests/` (scaffold via `dotnet new xunit`)
- Test: `tests/CustomerPortal.IntegrationTests/SqlServerFixture.cs`
- Test: `tests/CustomerPortal.IntegrationTests/CustomerRepositoryTests.cs`

**Interfaces:**
- Consumes: `Customer` (Task 1), `ICustomerRepository` (Task 2).
- Produces: `CustomerPortalDbContext` (with `DbSet<Customer> Customers`) and `CustomerRepository : ICustomerRepository`. Task 4's `Program.cs` registers both by these exact type names.

- [ ] **Step 1: Add EF Core packages and the dotnet-ef tool**

Run from `CustomerOps/`:

```bash
dotnet add src/CustomerPortal.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/CustomerPortal.Api package Microsoft.EntityFrameworkCore.Design
dotnet tool list --global
```

If `dotnet-ef` isn't in that list, install it:

```bash
dotnet tool install --global dotnet-ef
```

- [ ] **Step 2: Add the local SQL Server dependency**

Create `CustomerOps/docker-compose.yml`:

```yaml
services:
  sql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "LocalDev!2026"
    ports:
      - "1433:1433"
```

This is local-dev-only — the SA password here never leaves your machine and Azure SQL (Phase 10+) uses Managed Identity instead, per architecture.md §4.

Start it:

```bash
docker compose up -d sql
```

Add the connection string to `src/CustomerPortal.Api/appsettings.Development.json` (keep the existing `Logging` and `Cors` sections, add `ConnectionStrings`):

```json
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
    "CustomerPortal": "Server=localhost,1433;Database=CustomerPortal;User Id=sa;Password=LocalDev!2026;TrustServerCertificate=True;"
  }
}
```

- [ ] **Step 3: Scaffold the IntegrationTests project**

Run from `CustomerOps/`:

```bash
dotnet new xunit -n CustomerPortal.IntegrationTests -o tests/CustomerPortal.IntegrationTests
dotnet sln add tests/CustomerPortal.IntegrationTests/CustomerPortal.IntegrationTests.csproj
dotnet add tests/CustomerPortal.IntegrationTests reference src/CustomerPortal.Infrastructure src/CustomerPortal.Application src/CustomerPortal.Domain
dotnet add tests/CustomerPortal.IntegrationTests package Testcontainers.MsSql
rm tests/CustomerPortal.IntegrationTests/UnitTest1.cs
```

- [ ] **Step 4: Write the failing repository tests**

Create `tests/CustomerPortal.IntegrationTests/SqlServerFixture.cs`:

```csharp
using CustomerPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace CustomerPortal.IntegrationTests;

public class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();

    public CustomerPortalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CustomerPortalDbContext>()
            .UseSqlServer(_container.GetConnectionString())
            .Options;
        return new CustomerPortalDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(SqlServerCollection))]
public class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
```

Create `tests/CustomerPortal.IntegrationTests/CustomerRepositoryTests.cs`:

```csharp
using CustomerPortal.Domain;
using CustomerPortal.Infrastructure.Persistence;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(SqlServerCollection))]
public class CustomerRepositoryTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_ReturnsPersistedCustomer()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        var customer = Customer.Create("Ada", "Lovelace", "ada.repo@example.com", "555-0100");

        await repository.AddAsync(customer, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new CustomerRepository(readContext).GetByIdAsync(customer.Id, CancellationToken.None);

        Assert.NotNull(fetched);
        Assert.Equal("Ada", fetched!.FirstName);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownId_ReturnsNull()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SearchAsync_MatchesByLastNameCaseInsensitive()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        await repository.AddAsync(Customer.Create("Grace", "Hopper", "grace.search@example.com", "555-0200"), CancellationToken.None);

        var (items, total) = await repository.SearchAsync("hopper", pageNumber: 1, pageSize: 10, CancellationToken.None);

        Assert.Equal(1, total);
        Assert.Equal("Hopper", items[0].LastName);
    }

    [Fact]
    public async Task ListAsync_ReturnsRequestedPageSize()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        for (var i = 0; i < 3; i++)
        {
            await repository.AddAsync(
                Customer.Create($"First{i}", $"ListTest{i}", $"listtest{i}@example.com", "555-0300"),
                CancellationToken.None);
        }

        var (items, total) = await repository.ListAsync(pageNumber: 1, pageSize: 2, CancellationToken.None);

        Assert.True(total >= 3);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        await using var context = fixture.CreateContext();
        var repository = new CustomerRepository(context);
        var customer = Customer.Create("Ada", "Lovelace", "ada.update@example.com", "555-0100");
        await repository.AddAsync(customer, CancellationToken.None);

        customer.Update("Ada", "King", "ada.king@example.com", "555-0101");
        await repository.UpdateAsync(customer, CancellationToken.None);

        await using var readContext = fixture.CreateContext();
        var fetched = await new CustomerRepository(readContext).GetByIdAsync(customer.Id, CancellationToken.None);
        Assert.Equal("King", fetched!.LastName);
    }
}
```

`ListAsync_ReturnsRequestedPageSize` asserts `total >= 3` rather than an exact count because the fixture's container is shared across tests in this collection (no per-test reset) — `items.Count == 2` is still exact because `Take(2)` is deterministic once at least 2 rows exist.

- [ ] **Step 5: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.IntegrationTests`
Expected: FAIL to build — `CustomerPortalDbContext` and `CustomerRepository` don't exist yet. (Requires Docker running.)

- [ ] **Step 6: Implement the DbContext and repository**

Create `src/CustomerPortal.Infrastructure/Persistence/CustomerPortalDbContext.cs`:

```csharp
using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure.Persistence;

public class CustomerPortalDbContext(DbContextOptions<CustomerPortalDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

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
    }
}
```

Create `src/CustomerPortal.Infrastructure/Persistence/CustomerRepository.cs`:

```csharp
using CustomerPortal.Application.Customers;
using CustomerPortal.Domain;
using Microsoft.EntityFrameworkCore;

namespace CustomerPortal.Infrastructure.Persistence;

public class CustomerRepository(CustomerPortalDbContext context) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await context.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> ListAsync(int pageNumber, int pageSize, CancellationToken ct)
    {
        var query = context.Customers.AsNoTracking().OrderBy(c => c.LastName);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<Customer> Items, int TotalCount)> SearchAsync(string query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var matches = context.Customers.AsNoTracking()
            .Where(c => EF.Functions.Like(c.FirstName, $"%{query}%")
                     || EF.Functions.Like(c.LastName, $"%{query}%")
                     || EF.Functions.Like(c.Email, $"%{query}%"))
            .OrderBy(c => c.LastName);
        var total = await matches.CountAsync(ct);
        var items = await matches.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        context.Customers.Add(customer);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Customer customer, CancellationToken ct) =>
        await context.SaveChangesAsync(ct);
}
```

`UpdateAsync` only calls `SaveChangesAsync` because `customer` was fetched from this same (request-scoped) `DbContext` via `GetByIdAsync`, so EF Core is already tracking the mutated instance.

- [ ] **Step 7: Generate the initial migration**

Run from `CustomerOps/`:

```bash
dotnet ef migrations add InitialCreate --project src/CustomerPortal.Infrastructure --startup-project src/CustomerPortal.Api --output-dir Persistence/Migrations
```

- [ ] **Step 8: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.IntegrationTests`
Expected: PASS (5 tests) — Testcontainers pulls/starts a real SQL Server container, applies the migration, and exercises the repository against it. First run is slow (image pull); subsequent runs are faster.

- [ ] **Step 9: Commit**

```bash
git add docker-compose.yml src/CustomerPortal.Api/appsettings.Development.json src/CustomerPortal.Infrastructure tests/CustomerPortal.IntegrationTests
git commit -m "feat: add EF Core persistence for Customer"
```

---

### Task 4: Api — Versioned Controller, Validation/Error Handling, DI Wiring

**Files:**
- Modify: `src/CustomerPortal.Api/Program.cs`
- Create: `src/CustomerPortal.Api/ErrorHandling/CustomerApiExceptionHandler.cs`
- Create: `src/CustomerPortal.Api/Controllers/CustomerOperationsController.cs`
- Modify: `src/CustomerPortal.Api/CustomerPortal.Api.http`
- Create: `tests/CustomerPortal.ApiTests/CustomerApiFactory.cs`
- Test: `tests/CustomerPortal.ApiTests/CustomerEndpointsTests.cs`

**Interfaces:**
- Consumes: `CustomerService`, `ICustomerRepository`, `CustomerNotFoundException` (Task 2), `CustomerRepository`, `CustomerPortalDbContext` (Task 3).
- Produces: `GET/POST/PUT/DELETE /api/v1/customers[...]` per architecture.md §5 — this is the contract the frontend (a later, separate plan) integrates against.

- [ ] **Step 1: Add API versioning, FluentValidation DI, and Testcontainers packages**

Run from `CustomerOps/`:

```bash
dotnet add src/CustomerPortal.Api package Asp.Versioning.Mvc
dotnet add src/CustomerPortal.Api package Asp.Versioning.Mvc.ApiExplorer
dotnet add src/CustomerPortal.Api package FluentValidation.DependencyInjectionExtensions
dotnet add tests/CustomerPortal.ApiTests reference src/CustomerPortal.Infrastructure
dotnet add tests/CustomerPortal.ApiTests package Testcontainers.MsSql
```

- [ ] **Step 2: Write the failing API tests**

Create `tests/CustomerPortal.ApiTests/CustomerApiFactory.cs`:

```csharp
using CustomerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace CustomerPortal.ApiTests;

public class CustomerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<CustomerPortalDbContext>>();
            services.AddDbContext<CustomerPortalDbContext>(options =>
                options.UseSqlServer(_container.GetConnectionString()));
        });
    }

    public Task InitializeAsync() => _container.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(CustomerApiCollection))]
public class CustomerApiCollection : ICollectionFixture<CustomerApiFactory>;
```

Create `tests/CustomerPortal.ApiTests/CustomerEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CustomerPortal.Application.Common;
using CustomerPortal.Application.Customers;
using Xunit;

namespace CustomerPortal.ApiTests;

[Collection(nameof(CustomerApiCollection))]
public class CustomerEndpointsTests(CustomerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateCustomerRequest ValidCreateRequest(string suffix) => new()
    {
        FirstName = "Ada",
        LastName = $"Lovelace{suffix}",
        Email = $"ada.{suffix}@example.com",
        Phone = "555-0100"
    };

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedWithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithInvalidEmail_ReturnsValidationProblem()
    {
        var invalid = new CreateCustomerRequest { FirstName = "Ada", LastName = "Lovelace", Email = "not-an-email", Phone = "555-0100" };

        var response = await _client.PostAsJsonAsync("/api/v1/customers", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithUnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AfterCreate_ReturnsCustomer()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var response = await _client.GetAsync($"/api/v1/customers/{created!.Id}");
        var fetched = await response.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Update_ChangesCustomerFields()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var updateRequest = new UpdateCustomerRequest { FirstName = "Grace", LastName = "Hopper", Email = created!.Email, Phone = "555-0200" };
        var response = await _client.PutAsJsonAsync($"/api/v1/customers/{created.Id}", updateRequest);
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Grace", updated!.FirstName);
    }

    [Fact]
    public async Task Deactivate_ReturnsNoContentAndSetsStatusInactive()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/customers/{created!.Id}");
        var getResponse = await _client.GetAsync($"/api/v1/customers/{created.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<CustomerDto>();

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal("Inactive", fetched!.Status);
    }

    [Fact]
    public async Task Search_FiltersByLastName()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await _client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            FirstName = "Katherine", LastName = $"Johnson{suffix}", Email = $"katherine.{suffix}@example.com", Phone = "555-0300"
        });

        var response = await _client.GetAsync($"/api/v1/customers/search?query=Johnson{suffix}");
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CustomerDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(result!.Items);
    }
}
```

- [ ] **Step 3: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: FAIL — the existing `HealthEndpointTests` still pass, but `CustomerEndpointsTests` all fail with 404s (no `/api/v1/customers` routes registered yet).

- [ ] **Step 4: Implement the exception handler**

Create `src/CustomerPortal.Api/ErrorHandling/CustomerApiExceptionHandler.cs`:

```csharp
using CustomerPortal.Application.Customers;
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

            default:
                return false;
        }
    }
}
```

- [ ] **Step 5: Implement the controller**

Create `src/CustomerPortal.Api/Controllers/CustomerOperationsController.cs`:

```csharp
using Asp.Versioning;
using CustomerPortal.Application.Common;
using CustomerPortal.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace CustomerPortal.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/customers")]
public class CustomerOperationsController(CustomerService customerService) : ControllerBase
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
}
```

`Create` builds the `Location` header manually (`Created($"/api/v1/customers/{created.Id}", ...)`) instead of `CreatedAtAction`, sidestepping Asp.Versioning's route-value requirements for link generation — the URL is static and correct either way.

- [ ] **Step 6: Wire everything into Program.cs**

Full resulting `src/CustomerPortal.Api/Program.cs`:

```csharp
using Asp.Versioning;
using CustomerPortal.Api.ErrorHandling;
using CustomerPortal.Application.Customers;
using CustomerPortal.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddDbContext<CustomerPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CustomerPortal")));

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1.0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomerApiExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevelopment", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CustomerPortalDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("LocalDevelopment");
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
```

`db.Database.Migrate()` runs once at Development startup so `dotnet run` always has an up-to-date schema; `WebApplicationFactory`-based tests reuse this same path since they run with `UseEnvironment("Development")`.

- [ ] **Step 7: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: PASS (`HealthEndpointTests` + all of `CustomerEndpointsTests`)

Then re-run the full suite to make sure nothing regressed:

```bash
dotnet test
```

Expected: PASS across `CustomerPortal.UnitTests`, `CustomerPortal.IntegrationTests`, `CustomerPortal.ApiTests`.

- [ ] **Step 8: Add manual verification requests**

Append to `src/CustomerPortal.Api/CustomerPortal.Api.http` (keep the existing health request):

```http
### List customers
GET {{CustomerPortal.Api_HostAddress}}/api/v1/customers

### Create a customer
POST {{CustomerPortal.Api_HostAddress}}/api/v1/customers
Content-Type: application/json

{
  "firstName": "Ada",
  "lastName": "Lovelace",
  "email": "ada@example.com",
  "phone": "555-0100"
}

### Search customers
GET {{CustomerPortal.Api_HostAddress}}/api/v1/customers/search?query=Lovelace
```

- [ ] **Step 9: Commit**

```bash
git add src/CustomerPortal.Api tests/CustomerPortal.ApiTests
git commit -m "feat: add versioned Customer CRUD/search endpoints"
```

- [ ] **Step 10: Manual end-to-end verification**

Terminal 1, from `CustomerOps/`:

```bash
docker compose up -d sql
dotnet run --project src/CustomerPortal.Api
```

Terminal 2, from `CustomerOps/`: open `src/CustomerPortal.Api/CustomerPortal.Api.http` in an editor with a REST client (or use `curl`) and run the Create, List, and Search requests in order.

**Expected result:** Create returns `201 Created` with a `Location` header and the new customer's JSON; List returns a `PagedResult` containing that customer; Search with `query=Lovelace` returns it filtered. Posting a request with an invalid email returns `400` with a `ValidationProblemDetails` body; `GET` on a random GUID returns `404` with a `ProblemDetails` body.

---

## Self-Review Notes

- **Spec coverage:** architecture.md §5 endpoints — all six covered (List, GetById, Search, Create, Update, Deactivate-via-DELETE). §7 tech choices (SQL Server local, EF Core) — satisfied via docker-compose + EF Core SqlServer. §10 Customer schema (Id, FirstName, LastName, Email, Phone, Status, CreatedAt, UpdatedAt) — matches `Customer` entity exactly. CLAUDE.md §10 `AsNoTracking()`/projections for reads — applied in `ListAsync`/`SearchAsync`; `GetByIdAsync` stays tracked with the reason stated in Global Constraints. Frontend CRUD UI is intentionally out of scope here, same split Phase 1 used for foundation vs. CRUD — it's a follow-up plan.
- **Placeholder scan:** no TBD/TODO; every step has literal file contents or exact commands.
- **Type consistency:** `ICustomerRepository` method signatures match between Task 2's interface, the Task 2 in-memory test double, and the Task 3 `CustomerRepository` implementation. `CustomerService` method names/signatures match between Task 2's implementation and Task 4's controller calls. `CustomerDto`/`PagedResult<T>`/`CreateCustomerRequest`/`UpdateCustomerRequest` property names match across Application, the ApiTests JSON deserialization, and the `.http` file's request body. `CustomerPortalDbContext`/`CustomerRepository` names match between Task 3's implementation and Task 4's `Program.cs` DI registration and `CustomerApiFactory`'s `RemoveAll<DbContextOptions<CustomerPortalDbContext>>()`.
