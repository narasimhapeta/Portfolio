import json
from unittest.mock import MagicMock, patch
import numpy as np
import pytest
from app.consumer import handle_message

VALID_PAYLOAD = {
    "session_id": "test-session-1",
    "sensor_snapshot": {
        "pressure_psi": 850.0,
        "pressure_trend_pct": 14.0,
        "flow_rate_bpm": 13.0,
        "flow_rate_variance": 1.1,
        "vibration_g": 2.5,
        "temperature_c": 48.0,
    }
}


@pytest.fixture
def mock_clf():
    clf = MagicMock()
    clf.predict_proba.return_value = np.array([[0.2, 0.8]])
    clf.feature_importances_ = np.array([0.30, 0.20, 0.15, 0.10, 0.10, 0.10, 0.05])
    return clf


def test_handle_message_publishes_result(mock_clf):
    mock_channel = MagicMock()
    body = json.dumps(VALID_PAYLOAD).encode()

    with patch("app.predictor._get_model", return_value=mock_clf):
        handle_message(mock_channel, MagicMock(), MagicMock(), body)

    mock_channel.basic_publish.assert_called_once()
    call_kwargs = mock_channel.basic_publish.call_args.kwargs
    assert call_kwargs["routing_key"] == "analysis-results"
    result = json.loads(call_kwargs["body"])
    assert result["session_id"] == "test-session-1"
    assert "risk_pct" in result
    assert "contributing_factors" in result


def test_handle_message_acks_delivery(mock_clf):
    mock_channel = MagicMock()
    mock_method = MagicMock()
    mock_method.delivery_tag = 42
    body = json.dumps(VALID_PAYLOAD).encode()

    with patch("app.predictor._get_model", return_value=mock_clf):
        handle_message(mock_channel, mock_method, MagicMock(), body)

    mock_channel.basic_ack.assert_called_once_with(delivery_tag=42)


def test_handle_message_nacks_on_invalid_payload():
    mock_channel = MagicMock()
    mock_method = MagicMock()
    mock_method.delivery_tag = 99
    body = b"not valid json"

    handle_message(mock_channel, mock_method, MagicMock(), body)

    mock_channel.basic_nack.assert_called_once_with(delivery_tag=99, requeue=False)
    mock_channel.basic_publish.assert_not_called()
    mock_channel.basic_ack.assert_not_called()
