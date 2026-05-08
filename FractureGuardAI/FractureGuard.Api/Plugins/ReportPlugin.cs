using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FractureGuard.Api.Models;

namespace FractureGuard.Api.Plugins;

public class ReportPlugin(Kernel kernel)
{
    public string BuildReportPrompt(AnalysisResult result, string safetyContext) =>
        $"""
        You are a fracking site safety analyst. Generate a concise technical report.

        ML PREDICTION:
        - Screen-out risk: {result.RiskPct}% (confidence {result.Confidence:P0})
        - Primary drivers: {string.Join(", ", result.ContributingFactors)}

        RELEVANT SAFETY PROTOCOLS:
        {safetyContext}

        Write 3-5 sentences: state the risk level, explain the primary drivers in plain English,
        cite the relevant protocol, and give one concrete recommended action.
        """;

    [KernelFunction, Description("Generates a plain-English technical report from ML prediction output")]
    public async Task<string> GenerateReportAsync(AnalysisResult result, string safetyContext = "")
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddUserMessage(BuildReportPrompt(result, safetyContext));

        var response = await chat.GetChatMessageContentAsync(history);
        return response.Content ?? $"Screen-out risk: {result.RiskPct}% — analysis complete.";
    }
}
