using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly CustomerServiceDbContext _context;

    public PolicyRepository(CustomerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Policies.FindAsync([policyId], cancellationToken);

    public async Task<Policy?> GetPolicyWithDetailsAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Policies
            .Include(p => p.Drivers)
            .Include(p => p.Vehicles)
            .Include(p => p.Coverages)
            .Include(p => p.Endorsements)
            .FirstOrDefaultAsync(p => p.Id == policyId, cancellationToken);

    public async Task<UserAccount?> GetUserAccountAsync(string b2cObjectId, CancellationToken cancellationToken = default)
        => await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.B2CObjectId == b2cObjectId, cancellationToken);

    public async Task<List<Document>> GetPolicyDocumentsAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Documents
            .Where(d => d.PolicyId == policyId)
            .OrderByDescending(d => d.GeneratedAt)
            .ToListAsync(cancellationToken);

    public async Task AddEndorsementAsync(Endorsement endorsement, CancellationToken cancellationToken = default)
        => await _context.Endorsements.AddAsync(endorsement, cancellationToken);

    public async Task AddRenewalRequestAsync(RenewalRequest request, CancellationToken cancellationToken = default)
        => await _context.RenewalRequests.AddAsync(request, cancellationToken);

    public async Task<bool> HasPendingRenewalAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.RenewalRequests
            .AnyAsync(r => r.PolicyId == policyId && r.Status == Domain.Policy.RenewalStatus.Pending, cancellationToken);

    public async Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken = default)
        => await _context.UserAccounts.AddAsync(account, cancellationToken);

    public void UpdatePolicy(Policy policy)
        => _context.Policies.Update(policy);
}
