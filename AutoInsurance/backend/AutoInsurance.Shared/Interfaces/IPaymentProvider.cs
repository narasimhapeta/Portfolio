namespace AutoInsurance.Shared.Interfaces;

public record PaymentIntent(string PaymentIntentId, decimal Amount, string Currency);
public record PaymentConfirmation(string TransactionRef, bool Success, string? FailureReason = null);

public interface IPaymentProvider
{
    Task<PaymentIntent> InitiateAsync(decimal amount, string currency, CancellationToken cancellationToken = default);
    Task<PaymentConfirmation> ConfirmAsync(string paymentIntentId, CancellationToken cancellationToken = default);
}
