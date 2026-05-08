namespace FractureGuard.Api.Models;

public record RiskReport(
    string SessionId,
    string Content,
    double RiskPct,
    DateTimeOffset GeneratedAt
);
