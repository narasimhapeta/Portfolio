using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicies;

public class GetPoliciesQueryHandler : IRequestHandler<GetPoliciesQuery, Result<List<PolicySummaryDto>>>
{
    private readonly IPolicyRepository _repository;

    public GetPoliciesQueryHandler(IPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<PolicySummaryDto>>> Handle(GetPoliciesQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null)
            return Result<List<PolicySummaryDto>>.Success([]);

        var policy = await _repository.GetPolicyAsync(account.PolicyId, cancellationToken);
        if (policy is null)
            return Result<List<PolicySummaryDto>>.Success([]);

        var dto = new PolicySummaryDto(
            policy.Id, policy.PolicyNumber, policy.Status,
            policy.EffectiveDate, policy.ExpirationDate, policy.TotalAnnualPremium);

        return Result<List<PolicySummaryDto>>.Success([dto]);
    }
}
