using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.ConfirmPayment;

public class ConfirmPaymentCommandHandler : IRequestHandler<ConfirmPaymentCommand, Result<ConfirmPaymentResponse>>
{
    private readonly IPaymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentProvider _paymentProvider;

    public ConfirmPaymentCommandHandler(IPaymentRepository repository, IUnitOfWork unitOfWork, IPaymentProvider paymentProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _paymentProvider = paymentProvider;
    }

    public async Task<Result<ConfirmPaymentResponse>> Handle(ConfirmPaymentCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetPendingTransactionAsync(request.PolicyId, request.PaymentIntentId, cancellationToken);
        if (transaction is null)
            return Result<ConfirmPaymentResponse>.Failure("Pending payment transaction not found.");

        var confirmation = await _paymentProvider.ConfirmAsync(request.PaymentIntentId, cancellationToken);

        transaction.Status = confirmation.Success ? PaymentStatus.Success : PaymentStatus.Failed;
        transaction.TransactionRef = confirmation.TransactionRef;
        transaction.PaidAt = confirmation.Success ? DateTime.UtcNow : null;
        _repository.UpdateTransaction(transaction);

        if (confirmation.Success)
        {
            var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
            if (policy is not null && policy.Status != PolicyStatus.Active)
            {
                policy.Status = PolicyStatus.Active;
                _repository.UpdatePolicy(policy);
            }

            var schedule = await _repository.GetBillingScheduleAsync(request.PolicyId, cancellationToken);
            if (schedule is null)
            {
                await _repository.AddBillingScheduleAsync(new BillingSchedule
                {
                    PolicyId = request.PolicyId,
                    Frequency = BillingFrequency.Yearly,
                    NextDueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConfirmPaymentResponse>.Success(
            new ConfirmPaymentResponse(confirmation.TransactionRef, confirmation.Success, request.PolicyId));
    }
}
