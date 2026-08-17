# src/claims_assistant/eval/coverage_eval.py
from __future__ import annotations

from agent_framework import Agent

from claims_assistant.agents.coverage_agent import determine_coverage, lookup_policy_by_number
from claims_assistant.config import Settings
from claims_assistant.eval.judge import judge_grounding
from claims_assistant.eval.results import EvalResult, compute_composite_score
from claims_assistant.eval_fixtures import CoverageFixture
from claims_assistant.search.retrieval import retrieve_policy_chunks


async def run_coverage_eval(
    coverage_agent: Agent,
    judge_primary: Agent,
    judge_secondary: Agent,
    settings: Settings,
    fixtures: list[CoverageFixture],
) -> list[EvalResult]:
    results = []
    for fixture in fixtures:
        determination = await determine_coverage(
            coverage_agent, settings, fixture.policy_number, fixture.claim_narrative
        )
        determination_correct = float(determination.determination == fixture.gold_determination)
        citation_correct = float(fixture.gold_citation in determination.citations)
        correctness = (determination_correct + citation_correct) / 2

        policy = await lookup_policy_by_number(settings, fixture.policy_number)

        chunks = await retrieve_policy_chunks(
            settings, form_id=policy.policy_form_id, query_text=fixture.claim_narrative
        )
        cited = [c for c in chunks if c.chunk_id in determination.citations]
        clauses_text = "\n\n".join(f"[{c.chunk_id}] {c.content}" for c in cited)
        evidence_text = f"Claim narrative:\n{
            fixture.claim_narrative}\n\nRetrieved policy clauses:\n{clauses_text}"


        primary = await judge_grounding(judge_primary, determination.rationale, evidence_text)
        secondary = await judge_grounding(
            judge_secondary, determination.rationale, evidence_text
        )
        # Both judges must agree the rationale is grounded -- see Design Decisions:
        # gpt-5.5 (the primary judge) is a different model from Coverage's own gpt-5.4,
        # but still same-family, so requiring secondary (gpt-4.1, a distinct generation)
        # agreement too is what actually makes the anti-self-preference-bias check load-
        # bearing rather than informational.
        grounding = 1.0 if (primary.grounded and secondary.grounded) else 0.0

        results.append(
            EvalResult(
                agent="coverage",
                fixture_id=fixture.fixture_id,
                correctness_score=correctness,
                grounding_score=grounding,
                composite_score=compute_composite_score(correctness, grounding),
                primary_judge_grounded=primary.grounded,
                secondary_judge_grounded=secondary.grounded,
                judge_disagreement=primary.grounded != secondary.grounded,
            )
        )
    return results
