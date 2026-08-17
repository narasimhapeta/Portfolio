# Phase 1: Synthetic Data Generation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Produce all the synthetic ground-truth data later phases depend on: a seeded Postgres schema (policies, vehicles, claims history), a synthetic policy-document corpus (9 documents spanning liability-only / full-coverage / comprehensive-collision tiers across 3 states), and a starter set of FNOL extraction eval fixtures with gold JSON — all deterministic, hand-authored, or template-generated (no LLM calls, no external sourcing).

**Architecture:** SQLAlchemy 2.0 async ORM models on top of the existing Postgres (Task 4 of Phase 0) via a new `database.py` engine/session module — kept separate from Phase 0's `db.py` (which stays as the raw-`asyncpg` pool backing `/health/db`; consolidating the two is unnecessary churn for this phase). Seed data lives as plain Python literals in `seed_data.py`, inserted by an idempotent `seed_database()` function. Policy documents are produced by a small deterministic template generator (`policy_documents.py`) rather than hand-written prose, so the corpus is reproducible and testable. FNOL eval fixtures are hand-authored text+JSON pairs under `data/eval_fixtures/extraction/`, validated against a new Pydantic `FNOLFacts` schema that mirrors the spec's fixed extraction schema (minus per-field confidence, which is a Phase 3 extraction-time output, not a ground-truth attribute).

**Tech Stack:** SQLAlchemy 2.0 (`sqlalchemy[asyncio]`) + `asyncpg` driver (already a dependency), Pydantic v2, pytest + pytest-asyncio (`integration` marker from Phase 0), plain-Python template strings for document generation (no Jinja2 — templates are simple enough not to need it).

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All DB-touching functions are `async def` (per Phase 0's async I/O constraint).
- No LLM calls and no externally-sourced data anywhere in this phase — everything is deterministic Python or hand-authored text, so eval fixture gold JSON is trustworthy ground truth (spec §5.1, §6).
- `data/` is a permanent, git-tracked project artifact directory (not gitignored) — the policy corpus and eval fixtures are committed, not regenerated per-run.
- Every dependency addition goes through `uv add`.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Integration tests (`pytest.mark.integration`) require `docker-compose up -d postgres` running first, same as Phase 0.

---

### Task 1: SQLAlchemy models + async engine/session

**Files:**
- Modify: `src/claims_assistant/config.py`
- Modify: `tests/test_config.py`
- Create: `src/claims_assistant/models.py`
- Create: `src/claims_assistant/database.py`
- Test: `tests/test_database.py`

**Interfaces:**
- Consumes: `get_settings()` (Phase 0's `config.py`).
- Produces: `Settings.postgres_async_dsn: str` property; `Base` (SQLAlchemy `DeclarativeBase`), `Policy`, `Vehicle`, `ClaimHistory` ORM classes in `models.py`; `get_engine() -> AsyncEngine`, `get_session_factory() -> async_sessionmaker[AsyncSession]`, `create_all_tables() -> None` (async) in `database.py`. Later tasks in this plan import all of these.

- [ ] **Step 1: Add the SQLAlchemy dependency**

Run (PowerShell):
```powershell
uv add "sqlalchemy[asyncio]>=2"
```

- [ ] **Step 2: Extend the config test for the async DSN**

Replace `test_settings_reads_from_env` in `tests/test_config.py` with:

```python
# tests/test_config.py
import os

from claims_assistant.config import Settings, get_settings


def test_settings_reads_from_env(monkeypatch):
    monkeypatch.setenv("APP_ENV", "test")
    monkeypatch.setenv("POSTGRES_HOST", "db.example")
    monkeypatch.setenv("POSTGRES_PORT", "5433")
    monkeypatch.setenv("POSTGRES_DB", "testdb")
    monkeypatch.setenv("POSTGRES_USER", "testuser")
    monkeypatch.setenv("POSTGRES_PASSWORD", "testpass")

    settings = Settings()

    assert settings.app_env == "test"
    assert settings.postgres_host == "db.example"
    assert settings.postgres_port == 5433
    assert settings.postgres_dsn == (
        "postgresql://testuser:testpass@db.example:5433/testdb"
    )
    assert settings.postgres_async_dsn == (
        "postgresql+asyncpg://testuser:testpass@db.example:5433/testdb"
    )


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'postgres_async_dsn'`

- [ ] **Step 4: Add the `postgres_async_dsn` property**

In `src/claims_assistant/config.py`, add this property directly below `postgres_dsn`:

```python
    @property
    def postgres_async_dsn(self) -> str:
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [ ] **Step 6: Write the failing integration test for schema creation**

```python
# tests/test_database.py
import pytest
from sqlalchemy import text

from claims_assistant.database import create_all_tables, get_engine

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_create_all_tables_creates_expected_tables():
    await create_all_tables()

    engine = get_engine()
    async with engine.connect() as conn:
        result = await conn.execute(
            text(
                "SELECT table_name FROM information_schema.tables "
                "WHERE table_schema = 'public'"
            )
        )
        table_names = {row[0] for row in result}

    assert {"policies", "vehicles", "claims_history"}.issubset(table_names)
```

- [ ] **Step 7: Run the test to verify it fails**

Run: `docker-compose up -d postgres` then `uv run pytest tests/test_database.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.database'`

- [ ] **Step 8: Write the ORM models**

```python
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
```

- [ ] **Step 9: Write the engine/session module**

```python
# src/claims_assistant/database.py
from sqlalchemy.ext.asyncio import (
    AsyncEngine,
    AsyncSession,
    async_sessionmaker,
    create_async_engine,
)

from claims_assistant.config import get_settings
from claims_assistant.models import Base

_engine: AsyncEngine | None = None
_session_factory: async_sessionmaker[AsyncSession] | None = None


def get_engine() -> AsyncEngine:
    global _engine
    if _engine is None:
        _engine = create_async_engine(get_settings().postgres_async_dsn)
    return _engine


def get_session_factory() -> async_sessionmaker[AsyncSession]:
    global _session_factory
    if _session_factory is None:
        _session_factory = async_sessionmaker(get_engine(), expire_on_commit=False)
    return _session_factory


async def create_all_tables() -> None:
    engine = get_engine()
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
```

- [ ] **Step 10: Run the test to verify it passes**

Ensure `docker-compose up -d postgres` is running (from Step 7), then:
Run: `uv run pytest tests/test_database.py -v`
Expected: PASS (1 passed)

- [ ] **Step 11: Commit**

```powershell
git add src/claims_assistant/config.py src/claims_assistant/models.py src/claims_assistant/database.py tests/test_config.py tests/test_database.py pyproject.toml uv.lock
git commit -m "feat: add SQLAlchemy models and async engine for policies/vehicles/claims"
```

---

### Task 2: Deterministic seed data

**Files:**
- Create: `src/claims_assistant/seed_data.py`
- Test: `tests/test_seed_data.py`

**Interfaces:**
- Consumes: `Policy`, `Vehicle`, `ClaimHistory` (Task 1's `models.py`), `get_session_factory()` (Task 1's `database.py`).
- Produces: `POLICIES: list[dict[str, Any]]`, `VEHICLES: list[dict[str, Any]]`, `CLAIMS: list[dict[str, Any]]` module-level constants, and `async def seed_database() -> dict[str, int]` (deletes existing rows, inserts the seed set, returns row counts per table). Task 3's cross-check test reads `POLICIES` directly; Task 4's CLI script calls `seed_database()`.

This seed set is deliberately designed so later phases have real signal to work with: `POL-CA-0002` has 3 prior claims including one fraud-flagged/denied claim (frequency + prior-flag red flags for Phase 5's Fraud-Risk Agent); `POL-TX-0006` has a theft claim filed 5 days after the policy's effective date for an amount matching the vehicle's full market value (timing + total-loss red flags); `POL-CA-0001`, `POL-TX-0004`, and `POL-NY-0007` have clean (empty) claim histories, giving Phase 5 a "one clean, one flagged" pair as the roadmap's Phase 5 success criteria requires.

- [ ] **Step 1: Write the failing integration test**

```python
# tests/test_seed_data.py
import pytest
from sqlalchemy import select

from claims_assistant.database import create_all_tables, get_session_factory
from claims_assistant.models import ClaimHistory, Policy
from claims_assistant.seed_data import seed_database

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_seed_database_populates_expected_rows():
    await create_all_tables()
    counts = await seed_database()

    assert counts == {"policies": 9, "vehicles": 9, "claims_history": 10}

    session_factory = get_session_factory()
    async with session_factory() as session:
        result = await session.execute(
            select(Policy).where(Policy.policy_number == "POL-CA-0002")
        )
        policy = result.scalar_one()
        assert policy.coverage_tier == "full_coverage"
        assert policy.policy_form_id == "CA-FULL-COVERAGE"

        result = await session.execute(
            select(ClaimHistory).where(ClaimHistory.policy_number == "POL-CA-0002")
        )
        claims = result.scalars().all()
        assert len(claims) == 3
        assert any(c.fraud_flag for c in claims)

        result = await session.execute(
            select(ClaimHistory).where(ClaimHistory.policy_number == "POL-CA-0001")
        )
        assert result.scalars().all() == []
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `docker-compose up -d postgres` then `uv run pytest tests/test_seed_data.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.seed_data'`

- [ ] **Step 3: Write the seed data module**

```python
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
        session.add_all(Policy(**row) for row in POLICIES)  # type: ignore[arg-type]
        session.add_all(Vehicle(**row) for row in VEHICLES)  # type: ignore[arg-type]
        session.add_all(ClaimHistory(**row) for row in CLAIMS)  # type: ignore[arg-type]
    return {
        "policies": len(POLICIES),
        "vehicles": len(VEHICLES),
        "claims_history": len(CLAIMS),
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_seed_data.py -v`
Expected: PASS (1 passed)

- [ ] **Step 5: Commit**

```powershell
git add src/claims_assistant/seed_data.py tests/test_seed_data.py
git commit -m "feat: add deterministic seed data for policies, vehicles, claims history"
```

---

### Task 3: Synthetic policy document generator

**Files:**
- Create: `src/claims_assistant/policy_documents.py`
- Create: `scripts/generate_policy_docs.py`
- Test: `tests/test_policy_documents.py`
- Create (generated): `data/policy_documents/*.md` (9 files)

**Interfaces:**
- Consumes: `POLICIES` (Task 2's `seed_data.py`) — only to cross-check that every seeded `policy_form_id` has a matching generated document.
- Produces: `render_policy_document(state: str, tier: str) -> str` and `all_policy_forms() -> dict[str, str]` (form_id → document text) in `policy_documents.py`. Phase 4's Coverage Agent will index the generated `data/policy_documents/*.md` files into Azure AI Search and cite clause IDs like `Sec. 2.1` from them.

Tier definitions used throughout (stated explicitly since the spec's three tier names don't self-define their exact bundles): **liability_only** = Bodily Injury + Property Damage Liability only, no coverage for the policyholder's own vehicle. **full_coverage** = liability at state-minimum limits + Collision + Comprehensive for the policyholder's own vehicle. **comprehensive_collision** = liability at 2x state-minimum limits + Collision + Comprehensive with lower deductibles — a premium tier for higher-value vehicles.

- [ ] **Step 1: Write the failing unit tests**

```python
# tests/test_policy_documents.py
from claims_assistant.policy_documents import all_policy_forms, render_policy_document
from claims_assistant.seed_data import POLICIES


def test_all_policy_forms_returns_nine_documents():
    forms = all_policy_forms()

    assert len(forms) == 9


def test_generated_forms_cover_every_seeded_policy_form_id():
    forms = all_policy_forms()
    seeded_form_ids = {row["policy_form_id"] for row in POLICIES}

    assert seeded_form_ids.issubset(forms.keys())


def test_liability_only_document_excludes_physical_damage_coverage():
    text = render_policy_document("CA", "liability_only")

    assert "does NOT include Collision or Comprehensive coverage" in text
    assert "Sec. 2.1" in text


def test_full_coverage_document_includes_collision_and_comprehensive():
    text = render_policy_document("TX", "full_coverage")

    assert "Collision Coverage" in text
    assert "Comprehensive Coverage" in text
    assert "$500 deductible" in text


def test_state_endorsement_is_included():
    text = render_policy_document("NY", "comprehensive_collision")

    assert "No-Fault Personal Injury Protection" in text
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_policy_documents.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.policy_documents'`

- [ ] **Step 3: Write the document generator**

```python
# src/claims_assistant/policy_documents.py
from __future__ import annotations

STATE_MINIMUMS: dict[str, dict[str, int]] = {
    "CA": {"bi_per_person": 15_000, "bi_per_accident": 30_000, "property_damage": 5_000},
    "TX": {"bi_per_person": 30_000, "bi_per_accident": 60_000, "property_damage": 25_000},
    "NY": {"bi_per_person": 25_000, "bi_per_accident": 50_000, "property_damage": 10_000},
}

STATE_ENDORSEMENTS: dict[str, str] = {
    "CA": (
        "This policy's premium has been calculated and filed in accordance with "
        "California Proposition 103. Rate changes greater than 6.9% require prior "
        "approval from the California Department of Insurance."
    ),
    "TX": (
        "Uninsured/Underinsured Motorist Coverage (UM/UIM) is included at the same "
        "limits as Bodily Injury Liability unless rejected in writing by the "
        "policyholder, per Texas Insurance Code Sec. 1952.101."
    ),
    "NY": (
        "This policy includes No-Fault Personal Injury Protection (PIP) coverage of "
        "$50,000 per person for basic economic loss, regardless of fault, per New "
        "York Insurance Law Article 51."
    ),
}

TIER_TEXT: dict[str, dict[str, object]] = {
    "liability_only": {
        "label": "Liability Only",
        "collision": None,
        "comprehensive": None,
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability coverage only. It does NOT cover damage to the "
            "policyholder's own vehicle from any cause, including collision, "
            "theft, fire, or weather."
        ),
    },
    "full_coverage": {
        "label": "Full Coverage",
        "collision": "$500 deductible",
        "comprehensive": "$500 deductible",
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability at the state-mandated minimum limits, plus Collision and "
            "Comprehensive coverage for the policyholder's own vehicle, each "
            "subject to a $500 deductible."
        ),
    },
    "comprehensive_collision": {
        "label": "Comprehensive/Collision (Premium)",
        "collision": "$250 deductible",
        "comprehensive": "$100 deductible",
        "summary": (
            "This policy provides Bodily Injury Liability and Property Damage "
            "Liability at 2x the state-mandated minimum limits, plus Collision "
            "coverage ($250 deductible) and Comprehensive coverage ($100 "
            "deductible) for the policyholder's own vehicle."
        ),
    },
}


def render_policy_document(state: str, tier: str) -> str:
    form_id = f"{state}-{tier.upper().replace('_', '-')}"
    minimums = STATE_MINIMUMS[state]
    tier_info = TIER_TEXT[tier]
    bi_pp = minimums["bi_per_person"]
    bi_pa = minimums["bi_per_accident"]
    pd = minimums["property_damage"]
    if tier == "comprehensive_collision":
        bi_pp *= 2
        bi_pa *= 2
        pd *= 2

    lines = [
        f"# Auto Insurance Policy — {form_id}",
        "",
        f"**Coverage Tier:** {tier_info['label']}",
        f"**State:** {state}",
        "",
        "## Section 1. Definitions",
        "",
        "Sec. 1.1 \"Policyholder\" means the named insured on the declarations page.",
        "Sec. 1.2 \"Covered Vehicle\" means a vehicle listed on the declarations page.",
        "Sec. 1.3 \"Accident\" means a sudden, unintended event causing bodily "
        "injury or property damage.",
        "",
        "## Section 2. Liability Coverage",
        "",
        f"Sec. 2.1 Bodily Injury Liability: ${bi_pp:,} per person / ${bi_pa:,} per "
        "accident.",
        f"Sec. 2.2 Property Damage Liability: ${pd:,} per accident.",
        "Sec. 2.3 This coverage pays for injury or damage the Policyholder causes "
        "to others. It does not cover the Policyholder's own vehicle.",
        "",
        "## Section 3. Physical Damage Coverage",
        "",
    ]

    if tier_info["collision"] is None:
        lines.append(
            "Sec. 3.1 This policy does NOT include Collision or Comprehensive "
            "coverage."
        )
    else:
        lines.append(
            f"Sec. 3.1 Collision Coverage: pays for damage to the Covered Vehicle "
            f"from a collision, subject to a {tier_info['collision']}."
        )
        lines.append(
            f"Sec. 3.2 Comprehensive Coverage: pays for damage to the Covered "
            f"Vehicle from non-collision causes (theft, fire, weather, "
            f"vandalism), subject to a {tier_info['comprehensive']}."
        )

    lines += [
        "",
        "## Section 4. Exclusions",
        "",
        "Sec. 4.1 This policy does not cover damage or injury that occurs while "
        "the Covered Vehicle is being used to carry persons or property for a "
        "fee (ride-share or delivery use), unless a commercial-use endorsement "
        "has been added.",
        "Sec. 4.2 This policy does not cover intentional damage caused by the "
        "Policyholder.",
        "Sec. 4.3 This policy does not cover damage that occurred before the "
        "Effective Date or after the Expiration Date on the declarations page.",
        "",
        "## Section 5. Claims Filing Procedures",
        "",
        "Sec. 5.1 The Policyholder must report a claim within 30 days of the "
        "Accident.",
        "Sec. 5.2 The Policyholder must cooperate with the claims investigation, "
        "including providing a written statement and access to the Covered "
        "Vehicle for inspection.",
        "",
        "## Section 6. State-Specific Endorsement",
        "",
        f"Sec. 6.1 {STATE_ENDORSEMENTS[state]}",
        "",
        "## Summary",
        "",
        str(tier_info["summary"]),
        "",
    ]
    return "\n".join(lines)


def all_policy_forms() -> dict[str, str]:
    states = ["CA", "TX", "NY"]
    tiers = ["liability_only", "full_coverage", "comprehensive_collision"]
    return {
        f"{state}-{tier.upper().replace('_', '-')}": render_policy_document(state, tier)
        for state in states
        for tier in tiers
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_policy_documents.py -v`
Expected: PASS (5 passed)

- [ ] **Step 5: Write the generation script**

```python
# scripts/generate_policy_docs.py
"""Generate the synthetic policy document corpus into data/policy_documents/."""

from pathlib import Path

from claims_assistant.policy_documents import all_policy_forms

OUTPUT_DIR = Path(__file__).resolve().parents[1] / "data" / "policy_documents"


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    forms = all_policy_forms()
    for form_id, content in forms.items():
        (OUTPUT_DIR / f"{form_id}.md").write_text(content, encoding="utf-8")
    print(f"Wrote {len(forms)} policy documents to {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
```

- [ ] **Step 6: Run the script and verify the output**

Run: `uv run python scripts/generate_policy_docs.py`
Expected: `Wrote 9 policy documents to ...data/policy_documents`. Verify with `ls data/policy_documents` (or `Get-ChildItem data\policy_documents` in PowerShell) — 9 `.md` files, one per state/tier combination (e.g. `CA-LIABILITY-ONLY.md`).

- [ ] **Step 7: Commit**

```powershell
git add src/claims_assistant/policy_documents.py scripts/generate_policy_docs.py tests/test_policy_documents.py data/policy_documents
git commit -m "feat: add synthetic policy document generator and corpus"
```

---

### Task 4: Seed CLI script

**Files:**
- Create: `scripts/seed_db.py`

**Interfaces:**
- Consumes: `create_all_tables()` (Task 1's `database.py`), `seed_database()` (Task 2's `seed_data.py`).
- Produces: a single runnable command that brings a fresh local Postgres to the fully-seeded state.

- [ ] **Step 1: Write the script**

```python
# scripts/seed_db.py
"""One-shot local dev setup: create tables and seed Postgres with synthetic data."""

import asyncio

from claims_assistant.database import create_all_tables
from claims_assistant.seed_data import seed_database


async def main() -> None:
    await create_all_tables()
    counts = await seed_database()
    print(f"Seeded: {counts}")


if __name__ == "__main__":
    asyncio.run(main())
```

- [ ] **Step 2: Run it against the real Postgres**

Run: `docker-compose up -d postgres` then `uv run python scripts/seed_db.py`
Expected: `Seeded: {'policies': 9, 'vehicles': 9, 'claims_history': 10}`

- [ ] **Step 3: Verify by hand against the running container**

Run:
```powershell
docker-compose exec postgres psql -U claims_assistant -c "SELECT policy_number, coverage_tier FROM policies ORDER BY policy_number;"
docker-compose exec postgres psql -U claims_assistant -c "SELECT claim_id, policy_number, fraud_flag FROM claims_history WHERE policy_number = 'POL-CA-0002';"
```
Expected: first query lists all 9 policies; second query lists `CLM-0001`, `CLM-0002`, `CLM-0003` with `CLM-0003`'s `fraud_flag` = `t`.

- [ ] **Step 4: Commit**

```powershell
git add scripts/seed_db.py
git commit -m "feat: add seed_db CLI script for local dev setup"
```

---

### Task 5: FNOL extraction schema

**Files:**
- Create: `src/claims_assistant/fnol_schema.py`
- Test: `tests/test_fnol_schema.py`

**Interfaces:**
- Produces: `Party`, `VehicleInfo`, `FNOLFacts` Pydantic models. `Party.role` is one of `policyholder`, `other_driver`, `passenger`, `witness`, `pedestrian`. `VehicleInfo.role` is one of `policyholder_vehicle`, `other_vehicle`. `FNOLFacts` deliberately excludes a confidence field — per spec §5.4/§6, per-field confidence is an *output* of the Phase 3 Extraction Agent, not a ground-truth attribute a gold fixture can have; Phase 3 will wrap `FNOLFacts` with confidence separately. Task 6's eval fixtures validate their gold JSON against `FNOLFacts`.

- [ ] **Step 1: Write the failing tests**

```python
# tests/test_fnol_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def test_fnol_facts_valid_construction():
    facts = FNOLFacts(
        incident_datetime="2025-09-14T17:30",
        location="Elm Street, Sacramento, CA",
        parties=[
            Party(role="policyholder", name="Maria Gonzalez"),
            Party(role="other_driver", name="Kevin Ortiz", contact="916-555-0142"),
        ],
        vehicles=[
            VehicleInfo(role="policyholder_vehicle", description="Ford Focus"),
            VehicleInfo(role="other_vehicle", description="blue Honda Civic"),
        ],
        injuries=False,
        narrative_summary="Policyholder rear-ended another vehicle stopped at a red light.",
    )

    assert facts.injuries is False
    assert facts.parties[0].role == "policyholder"
    assert facts.vehicles[1].vin is None


def test_fnol_facts_requires_narrative_summary():
    with pytest.raises(ValidationError):
        FNOLFacts(
            incident_datetime="2025-09-14T17:30",
            location="Elm Street, Sacramento, CA",
            parties=[],
            vehicles=[],
            injuries=False,
        )
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_fnol_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.fnol_schema'`

- [ ] **Step 3: Write the schema**

```python
# src/claims_assistant/fnol_schema.py
from __future__ import annotations

from pydantic import BaseModel


class Party(BaseModel):
    """role is one of: policyholder, other_driver, passenger, witness, pedestrian."""

    role: str
    name: str
    contact: str | None = None


class VehicleInfo(BaseModel):
    """role is one of: policyholder_vehicle, other_vehicle."""

    role: str
    vin: str | None = None
    description: str


class FNOLFacts(BaseModel):
    incident_datetime: str
    location: str
    parties: list[Party]
    vehicles: list[VehicleInfo]
    injuries: bool
    injury_description: str | None = None
    narrative_summary: str
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_fnol_schema.py -v`
Expected: PASS (2 passed)

- [ ] **Step 5: Commit**

```powershell
git add src/claims_assistant/fnol_schema.py tests/test_fnol_schema.py
git commit -m "feat: add FNOL extraction Pydantic schema"
```

---

### Task 6: FNOL extraction eval fixtures

**Files:**
- Create: `data/eval_fixtures/extraction/*.txt` and `*.json` (10 fixture pairs)
- Create: `src/claims_assistant/eval_fixtures.py`
- Test: `tests/test_eval_fixtures.py`

**Interfaces:**
- Consumes: `FNOLFacts` (Task 5's `fnol_schema.py`).
- Produces: `ExtractionFixture` dataclass (`fixture_id: str`, `narrative_text: str`, `gold: FNOLFacts`) and `load_extraction_fixtures() -> list[ExtractionFixture]`. Phase 3's extraction eval (and Phase 8's eval harness) will import `load_extraction_fixtures()` directly.

This is a **starter set of 10 fixtures** covering the few-shot edge cases called out in spec §5.4 (multi-vehicle pileup, hit-and-run, ambiguous fault, ambiguous injury) plus clean/injury/theft/pedestrian/exclusion-relevant variants. Spec §6 targets ~40-60 fixtures eventually — that expansion happens incrementally in Phase 8 (Eval Framework), not here; this set is enough for Phase 3 to validate the Extraction Agent against real cases as soon as it exists. Three fixtures deliberately reuse policies from Task 2's seed data (`POL-NY-0009` on fixture 6 matches `CLM-0009` exactly; `POL-CA-0002` appears on fixtures 4 and 10) so Phase 5's Fraud-Risk Agent has FNOL-to-history continuity to reason over later.

- [ ] **Step 1: Write the failing test**

```python
# tests/test_eval_fixtures.py
from claims_assistant.eval_fixtures import load_extraction_fixtures
from claims_assistant.fnol_schema import FNOLFacts


def test_load_extraction_fixtures_returns_all_fixtures():
    fixtures = load_extraction_fixtures()

    assert len(fixtures) == 10
    ids = {f.fixture_id for f in fixtures}
    assert len(ids) == 10
    for fixture in fixtures:
        assert fixture.narrative_text
        assert isinstance(fixture.gold, FNOLFacts)


def test_hit_and_run_fixture_has_no_named_other_driver():
    fixtures = {f.fixture_id: f for f in load_extraction_fixtures()}
    fixture = fixtures["fnol_003_hit_and_run"]

    assert fixture.gold.injuries is False
    assert all(p.role != "other_driver" for p in fixture.gold.parties)


def test_ambiguous_injury_fixture_marks_injuries_true():
    fixtures = {f.fixture_id: f for f in load_extraction_fixtures()}
    fixture = fixtures["fnol_005_ambiguous_injury"]

    assert fixture.gold.injuries is True
    assert fixture.gold.injury_description is not None
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval_fixtures'`

- [ ] **Step 3: Create fixture 1 — simple single vehicle**

Create `data/eval_fixtures/extraction/fnol_001_simple_single_vehicle.txt`:
```
On September 14, 2025 at approximately 5:30 PM, I (Maria Gonzalez) was driving my Ford Focus westbound on Elm Street in Sacramento, CA, when I rear-ended the vehicle ahead of me that had stopped suddenly for a red light. The other driver, Kevin Ortiz, was driving a blue Honda Civic. There was minor damage to both bumpers. No one was hurt. Kevin's contact number is 916-555-0142.
```

Create `data/eval_fixtures/extraction/fnol_001_simple_single_vehicle.json`:
```json
{
  "incident_datetime": "2025-09-14T17:30",
  "location": "Elm Street, Sacramento, CA",
  "parties": [
    {"role": "policyholder", "name": "Maria Gonzalez", "contact": null},
    {"role": "other_driver", "name": "Kevin Ortiz", "contact": "916-555-0142"}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Ford Focus"},
    {"role": "other_vehicle", "vin": null, "description": "blue Honda Civic"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Policyholder rear-ended another vehicle stopped at a red light on Elm Street, Sacramento, CA; minor bumper damage to both vehicles, no injuries."
}
```

- [ ] **Step 4: Create fixture 2 — multi-vehicle pileup**

Create `data/eval_fixtures/extraction/fnol_002_multi_vehicle_pileup.txt`:
```
On July 28, 2025 around 8:10 AM, heavy fog caused a chain-reaction crash on I-45 northbound near Conroe, TX. My Ford F-150 (Derek Owusu) was the third of four vehicles involved. The car ahead of me, a gray Nissan Altima driven by Wendy Sato, stopped short and I struck it; the SUV behind me, driven by Marcus Webb, then struck my truck. A fourth vehicle, a delivery van, also struck Marcus's SUV. Everyone exchanged information. No injuries were reported at the scene.
```

Create `data/eval_fixtures/extraction/fnol_002_multi_vehicle_pileup.json`:
```json
{
  "incident_datetime": "2025-07-28T08:10",
  "location": "I-45 northbound near Conroe, TX",
  "parties": [
    {"role": "policyholder", "name": "Derek Owusu", "contact": null},
    {"role": "other_driver", "name": "Wendy Sato", "contact": null},
    {"role": "other_driver", "name": "Marcus Webb", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Ford F-150"},
    {"role": "other_vehicle", "vin": null, "description": "gray Nissan Altima"},
    {"role": "other_vehicle", "vin": null, "description": "Marcus Webb's SUV"},
    {"role": "other_vehicle", "vin": null, "description": "delivery van"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Four-vehicle chain-reaction crash on I-45 northbound near Conroe, TX during heavy fog; policyholder's truck struck a stopped Nissan Altima ahead and was struck from behind by an SUV, which was itself struck by a delivery van. No injuries reported."
}
```

- [ ] **Step 5: Create fixture 3 — hit and run**

Create `data/eval_fixtures/extraction/fnol_003_hit_and_run.txt`:
```
On February 3, 2026 at about 9:00 PM, I (Linda Park) returned to my parked Toyota Corolla outside my apartment on 4th Ave in Brooklyn, NY, to find the rear bumper and taillight smashed in. A neighbor said they heard a crash and saw a dark-colored sedan speed off but didn't get a plate number. No other driver information is available. I was not in the vehicle at the time and was not hurt.
```

Create `data/eval_fixtures/extraction/fnol_003_hit_and_run.json`:
```json
{
  "incident_datetime": "2026-02-03T21:00",
  "location": "4th Ave, Brooklyn, NY",
  "parties": [
    {"role": "policyholder", "name": "Linda Park", "contact": null},
    {"role": "witness", "name": "unnamed neighbor", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Toyota Corolla"},
    {"role": "other_vehicle", "vin": null, "description": "dark-colored sedan, fled scene, no plate captured"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Hit-and-run: policyholder's parked Toyota Corolla was struck by an unidentified dark sedan that fled the scene; a neighbor witnessed the vehicle leaving but did not get identifying information. No injuries, policyholder was not present."
}
```

- [ ] **Step 6: Create fixture 4 — ambiguous fault**

Create `data/eval_fixtures/extraction/fnol_004_ambiguous_fault.txt`:
```
On April 2, 2025 at roughly 3:45 PM, my Tesla Model 3 (James Whitfield) collided with a silver Toyota Camry driven by Aaron Feldman at the intersection of 9th and Mission in San Francisco, CA. I believe the light was green for me, but Aaron says the same thing from his direction. There were no working traffic cameras at the intersection. Both cars have front-end damage. No injuries reported.
```

Create `data/eval_fixtures/extraction/fnol_004_ambiguous_fault.json`:
```json
{
  "incident_datetime": "2025-04-02T15:45",
  "location": "9th and Mission, San Francisco, CA",
  "parties": [
    {"role": "policyholder", "name": "James Whitfield", "contact": null},
    {"role": "other_driver", "name": "Aaron Feldman", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Tesla Model 3"},
    {"role": "other_vehicle", "vin": null, "description": "silver Toyota Camry"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Intersection collision at 9th and Mission, San Francisco, CA; both drivers claim they had the green light and no traffic camera footage exists to confirm fault. Front-end damage to both vehicles, no injuries."
}
```

- [ ] **Step 7: Create fixture 5 — ambiguous injury**

Create `data/eval_fixtures/extraction/fnol_005_ambiguous_injury.txt`:
```
On June 20, 2025 around 12:15 PM, I (Angela Brooks) was stopped at a light on Westheimer Rd in Houston, TX when a black pickup driven by Todd Reyes rear-ended my Honda Accord at low speed. There's a small dent in my bumper. My neck felt a little stiff afterward but I didn't think much of it and didn't go to a doctor. It's probably nothing. Todd's number is 713-555-0198.
```

Create `data/eval_fixtures/extraction/fnol_005_ambiguous_injury.json`:
```json
{
  "incident_datetime": "2025-06-20T12:15",
  "location": "Westheimer Rd, Houston, TX",
  "parties": [
    {"role": "policyholder", "name": "Angela Brooks", "contact": null},
    {"role": "other_driver", "name": "Todd Reyes", "contact": "713-555-0198"}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Honda Accord"},
    {"role": "other_vehicle", "vin": null, "description": "black pickup truck"}
  ],
  "injuries": true,
  "injury_description": "Policyholder reported neck stiffness following the collision but did not seek medical attention and described it as minor/uncertain.",
  "narrative_summary": "Low-speed rear-end collision on Westheimer Rd, Houston, TX; minor bumper dent and policyholder reports mild, unconfirmed neck stiffness with no medical treatment sought."
}
```

- [ ] **Step 8: Create fixture 6 — clear injury**

Create `data/eval_fixtures/extraction/fnol_006_clear_injury.txt`:
```
On August 15, 2025 at 6:50 PM, my Hyundai Sonata (Samantha Cruz) was broadsided by a delivery truck that ran a stop sign at the corner of Flatbush Ave and Church Ave in Brooklyn, NY. My passenger, my brother Tomas Cruz, hit his head on the window and was bleeding. An ambulance took him to Kings County Hospital. The delivery truck driver, identified as Greg Halloran, admitted fault at the scene.
```

Create `data/eval_fixtures/extraction/fnol_006_clear_injury.json`:
```json
{
  "incident_datetime": "2025-08-15T18:50",
  "location": "Flatbush Ave and Church Ave, Brooklyn, NY",
  "parties": [
    {"role": "policyholder", "name": "Samantha Cruz", "contact": null},
    {"role": "passenger", "name": "Tomas Cruz", "contact": null},
    {"role": "other_driver", "name": "Greg Halloran", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Hyundai Sonata"},
    {"role": "other_vehicle", "vin": null, "description": "delivery truck"}
  ],
  "injuries": true,
  "injury_description": "Passenger Tomas Cruz struck his head on the window and was bleeding; transported by ambulance to Kings County Hospital.",
  "narrative_summary": "Delivery truck ran a stop sign and broadsided policyholder's vehicle at Flatbush Ave and Church Ave, Brooklyn, NY; passenger sustained a head injury and was taken to the hospital by ambulance. Other driver admitted fault at the scene."
}
```

- [ ] **Step 9: Create fixture 7 — parking lot, minor, single vehicle**

Create `data/eval_fixtures/extraction/fnol_007_parking_lot_minor.txt`:
```
On November 5, 2025 at about 1:20 PM, I (Priya Natarajan) was backing my Jeep Grand Cherokee out of a parking space at the Stonestown Galleria in San Francisco, CA and clipped a concrete parking barrier, denting the rear bumper. No other vehicles or people were involved. No injuries.
```

Create `data/eval_fixtures/extraction/fnol_007_parking_lot_minor.json`:
```json
{
  "incident_datetime": "2025-11-05T13:20",
  "location": "Stonestown Galleria parking lot, San Francisco, CA",
  "parties": [
    {"role": "policyholder", "name": "Priya Natarajan", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Jeep Grand Cherokee"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Single-vehicle incident: policyholder backed into a concrete parking barrier at a shopping center parking lot in San Francisco, CA, denting the rear bumper. No other parties involved, no injuries."
}
```

- [ ] **Step 10: Create fixture 8 — theft report**

Create `data/eval_fixtures/extraction/fnol_008_theft_report.txt`:
```
Sometime overnight between May 1 and May 2, 2025, my BMW 3 Series (Michael Ferraro) was stolen from my driveway on Willow St in Buffalo, NY. I parked it at around 10:00 PM on May 1 and discovered it missing at 7:00 AM on May 2. I have already filed a police report, case number BPD-2025-04471. No other vehicles or parties were involved.
```

Create `data/eval_fixtures/extraction/fnol_008_theft_report.json`:
```json
{
  "incident_datetime": "2025-05-02T07:00",
  "location": "Willow St, Buffalo, NY",
  "parties": [
    {"role": "policyholder", "name": "Michael Ferraro", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "BMW 3 Series"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Policyholder's BMW 3 Series was stolen overnight from the driveway of their Buffalo, NY residence between 10:00 PM and 7:00 AM; police report BPD-2025-04471 already filed. No other parties involved."
}
```

- [ ] **Step 11: Create fixture 9 — pedestrian, multiple witnesses**

Create `data/eval_fixtures/extraction/fnol_009_multiple_witnesses.txt`:
```
On March 22, 2025 at 4:40 PM, my Chevrolet Equinox (Robert Kessler) struck a pedestrian, Maria Delgado, who stepped into the crosswalk on Main St in Austin, TX against the signal. She was struck at low speed and was able to stand afterward but complained of pain in her left ankle; paramedics checked her at the scene. Two witnesses, Dana Fields and Omar Haddad, saw the pedestrian enter the crosswalk against the light and gave statements to police.
```

Create `data/eval_fixtures/extraction/fnol_009_multiple_witnesses.json`:
```json
{
  "incident_datetime": "2025-03-22T16:40",
  "location": "Main St, Austin, TX",
  "parties": [
    {"role": "policyholder", "name": "Robert Kessler", "contact": null},
    {"role": "pedestrian", "name": "Maria Delgado", "contact": null},
    {"role": "witness", "name": "Dana Fields", "contact": null},
    {"role": "witness", "name": "Omar Haddad", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Chevrolet Equinox"}
  ],
  "injuries": true,
  "injury_description": "Pedestrian complained of left ankle pain after being struck at low speed; evaluated by paramedics at the scene.",
  "narrative_summary": "Policyholder's vehicle struck a pedestrian who entered a crosswalk against the signal on Main St, Austin, TX; pedestrian reported ankle pain and was checked by paramedics. Two independent witnesses corroborated the pedestrian entered against the signal."
}
```

- [ ] **Step 12: Create fixture 10 — rideshare exclusion relevance**

Create `data/eval_fixtures/extraction/fnol_010_rideshare_exclusion.txt`:
```
On October 9, 2025 at 10:15 PM, I (James Whitfield) was driving my Tesla Model 3 while logged into the Lyft app waiting for a ride request when another car, driven by Carla Nguyen, rear-ended me at a stoplight on Van Ness Ave in San Francisco, CA. I did not have a passenger in the car yet. My rear bumper and trunk are damaged. No injuries reported.
```

Create `data/eval_fixtures/extraction/fnol_010_rideshare_exclusion.json`:
```json
{
  "incident_datetime": "2025-10-09T22:15",
  "location": "Van Ness Ave, San Francisco, CA",
  "parties": [
    {"role": "policyholder", "name": "James Whitfield", "contact": null},
    {"role": "other_driver", "name": "Carla Nguyen", "contact": null}
  ],
  "vehicles": [
    {"role": "policyholder_vehicle", "vin": null, "description": "Tesla Model 3"},
    {"role": "other_vehicle", "vin": null, "description": "Carla Nguyen's vehicle"}
  ],
  "injuries": false,
  "injury_description": null,
  "narrative_summary": "Policyholder's vehicle was rear-ended at a stoplight on Van Ness Ave, San Francisco, CA while logged into a rideshare app awaiting a ride request, no passenger yet aboard; rear bumper and trunk damaged, no injuries."
}
```

- [ ] **Step 13: Write the fixture loader**

```python
# src/claims_assistant/eval_fixtures.py
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from claims_assistant.fnol_schema import FNOLFacts

FIXTURES_DIR = Path(__file__).resolve().parents[2] / "data" / "eval_fixtures" / "extraction"


@dataclass(frozen=True)
class ExtractionFixture:
    fixture_id: str
    narrative_text: str
    gold: FNOLFacts


def load_extraction_fixtures() -> list[ExtractionFixture]:
    fixtures = []
    for txt_path in sorted(FIXTURES_DIR.glob("*.txt")):
        fixture_id = txt_path.stem
        json_path = txt_path.with_suffix(".json")
        narrative_text = txt_path.read_text(encoding="utf-8").strip()
        gold_data = json.loads(json_path.read_text(encoding="utf-8"))
        gold = FNOLFacts.model_validate(gold_data)
        fixtures.append(ExtractionFixture(fixture_id, narrative_text, gold))
    return fixtures
```

- [ ] **Step 14: Run the tests to verify they pass**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: PASS (3 passed)

- [ ] **Step 15: Commit**

```powershell
git add data/eval_fixtures src/claims_assistant/eval_fixtures.py tests/test_eval_fixtures.py
git commit -m "feat: add FNOL extraction eval fixtures with gold JSON"
```

---

## Definition of Done for Phase 1

- [x] `uv run pytest -v -m "not integration"` passes with no Postgres running (schema/data tests skipped, doc + fixture + schema tests pass).
- [x] `docker-compose up -d postgres` then `uv run pytest -v -m integration` passes.
- [x] `uv run python scripts/seed_db.py` run against a fresh Postgres reports `{'policies': 9, 'vehicles': 9, 'claims_history': 10}`.
- [x] `data/policy_documents/` contains 9 `.md` files; `data/eval_fixtures/extraction/` contains 10 `.txt`/`.json` pairs, both committed.
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [x] Roadmap doc's Phase 1 checkbox is checked off.
- [x] Everything above is committed.

**Note (implementation deviation from plan):** async integration tests required an additional fix not anticipated in the original plan — pytest-asyncio's default per-test event loop scope doesn't match `database.py`'s module-level cached `AsyncEngine`, causing a `RuntimeError: Event loop is closed` when a second async test reused the cached engine under a new loop. Fixed by setting `asyncio_default_test_loop_scope = "session"` in `pyproject.toml`'s `[tool.pytest.ini_options]`, so all async tests in a run share one event loop — matching how the real app runs under a single long-lived Uvicorn event loop. See commit "fix: use session-scoped event loop for async tests, clean up mypy/ruff findings".

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 2 (MCP servers) plan next — it will wrap these same three Postgres tables as `policy-db-mcp`, `claims-history-mcp`, and `vin-vehicle-mcp` tool servers.
