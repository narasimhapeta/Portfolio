using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.UploadClaimDocument;

public record UploadClaimDocumentCommand(Guid ClaimId, string DocumentType, string Base64Content, string FileName) : IRequest<Result<Guid>>;
