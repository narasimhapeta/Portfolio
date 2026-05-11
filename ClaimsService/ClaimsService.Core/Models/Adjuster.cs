// ClaimsService.Core/Models/Adjuster.cs
using Newtonsoft.Json;

namespace ClaimsService.Core.Models;

public class Adjuster
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("isAvailable")]
    public bool IsAvailable { get; set; } = true;
}
