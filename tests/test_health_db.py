# tests/test_health_db.py
import pytest
from fastapi.testclient import TestClient

from claims_assistant.main import create_app

pytestmark = pytest.mark.integration


def test_health_db_returns_ok_when_postgres_reachable():
    client = TestClient(create_app())

    response = client.get("/health/db")

    assert response.status_code == 200
    assert response.json() == {"status": "ok", "db": "reachable"}
