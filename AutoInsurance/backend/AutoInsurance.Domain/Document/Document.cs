namespace AutoInsurance.Domain.Document;

public class Document : BaseEntity
{
    public Guid PolicyId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public static class DocumentType
{
    public const string InsuranceCard = "InsuranceCard";
    public const string DeclarationPage = "DeclarationPage";
}
