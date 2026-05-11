// ClaimsService.Core/Models/Claim.cs
using Newtonsoft.Json;

namespace ClaimsService.Core.Models;

public class Claim
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("policyNumber")]
    public string PolicyNumber { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = "FNOL";

    [JsonProperty("incidentDate")]
    public DateTime IncidentDate { get; set; }

    [JsonProperty("incidentDescription")]
    public string IncidentDescription { get; set; } = string.Empty;

    [JsonProperty("photosBlobPaths")]
    public List<string> PhotosBlobPaths { get; set; } = new();

    [JsonProperty("damageScore")]
    public int? DamageScore { get; set; }

    [JsonProperty("adjusterId")]
    public string? AdjusterId { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
