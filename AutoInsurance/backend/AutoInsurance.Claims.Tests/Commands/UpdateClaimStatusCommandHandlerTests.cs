using AutoInsurance.Claims.Application.Commands.UpdateClaimStatus;
using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Claims;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.Claims.Tests.Commands;

public class UpdateClaimStatusCommandHandlerTests
{
    private readonly Mock<IClaimRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private UpdateClaimStatusCommandHandler CreateHandler() => new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsFailure()
    {
        var result = await CreateHandler().Handle(
            new UpdateClaimStatusCommand(Guid.NewGuid(), "Pending"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task Handle_ClaimNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetClaimAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Claim?)null);

        var result = await CreateHandler().Handle(
            new UpdateClaimStatusCommand(Guid.NewGuid(), ClaimStatus.UnderReview), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_InvalidTransition_ReturnsFailure()
    {
        var claim = new Claim { Status = ClaimStatus.Approved };
        _repoMock.Setup(r => r.GetClaimAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);

        var result = await CreateHandler().Handle(
            new UpdateClaimStatusCommand(Guid.NewGuid(), ClaimStatus.Submitted), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Cannot transition");
    }

    [Fact]
    public async Task Handle_ValidTransition_UpdatesStatus()
    {
        var claimId = Guid.NewGuid();
        var claim = new Claim { Id = claimId, Status = ClaimStatus.Submitted };
        _repoMock.Setup(r => r.GetClaimAsync(claimId, It.IsAny<CancellationToken>())).ReturnsAsync(claim);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new UpdateClaimStatusCommand(claimId, ClaimStatus.UnderReview), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        claim.Status.Should().Be(ClaimStatus.UnderReview);
        _repoMock.Verify(r => r.UpdateClaim(claim), Times.Once);
    }
}
