using AutoInsurance.DocumentGeneration.Application.DTOs;
using AutoInsurance.DocumentGeneration.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.DocumentGeneration.Application.Queries.GetDocuments;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, Result<List<DocumentDto>>>
{
    private readonly IDocumentRepository _repository;

    public GetDocumentsQueryHandler(IDocumentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<DocumentDto>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _repository.GetByPolicyAsync(request.PolicyId, cancellationToken);
        return Result<List<DocumentDto>>.Success(
            docs.Select(d => new DocumentDto(d.Id, d.PolicyId, d.Type, d.BlobUrl, d.GeneratedAt)).ToList());
    }
}
