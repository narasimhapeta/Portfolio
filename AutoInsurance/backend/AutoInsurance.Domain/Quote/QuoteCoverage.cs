namespace AutoInsurance.Domain.Quote;

public class QuoteCoverage
{
    public Guid QuoteId { get; set; }
    public int CoverageTypeId { get; set; }
    public string LimitOption { get; set; } = string.Empty;
    public decimal Deductible { get; set; }
    public decimal AnnualPremium { get; set; }

    public Quote? Quote { get; set; }
    public CoverageType? CoverageType { get; set; }
}
