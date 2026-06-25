using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;

namespace AutoInsurance.Payment.Infrastructure.Persistence.Repositories;

public interface IPaymentRepository
{
    Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
    void UpdatePolicy(Policy policy);
    Task<List<PaymentTransaction>> GetHistoryAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetPendingTransactionAsync(Guid policyId, string paymentIntentId, CancellationToken cancellationToken = default);
    void UpdateTransaction(PaymentTransaction transaction);
    Task<BillingSchedule?> GetBillingScheduleAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task AddBillingScheduleAsync(BillingSchedule schedule, CancellationToken cancellationToken = default);
    void UpdateBillingSchedule(BillingSchedule schedule);
}
