# tests/test_eval_judge.py
from __future__ import annotations

import pytest

from claims_assistant.config import get_settings
from claims_assistant.eval.judge import build_judge_agent, judge_grounding

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_primary_judge_marks_directly_supported_claim_as_grounded():
    settings = get_settings()
    agent = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)

    judgment = await judge_grounding(
        agent,
        claim_text="The policy covers collision damage subject to a $500 deductible.",
        evidence_text=(
            "Sec. 3.1 Collision Coverage: pays for damage to the Covered Vehicle from a "
            "collision, subject to a $500 deductible."
        ),
    )

    assert judgment.grounded is True


@pytest.mark.asyncio
async def test_primary_judge_marks_fabricated_claim_as_not_grounded():
    settings = get_settings()
    agent = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)

    judgment = await judge_grounding(
        agent,
        claim_text="The policy covers rental car reimbursement up to $75 per day.",
        evidence_text=(
            "Sec. 3.1 Collision Coverage: pays for damage to the Covered Vehicle from a "
            "collision, subject to a $500 deductible."
        ),
    )

    assert judgment.grounded is False


@pytest.mark.asyncio
async def test_secondary_judge_marks_directly_supported_claim_as_grounded():
    settings = get_settings()
    agent = build_judge_agent(settings, settings.azure_openai_eval_judge_secondary_deployment)

    judgment = await judge_grounding(
        agent,
        claim_text="Days since policy effective: 12, below the 30-day recent-inception window.",
        evidence_text="Days since policy effective: 12\nPrior claim count: 0",
    )

    assert judgment.grounded is True
