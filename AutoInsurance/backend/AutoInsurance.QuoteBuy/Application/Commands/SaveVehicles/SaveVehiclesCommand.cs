using AutoInsurance.QuoteBuy.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.QuoteBuy.Application.Commands.SaveVehicles;

public record SaveVehiclesCommand(Guid QuoteId, List<VehicleDto> Vehicles) : IRequest<Result>;
