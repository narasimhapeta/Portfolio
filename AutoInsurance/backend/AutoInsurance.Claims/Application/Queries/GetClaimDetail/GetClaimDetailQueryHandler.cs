using AutoInsurance.Claims.Application.DTOs;
using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Queries.GetClaimDetail;

public class GetClaimDetailQueryHandler : IRequestHandler<GetClaimDetailQuery, Result<ClaimDetailDto>>
{
    private readonly IClaimRepository _repository;

    public GetClaimDetailQueryHandler(IClaimRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<ClaimDetailDto>> Handle(GetClaimDetailQuery request, CancellationToken cancellationToken)
    {
        var claim = await _repository.GetClaimWithDocumentsAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return Result<ClaimDetailDto>.Failure("Claim not found.");

        return Result<ClaimDetailDto>.Success(new ClaimDetailDto(
            claim.Id, claim.PolicyId, claim.IncidentDate, claim.Description, claim.Status, claim.CreatedAt,
            claim.Documents.Select(d => new ClaimDocumentDto(d.Id, d.Type, d.BlobUrl, d.UploadedAt)).ToList()
        ));
    }
}
