from pydantic import BaseModel

class SensorSnapshot(BaseModel):
    pressure_psi: float
    pressure_trend_pct: float
    flow_rate_bpm: float
    flow_rate_variance: float
    vibration_g: float
    temperature_c: float

class AnalysisRequest(BaseModel):
    session_id: str
    sensor_snapshot: SensorSnapshot

class AnalysisResult(BaseModel):
    session_id: str
    risk_pct: float
    contributing_factors: list[str]
    confidence: float
