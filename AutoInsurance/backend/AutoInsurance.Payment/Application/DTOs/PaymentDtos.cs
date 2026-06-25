namespace AutoInsurance.Payment.Application.DTOs;

public record PaymentTransactionDto(
    Guid Id,
    Guid PolicyId,
    decimal Amount,
    string TransactionRef,
    string Status,
    DateTime? PaidAt,
    DateTime CreatedAt
);

public record BillingScheduleDto(Guid PolicyId, string Frequency, DateOnly NextDueDate);
