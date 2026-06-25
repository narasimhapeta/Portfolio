using AutoInsurance.DocumentGeneration.Application.DTOs;
using AutoInsurance.DocumentGeneration.Infrastructure.Persistence.Repositories;
using AutoInsurance.Domain.Document;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;
using System.Text;

namespace AutoInsurance.DocumentGeneration.Application.Commands.GenerateDocument;

public class GenerateDocumentCommandHandler : IRequestHandler<GenerateDocumentCommand, Result<DocumentDto>>
{
    private static readonly HashSet<string> ValidTypes = [DocumentType.InsuranceCard, DocumentType.DeclarationPage];
    private const string Container = "policy-documents";

    private readonly IDocumentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobService _blobService;

    public GenerateDocumentCommandHandler(IDocumentRepository repository, IUnitOfWork unitOfWork, IBlobService blobService)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _blobService = blobService;
    }

    public async Task<Result<DocumentDto>> Handle(GenerateDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!ValidTypes.Contains(request.DocumentType))
            return Result<DocumentDto>.Failure($"Invalid document type. Valid: {string.Join(", ", ValidTypes)}");

        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<DocumentDto>.Failure("Policy not found.");

        var pdfBytes = GenerateMockPdf(request.DocumentType, policy.PolicyNumber);
        var blobName = $"{request.PolicyId}/{request.DocumentType}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

        using var stream = new MemoryStream(pdfBytes);
        var blobUrl = await _blobService.UploadAsync(Container, blobName, stream, "application/pdf", cancellationToken);

        var document = new Document
        {
            PolicyId = request.PolicyId,
            Type = request.DocumentType,
            BlobUrl = blobUrl,
            GeneratedAt = DateTime.UtcNow
        };

        await _repository.AddDocumentAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<DocumentDto>.Success(new DocumentDto(document.Id, document.PolicyId, document.Type, document.BlobUrl, document.GeneratedAt));
    }

    private static byte[] GenerateMockPdf(string documentType, string policyNumber)
    {
        var content = $"[MOCK PDF] {documentType} for Policy: {policyNumber} | Generated: {DateTime.UtcNow:O}";
        return Encoding.UTF8.GetBytes(content);
    }
}
