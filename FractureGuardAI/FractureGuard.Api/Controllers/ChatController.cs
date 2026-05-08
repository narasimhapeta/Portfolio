using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FractureGuard.Api.Infrastructure;
using FractureGuard.Api.Models;
using FractureGuard.Api.Plugins;
using System.Security.Claims;
using System.Text;

namespace FractureGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController(
    Kernel kernel,
    ICosmosDbService cosmosDb,
    SensorPlugin sensorPlugin,
    RAGPlugin ragPlugin,
    PredictionPlugin predictionPlugin) : ControllerBase
{
    [HttpPost]
    public async Task Post([FromBody] ChatRequest request, CancellationToken ct)
    {
        var userId    = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        var session   = await cosmosDb.GetOrCreateSessionAsync(sessionId, userId);

        await cosmosDb.AppendMessageAsync(sessionId, userId,
            new ChatMessage("user", request.Message, DateTimeOffset.UtcNow));

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var snapshot      = await sensorPlugin.GetCurrentReadingsAsync();
        var safetyContext = await ragPlugin.GetSafetyContextAsync(request.Message);

        bool needsPrediction =
            request.Message.Contains("risk", StringComparison.OrdinalIgnoreCase)
            || request.Message.Contains("screen-out", StringComparison.OrdinalIgnoreCase)
            || request.Message.Contains("probability", StringComparison.OrdinalIgnoreCase);

        var history = new ChatHistory();
        history.AddSystemMessage(
            $"""
            You are FractureGuard AI, a safety analyst for a hydraulic fracturing site.
            Current sensor readings: {System.Text.Json.JsonSerializer.Serialize(snapshot)}
            Relevant safety protocols: {safetyContext}
            Be concise and technical. Always cite sensor values when making risk assessments.
            """
        );

        foreach (var msg in session.Messages)
            if (msg.Role == "user") history.AddUserMessage(msg.Content);
            else history.AddAssistantMessage(msg.Content);

        history.AddUserMessage(request.Message);

        if (needsPrediction && snapshot is not null)
        {
            var ack = await predictionPlugin.RequestPredictionAsync(sessionId, snapshot, User);
            history.AddAssistantMessage(ack);
            history.AddUserMessage(
                "While the simulation runs, briefly explain what current readings suggest.");
        }

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var sb   = new StringBuilder();

        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: ct))
        {
            var text = chunk.Content ?? string.Empty;
            sb.Append(text);
            await Response.WriteAsync($"data: {text}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        await cosmosDb.AppendMessageAsync(sessionId, userId,
            new ChatMessage("assistant", sb.ToString(), DateTimeOffset.UtcNow));
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetHistory(string sessionId)
    {
        var userId  = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var session = await cosmosDb.GetOrCreateSessionAsync(sessionId, userId);
        return Ok(session.Messages);
    }
}

public record ChatRequest(string Message, string? SessionId);
