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
