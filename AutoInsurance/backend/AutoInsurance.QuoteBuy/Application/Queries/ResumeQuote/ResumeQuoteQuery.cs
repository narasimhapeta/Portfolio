using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Queries.ResumeQuote;

public record ResumeQuoteQuery(string QuoteNumber, string ZipCode) : IRequest<Result<ResumeQuoteResponse>>;

public record ResumeQuoteResponse(Guid QuoteId, string QuoteNumber, string DraftStateJson, int StepReached);
