using AutoInsurance.Payment.Application.DTOs;
using AutoInsurance.Payment.Infrastructure.Persistence.Repositories;
using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Payment.Application.Queries.GetPaymentHistory;

public class GetPaymentHistoryQueryHandler : IRequestHandler<GetPaymentHistoryQuery, Result<List<PaymentTransactionDto>>>
{
    private readonly IPaymentRepository _repository;

    public GetPaymentHistoryQueryHandler(IPaymentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<List<PaymentTransactionDto>>> Handle(GetPaymentHistoryQuery request, CancellationToken cancellationToken)
    {
        var transactions = await _repository.GetHistoryAsync(request.PolicyId, cancellationToken);

        return Result<List<PaymentTransactionDto>>.Success(
            transactions.Select(t => new PaymentTransactionDto(
                t.Id, t.PolicyId, t.Amount, t.TransactionRef, t.Status, t.PaidAt, t.CreatedAt
            )).ToList());
    }
}
