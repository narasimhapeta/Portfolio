using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveCoverages;

public record SaveCoveragesCommand(Guid QuoteId, List<CoverageDto> Coverages) : IRequest<Result<decimal>>;
