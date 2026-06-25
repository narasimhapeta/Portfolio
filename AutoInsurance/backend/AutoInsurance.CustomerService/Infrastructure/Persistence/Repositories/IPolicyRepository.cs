using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;

namespace AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;

public interface IPolicyRepository
{
    Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Policy?> GetPolicyWithDetailsAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetUserAccountAsync(string b2cObjectId, CancellationToken cancellationToken = default);
    Task<List<Document>> GetPolicyDocumentsAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddEndorsementAsync(Endorsement endorsement, CancellationToken cancellationToken = default);
    Task AddRenewalRequestAsync(RenewalRequest request, CancellationToken cancellationToken = default);
    Task<bool> HasPendingRenewalAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddUserAccountAsync(UserAccount account, CancellationToken cancellationToken = default);
    void UpdatePolicy(Policy policy);
}
