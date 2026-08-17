using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;

public record CreateQuoteCommand(
    string FirstName,
    string LastName,
    string DateOfBirth,
    string Email,
    string Phone,
    string Street,
    string City,
    string State,
    string ZipCode
) : IRequest<Result<CreateQuoteResponse>>;

public record CreateQuoteResponse(Guid QuoteId, string QuoteNumber, string ZipCode, string SessionToken, int StepReached);
