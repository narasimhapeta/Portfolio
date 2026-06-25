using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicyDetail;

public record GetPolicyDetailQuery(Guid PolicyId, string B2CObjectId) : IRequest<Result<PolicyDetailDto>>;
