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
