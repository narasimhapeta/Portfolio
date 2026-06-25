using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveDrivers;

public record SaveDriversCommand(Guid QuoteId, List<DriverDto> Drivers) : IRequest<Result>;
