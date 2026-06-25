using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.SetBillingSchedule;

public record SetBillingScheduleCommand(Guid PolicyId, string Frequency) : IRequest<Result>;
