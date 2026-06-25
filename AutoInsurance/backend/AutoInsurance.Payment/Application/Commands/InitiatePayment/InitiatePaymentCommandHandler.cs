using AutoInsurance.Domain.Payment;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using AutoInsurance.Shared.Interfaces;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.InitiatePayment;

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, Result<InitiatePaymentResponse>>
{
    private readonly IPaymentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentProvider _paymentProvider;

    public InitiatePaymentCommandHandler(IPaymentRepository repository, IUnitOfWork unitOfWork, IPaymentProvider paymentProvider)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _paymentProvider = paymentProvider;
    }

    public async Task<Result<InitiatePaymentResponse>> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        var policy = await _repository.GetPolicyAsync(request.PolicyId, cancellationToken);
        if (policy is null)
            return Result<InitiatePaymentResponse>.Failure("Policy not found.");

        var intent = await _paymentProvider.InitiateAsync(request.Amount, request.Currency, cancellationToken);

        var transaction = new PaymentTransaction
        {
            PolicyId = request.PolicyId,
            Amount = request.Amount,
            TransactionRef = intent.PaymentIntentId,
            Status = PaymentStatus.Pending
        };

        await _repository.AddTransactionAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<InitiatePaymentResponse>.Success(
            new InitiatePaymentResponse(transaction.Id, intent.PaymentIntentId, request.Amount));
    }
}
