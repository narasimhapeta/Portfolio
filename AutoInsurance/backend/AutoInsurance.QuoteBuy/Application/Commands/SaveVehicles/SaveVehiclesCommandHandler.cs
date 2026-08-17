using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveVehicles;

public class SaveVehiclesCommandHandler : IRequestHandler<SaveVehiclesCommand, Result>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SaveVehiclesCommandHandler(IQuoteRepository quoteRepository, IUnitOfWork unitOfWork)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SaveVehiclesCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure("Quote not found.");

        if (quote.Status == QuoteStatus.Bound)
            return Result.Failure("Cannot modify a bound quote.");

        await _quoteRepository.DeleteVehiclesAsync(request.QuoteId, cancellationToken);

        foreach (var dto in request.Vehicles)
        {
            quote.Vehicles.Add(new Vehicle
            {
                QuoteId = quote.Id,
                Year = dto.Year,
                Make = dto.Make,
                Model = dto.Model,
                VIN = dto.Vin,
                PrimaryUse = dto.PrimaryUse
            });
        }

        quote.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
