using AutoInsurance.DocumentGeneration.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.DocumentGeneration.Application.Commands.GenerateDocument;

public record GenerateDocumentCommand(Guid PolicyId, string DocumentType) : IRequest<Result<DocumentDto>>;
