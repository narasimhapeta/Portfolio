# Phase 3: Extraction Agent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development and superpowers:executing-plans are the defaults for this template, but **this project overrides that**: execute via **guided walkthrough** — present each step's snippet + file path in chat, the human creates/edits the file and runs the test/command themselves and reports the result back; do not use Write/Edit/Bash to create or modify source files directly. Steps use checkbox (`- [ ]`) syntax for tracking progress across the walkthrough.

**Goal:** Build the first real agent in the pipeline — a Microsoft Agent Framework `Agent` that turns raw FNOL narrative text into schema-valid structured JSON: Phase 1's fixed `FNOLFacts` plus per-field extraction confidence — using few-shot prompting, and demonstrate it passes a first cut of Phase 1's extraction eval fixtures.

**Architecture:** A new `src/claims_assistant/agents/` subpackage. `extraction_schema.py` defines `FNOLExtraction` (`facts: FNOLFacts` nested + `confidence: FieldConfidence`, a *fixed*, enumerated set of per-field confidence floats — deliberately not a `dict[str, float]`, since OpenAI/Azure OpenAI structured-output "strict" mode requires a fixed, enumerated property set and does not support free-form/dynamic dict keys). `few_shot_examples.py` hand-authors four narrative+JSON example pairs covering the spec's four required categories (multi-vehicle pileup, hit-and-run, ambiguous fault, ambiguous injury) — **distinct from** the Phase 1 eval fixtures (which are deliberately held-out variants of these same categories per spec §6, so reusing them as prompt examples would contaminate the eval). `extraction_agent.py` builds an `Agent` wrapping `OpenAIChatCompletionClient` (Azure OpenAI routing, confirmed against the installed SDK below) with the few-shot block folded into its system instructions, and exposes `extract_fnol_facts()` which requests `FNOLExtraction` as structured `response_format` and returns the parsed, Pydantic-validated result. `extraction_scoring.py` adds a small first-cut field-level scorer (exact/near-exact match on the fields that have one correct answer — `incident_datetime`, `location`, party/vehicle roles, `injuries`) used to check the agent against all 10 Phase 1 fixtures; this is intentionally lightweight — the full deterministic/LLM-judge eval harness with checked-in baselines and CI gating is Phase 8's job, not this phase's.

**Tech Stack:** `agent-framework-core` + `agent-framework-openai` (Microsoft Agent Framework's Python SDK — see the confirmed-API note in Global Constraints), on top of Phase 1's `FNOLFacts`/`eval_fixtures.py`, Pydantic v2, pytest + pytest-asyncio (`integration` marker, reused here for tests that need real Azure OpenAI credentials, the same way Phase 1/2 reused it for tests that need Postgres).

**Spec:** [docs/superpowers/specs/2026-08-10-auto-claims-assistant-design.md](../specs/2026-08-10-auto-claims-assistant-design.md) (§3.1 Extraction Agent, §4 model tiering, §5.4 FNOL extraction schema, §6 extraction eval)

## Global Constraints

- Python 3.12, src-layout under `src/claims_assistant/` (per Phase 0).
- All I/O-bound functions (LLM calls) are `async def` (per Phase 0's async I/O constraint).
- `FNOLFacts` (Phase 1's `fnol_schema.py`) is the fixed, unmodified ground-truth schema used by eval fixture gold JSON — this phase does not add confidence fields to it. Confidence lives in a separate wrapper (`FNOLExtraction`), per Phase 1's own documented design decision (see the Phase 1 plan's Architecture note).
- Every dependency addition goes through `uv add`.
- Every task ends with the relevant tests passing (and `uv run ruff check .` / `uv run mypy src` clean for any touched source files) before moving to the next task.
- Tests that make real LLM calls are `pytest.mark.integration` and require a filled-in `.env` with `AZURE_OPENAI_*` values (this phase's equivalent of Phase 1/2's "requires `docker-compose up -d postgres`" precondition) — no new pytest marker needed, `integration` already means "requires external services."
- **Confirmed against the actually-installed SDK.** Originally verified against `agent-framework-core==1.13.0`/`agent-framework-openai==1.12.0` in a scratch venv while writing this plan; re-verified against `agent-framework-core==1.14.0`/`agent-framework-openai==1.13.0` (what `uv add` actually resolved to in Task 1, Step 1) directly in this project's `.venv` — every claim below held unchanged across both versions. Trained/web knowledge of this SDK is unreliable, same lesson as Phase 2's `mcp` surprise:
  - The agent class is **`agent_framework.Agent`** (constructor: `Agent(client, instructions=None, *, name=None, tools=None, ...)`), **not** `ChatAgent` — older blog posts/docs referencing `ChatAgent` describe a stale API.
  - Structured output: pass `options=agent_framework.ChatOptions(response_format=SomeBaseModel)` to `Agent.run()`. `ChatOptions` is a `TypedDict` (plain dict at runtime, not a Pydantic model) so `ChatOptions(response_format=X)` just builds `{"response_format": X}`.
  - `Agent.run(messages, *, options=None, ...)` returns (when `stream=False`, the default) an `AgentResponse[Any]`. `response.value` lazily parses the last assistant message's text against `response_format` and returns a validated instance of that Pydantic model (raises `pydantic.ValidationError` on a schema mismatch) — this is the mechanism `extract_fnol_facts()` relies on.
  - **The `instructions` set on `Agent(...)` at construction time survive a per-call `agent.run(text, options=ChatOptions(response_format=...))`** — `options` passed to `run()` is merged with, not substituted for, the agent's `default_options`, and `instructions` specifically are concatenated rather than dropped. This is what lets `extract_fnol_facts()` pass only `response_format` at call time while the few-shot prompt baked into `build_extraction_agent()`'s `Agent(instructions=...)` still reaches the model. Verified by executing a real merged call and inspecting the outgoing request; the actual merge logic lives in `agent_framework/_agents.py` (a private helper called from `Agent.run`'s request-building path) — `agent_framework/_types.py` separately exposes a public `merge_chat_options()` with equivalent documented behavior, but that public function is not what `Agent.run()` itself calls, so re-verification should look at `_agents.py`, not just `_types.py`.
  - **Do not add `agent-framework-azure-ai`** — that package (v1.0.0rc6) targets Azure AI Foundry's separate hosted *Agents Service* and, as installed today, is version-incompatible with `agent-framework-core==1.13.0` (`ImportError: cannot import name 'BaseContextProvider'`). It is also the wrong tool for this phase: Phase 3 only needs single-turn structured-output chat calls.
  - Use **`agent_framework.openai.OpenAIChatCompletionClient`** (from the `agent-framework-openai` package), not `OpenAIChatClient` (a different class in the same module that talks to OpenAI's newer "Responses" API — its Azure default API version is literally `"preview"`, which is less predictable than the Chat Completions client's dated default). `OpenAIChatCompletionClient` natively supports Azure OpenAI routing: pass `azure_endpoint=`, `api_key=`, `api_version=`, `model=` (in Azure OpenAI, `model` means your **deployment name**, not the underlying model family name) explicitly — this project passes them explicitly from `Settings` rather than relying on the client's own `AZURE_OPENAI_*` env-var auto-detection, to stay consistent with the rest of the codebase's `pydantic-settings`-centralized config pattern. Its built-in default `api_version` (used only if you don't pass one) is `"2024-12-01-preview"`.
  - If a future `uv sync` pulls different versions and something below breaks, re-run this same inspection (`uv pip show agent-framework-core agent-framework-openai`, read `agent_framework/_types.py`'s `AgentResponse`/`ChatOptions`, `agent_framework/_agents.py`'s options-merging in `Agent.run`, and `agent_framework_openai/_chat_completion_client.py`) rather than guessing.

---

### Task 1: Agent Framework dependency, Azure OpenAI config, `agents` subpackage scaffold

**Files:**
- Modify: `pyproject.toml`, `uv.lock` (via `uv add`)
- Modify: `src/claims_assistant/config.py`
- Modify: `.env.example`
- Modify: `tests/test_config.py`
- Create: `src/claims_assistant/agents/__init__.py`
- Test: `tests/test_agent_framework_setup.py`

**Interfaces:**
- Consumes: nothing new (first task of the phase).
- Produces: `agent_framework.Agent`, `agent_framework.ChatOptions`, `agent_framework.openai.OpenAIChatCompletionClient` available for import; `Settings.azure_openai_endpoint`, `.azure_openai_api_key`, `.azure_openai_chat_deployment`, `.azure_openai_api_version: str` fields; the `claims_assistant.agents` subpackage that Tasks 2–4 add modules to.

- [x] **Step 1: Add the Agent Framework dependencies**

Run (PowerShell):
```powershell
uv add agent-framework-core agent-framework-openai
```

- [x] **Step 2: Extend the config test for the Azure OpenAI settings**

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
    monkeypatch.setenv("AZURE_OPENAI_ENDPOINT", "https://example.openai.azure.com")
    monkeypatch.setenv("AZURE_OPENAI_API_KEY", "test-key")
    monkeypatch.setenv("AZURE_OPENAI_CHAT_DEPLOYMENT", "test-deployment")
    monkeypatch.setenv("AZURE_OPENAI_API_VERSION", "2024-12-01-preview")

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
    assert settings.azure_openai_endpoint == "https://example.openai.azure.com"
    assert settings.azure_openai_api_key == "test-key"
    assert settings.azure_openai_chat_deployment == "test-deployment"
    assert settings.azure_openai_api_version == "2024-12-01-preview"


def test_get_settings_is_cached():
    assert get_settings() is get_settings()
```

- [x] **Step 3: Run the test to verify it fails**

Run: `uv run pytest tests/test_config.py -v`
Expected: FAIL — `AttributeError: 'Settings' object has no attribute 'azure_openai_endpoint'`

- [x] **Step 4: Add the Azure OpenAI settings fields**

In `src/claims_assistant/config.py`, add these fields to the `Settings` class (after the existing `postgres_password` field):

```python
    azure_openai_endpoint: str = ""
    azure_openai_api_key: str = ""
    azure_openai_chat_deployment: str = ""
    azure_openai_api_version: str = "2024-12-01-preview"
```

- [x] **Step 5: Run the test to verify it passes**

Run: `uv run pytest tests/test_config.py -v`
Expected: PASS (2 passed)

- [x] **Step 6: Document the new env vars**

Add to `.env.example` (and your own `.env`, with your real Azure OpenAI values):

```env
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_API_KEY=your-azure-openai-key
AZURE_OPENAI_CHAT_DEPLOYMENT=your-gpt-5-mini-deployment-name
AZURE_OPENAI_API_VERSION=2024-12-01-preview
```

- [x] **Step 7: Create the subpackage**

Create `src/claims_assistant/agents/__init__.py` (empty file).

- [x] **Step 8: Write a smoke test (no network call)**

```python
# tests/test_agent_framework_setup.py
from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient


def test_openai_chat_completion_client_constructs_without_network_call():
    client = OpenAIChatCompletionClient(
        model="test-deployment",
        azure_endpoint="https://example.openai.azure.com",
        api_key="test-key",
        api_version="2024-12-01-preview",
    )

    assert client.azure_endpoint == "https://example.openai.azure.com"


def test_agent_constructs_around_a_client():
    client = OpenAIChatCompletionClient(
        model="test-deployment",
        azure_endpoint="https://example.openai.azure.com",
        api_key="test-key",
    )

    agent = Agent(client=client, instructions="You are a test agent.")

    # Agent has no public `.instructions` attribute — the constructor folds it into
    # `default_options["instructions"]`, which is what actually gets sent per-call.
    assert agent.default_options["instructions"] == "You are a test agent."


def test_chat_options_is_a_plain_dict_with_response_format():
    options = ChatOptions(response_format=dict)

    assert options == {"response_format": dict}
```

- [x] **Step 9: Run the test to verify it passes**

Run: `uv run pytest tests/test_agent_framework_setup.py -v`
Expected: PASS (3 passed) — no network call is made; these only exercise object construction.

- [x] **Step 10: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 11: Commit**

```powershell
git add pyproject.toml uv.lock src/claims_assistant/config.py src/claims_assistant/agents/__init__.py .env.example tests/test_config.py tests/test_agent_framework_setup.py
git commit -m "feat: add Agent Framework dependency and Azure OpenAI config"
```

---

### Task 2: `FNOLExtraction` schema + few-shot examples

**Files:**
- Create: `src/claims_assistant/agents/extraction_schema.py`
- Create: `src/claims_assistant/agents/few_shot_examples.py`
- Test: `tests/test_extraction_schema.py`
- Test: `tests/test_few_shot_examples.py`

**Interfaces:**
- Consumes: `FNOLFacts`, `Party`, `VehicleInfo` (Phase 1's `fnol_schema.py`).
- Produces: `extraction_schema.py`'s `FieldConfidence` and `FNOLExtraction` (`facts: FNOLFacts`, `confidence: FieldConfidence`) Pydantic models. `few_shot_examples.py`'s `FEW_SHOT_EXAMPLES: list[tuple[str, FNOLExtraction]]` and `render_few_shot_block() -> str`. Task 3's `extraction_agent.py` imports all four names.

- [x] **Step 1: Write the failing schema tests**

```python
# tests/test_extraction_schema.py
import pytest
from pydantic import ValidationError

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def _sample_facts() -> FNOLFacts:
    return FNOLFacts(
        incident_datetime="2026-02-03T21:00",
        location="4th Ave, Brooklyn, NY",
        parties=[Party(role="policyholder", name="Linda Park")],
        vehicles=[VehicleInfo(role="policyholder_vehicle", description="Toyota Corolla")],
        injuries=False,
        narrative_summary="Parked car struck by a hit-and-run driver.",
    )


def test_fnol_extraction_validates_with_facts_and_confidence():
    extraction = FNOLExtraction(
        facts=_sample_facts(),
        confidence=FieldConfidence(
            incident_datetime=0.9,
            location=0.85,
            parties=0.8,
            vehicles=0.8,
            injuries=0.95,
            narrative_summary=0.9,
        ),
    )

    assert extraction.facts.location == "4th Ave, Brooklyn, NY"
    assert extraction.confidence.injuries == 0.95


def test_field_confidence_rejects_out_of_range_values():
    with pytest.raises(ValidationError):
        FieldConfidence(
            incident_datetime=1.5,
            location=0.5,
            parties=0.5,
            vehicles=0.5,
            injuries=0.5,
            narrative_summary=0.5,
        )
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_extraction_schema.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.extraction_schema'`

- [x] **Step 3: Write the schema**

```python
# src/claims_assistant/agents/extraction_schema.py
from __future__ import annotations

from pydantic import BaseModel, Field

from claims_assistant.fnol_schema import FNOLFacts


class FieldConfidence(BaseModel):
    """Per-field extraction confidence, one score per top-level FNOLFacts group (spec §5.4)."""

    incident_datetime: float = Field(ge=0.0, le=1.0)
    location: float = Field(ge=0.0, le=1.0)
    parties: float = Field(ge=0.0, le=1.0)
    vehicles: float = Field(ge=0.0, le=1.0)
    injuries: float = Field(ge=0.0, le=1.0)
    narrative_summary: float = Field(ge=0.0, le=1.0)


class FNOLExtraction(BaseModel):
    facts: FNOLFacts
    confidence: FieldConfidence
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_extraction_schema.py -v`
Expected: PASS (2 passed)

- [x] **Step 5: Write the failing few-shot examples test**

```python
# tests/test_few_shot_examples.py
from claims_assistant.agents.few_shot_examples import FEW_SHOT_EXAMPLES, render_few_shot_block


def test_four_few_shot_examples_are_defined():
    assert len(FEW_SHOT_EXAMPLES) == 4


def test_few_shot_examples_cover_the_required_categories():
    narratives = " ".join(narrative.lower() for narrative, _ in FEW_SHOT_EXAMPLES)

    assert "box truck" in narratives  # multi-vehicle pileup
    assert (
        "sped off" in narratives or "fled" in narratives or "speeding away" in narratives
    )  # hit-and-run, no other party
    assert "right of way" in narratives  # ambiguous fault language
    assert "sore" in narratives  # ambiguous injury mention


def test_render_few_shot_block_includes_every_example_narrative():
    block = render_few_shot_block()

    for narrative, _ in FEW_SHOT_EXAMPLES:
        assert narrative in block


def test_render_few_shot_block_includes_expected_json_output():
    block = render_few_shot_block()

    assert '"role": "policyholder"' in block
    assert '"confidence"' in block
```

- [x] **Step 6: Run the test to verify it fails**

Run: `uv run pytest tests/test_few_shot_examples.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.few_shot_examples'`

- [x] **Step 7: Write the few-shot examples**

```python
# src/claims_assistant/agents/few_shot_examples.py
from __future__ import annotations

from claims_assistant.agents.extraction_schema import FieldConfidence, FNOLExtraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo

# Distinct from the Phase 1 eval fixtures (data/eval_fixtures/extraction/) on purpose —
# those are held-out variants of these same categories (spec §6), used to test
# generalization, not to be echoed back in the prompt.

_MULTI_VEHICLE_PILEUP = (
    "On March 12, 2026 around 7:45 AM, I (Marcus Webb) was driving my Subaru Outback "
    "on I-95 in Providence, RI during heavy fog when the car ahead of me braked "
    "suddenly. I couldn't stop in time and hit it, and then a box truck behind me hit "
    "my rear bumper, pushing me further into the car ahead. Three vehicles total were "
    "involved. The driver of the car I hit, Priya Nair, said her neck hurt but she "
    "could move it fine. The box truck driver, Sam Ostrowski, seemed unhurt. No one "
    "else was injured.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-03-12T07:45",
            location="I-95, Providence, RI",
            parties=[
                Party(role="policyholder", name="Marcus Webb"),
                Party(role="other_driver", name="Priya Nair"),
                Party(role="other_driver", name="Sam Ostrowski"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Subaru Outback"),
                VehicleInfo(
                    role="other_vehicle",
                    description="car driven by Priya Nair, struck from behind by policyholder",
                ),
                VehicleInfo(
                    role="other_vehicle",
                    description=(
                        "box truck driven by Sam Ostrowski, struck policyholder's vehicle "
                        "from behind"
                    ),
                ),
            ],
            injuries=True,
            injury_description=(
                "Priya Nair reported neck pain but retained range of motion; Sam Ostrowski "
                "and the policyholder were not injured."
            ),
            narrative_summary=(
                "Three-vehicle chain-reaction collision on I-95 in Providence, RI during "
                "heavy fog; policyholder's Subaru Outback struck the vehicle ahead after it "
                "braked suddenly, then was struck from behind by a box truck. One other "
                "driver reported minor neck pain."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.85,
            vehicles=0.85,
            injuries=0.8,
            narrative_summary=0.9,
        ),
    ),
)

_HIT_AND_RUN = (
    "On April 2, 2026 at about 6:30 AM, I (Denise Ochoa) was driving my Kia Sportage "
    "on Route 9 in Poughkeepsie, NY when an SUV I couldn't identify merged into my "
    "lane and clipped my front fender before speeding away. I didn't get a plate "
    "number and there were no witnesses nearby. I wasn't hurt.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-04-02T06:30",
            location="Route 9, Poughkeepsie, NY",
            parties=[Party(role="policyholder", name="Denise Ochoa")],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Kia Sportage"),
                VehicleInfo(
                    role="other_vehicle",
                    description="unidentified SUV, fled scene, no plate captured",
                ),
            ],
            injuries=False,
            narrative_summary=(
                "Hit-and-run sideswipe on Route 9, Poughkeepsie, NY; an unidentified SUV "
                "merged into the policyholder's lane, clipped the front fender, and sped off "
                "without a plate number captured. No witnesses, no injuries."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.9,
            vehicles=0.75,
            injuries=0.95,
            narrative_summary=0.9,
        ),
    ),
)

_AMBIGUOUS_FAULT = (
    "On May 18, 2026 around 4:00 PM, I (Grant Okafor) was merging onto the ramp for "
    "Highway 101 near San Jose, CA when my Mazda CX-5 collided with a Chevy Malibu "
    "driven by Renee Castillo. We're not totally sure who had the right of way — I "
    "thought I had space to merge, but Renee says she was already in the lane. "
    "There's damage to both cars' sides. No injuries reported by either of us.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-05-18T16:00",
            location="Highway 101 on-ramp, San Jose, CA",
            parties=[
                Party(role="policyholder", name="Grant Okafor"),
                Party(role="other_driver", name="Renee Castillo"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Mazda CX-5"),
                VehicleInfo(
                    role="other_vehicle", description="Chevy Malibu driven by Renee Castillo"
                ),
            ],
            injuries=False,
            narrative_summary=(
                "Side-swipe collision while merging onto the Highway 101 on-ramp near San "
                "Jose, CA; fault is disputed, with both the policyholder and the other "
                "driver believing they had the right of way. No injuries reported."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.9,
            location=0.85,
            parties=0.9,
            vehicles=0.9,
            injuries=0.9,
            narrative_summary=0.7,
        ),
    ),
)

_AMBIGUOUS_INJURY = (
    "On June 30, 2026 around 2:10 PM, I (Yuki Tanaka) was stopped in traffic on "
    "Peachtree St in Atlanta, GA when a Ford Escape driven by Cody Lindgren bumped "
    "into the back of my Nissan Altima. It was a light tap, no visible damage, but I "
    "felt a little sore in my lower back afterward. I'm not sure if it's from the "
    "accident or just from sitting in the car all day. I haven't seen a doctor.",
    FNOLExtraction(
        facts=FNOLFacts(
            incident_datetime="2026-06-30T14:10",
            location="Peachtree St, Atlanta, GA",
            parties=[
                Party(role="policyholder", name="Yuki Tanaka"),
                Party(role="other_driver", name="Cody Lindgren"),
            ],
            vehicles=[
                VehicleInfo(role="policyholder_vehicle", description="Nissan Altima"),
                VehicleInfo(
                    role="other_vehicle", description="Ford Escape driven by Cody Lindgren"
                ),
            ],
            injuries=True,
            injury_description=(
                "Policyholder reported mild lower-back soreness after the collision but was "
                "uncertain whether it was related to the accident; no medical care sought."
            ),
            narrative_summary=(
                "Minor rear-end tap on Peachtree St, Atlanta, GA with no visible vehicle "
                "damage; policyholder reports uncertain, mild lower-back soreness not yet "
                "evaluated by a doctor."
            ),
        ),
        confidence=FieldConfidence(
            incident_datetime=0.95,
            location=0.9,
            parties=0.9,
            vehicles=0.9,
            injuries=0.55,
            narrative_summary=0.85,
        ),
    ),
)

FEW_SHOT_EXAMPLES: list[tuple[str, FNOLExtraction]] = [
    _MULTI_VEHICLE_PILEUP,
    _HIT_AND_RUN,
    _AMBIGUOUS_FAULT,
    _AMBIGUOUS_INJURY,
]


def render_few_shot_block() -> str:
    sections = []
    for i, (narrative, extraction) in enumerate(FEW_SHOT_EXAMPLES, start=1):
        sections.append(
            f"Example {i}:\nFNOL Report:\n{narrative}\n\n"
            f"Extracted JSON:\n{extraction.model_dump_json(indent=2)}"
        )
    return "\n\n".join(sections)
```

- [x] **Step 8: Run the tests to verify they pass**

Run: `uv run pytest tests/test_few_shot_examples.py -v`
Expected: PASS (4 passed)

- [x] **Step 9: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 10: Commit**

```powershell
git add src/claims_assistant/agents/extraction_schema.py src/claims_assistant/agents/few_shot_examples.py tests/test_extraction_schema.py tests/test_few_shot_examples.py
git commit -m "feat: add FNOLExtraction schema and few-shot examples"
```

---

### Task 3: Extraction agent

**Files:**
- Create: `src/claims_assistant/agents/extraction_agent.py`
- Test: `tests/test_extraction_agent.py`

**Interfaces:**
- Consumes: `Settings`, `get_settings()` (Task 1's `config.py`); `Agent`, `ChatOptions` (`agent_framework`); `OpenAIChatCompletionClient` (`agent_framework.openai`); `FNOLExtraction` (Task 2's `extraction_schema.py`); `FEW_SHOT_EXAMPLES`, `render_few_shot_block()` (Task 2's `few_shot_examples.py`).
- Produces: `build_chat_client(settings: Settings) -> OpenAIChatCompletionClient`, `build_extraction_agent(settings: Settings) -> Agent`, `async def extract_fnol_facts(agent: Agent, narrative_text: str) -> FNOLExtraction`. Task 4 imports `build_extraction_agent` and `extract_fnol_facts`.

- [x] **Step 1: Write the failing integration test**

This test needs real Azure OpenAI credentials in `.env` (Task 1, Step 6).

```python
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
```

- [x] **Step 2: Run the test to verify it fails**

Run: `uv run pytest tests/test_extraction_agent.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.extraction_agent'`

- [x] **Step 3: Write the extraction agent**

```python
# src/claims_assistant/agents/extraction_agent.py
from __future__ import annotations

from agent_framework import Agent, ChatOptions
from agent_framework.openai import OpenAIChatCompletionClient

from claims_assistant.agents.extraction_schema import FNOLExtraction
from claims_assistant.agents.few_shot_examples import render_few_shot_block
from claims_assistant.config import Settings

INSTRUCTIONS_TEMPLATE = """\
You are an insurance claims intake specialist. You convert a First Notice of Loss \
(FNOL) narrative — a policyholder's own description of an accident — into structured \
JSON matching the required schema exactly.

Rules:
- Extract only what the narrative states or clearly implies. Do not invent names, \
VINs, or details that are not present.
- If a VIN is not mentioned for a vehicle, leave it null.
- "injuries" is true if any injury, however minor or uncertain, is mentioned for \
anyone involved; injury_description should summarize what was said, including any \
uncertainty the narrator expressed.
- Assign each of the six confidence fields a score from 0.0 to 1.0 reflecting how \
directly the source narrative supports that field. Vague, hedged, or inferred \
information should get a lower score than information stated plainly. For example, \
"I felt a little sore, not sure if it's from the accident" should produce a lower \
injuries confidence than "the paramedics confirmed I broke my arm."

Here are examples of narratives and their correct extractions:

{few_shot_block}

Now extract the following FNOL report into the same JSON structure.
"""


def build_chat_client(settings: Settings) -> OpenAIChatCompletionClient:
    return OpenAIChatCompletionClient(
        model=settings.azure_openai_chat_deployment,
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


def build_extraction_agent(settings: Settings) -> Agent:
    client = build_chat_client(settings)
    instructions = INSTRUCTIONS_TEMPLATE.format(few_shot_block=render_few_shot_block())
    return Agent(client=client, instructions=instructions)


async def extract_fnol_facts(agent: Agent, narrative_text: str) -> FNOLExtraction:
    response = await agent.run(
        narrative_text, options=ChatOptions(response_format=FNOLExtraction)
    )
    value = response.value
    assert isinstance(value, FNOLExtraction)
    return value
```

- [x] **Step 4: Run the test to verify it passes**

Run: `uv run pytest tests/test_extraction_agent.py -v`
Expected: PASS (1 passed). If it fails with an authentication or 404 error, double-check `.env`'s `AZURE_OPENAI_ENDPOINT`/`AZURE_OPENAI_CHAT_DEPLOYMENT` match your actual Azure AI Foundry resource and deployment name.

- [x] **Step 5: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 6: Commit**

```powershell
git add src/claims_assistant/agents/extraction_agent.py tests/test_extraction_agent.py
git commit -m "feat: add extraction agent producing schema-valid FNOLExtraction JSON"
```

---

### Task 4: First-cut scoring against Phase 1 eval fixtures

**Files:**
- Create: `src/claims_assistant/agents/extraction_scoring.py`
- Test: `tests/test_extraction_scoring.py`
- Test: `tests/test_extraction_eval_fixtures.py`

**Interfaces:**
- Consumes: `FNOLFacts` (Phase 1's `fnol_schema.py`); `load_extraction_fixtures()`, `ExtractionFixture` (Phase 1's `eval_fixtures.py`); `build_extraction_agent`, `extract_fnol_facts` (Task 3's `extraction_agent.py`); `get_settings()` (Task 1's `config.py`).
- Produces: `extraction_scoring.py`'s `score_extraction(predicted: FNOLFacts, gold: FNOLFacts) -> float`. Not consumed by later phases in this plan — this is this phase's own first-cut check; Phase 8 builds the real, checked-in-baseline eval harness.

- [x] **Step 1: Write the failing scoring tests**

```python
# tests/test_extraction_scoring.py
from claims_assistant.agents.extraction_scoring import score_extraction
from claims_assistant.fnol_schema import FNOLFacts, Party, VehicleInfo


def _facts(**overrides: object) -> FNOLFacts:
    defaults: dict[str, object] = {
        "incident_datetime": "2026-02-03T21:00",
        "location": "4th Ave, Brooklyn, NY",
        "parties": [Party(role="policyholder", name="Linda Park")],
        "vehicles": [VehicleInfo(role="policyholder_vehicle", description="Toyota Corolla")],
        "injuries": False,
        "narrative_summary": "Parked car struck by a hit-and-run driver.",
    }
    defaults.update(overrides)
    return FNOLFacts(**defaults)  # type: ignore[arg-type]


def test_identical_facts_score_one():
    facts = _facts()

    assert score_extraction(facts, facts) == 1.0


def test_location_match_is_case_insensitive():
    predicted = _facts(location="4TH AVE, BROOKLYN, NY")
    gold = _facts(location="4th Ave, Brooklyn, NY")

    assert score_extraction(predicted, gold) == 1.0


def test_mismatched_injuries_lowers_score():
    predicted = _facts(injuries=True, injury_description="minor bruise")
    gold = _facts(injuries=False)

    assert score_extraction(predicted, gold) == 0.8  # 4 of 5 checks match


def test_completely_different_facts_score_zero():
    predicted = _facts(
        incident_datetime="2025-01-01T00:00",
        location="Nowhere",
        parties=[Party(role="other_driver", name="Nobody")],
        vehicles=[VehicleInfo(role="other_vehicle", description="unknown")],
        injuries=True,
    )
    gold = _facts()

    assert score_extraction(predicted, gold) == 0.0
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `uv run pytest tests/test_extraction_scoring.py -v`
Expected: FAIL — `ModuleNotFoundError: No module named 'claims_assistant.agents.extraction_scoring'`

- [x] **Step 3: Write the scorer**

```python
# src/claims_assistant/agents/extraction_scoring.py
from __future__ import annotations

from collections import Counter

from claims_assistant.fnol_schema import FNOLFacts

# First-cut, deterministic field-level scorer for this phase's own sanity check.
# narrative_summary is intentionally excluded: it's a generated summary, not a
# single-correct-answer extracted fact. Phase 8 owns the real fuzzy/LLM-judge scorer
# with a checked-in baseline and CI gating (spec §6).
#
# Party/vehicle roles are compared as multisets (Counter), not sets: the
# multi-vehicle-pileup category (spec §5.4) can have two+ parties or vehicles with the
# same role (e.g. two "other_driver" parties), and a set comparison would treat
# dropping one of them as a full match.


def score_extraction(predicted: FNOLFacts, gold: FNOLFacts) -> float:
    checks = [
        predicted.incident_datetime == gold.incident_datetime,
        predicted.location.strip().lower() == gold.location.strip().lower(),
        Counter(p.role for p in predicted.parties) == Counter(p.role for p in gold.parties),
        Counter(v.role for v in predicted.vehicles) == Counter(v.role for v in gold.vehicles),
        predicted.injuries == gold.injuries,
    ]
    return sum(checks) / len(checks)
```

- [x] **Step 4: Run the tests to verify they pass**

Run: `uv run pytest tests/test_extraction_scoring.py -v`
Expected: PASS (4 passed)

- [x] **Step 5: Write the eval-fixtures integration test**

Unlike the other tests in this plan, this one has no new production code to write first —
it's a system-level check that exercises Task 3's agent and this task's scorer together
against real fixtures, so there's nothing to watch fail before implementing; it should
run for real the first time. This test needs real Azure OpenAI credentials in `.env` and
makes 10 real LLM calls (one per Phase 1 fixture).

```python
# tests/test_extraction_eval_fixtures.py
import pytest

from claims_assistant.agents.extraction_agent import build_extraction_agent, extract_fnol_facts
from claims_assistant.agents.extraction_scoring import score_extraction
from claims_assistant.config import get_settings
from claims_assistant.eval_fixtures import load_extraction_fixtures

pytestmark = pytest.mark.integration

FIRST_CUT_SCORE_FLOOR = 0.7


@pytest.mark.asyncio
async def test_extraction_passes_first_cut_of_eval_fixtures():
    agent = build_extraction_agent(get_settings())
    fixtures = load_extraction_fixtures()

    results = []
    for fixture in fixtures:
        extraction = await extract_fnol_facts(agent, fixture.narrative_text)
        score = score_extraction(extraction.facts, fixture.gold)
        results.append((fixture.fixture_id, score))

    mean_score = sum(score for _, score in results) / len(results)

    assert mean_score >= FIRST_CUT_SCORE_FLOOR, (
        f"mean extraction score {mean_score:.2f} below first-cut floor "
        f"{FIRST_CUT_SCORE_FLOOR}; per-fixture scores={results}"
    )
```

- [x] **Step 6: Run the test**

Run: `uv run pytest tests/test_extraction_eval_fixtures.py -v`
Expected: PASS (1 passed). If it fails because `mean_score` is below `0.7`, read the printed per-fixture scores, open the lowest-scoring fixture's `.txt`/`.json` under `data/eval_fixtures/extraction/`, and compare it against what the agent actually returned (add a temporary `print(extraction)` in the loop, or inspect via a debugger) — this is real signal about the prompt/instructions, not a fixture problem, since the fixtures were hand-authored as ground truth in Phase 1.

- [x] **Step 7: Lint and type-check**

Run: `uv run ruff check .` and `uv run mypy src`
Expected: both clean.

- [x] **Step 8: Commit**

```powershell
git add src/claims_assistant/agents/extraction_scoring.py tests/test_extraction_scoring.py tests/test_extraction_eval_fixtures.py
git commit -m "feat: add first-cut extraction scoring against Phase 1 eval fixtures"
```

---

## Definition of Done for Phase 3

- [x] `uv run pytest -v -m "not integration"` passes with no Azure OpenAI credentials needed (config, schema, few-shot, and scoring unit tests).
- [x] With real `AZURE_OPENAI_*` values in `.env`, `uv run pytest -v -m integration` passes — including `test_extraction_agent.py` and `test_extraction_eval_fixtures.py`, plus all prior phases' integration tests.
- [x] `uv run ruff check .` and `uv run mypy src` both pass clean.
- [x] Roadmap doc's Phase 3 checkbox is checked off.
- [x] Everything above is committed.

Once this is done, update [the roadmap](2026-08-10-roadmap.md) status and we write the Phase 4 (Azure AI Search indexing + Coverage Agent) plan next — it's independent of this phase's agent (per the roadmap's dependency notes) but will reuse this phase's `agent_framework` wiring patterns (`Agent`, `OpenAIChatCompletionClient`, `ChatOptions(response_format=...)`) and will need its own live-inspection check of Azure AI Search's Python SDK before locking in indexing/retrieval code.

**Notes from execution:** `uv add` resolved `agent-framework-core==1.14.0`/`agent-framework-openai==1.13.0` (one patch ahead of the `1.13.0`/`1.12.0` verified while writing this plan) — re-verified every Global Constraints claim directly against the newer version in this project's `.venv` before proceeding; all held unchanged, no plan corrections needed. Provisioning the Azure OpenAI resource surfaced two real-world details worth carrying into Phase 4/5 (which will also need Azure OpenAI/Azure AI Search wiring): (1) the model catalog had already moved past the spec's GPT-5-mini example to `gpt-5.4-mini` (2026-03-17) — deployed that as the closest current match to the spec's "mini"-tier intent for a high-volume task; (2) newer models like `gpt-5.4-mini` only support the `GlobalStandard` deployment SKU, not the older `Standard` SKU — `az cognitiveservices account list-models ... --query "[?name=='<model>']"` shows the valid `skus` for a given model before deploying. `api_version="2024-12-01-preview"` (this plan's/the SDK's default) worked without changes against `gpt-5.4-mini` via the classic Azure OpenAI Chat Completions path (`AsyncAzureOpenAI` with a dated `api-version`) — Azure's newer "v1 API" (dropping dated `api-version` entirely, `base_url=".../openai/v1/"`) exists but isn't what `agent-framework-openai` uses today, so it wasn't needed here. Full test suite after Task 4: 27 passed (`-m "not integration"`), 21 passed (`-m integration`, needs `docker-compose up -d postgres` + real `AZURE_OPENAI_*` in `.env`) — no regressions across Phases 0–3.
