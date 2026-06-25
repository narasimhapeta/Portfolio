namespace AutoInsurance.Domain.Policy;

public class RenewalRequest : BaseEntity
{
    public Guid PolicyId { get; set; }
    public DateOnly NewEffectiveDate { get; set; }
    public string Status { get; set; } = RenewalStatus.Pending;

    public Policy? Policy { get; set; }
}

public static class RenewalStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Declined = "Declined";
}
