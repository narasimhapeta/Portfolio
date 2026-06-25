namespace AutoInsurance.Domain.Payment;

public class PaymentTransaction : BaseEntity
{
    public Guid PolicyId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidAt { get; set; }
}

public static class PaymentStatus
{
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failed = "Failed";
}
