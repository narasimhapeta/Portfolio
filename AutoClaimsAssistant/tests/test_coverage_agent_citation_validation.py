# tests/test_coverage_agent_citation_validation.py
import pytest

from claims_assistant.agents.coverage_agent import _validate_citations
from claims_assistant.search.retrieval import RetrievedChunk

_RETRIEVED = [
    RetrievedChunk(
        chunk_id="CA-FULL-COVERAGE_section-3-physical-damage-coverage",
        form_id="CA-FULL-COVERAGE",
        section_title="Section 3. Physical Damage Coverage",
        content="...",
        score=1.5,
    )
]


def test_validate_citations_passes_when_all_citations_were_retrieved():
    _validate_citations(
        ["CA-FULL-COVERAGE_section-3-physical-damage-coverage"], _RETRIEVED
    )  # does not raise


def test_validate_citations_raises_on_a_fabricated_chunk_id():
    with pytest.raises(ValueError, match="section-99-does-not-exist"):
        _validate_citations(["CA-FULL-COVERAGE_section-99-does-not-exist"], _RETRIEVED)
