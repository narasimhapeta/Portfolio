# tests/test_coverage_agent.py
import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent, determine_coverage
from claims_assistant.config import get_settings

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_collision_claim_on_full_coverage_policy_is_approved(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0002",
        claim_narrative=(
            "I rear-ended another car while driving to work in my Tesla Model 3; my front "
            "bumper is damaged."
        ),
    )

    assert result.determination == "approve"
    assert len(result.citations) > 0
    assert all(c.startswith("CA-FULL-COVERAGE_") for c in result.citations)


@pytest.mark.asyncio
async def test_collision_claim_on_liability_only_policy_is_denied(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0001",
        claim_narrative=(
            "I rear-ended another car while driving to work in my Ford Focus; my front "
            "bumper is damaged."
        ),
    )

    assert result.determination == "deny"
    assert "CA-LIABILITY-ONLY_section-3-physical-damage-coverage" in result.citations


@pytest.mark.asyncio
async def test_delivery_use_collision_with_unstated_endorsement_needs_info(seeded_db):
    settings = get_settings()
    agent = build_coverage_agent(settings)

    result = await determine_coverage(
        agent,
        settings,
        policy_number="POL-CA-0002",
        claim_narrative=(
            "I had just dropped off a food delivery order for a local restaurant's delivery "
            "app when another driver rear-ended me at a stoplight, denting my rear bumper. "
            "This was the first time I've ever done a delivery run in this car."
        ),
    )

    # First-cut assertion, same spirit as Phase 3's eval-fixture floor: this exercises a
    # genuinely conditional clause (Section 4.1's delivery-use exclusion "unless a
    # commercial-use endorsement has been added") where the narrative never confirms or
    # denies the endorsement. If the model instead returns "deny", that's real prompt-tuning
    # signal, not necessarily a bug in this test.
    assert result.determination == "needs_info"
    assert len(result.citations) > 0
