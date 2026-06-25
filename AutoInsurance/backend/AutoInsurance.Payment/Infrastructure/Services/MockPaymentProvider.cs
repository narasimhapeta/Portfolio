using AutoInsurance.Shared.Interfaces;

namespace AutoInsurance.Payment.Infrastructure.Services;

public class MockPaymentProvider : IPaymentProvider
{
    public Task<PaymentIntent> InitiateAsync(decimal amount, string currency, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentIntent($"mock_pi_{Guid.NewGuid():N}", amount, currency));

    public Task<PaymentConfirmation> ConfirmAsync(string paymentIntentId, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentConfirmation($"mock_txn_{Guid.NewGuid():N}", true));
}
