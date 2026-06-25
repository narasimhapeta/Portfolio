using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.LinkAccount;

public record LinkAccountCommand(string B2CObjectId, Guid PolicyId, string Email) : IRequest<Result<Guid>>;
