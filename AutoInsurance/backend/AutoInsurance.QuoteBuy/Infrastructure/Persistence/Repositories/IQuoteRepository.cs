using AutoInsurance.Domain.Policy;
using AutoInsurance.Domain.Quote;
using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;

public interface IQuoteRepository : IRepository<Quote>
{
    Task<Quote?> GetWithDriversAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetWithVehiclesAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetWithCoveragesAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetWithDraftAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetFullQuoteAsync(Guid quoteId, CancellationToken cancellationToken = default);
    Task<Quote?> GetByQuoteNumberAsync(string quoteNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoverageType>> GetCoverageTypesAsync(CancellationToken cancellationToken = default);
    Task AddPolicyAsync(Policy policy, CancellationToken cancellationToken = default);
}
