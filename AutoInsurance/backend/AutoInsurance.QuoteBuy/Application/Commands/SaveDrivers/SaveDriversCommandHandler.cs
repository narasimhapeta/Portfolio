using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveDrivers;

public class SaveDriversCommandHandler : IRequestHandler<SaveDriversCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveDriversCommandHandler(IQuoteRepository quoteRepository, IUnitOfWork unitOfWork)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SaveDriversCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure("Quote not found.");

        if (quote.Status == QuoteStatus.Bound)
            return Result.Failure("Cannot modify a bound quote.");

        await _quoteRepository.DeleteDriversAsync(request.QuoteId, cancellationToken);

        foreach (var dto in request.Drivers)
        {
            quote.Drivers.Add(new Driver
            {
                QuoteId = quote.Id,
                DriverType = dto.DriverType,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                DateOfBirth = DateOnly.Parse(dto.DateOfBirth),
                LicenseNumber = dto.LicenseNumber,
                LicenseState = dto.LicenseState
            });
        }

        quote.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
