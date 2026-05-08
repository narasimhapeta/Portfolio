import numpy as np
from app.models import SensorSnapshot

FEATURE_NAMES = [
    "pressure_psi",
    "pressure_trend_pct",
    "flow_rate_bpm",
    "flow_rate_variance",
    "vibration_g",
    "temperature_c",
    "pressure_x_vibration",   # interaction: key screen-out signal
]

def extract_features(snapshot: SensorSnapshot) -> np.ndarray:
    """Return a 1x7 feature array from a sensor snapshot."""
    pressure_x_vibration = snapshot.pressure_psi * snapshot.vibration_g
    row = [
        snapshot.pressure_psi,
        snapshot.pressure_trend_pct,
        snapshot.flow_rate_bpm,
        snapshot.flow_rate_variance,
        snapshot.vibration_g,
        snapshot.temperature_c,
        pressure_x_vibration,
    ]
    return np.array(row, dtype=float).reshape(1, -1)
