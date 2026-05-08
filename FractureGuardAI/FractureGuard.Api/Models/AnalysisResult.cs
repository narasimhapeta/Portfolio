namespace FractureGuard.Api.Models;

public record AnalysisResult(
    string SessionId,
    double RiskPct,
    List<string> ContributingFactors,
    double Confidence
);
