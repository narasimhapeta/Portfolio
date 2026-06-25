using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Queries.GetQuoteReview;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Moq;

namespace AutoInsurance.QuoteBuy.Tests.Queries;

public class GetQuoteReviewQueryHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();

    private GetQuoteReviewQueryHandler CreateHandler() =>
        new(_repoMock.Object);

    [Fact]
    public async Task Handle_QuoteNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetFullQuoteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);
        _repoMock.Setup(r => r.GetCoverageTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetQuoteReviewQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidQuote_ReturnsSummaryWithTotals()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote
        {
            Id = quoteId,
            QuoteNumber = "Q-20260625-REVIEW01",
            Status = QuoteStatus.Review,
            ZipCode = "78701",
            Drivers = [new Driver { DriverType = "Primary", FirstName = "Alice", LastName = "Smith", DateOfBirth = new DateOnly(1990, 1, 1), LicenseNumber = "TX9876", LicenseState = "TX" }],
            Vehicles = [new Vehicle { Year = 2021, Make = "Honda", Model = "Civic", VIN = "1HGBH41JXMN109186", PrimaryUse = "Commute" }],
            Coverages =
            [
                new QuoteCoverage { CoverageTypeId = 1, LimitOption = "100/300", Deductible = 500m, AnnualPremium = 320m },
                new QuoteCoverage { CoverageTypeId = 2, LimitOption = "100000", Deductible = 0m, AnnualPremium = 180m }
            ],
            Draft = new QuoteDraft { DraftStateJson = "{}", StepReached = 4 }
        };

        _repoMock.Setup(r => r.GetCoverageTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new CoverageType { Id = 1, Code = CoverageCode.BodilyInjury, Description = "Bodily Injury", MockAnnualRate = 320m },
                new CoverageType { Id = 2, Code = CoverageCode.PropertyDamage, Description = "Property Damage", MockAnnualRate = 180m }
            ]);
        _repoMock.Setup(r => r.GetFullQuoteAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await CreateHandler().Handle(new GetQuoteReviewQuery(quoteId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalAnnualPremium.Should().Be(500m);
        result.Value.TotalMonthlyPremium.Should().Be(Math.Round(500m / 12, 2));
        result.Value.Drivers.Should().HaveCount(1);
        result.Value.Vehicles.Should().HaveCount(1);
        result.Value.Coverages.Should().HaveCount(2);
    }
}
