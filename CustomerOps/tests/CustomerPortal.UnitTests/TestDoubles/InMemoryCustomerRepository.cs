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
