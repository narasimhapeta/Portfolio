# src/claims_assistant/eval/extraction_eval.py
from __future__ import annotations

from agent_framework import Agent

from claims_assistant.agents.extraction_agent import extract_fnol_facts
from claims_assistant.agents.extraction_scoring import score_extraction
from claims_assistant.eval.results import EvalResult, compute_composite_score
from claims_assistant.eval_fixtures import ExtractionFixture


async def run_extraction_eval(
    agent: Agent, fixtures: list[ExtractionFixture]
) -> list[EvalResult]:
    results = []
    for fixture in fixtures:
        extraction = await extract_fnol_facts(agent, fixture.narrative_text)
        correctness = score_extraction(extraction.facts, fixture.gold)
        results.append(
            EvalResult(
                agent="extraction",
                fixture_id=fixture.fixture_id,
                correctness_score=correctness,
                grounding_score=None,
                composite_score=compute_composite_score(correctness, None),
                primary_judge_grounded=None,
                secondary_judge_grounded=None,
                judge_disagreement=False,
            )
        )
    return results
