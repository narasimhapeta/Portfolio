using AutoInsurance.CustomerService.Application.Commands.ChangeCoverage;
using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.CustomerService.Tests.Commands;

public class ChangeCoverageCommandHandlerTests
{
    private readonly Mock<IPolicyRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private ChangeCoverageCommandHandler CreateHandler() => new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_AccessDenied_WhenPolicyNotOwnedByUser()
    {
        var account = new UserAccount { B2CObjectId = "b2c-user", PolicyId = Guid.NewGuid() };
        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-user", It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var command = new ChangeCoverageCommand(Guid.NewGuid(), "b2c-user", []);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("access denied");
    }

    [Fact]
    public async Task Handle_InactivePolicy_ReturnsFailure()
    {
        var policyId = Guid.NewGuid();
        var account = new UserAccount { B2CObjectId = "b2c-user", PolicyId = policyId };
        var policy = new Policy { Id = policyId, Status = PolicyStatus.Cancelled };

        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-user", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var command = new ChangeCoverageCommand(policyId, "b2c-user", []);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("active");
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesEndorsementAndReturnsId()
    {
        var policyId = Guid.NewGuid();
        var account = new UserAccount { B2CObjectId = "b2c-user", PolicyId = policyId };
        var policy = new Policy { Id = policyId, Status = PolicyStatus.Active };

        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-user", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.AddEndorsementAsync(It.IsAny<Endorsement>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var changes = new List<CoverageChangeDto> { new(1, "100/300", 500m) };
        var command = new ChangeCoverageCommand(policyId, "b2c-user", changes);
        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _repoMock.Verify(r => r.AddEndorsementAsync(It.Is<Endorsement>(e => e.Type == EndorsementType.CoverageChange), It.IsAny<CancellationToken>()), Times.Once);
    }
}
