using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Commands.SaveDrivers;
using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.QuoteBuy.Tests.Commands;

public class SaveDriversCommandHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private SaveDriversCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_QuoteNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetWithDriversAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Quote?)null);

        var command = new SaveDriversCommand(Guid.NewGuid(), []);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_BoundQuote_ReturnsFailure()
    {
        var quote = new Quote { Status = QuoteStatus.Bound };
        _repoMock.Setup(r => r.GetWithDriversAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);

        var command = new SaveDriversCommand(Guid.NewGuid(), []);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("bound");
    }

    [Fact]
    public async Task Handle_ValidQuote_ReplacesDriversAndSaves()
    {
        var quoteId = Guid.NewGuid();
        var quote = new Quote { Id = quoteId, Status = QuoteStatus.Draft };
        _repoMock.Setup(r => r.GetWithDriversAsync(quoteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(quote);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var drivers = new List<DriverDto>
        {
            new("Primary", "Alice", "Smith", "1990-01-01", "TX12345678", "TX")
        };

        var result = await CreateHandler().Handle(new SaveDriversCommand(quoteId, drivers), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        quote.Drivers.Should().HaveCount(1);
        quote.Drivers.First().FirstName.Should().Be("Alice");
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
