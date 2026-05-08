using Moq;
using FluentAssertions;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Services;
using FractureGuard.Api.Models;
using System.Security.Claims;

namespace FractureGuard.Api.Tests.Plugins;

public class PredictionPluginTests
{
    private static SensorSnapshot TestSnapshot() => new(
        PressurePsi: 847, PressureTrendPct: 12.3,
        FlowRateBpm: 12.4, FlowRateVariance: 0.8,
        VibrationG: 2.3, TemperatureC: 42
    );

    [Fact]
    public async Task RequestPrediction_WithEngineerRole_PublishesJob()
    {
        var mockJobService = new Mock<IAnalysisJobService>();
        var plugin = new PredictionPlugin(mockJobService.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("roles", "SiteEngineer") }));

        var result = await plugin.RequestPredictionAsync("session-1", TestSnapshot(), principal);

        mockJobService.Verify(s => s.PublishAsync(
            It.Is<AnalysisRequest>(r => r.SessionId == "session-1")), Times.Once);
        result.Should().Contain("simulation");
    }

    [Fact]
    public async Task RequestPrediction_WithoutEngineerRole_ThrowsUnauthorized()
    {
        var mockJobService = new Mock<IAnalysisJobService>();
        var plugin = new PredictionPlugin(mockJobService.Object);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("roles", "SiteOperator") }));

        var act = async () => await plugin.RequestPredictionAsync("session-1", TestSnapshot(), principal);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        mockJobService.Verify(s => s.PublishAsync(It.IsAny<AnalysisRequest>()), Times.Never);
    }
}
