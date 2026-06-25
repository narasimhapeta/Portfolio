using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Claims.Application.Commands.SubmitClaim;

public record SubmitClaimCommand(Guid PolicyId, DateOnly IncidentDate, string Description) : IRequest<Result<Guid>>;
