using AutoInsurance.Domain.Policy;
using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.BindQuote;

public class BindQuoteCommandHandler : IRequestHandler<BindQuoteCommand, Result<BindQuoteResponse>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BindQuoteCommandHandler(IQuoteRepository quoteRepository, IUnitOfWork unitOfWork)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BindQuoteResponse>> Handle(BindQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetFullQuoteAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result<BindQuoteResponse>.Failure("Quote not found.");

        if (quote.Status != QuoteStatus.Review)
            return Result<BindQuoteResponse>.Failure("Quote must be in Review status to bind.");

        var policyNumber = $"POL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
        var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var expirationDate = effectiveDate.AddYears(1);
        var totalPremium = quote.Coverages.Sum(c => c.AnnualPremium);

        var policy = new Policy
        {
            QuoteId = quote.Id,
            PolicyNumber = policyNumber,
            Status = PolicyStatus.Active,
            EffectiveDate = effectiveDate,
            ExpirationDate = expirationDate,
            TotalAnnualPremium = totalPremium
        };

        foreach (var d in quote.Drivers)
        {
            policy.Drivers.Add(new PolicyDriver
            {
                PolicyId = policy.Id,
                DriverType = d.DriverType,
                FirstName = d.FirstName,
                LastName = d.LastName,
                DateOfBirth = d.DateOfBirth,
                LicenseNumber = d.LicenseNumber,
                LicenseState = d.LicenseState
            });
        }

        foreach (var v in quote.Vehicles)
        {
            policy.Vehicles.Add(new PolicyVehicle
            {
                PolicyId = policy.Id,
                Year = v.Year,
                Make = v.Make,
                Model = v.Model,
                VIN = v.VIN,
                PrimaryUse = v.PrimaryUse
            });
        }

        foreach (var c in quote.Coverages)
        {
            policy.Coverages.Add(new PolicyCoverage
            {
                PolicyId = policy.Id,
                CoverageTypeId = c.CoverageTypeId,
                LimitOption = c.LimitOption,
                Deductible = c.Deductible,
                AnnualPremium = c.AnnualPremium
            });
        }

        await _quoteRepository.AddPolicyAsync(policy, cancellationToken);

        quote.Status = QuoteStatus.Bound;
        quote.UpdatedAt = DateTime.UtcNow;
        _quoteRepository.Update(quote);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BindQuoteResponse>.Success(new BindQuoteResponse(policy.Id, policyNumber));
    }
}
