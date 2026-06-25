using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.UpdateClaimStatus;

public record UpdateClaimStatusCommand(Guid ClaimId, string NewStatus) : IRequest<Result>;
