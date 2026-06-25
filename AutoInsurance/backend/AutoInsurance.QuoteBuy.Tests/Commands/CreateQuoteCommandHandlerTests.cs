using AutoInsurance.Domain.Quote;
using AutoInsurance.QuoteBuy.Application.Commands.CreateQuote;
using AutoInsurance.QuoteBuy.Infrastructure.Persistence.Repositories;
using AutoInsurance.QuoteBuy.Infrastructure.Services;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.QuoteBuy.Tests.Commands;

public class CreateQuoteCommandHandlerTests
{
    private readonly Mock<IQuoteRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IQuoteNumberGenerator> _generatorMock = new();

    private CreateQuoteCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object, _generatorMock.Object);

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessWithQuoteNumber()
    {
        _generatorMock.Setup(g => g.Generate()).Returns("Q-20260625-ABCDEFGH");
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateQuoteCommand(
            "John", "Doe", "1985-05-10",
            "john@example.com", "555-1234",
            "123 Main St", "Austin", "TX", "78701"
        );

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuoteNumber.Should().Be("Q-20260625-ABCDEFGH");
        result.Value.ZipCode.Should().Be("78701");
    }

    [Fact]
    public async Task Handle_ValidCommand_StoredQuoteHasSessionTokenHash()
    {
        _generatorMock.Setup(g => g.Generate()).Returns("Q-20260625-TESTQNUM");
        Quote? capturedQuote = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((q, _) => capturedQuote = q)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateQuoteCommand(
            "Jane", "Smith", "1990-01-01",
            "jane@example.com", "555-9999",
            "456 Oak Ave", "Houston", "TX", "77001"
        );

        await CreateHandler().Handle(command, CancellationToken.None);

        capturedQuote.Should().NotBeNull();
        capturedQuote!.SessionTokenHash.Should().NotBeNullOrEmpty();
        capturedQuote.SessionTokenHash!.Length.Should().Be(64);
        capturedQuote.SessionTokenExpiry.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesDraftWithStep1()
    {
        _generatorMock.Setup(g => g.Generate()).Returns("Q-20260625-DRAFTTEST");
        Quote? capturedQuote = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Quote>(), It.IsAny<CancellationToken>()))
            .Callback<Quote, CancellationToken>((q, _) => capturedQuote = q)
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateQuoteCommand(
            "Bob", "Jones", "1975-03-15",
            "bob@example.com", "555-7777",
            "789 Elm St", "Dallas", "TX", "75201"
        );

        await CreateHandler().Handle(command, CancellationToken.None);

        capturedQuote!.Draft.Should().NotBeNull();
        capturedQuote.Draft!.StepReached.Should().Be(1);
        capturedQuote.Draft.DraftStateJson.Should().Contain("Bob");
    }
}
