# src/claims_assistant/search/chunking.py
from __future__ import annotations

import re
from dataclasses import dataclass

_SECTION_BOUNDARY = re.compile(r"\n(?=## )")


@dataclass(frozen=True)
class PolicyChunk:
    chunk_id: str
    form_id: str
    state: str
    tier: str
    section_title: str
    content: str


def _slugify_section_title(section_title: str) -> str:
    return section_title.lower().replace(". ", "-").replace(" ", "-").replace(".", "")


def chunk_policy_document(
    form_id: str, state: str, tier: str, document_text: str
) -> list[PolicyChunk]:
    """Split a rendered policy document into one chunk per `## Section` heading.

    The document's leading title/metadata block (before the first `## ` heading) is
    dropped — `state`/`tier`/`form_id` are already carried as index fields, so it adds
    no new information and isn't a citable clause.
    """
    chunks = []
    for raw_section in _SECTION_BOUNDARY.split(document_text):
        section = raw_section.strip()
        if not section.startswith("## "):
            continue
        title_line, _, _ = section.partition("\n")
        section_title = title_line.removeprefix("## ").strip()        
        chunk_id = f"{form_id}_{_slugify_section_title(section_title)}"

        chunks.append(
            PolicyChunk(
                chunk_id=chunk_id,
                form_id=form_id,
                state=state,
                tier=tier,
                section_title=section_title,
                content=section,
            )
        )
    return chunks
