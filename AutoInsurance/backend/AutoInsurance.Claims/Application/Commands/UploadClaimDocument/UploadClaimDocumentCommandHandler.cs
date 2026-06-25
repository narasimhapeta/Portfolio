using AutoInsurance.Claims.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Claims;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.UploadClaimDocument;

public class UploadClaimDocumentCommandHandler : IRequestHandler<UploadClaimDocumentCommand, Result<Guid>>
{
    private static readonly HashSet<string> ValidTypes =
        [ClaimDocumentType.IncidentPhoto, ClaimDocumentType.DamagePhoto, ClaimDocumentType.Other];
    private const string Container = "claim-documents";

    private readonly IClaimRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;

    public UploadClaimDocumentCommandHandler(IClaimRepository repository, IUnitOfWork unitOfWork, IBlobService blobService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
    }

    public async Task<Result<Guid>> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!ValidTypes.Contains(request.DocumentType))
            return Result<Guid>.Failure($"Invalid document type. Valid: {string.Join(", ", ValidTypes)}");

        var claim = await _repository.GetClaimAsync(request.ClaimId, cancellationToken);
        if (claim is null)
            return Result<Guid>.Failure("Claim not found.");

        byte[] fileBytes;
        try { fileBytes = Convert.FromBase64String(request.Base64Content); }
        catch { return Result<Guid>.Failure("Invalid base64 content."); }

        var ext = Path.GetExtension(request.FileName).TrimStart('.');
        var blobName = $"{request.ClaimId}/{request.DocumentType}-{Guid.NewGuid():N}.{ext}";
        var contentType = ext is "pdf" ? "application/pdf" : "image/jpeg";

        using var stream = new MemoryStream(fileBytes);
        var blobUrl = await _blobService.UploadAsync(Container, blobName, stream, contentType, cancellationToken);

        var doc = new ClaimDocument
        {
            ClaimId = request.ClaimId,
            Type = request.DocumentType,
            BlobUrl = blobUrl,
            UploadedAt = DateTime.UtcNow
        };

        await _repository.AddClaimDocumentAsync(doc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(doc.Id);
    }
}
