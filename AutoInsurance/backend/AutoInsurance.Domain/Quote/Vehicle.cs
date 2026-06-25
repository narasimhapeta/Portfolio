namespace AutoInsurance.Domain.Quote;

public class Vehicle : BaseEntity
{
    public Guid QuoteId { get; set; }
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string VIN { get; set; } = string.Empty;
    public string PrimaryUse { get; set; } = string.Empty;

    public Quote? Quote { get; set; }
}

public static class VehiclePrimaryUse
{
    public const string Commute = "Commute";
    public const string Pleasure = "Pleasure";
    public const string Business = "Business";
}
