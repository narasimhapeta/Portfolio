using AutoInsurance.DocumentGeneration.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.DocumentGeneration.Application.Queries.GetDocuments;

public record GetDocumentsQuery(Guid PolicyId) : IRequest<Result<List<DocumentDto>>>;
