namespace AutoInsurance.Domain.Claims;

public class ClaimDocument : BaseEntity
{
    public Guid ClaimId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Claim? Claim { get; set; }
}

public static class ClaimDocumentType
{
    public const string IncidentPhoto = "IncidentPhoto";
    public const string DamagePhoto = "DamagePhoto";
    public const string Other = "Other";
}
