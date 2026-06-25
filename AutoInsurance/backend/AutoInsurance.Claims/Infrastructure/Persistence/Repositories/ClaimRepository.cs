using AutoInsurance.Domain.Claims;
using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.Claims.Infrastructure.Persistence.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly ClaimsDbContext _context;

    public ClaimRepository(ClaimsDbContext context)
    {
        _context = context;
    }

    public async Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Policies.FindAsync([policyId], cancellationToken);

    public async Task<Claim?> GetClaimAsync(Guid claimId, CancellationToken cancellationToken = default)
        => await _context.Claims.FindAsync([claimId], cancellationToken);

    public async Task<Claim?> GetClaimWithDocumentsAsync(Guid claimId, CancellationToken cancellationToken = default)
        => await _context.Claims
            .Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == claimId, cancellationToken);

    public async Task<List<Claim>> GetClaimsByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Claims
            .Where(c => c.PolicyId == policyId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        => await _context.Claims.AddAsync(claim, cancellationToken);

    public void UpdateClaim(Claim claim)
        => _context.Claims.Update(claim);

    public async Task AddClaimDocumentAsync(ClaimDocument document, CancellationToken cancellationToken = default)
        => await _context.ClaimDocuments.AddAsync(document, cancellationToken);
}
