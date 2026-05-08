using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Plugins;

public class SensorPlugin(HttpClient httpClient, ILogger<SensorPlugin> logger)
{
    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    [KernelFunction, Description("Fetches the latest live sensor readings from the fracking site")]
    public async Task<SensorSnapshot?> GetCurrentReadingsAsync()
    {
        try
        {
            var response = await httpClient.GetAsync("/api/sensors/latest");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Sensor endpoint returned {Status}", response.StatusCode);
                return null;
            }
            var snapshot = await response.Content.ReadFromJsonAsync<SensorSnapshot>(_json);
            if (snapshot is null)
                logger.LogWarning("Sensor endpoint returned null or empty JSON");
            return snapshot;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reach sensor endpoint");
            return null;
        }
    }
}
