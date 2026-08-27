# CustomerOps Phase 3 — Redis Cache-Aside Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **For this project specifically:** `CustomerOps/CLAUDE.md` §40/§41 requires guiding the human through each step interactively (small snippets, human runs commands, human writes code) rather than an agent autonomously completing tasks. Treat this plan as the reference the human works from during that walkthrough, not as a batch-execution script.

**Goal:** Add a Redis cache-aside layer in front of `CustomerService.GetByIdAsync` — read-through on miss, invalidate-on-write, and graceful degradation to SQL when Redis is unavailable — per architecture.md §8 Phase 2 and CLAUDE.md §11.

**Architecture:** `IDistributedCache` (Microsoft.Extensions.Caching.StackExchangeRedis) is registered in the Api composition root, config-driven so the local container swaps for Azure Cache for Redis later without a code change. `CustomerService` (Application layer) gains it as a constructor dependency alongside the existing `ICustomerRepository`: `GetByIdAsync` checks the cache first, falls back to the repository on miss and populates the cache; `UpdateAsync`/`DeactivateAsync` invalidate the entry after a successful SQL write. Every cache operation is wrapped so a Redis outage degrades to direct-SQL behavior with a logged warning, never an exception.

**Tech Stack:** Microsoft.Extensions.Caching.StackExchangeRedis (`IDistributedCache`), System.Text.Json for DTO serialization, Testcontainers.Redis (integration/API tests), `redis:7-alpine` for local dev.

**Spec:** [../architecture.md](../architecture.md) §7/§8 Phase 2; `CustomerOps/CLAUDE.md` §11.

## Global Constraints

- .NET SDK 10.0.302, target framework `net10.0` (unchanged from Phase 2)
- Cache-aside applies to `CustomerService.GetByIdAsync` only — `ListAsync`/`SearchAsync` stay SQL-only this phase (pagination/filter cache keys are out of scope, per the approved design)
- Cache key format: `customer:{id}` where `{id}` is `Guid.ToString()` (default format, no braces/dashes stripped)
- TTL: fixed 5 minutes via `DistributedCacheEntryOptions.AbsoluteExpirationRelativeToNow` — no sliding expiration
- Invalidate-on-write: `UpdateAsync` and `DeactivateAsync` call `RemoveAsync` on the cache key after the SQL write succeeds; `CreateAsync` never pre-populates the cache (lazy-fill on first read)
- Application code depends on `IDistributedCache` only — never `IConnectionMultiplexer`/`StackExchange.Redis` types directly (keeps Infrastructure swappable, per architecture.md §4)
- Redis is never a hard dependency for read/write availability: every cache operation in `CustomerService` is wrapped in try/catch, logs a warning via `ILogger<CustomerService>`, and falls through to the SQL repository path
- Local Redis runs via `docker-compose.yml` (`redis:7-alpine`, port 6379), dependency-only — same pattern as the existing `sql` service
- No Polly/retry/circuit-breaker policies yet — that's the Testing + Resiliency phase (architecture.md §8 Phase 5); this phase's failure handling is a plain try/catch
- No write-through, no cache warming, no caching of `ListAsync`/`SearchAsync` — explicitly deferred per the approved design

---

### Task 1: Infrastructure — Redis Dependency, Package Wiring, `IDistributedCache` Registration

**Files:**
- Modify: `docker-compose.yml`
- Modify: `src/CustomerPortal.Api/appsettings.Development.json`
- Modify: `src/CustomerPortal.Api/Program.cs`
- Test: `tests/CustomerPortal.IntegrationTests/RedisFixture.cs`
- Test: `tests/CustomerPortal.IntegrationTests/RedisCacheTests.cs`

**Interfaces:**
- Produces: `IDistributedCache` registered in the Api's DI container, config-driven via `ConnectionStrings:Redis`. Task 2's `CustomerService` consumes it by constructor injection under this exact type.

- [ ] **Step 1: Add packages**

Run from `CustomerOps/`:

```bash
dotnet add src/CustomerPortal.Api package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add src/CustomerPortal.Application package Microsoft.Extensions.Caching.Abstractions
dotnet add src/CustomerPortal.Application package Microsoft.Extensions.Logging.Abstractions
dotnet add tests/CustomerPortal.IntegrationTests package Testcontainers.Redis
dotnet add tests/CustomerPortal.IntegrationTests package Microsoft.Extensions.Caching.StackExchangeRedis
```

`CustomerPortal.Application` is a plain class library (not the ASP.NET Core SDK), so `IDistributedCache` and `ILogger<T>` need explicit package references — the Api project gets these for free via `Microsoft.NET.Sdk.Web`.

- [ ] **Step 2: Add Redis to the local dependency stack**

Replace `CustomerOps/docker-compose.yml` with:

```yaml
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
```

Start it:

```bash
docker compose up -d redis
```

- [ ] **Step 3: Add the Redis connection string**

In `src/CustomerPortal.Api/appsettings.Development.json`, add `"Redis"` alongside the existing `"CustomerPortal"` entry under `ConnectionStrings` (keep `Logging`/`Cors` untouched):

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
    "CustomerPortal": "Server=localhost,1433;Database=CustomerPortal;User Id=sa;Password=LocalDev!2026;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  }
}
```

- [ ] **Step 4: Register `IDistributedCache` in `Program.cs`**

In `src/CustomerPortal.Api/Program.cs`, add this immediately after the existing `builder.Services.AddDbContext<CustomerPortalDbContext>(...)` block, before `builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();`:

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
```

- [ ] **Step 5: Write a Redis wiring smoke test**

This test validates the container + client library independently of any of our application code — it's infrastructure validation, not TDD against a type we're about to write, so there's no red/green cycle here.

Create `tests/CustomerPortal.IntegrationTests/RedisFixture.cs`:

```csharp
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using Testcontainers.Redis;
using Xunit;

namespace CustomerPortal.IntegrationTests;

public class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder().Build();

    public IDistributedCache CreateCache() =>
        new RedisCache(Options.Create(new RedisCacheOptions { Configuration = _container.GetConnectionString() }));

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(RedisCollection))]
public class RedisCollection : ICollectionFixture<RedisFixture>;
```

Create `tests/CustomerPortal.IntegrationTests/RedisCacheTests.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Xunit;

namespace CustomerPortal.IntegrationTests;

[Collection(nameof(RedisCollection))]
public class RedisCacheTests(RedisFixture fixture)
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        var cache = fixture.CreateCache();
        var key = $"test:{Guid.NewGuid()}";
        var value = Encoding.UTF8.GetBytes("hello-redis");

        await cache.SetAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });
        var fetched = await cache.GetAsync(key);

        Assert.Equal(value, fetched);
    }

    [Fact]
    public async Task RemoveAsync_DeletesTheKey()
    {
        var cache = fixture.CreateCache();
        var key = $"test:{Guid.NewGuid()}";
        await cache.SetAsync(key, Encoding.UTF8.GetBytes("value"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        });

        await cache.RemoveAsync(key);
        var fetched = await cache.GetAsync(key);

        Assert.Null(fetched);
    }
}
```

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.IntegrationTests`
Expected: PASS — including the two new `RedisCacheTests`, plus the existing `CustomerRepositoryTests`. (Requires Docker running; first run pulls the `redis:7-alpine` image.)

- [ ] **Step 7: Commit**

```bash
git add docker-compose.yml src/CustomerPortal.Api/appsettings.Development.json src/CustomerPortal.Api/Program.cs src/CustomerPortal.Api/CustomerPortal.Api.csproj src/CustomerPortal.Application/CustomerPortal.Application.csproj tests/CustomerPortal.IntegrationTests
git commit -m "feat: wire Redis dependency and register IDistributedCache"
```

---

### Task 2: Application — Cache-Aside in `CustomerService`

**Files:**
- Modify: `src/CustomerPortal.Application/Customers/CustomerService.cs`
- Modify: `tests/CustomerPortal.UnitTests/Customers/CustomerServiceTests.cs`
- Modify: `tests/CustomerPortal.UnitTests/TestDoubles/InMemoryCustomerRepository.cs`
- Test: `tests/CustomerPortal.UnitTests/TestDoubles/FakeDistributedCache.cs`
- Test: `tests/CustomerPortal.UnitTests/TestDoubles/ThrowingDistributedCache.cs`

**Interfaces:**
- Consumes: `IDistributedCache` (Task 1's registration, `Microsoft.Extensions.Caching.Distributed` namespace).
- Produces: `CustomerService`'s constructor becomes `CustomerService(ICustomerRepository repository, IDistributedCache cache, ILogger<CustomerService> logger, IValidator<CreateCustomerRequest> createValidator, IValidator<UpdateCustomerRequest> updateValidator)`. `Program.cs`'s existing `AddScoped<CustomerService>()` resolves this automatically (both new dependencies are already DI-registered) — no `Program.cs` change needed in this task. Any future caller must construct `CustomerService` with this exact parameter order.

- [ ] **Step 1: Add a call counter to the in-memory repository test double**

Modify `tests/CustomerPortal.UnitTests/TestDoubles/InMemoryCustomerRepository.cs` — add a public counter and increment it in `GetByIdAsync`, so tests can assert a cache hit skipped the repository:

```csharp
using CustomerPortal.Application.Customers;
using CustomerPortal.Domain;

namespace CustomerPortal.UnitTests.TestDoubles;

public class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new();

    public int GetByIdCallCount { get; private set; }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        GetByIdCallCount++;
        return Task.FromResult(_customers.FirstOrDefault(c => c.Id == id));
    }

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

- [ ] **Step 2: Create the cache test doubles**

Create `tests/CustomerPortal.UnitTests/TestDoubles/FakeDistributedCache.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace CustomerPortal.UnitTests.TestDoubles;

public class FakeDistributedCache : IDistributedCache
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new();

    public bool ContainsKey(string key) => _store.ContainsKey(key);

    public byte[]? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Task.FromResult(Get(key));

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => _store.TryRemove(key, out _);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}
```

Create `tests/CustomerPortal.UnitTests/TestDoubles/ThrowingDistributedCache.cs` (used to prove graceful degradation when Redis is unreachable):

```csharp
using Microsoft.Extensions.Caching.Distributed;

namespace CustomerPortal.UnitTests.TestDoubles;

public class ThrowingDistributedCache : IDistributedCache
{
    public byte[]? Get(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Refresh(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Remove(string key) => throw new InvalidOperationException("Redis unavailable");

    public Task RemoveAsync(string key, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        throw new InvalidOperationException("Redis unavailable");

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
        throw new InvalidOperationException("Redis unavailable");
}
```

- [ ] **Step 3: Rewrite `CustomerServiceTests.cs` with the new constructor and cache-behavior tests**

Replace `tests/CustomerPortal.UnitTests/Customers/CustomerServiceTests.cs` in full:

```csharp
using CustomerPortal.Application.Customers;
using CustomerPortal.UnitTests.TestDoubles;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerPortal.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly InMemoryCustomerRepository _repository = new();
    private readonly FakeDistributedCache _cache = new();
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(
            _repository,
            _cache,
            NullLogger<CustomerService>.Instance,
            new CreateCustomerRequestValidator(),
            new UpdateCustomerRequestValidator());
    }

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

    [Fact]
    public async Task GetByIdAsync_OnCacheMiss_PopulatesCache()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.True(_cache.ContainsKey($"customer:{created.Id}"));
    }

    [Fact]
    public async Task GetByIdAsync_OnCacheHit_DoesNotCallRepositoryAgain()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);
        await _service.GetByIdAsync(created.Id, CancellationToken.None);
        var callsAfterFirstRead = _repository.GetByIdCallCount;

        var second = await _service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal(callsAfterFirstRead, _repository.GetByIdCallCount);
        Assert.Equal(created.Id, second.Id);
    }

    [Fact]
    public async Task UpdateAsync_InvalidatesCache()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);
        await _service.GetByIdAsync(created.Id, CancellationToken.None);

        await _service.UpdateAsync(
            created.Id,
            new UpdateCustomerRequest { FirstName = "Grace", LastName = "Hopper", Email = "grace@example.com", Phone = "555-0200" },
            CancellationToken.None);

        Assert.False(_cache.ContainsKey($"customer:{created.Id}"));
    }

    [Fact]
    public async Task DeactivateAsync_InvalidatesCache()
    {
        var created = await _service.CreateAsync(ValidCreateRequest(), CancellationToken.None);
        await _service.GetByIdAsync(created.Id, CancellationToken.None);

        await _service.DeactivateAsync(created.Id, CancellationToken.None);

        Assert.False(_cache.ContainsKey($"customer:{created.Id}"));
    }

    [Fact]
    public async Task GetByIdAsync_WhenCacheThrows_FallsBackToRepository()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(
            repository,
            new ThrowingDistributedCache(),
            NullLogger<CustomerService>.Instance,
            new CreateCustomerRequestValidator(),
            new UpdateCustomerRequestValidator());
        var created = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        var fetched = await service.GetByIdAsync(created.Id, CancellationToken.None);

        Assert.Equal(created.Id, fetched.Id);
    }
}
```

- [ ] **Step 4: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: FAIL to build — `CustomerService`'s constructor doesn't accept `IDistributedCache`/`ILogger<CustomerService>` yet.

- [ ] **Step 5: Implement cache-aside in `CustomerService`**

Replace `src/CustomerPortal.Application/Customers/CustomerService.cs` in full:

```csharp
using System.Text.Json;
using CustomerPortal.Application.Common;
using CustomerPortal.Domain;
using FluentValidation;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CustomerPortal.Application.Customers;

public class CustomerService(
    ICustomerRepository repository,
    IDistributedCache cache,
    ILogger<CustomerService> logger,
    IValidator<CreateCustomerRequest> createValidator,
    IValidator<UpdateCustomerRequest> updateValidator)
{
    private const int MaxPageSize = 100;

    private static readonly DistributedCacheEntryOptions CacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = CacheKey(id);
        var cached = await TryGetCachedAsync(cacheKey, ct);
        if (cached is not null)
        {
            return cached;
        }

        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        var dto = ToDto(customer);
        await TrySetCachedAsync(cacheKey, dto, ct);
        return dto;
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
        await TryRemoveCachedAsync(CacheKey(id), ct);
        return ToDto(customer);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        customer.Deactivate();
        await repository.UpdateAsync(customer, ct);
        await TryRemoveCachedAsync(CacheKey(id), ct);
    }

    private static string CacheKey(Guid id) => $"customer:{id}";

    private async Task<CustomerDto?> TryGetCachedAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(cacheKey, ct);
            return bytes is null ? null : JsonSerializer.Deserialize<CustomerDto>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis read failed for cache key {CacheKey}; falling back to the database", cacheKey);
            return null;
        }
    }

    private async Task TrySetCachedAsync(string cacheKey, CustomerDto dto, CancellationToken ct)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(dto);
            await cache.SetAsync(cacheKey, bytes, CacheEntryOptions, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis write failed for cache key {CacheKey}; continuing without caching this read", cacheKey);
        }
    }

    private async Task TryRemoveCachedAsync(string cacheKey, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync(cacheKey, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis invalidation failed for cache key {CacheKey}; the cache may serve a stale value until it expires", cacheKey);
        }
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

- [ ] **Step 6: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.UnitTests`
Expected: PASS (12 tests — the 7 pre-existing plus 5 new cache-behavior tests).

Do **not** run the full solution (`dotnet test` at the repo root) yet — `CustomerPortal.ApiTests` boots the real `Program.cs`, which now registers a real Redis client pointed at `localhost:6379`; without Task 3's Testcontainers wiring in `CustomerApiFactory`, those tests will fail or hang trying to reach a Redis instance that isn't guaranteed to be running.

- [ ] **Step 7: Commit**

```bash
git add src/CustomerPortal.Application tests/CustomerPortal.UnitTests
git commit -m "feat: add Redis cache-aside to CustomerService.GetByIdAsync"
```

---

### Task 3: Api/Integration — End-to-End Cache Verification

**Files:**
- Modify: `tests/CustomerPortal.ApiTests/CustomerApiFactory.cs`
- Test: `tests/CustomerPortal.ApiTests/CustomerCacheTests.cs`

**Interfaces:**
- Consumes: `CustomerApiFactory` (existing, from Phase 2 Task 4), cache-aside behavior from Task 2.
- Produces: nothing further downstream — this is the last task of the phase.

- [ ] **Step 1: Add test packages**

Run from `CustomerOps/`:

```bash
dotnet add tests/CustomerPortal.ApiTests package Testcontainers.Redis
dotnet add tests/CustomerPortal.ApiTests package StackExchange.Redis
```

- [ ] **Step 2: Write the failing cache tests and update the factory**

Replace `tests/CustomerPortal.ApiTests/CustomerApiFactory.cs` in full — it now starts a Redis container alongside SQL Server and overrides `IDistributedCache` to point at it, and exposes the connection string so tests can inspect Redis directly:

```csharp
using CustomerPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace CustomerPortal.ApiTests;

public class CustomerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    private readonly RedisContainer _redisContainer = new RedisBuilder().Build();

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
        });
    }

    public Task InitializeAsync() => Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync());

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(CustomerApiCollection))]
public class CustomerApiCollection : ICollectionFixture<CustomerApiFactory>;
```

Create `tests/CustomerPortal.ApiTests/CustomerCacheTests.cs`:

```csharp
using System.Net.Http.Json;
using CustomerPortal.Application.Customers;
using StackExchange.Redis;
using Xunit;

namespace CustomerPortal.ApiTests;

[Collection(nameof(CustomerApiCollection))]
public class CustomerCacheTests(CustomerApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateCustomerRequest ValidCreateRequest(string suffix) => new()
    {
        FirstName = "Ada",
        LastName = $"Lovelace{suffix}",
        Email = $"ada.cache.{suffix}@example.com",
        Phone = "555-0100"
    };

    private async Task<bool> CacheKeyExistsAsync(Guid customerId)
    {
        await using var redis = await ConnectionMultiplexer.ConnectAsync(factory.RedisConnectionString);
        return await redis.GetDatabase().KeyExistsAsync($"customer:{customerId}");
    }

    [Fact]
    public async Task GetById_PopulatesRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();

        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        Assert.True(await CacheKeyExistsAsync(created.Id));
    }

    [Fact]
    public async Task Update_RemovesEntryFromRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();
        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        await _client.PutAsJsonAsync($"/api/v1/customers/{created.Id}", new UpdateCustomerRequest
        {
            FirstName = "Grace", LastName = "Hopper", Email = created.Email, Phone = "555-0200"
        });

        Assert.False(await CacheKeyExistsAsync(created.Id));
    }

    [Fact]
    public async Task Deactivate_RemovesEntryFromRedisCache()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", ValidCreateRequest(Guid.NewGuid().ToString("N")));
        var created = await createResponse.Content.ReadFromJsonAsync<CustomerDto>();
        await _client.GetAsync($"/api/v1/customers/{created!.Id}");

        await _client.DeleteAsync($"/api/v1/customers/{created.Id}");

        Assert.False(await CacheKeyExistsAsync(created.Id));
    }
}
```

- [ ] **Step 3: Run the tests and verify they fail**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: FAIL to build — `CustomerApiFactory.RedisConnectionString` doesn't exist on the old factory yet.

(After replacing `CustomerApiFactory.cs` per Step 2, this becomes moot since the file already has the property — if you're following strict red/green, run the tests once before editing the factory to see the build failure, then apply the Step 2 factory change.)

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/CustomerPortal.ApiTests`
Expected: PASS — all pre-existing `CustomerEndpointsTests` plus the 3 new `CustomerCacheTests`. (Requires Docker; spins up both a SQL Server and a Redis container per test run.)

- [ ] **Step 5: Run the full solution test suite**

Run from `CustomerOps/`:

```bash
dotnet test
```

Expected: PASS across `CustomerPortal.UnitTests`, `CustomerPortal.IntegrationTests`, `CustomerPortal.ApiTests`.

- [ ] **Step 6: Commit**

```bash
git add tests/CustomerPortal.ApiTests
git commit -m "feat: verify Redis cache-aside end-to-end via ApiTests"
```

- [ ] **Step 7: Manual end-to-end verification**

Terminal 1, from `CustomerOps/`:

```bash
docker compose up -d sql redis
dotnet run --project src/CustomerPortal.Api
```

Terminal 2, from `CustomerOps/`: use `src/CustomerPortal.Api/CustomerPortal.Api.http` (or `curl`) to Create a customer, then GetById it.

Terminal 3, inspect Redis directly:

```bash
docker exec -it customerops-redis-1 redis-cli KEYS "customer:*"
docker exec -it customerops-redis-1 redis-cli HGETALL "customer:<the-id-from-step-above>"
```

**Expected result:** the key exists and `HGETALL` returns three hash fields — `data` (the serialized `CustomerDto` JSON), `absexp`, and `sldexp`. Plain `redis-cli GET` on this key fails with `WRONGTYPE` — `Microsoft.Extensions.Caching.StackExchangeRedis` stores every entry as a Redis hash, not a string, so hash commands (`HGETALL`/`HGET ... data`) are required to inspect it. Now `PUT` an update to that customer via the `.http` file, and re-run `KEYS "customer:*"` — the key should be gone (invalidated). `GET` the customer again via the API — the key reappears (repopulated from SQL on the next read).

Optional: stop the `redis` container (`docker compose stop redis`) and `GET` the customer again — the API should still return `200 OK` with the customer data (falling back to SQL), not an error, demonstrating the graceful-degradation path from Task 2.

---

## Self-Review Notes

- **Spec coverage:** CLAUDE.md §11's checklist — cache-aside ✓ (Task 2), TTL ✓ (5 min, Task 2), cache invalidation ✓ (Update/Deactivate, Task 2), cache miss handling ✓ (Task 2's `TryGetCachedAsync` returning `null`), cache failure graceful degradation ✓ (Task 2's try/catch + Task 3 Step 7's manual "stop Redis" check), "not a hard dependency for basic availability" ✓ (same). architecture.md §4's config-driven local→Azure swap ✓ (Task 1's `ConnectionStrings:Redis`, no environment branching in code).
- **Placeholder scan:** no TBD/TODO; every step has literal file contents or exact commands.
- **Type consistency:** `CustomerService`'s constructor signature (`ICustomerRepository, IDistributedCache, ILogger<CustomerService>, IValidator<CreateCustomerRequest>, IValidator<UpdateCustomerRequest>`) matches between Task 2's implementation and Task 2's own `CustomerServiceTests.cs` call sites — no other production code constructs `CustomerService` directly (it's DI-resolved in `Program.cs` via the existing `AddScoped<CustomerService>()`, unchanged). `FakeDistributedCache`/`ThrowingDistributedCache` both implement the full `IDistributedCache` interface used by `CustomerService`. `CustomerApiFactory.RedisConnectionString` (Task 3) matches its usage in `CustomerCacheTests.cs`. Cache key format `customer:{id}` is identical across `CustomerService.CacheKey` (Task 2) and `CustomerCacheTests`' `CacheKeyExistsAsync` (Task 3).

---

## Lessons Learned

Captured from debugging findings hit while executing this plan and from the CLAUDE.md §46 interview checkpoint at the end of the phase.

### Debugging findings

- **`Microsoft.Extensions.Caching.StackExchangeRedis` stores entries as Redis hashes, not strings.** Manually inspecting a cached key with `redis-cli GET "customer:<id>"` fails with `(error) WRONGTYPE Operation against a key holding the wrong kind of value`. The library's `RedisCache` implementation writes each entry via `HSET`, with three fields: `data` (the byte payload — our serialized `CustomerDto` JSON), `absexp` (absolute expiration, as ticks), and `sldexp` (sliding expiration, as ticks — unused here since we only set `AbsoluteExpirationRelativeToNow`). `HGETALL "customer:<id>"` (or `HGET "customer:<id>" data` for just the payload) is the correct inspection command. This didn't affect `CustomerCacheTests` because it asserts via `KeyExistsAsync`, which is type-agnostic — a test that tried `StringGetAsync` instead would have hit the same `WRONGTYPE` error. Take-away for future plans: when a plan's manual-verification steps show raw `redis-cli` commands against a key written through `IDistributedCache`, use hash commands (`HGETALL`/`HGET ... data`), not `GET` — this is now corrected in Task 3 Step 7 above.

### Interview checkpoint Q&A

**Q1. Why does `CustomerService` depend on `IDistributedCache` rather than injecting `IConnectionMultiplexer`/`StackExchange.Redis` directly?**
Answer given: `IDistributedCache` is used to manage the cache across application servers, whereas `IConnectionMultiplexer`/`StackExchange.Redis` are used to connect to or set up Redis.
Assessment: corrected — `IDistributedCache` doesn't itself coordinate anything across servers; it's just an interface. The actual reasons mirror Phase 2's `ICustomerRepository` rationale (its own Q1): **testability** (`FakeDistributedCache`/`ThrowingDistributedCache` let `CustomerServiceTests` exercise hit/miss/failure behavior with zero real Redis, versus mocking Redis's much larger native API) and **swappability** (`IDistributedCache` has other backends — SQL Server, in-memory, Azure Cache for Redis via the same client — so the Phase 10 Azure move is a `Program.cs` connection-string change, not a `CustomerService` change). `IConnectionMultiplexer` is still the right tool when Redis-specific features are needed beyond the `IDistributedCache` contract — which is exactly why `CustomerCacheTests` used it directly, since "does this specific key exist" isn't part of that interface.

**Q2. Walk through exactly what happens inside `GetByIdAsync` if Redis is completely unreachable — where does it fail, and how does it still return `200 OK`?**
Answer given: if Redis is unreachable it returns null, and a null cached value falls back to the database, returning 200.
Assessment: correct on the outcome, sharpened on the mechanism — Redis doesn't return null when unreachable; the client **throws** (e.g. a connection exception) when `cache.GetAsync` is called. Inside `TryGetCachedAsync`'s `try`/`catch`, that exception is caught, a warning is logged, and the method returns `null` regardless of whether the real cause was "key not found" (genuine miss) or "Redis is down" (failure). `GetByIdAsync` can't tell these two apart and doesn't need to — both fall through to `repository.GetByIdAsync` identically, which is why no `if (redisIsDown)` branch exists anywhere in the service.

**Q3. Why does `CustomerApiFactory.ConfigureWebHost` call `services.RemoveAll<IDistributedCache>()` before calling `AddStackExchangeRedisCache` again, instead of just calling it once with the test container's connection string?**
Answer given: it clears any stale cache data.
Assessment: corrected — `RemoveAll<IDistributedCache>()` removes a *service registration* from the DI container at host-startup time; there's no cached data involved yet at that point. The real issue: `Program.cs` already registers `IDistributedCache` once, pointed at `localhost:6379` (the Development config value). Without `RemoveAll`, calling `AddStackExchangeRedisCache` again would leave **two** registrations in the container — the app's real one and the test one. .NET's default container happens to resolve a single dependency to the *last* registration, so this might work by luck of ordering, but that's an implicit guarantee, not an explicit one, and any code resolving `IEnumerable<IDistributedCache>` would see both (including one pointed at a `localhost:6379` that may not be running in CI). `RemoveAll` makes the test host hermetic — exactly the same reasoning already applied to `RemoveAll<DbContextOptions<CustomerPortalDbContext>>()` for SQL Server back in Phase 2.
