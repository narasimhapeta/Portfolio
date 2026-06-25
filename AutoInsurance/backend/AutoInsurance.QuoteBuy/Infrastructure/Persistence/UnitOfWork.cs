using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.QuoteBuy.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly QuoteBuyDbContext _context;

    public UnitOfWork(QuoteBuyDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
        => _context.Dispose();
}
