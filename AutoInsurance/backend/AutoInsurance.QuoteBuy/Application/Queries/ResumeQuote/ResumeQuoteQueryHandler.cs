using System.Security.Cryptography;
using System.Text;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Queries.ResumeQuote;

public class ResumeQuoteQueryHandler : IRequestHandler<ResumeQuoteQuery, Result<ResumeQuoteResponse>>
{
    private readonly IQuoteRepository _quoteRepository;

    public ResumeQuoteQueryHandler(IQuoteRepository quoteRepository)
    {
        _quoteRepository = quoteRepository;
    }

    public async Task<Result<ResumeQuoteResponse>> Handle(ResumeQuoteQuery request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByQuoteNumberAsync(request.QuoteNumber, cancellationToken);
        if (quote is null)
            return Result<ResumeQuoteResponse>.Failure("Quote not found.");

        if (quote.SessionTokenExpiry < DateTime.UtcNow)
            return Result<ResumeQuoteResponse>.Failure("Quote session has expired.");

        var expectedHash = ComputeSessionHash(request.QuoteNumber, request.ZipCode);
        if (!string.Equals(quote.SessionTokenHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return Result<ResumeQuoteResponse>.Failure("Invalid quote number or ZIP code.");

        return Result<ResumeQuoteResponse>.Success(new ResumeQuoteResponse(
            quote.Id,
            quote.QuoteNumber,
            quote.Draft?.DraftStateJson ?? "{}",
            quote.Draft?.StepReached ?? 1
        ));
    }

    private static string ComputeSessionHash(string quoteNumber, string zipCode)
    {
        var input = Encoding.UTF8.GetBytes(quoteNumber + zipCode);
        var hash = SHA256.HashData(input);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
