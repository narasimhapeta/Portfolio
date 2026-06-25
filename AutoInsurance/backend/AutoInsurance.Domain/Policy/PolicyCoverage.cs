namespace AutoInsurance.Domain.Policy;

public class PolicyCoverage
{
    public Guid PolicyId { get; set; }
    public int CoverageTypeId { get; set; }
    public string LimitOption { get; set; } = string.Empty;
    public decimal Deductible { get; set; }
    public decimal AnnualPremium { get; set; }

    public Policy? Policy { get; set; }
}
