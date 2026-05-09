namespace AutoInsuranceMind.API.Models;

public class Coverage
{
    public string Id { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // liability, collision, comprehensive
    public decimal Limit { get; set; }
    public decimal Deductible { get; set; }
    public string Description { get; set; } = string.Empty;
}
