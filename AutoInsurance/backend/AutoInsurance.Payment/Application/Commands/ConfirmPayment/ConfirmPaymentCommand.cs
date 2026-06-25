using AutoInsurance.Shared;
using MediatR;

namespace AutoInsurance.Payment.Application.Commands.ConfirmPayment;

public record ConfirmPaymentCommand(Guid PolicyId, string PaymentIntentId) : IRequest<Result<ConfirmPaymentResponse>>;

public record ConfirmPaymentResponse(string TransactionRef, bool Success, Guid PolicyId);
