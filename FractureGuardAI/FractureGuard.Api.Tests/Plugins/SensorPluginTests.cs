using FluentAssertions;
using FractureGuard.Api.Plugins;

namespace FractureGuard.Api.Tests.Plugins;

public class SensorPluginTests
{
    [Fact]
    public async Task GetCurrentReadings_ReturnsSnapshot()
    {
        var mockHandler = new MockHttpMessageHandler(
            System.Net.HttpStatusCode.OK,
            """{"pressure_psi":847,"pressure_trend_pct":12.3,"flow_rate_bpm":12.4,"flow_rate_variance":0.8,"vibration_g":2.3,"temperature_c":42}"""
        );
        var plugin = new SensorPlugin(new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:3001")
        });

        var result = await plugin.GetCurrentReadingsAsync();

        result.Should().NotBeNull();
        result!.PressurePsi.Should().Be(847);
        result.VibrationG.Should().Be(2.3);
    }
}

public class MockHttpMessageHandler(System.Net.HttpStatusCode status, string body)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        });
}
