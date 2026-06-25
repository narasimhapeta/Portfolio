namespace AutoInsurance.Domain.Claims;

public class Claim : BaseEntity
{
    public Guid PolicyId { get; set; }
    public DateOnly IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = ClaimStatus.Submitted;

    public ICollection<ClaimDocument> Documents { get; set; } = new List<ClaimDocument>();
}

public static class ClaimStatus
{
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
    public const string Closed = "Closed";
}
