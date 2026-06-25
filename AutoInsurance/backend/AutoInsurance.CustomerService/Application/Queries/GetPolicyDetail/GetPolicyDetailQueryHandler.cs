using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicyDetail;

public class GetPolicyDetailQueryHandler : IRequestHandler<GetPolicyDetailQuery, Result<PolicyDetailDto>>
{
    private readonly IPolicyRepository _repository;

    public GetPolicyDetailQueryHandler(IPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PolicyDetailDto>> Handle(GetPolicyDetailQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null || account.PolicyId != request.PolicyId)
            return Result<PolicyDetailDto>.Failure("Policy not found or access denied.");

        var policy = await _repository.GetPolicyWithDetailsAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<PolicyDetailDto>.Failure("Policy not found.");

        var dto = new PolicyDetailDto(
            policy.Id, policy.PolicyNumber, policy.Status,
            policy.EffectiveDate, policy.ExpirationDate, policy.TotalAnnualPremium,
            policy.Drivers.Select(d => new PolicyDriverDto(d.Id, d.DriverType, d.FirstName, d.LastName,
                d.DateOfBirth.ToString("yyyy-MM-dd"), d.LicenseState)).ToList(),
            policy.Vehicles.Select(v => new PolicyVehicleDto(v.Id, v.Year, v.Make, v.Model, v.VIN, v.PrimaryUse)).ToList(),
            policy.Coverages.Select(c => new PolicyCoverageDto(c.CoverageTypeId, c.LimitOption, c.Deductible, c.AnnualPremium)).ToList(),
            policy.Endorsements.Select(e => new EndorsementDto(e.Id, e.Type, e.Status, e.EffectiveDate, e.CreatedAt)).ToList()
        );

        return Result<PolicyDetailDto>.Success(dto);
    }
}
