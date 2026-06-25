using AutoInsurance.CustomerService.Application.DTOs;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.CustomerService.Application.Commands.ChangeCoverage;

public record ChangeCoverageCommand(Guid PolicyId, string B2CObjectId, List<CoverageChangeDto> Changes) : IRequest<Result<Guid>>;
