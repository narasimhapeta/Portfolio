using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicyDocuments;

public record GetPolicyDocumentsQuery(Guid PolicyId, string B2CObjectId) : IRequest<Result<List<DocumentDto>>>;
