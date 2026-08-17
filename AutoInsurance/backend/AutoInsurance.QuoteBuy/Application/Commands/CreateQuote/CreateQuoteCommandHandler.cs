using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.QuoteBuy.Infrastructure.Services;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;

public class CreateQuoteCommandHandler : IRequestHandler<CreateQuoteCommand, Result<CreateQuoteResponse>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuoteNumberGenerator _quoteNumberGenerator;

    public CreateQuoteCommandHandler(
        IQuoteRepository quoteRepository,
        IUnitOfWork unitOfWork,
        IQuoteNumberGenerator quoteNumberGenerator)
    {
        _quoteRepository = quoteRepository;
        _unitOfWork = unitOfWork;
        _quoteNumberGenerator = quoteNumberGenerator;
    }

    public async Task<Result<CreateQuoteResponse>> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        var quoteNumber = _quoteNumberGenerator.Generate();
        var sessionTokenHash = ComputeSessionHash(quoteNumber, request.ZipCode);

        var personalInfo = new
        {
            request.FirstName,
            request.LastName,
            request.DateOfBirth,
            request.Email,
            request.Phone,
            Address = new { request.Street, request.City, request.State, request.ZipCode }
        };

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            Status = QuoteStatus.Draft,
            ZipCode = request.ZipCode,
            SessionTokenHash = sessionTokenHash,
            SessionTokenExpiry = DateTime.UtcNow.AddHours(24),
            Draft = new QuoteDraft
            {
                StepReached = 1,
                DraftStateJson = JsonSerializer.Serialize(new { personalInfo }),
                UpdatedAt = DateTime.UtcNow
            }
        };

        await _quoteRepository.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateQuoteResponse>.Success(new CreateQuoteResponse(quote.Id, quoteNumber, request.ZipCode, sessionTokenHash, 1));
    }

    private static string ComputeSessionHash(string quoteNumber, string zipCode)
    {
        var input = Encoding.UTF8.GetBytes(quoteNumber + zipCode);
        var hash = SHA256.HashData(input);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
