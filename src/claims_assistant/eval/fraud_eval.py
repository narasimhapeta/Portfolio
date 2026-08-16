# src/claims_assistant/eval/fraud_eval.py
from __future__ import annotations

from agent_framework import Agent

from claims_assistant.agents.coverage_agent import lookup_policy_by_number
from claims_assistant.agents.fraud_agent import (
    assess_fraud_risk,
    lookup_claims_history,
    lookup_vehicle_by_vin,
)
from claims_assistant.agents.fraud_signals import (
    FraudSignals,
    compute_fraud_signals,
    determine_actual_red_flags,
)
from claims_assistant.eval.judge import judge_grounding
from claims_assistant.eval.results import EvalResult, compute_composite_score
from claims_assistant.eval_fixtures import FraudFixture


def _evidence_text(signals: FraudSignals) -> str:
    return (
        f"Days since policy effective: {signals.days_since_policy_effective}\n"
        f"Prior claim count: {signals.claim_count}\n"
        f"Prior fraud-flagged claims: {signals.prior_fraud_flag_count}\n"
        f"Days since most recent prior claim: {signals.days_since_most_recent_prior_claim}\n"
        f"Highest prior claim amount: {signals.highest_prior_claim_amount_usd}\n"
        f"Vehicle market value: {signals.vehicle_market_value_usd}\n"
    )


async def run_fraud_eval(
    fraud_agent: Agent,
    judge_primary: Agent,
    judge_secondary: Agent,
    fixtures: list[FraudFixture],
) -> list[EvalResult]:
    results = []
    for fixture in fixtures:
        assessment = await assess_fraud_risk(
            fraud_agent,
            fixture.policy_number,
            fixture.vin,
            fixture.incident_date,
            fixture.claim_narrative,
        )
        tier_correct = float(assessment.risk_tier == fixture.gold_risk_tier)

        policy = await lookup_policy_by_number(fixture.policy_number)
        claims_history = await lookup_claims_history(fixture.policy_number)
        vehicle = await lookup_vehicle_by_vin(fixture.vin)
        signals = compute_fraud_signals(
            policy, claims_history, vehicle, fixture.incident_date
        )
        actual_flags = determine_actual_red_flags(signals)
        assert set(fixture.gold_red_flags) == actual_flags, (
            f"fixture {fixture.fixture_id} gold_red_flags stale vs deterministic "
            f"computation: gold={fixture.gold_red_flags} actual={sorted(actual_flags)}"
        )
        flags_correct = float(set(assessment.red_flags) == set(fixture.gold_red_flags))
        correctness = (tier_correct + flags_correct) / 2

        evidence_text = _evidence_text(signals)
        primary = await judge_grounding(judge_primary, assessment.rationale, evidence_text)
        secondary = await judge_grounding(judge_secondary, assessment.rationale, evidence_text)
        # Both judges must agree the rationale is grounded -- see Design Decisions: the
        # primary judge deployment (gpt-5.5) is the literal same model already deployed as
        # fraud-risk-agent, so scoring on the primary judge alone would let the agent's own
        # model grade its own rationale on the one agent spec Section 4 calls highest-stakes.
        # Requiring the distinct secondary judge (gpt-4.1) to also agree is what makes the
        # anti-self-preference-bias check actually gate the score here, not just annotate it.
        grounding = 1.0 if (primary.grounded and secondary.grounded) else 0.0

        results.append(
            EvalResult(
                agent="fraud",
                fixture_id=fixture.fixture_id,
                correctness_score=correctness,
                grounding_score=grounding,
                composite_score=compute_composite_score(correctness, grounding),
                primary_judge_grounded=primary.grounded,
                secondary_judge_grounded=secondary.grounded,
                judge_disagreement=primary.grounded != secondary.grounded,
            )
        )
    return results
