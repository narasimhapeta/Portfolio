namespace AutoInsurance.Domain.Quote;

public class CoverageType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MockAnnualRate { get; set; }
}

public static class CoverageCode
{
    public const string BodilyInjury = "BODILY_INJURY";
    public const string PropertyDamage = "PROPERTY_DAMAGE";
    public const string Comprehensive = "COMPREHENSIVE";
    public const string Collision = "COLLISION";
    public const string Uninsured = "UNINSURED";
}
