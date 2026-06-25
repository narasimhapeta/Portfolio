using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveCoverages;

public class SaveCoveragesCommandHandler : IRequestHandler<SaveCoveragesCommand, Result<decimal>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCoveragesCommandHandler(IQuoteRepository quoteRepository, IUnitOfWork unitOfWork)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<decimal>> Handle(SaveCoveragesCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetWithCoveragesAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result<decimal>.Failure("Quote not found.");

        if (quote.Status == QuoteStatus.Bound)
            return Result<decimal>.Failure("Cannot modify a bound quote.");

        var coverageTypes = await _quoteRepository.GetCoverageTypesAsync(cancellationToken);
        var typeMap = coverageTypes.ToDictionary(ct => ct.Id);

        quote.Coverages.Clear();
        decimal totalAnnual = 0;

        foreach (var dto in request.Coverages)
        {
            if (!typeMap.TryGetValue(dto.CoverageTypeId, out var coverageType))
                return Result<decimal>.Failure($"Unknown coverage type: {dto.CoverageTypeId}");

            var annualPremium = coverageType.MockAnnualRate;
            totalAnnual += annualPremium;

            quote.Coverages.Add(new QuoteCoverage
            {
                QuoteId = quote.Id,
                CoverageTypeId = dto.CoverageTypeId,
                LimitOption = dto.LimitOption,
                Deductible = dto.Deductible,
                AnnualPremium = annualPremium
            });
        }

        quote.Status = QuoteStatus.Review;
        quote.UpdatedAt = DateTime.UtcNow;
        _quoteRepository.Update(quote);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<decimal>.Success(totalAnnual);
    }
}
