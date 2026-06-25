using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.CustomerService.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly CustomerServiceDbContext _context;

    public UnitOfWork(CustomerServiceDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
        => _context.Dispose();
}
