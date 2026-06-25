using AutoInsurance.Claims.Application.DTOs;
using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Queries.GetClaims;

public class GetClaimsQueryHandler : IRequestHandler<GetClaimsQuery, Result<List<ClaimDto>>>
{
    private readonly IClaimRepository _repository;

    public GetClaimsQueryHandler(IClaimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<ClaimDto>>> Handle(GetClaimsQuery request, CancellationToken cancellationToken)
    {
        var claims = await _repository.GetClaimsByPolicyAsync(request.PolicyId, cancellationToken);
        return Result<List<ClaimDto>>.Success(
            claims.Select(c => new ClaimDto(c.Id, c.PolicyId, c.IncidentDate, c.Description, c.Status, c.CreatedAt)).ToList());
    }
}
