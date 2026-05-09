using AutoInsuranceMind.API.Models;
using AutoInsuranceMind.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoInsuranceMind.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AIController : ControllerBase
{
    private readonly AIService _aiService;
    private readonly ILogger<AIController> _logger;

    public AIController(AIService aiService, ILogger<AIController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message cannot be empty" });

        try
        {
            var (response, sources, usedRag) = await _aiService.ProcessMessageAsync(
                request.Message, request.DocumentId);

            var chatMsg = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                CustomerId = request.CustomerId ?? "cust-001",
                Message = request.Message,
                Response = response,
                Sources = sources,
                UsedRag = usedRag,
                Timestamp = DateTime.UtcNow
            };

            // Keep last 50 messages in history
            var history = _aiService.GetChatHistory();
            history.Add(chatMsg);
            if (history.Count > 50) history.RemoveAt(0);

            return Ok(chatMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat processing failed");
            return StatusCode(500, new { message = "Failed to process your message. Please try again." });
        }
    }

    [HttpGet("chat/history")]
    public IActionResult GetChatHistory([FromQuery] string customerId = "cust-001")
    {
        var history = _aiService.GetChatHistory()
            .Where(m => m.CustomerId == customerId)
            .OrderBy(m => m.Timestamp)
            .ToList();
        return Ok(new { history, total = history.Count });
    }

    [HttpPost("reset")]
    public IActionResult ResetChat()
    {
        _aiService.ResetChat();
        return Ok(new { success = true, message = "Chat history cleared" });
    }
}
