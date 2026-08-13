# src/claims_assistant/seed_data.py
from __future__ import annotations

import datetime
from typing import Any

from sqlalchemy import delete

from claims_assistant.database import get_session_factory
from claims_assistant.models import ClaimHistory, Policy, Vehicle

POLICIES: list[dict[str, Any]] = [
    {
        "policy_number": "POL-CA-0001",
        "policyholder_name": "Maria Gonzalez",
        "state": "CA",
        "coverage_tier": "liability_only",
        "policy_form_id": "CA-LIABILITY-ONLY",
        "effective_date": datetime.date(2025, 1, 15),
        "expiration_date": datetime.date(2026, 1, 15),
        "premium_monthly": 89.00,
    },
    {
        "policy_number": "POL-CA-0002",
        "policyholder_name": "James Whitfield",
        "state": "CA",
        "coverage_tier": "full_coverage",
        "policy_form_id": "CA-FULL-COVERAGE",
        "effective_date": datetime.date(2025, 3, 1),
        "expiration_date": datetime.date(2026, 3, 1),
        "premium_monthly": 156.50,
    },
    {
        "policy_number": "POL-CA-0003",
        "policyholder_name": "Priya Natarajan",
        "state": "CA",
        "coverage_tier": "comprehensive_collision",
        "policy_form_id": "CA-COMPREHENSIVE-COLLISION",
        "effective_date": datetime.date(2025, 5, 20),
        "expiration_date": datetime.date(2026, 5, 20),
        "premium_monthly": 210.75,
    },
    {
        "policy_number": "POL-TX-0004",
        "policyholder_name": "Robert Kessler",
        "state": "TX",
        "coverage_tier": "liability_only",
        "policy_form_id": "TX-LIABILITY-ONLY",
        "effective_date": datetime.date(2025, 2, 10),
        "expiration_date": datetime.date(2026, 2, 10),
        "premium_monthly": 72.25,
    },
    {
        "policy_number": "POL-TX-0005",
        "policyholder_name": "Angela Brooks",
        "state": "TX",
        "coverage_tier": "full_coverage",
        "policy_form_id": "TX-FULL-COVERAGE",
        "effective_date": datetime.date(2025, 6, 1),
        "expiration_date": datetime.date(2026, 6, 1),
        "premium_monthly": 148.00,
    },
    {
        "policy_number": "POL-TX-0006",
        "policyholder_name": "Derek Owusu",
        "state": "TX",
        "coverage_tier": "comprehensive_collision",
        "policy_form_id": "TX-COMPREHENSIVE-COLLISION",
        "effective_date": datetime.date(2025, 7, 15),
        "expiration_date": datetime.date(2026, 7, 15),
        "premium_monthly": 198.40,
    },
    {
        "policy_number": "POL-NY-0007",
        "policyholder_name": "Linda Park",
        "state": "NY",
        "coverage_tier": "liability_only",
        "policy_form_id": "NY-LIABILITY-ONLY",
        "effective_date": datetime.date(2025, 1, 1),
        "expiration_date": datetime.date(2026, 1, 1),
        "premium_monthly": 95.60,
    },
    {
        "policy_number": "POL-NY-0008",
        "policyholder_name": "Michael Ferraro",
        "state": "NY",
        "coverage_tier": "full_coverage",
        "policy_form_id": "NY-FULL-COVERAGE",
        "effective_date": datetime.date(2025, 4, 18),
        "expiration_date": datetime.date(2026, 4, 18),
        "premium_monthly": 175.25,
    },
    {
        "policy_number": "POL-NY-0009",
        "policyholder_name": "Samantha Cruz",
        "state": "NY",
        "coverage_tier": "comprehensive_collision",
        "policy_form_id": "NY-COMPREHENSIVE-COLLISION",
        "effective_date": datetime.date(2025, 8, 1),
        "expiration_date": datetime.date(2026, 8, 1),
        "premium_monthly": 225.90,
    },
]

VEHICLES: list[dict[str, Any]] = [
    {
        "vin": "1FADP3F20EL123456",
        "policy_number": "POL-CA-0001",
        "make": "Ford",
        "model": "Focus",
        "year": 2018,
        "market_value_usd": 8200.00,
    },
    {
        "vin": "5YJ3E1EA7JF123457",
        "policy_number": "POL-CA-0002",
        "make": "Tesla",
        "model": "Model 3",
        "year": 2021,
        "market_value_usd": 28500.00,
    },
    {
        "vin": "1C4RJFBG5FC123458",
        "policy_number": "POL-CA-0003",
        "make": "Jeep",
        "model": "Grand Cherokee",
        "year": 2020,
        "market_value_usd": 24300.00,
    },
    {
        "vin": "3GNAXUEV5LL123459",
        "policy_number": "POL-TX-0004",
        "make": "Chevrolet",
        "model": "Equinox",
        "year": 2019,
        "market_value_usd": 15800.00,
    },
    {
        "vin": "1HGCV1F34LA123460",
        "policy_number": "POL-TX-0005",
        "make": "Honda",
        "model": "Accord",
        "year": 2022,
        "market_value_usd": 23400.00,
    },
    {
        "vin": "1FTFW1ET5EF123461",
        "policy_number": "POL-TX-0006",
        "make": "Ford",
        "model": "F-150",
        "year": 2017,
        "market_value_usd": 19750.00,
    },
    {
        "vin": "2T1BURHE0JC123462",
        "policy_number": "POL-NY-0007",
        "make": "Toyota",
        "model": "Corolla",
        "year": 2020,
        "market_value_usd": 14200.00,
    },
    {
        "vin": "WBA8E9G59JNU12345",
        "policy_number": "POL-NY-0008",
        "make": "BMW",
        "model": "3 Series",
        "year": 2019,
        "market_value_usd": 21600.00,
    },
    {
        "vin": "5NPE34AF9KH123464",
        "policy_number": "POL-NY-0009",
        "make": "Hyundai",
        "model": "Sonata",
        "year": 2021,
        "market_value_usd": 16900.00,
    },
]

CLAIMS: list[dict[str, Any]] = [
    {
        "claim_id": "CLM-0001",
        "policy_number": "POL-CA-0002",
        "claim_date": datetime.date(2025, 3, 5),
        "claim_type": "collision",
        "amount_usd": 6200.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0002",
        "policy_number": "POL-CA-0002",
        "claim_date": datetime.date(2025, 6, 12),
        "claim_type": "theft",
        "amount_usd": 3400.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0003",
        "policy_number": "POL-CA-0002",
        "claim_date": datetime.date(2025, 9, 2),
        "claim_type": "collision",
        "amount_usd": 7800.00,
        "status": "denied",
        "fraud_flag": True,
    },
    {
        "claim_id": "CLM-0004",
        "policy_number": "POL-CA-0003",
        "claim_date": datetime.date(2025, 11, 1),
        "claim_type": "comprehensive",
        "amount_usd": 2100.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0005",
        "policy_number": "POL-TX-0005",
        "claim_date": datetime.date(2025, 6, 20),
        "claim_type": "collision",
        "amount_usd": 4500.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0006",
        "policy_number": "POL-TX-0005",
        "claim_date": datetime.date(2026, 1, 10),
        "claim_type": "collision",
        "amount_usd": 5100.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0007",
        "policy_number": "POL-TX-0006",
        "claim_date": datetime.date(2025, 7, 20),
        "claim_type": "theft",
        "amount_usd": 19750.00,
        "status": "pending",
        "fraud_flag": True,
    },
    {
        "claim_id": "CLM-0008",
        "policy_number": "POL-NY-0008",
        "claim_date": datetime.date(2025, 5, 1),
        "claim_type": "collision",
        "amount_usd": 3900.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0009",
        "policy_number": "POL-NY-0009",
        "claim_date": datetime.date(2025, 8, 15),
        "claim_type": "collision",
        "amount_usd": 5600.00,
        "status": "approved",
        "fraud_flag": False,
    },
    {
        "claim_id": "CLM-0010",
        "policy_number": "POL-NY-0009",
        "claim_date": datetime.date(2025, 12, 1),
        "claim_type": "comprehensive",
        "amount_usd": 2200.00,
        "status": "approved",
        "fraud_flag": False,
    },
]


async def seed_database() -> dict[str, int]:
    session_factory = get_session_factory()
    async with session_factory() as session, session.begin():
        await session.execute(delete(ClaimHistory))
        await session.execute(delete(Vehicle))
        await session.execute(delete(Policy))
        session.add_all(Policy(**row) for row in POLICIES)  
        session.add_all(Vehicle(**row) for row in VEHICLES)  
        session.add_all(ClaimHistory(**row) for row in CLAIMS)  
    return {
        "policies": len(POLICIES),
        "vehicles": len(VEHICLES),
        "claims_history": len(CLAIMS),
    }
