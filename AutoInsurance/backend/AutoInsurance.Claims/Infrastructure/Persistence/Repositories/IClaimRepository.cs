using AutoInsurance.Domain.Claims;
using AutoInsurance.Domain.Policy;

namespace AutoInsurance.Claims.Infrastructure.Persistence.Repositories;

public interface IClaimRepository
{
    Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Claim?> GetClaimAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<Claim?> GetClaimWithDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<List<Claim>> GetClaimsByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddClaimAsync(Claim claim, CancellationToken cancellationToken = default);
    void UpdateClaim(Claim claim);
    Task AddClaimDocumentAsync(ClaimDocument document, CancellationToken cancellationToken = default);
}
