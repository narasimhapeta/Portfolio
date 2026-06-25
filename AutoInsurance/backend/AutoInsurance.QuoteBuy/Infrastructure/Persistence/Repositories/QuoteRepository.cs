using AutoInsurance.Domain.Policy;
using AutoInsurance.Domain.Quote;
using AutoInsurance.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;

public class QuoteRepository : IQuoteRepository
{
    private readonly QuoteBuyDbContext _context;

    public QuoteRepository(QuoteBuyDbContext context)
    {
        _context = context;
    }

    public async Task<Quote?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Quotes.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Quotes.ToListAsync(cancellationToken);

    public async Task AddAsync(Quote entity, CancellationToken cancellationToken = default)
        => await _context.Quotes.AddAsync(entity, cancellationToken);

    public void Update(Quote entity)
        => _context.Quotes.Update(entity);

    public void Delete(Quote entity)
        => _context.Quotes.Remove(entity);

    public async Task<Quote?> GetWithDriversAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => await _context.Quotes.Include(q => q.Drivers)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public async Task<Quote?> GetWithVehiclesAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => await _context.Quotes.Include(q => q.Vehicles)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public async Task<Quote?> GetWithCoveragesAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => await _context.Quotes.Include(q => q.Coverages)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public async Task<Quote?> GetWithDraftAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => await _context.Quotes.Include(q => q.Draft)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public async Task<Quote?> GetFullQuoteAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => await _context.Quotes
            .Include(q => q.Drivers)
            .Include(q => q.Vehicles)
            .Include(q => q.Coverages)
            .Include(q => q.Draft)
            .FirstOrDefaultAsync(q => q.Id == quoteId, cancellationToken);

    public async Task<Quote?> GetByQuoteNumberAsync(string quoteNumber, CancellationToken cancellationToken = default)
        => await _context.Quotes.Include(q => q.Draft)
            .FirstOrDefaultAsync(q => q.QuoteNumber == quoteNumber, cancellationToken);

    public async Task<IReadOnlyList<CoverageType>> GetCoverageTypesAsync(CancellationToken cancellationToken = default)
        => await _context.CoverageTypes.ToListAsync(cancellationToken);

    public async Task AddPolicyAsync(Policy policy, CancellationToken cancellationToken = default)
        => await _context.Policies.AddAsync(policy, cancellationToken);
}
