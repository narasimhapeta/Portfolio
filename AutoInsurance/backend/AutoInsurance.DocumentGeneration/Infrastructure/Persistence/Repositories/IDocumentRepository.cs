using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;

namespace AutoInsurance.DocumentGeneration.Infrastructure.Persistence.Repositories;

public interface IDocumentRepository
{
    Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<List<Document>> GetByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddDocumentAsync(Document document, CancellationToken cancellationToken = default);
}
