import pickle
from pathlib import Path
from app.features import extract_features, FEATURE_NAMES
from app.models import SensorSnapshot, AnalysisResult

_MODEL_PATH = Path(__file__).parent.parent / "ml" / "screen_out_rf.pkl"
_clf = None

def _get_model():
    global _clf
    if _clf is None:
        if not _MODEL_PATH.exists():
            raise FileNotFoundError(
                f"Model artefact not found: {_MODEL_PATH}. "
                "Run `python scripts/train_model.py` to generate it."
            )
        with open(_MODEL_PATH, "rb") as f:
            model = pickle.load(f)
        if not hasattr(model, "predict_proba") or not hasattr(model, "feature_importances_"):
            raise TypeError(f"Loaded object from {_MODEL_PATH} is not a fitted sklearn classifier.")
        _clf = model
    return _clf

def predict_screen_out(snapshot: SensorSnapshot, session_id: str = "") -> AnalysisResult:
    clf = _get_model()
    X = extract_features(snapshot)
    probas = clf.predict_proba(X)[0]
    risk_pct   = float(probas[1] * 100)
    confidence = float(max(probas))

    if hasattr(clf, "feature_importances_"):
        importances = clf.feature_importances_
        ranked = sorted(
            zip(FEATURE_NAMES, importances), key=lambda x: x[1], reverse=True
        )
        contributing_factors = [name for name, imp in ranked[:3] if imp > 0.05]
    else:
        contributing_factors = []

    return AnalysisResult(
        session_id=session_id,
        risk_pct=round(risk_pct, 1),
        contributing_factors=contributing_factors,
        confidence=round(confidence, 3),
    )
