using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CustomerPortal.ApiTests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
                          .CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkWithHealthyBody()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task GetHealth_WithAllowedOrigin_IncludesCorsHeader()
    {
        _client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");

        var response = await _client.GetAsync("/health");

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Equal("http://localhost:5173", values!.Single());
    }
}
