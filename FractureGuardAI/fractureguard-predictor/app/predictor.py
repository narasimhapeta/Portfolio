import pickle
from pathlib import Path
from app.features import extract_features, FEATURE_NAMES
from app.models import SensorSnapshot, AnalysisResult

_MODEL_PATH = Path(__file__).parent.parent / "ml" / "screen_out_rf.pkl"

def _load_model():
    with open(_MODEL_PATH, "rb") as f:
        return pickle.load(f)

_clf = _load_model()

def predict_screen_out(snapshot: SensorSnapshot) -> AnalysisResult:
    X = extract_features(snapshot)
    probas = _clf.predict_proba(X)[0]
    risk_pct   = float(probas[1] * 100)
    confidence = float(max(probas))

    importances = _clf.feature_importances_
    ranked = sorted(
        zip(FEATURE_NAMES, importances), key=lambda x: x[1], reverse=True
    )
    contributing_factors = [name for name, imp in ranked[:3] if imp > 0.05]

    return AnalysisResult(
        session_id="",
        risk_pct=round(risk_pct, 1),
        contributing_factors=contributing_factors,
        confidence=round(confidence, 3),
    )
