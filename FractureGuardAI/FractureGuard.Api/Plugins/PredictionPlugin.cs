using System.ComponentModel;
using System.Security.Claims;
using Microsoft.SemanticKernel;
using FractureGuard.Api.Models;
using FractureGuard.Api.Services;

namespace FractureGuard.Api.Plugins;

public class PredictionPlugin(IAnalysisJobService jobService)
{
    [KernelFunction, Description("Submits a screen-out risk simulation. Requires SiteEngineer role.")]
    public async Task<string> RequestPredictionAsync(
        [Description("The current chat session ID")] string sessionId,
        [Description("Current sensor snapshot")] SensorSnapshot snapshot,
        ClaimsPrincipal caller)
    {
        var roles = caller.FindAll("roles").Select(c => c.Value);
        if (!roles.Contains("SiteEngineer"))
            throw new UnauthorizedAccessException("ML simulations require the SiteEngineer role.");

        await jobService.PublishAsync(new AnalysisRequest(sessionId, snapshot));
        return "Screen-out simulation submitted. I'll push the results to your dashboard when the analysis completes.";
    }
}
