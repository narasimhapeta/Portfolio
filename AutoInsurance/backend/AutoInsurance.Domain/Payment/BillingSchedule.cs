namespace AutoInsurance.Domain.Payment;

public class BillingSchedule
{
    public Guid PolicyId { get; set; }
    public string Frequency { get; set; } = BillingFrequency.Yearly;
    public DateOnly NextDueDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class BillingFrequency
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";
}
