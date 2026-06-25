using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Queries.ResumeQuote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace AutoInsurance.QuoteBuy.Tests.Queries;

public class ResumeQuoteQueryHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();

    private ResumeQuoteQueryHandler CreateHandler() => new(_repoMock.Object);

    private static string ComputeHash(string quoteNumber, string zipCode)
    {
        var input = Encoding.UTF8.GetBytes(quoteNumber + zipCode);
        return Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsQuoteDraft()
    {
        const string quoteNumber = "Q-20260625-RESUME01";
        const string zipCode = "78701";
        var hash = ComputeHash(quoteNumber, zipCode);

        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            QuoteNumber = quoteNumber,
            ZipCode = zipCode,
            SessionTokenHash = hash,
            SessionTokenExpiry = DateTime.UtcNow.AddHours(20),
            Draft = new QuoteDraft { DraftStateJson = "{\"step\":2}", StepReached = 2 }
        };

        _repoMock.Setup(r => r.GetByQuoteNumberAsync(quoteNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await CreateHandler().Handle(new ResumeQuoteQuery(quoteNumber, zipCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StepReached.Should().Be(2);
        result.Value.DraftStateJson.Should().Contain("step");
    }

    [Fact]
    public async Task Handle_WrongZipCode_ReturnsFailure()
    {
        const string quoteNumber = "Q-20260625-WRONGZIP";
        var hash = ComputeHash(quoteNumber, "78701");

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            SessionTokenHash = hash,
            SessionTokenExpiry = DateTime.UtcNow.AddHours(20)
        };

        _repoMock.Setup(r => r.GetByQuoteNumberAsync(quoteNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await CreateHandler().Handle(new ResumeQuoteQuery(quoteNumber, "99999"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid");
    }

    [Fact]
    public async Task Handle_ExpiredSession_ReturnsFailure()
    {
        const string quoteNumber = "Q-20260625-EXPIRED1";
        const string zipCode = "78701";
        var hash = ComputeHash(quoteNumber, zipCode);

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            SessionTokenHash = hash,
            SessionTokenExpiry = DateTime.UtcNow.AddHours(-1)
        };

        _repoMock.Setup(r => r.GetByQuoteNumberAsync(quoteNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await CreateHandler().Handle(new ResumeQuoteQuery(quoteNumber, zipCode), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expired");
    }
}
