namespace FractureGuard.Api.Models;

public record ChatMessage(
    string Role,
    string Content,
    DateTimeOffset Timestamp
);
