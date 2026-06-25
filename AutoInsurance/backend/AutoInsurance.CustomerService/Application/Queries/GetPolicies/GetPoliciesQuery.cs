using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetPolicies;

public record GetPoliciesQuery(string B2CObjectId) : IRequest<Result<List<PolicySummaryDto>>>;
