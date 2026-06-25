using AutoInsurance.Claims.Application.Commands.SubmitClaim;
using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Claims;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.Claims.Tests.Commands;

public class SubmitClaimCommandHandlerTests
{
    private readonly Mock<IClaimRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private SubmitClaimCommandHandler CreateHandler() => new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_PolicyNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetPolicyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Policy?)null);

        var result = await CreateHandler().Handle(
            new SubmitClaimCommand(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), "Test"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_FutureIncidentDate_ReturnsFailure()
    {
        var policyId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Policy { Id = policyId });

        var result = await CreateHandler().Handle(
            new SubmitClaimCommand(policyId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), "Future incident"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("future");
    }

    [Fact]
    public async Task Handle_ValidClaim_CreatesWithSubmittedStatus()
    {
        var policyId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Policy { Id = policyId });
        _repoMock.Setup(r => r.AddClaimAsync(It.IsAny<Claim>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new SubmitClaimCommand(policyId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), "Minor fender bender"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repoMock.Verify(r => r.AddClaimAsync(
            It.Is<Claim>(c => c.Status == ClaimStatus.Submitted && c.PolicyId == policyId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
