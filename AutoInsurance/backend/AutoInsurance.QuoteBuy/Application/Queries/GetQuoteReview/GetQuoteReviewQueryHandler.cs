using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Queries.GetQuoteReview;

public class GetQuoteReviewQueryHandler : IRequestHandler<GetQuoteReviewQuery, Result<QuoteReviewDto>>
{
    private readonly IQuoteRepository _quoteRepository;

    public GetQuoteReviewQueryHandler(IQuoteRepository quoteRepository)
    {
        _quoteRepository = quoteRepository;
    }

    public async Task<Result<QuoteReviewDto>> Handle(GetQuoteReviewQuery request, CancellationToken cancellationToken)
    {
        var coverageTypes = await _quoteRepository.GetCoverageTypesAsync(cancellationToken);
        var typeMap = coverageTypes.ToDictionary(ct => ct.Id);

        var quote = await _quoteRepository.GetFullQuoteAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result<QuoteReviewDto>.Failure("Quote not found.");

        var drivers = quote.Drivers.Select(d => new DriverReviewDto(
            d.Id, d.DriverType, d.FirstName, d.LastName,
            d.DateOfBirth.ToString("yyyy-MM-dd"), d.LicenseNumber, d.LicenseState
        )).ToList();

        var vehicles = quote.Vehicles.Select(v => new VehicleReviewDto(
            v.Id, v.Year, v.Make, v.Model, v.VIN, v.PrimaryUse
        )).ToList();

        var coverages = quote.Coverages.Select(c =>
        {
            typeMap.TryGetValue(c.CoverageTypeId, out var ct);
            return new CoverageReviewDto(
                c.CoverageTypeId,
                ct?.Code ?? string.Empty,
                ct?.Description ?? string.Empty,
                c.LimitOption,
                c.Deductible,
                c.AnnualPremium
            );
        }).ToList();

        var totalAnnual = coverages.Sum(c => c.AnnualPremium);

        return Result<QuoteReviewDto>.Success(new QuoteReviewDto(
            quote.Id,
            quote.QuoteNumber,
            quote.Status,
            quote.ZipCode,
            quote.Draft?.DraftStateJson ?? "{}",
            drivers,
            vehicles,
            coverages,
            totalAnnual,
            Math.Round(totalAnnual / 12, 2)
        ));
    }
}
