namespace AutoInsurance.Domain.Policy;

public class Policy : BaseEntity
{
    public Guid QuoteId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = PolicyStatus.Active;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal TotalAnnualPremium { get; set; }

    public ICollection<PolicyDriver> Drivers { get; set; } = new List<PolicyDriver>();
    public ICollection<PolicyVehicle> Vehicles { get; set; } = new List<PolicyVehicle>();
    public ICollection<PolicyCoverage> Coverages { get; set; } = new List<PolicyCoverage>();
    public ICollection<Endorsement> Endorsements { get; set; } = new List<Endorsement>();
}

public static class PolicyStatus
{
    public const string Active = "Active";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string PendingRenewal = "PendingRenewal";
}
