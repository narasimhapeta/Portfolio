using AutoInsurance.Domain.Payment;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.SetBillingSchedule;

public class SetBillingScheduleCommandHandler : IRequestHandler<SetBillingScheduleCommand, Result>
{
    private readonly IPaymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly HashSet<string> ValidFrequencies =
        [BillingFrequency.Monthly, BillingFrequency.Quarterly, BillingFrequency.Yearly];

    public SetBillingScheduleCommandHandler(IPaymentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SetBillingScheduleCommand request, CancellationToken cancellationToken)
    {
        if (!ValidFrequencies.Contains(request.Frequency))
            return Result.Failure($"Invalid frequency. Valid values: {string.Join(", ", ValidFrequencies)}");

        var schedule = await _repository.GetBillingScheduleAsync(request.PolicyId, cancellationToken);

        if (schedule is null)
        {
            await _repository.AddBillingScheduleAsync(new BillingSchedule
            {
                PolicyId = request.PolicyId,
                Frequency = request.Frequency,
                NextDueDate = ComputeNextDueDate(request.Frequency),
                UpdatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            schedule.Frequency = request.Frequency;
            schedule.NextDueDate = ComputeNextDueDate(request.Frequency);
            schedule.UpdatedAt = DateTime.UtcNow;
            _repository.UpdateBillingSchedule(schedule);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static DateOnly ComputeNextDueDate(string frequency) => frequency switch
    {
        BillingFrequency.Monthly => DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
        BillingFrequency.Quarterly => DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
        _ => DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
    };
}
