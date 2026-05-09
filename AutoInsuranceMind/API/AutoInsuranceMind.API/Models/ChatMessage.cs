namespace AutoInsuranceMind.API.Models;

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public bool UsedRag { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? DocumentId { get; set; }
}
