using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Plugins;

public class SensorPlugin(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions _json =
        new(JsonSerializerDefaults.Web);

    [KernelFunction, Description("Fetches the latest live sensor readings from the fracking site")]
    public async Task<SensorSnapshot?> GetCurrentReadingsAsync()
    {
        var response = await httpClient.GetAsync("/api/sensors/latest");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SensorSnapshot>(_json);
    }
}
