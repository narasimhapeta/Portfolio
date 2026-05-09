namespace AutoInsuranceMind.API.Models;

public class Policy
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // auto, home
    public string Status { get; set; } = string.Empty; // active, expired, pending
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<Coverage> Coverages { get; set; } = new();
}
