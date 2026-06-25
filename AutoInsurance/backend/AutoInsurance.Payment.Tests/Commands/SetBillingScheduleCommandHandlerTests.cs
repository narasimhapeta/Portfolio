using AutoInsurance.Domain.Payment;
using AutoInsurance.Payment.Application.Commands.SetBillingSchedule;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.Payment.Tests.Commands;

public class SetBillingScheduleCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private SetBillingScheduleCommandHandler CreateHandler() => new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_InvalidFrequency_ReturnsFailure()
    {
        var result = await CreateHandler().Handle(
            new SetBillingScheduleCommand(Guid.NewGuid(), "Weekly"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid frequency");
    }

    [Fact]
    public async Task Handle_NoExistingSchedule_CreatesNewSchedule()
    {
        var policyId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetBillingScheduleAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BillingSchedule?)null);
        _repoMock.Setup(r => r.AddBillingScheduleAsync(It.IsAny<BillingSchedule>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new SetBillingScheduleCommand(policyId, BillingFrequency.Monthly), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.AddBillingScheduleAsync(
            It.Is<BillingSchedule>(s => s.Frequency == BillingFrequency.Monthly), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingSchedule_UpdatesFrequency()
    {
        var policyId = Guid.NewGuid();
        var existing = new BillingSchedule { PolicyId = policyId, Frequency = BillingFrequency.Yearly };

        _repoMock.Setup(r => r.GetBillingScheduleAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new SetBillingScheduleCommand(policyId, BillingFrequency.Quarterly), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Frequency.Should().Be(BillingFrequency.Quarterly);
        _repoMock.Verify(r => r.UpdateBillingSchedule(existing), Times.Once);
    }
}
