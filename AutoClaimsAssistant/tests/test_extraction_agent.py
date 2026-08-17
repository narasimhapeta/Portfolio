# tests/test_extraction_agent.py
import pytest

from claims_assistant.agents.extraction_agent import build_extraction_agent, extract_fnol_facts
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration

SAMPLE_NARRATIVE = (
    "On July 9, 2026 at around 5:15 PM, I (Harold Bennett) was driving my Chevrolet "
    "Equinox on Elm Street in Columbus, OH when I stopped short for a pedestrian and "
    "was rear-ended by a delivery van driven by Wanda Price. There's noticeable damage "
    "to my rear bumper. Neither of us was hurt. Wanda's phone number is 614-555-0142."
)


@pytest.mark.asyncio
async def test_extract_fnol_facts_produces_schema_valid_json():
    agent = build_extraction_agent(get_settings())

    extraction = await extract_fnol_facts(agent, SAMPLE_NARRATIVE)

    assert extraction.facts.location.lower().count("columbus") == 1
    assert extraction.facts.injuries is False
    assert any(p.role == "policyholder" and "Bennett" in p.name for p in extraction.facts.parties)
    assert any(p.contact and "614-555-0142" in p.contact for p in extraction.facts.parties)
    assert 0.0 <= extraction.confidence.location <= 1.0
    assert extraction.facts.narrative_summary
