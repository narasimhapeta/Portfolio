namespace AutoInsurance.Domain.Policy;

public class UserAccount : BaseEntity
{
    public string B2CObjectId { get; set; } = string.Empty;
    public Guid PolicyId { get; set; }
    public string Email { get; set; } = string.Empty;

    public Policy? Policy { get; set; }
}
