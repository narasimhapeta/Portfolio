namespace FractureGuard.Api.Models;

public record SensorSnapshot(
    double PressurePsi,
    double PressureTrendPct,
    double FlowRateBpm,
    double FlowRateVariance,
    double VibrationG,
    double TemperatureC
);
