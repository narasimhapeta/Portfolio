# Phase 1 — Python Fracture Predictor

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Python ML microservice that listens on RabbitMQ, runs a RandomForest screen-out risk prediction, and publishes results back to the results queue.

**Architecture:** FastAPI app with a background thread running a blocking RabbitMQ consumer loop. The ML model is trained once via a script and saved as a pickle artifact. Feature engineering lives in its own module. No HTTP surface for predictions — all communication is via message queue.

**Tech Stack:** Python 3.12 · FastAPI 0.115.x · Scikit-learn 1.5.x · Pika 1.3.x · Pydantic 2.x

**Depends on:** Phase 0 (RabbitMQ must be running)

---

## File Map

```
fractureguard-predictor/
├── pyproject.toml
├── Dockerfile
├── app/
│   ├── __init__.py
│   ├── main.py          FastAPI lifespan, /health endpoint, starts consumer thread
│   ├── consumer.py      RabbitMQ blocking consumer, calls predictor, publishes result
│   ├── predictor.py     Loads pickle model, runs inference, returns AnalysisResult
│   ├── features.py      Converts SensorSnapshot → numpy feature array
│   └── models.py        Pydantic schemas: SensorSnapshot, AnalysisRequest, AnalysisResult
├── ml/
│   └── screen_out_rf.pkl    (generated — not committed)
├── scripts/
│   └── train_model.py   Generates synthetic data, trains RF, saves pickle
└── tests/
    ├── test_features.py
    ├── test_predictor.py
    └── test_consumer.py
```

---

### Task 1: Project setup + feature engineering + model training

**Files:**
- Create: `fractureguard-predictor/pyproject.toml`
- Create: `fractureguard-predictor/app/models.py`
- Create: `fractureguard-predictor/app/features.py`
- Create: `fractureguard-predictor/scripts/train_model.py`
- Test: `fractureguard-predictor/tests/test_features.py`

- [ ] **Step 1: Create `pyproject.toml`**

```toml
[project]
name = "fractureguard-predictor"
version = "0.1.0"
requires-python = ">=3.12"
dependencies = [
    "fastapi==0.115.0",
    "uvicorn[standard]==0.30.0",
    "scikit-learn==1.5.2",
    "pandas==2.2.2",
    "numpy==1.26.4",
    "pika==1.3.2",
    "pydantic==2.8.2",
]

[project.optional-dependencies]
dev = [
    "pytest==8.3.2",
    "pytest-asyncio==0.23.8",
    "httpx==0.27.0",
]

[tool.pytest.ini_options]
testpaths = ["tests"]
asyncio_mode = "auto"
```

- [ ] **Step 2: Set up virtual environment**

```bash
cd fractureguard-predictor
python -m venv .venv
source .venv/bin/activate        # Windows: .venv\Scripts\activate
pip install -e ".[dev]"
```

Expected: All packages install without errors.

- [ ] **Step 3: Create `app/__init__.py`** (empty file)

```bash
touch app/__init__.py
```

- [ ] **Step 4: Create `app/models.py`**

```python
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
```

- [ ] **Step 5: Create `app/features.py`**

```python
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
    """Return a 1×7 feature array from a sensor snapshot."""
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
```

- [ ] **Step 6: Write failing test for `features.py`**

```python
# tests/test_features.py
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
```

- [ ] **Step 7: Run test — expect FAIL**

```bash
pytest tests/test_features.py -v
```

Expected: `ImportError` — modules not yet importable from root.

- [ ] **Step 8: Run test — expect PASS (modules now exist)**

```bash
pytest tests/test_features.py -v
```

Expected: All 3 tests PASS.

- [ ] **Step 9: Create `scripts/train_model.py`**

```python
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

    # Screen-out occurs when pressure is high AND vibration is high
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
```

- [ ] **Step 10: Run training script**

```bash
mkdir -p ml
python scripts/train_model.py
```

Expected: Classification report with F1 > 0.85, then `Model saved to ml/screen_out_rf.pkl`.

- [ ] **Step 11: Commit**

```bash
git add fractureguard-predictor/pyproject.toml \
        fractureguard-predictor/app/ \
        fractureguard-predictor/scripts/ \
        fractureguard-predictor/tests/test_features.py
git commit -m "feat(predictor): project setup, feature engineering, and trained RF model"
```

---

### Task 2: Predictor inference service

**Files:**
- Create: `fractureguard-predictor/app/predictor.py`
- Test: `fractureguard-predictor/tests/test_predictor.py`

- [ ] **Step 1: Write failing test**

```python
# tests/test_predictor.py
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
```

- [ ] **Step 2: Run — expect FAIL**

```bash
pytest tests/test_predictor.py -v
```

Expected: `ModuleNotFoundError: No module named 'app.predictor'`

- [ ] **Step 3: Create `app/predictor.py`**

```python
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
        session_id="",   # filled in by consumer
        risk_pct=round(risk_pct, 1),
        contributing_factors=contributing_factors,
        confidence=round(confidence, 3),
    )
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
pytest tests/test_predictor.py -v
```

Expected: All 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add fractureguard-predictor/app/predictor.py \
        fractureguard-predictor/tests/test_predictor.py
git commit -m "feat(predictor): RandomForest inference with contributing factors"
```

---

### Task 3: FastAPI app + RabbitMQ consumer + Dockerfile

**Files:**
- Create: `fractureguard-predictor/app/consumer.py`
- Create: `fractureguard-predictor/app/main.py`
- Create: `fractureguard-predictor/Dockerfile`
- Test: `fractureguard-predictor/tests/test_consumer.py`

- [ ] **Step 1: Write failing test for consumer message handling**

```python
# tests/test_consumer.py
import json
from unittest.mock import MagicMock
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

def test_handle_message_publishes_result():
    mock_channel = MagicMock()
    body = json.dumps(VALID_PAYLOAD).encode()

    handle_message(mock_channel, MagicMock(), MagicMock(), body)

    mock_channel.basic_publish.assert_called_once()
    call_kwargs = mock_channel.basic_publish.call_args.kwargs
    assert call_kwargs["routing_key"] == "analysis-results"
    result = json.loads(call_kwargs["body"])
    assert result["session_id"] == "test-session-1"
    assert "risk_pct" in result
    assert "contributing_factors" in result

def test_handle_message_acks_delivery():
    mock_channel = MagicMock()
    mock_method = MagicMock()
    mock_method.delivery_tag = 42
    body = json.dumps(VALID_PAYLOAD).encode()

    handle_message(mock_channel, mock_method, MagicMock(), body)

    mock_channel.basic_ack.assert_called_once_with(delivery_tag=42)
```

- [ ] **Step 2: Run — expect FAIL**

```bash
pytest tests/test_consumer.py -v
```

Expected: `ModuleNotFoundError: No module named 'app.consumer'`

- [ ] **Step 3: Create `app/consumer.py`**

```python
import json
import logging
import os
import time
import pika
from app.models import AnalysisRequest
from app.predictor import predict_screen_out

logger = logging.getLogger(__name__)

RABBITMQ_HOST = os.getenv("RABBITMQ_HOST", "localhost")
RABBITMQ_USER = os.getenv("RABBITMQ_USER", "guest")
RABBITMQ_PASS = os.getenv("RABBITMQ_PASS", "guest")
REQUEST_QUEUE  = "analysis-requests"
RESULT_QUEUE   = "analysis-results"

def handle_message(channel, method, properties, body: bytes) -> None:
    try:
        payload = json.loads(body)
        request = AnalysisRequest(**payload)
        result = predict_screen_out(request.sensor_snapshot)
        result.session_id = request.session_id

        channel.basic_publish(
            exchange="",
            routing_key=RESULT_QUEUE,
            body=result.model_dump_json(),
            properties=pika.BasicProperties(content_type="application/json"),
        )
        channel.basic_ack(delivery_tag=method.delivery_tag)
        logger.info("Published result for session %s risk=%.1f%%", result.session_id, result.risk_pct)
    except Exception:
        logger.exception("Failed to process message")
        channel.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

def start_consuming() -> None:
    while True:
        try:
            credentials = pika.PlainCredentials(RABBITMQ_USER, RABBITMQ_PASS)
            conn = pika.BlockingConnection(
                pika.ConnectionParameters(host=RABBITMQ_HOST, credentials=credentials)
            )
            ch = conn.channel()
            ch.queue_declare(queue=REQUEST_QUEUE, durable=True)
            ch.queue_declare(queue=RESULT_QUEUE, durable=True)
            ch.basic_qos(prefetch_count=1)
            ch.basic_consume(queue=REQUEST_QUEUE, on_message_callback=handle_message)
            logger.info("Waiting for analysis requests on %s", REQUEST_QUEUE)
            ch.start_consuming()
        except pika.exceptions.AMQPConnectionError:
            logger.warning("RabbitMQ not ready, retrying in 5s...")
            time.sleep(5)
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
pytest tests/test_consumer.py -v
```

Expected: Both tests PASS.

- [ ] **Step 5: Create `app/main.py`**

```python
import threading
import logging
from contextlib import asynccontextmanager
from fastapi import FastAPI
from app.consumer import start_consuming

logging.basicConfig(level=logging.INFO)

@asynccontextmanager
async def lifespan(app: FastAPI):
    thread = threading.Thread(target=start_consuming, daemon=True)
    thread.start()
    yield

app = FastAPI(title="FractureGuard Predictor", lifespan=lifespan)

@app.get("/health")
def health():
    return {"status": "ok"}
```

- [ ] **Step 6: Create `Dockerfile`**

```dockerfile
FROM python:3.12-slim
WORKDIR /app
COPY pyproject.toml .
RUN pip install -e .
COPY app/ app/
COPY ml/ ml/
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
```

- [ ] **Step 7: Build and verify Docker image**

```bash
docker build -t fractureguard-predictor .
```

Expected: Image builds without errors.

- [ ] **Step 8: Commit**

```bash
git add fractureguard-predictor/app/consumer.py \
        fractureguard-predictor/app/main.py \
        fractureguard-predictor/Dockerfile \
        fractureguard-predictor/tests/test_consumer.py
git commit -m "feat(predictor): FastAPI app with RabbitMQ consumer and Dockerfile"
```

---

*Phase 1 complete → Phase 5 (Integration) requires this phase*
