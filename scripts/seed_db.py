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
