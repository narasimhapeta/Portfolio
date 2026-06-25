using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Payment.Application.Commands.InitiatePayment;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.Payment.Tests.Commands;

public class InitiatePaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IPaymentProvider> _providerMock = new();

    private InitiatePaymentCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object, _providerMock.Object);

    [Fact]
    public async Task Handle_PolicyNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetPolicyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Policy?)null);

        var result = await CreateHandler().Handle(
            new InitiatePaymentCommand(Guid.NewGuid(), 1200m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ValidPolicy_ReturnsPaymentIntentId()
    {
        var policyId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Policy { Id = policyId });
        _providerMock.Setup(p => p.InitiateAsync(1200m, "USD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntent("mock_pi_abc123", 1200m, "USD"));
        _repoMock.Setup(r => r.AddTransactionAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new InitiatePaymentCommand(policyId, 1200m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PaymentIntentId.Should().Be("mock_pi_abc123");
        result.Value.Amount.Should().Be(1200m);
    }
}
