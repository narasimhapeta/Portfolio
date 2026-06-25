using AutoInsurance.Domain.Document;
using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.DocumentGeneration.Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly DocumentDbContext _context;

    public DocumentRepository(DocumentDbContext context)
    {
        _context = context;
    }

    public async Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Policies.FindAsync([policyId], cancellationToken);

    public async Task<List<Document>> GetByPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Documents
            .Where(d => d.PolicyId == policyId)
            .OrderByDescending(d => d.GeneratedAt)
            .ToListAsync(cancellationToken);

    public async Task AddDocumentAsync(Document document, CancellationToken cancellationToken = default)
        => await _context.Documents.AddAsync(document, cancellationToken);
}
