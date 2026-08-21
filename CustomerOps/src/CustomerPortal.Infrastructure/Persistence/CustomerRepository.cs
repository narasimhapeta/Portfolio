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
