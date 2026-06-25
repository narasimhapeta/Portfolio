using AutoInsurance.Claims.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Queries.GetClaimDetail;

public record GetClaimDetailQuery(Guid ClaimId) : IRequest<Result<ClaimDetailDto>>;
