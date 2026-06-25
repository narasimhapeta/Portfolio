namespace AutoInsurance.Domain.Policy;

public class PolicyVehicle : BaseEntity
{
    public Guid PolicyId { get; set; }
    public int Year { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string VIN { get; set; } = string.Empty;
    public string PrimaryUse { get; set; } = string.Empty;

    public Policy? Policy { get; set; }
}
