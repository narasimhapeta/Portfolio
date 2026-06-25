using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.InitiatePayment;

public record InitiatePaymentCommand(Guid PolicyId, decimal Amount, string Currency = "USD") : IRequest<Result<InitiatePaymentResponse>>;

public record InitiatePaymentResponse(Guid TransactionId, string PaymentIntentId, decimal Amount);
