# Phase 8: Eval Framework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Build the pytest-based eval harness spec §6 describes: deterministic extraction scoring (already exists, Phase 1/3 — this phase wires it into a unified report), LLM-as-judge grounding checks for Coverage and Fraud-Risk, a Pandas aggregation report, and checked-in baseline thresholds that gate a `pytest.mark.integration` suite. Running it locally produces a scored report; deliberately weakening a prompt drops a score below baseline and the suite fails (roadmap Phase 8 success criteria).

**Architecture:** A new `src/claims_assistant/eval/` package holds everything this phase adds: a generic LLM-as-judge module (`judge_schema.py`/`judge.py`) reused by both Coverage and Fraud (the underlying task is identical in both cases — "is this claim text grounded in this evidence text" — so one judge module serves both, called with different evidence), three per-agent runner modules (`extraction_eval.py`, `coverage_eval.py`, `fraud_eval.py`) that each produce a list of a shared `EvalResult` row type (`results.py`), a Pandas reporting module (`report.py`), and checked-in baseline constants (`baselines.py`). The already-existing `src/claims_assistant/eval_fixtures.py` (Phase 1) stays where it is — not moved into the new package — and is *extended* with two new fixture types (`CoverageFixture`, `FraudFixture`) and their loaders, alongside the existing `ExtractionFixture`/`load_extraction_fixtures()`; this keeps "eval fixtures" as one cohesive, already-established concept in one file rather than unilaterally restructuring Phase 1's layout. `tests/test_eval_suite.py` is the harness itself: it loads all three fixture sets, runs all three agents (+ the judge) for real, builds the report, and asserts each agent's mean composite score is at or above its checked-in baseline.

**Design decisions resolved during planning (not guessed at):**
- **What already exists vs. what this phase adds:** Phase 1 built `eval_fixtures.py` + `load_extraction_fixtures()` + the 10 extraction fixtures under `data/eval_fixtures/extraction/`, and Phase 3 added `agents/extraction_scoring.py::score_extraction()` plus a first-cut floor test (`tests/test_extraction_eval_fixtures.py`, floor 0.7). **Confirmed by reading both files and the `data/eval_fixtures/` directory tree directly**: no coverage-determination or fraud-risk eval fixture sets exist anywhere in the repo — spec §6 describes them, but Phase 1's actual deliverable (per the roadmap row) was extraction-only. This phase creates both from scratch (Tasks 3–4), reusing the exact `policy_number`/`vin`/narrative scenarios already hand-verified correct in `tests/test_coverage_agent.py` and `tests/test_fraud_agent.py` where possible, extended with two more of each for state/tier diversity — all gold labels below are checked directly against the real policy document text and the real seeded Postgres data (Sections referenced, day-math, and claim history all reproduced from the actual files, not assumed).
- **Judge model selection:** live-checked via `az cognitiveservices account list-models`/`deployment list` while writing this plan (Global Constraints below) — nothing has moved since Phase 5's check (same newest-but-ambiguous `gpt-5.6-sol/luna/terra` trio). Coverage runs on `gpt-5.4`, Fraud-Risk runs on `gpt-5.5` — both already "GPT-5 (full)" per spec §4. Per spec §4's explicit anti-self-preference-bias clause ("a second distinct judge model spot-checks any output produced by GPT-5 itself"), a judge that is *also* GPT-5-family doesn't fully escape that risk merely by being a different point version — the risk is family-level (stylistic self-favoring), not just literal-same-model. So: **primary judge = `gpt-5.5`** (newest unambiguous full-tier GA model, satisfies spec §4's "GPT-5 (full)" row), **secondary judge = `gpt-4.1`** (a genuinely different model generation, not a GPT-5 point release — the first non-GPT-5-family deployment this project has ever used). Given this project's fixture sets are small (5 cases each for Coverage/Fraud, sized to this capstone's demonstrated scale — see below), the secondary judge scores **every** Coverage/Fraud case rather than a random subset: at n≈5 a "spot check" and a full check are practically the same thing, and skipping random sampling avoids adding a dependency on Python's `random` module for no real benefit.
  **The `eval-judge-primary` deployment (`gpt-5.5`) is not just same-family but the literal same model already deployed as `fraud-risk-agent` — a direct same-model self-judgment on the one agent spec §4 calls out as highest-stakes for false positives/negatives, caught during review of this plan before execution began.** Rather than special-case which judge is "primary" per agent (asymmetric, harder to reason about), the grounding dimension of the composite score is gated on **both judges agreeing**: `grounding_score = 1.0` only if `primary.grounded and secondary.grounded`, `0.0` otherwise. This makes the distinct-model check load-bearing (an ungrounded rationale must fool both models to pass) rather than merely informational, directly for the case where the primary judge and the agent under test are the same deployment. `judge_disagreement` (`primary.grounded != secondary.grounded`) is still recorded on every row as a diagnostic signal, and both judges' individual verdicts are included as their own columns in the printed report (Task 9) so a human can see *what* each judge concluded, not just that they disagreed.
- **pandas dependency:** not in `pyproject.toml` before this phase. **Verified empirically in a scratch venv while writing this plan** (not the project's own venv — nothing was installed into this project without the user running the command): `pandas==3.0.5` + bare `mypy --strict` on a `DataFrame(list[dict])` / `.groupby(col)[col].mean().reset_index(name=...)` / `.to_string()` snippet fails with `Library stubs not installed for "pandas"  [import-untyped]` plus a `Returning Any from function declared to return "str"` on the `to_string()` call. Installing `pandas-stubs` alongside it and re-running the identical snippet: **`Success: no issues found in 1 source file`**. So Task 1 adds both `pandas` (runtime) and `pandas-stubs` (dev group, alongside the existing `mypy`/`ruff`/`pytest` dev deps) — no `[[tool.mypy.overrides]]` ignore-block needed, unlike `asyncpg.*`.
- **Where baselines come from:** there is no prior eval run to compare against yet. Task 8 checks in a conservative starting floor (`0.70` per agent, matching the precedent Phase 3 already established for extraction's own first-cut floor) so the harness and its regression-detection *logic* can be built and tested now, then its final step has the user run the suite for real against the actual shipped agents, read the printed per-agent mean scores, and tighten each baseline to just below its observed real mean (leaving headroom for ordinary LLM response variance while still catching a real regression) — this is "run once against current known-good agents, checked-in scores become the baseline," made concrete rather than left as a TODO.
- **File structure:** a new `src/claims_assistant/eval/` package (judge + per-agent runners + report + baselines — all net-new infrastructure this phase adds), *not* `scripts/` (this needs to be imported by tests, not run standalone) and *not* flattened into `agents/` (the judge isn't part of the production claims-intake graph — keeping it out of `agents/` avoids implying it is). `eval_fixtures.py` stays put per above.
- **Coverage/fraud fixture set sizes (5 each):** spec §6 gives extraction an explicit ~40–60 target but no number for Coverage/Fraud. Phase 1 actually shipped 10 extraction fixtures (below that range) — this project runs at capstone/demo scale throughout (9 policy documents, 10 extraction fixtures), so 5 Coverage + 5 Fraud fixtures (each spanning all of their gold-label space: approve/deny/needs_info for Coverage, low/medium/high for Fraud) is sized consistently with that established scale rather than spec's aspirational extraction number.

**Tech Stack:** Adds `pandas` (runtime) and `pandas-stubs` (dev) — verified above. Everything else reuses already-installed, already-verified surface: `agent_framework`/`agent_framework.openai` (`Agent`, `ChatOptions`, `OpenAIChatCompletionClient`), `pydantic` v2, `pytest`/`pytest-asyncio` (`integration` marker).

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§6 Evaluation Framework — this phase's entire scope; §4 model tiering — judge model selection; §5.4 FNOL schema — extraction gold shape, unchanged from Phase 1)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0). Every I/O-bound function is `async def`.
- No new Postgres tables/migrations this phase — no `Base.metadata` changes.
- **Live Azure OpenAI model catalog re-checked while writing this plan** (`az cognitiveservices account list-models --name claims-assistant-openai --resource-group claims-assistant-rg`): unchanged since Phase 5's check — newest full-tier GA entries are still `gpt-5.5` (`2026-04-24`, unambiguous naming) and the three same-day, ambiguously-named `gpt-5.6-sol`/`gpt-5.6-luna`/`gpt-5.6-terra` (`2026-07-09`) that Phase 5 already passed over. Existing deployments confirmed via `az cognitiveservices account deployment list`: `extraction-agent` (`gpt-5.4-mini`), `coverage-agent` (`gpt-5.4`), `policy-embeddings` (`text-embedding-3-small`), `fraud-risk-agent` (`gpt-5.5`), `adjuster-summary-agent` (`gpt-5.4-mini`). Task 2 adds two more on the same resource: `eval-judge-primary` (`gpt-5.5`) and `eval-judge-secondary` (`gpt-4.1`, version `2025-04-14`, `GenerallyAvailable` per the same catalog check — the first non-GPT-5-family model this project deploys; see Design Decisions above for why).
- **`pandas`/`pandas-stubs` compatibility with this project's `mypy --strict` verified empirically in a throwaway venv, not this project's own** (see Design Decisions above for the exact probe and result): `pandas==3.0.5` + `pandas-stubs` passes `mypy --strict` cleanly on `DataFrame(list[dict[str, object]])`, `.groupby(col)[col].mean().reset_index(name=...)`, and `.to_string(index=False)` — the exact calls this phase's `eval/report.py` uses.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Every test that calls a real agent (Extraction/Coverage/Fraud/Judge) or touches real Postgres is `pytest.mark.integration`, matching every prior phase's convention — this phase has no pure-unit tests beyond Task 1's pandas API-surface probe (`tests/test_pandas_setup.py`, same style as the existing `tests/test_agent_framework_setup.py`).
- **Coverage/fraud fixture gold labels were verified against the real policy-document text and the real seeded Postgres data while writing this plan, not assumed**: `CA-FULL-COVERAGE.md`, `CA-LIABILITY-ONLY.md` (referenced via the existing `tests/test_coverage_agent.py` assertions), `TX-COMPREHENSIVE-COLLISION.md`, and `NY-LIABILITY-ONLY.md` were read directly; the chunk-id convention (`{form_id}_{slugified section title}`, confirmed in `src/claims_assistant/search/chunking.py::_slugify_section_title`) was used to derive each fixture's `gold_citation` deterministically from the real section title that supports the gold determination. (What this format-level derivation does *not* guarantee is that `retrieve_policy_chunks`'s live hybrid search will actually surface that section in its top-`k` results for a given narrative — that's an empirical retrieval-quality question, not something derivable from the slug function alone; Task 7's own integration test surfaces any mismatch immediately if it occurs.) Fraud fixture day-math (`days_since_policy_effective`, `days_since_most_recent_prior_claim`, claim-amount-to-market-value ratios) was computed by hand against the real `POLICIES`/`VEHICLES`/`CLAIMS` rows in `src/claims_assistant/seed_data.py`, and `run_fraud_eval` (Task 8) additionally asserts each fixture's checked-in `gold_red_flags` matches `determine_actual_red_flags()`'s real deterministic output at test time — so a hand-authoring mistake in a fixture fails loudly instead of silently mis-grading the agent. **This exact safety net caught a real mistake during review of this plan**: `fraud_004`'s `gold_red_flags` (Task 4) originally omitted `prior_claim_near_vehicle_value` — `POL-TX-0006`'s prior claim `CLM-0007` (`$19,750.00`) and its vehicle's market value (`$19,750.00`, VIN `1FTFW1ET5EF123461`) are identical in `seed_data.py`, a coincidence easy to miss by eye but caught by independently recomputing the ratio (`19750/19750 = 1.0 ≥ 0.9`) — fixed below before this plan was finalized.
- The Coverage/Fraud judge's job is **grounding only** ("does this rationale text hold up against this evidence text"), not correctness — correctness is scored deterministically in Python, no LLM needed, since every correctness dimension is an already-structured field with a known-correct answer: Coverage's correctness is the mean of `determination == gold_determination` and `gold_citation in determination.citations` (spec §6's "known-correct outcome **and required citation**" — both halves of that sentence are checked, not just the outcome); Fraud's correctness is the mean of `risk_tier == gold_risk_tier` and `set(red_flags) == set(gold_red_flags)`. This split matches spec §6's own two-part description of Coverage/Fraud scoring (a correctness dimension + a separate grounding dimension) and keeps the judge's task narrow enough to actually verify reliably.
- `agents/coverage_agent.py::determine_coverage()` and `agents/fraud_agent.py::assess_fraud_risk()` are used exactly as Phase 4/5 shipped them — unmodified. Where the eval runners need intermediate data those functions don't return (the actually-cited chunks' content for Coverage; the computed `FraudSignals` for Fraud), the runners re-derive it via already-public helper functions (`lookup_policy_by_number`, `retrieve_policy_chunks`, `lookup_claims_history`, `lookup_vehicle_by_vin`, `compute_fraud_signals`) rather than changing either agent's already-shipped, already-tested public contract.

---

### Task 1: `pandas`/`pandas-stubs` dependency + API-surface verification

**Files:**
- Modify: `pyproject.toml`
- Test: `tests/test_pandas_setup.py`

**Interfaces:**
- Consumes: nothing new (first task of the phase).
- Produces: `pandas` importable at runtime; confirms the exact `DataFrame`/`groupby`/`to_string` calls Task 9's `eval/report.py` uses are `mypy --strict`-clean with `pandas-stubs` installed.

- [ ] **Step 1: Add the dependencies**

```powershell
uv add pandas
uv add --group dev pandas-stubs
```

Expected: `pyproject.toml`'s `[project.dependencies]` gains `pandas>=3.0.5` (or whatever the resolved version is) and `[dependency-groups.dev]` gains `pandas-stubs`.

- [ ] **Step 2: Write the API-surface probe test**

Same style as the existing `tests/test_agent_framework_setup.py` — no network, just confirms the installed library's shape matches what later tasks assume.

```python
# tests/test_pandas_setup.py
from __future__ import annotations

import pandas as pd


def test_dataframe_constructs_from_list_of_dicts():
    rows: list[dict[str, object]] = [
        {"agent": "coverage", "composite_score": 0.9},
        {"agent": "coverage", "composite_score": 0.8},
        {"agent": "fraud", "composite_score": 1.0},
    ]

    df = pd.DataFrame(rows)

    assert list(df.columns) == ["agent", "composite_score"]
    assert len(df) == 3


def test_groupby_mean_reset_index_produces_named_summary_column():
    df = pd.DataFrame(
        [
            {"agent": "coverage", "composite_score": 0.9},
            {"agent": "coverage", "composite_score": 0.7},
        ]
    )

    summary = df.groupby("agent")["composite_score"].mean().reset_index(name="mean_score")

    assert list(summary.columns) == ["agent", "mean_score"]
    assert summary["mean_score"].iloc[0] == 0.8


def test_to_string_returns_a_str():
    df = pd.DataFrame([{"agent": "coverage", "composite_score": 0.9}])

    text = df.to_string(index=False)

    assert isinstance(text, str)
    assert "coverage" in text
```

- [ ] **Step 3: Run the test**

Run: `uv run pytest tests/test_pandas_setup.py -v`
Expected: PASS (3 passed)

- [ ] **Step 4: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean (this is the check that actually confirms `pandas-stubs` resolved the `import-untyped`/`no-any-return` errors found during planning).

- [ ] **Step 5: Commit**

```powershell
git add pyproject.toml uv.lock tests/test_pandas_setup.py
git commit -m "chore: add pandas + pandas-stubs for the eval aggregation report"
```

---

### Task 2: Judge model deployments + config wiring

**Files:**
- Modify: `src/claims_assistant/config.py`
- Modify: `.env.example`
- Modify: `tests/test_config.py`

**Interfaces:**
- Consumes: nothing new.
- Produces: `Settings.azure_openai_eval_judge_primary_deployment: str`, `Settings.azure_openai_eval_judge_secondary_deployment: str` — consumed by Task 5's `build_judge_agent()` calls in `tests/test_eval_judge.py` and later by `tests/test_eval_suite.py` (Task 10).

- [ ] **Step 1: Provision the two judge deployments**

Reuses the existing `claims-assistant-openai` resource (per the working agreement — no second OpenAI resource). Per the live catalog check in Global Constraints: `gpt-5.5` for the primary judge (matches spec §4's "GPT-5 (full)" row), `gpt-4.1` for the secondary, distinct-family judge.

```powershell
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name eval-judge-primary --model-name gpt-5.5 --model-version "2026-04-24" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
az cognitiveservices account deployment create --name claims-assistant-openai --resource-group claims-assistant-rg --deployment-name eval-judge-secondary --model-name gpt-4.1 --model-version "2025-04-14" --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
```

If either fails with a capacity/quota error, retry with a lower `--sku-capacity` (e.g. `5`) — this is a demo workload, not production traffic.

- [ ] **Step 2: Extend the config test**

Add these two lines inside `test_settings_reads_from_env` in `tests/test_config.py`, right after the existing `AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT` line:

```python
    monkeypatch.setenv(
        "AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT", "test-judge-primary-deployment"
    )
    monkeypatch.setenv(
        "AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT", "test-judge-secondary-deployment"
    )
```

And these two assertions, right after the existing `azure_openai_adjuster_summary_deployment` assertion:

```python
    assert settings.azure_openai_eval_judge_primary_deployment == "test-judge-primary-deployment"
    assert (
        settings.azure_openai_eval_judge_secondary_deployment
        == "test-judge-secondary-deployment"
    )
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'azure_openai_eval_judge_primary_deployment'`

- [ ] **Step 4: Add the settings fields**

In `src/claims_assistant/config.py`, add these two lines right after the existing `azure_openai_adjuster_summary_deployment: str = ""` line:

```python
    azure_openai_eval_judge_primary_deployment: str = ""
    azure_openai_eval_judge_secondary_deployment: str = ""
```

- [ ] **Step 5: Update `.env.example`**

Add these two lines to `.env.example`, after the existing `AZURE_OPENAI_ADJUSTER_SUMMARY_DEPLOYMENT` line:

```
AZURE_OPENAI_EVAL_JUDGE_PRIMARY_DEPLOYMENT=eval-judge-primary
AZURE_OPENAI_EVAL_JUDGE_SECONDARY_DEPLOYMENT=eval-judge-secondary
```

Also add the same two lines (with your real values) to your actual `.env` — every later integration test in this phase needs them.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [ ] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 8: Commit**

```powershell
git add src/claims_assistant/config.py .env.example tests/test_config.py
git commit -m "feat: add eval judge model deployments and config"
```

---

### Task 3: Coverage-determination eval fixtures

**Files:**
- Create: `data/eval_fixtures/coverage/cov_001_full_coverage_collision_approve.json`
- Create: `data/eval_fixtures/coverage/cov_002_liability_only_collision_deny.json`
- Create: `data/eval_fixtures/coverage/cov_003_delivery_use_unstated_endorsement_needs_info.json`
- Create: `data/eval_fixtures/coverage/cov_004_comprehensive_collision_hail_approve.json`
- Create: `data/eval_fixtures/coverage/cov_005_liability_only_theft_deny.json`
- Modify: `src/claims_assistant/eval_fixtures.py`
- Test: `tests/test_eval_fixtures.py`

**Interfaces:**
- Consumes: nothing new.
- Produces: `CoverageFixture` dataclass (`fixture_id: str`, `policy_number: str`, `claim_narrative: str`, `gold_determination: Literal["approve", "deny", "needs_info"]`, `gold_citation: str`), `load_coverage_fixtures() -> list[CoverageFixture]` (`eval_fixtures.py`). Task 7's `eval/coverage_eval.py` and Task 10's `tests/test_eval_suite.py` import both.

Each fixture's `gold_determination`/`gold_citation` was verified against the real policy document text (Global Constraints) — not guessed. Fixtures 1–3 reuse the exact `policy_number`/narrative already hand-verified correct in `tests/test_coverage_agent.py`; fixtures 4–5 are new, added for state/tier diversity (comprehensive-type claim, and a second liability-only deny case in a different state).

- [ ] **Step 1: Create the five fixture files**

```json
// data/eval_fixtures/coverage/cov_001_full_coverage_collision_approve.json
{
  "policy_number": "POL-CA-0002",
  "claim_narrative": "I rear-ended another car while driving to work in my Tesla Model 3; my front bumper is damaged.",
  "gold_determination": "approve",
  "gold_citation": "CA-FULL-COVERAGE_section-3-physical-damage-coverage"
}
```

```json
// data/eval_fixtures/coverage/cov_002_liability_only_collision_deny.json
{
  "policy_number": "POL-CA-0001",
  "claim_narrative": "I rear-ended another car while driving to work in my Ford Focus; my front bumper is damaged.",
  "gold_determination": "deny",
  "gold_citation": "CA-LIABILITY-ONLY_section-3-physical-damage-coverage"
}
```

```json
// data/eval_fixtures/coverage/cov_003_delivery_use_unstated_endorsement_needs_info.json
{
  "policy_number": "POL-CA-0002",
  "claim_narrative": "I had just dropped off a food delivery order for a local restaurant's delivery app when another driver rear-ended me at a stoplight, denting my rear bumper. This was the first time I've ever done a delivery run in this car.",
  "gold_determination": "needs_info",
  "gold_citation": "CA-FULL-COVERAGE_section-4-exclusions"
}
```

```json
// data/eval_fixtures/coverage/cov_004_comprehensive_collision_hail_approve.json
{
  "policy_number": "POL-TX-0006",
  "claim_narrative": "Hail damage to my Ford F-150 while it was parked outside overnight during a severe thunderstorm in Austin, TX.",
  "gold_determination": "approve",
  "gold_citation": "TX-COMPREHENSIVE-COLLISION_section-3-physical-damage-coverage"
}
```

```json
// data/eval_fixtures/coverage/cov_005_liability_only_theft_deny.json
{
  "policy_number": "POL-NY-0007",
  "claim_narrative": "My Toyota Corolla was stolen from outside my apartment overnight in Buffalo, NY.",
  "gold_determination": "deny",
  "gold_citation": "NY-LIABILITY-ONLY_section-3-physical-damage-coverage"
}
```

- [ ] **Step 2: Write the failing loader test**

Add this test to `tests/test_eval_fixtures.py` (below the existing extraction fixture tests):

```python
def test_load_coverage_fixtures_returns_all_fixtures():
    from claims_assistant.eval_fixtures import load_coverage_fixtures

    fixtures = load_coverage_fixtures()

    assert len(fixtures) == 5
    ids = {f.fixture_id for f in fixtures}
    assert len(ids) == 5
    determinations = {f.gold_determination for f in fixtures}
    assert determinations == {"approve", "deny", "needs_info"}
    for fixture in fixtures:
        assert fixture.policy_number
        assert fixture.claim_narrative
        assert fixture.gold_citation
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: FAIL — `ImportError: cannot import name 'load_coverage_fixtures'`

- [ ] **Step 4: Extend `eval_fixtures.py`**

Replace the top of `src/claims_assistant/eval_fixtures.py` (the `FIXTURES_DIR` constant and imports) with:

```python
# src/claims_assistant/eval_fixtures.py
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

from pydantic import BaseModel

from claims_assistant.fnol_schema import FNOLFacts

FIXTURES_ROOT = Path(__file__).resolve().parents[2] / "data" / "eval_fixtures"
EXTRACTION_FIXTURES_DIR = FIXTURES_ROOT / "extraction"
COVERAGE_FIXTURES_DIR = FIXTURES_ROOT / "coverage"
FRAUD_FIXTURES_DIR = FIXTURES_ROOT / "fraud"
```

Change the existing `load_extraction_fixtures()` to use the renamed constant — replace:

```python
    for txt_path in sorted(FIXTURES_DIR.glob("*.txt")):
```

with:

```python
    for txt_path in sorted(EXTRACTION_FIXTURES_DIR.glob("*.txt")):
```

Then add this at the end of the file:

```python
class _CoverageFixtureData(BaseModel):
    policy_number: str
    claim_narrative: str
    gold_determination: Literal["approve", "deny", "needs_info"]
    gold_citation: str


@dataclass(frozen=True)
class CoverageFixture:
    fixture_id: str
    policy_number: str
    claim_narrative: str
    gold_determination: Literal["approve", "deny", "needs_info"]
    gold_citation: str


def load_coverage_fixtures() -> list[CoverageFixture]:
    fixtures = []
    for json_path in sorted(COVERAGE_FIXTURES_DIR.glob("*.json")):
        data = _CoverageFixtureData.model_validate_json(json_path.read_text(encoding="utf-8"))
        fixtures.append(
            CoverageFixture(
                fixture_id=json_path.stem,
                policy_number=data.policy_number,
                claim_narrative=data.claim_narrative,
                gold_determination=data.gold_determination,
                gold_citation=data.gold_citation,
            )
        )
    return fixtures
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: PASS (all extraction tests still pass, plus the new coverage test — 4 passed)

- [ ] **Step 6: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 7: Commit**

```powershell
git add data/eval_fixtures/coverage src/claims_assistant/eval_fixtures.py tests/test_eval_fixtures.py
git commit -m "feat: add coverage-determination eval fixtures"
```

---

### Task 4: Fraud-risk eval fixtures

**Files:**
- Create: `data/eval_fixtures/fraud/fraud_001_clean_low_risk.json`
- Create: `data/eval_fixtures/fraud/fraud_002_single_old_claim_low_risk.json`
- Create: `data/eval_fixtures/fraud/fraud_003_repeat_claims_medium_risk.json`
- Create: `data/eval_fixtures/fraud/fraud_004_recent_policy_prior_fraud_high_risk.json`
- Create: `data/eval_fixtures/fraud/fraud_005_frequent_claims_prior_fraud_high_risk.json`
- Modify: `src/claims_assistant/eval_fixtures.py`
- Test: `tests/test_eval_fixtures.py`

**Interfaces:**
- Consumes: `RedFlagCode` (`agents/fraud_signals.py`, already defined).
- Produces: `FraudFixture` dataclass (`fixture_id: str`, `policy_number: str`, `vin: str`, `incident_date: str`, `claim_narrative: str`, `gold_risk_tier: Literal["low", "medium", "high"]`, `gold_red_flags: list[RedFlagCode]`), `load_fraud_fixtures() -> list[FraudFixture]` (`eval_fixtures.py`). Task 8's `eval/fraud_eval.py` and Task 10's `tests/test_eval_suite.py` import both.

Each fixture's `gold_risk_tier`/`gold_red_flags` was computed by hand against the real seeded data in `src/claims_assistant/seed_data.py` (Global Constraints) — `run_fraud_eval` (Task 8) re-derives the same numbers deterministically at test time and asserts they still match, so any hand-authoring mistake here fails loudly rather than silently mis-grading the agent. Fixtures 1 and 4 reuse the exact scenarios already hand-verified in `tests/test_fraud_agent.py`.

- [ ] **Step 1: Create the five fixture files**

```json
// data/eval_fixtures/fraud/fraud_001_clean_low_risk.json
{
  "policy_number": "POL-CA-0003",
  "vin": "1C4RJFBG5FC123458",
  "incident_date": "2026-03-10",
  "claim_narrative": "Hail damage to my Jeep Grand Cherokee while it was parked outside my home overnight during a storm.",
  "gold_risk_tier": "low",
  "gold_red_flags": []
}
```

```json
// data/eval_fixtures/fraud/fraud_002_single_old_claim_low_risk.json
{
  "policy_number": "POL-NY-0008",
  "vin": "WBA8E9G59JNU12345",
  "incident_date": "2026-02-01",
  "claim_narrative": "My windshield cracked from a rock while driving on the highway.",
  "gold_risk_tier": "low",
  "gold_red_flags": []
}
```

```json
// data/eval_fixtures/fraud/fraud_003_repeat_claims_medium_risk.json
{
  "policy_number": "POL-TX-0005",
  "vin": "1HGCV1F34LA123460",
  "incident_date": "2026-06-01",
  "claim_narrative": "I was rear-ended at a red light; my rear bumper and trunk are damaged.",
  "gold_risk_tier": "medium",
  "gold_red_flags": ["high_claim_frequency"]
}
```

```json
// data/eval_fixtures/fraud/fraud_004_recent_policy_prior_fraud_high_risk.json
{
  "policy_number": "POL-TX-0006",
  "vin": "1FTFW1ET5EF123461",
  "incident_date": "2025-08-01",
  "claim_narrative": "My Ford F-150 was stolen overnight from a parking lot; I don't have any other details.",
  "gold_risk_tier": "high",
  "gold_red_flags": [
    "recent_policy_inception",
    "prior_fraud_flag",
    "clustered_recent_claims",
    "prior_claim_near_vehicle_value"
  ]
}
```

```json
// data/eval_fixtures/fraud/fraud_005_frequent_claims_prior_fraud_high_risk.json
{
  "policy_number": "POL-CA-0002",
  "vin": "5YJ3E1EA7JF123457",
  "incident_date": "2026-02-01",
  "claim_narrative": "My Tesla Model 3 was stolen from my driveway overnight; I have no additional details.",
  "gold_risk_tier": "high",
  "gold_red_flags": ["prior_fraud_flag", "high_claim_frequency"]
}
```

- [ ] **Step 2: Write the failing loader test**

Add this test to `tests/test_eval_fixtures.py`:

```python
def test_load_fraud_fixtures_returns_all_fixtures():
    from claims_assistant.eval_fixtures import load_fraud_fixtures

    fixtures = load_fraud_fixtures()

    assert len(fixtures) == 5
    ids = {f.fixture_id for f in fixtures}
    assert len(ids) == 5
    tiers = {f.gold_risk_tier for f in fixtures}
    assert tiers == {"low", "medium", "high"}
    for fixture in fixtures:
        assert fixture.policy_number
        assert fixture.vin
        assert fixture.incident_date
        assert fixture.claim_narrative
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: FAIL — `ImportError: cannot import name 'load_fraud_fixtures'`

- [ ] **Step 4: Extend `eval_fixtures.py`**

Add this import near the top of `src/claims_assistant/eval_fixtures.py`, alongside the existing `from claims_assistant.fnol_schema import FNOLFacts` line:

```python
from claims_assistant.agents.fraud_signals import RedFlagCode
```

Then add this at the end of the file:

```python
class _FraudFixtureData(BaseModel):
    policy_number: str
    vin: str
    incident_date: str
    claim_narrative: str
    gold_risk_tier: Literal["low", "medium", "high"]
    gold_red_flags: list[RedFlagCode]


@dataclass(frozen=True)
class FraudFixture:
    fixture_id: str
    policy_number: str
    vin: str
    incident_date: str
    claim_narrative: str
    gold_risk_tier: Literal["low", "medium", "high"]
    gold_red_flags: list[RedFlagCode]


def load_fraud_fixtures() -> list[FraudFixture]:
    fixtures = []
    for json_path in sorted(FRAUD_FIXTURES_DIR.glob("*.json")):
        data = _FraudFixtureData.model_validate_json(json_path.read_text(encoding="utf-8"))
        fixtures.append(
            FraudFixture(
                fixture_id=json_path.stem,
                policy_number=data.policy_number,
                vin=data.vin,
                incident_date=data.incident_date,
                claim_narrative=data.claim_narrative,
                gold_risk_tier=data.gold_risk_tier,
                gold_red_flags=data.gold_red_flags,
            )
        )
    return fixtures
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_eval_fixtures.py -v`
Expected: PASS (5 passed)

- [ ] **Step 6: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 7: Commit**

```powershell
git add data/eval_fixtures/fraud src/claims_assistant/eval_fixtures.py tests/test_eval_fixtures.py
git commit -m "feat: add fraud-risk eval fixtures"
```

---

### Task 5: LLM-as-judge module

**Files:**
- Create: `src/claims_assistant/eval/__init__.py`
- Create: `src/claims_assistant/eval/judge_schema.py`
- Create: `src/claims_assistant/eval/judge.py`
- Test: `tests/test_eval_judge.py`

**Interfaces:**
- Consumes: `Settings` (`config.py`, Task 2's new fields).
- Produces: `GroundingJudgment` (Pydantic: `grounded: bool`, `reasoning: str`), `build_judge_agent(settings: Settings, deployment: str) -> Agent`, `judge_grounding(agent: Agent, claim_text: str, evidence_text: str) -> GroundingJudgment` (`eval/judge.py`). Tasks 7 and 8's coverage/fraud runners import all three.

This single module serves both Coverage and Fraud grounding checks — the underlying task ("is this claim text supported by this evidence text") is identical in both cases; only what counts as "evidence" differs, and that's the caller's concern, not the judge's.

- [ ] **Step 1: Create the empty package init**

```python
# src/claims_assistant/eval/__init__.py
```

- [ ] **Step 2: Write the failing judge tests**

These call both real judge deployments — this is also the empirical confirmation that `gpt-4.1` (the first non-GPT-5-family model this project has ever used) actually supports `ChatOptions(response_format=...)` structured output the same way the GPT-5 family already does.

```python
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
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `uv run pytest tests/test_eval_judge.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.judge'`

- [ ] **Step 4: Write the judge schema**

```python
# src/claims_assistant/eval/judge_schema.py
from __future__ import annotations

from pydantic import BaseModel


class GroundingJudgment(BaseModel):
    grounded: bool
    reasoning: str
```

- [ ] **Step 5: Write the judge module**

```python
# src/claims_assistant/eval/judge.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.config import Settings
from claims_assistant.eval.judge_schema import GroundingJudgment

INSTRUCTIONS = """\
You are an evaluation judge for an insurance claims-assistant system. You are given a \
CLAIM -- a short piece of reasoning text an agent produced -- and EVIDENCE -- the source \
material the agent was supposed to base that reasoning on.

Decide whether every factual assertion in the CLAIM is actually supported by the EVIDENCE. \
This is a grounding check, not a correctness check: you are not judging whether the \
agent's ultimate decision (e.g. approve/deny, or a fraud risk tier) was the right call. \
You are judging only whether the stated reasoning is faithful to the evidence given.

Set "grounded" to true only if every specific factual claim in the CLAIM text traces back \
to something actually stated in the EVIDENCE. Set it to false if the CLAIM asserts \
anything -- a number, a clause, a fact -- that the EVIDENCE does not support, or that \
contradicts the EVIDENCE.

A CLAIM is allowed to draw a reasonable category, label, or summary from facts that ARE \
present in the EVIDENCE -- for example, calling weather damage to a parked vehicle a \
"comprehensive-type loss," or summarizing several individually-listed true/false signals as \
"no red flags." That is normal reasoning over the evidence, not fabrication. Only mark \
"grounded" false when the CLAIM states a specific fact, number, name, date, or computed \
value that is absent from or contradicted by the EVIDENCE -- not merely paraphrased, \
categorized, or summarized differently than the EVIDENCE happens to phrase it.

"reasoning" should briefly explain your verdict, quoting the specific part of the CLAIM \
that is or isn't supported.
"""


def build_judge_agent(settings: Settings, deployment: str) -> Agent:
    client = OpenAIChatCompletionClient(
        model=deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )
    return Agent(client=client, instructions=INSTRUCTIONS)


def _build_prompt(claim_text: str, evidence_text: str) -> str:
    return (
        f"CLAIM:\n{claim_text}\n\n"
        f"EVIDENCE:\n{evidence_text}\n\n"
        f"Judge whether the CLAIM is grounded in the EVIDENCE."
    )


async def judge_grounding(agent: Agent, claim_text: str, evidence_text: str) -> GroundingJudgment:
    prompt = _build_prompt(claim_text, evidence_text)
    response = await agent.run(prompt, options=ChatOptions(response_format=GroundingJudgment))
    judgment = response.value
    assert isinstance(judgment, GroundingJudgment)
    return judgment
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `uv run pytest tests/test_eval_judge.py -v -m integration`
Expected: PASS (3 passed). If the secondary-judge test fails specifically (the other two pass), re-check `gpt-4.1`'s structured-output behavior directly before assuming a bug in `judge.py` — this is the first time this project has sent `ChatOptions(response_format=...)` to a non-GPT-5-family deployment.

- [ ] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 8: Commit**

```powershell
git add src/claims_assistant/eval/__init__.py src/claims_assistant/eval/judge_schema.py src/claims_assistant/eval/judge.py tests/test_eval_judge.py
git commit -m "feat: add LLM-as-judge grounding module"
```

---

### Task 6: `EvalResult` type + extraction eval runner

**Files:**
- Create: `src/claims_assistant/eval/results.py`
- Create: `src/claims_assistant/eval/extraction_eval.py`
- Test: `tests/test_extraction_eval_runner.py`

**Interfaces:**
- Consumes: `extract_fnol_facts` (`agents/extraction_agent.py`); `score_extraction` (`agents/extraction_scoring.py`); `ExtractionFixture` (`eval_fixtures.py`).
- Produces: `AgentName` (`Literal["extraction", "coverage", "fraud"]`), `EvalResult` (dataclass: `agent: AgentName`, `fixture_id: str`, `correctness_score: float`, `grounding_score: float | None`, `composite_score: float`, `primary_judge_grounded: bool | None`, `secondary_judge_grounded: bool | None`, `judge_disagreement: bool`), `compute_composite_score(correctness: float, grounding: float | None) -> float` (`eval/results.py`); `run_extraction_eval(agent: Agent, fixtures: list[ExtractionFixture]) -> list[EvalResult]` (`eval/extraction_eval.py`). Tasks 7, 8, 9, 10 all import `EvalResult`/`compute_composite_score`; Task 10 imports `run_extraction_eval`.

- [ ] **Step 1: Write the failing runner test**

```python
# tests/test_extraction_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.extraction_eval import run_extraction_eval
from claims_assistant.eval_fixtures import load_extraction_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_extraction_eval_returns_one_result_per_fixture():
    settings = get_settings()
    agent = build_extraction_agent(settings)
    fixtures = load_extraction_fixtures()

    results = await run_extraction_eval(agent, fixtures)

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "extraction"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score is None
        assert result.composite_score == result.correctness_score
        assert result.primary_judge_grounded is None
        assert result.judge_disagreement is False
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_extraction_eval_runner.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.results'`

- [ ] **Step 3: Write the shared result type**

```python
# src/claims_assistant/eval/results.py
from __future__ import annotations

from dataclasses import dataclass
from typing import Literal

AgentName = Literal["extraction", "coverage", "fraud"]


@dataclass(frozen=True)
class EvalResult:
    agent: AgentName
    fixture_id: str
    correctness_score: float
    grounding_score: float | None
    composite_score: float
    primary_judge_grounded: bool | None
    secondary_judge_grounded: bool | None
    judge_disagreement: bool


def compute_composite_score(correctness: float, grounding: float | None) -> float:
    scores = [correctness] if grounding is None else [correctness, grounding]
    return sum(scores) / len(scores)
```

`compute_composite_score` is deliberately not named `composite_score` — that name is already taken by `EvalResult.composite_score` above it, and a same-named module-level function reads confusingly at call sites (`composite_score=composite_score(...)`).

- [ ] **Step 4: Write the extraction runner**

```python
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
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `uv run pytest tests/test_extraction_eval_runner.py -v -m integration`
Expected: PASS (1 passed)

- [ ] **Step 6: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 7: Commit**

```powershell
git add src/claims_assistant/eval/results.py src/claims_assistant/eval/extraction_eval.py tests/test_extraction_eval_runner.py
git commit -m "feat: add EvalResult type and extraction eval runner"
```

---

### Task 7: Coverage eval runner

**Files:**
- Create: `src/claims_assistant/eval/coverage_eval.py`
- Test: `tests/test_coverage_eval_runner.py`

**Interfaces:**
- Consumes: `determine_coverage`, `lookup_policy_by_number` (`agents/coverage_agent.py`); `retrieve_policy_chunks` (`search/retrieval.py`); `build_judge_agent`, `judge_grounding` (`eval/judge.py`, Task 5); `EvalResult`, `composite_score` (`eval/results.py`, Task 6); `CoverageFixture` (`eval_fixtures.py`, Task 3).
- Produces: `run_coverage_eval(coverage_agent: Agent, judge_primary: Agent, judge_secondary: Agent, settings: Settings, fixtures: list[CoverageFixture]) -> list[EvalResult]` (`eval/coverage_eval.py`). Task 10's `tests/test_eval_suite.py` imports it.

- [ ] **Step 1: Write the failing runner test**

```python
# tests/test_coverage_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.coverage_eval import run_coverage_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval_fixtures import load_coverage_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_coverage_eval_returns_one_result_per_fixture(seeded_db):
    settings = get_settings()
    coverage_agent = build_coverage_agent(settings)
    judge_primary = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )
    fixtures = load_coverage_fixtures()

    results = await run_coverage_eval(
        coverage_agent, judge_primary, judge_secondary, settings, fixtures
    )

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "coverage"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score in (0.0, 1.0)
        assert result.primary_judge_grounded is not None
        assert result.secondary_judge_grounded is not None
        assert isinstance(result.judge_disagreement, bool)
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_coverage_eval_runner.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.coverage_eval'`

- [ ] **Step 3: Write the coverage runner**

```python
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

        policy = await lookup_policy_by_number(fixture.policy_number)
        chunks = await retrieve_policy_chunks(
            settings, form_id=policy.policy_form_id, query_text=fixture.claim_narrative
        )
        cited = [c for c in chunks if c.chunk_id in determination.citations]
        clauses_text = "\n\n".join(f"[{c.chunk_id}] {c.content}" for c in cited)
        # The rationale legitimately restates facts from the claim narrative itself (e.g.
        # "damage to the front bumper"), not just the retrieved clauses -- that's real input
        # the agent was given, not fabrication. The judge's EVIDENCE must include it too, or
        # a strict judge correctly (per its own instructions) flags a true, narrative-sourced
        # statement as unsupported, discovered empirically during Task 10's first real run.
        evidence_text = f"Claim narrative:\n{fixture.claim_narrative}\n\nRetrieved policy clauses:\n{clauses_text}"

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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_coverage_eval_runner.py -v -m integration`
Expected: PASS (1 passed)

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/eval/coverage_eval.py tests/test_coverage_eval_runner.py
git commit -m "feat: add coverage eval runner with dual-judge grounding check"
```

---

### Task 8: Fraud eval runner

**Files:**
- Create: `src/claims_assistant/eval/fraud_eval.py`
- Test: `tests/test_fraud_eval_runner.py`

**Interfaces:**
- Consumes: `assess_fraud_risk`, `lookup_claims_history`, `lookup_vehicle_by_vin` (`agents/fraud_agent.py`); `lookup_policy_by_number` (`agents/coverage_agent.py`); `compute_fraud_signals`, `determine_actual_red_flags`, `FraudSignals` (`agents/fraud_signals.py`); `build_judge_agent`, `judge_grounding` (`eval/judge.py`); `EvalResult`, `composite_score` (`eval/results.py`); `FraudFixture` (`eval_fixtures.py`, Task 4).
- Produces: `run_fraud_eval(fraud_agent: Agent, judge_primary: Agent, judge_secondary: Agent, fixtures: list[FraudFixture]) -> list[EvalResult]` (`eval/fraud_eval.py`). Task 10's `tests/test_eval_suite.py` imports it.

- [ ] **Step 1: Write the failing runner test**

```python
# tests/test_fraud_eval_runner.py
from __future__ import annotations

import pytest

from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.fraud_eval import run_fraud_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval_fixtures import load_fraud_fixtures

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_run_fraud_eval_returns_one_result_per_fixture(seeded_db):
    settings = get_settings()
    fraud_agent = build_fraud_agent(settings)
    judge_primary = build_judge_agent(settings, settings.azure_openai_eval_judge_primary_deployment)
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )
    fixtures = load_fraud_fixtures()

    results = await run_fraud_eval(fraud_agent, judge_primary, judge_secondary, fixtures)

    assert len(results) == len(fixtures)
    for result in results:
        assert result.agent == "fraud"
        assert 0.0 <= result.correctness_score <= 1.0
        assert result.grounding_score in (0.0, 1.0)
        assert result.primary_judge_grounded is not None
        assert result.secondary_judge_grounded is not None
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_fraud_eval_runner.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.fraud_eval'`

- [ ] **Step 3: Write the fraud runner**

```python
# src/claims_assistant/eval/fraud_eval.py
from __future__ import annotations

from typing import get_args

from agent_framework import Agent

from claims_assistant.agents.coverage_agent import lookup_policy_by_number
from claims_assistant.agents.fraud_agent import (
    assess_fraud_risk,
    lookup_claims_history,
    lookup_vehicle_by_vin,
)
from claims_assistant.agents.fraud_signals import (
    FraudSignals,
    RedFlagCode,
    compute_fraud_signals,
    determine_actual_red_flags,
)
from claims_assistant.eval.judge import judge_grounding
from claims_assistant.eval.results import EvalResult, compute_composite_score
from claims_assistant.eval_fixtures import FraudFixture


def _evidence_text(
    signals: FraudSignals, actual_flags: set[RedFlagCode], claim_narrative: str
) -> str:
    # Mirrors everything fraud_agent.py's own prompt actually hands the agent (its
    # _build_prompt): the claim narrative, the vehicle make/model/year, and the computed
    # true/false red-flag block -- not just the raw signal numbers. Discovered empirically
    # during Task 10's first real run: a strict judge correctly (per its own instructions)
    # flagged the agent's true statements ("2020 Jeep Grand Cherokee", "no computed red-flag
    # signals are true") as unsupported when the judge's evidence omitted the very inputs
    # those statements came from.
    flags_block = "\n".join(
        f"- {code}: {'TRUE' if code in actual_flags else 'false'}"
        for code in get_args(RedFlagCode)
    )
    return (
        f"Claim narrative: {claim_narrative}\n\n"
        f"Vehicle: {signals.vehicle_year} {signals.vehicle_make} {signals.vehicle_model}\n"
        f"Days since policy effective: {signals.days_since_policy_effective}\n"
        f"Prior claim count: {signals.claim_count}\n"
        f"Prior fraud-flagged claims: {signals.prior_fraud_flag_count}\n"
        f"Days since most recent prior claim: {signals.days_since_most_recent_prior_claim}\n"
        f"Highest prior claim amount: {signals.highest_prior_claim_amount_usd}\n"
        f"Vehicle market value: {signals.vehicle_market_value_usd}\n\n"
        f"Computed red-flag signals:\n{flags_block}\n"
    )


async def run_fraud_eval(
    fraud_agent: Agent,
    judge_primary: Agent,
    judge_secondary: Agent,
    fixtures: list[FraudFixture],
) -> list[EvalResult]:
    results = []
    for fixture in fixtures:
        assessment = await assess_fraud_risk(
            fraud_agent,
            fixture.policy_number,
            fixture.vin,
            fixture.incident_date,
            fixture.claim_narrative,
        )
        tier_correct = float(assessment.risk_tier == fixture.gold_risk_tier)

        policy = await lookup_policy_by_number(fixture.policy_number)
        claims_history = await lookup_claims_history(fixture.policy_number)
        vehicle = await lookup_vehicle_by_vin(fixture.vin)
        signals = compute_fraud_signals(
            policy, claims_history, vehicle, fixture.incident_date
        )
        actual_flags = determine_actual_red_flags(signals)
        assert set(fixture.gold_red_flags) == actual_flags, (
            f"fixture {fixture.fixture_id} gold_red_flags stale vs deterministic "
            f"computation: gold={fixture.gold_red_flags} actual={sorted(actual_flags)}"
        )
        flags_correct = float(set(assessment.red_flags) == set(fixture.gold_red_flags))
        correctness = (tier_correct + flags_correct) / 2

        evidence_text = _evidence_text(signals, actual_flags, fixture.claim_narrative)
        primary = await judge_grounding(judge_primary, assessment.rationale, evidence_text)
        secondary = await judge_grounding(judge_secondary, assessment.rationale, evidence_text)
        # Both judges must agree the rationale is grounded -- see Design Decisions: the
        # primary judge deployment (gpt-5.5) is the literal same model already deployed as
        # fraud-risk-agent, so scoring on the primary judge alone would let the agent's own
        # model grade its own rationale on the one agent spec Section 4 calls highest-stakes.
        # Requiring the distinct secondary judge (gpt-4.1) to also agree is what makes the
        # anti-self-preference-bias check actually gate the score here, not just annotate it.
        grounding = 1.0 if (primary.grounded and secondary.grounded) else 0.0

        results.append(
            EvalResult(
                agent="fraud",
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
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_fraud_eval_runner.py -v -m integration`
Expected: PASS (1 passed). If the `assert set(fixture.gold_red_flags) == actual_flags` line fails for one of Task 4's fixtures: that means this plan's hand-computed day-math for that fixture was wrong — recompute `days_since_policy_effective`/`days_since_most_recent_prior_claim` against `seed_data.py`'s real dates and fix the fixture's `gold_red_flags`, don't change this assertion.

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/eval/fraud_eval.py tests/test_fraud_eval_runner.py
git commit -m "feat: add fraud eval runner with dual-judge grounding check"
```

---

### Task 9: Pandas aggregation report

**Files:**
- Create: `src/claims_assistant/eval/report.py`
- Test: `tests/test_eval_report.py`

**Interfaces:**
- Consumes: `EvalResult` (`eval/results.py`, Task 6).
- Produces: `build_eval_report(results: list[EvalResult]) -> pd.DataFrame`, `summarize_by_agent(report: pd.DataFrame) -> pd.DataFrame` (`eval/report.py`). Task 10's `tests/test_eval_suite.py` imports both.

This is the phase's only pure-unit task besides Task 1 — `EvalResult` is a plain dataclass, so no agents/network/DB are needed to test report-building. `build_eval_report` materializes the generator expression into a `list[dict[str, object]]` before passing it to `pd.DataFrame(...)` — Task 1's `mypy --strict` probe verified that exact `list`-of-`dict` shape, not a bare generator, so this task deliberately uses the same shape rather than a superficially-equivalent one that was never actually checked.

- [ ] **Step 1: Write the failing report tests**

```python
# tests/test_eval_report.py
from __future__ import annotations

from claims_assistant.eval.report import build_eval_report, summarize_by_agent
from claims_assistant.eval.results import EvalResult

_RESULTS = [
    EvalResult(
        agent="extraction",
        fixture_id="fnol_001",
        correctness_score=1.0,
        grounding_score=None,
        composite_score=1.0,
        primary_judge_grounded=None,
        secondary_judge_grounded=None,
        judge_disagreement=False,
    ),
    EvalResult(
        agent="coverage",
        fixture_id="cov_001",
        correctness_score=1.0,
        grounding_score=1.0,
        composite_score=1.0,
        primary_judge_grounded=True,
        secondary_judge_grounded=True,
        judge_disagreement=False,
    ),
    EvalResult(
        agent="coverage",
        fixture_id="cov_002",
        correctness_score=0.0,
        grounding_score=1.0,
        composite_score=0.5,
        primary_judge_grounded=True,
        secondary_judge_grounded=False,
        judge_disagreement=True,
    ),
]


def test_build_eval_report_has_one_row_per_result():
    report = build_eval_report(_RESULTS)

    assert len(report) == 3
    assert list(report.columns) == [
        "agent",
        "fixture_id",
        "correctness_score",
        "grounding_score",
        "composite_score",
        "primary_judge_grounded",
        "secondary_judge_grounded",
        "judge_disagreement",
    ]


def test_summarize_by_agent_averages_composite_score_per_agent():
    report = build_eval_report(_RESULTS)

    summary = summarize_by_agent(report)

    assert list(summary.columns) == ["agent", "mean_score"]
    coverage_row = summary[summary["agent"] == "coverage"].iloc[0]
    assert coverage_row["mean_score"] == 0.75
    extraction_row = summary[summary["agent"] == "extraction"].iloc[0]
    assert extraction_row["mean_score"] == 1.0
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_eval_report.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.report'`

- [ ] **Step 3: Write the report module**

```python
# src/claims_assistant/eval/report.py
from __future__ import annotations

import pandas as pd

from claims_assistant.eval.results import EvalResult


def build_eval_report(results: list[EvalResult]) -> pd.DataFrame:
    return pd.DataFrame(
        [
            {
                "agent": r.agent,
                "fixture_id": r.fixture_id,
                "correctness_score": r.correctness_score,
                "grounding_score": r.grounding_score,
                "composite_score": r.composite_score,
                "primary_judge_grounded": r.primary_judge_grounded,
                "secondary_judge_grounded": r.secondary_judge_grounded,
                "judge_disagreement": r.judge_disagreement,
            }
            for r in results
        ]
    )


def summarize_by_agent(report: pd.DataFrame) -> pd.DataFrame:
    return report.groupby("agent")["composite_score"].mean().reset_index(name="mean_score")
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_eval_report.py -v`
Expected: PASS (2 passed)

- [ ] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 6: Commit**

```powershell
git add src/claims_assistant/eval/report.py tests/test_eval_report.py
git commit -m "feat: add pandas eval aggregation report"
```

---

### Task 10: Baseline thresholds + the eval suite itself

**Files:**
- Create: `src/claims_assistant/eval/baselines.py`
- Create: `tests/test_eval_suite.py`

**Interfaces:**
- Consumes: everything from Tasks 2–9: `build_extraction_agent`, `build_coverage_agent`, `build_fraud_agent`; `build_judge_agent`; `run_extraction_eval`, `run_coverage_eval`, `run_fraud_eval`; `build_eval_report`, `summarize_by_agent`; `load_extraction_fixtures`, `load_coverage_fixtures`, `load_fraud_fixtures`; `get_settings`; `seeded_db` fixture (`tests/conftest.py`).
- Produces: `BASELINES: dict[str, float]` (`eval/baselines.py`); the roadmap's actual deliverable — a passing, real, scored eval run.

- [ ] **Step 1: Write the baseline constants**

Starting floor, not yet tuned against a real run — Step 5 below tunes it. (Final, real-run-tuned values ended up at `0.80` for all three agents — see Step 5's writeup below for the actual reasoning and the two genuine bugs this plan's first execution surfaced along the way.)

```python
# src/claims_assistant/eval/baselines.py
from __future__ import annotations

BASELINES: dict[str, float] = {
    "extraction": 0.70,
    "coverage": 0.70,
    "fraud": 0.70,
}
```

- [ ] **Step 2: Write the eval suite test**

```python
# tests/test_eval_suite.py
from __future__ import annotations

import pytest

from claims_assistant.agents.coverage_agent import build_coverage_agent
from claims_assistant.agents.extraction_agent import build_extraction_agent
from claims_assistant.agents.fraud_agent import build_fraud_agent
from claims_assistant.config import get_settings
from claims_assistant.eval.baselines import BASELINES
from claims_assistant.eval.coverage_eval import run_coverage_eval
from claims_assistant.eval.extraction_eval import run_extraction_eval
from claims_assistant.eval.fraud_eval import run_fraud_eval
from claims_assistant.eval.judge import build_judge_agent
from claims_assistant.eval.report import build_eval_report, summarize_by_agent
from claims_assistant.eval_fixtures import (
    load_coverage_fixtures,
    load_extraction_fixtures,
    load_fraud_fixtures,
)

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_eval_suite_produces_report_above_baseline(seeded_db):
    settings = get_settings()
    judge_primary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_primary_deployment
    )
    judge_secondary = build_judge_agent(
        settings, settings.azure_openai_eval_judge_secondary_deployment
    )

    extraction_results = await run_extraction_eval(
        build_extraction_agent(settings), load_extraction_fixtures()
    )
    coverage_results = await run_coverage_eval(
        build_coverage_agent(settings),
        judge_primary,
        judge_secondary,
        settings,
        load_coverage_fixtures(),
    )
    fraud_results = await run_fraud_eval(
        build_fraud_agent(settings), judge_primary, judge_secondary, load_fraud_fixtures()
    )

    report = build_eval_report(extraction_results + coverage_results + fraud_results)
    summary = summarize_by_agent(report)
    print("\n" + summary.to_string(index=False))
    disagreements = report[report["judge_disagreement"]]
    if len(disagreements):
        print("\nJudge disagreements:\n" + disagreements.to_string(index=False))

    for agent, baseline in BASELINES.items():
        mean_score = summary.loc[summary["agent"] == agent, "mean_score"].iloc[0]
        assert mean_score >= baseline, (
            f"{agent} mean score {mean_score:.2f} dropped below baseline {baseline}"
        )
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `uv run pytest tests/test_eval_suite.py -v -m integration`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.eval.baselines'`

- [ ] **Step 4: Run the tests to verify they pass against the `0.70` starting floor**

Run: `uv run pytest tests/test_eval_suite.py -v -m integration -s`
Expected: PASS (1 passed), with the per-agent report printed to the console (the `-s` flag is what makes the `print()` calls visible — this is "running the eval suite locally produces a scored report," the roadmap's own success-criteria wording).

**This is where this plan's first real execution surfaced two genuine bugs, not just baseline-tuning noise** — worth understanding even though the code blocks in Tasks 5/7/8 above already reflect the fix, so a fresh execution of this plan won't hit them the same way: the first real run scored `coverage 0.50` / `fraud 0.50`, with every disagreeing case showing `primary_judge_grounded=False, secondary_judge_grounded=True`, correctness `1.0` (the agent's answer was right, only "grounding" failed). Diagnosing one fixture directly (calling `determine_coverage`/`assess_fraud_risk` and `judge_grounding` standalone, printing the judge's `reasoning` field) showed the primary judge correctly — per its own literal instructions — flagging true statements as "unsupported" because the `evidence_text` built in `coverage_eval.py`/`fraud_eval.py` never included the claim narrative, the vehicle make/model/year, or the actual computed red-flag true/false block, even though the real agent's own prompt includes all of that. A rationale that legitimately restates a narrative fact or a computed flag looked like fabrication to a strict judge that was shown a narrower "evidence" set than the agent actually had. Fixed by expanding both runners' evidence construction to mirror everything the agent was actually given (Tasks 7/8's `evidence_text` above). A second, distinct issue remained after that fix — the primary judge flagging reasonable category labels like "comprehensive-type loss" as unsupported, since that exact phrase isn't literally present in the evidence even though the underlying facts (hail, parked, weather) are — fixed by loosening `eval/judge.py`'s `INSTRUCTIONS` to explicitly permit "a reasonable category, label, or summary from facts that ARE present," while still rejecting genuinely fabricated facts/numbers (Task 5's `INSTRUCTIONS` above already reflects this). Confirmed via `tests/test_eval_judge.py`'s existing fabrication-detection test still passing after the loosening — the instruction change made the judge less literal-minded, not less rigorous.

- [ ] **Step 5: Tighten the baselines against the real observed scores**

Read the printed summary table from Step 4. For each agent, set `BASELINES[agent]` in `src/claims_assistant/eval/baselines.py` to a value slightly below its observed real mean score — enough headroom to absorb ordinary LLM/judge variance run-to-run, tight enough that a genuine regression still trips it. With 5 fixtures per agent and each disagreeing case landing exactly on a `0.5` grounding score, one judge disagreement produces a composite mean of `0.90` and two simultaneous disagreements produce `0.80` — both are normal, expected outcomes of having two genuinely distinct judges (that's what the dual-judge design is *for*; zero disagreement, ever, would mean the second judge isn't actually independent). After the Step 4 fixes, two consecutive real runs landed at `coverage 0.90 / extraction 0.88 / fraud 0.90` and `coverage 0.90 / extraction 0.88 / fraud 0.90` respectively, with the *specific* disagreeing fixture differing between runs both times — evidence this is genuine per-call judge variance, not a remaining deterministic bug (a real bug keeps failing the same fixture). Baseline set to **`0.80`** for all three agents: tolerates up to two ordinary disagreements without flapping, while still catching a genuine correctness regression or a systematic grounding failure. Re-run Step 4's command to confirm the suite still passes with the tightened numbers.

- [ ] **Step 6: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [ ] **Step 7: Commit**

```powershell
git add src/claims_assistant/eval/baselines.py tests/test_eval_suite.py
git commit -m "feat: add eval suite with baseline-gated pandas report"
```

---

## Lessons Learned During Execution (Tasks 1–10)

Everything below was discovered running this plan for real, not while writing it — captured here so the reasoning behind the current code isn't lost, and so the same category of mistake is easier to recognize if this eval framework is extended later (e.g. a new agent added in a future phase).

**1. `mypy` command scope mistake (Task 1).** The plan originally told the executor to run `uv run mypy src tests/test_pandas_setup.py` — but this project's convention, in every other task in this plan and every prior phase, is `uv run mypy src` only; test files are never held to `disallow_untyped_defs`. This produced 3 spurious `no-untyped-def` errors on plain `def test_...():` functions. Fixed by reverting the command to match the established convention (now reflected in Task 1 Step 4 above). Not a code bug — a plan-authoring slip that only surfaced by actually running the command.

**2. Test-count expectation typo (Task 2).** The plan said "Expected: PASS (3 passed)" for `tests/test_config.py`, but that file has exactly 2 test functions (`test_settings_reads_from_env`, extended in Task 2, plus the pre-existing `test_get_settings_is_cached`) — never 3. Cosmetic, but worth noting: expected counts in a plan are a claim, and this one was arithmetic carried over incorrectly from a different file. Fixed in Task 2 Step 6 above.

**3. Coverage grounding evidence was incomplete (Tasks 7 and 10).** The first real run of the full suite scored `coverage 0.50`, with every disagreement showing the primary judge marking a *correct* rationale as ungrounded. Diagnosing one fixture directly (calling `determine_coverage` and `judge_grounding` standalone, printing the judge's `reasoning`) showed the judge correctly — per its own literal instructions — flagging a true statement ("damage to the policyholder's own front bumper") as unsupported, because `evidence_text` was built from the retrieved policy clauses only and never included the claim narrative that fact actually came from. The root cause: `evidence_text` was assembled as "the retrieval output," not as "everything the agent was actually given" — those aren't the same thing, and the agent's own rationale is allowed to (and does) reference both. Fixed by prepending the claim narrative to `evidence_text` (Task 7's `run_coverage_eval` above).

**4. Fraud grounding evidence had the same root cause, twice more (Tasks 8 and 10).** Same diagnostic approach on a fraud fixture found `_evidence_text` omitted (a) the claim narrative — identical to #3 — and (b) the vehicle's make/model/**year**, and (c) the actual computed red-flag true/false block. All three are literally handed to the real Fraud-Risk Agent in its own prompt (`fraud_agent.py::_build_prompt`), so a rationale legitimately citing "2020 Jeep Grand Cherokee" or "no computed red-flag signals are true" looked fabricated to a judge that was never shown that agent's real input. Fixed by rebuilding `_evidence_text` to mirror `_build_prompt`'s actual content, reusing `actual_flags` (already computed in the loop for the gold-label self-check assertion) and `typing.get_args(RedFlagCode)` to enumerate the flag codes rather than hand-listing a subset (Task 8's `_evidence_text`/`run_fraud_eval` above).

**5. The grounding judge was over-literal about reasonable categorization (Tasks 5 and 10).** Even after #3/#4 were fixed, one disagreement persisted: the judge flagged "consistent with a comprehensive-type loss" as unsupported, since that exact phrase isn't literally in the evidence — even though the underlying facts it's summarizing (hail, parked vehicle, weather) are. This is a different kind of gap than #3/#4: not missing evidence, but the judge's own instructions not distinguishing "inventing a new fact" from "reasonably labeling/summarizing facts that are present." Fixed by adding an explicit carve-out to `eval/judge.py`'s `INSTRUCTIONS` (Task 5 above) — verified `tests/test_eval_judge.py`'s fabrication-detection test still correctly returns `grounded=False` afterward, confirming the change made the judge less literal-minded without making it less rigorous.

**6. Manual-edit transcription slips during the guided walkthrough.** Twice, applying a multi-part diff by hand dropped part of the change: `fraud_eval.py` lost its `judge_grounding` import (a `NameError` at runtime caught it immediately), and separately the `judge.py` `INSTRUCTIONS` update for #5 simply never got saved (caught only because re-running the single-fixture diagnostic showed the *exact* phrase the new instructions were supposed to permit still being flagged, which shouldn't have been possible if the edit had landed). Neither was a plan defect — both are inherent risks of the "present a diff, human applies it by hand" execution model this project deliberately uses. What caught them both: verifying with a real, targeted diagnostic run instead of assuming a described edit took effect.

**7. Baseline tuning needed the real bugs fixed first, not just averaging noisy runs.** The very first real run (0.50/0.50) reflected the evidence-completeness bug in #3/#4, not actual agent quality — tuning a baseline against that number would have been tuning around a bug. Only after #3–#5 were fixed did repeated runs converge to a stable range (`~0.90` per agent, with the *specific* disagreeing fixture differing between consecutive runs — evidence of genuine per-call judge variance, not a remaining deterministic defect). Final baseline: **`0.80`** for all three agents (Task 10 Step 5 above has the full reasoning: with 5 fixtures each, one judge disagreement lands the mean at `0.90`, two land at `0.80` — both are normal outcomes of having a genuinely independent second judge, not failures).

**The general lesson underlying #3–#5**: a grounding judge is only as good as the `evidence_text` it's handed. Any eval runner that reconstructs a subset of an agent's real input for judging purposes risks silently narrowing what counts as "grounded," which shows up as *false* regressions (a correct, well-reasoned rationale scored as broken) rather than caught real ones. If this eval framework's pattern is reused for a future agent, `evidence_text` should be built by checking that agent's actual prompt-construction function directly, not assembled independently from memory of what the agent "should" need.

---

### Task 11: Regression-detection verification + roadmap update

**Files:**
- Modify (temporarily, then reverted): `src/claims_assistant/agents/coverage_agent.py`
- Modify: `docs/superpowers/plans/2026-08-10-roadmap.md`

**Interfaces:**
- Consumes: `tests/test_eval_suite.py` (Task 10).
- Produces: nothing new — this is the roadmap's own success-criteria check ("intentionally breaking a prompt drops a score below baseline and the harness flags it"), matching this project's established practice of proving a regression check actually catches the regression before trusting it (not just proving it passes on already-correct code).

- [ ] **Step 1: Weaken the Coverage Agent's grounding instructions**

In `src/claims_assistant/agents/coverage_agent.py`, temporarily change this line in `INSTRUCTIONS`:

```python
- Base your determination ONLY on the retrieved policy clauses provided. Do not use outside \
knowledge of insurance law or assume coverage that isn't stated in the clauses.
```

to:

```python
- Use your general knowledge of standard auto insurance practices to fill in any gaps in \
the retrieved policy clauses, even if the clauses provided don't fully support your answer.
```

- [ ] **Step 2: Re-run the eval suite and confirm it now fails**

Run: `uv run pytest tests/test_eval_suite.py -v -m integration -s`
Expected: FAIL — `coverage mean score ... dropped below baseline ...` (the grounding judge should now catch rationales that lean on unretrieved "general knowledge" instead of the actual cited clauses). If it does NOT fail, the grounding judge isn't actually discriminating — re-check `eval/judge.py`'s `INSTRUCTIONS` before trusting the harness, per this project's established practice of verifying a regression check actually fires before relying on it.

- [ ] **Step 3: Revert the instructions change**

Revert `src/claims_assistant/agents/coverage_agent.py`'s `INSTRUCTIONS` back to the original wording (Step 1's "before" text). Confirm with `git diff src/claims_assistant/agents/coverage_agent.py` that the file matches the last commit exactly, then discard the change:

```powershell
git checkout -- src/claims_assistant/agents/coverage_agent.py
```

- [ ] **Step 4: Re-run the eval suite to confirm it passes again**

Run: `uv run pytest tests/test_eval_suite.py -v -m integration -s`
Expected: PASS (1 passed) — confirms the revert was clean and the suite is back to its baseline-passing state.

- [ ] **Step 5: Update the roadmap**

In `docs/superpowers/plans/2026-08-10-roadmap.md`, check off Phase 8:

```markdown
- [x] Phase 8 — Eval framework
```

- [ ] **Step 6: Commit**

```powershell
git add docs/superpowers/plans/2026-08-10-roadmap.md
git commit -m "docs: check off Phase 8 in the roadmap"
```

---

## Definition of Done for Phase 8

- [ ] `uv run pytest -v -m "not integration"` passes with no external services needed (`test_pandas_setup.py`, `test_eval_fixtures.py`'s loader tests, `test_eval_report.py`, plus all prior phases' unit tests, unchanged).
- [ ] With real `AZURE_OPENAI_*`, `AZURE_SEARCH_*` values in `.env` (including this phase's two new judge deployment settings) and `docker-compose up -d postgres` running (seeded), `uv run pytest -v -m integration` passes — including this phase's `test_eval_judge.py` (3 tests), `test_extraction_eval_runner.py`, `test_coverage_eval_runner.py`, `test_fraud_eval_runner.py`, and `test_eval_suite.py`, plus all prior phases' integration tests (no regressions).
- [ ] `uv run pytest tests/test_eval_suite.py -v -m integration -s` (Task 10) prints a per-agent scored report and passes against the checked-in, real-run-tightened baselines in `src/claims_assistant/eval/baselines.py` (roadmap Phase 8 success criteria, part 1: "Running the eval suite locally produces a scored report").
- [ ] Task 11's regression demonstration was actually run and observed to fail before being reverted — not assumed (roadmap Phase 8 success criteria, part 2: "intentionally breaking a prompt drops a score below baseline and the harness flags it").
- [ ] Coverage and Fraud eval results are scored by two distinct judge models (`gpt-5.5` primary, `gpt-4.1` secondary) per spec §4's anti-self-preference-bias requirement, with disagreements surfaced in the printed report.
- [ ] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [ ] Roadmap doc's Phase 8 checkbox is checked off.
- [ ] Everything above is committed.

Once this is done, Phase 9 (Containerization & CI) is next — it wires this exact eval suite into GitHub Actions as the CI gate the roadmap describes, now that there's a real, working, baseline-gated harness to wire in.
