using System.Text.Json.Serialization;

namespace FractureGuard.Api.Models;

public record SensorSnapshot(
    [property: JsonPropertyName("pressure_psi")] double PressurePsi,
    [property: JsonPropertyName("pressure_trend_pct")] double PressureTrendPct,
    [property: JsonPropertyName("flow_rate_bpm")] double FlowRateBpm,
    [property: JsonPropertyName("flow_rate_variance")] double FlowRateVariance,
    [property: JsonPropertyName("vibration_g")] double VibrationG,
    [property: JsonPropertyName("temperature_c")] double TemperatureC
);
