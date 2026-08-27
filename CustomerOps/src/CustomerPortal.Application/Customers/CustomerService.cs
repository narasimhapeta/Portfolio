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
