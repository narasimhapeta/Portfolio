namespace AutoInsurance.Domain.Policy;

public class PolicyDriver : BaseEntity
{
    public Guid PolicyId { get; set; }
    public string DriverType { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseState { get; set; } = string.Empty;

    public Policy? Policy { get; set; }
}
