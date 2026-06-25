using AutoInsurance.Domain.Payment;
using AutoInsurance.Domain.Policy;
using Microsoft.EntityFrameworkCore;

namespace AutoInsurance.Payment.Infrastructure.Persistence.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }

    public async Task<Policy?> GetPolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.Policies.FindAsync([policyId], cancellationToken);

    public void UpdatePolicy(Policy policy)
        => _context.Policies.Update(policy);

    public async Task<List<PaymentTransaction>> GetHistoryAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.PaymentTransactions
            .Where(t => t.PolicyId == policyId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddTransactionAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        => await _context.PaymentTransactions.AddAsync(transaction, cancellationToken);

    public async Task<PaymentTransaction?> GetPendingTransactionAsync(Guid policyId, string paymentIntentId, CancellationToken cancellationToken = default)
        => await _context.PaymentTransactions
            .FirstOrDefaultAsync(t => t.PolicyId == policyId
                && t.TransactionRef == paymentIntentId
                && t.Status == PaymentStatus.Pending, cancellationToken);

    public void UpdateTransaction(PaymentTransaction transaction)
        => _context.PaymentTransactions.Update(transaction);

    public async Task<BillingSchedule?> GetBillingScheduleAsync(Guid policyId, CancellationToken cancellationToken = default)
        => await _context.BillingSchedules.FindAsync([policyId], cancellationToken);

    public async Task AddBillingScheduleAsync(BillingSchedule schedule, CancellationToken cancellationToken = default)
        => await _context.BillingSchedules.AddAsync(schedule, cancellationToken);

    public void UpdateBillingSchedule(BillingSchedule schedule)
        => _context.BillingSchedules.Update(schedule);
}
