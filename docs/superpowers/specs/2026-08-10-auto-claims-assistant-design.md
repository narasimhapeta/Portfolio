# AI-Powered Auto Insurance Claims Assistant — Design Spec

**Date:** 2026-08-10
**Status:** Approved for planning
**Purpose:** Portfolio/capstone project demonstrating production-grade agentic AI engineering on the Microsoft stack (Python/FastAPI, Microsoft Agent Framework, Azure AI Foundry, Azure AI Search, MCP, Azure Container Apps, GitHub Actions), built and evaluated the way enterprise teams do: architect → design → build → eval → test → deploy.

## 1. Goals & Non-Goals

**Goals:**
- Demonstrate a real, working multi-agent claims-intake pipeline: FNOL entity extraction, policy coverage determination grounded in real documents (RAG), fraud-risk scoring, and adjuster-facing summarization — coordinated by a supervisor agent using Microsoft Agent Framework's graph-based workflow engine.
- Demonstrate MCP as the mechanism agents use to reach real systems (policy DB, claims history, VIN/vehicle-value) instead of hallucinating facts.
- Demonstrate an eval-gated CI/CD pipeline: an LLM-as-judge + deterministic eval suite that blocks prompt/model regressions before they reach production, with canary rollout.
- Produce a portfolio-credible, cost-conscious real deployment on Azure (not a local-only simulation).

**Non-Goals (explicitly out of scope for this spec):**
- Real customer data or real compliance certification (HIPAA/SOC2/state insurance regulatory approval). This is a demonstration system using synthetic data only.
- Autonomous final claims decisions. The system always produces a recommendation for a human adjuster; it never auto-approves or auto-denies a claim.
- A user-facing web or chat UI. Interaction surface is the API itself (FastAPI + OpenAPI/Swagger docs), consumed via Postman/HTTP client for demos.
- Literal AKS usage. Azure Container Apps is used as the containerized-compute layer; the AKS-vs-ACA tradeoff is documented (Section 7) so it can be discussed accurately rather than misrepresented on a resume.
- ML-trained fraud model. Fraud-risk scoring is LLM reasoning over real signals (via MCP), not a trained classifier — see Section 4 for rationale.

## 2. Success Criteria

- A submitted synthetic FNOL report flows end-to-end through extraction → (coverage + fraud in parallel) → adjuster summary, returning a structured, cited recommendation via the API.
- Low-confidence extractions are routed to a clarification/escalation path instead of silently propagating bad data (demonstrates the "handoff" orchestration mode, not just "sequential").
- The eval suite runs in CI on every change and fails the build if extraction accuracy, coverage-grounding, or fraud-rationale-grounding scores drop below a checked-in baseline.
- The system is deployed to real Azure Container Apps behind a canary revision, with a documented promote-to-100% step.
- Every coverage claim in the final output is traceable to an actual cited policy clause (no hallucinated grounding).

## 3. Architecture

### 3.1 Orchestration graph

The workflow is a **sequential backbone with a parallel fan-out and one conditional handoff edge**, built on Microsoft Agent Framework's graph-based workflow engine, coordinated by a supervisor agent:

```
FNOL text/police report
        │
        ▼
 ┌───────────────┐
 │ Extraction     │  → structured JSON facts + per-field confidence
 │ Agent          │
 └───────┬────────┘
         │
   supervisor checks confidence
         │
   ┌─────┴─────┐
   │           │
 low conf.   sufficient conf.
   │           │
   ▼           ▼ (parallel fan-out)
┌─────────┐ ┌──────────┐  ┌───────────────┐
│Clarify/ │ │ Coverage │  │ Fraud-Risk    │
│Escalate │ │ Agent    │  │ Agent         │
│(handoff)│ │ (RAG)    │  │ (MCP signals) │
└─────────┘ └────┬─────┘  └──────┬────────┘
                  └───────┬───────┘
                          ▼
                 ┌──────────────────┐
                 │ Adjuster-Summary  │
                 │ Agent             │
                 └────────┬──────────┘
                          ▼
              structured recommendation
              (coverage + fraud + rationale + citations)
                          │
                          ▼
                  human adjuster decides
```

- **Extraction Agent** — structured prompting + few-shot examples, converts unstructured FNOL text/police reports into a fixed Pydantic-validated JSON schema. Emits per-field confidence.
- **Supervisor** — inspects extraction confidence/completeness. Below threshold or missing required fields → **handoff** to a clarification/escalation path (flagged for human input, pipeline does not guess). Otherwise → fan-out to Coverage and Fraud-Risk agents in parallel (they are independent given the extracted facts).
- **Coverage Agent** — RAG over Azure AI Search (policy document corpus) + `policy-db-mcp` tool, produces a grounded coverage determination (approve/deny/needs-info) with citations to specific policy clauses.
- **Fraud-Risk Agent** — calls `claims-history-mcp` and `vin-vehicle-mcp`, reasons over red-flag signals (claim timing vs policy effective date, claim frequency, narrative inconsistencies, prior flags), produces a 0–100 risk score + rationale tied to specific tool-returned facts.
- **Adjuster-Summary Agent** — merges Coverage + Fraud-Risk outputs into one structured recommendation packet. This is the terminal output; the system never auto-decides.

### 3.2 Why this topology (vs alternatives considered)

- **Fully sequential** (rejected as primary): simplest, but coverage and fraud scoring have no real dependency on each other, so serializing them wastes latency and doesn't exercise the framework's parallel/handoff capabilities the resume bullet claims.
- **Fully dynamic supervisor-routed handoff** (rejected as primary): most flexible, but a non-deterministic path is harder to test and eval deterministically — bad fit for a system whose core differentiator is CI-gated evals.
- **Chosen (sequential + parallel fan-out + conditional handoff)**: deterministic and testable for the common path, while still genuinely using both orchestration modes for a real reason (confidence-based escalation), not just to check a resume box.

## 4. Agents & Model Tiering

All models are served via Azure AI Foundry / Azure OpenAI Service. Tiering reflects a deliberate cost/quality tradeoff: expensive reasoning only where grounding and stakes justify it.

| Agent | Model | Rationale |
|---|---|---|
| Extraction | GPT-5-mini | High-volume, structured-output task; strong instruction-following suffices at lower cost/latency. |
| Coverage/RAG | GPT-5 (full) | Grounded reasoning over retrieved policy text with correct citation is the highest-stakes step. |
| Fraud-Risk | GPT-5 (full) | Reasoning over multiple weak signals; both false positives and false negatives are costly. |
| Adjuster-Summary | GPT-5-mini | Summarizes already-structured upstream output; lower reasoning burden. |
| Supervisor/orchestrator | GPT-5-mini *(superseded — see note)* | Frequent, cheap confidence-check/routing calls, not deep reasoning. |
| Eval judge | GPT-5 (full); a second distinct judge model spot-checks any output produced by GPT-5 itself | Avoids a model favoring its own outputs (self-preference bias) during eval. |

*Note: model names above reflect the Azure OpenAI/Foundry catalog as of 2026-08-10 (GPT-5 family, including newer variants like GPT-5.6 already shipping). Re-check the live Azure AI Foundry model catalog at implementation time and swap in whatever the current equivalent nano/mini/full tiers are — the tiering strategy (cheap model for structured/high-volume steps, strong model for grounded/high-stakes reasoning) is the durable part of this decision, not the specific model name.*

*Supervisor row superseded during Phase 6 planning: the Supervisor's actual job (§3.1 — checking per-field confidence floats and required-field presence that the Extraction Agent already computed) is a threshold check with no natural-language reasoning involved, so it's implemented as a deterministic Python predicate instead of an LLM call — no Supervisor deployment exists. See [Phase 6's plan](../plans/2026-08-15-phase-6-supervisor-orchestration-graph.md) (Architecture section) for the full rationale, confirmed with the project owner before implementation.*

**Fraud-risk approach:** LLM-based reasoning over MCP-sourced signals, not a trained ML classifier. Rationale: no ML training pipeline/labeled fraud dataset is needed, it fits the agentic architecture directly (same tool-calling pattern as Coverage), and it produces an explainable rationale an adjuster can read — appropriate for a recommendation-only system where a human makes the final call.

## 5. Data & Grounding

### 5.1 Synthetic data (generated, not sourced externally)
- **Policy document corpus**: 8–12 synthetic auto insurance policy documents spanning liability-only, full coverage, and comprehensive/collision tiers, plus a couple of state-variant clause sets — enough for real retrieval behavior and edge-case coverage without corpus-scale engineering effort.
- **Postgres** (containerized, local to the deployment): seeded tables for policies, claims history, and VIN/vehicle records.

### 5.2 Azure AI Search
Policy documents are chunked and embedded into Azure AI Search (hybrid vector + keyword search) — this is what the Coverage Agent retrieves against for grounded, citable answers.

### 5.3 MCP servers
Three MCP servers, each wrapping the Postgres tables (not the LLM), so agents call real systems instead of inventing facts:
1. `policy-db-mcp` — policy lookup by policy number/VIN → coverage tier, limits, effective dates.
2. `claims-history-mcp` — prior claims by policyholder → frequency, recency, prior fraud flags.
3. `vin-vehicle-mcp` — VIN decode + vehicle market value.

### 5.4 FNOL extraction schema (fixed, Pydantic-validated)
Incident date/time, location, parties involved (roles, contact info), vehicles (VIN if present), injuries (boolean + description), narrative summary, per-field extraction confidence. Few-shot examples cover: multi-vehicle pileups, hit-and-run (no other party), ambiguous fault language, ambiguous injury mentions.

## 6. Evaluation Framework

The eval suite is the mechanism that actually earns the "production-grade" claim — it gates every prompt/model change before it ships.

**Test sets** (versioned, checked into the repo, generated alongside the synthetic corpus):
- Extraction accuracy set (~40–60 labeled FNOL/police-report samples with gold JSON, including held-out variants of the few-shot edge cases).
- Coverage-determination set (FNOL + policy pairs with known-correct outcome and required citation).
- Fraud-risk set (cases with known-good risk tier: low/med/high).

**Metrics:**
- **Extraction**: deterministic field-level exact/fuzzy match against gold JSON — not LLM-judged, since this is a structured-output task with a real ground truth.
- **Coverage**: LLM-as-judge scores (a) correctness of approve/deny/needs-info vs gold label, (b) a **hallucination/grounding check** — every coverage claim must trace to a policy clause that (i) actually exists in the corpus and (ii) actually supports the claim.
- **Fraud-risk**: LLM-as-judge checks the rationale is supported by real tool-returned data (not fabricated red flags), plus tier-accuracy vs gold.
- **Judge model**: distinct/stronger model than the one under test where feasible (see Section 4).

**Regression gating:** pytest-based harness produces a scored report (Pandas is appropriate here for batch aggregation across the test set — not on the live single-claim request path). CI fails the build if aggregate scores drop below a checked-in baseline per agent.

## 7. Deployment & CI/CD

- **Compute**: Azure Container Apps — one container app per service (FastAPI orchestrator API; each MCP server as its own container app). Scale-to-zero keeps demo cost low; KEDA-based autoscaling underneath gives the same "Kubernetes-based" story as AKS without a standing cluster bill. AKS was considered and rejected for this scale — documented here so the tradeoff can be discussed accurately (real node-pool cost/complexity vs a portfolio demo's actual traffic).
- **Storage**: Azure Blob Storage for uploaded claim documents/photos (referenced by URL in the claim record; not passed inline to the LLM).
- **Vector store**: Azure AI Search (Section 5.2).
- **Model hosting**: Azure AI Foundry / Azure OpenAI Service (Section 4).
- **Containerization**: one Dockerfile per service; docker-compose for local dev (Postgres + all services together).
- **CI/CD**: GitHub Actions. On every PR: lint/typecheck → unit tests → eval suite (Section 6) against baseline thresholds. On merge to main: build+push images → deploy to a canary revision in Container Apps (traffic-split, e.g. 10%) → manual promote-to-100% step.
- **Secrets**: Azure OpenAI keys/connection strings via GitHub Actions OIDC → Azure federated credentials, not long-lived secrets in the repo or environment files.

## 8. Error Handling

- **Low extraction confidence / missing required fields** → handoff to clarification/escalation path (Section 3.1), not silent continuation.
- **MCP tool call failure** (e.g. policy not found, DB unreachable) → agent surfaces this explicitly in its output ("policy lookup failed — cannot determine coverage") rather than the LLM guessing a plausible-sounding answer.
- **Coverage claim without a valid citation** → caught by the eval suite's grounding check pre-production; at runtime, the Coverage Agent's prompt requires citing a real retrieved chunk ID, and the API layer validates the cited chunk ID actually exists in the retrieval set before returning it.
- **Eval score regression** → CI build fails, blocking merge (Section 6).

## 9. Testing

- **Unit tests**: Pydantic schema validation, MCP tool client behavior (mocked DB), FastAPI endpoint contracts.
- **Integration tests**: full workflow graph run against the seeded synthetic Postgres + Azure AI Search index (using a test index/namespace), asserting the graph reaches the correct terminal node (normal path vs clarification handoff) for representative fixtures.
- **Eval suite**: Section 6 — the primary quality gate, run in CI.
- **Manual/API-level demo testing**: exercised via the OpenAPI/Swagger docs and Postman collection before considering any milestone "done" — per this project's engineering practice, UI/API-facing changes are verified by actually calling the running service, not just by passing automated tests.

## 10. Working Agreement for Implementation

- **No direct code writes to this project by the assistant.** During implementation, code is provided as explained snippets in chat; the user creates the files and types/pastes the code themselves, as a deliberate part of how they want to learn this material. This spec and other planning artifacts (design docs, plans) are the exception — those are written directly, per the user's request in this session.

## 11. Open Items / Future Work (explicitly deferred)

- Real AKS deployment, if a later goal requires it for a specific job description's literal keyword match.
- A web or chat UI on top of the API.
- Automated routing of high-value/high-fraud-risk claims to mandatory human sign-off (conditional gating) — noted as a natural extension once the base pipeline is solid, not built in v1.
- Expanding the policy corpus beyond ~8–12 documents if retrieval quality needs more stress-testing.
