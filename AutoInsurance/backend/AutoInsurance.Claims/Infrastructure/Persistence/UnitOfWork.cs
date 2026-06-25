using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.Claims.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ClaimsDbContext _context;

    public UnitOfWork(ClaimsDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
        => _context.Dispose();
}
