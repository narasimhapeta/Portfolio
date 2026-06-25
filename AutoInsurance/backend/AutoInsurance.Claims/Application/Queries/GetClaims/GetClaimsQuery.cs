using AutoInsurance.Claims.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Queries.GetClaims;

public record GetClaimsQuery(Guid PolicyId) : IRequest<Result<List<ClaimDto>>>;
