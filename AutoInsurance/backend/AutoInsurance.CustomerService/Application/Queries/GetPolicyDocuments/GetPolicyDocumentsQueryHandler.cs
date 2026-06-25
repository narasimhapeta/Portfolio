using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.CustomerService.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicyDocuments;

public class GetPolicyDocumentsQueryHandler : IRequestHandler<GetPolicyDocumentsQuery, Result<List<DocumentDto>>>
{
    private readonly IPolicyRepository _repository;

    public GetPolicyDocumentsQueryHandler(IPolicyRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<DocumentDto>>> Handle(GetPolicyDocumentsQuery request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetUserAccountAsync(request.B2CObjectId, cancellationToken);
        if (account is null || account.PolicyId != request.PolicyId)
            return Result<List<DocumentDto>>.Failure("Access denied.");

        var documents = await _repository.GetPolicyDocumentsAsync(request.PolicyId, cancellationToken);

        return Result<List<DocumentDto>>.Success(
            documents.Select(d => new DocumentDto(d.Id, d.Type, d.BlobUrl, d.GeneratedAt)).ToList());
    }
}
