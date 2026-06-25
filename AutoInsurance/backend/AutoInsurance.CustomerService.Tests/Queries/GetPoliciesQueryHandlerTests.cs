using AutoInsurance.CustomerService.Application.Queries.GetPolicies;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Policy;
using FluentAssertions;
using Moq;

namespace AutoInsurance.CustomerService.Tests.Queries;

public class GetPoliciesQueryHandlerTests
{
    private readonly Mock<IPolicyRepository> _repoMock = new();

    private GetPoliciesQueryHandler CreateHandler() => new(_repoMock.Object);

    [Fact]
    public async Task Handle_UserHasNoAccount_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetUserAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);

        var result = await CreateHandler().Handle(new GetPoliciesQuery("unknown-id"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UserHasLinkedPolicy_ReturnsSinglePolicySummary()
    {
        var policyId = Guid.NewGuid();
        var account = new UserAccount { B2CObjectId = "b2c-123", PolicyId = policyId, Email = "test@test.com" };
        var policy = new Policy
        {
            Id = policyId,
            PolicyNumber = "POL-TEST",
            Status = PolicyStatus.Active,
            EffectiveDate = new DateOnly(2026, 1, 1),
            ExpirationDate = new DateOnly(2027, 1, 1),
            TotalAnnualPremium = 1000m
        };

        _repoMock.Setup(r => r.GetUserAccountAsync("b2c-123", It.IsAny<CancellationToken>())).ReturnsAsync(account);
        _repoMock.Setup(r => r.GetPolicyAsync(policyId, It.IsAny<CancellationToken>())).ReturnsAsync(policy);

        var result = await CreateHandler().Handle(new GetPoliciesQuery("b2c-123"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].PolicyNumber.Should().Be("POL-TEST");
        result.Value![0].TotalAnnualPremium.Should().Be(1000m);
    }
}
