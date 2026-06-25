using AutoInsurance.Domain.Policy;
using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Commands.BindQuote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.QuoteBuy.Tests.Commands;

public class BindQuoteCommandHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private BindQuoteCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_QuoteInReview_BindsAndCreatesPolicyRecord()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote
        {
            Id = quoteId,
            Status = QuoteStatus.Review,
            Drivers = [new Driver { DriverType = "Primary", FirstName = "John", LastName = "Doe", DateOfBirth = new DateOnly(1985, 1, 1), LicenseNumber = "TX123", LicenseState = "TX" }],
            Vehicles = [new Vehicle { Year = 2022, Make = "Toyota", Model = "Camry", VIN = "1HGBH41JXMN109186", PrimaryUse = "Commute" }],
            Coverages = [new QuoteCoverage { CoverageTypeId = 1, LimitOption = "100/300", Deductible = 500m, AnnualPremium = 320m }]
        };

        _repoMock.Setup(r => r.GetFullQuoteAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        Policy? capturedPolicy = null;
        _repoMock.Setup(r => r.AddPolicyAsync(It.IsAny<Policy>(), It.IsAny<CancellationToken>()))
            .Callback<Policy, CancellationToken>((p, _) => capturedPolicy = p)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await CreateHandler().Handle(new BindQuoteCommand(quoteId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PolicyNumber.Should().StartWith("POL-");
        quote.Status.Should().Be(QuoteStatus.Bound);
        capturedPolicy.Should().NotBeNull();
        capturedPolicy!.Drivers.Should().HaveCount(1);
        capturedPolicy.Vehicles.Should().HaveCount(1);
        capturedPolicy.Coverages.Should().HaveCount(1);
        capturedPolicy.TotalAnnualPremium.Should().Be(320m);
    }

    [Fact]
    public async Task Handle_QuoteNotInReview_ReturnsFailure()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote { Id = quoteId, Status = QuoteStatus.Draft };

        _repoMock.Setup(r => r.GetFullQuoteAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var result = await CreateHandler().Handle(new BindQuoteCommand(quoteId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Review");
    }
}
