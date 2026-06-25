using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Commands.SaveCoverages;
using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.QuoteBuy.Tests.Commands;

public class SaveCoveragesCommandHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private SaveCoveragesCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_ValidCoverages_CalculatesPremiumAndSetsReviewStatus()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote { Id = quoteId, Status = QuoteStatus.Draft };

        _repoMock.Setup(r => r.GetWithCoveragesAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        _repoMock.Setup(r => r.GetCoverageTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoverageType>
            {
                new() { Id = 1, Code = CoverageCode.BodilyInjury, Description = "Bodily Injury", MockAnnualRate = 320m },
                new() { Id = 2, Code = CoverageCode.PropertyDamage, Description = "Property Damage", MockAnnualRate = 180m }
            });
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new SaveCoveragesCommand(quoteId, [
            new CoverageDto(1, "100/300", 500m),
            new CoverageDto(2, "100000", 500m)
        ]);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(500m);
        quote.Status.Should().Be(QuoteStatus.Review);
        quote.Coverages.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UnknownCoverageType_ReturnsFailure()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote { Id = quoteId, Status = QuoteStatus.Draft };

        _repoMock.Setup(r => r.GetWithCoveragesAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        _repoMock.Setup(r => r.GetCoverageTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CoverageType>());

        var command = new SaveCoveragesCommand(quoteId, [new CoverageDto(99, "option", 0m)]);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Unknown coverage type");
    }
}
