namespace AutoInsurance.Domain.Quote;

public class QuoteDraft
{
    public Guid QuoteId { get; set; }
    public int StepReached { get; set; }
    public string DraftStateJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Quote? Quote { get; set; }
}
