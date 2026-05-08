using FluentAssertions;
using FractureGuard.Api.Plugins;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Tests.Plugins;

public class ReportPluginTests
{
    [Fact]
    public void BuildReportPrompt_IncludesRiskAndFactors()
    {
        var plugin = new ReportPlugin(null!);
        var result = new AnalysisResult(
            SessionId: "test",
            RiskPct: 85.0,
            ContributingFactors: ["pressure_trend", "vibration_amplitude"],
            Confidence: 0.91
        );

        var prompt = plugin.BuildReportPrompt(result, "Pressure exceeds threshold per Protocol 4.2");

        prompt.Should().Contain("85");
        prompt.Should().Contain("pressure_trend");
        prompt.Should().Contain("Protocol 4.2");
    }
}
