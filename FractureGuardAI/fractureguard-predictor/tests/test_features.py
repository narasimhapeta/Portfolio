import pytest
import numpy as np
from app.models import SensorSnapshot
from app.features import extract_features, FEATURE_NAMES

def make_snapshot(**overrides) -> SensorSnapshot:
    defaults = dict(
        pressure_psi=700.0,
        pressure_trend_pct=5.0,
        flow_rate_bpm=10.0,
        flow_rate_variance=0.5,
        vibration_g=1.2,
        temperature_c=38.0,
    )
    return SensorSnapshot(**{**defaults, **overrides})

def test_extract_features_returns_correct_shape():
    snap = make_snapshot()
    result = extract_features(snap)
    assert result.shape == (1, len(FEATURE_NAMES))

def test_extract_features_pressure_x_vibration_interaction():
    snap = make_snapshot(pressure_psi=800.0, vibration_g=2.0)
    result = extract_features(snap)
    assert result[0, -1] == pytest.approx(1600.0)

def test_extract_features_preserves_values():
    snap = make_snapshot(pressure_psi=847.0, flow_rate_bpm=12.4)
    result = extract_features(snap)
    assert result[0, 0] == pytest.approx(847.0)
    assert result[0, 2] == pytest.approx(12.4)
