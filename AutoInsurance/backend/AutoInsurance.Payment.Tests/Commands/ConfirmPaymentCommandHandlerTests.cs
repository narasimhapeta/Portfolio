using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Payment.Application.Commands.ConfirmPayment;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.Payment.Tests.Commands;

public class ConfirmPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IPaymentProvider> _providerMock = new();

    private ConfirmPaymentCommandHandler CreateHandler() =>
        new(_repoMock.Object, _uowMock.Object, _providerMock.Object);

    [Fact]
    public async Task Handle_PendingTransactionNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetPendingTransactionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentTransaction?)null);

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(Guid.NewGuid(), "mock_pi_missing"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_SuccessfulConfirmation_ActivatesPolicyAndCreatesBillingSchedule()
    {
        var policyId = Guid.NewGuid();
        var transaction = new PaymentTransaction { PolicyId = policyId, TransactionRef = "mock_pi_abc", Status = PaymentStatus.Pending, Amount = 1200m };
        var policy = new Policy { Id = policyId, Status = PolicyStatus.Active };

        _repoMock.Setup(r => r.GetPendingTransactionAsync(policyId, "mock_pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _providerMock.Setup(p => p.ConfirmAsync("mock_pi_abc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentConfirmation("mock_txn_xyz", true));
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetBillingScheduleAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingSchedule?)null);
        _repoMock.Setup(r => r.AddBillingScheduleAsync(It.IsAny<BillingSchedule>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var result = await CreateHandler().Handle(
            new ConfirmPaymentCommand(policyId, "mock_pi_abc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.TransactionRef.Should().Be("mock_txn_xyz");
        transaction.Status.Should().Be(PaymentStatus.Success);
        _repoMock.Verify(r => r.AddBillingScheduleAsync(
            It.Is<BillingSchedule>(s => s.Frequency == BillingFrequency.Yearly), It.IsAny<CancellationToken>()), Times.Once);
    }
}
