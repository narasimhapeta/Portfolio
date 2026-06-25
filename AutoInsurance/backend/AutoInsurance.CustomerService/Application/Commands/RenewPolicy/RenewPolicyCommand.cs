using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.RenewPolicy;

public record RenewPolicyCommand(Guid PolicyId, string B2CObjectId) : IRequest<Result<Guid>>;
