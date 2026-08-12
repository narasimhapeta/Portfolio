# src/claims_assistant/models.py
from __future__ import annotations

import datetime

from sqlalchemy import ForeignKey
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship


class Base(DeclarativeBase):
    pass


class Policy(Base):
    """coverage_tier is one of: liability_only, full_coverage, comprehensive_collision."""

    __tablename__ = "policies"

    policy_number: Mapped[str] = mapped_column(primary_key=True)
    policyholder_name: Mapped[str]
    state: Mapped[str]
    coverage_tier: Mapped[str]
    policy_form_id: Mapped[str]
    effective_date: Mapped[datetime.date]
    expiration_date: Mapped[datetime.date]
    premium_monthly: Mapped[float]

    vehicles: Mapped[list["Vehicle"]] = relationship(back_populates="policy")
    claims: Mapped[list["ClaimHistory"]] = relationship(back_populates="policy")


class Vehicle(Base):
    __tablename__ = "vehicles"

    vin: Mapped[str] = mapped_column(primary_key=True)
    policy_number: Mapped[str] = mapped_column(ForeignKey("policies.policy_number"))
    make: Mapped[str]
    model: Mapped[str]
    year: Mapped[int]
    market_value_usd: Mapped[float]

    policy: Mapped["Policy"] = relationship(back_populates="vehicles")


class ClaimHistory(Base):
    """claim_type is one of: collision, comprehensive, liability, theft.
    status is one of: approved, denied, pending.
    """

    __tablename__ = "claims_history"

    claim_id: Mapped[str] = mapped_column(primary_key=True)
    policy_number: Mapped[str] = mapped_column(ForeignKey("policies.policy_number"))
    claim_date: Mapped[datetime.date]
    claim_type: Mapped[str]
    amount_usd: Mapped[float]
    status: Mapped[str]
    fraud_flag: Mapped[bool] = mapped_column(default=False)

    policy: Mapped["Policy"] = relationship(back_populates="claims")
