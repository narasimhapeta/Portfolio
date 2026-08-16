# tests/test_fraud_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.fraud_schema import FraudRiskAssessment


def test_fraud_risk_assessment_validates():
    assessment = FraudRiskAssessment(
        risk_score=80,
        risk_tier="high",
        red_flags=["prior_fraud_flag", "recent_policy_inception"],
        rationale=(
            "Prior fraud-flagged claim and a new claim filed 17 days after the "
            "policy started."
        ),
    )

    assert assessment.risk_score == 80
    assert assessment.red_flags == ["prior_fraud_flag", "recent_policy_inception"]


def test_fraud_risk_assessment_rejects_score_out_of_range():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=150, risk_tier="high", red_flags=[], rationale="invalid score"
        )


def test_fraud_risk_assessment_rejects_invalid_tier():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=50, risk_tier="severe", red_flags=[], rationale="invalid tier"
        )


def test_fraud_risk_assessment_rejects_invalid_red_flag_code():
    with pytest.raises(ValidationError):
        FraudRiskAssessment(
            risk_score=50,
            risk_tier="medium",
            red_flags=["not_a_real_flag"],
            rationale="invalid flag",
        )
