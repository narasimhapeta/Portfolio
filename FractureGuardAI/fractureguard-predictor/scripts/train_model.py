"""Generate synthetic fracking data and train the RandomForest model."""
import pickle
from pathlib import Path
import numpy as np
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report

RANDOM_SEED = 42
N_SAMPLES = 5000
MODEL_PATH = Path(__file__).parent.parent / "ml" / "screen_out_rf.pkl"

def generate_synthetic_data(n: int, seed: int) -> tuple[np.ndarray, np.ndarray]:
    rng = np.random.default_rng(seed)
    pressure_psi        = rng.uniform(400, 1000, n)
    pressure_trend_pct  = rng.uniform(-5, 20, n)
    flow_rate_bpm       = rng.uniform(5, 20, n)
    flow_rate_variance  = rng.uniform(0, 2, n)
    vibration_g         = rng.uniform(0.5, 3.5, n)
    temperature_c       = rng.uniform(30, 60, n)
    pressure_x_vibration = pressure_psi * vibration_g

    X = np.column_stack([
        pressure_psi, pressure_trend_pct, flow_rate_bpm,
        flow_rate_variance, vibration_g, temperature_c,
        pressure_x_vibration,
    ])

    risk_score = (
        (pressure_psi - 400) / 600 * 0.4
        + pressure_trend_pct / 20 * 0.3
        + (vibration_g - 0.5) / 3.0 * 0.3
    )
    y = (risk_score + rng.normal(0, 0.05, n) > 0.55).astype(int)
    return X, y

if __name__ == "__main__":
    X, y = generate_synthetic_data(N_SAMPLES, RANDOM_SEED)
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=RANDOM_SEED
    )
    clf = RandomForestClassifier(n_estimators=100, random_state=RANDOM_SEED)
    clf.fit(X_train, y_train)
    print(classification_report(y_test, clf.predict(X_test)))
    MODEL_PATH.parent.mkdir(exist_ok=True)
    with open(MODEL_PATH, "wb") as f:
        pickle.dump(clf, f)
    print(f"Model saved to {MODEL_PATH}")
