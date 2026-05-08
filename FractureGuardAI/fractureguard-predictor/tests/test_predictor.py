import pytest
from app.models import SensorSnapshot, AnalysisResult
from app.predictor import predict_screen_out

HIGH_RISK_SNAPSHOT = SensorSnapshot(
    pressure_psi=900.0, pressure_trend_pct=18.0,
    flow_rate_bpm=15.0, flow_rate_variance=1.5,
    vibration_g=3.0, temperature_c=55.0,
)
LOW_RISK_SNAPSHOT = SensorSnapshot(
    pressure_psi=450.0, pressure_trend_pct=1.0,
    flow_rate_bpm=8.0, flow_rate_variance=0.2,
    vibration_g=0.6, temperature_c=32.0,
)

def test_predict_returns_analysis_result():
    result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert isinstance(result, AnalysisResult)

def test_high_risk_snapshot_returns_elevated_risk():
    result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert result.risk_pct > 50.0

def test_low_risk_snapshot_returns_low_risk():
    result = predict_screen_out(LOW_RISK_SNAPSHOT)
    assert result.risk_pct < 50.0

def test_result_includes_contributing_factors():
    result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert len(result.contributing_factors) >= 1
    assert all(isinstance(f, str) for f in result.contributing_factors)

def test_confidence_is_between_0_and_1():
    result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert 0.0 <= result.confidence <= 1.0
