using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Queries.GetAccount;

public record GetAccountQuery(string B2CObjectId) : IRequest<Result<AccountDto>>;
