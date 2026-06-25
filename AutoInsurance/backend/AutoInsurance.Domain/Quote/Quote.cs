namespace AutoInsurance.Domain.Quote;

public class Quote : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = QuoteStatus.Draft;
    public string ZipCode { get; set; } = string.Empty;
    public string? SessionTokenHash { get; set; }
    public DateTime? SessionTokenExpiry { get; set; }

    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<QuoteCoverage> Coverages { get; set; } = new List<QuoteCoverage>();
    public QuoteDraft? Draft { get; set; }
}

public static class QuoteStatus
{
    public const string Draft = "Draft";
    public const string Review = "Review";
    public const string Bound = "Bound";
    public const string Expired = "Expired";
}
