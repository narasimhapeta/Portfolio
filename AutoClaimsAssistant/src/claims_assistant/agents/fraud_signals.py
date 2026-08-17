# src/claims_assistant/agents/fraud_signals.py
from __future__ import annotations

import datetime
from dataclasses import dataclass
from typing import Literal

from claims_assistant.mcp_servers.claims_history import ClaimsHistoryResult
from claims_assistant.mcp_servers.policy_db import PolicyLookupResult
from claims_assistant.mcp_servers.vin_vehicle import VehicleLookupResult

RedFlagCode = Literal[
    "recent_policy_inception",
    "high_claim_frequency",
    "prior_fraud_flag",
    "clustered_recent_claims",
    "prior_claim_near_vehicle_value",
]

RECENT_POLICY_INCEPTION_DAYS = 30
CLUSTERED_CLAIMS_DAYS = 45
HIGH_FREQUENCY_CLAIM_COUNT = 2
NEAR_MARKET_VALUE_RATIO = 0.9


@dataclass(frozen=True)
class FraudSignals:
    policy_number: str
    incident_date: datetime.date
    policy_effective_date: datetime.date
    days_since_policy_effective: int
    claim_count: int
    prior_fraud_flag_count: int
    most_recent_prior_claim_date: datetime.date | None
    days_since_most_recent_prior_claim: int | None
    vehicle_make: str
    vehicle_model: str
    vehicle_year: int
    vehicle_market_value_usd: float
    highest_prior_claim_amount_usd: float | None
    highest_prior_claim_to_market_value_ratio: float | None


def compute_fraud_signals(
    policy: PolicyLookupResult,
    claims_history: ClaimsHistoryResult,
    vehicle: VehicleLookupResult,
    incident_date: str,
) -> FraudSignals:
    incident = datetime.date.fromisoformat(incident_date)
    effective = datetime.date.fromisoformat(policy.effective_date)
    most_recent = (
        datetime.date.fromisoformat(claims_history.most_recent_claim_date)
        if claims_history.most_recent_claim_date
        else None
    )
    # claims_history.claims aren't tied to a specific vehicle (ClaimHistory has no VIN),
    # so this assumes one vehicle per policy — true for all current seed data, but a future
    # multi-vehicle policy would need this signal recomputed per-vehicle.
    highest_amount = (
        max(c.amount_usd for c in claims_history.claims) if claims_history.claims else None
    )
    return FraudSignals(
        policy_number=policy.policy_number,
        incident_date=incident,
        policy_effective_date=effective,
        days_since_policy_effective=(incident - effective).days,
        claim_count=claims_history.claim_count,
        prior_fraud_flag_count=claims_history.prior_fraud_flag_count,
        most_recent_prior_claim_date=most_recent,
        days_since_most_recent_prior_claim=(
            (incident - most_recent).days if most_recent else None
        ),
        vehicle_make=vehicle.make,
        vehicle_model=vehicle.model,
        vehicle_year=vehicle.year,
        vehicle_market_value_usd=vehicle.market_value_usd,
        highest_prior_claim_amount_usd=highest_amount,
        highest_prior_claim_to_market_value_ratio=(
            highest_amount / vehicle.market_value_usd if highest_amount is not None else None
        ),
    )


def determine_actual_red_flags(signals: FraudSignals) -> set[RedFlagCode]:
    flags: set[RedFlagCode] = set()
    if 0 <= signals.days_since_policy_effective < RECENT_POLICY_INCEPTION_DAYS:
        flags.add("recent_policy_inception")
    if signals.claim_count >= HIGH_FREQUENCY_CLAIM_COUNT:
        flags.add("high_claim_frequency")
    if signals.prior_fraud_flag_count > 0:
        flags.add("prior_fraud_flag")
    if (
        signals.days_since_most_recent_prior_claim is not None
        and 0 <= signals.days_since_most_recent_prior_claim < CLUSTERED_CLAIMS_DAYS
    ):
        flags.add("clustered_recent_claims")
    if (
        signals.highest_prior_claim_to_market_value_ratio is not None
        and signals.highest_prior_claim_to_market_value_ratio >= NEAR_MARKET_VALUE_RATIO
    ):
        flags.add("prior_claim_near_vehicle_value")
    return flags
