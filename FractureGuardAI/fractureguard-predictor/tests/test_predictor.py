import numpy as np
import pytest
from unittest.mock import MagicMock, patch
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


@pytest.fixture
def mock_clf():
    clf = MagicMock()
    clf.feature_importances_ = np.array([0.30, 0.20, 0.15, 0.10, 0.10, 0.10, 0.05])
    return clf


def test_predict_returns_analysis_result(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.1, 0.9]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert isinstance(result, AnalysisResult)


def test_high_risk_snapshot_returns_elevated_risk(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.1, 0.9]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert result.risk_pct > 50.0


def test_low_risk_snapshot_returns_low_risk(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.85, 0.15]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(LOW_RISK_SNAPSHOT)
    assert result.risk_pct < 50.0


def test_result_includes_contributing_factors(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.1, 0.9]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert len(result.contributing_factors) >= 1
    assert all(isinstance(f, str) for f in result.contributing_factors)


def test_confidence_is_between_0_and_1(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.1, 0.9]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(HIGH_RISK_SNAPSHOT)
    assert 0.0 <= result.confidence <= 1.0


def test_session_id_is_forwarded(mock_clf):
    mock_clf.predict_proba.return_value = np.array([[0.1, 0.9]])
    with patch("app.predictor._get_model", return_value=mock_clf):
        result = predict_screen_out(HIGH_RISK_SNAPSHOT, session_id="abc-123")
    assert result.session_id == "abc-123"
