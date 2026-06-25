using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.DocumentGeneration.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly DocumentDbContext _context;

    public UnitOfWork(DocumentDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public void Dispose()
        => _context.Dispose();
}
