from __future__ import annotations

import pytest

from claims_assistant.frontend.auth import verify_password


def test_verify_password_accepts_correct_password(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("FRONTEND_ACCESS_PASSWORD", "correct-horse")
    assert verify_password("correct-horse") is True


def test_verify_password_rejects_incorrect_password(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("FRONTEND_ACCESS_PASSWORD", "correct-horse")
    assert verify_password("wrong") is False
