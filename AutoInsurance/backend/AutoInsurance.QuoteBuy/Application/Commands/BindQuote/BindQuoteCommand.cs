using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.BindQuote;

public record BindQuoteCommand(Guid QuoteId) : IRequest<Result<BindQuoteResponse>>;

public record BindQuoteResponse(Guid PolicyId, string PolicyNumber);
