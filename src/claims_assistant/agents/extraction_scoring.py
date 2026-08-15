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
