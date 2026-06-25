namespace AutoInsurance.Domain.Policy;

public class Endorsement : BaseEntity
{
    public Guid PolicyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string Status { get; set; } = EndorsementStatus.Pending;
    public string ChangeJson { get; set; } = string.Empty;

    public Policy? Policy { get; set; }
}

public static class EndorsementStatus
{
    public const string Pending = "Pending";
    public const string Applied = "Applied";
}

public static class EndorsementType
{
    public const string CoverageChange = "CoverageChange";
    public const string VehicleAdd = "VehicleAdd";
    public const string DriverAdd = "DriverAdd";
}
