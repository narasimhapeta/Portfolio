using AutoInsurance.CustomerService.Application.Commands.LinkAccount;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Shared.Interfaces;
using FluentAssertions;
using Moq;

namespace AutoInsurance.CustomerService.Tests.Commands;

public class LinkAccountCommandHandlerTests
{
    private readonly Mock<IPolicyRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private LinkAccountCommandHandler CreateHandler() => new(_repoMock.Object, _uowMock.Object);

    [Fact]
    public async Task Handle_PolicyNotFound_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetPolicyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Policy?)null);

        var result = await CreateHandler().Handle(
            new LinkAccountCommand("b2c-123", Guid.NewGuid(), "user@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_AlreadyLinked_ReturnsExistingAccountId()
    {
        var policyId = Guid.NewGuid();
        var existingAccountId = Guid.NewGuid();
        var policy = new Policy { Id = policyId };
        var existing = new UserAccount { Id = existingAccountId, B2CObjectId = "b2c-123", PolicyId = policyId };

        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-123", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(
            new LinkAccountCommand("b2c-123", policyId, "user@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingAccountId);
        _repoMock.Verify(r => r.AddUserAccountAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NewLink_CreatesUserAccountAndReturnsId()
    {
        var policyId = Guid.NewGuid();
        var policy = new Policy { Id = policyId };

        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);
        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-new", It.IsAny<CancellationToken>())).ReturnsAsync((UserAccount?)null);
        _repoMock.Setup(r => r.AddUserAccountAsync(It.IsAny<UserAccount>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateHandler().Handle(
            new LinkAccountCommand("b2c-new", policyId, "new@test.com"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.AddUserAccountAsync(It.Is<UserAccount>(a => a.Email == "new@test.com"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
