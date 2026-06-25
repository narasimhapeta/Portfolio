namespace AutoInsurance.Domain.Quote;

public class Driver : BaseEntity
{
    public Guid QuoteId { get; set; }
    public string DriverType { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseState { get; set; } = string.Empty;

    public Quote? Quote { get; set; }
}

public static class DriverType
{
    public const string Primary = "Primary";
    public const string Secondary = "Secondary";
    public const string Occasional = "Occasional";
}
