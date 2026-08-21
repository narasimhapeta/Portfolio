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
