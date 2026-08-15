# tests/test_chunking.py
from claims_assistant.policy_documents import render_policy_document
from claims_assistant.search.chunking import chunk_policy_document


def test_chunk_policy_document_produces_one_chunk_per_section():
    text = render_policy_document("CA", "full_coverage")

    chunks = chunk_policy_document("CA-FULL-COVERAGE", "CA", "full_coverage", text)

    assert [c.section_title for c in chunks] == [
        "Section 1. Definitions",
        "Section 2. Liability Coverage",
        "Section 3. Physical Damage Coverage",
        "Section 4. Exclusions",
        "Section 5. Claims Filing Procedures",
        "Section 6. State-Specific Endorsement",
        "Summary",
    ]


def test_chunk_ids_are_deterministic_and_namespaced_by_form_id():
    text = render_policy_document("CA", "full_coverage")

    chunks = chunk_policy_document("CA-FULL-COVERAGE", "CA", "full_coverage", text)

    assert chunks[2].chunk_id == "CA-FULL-COVERAGE_section-3-physical-damage-coverage"
    assert all(c.chunk_id.startswith("CA-FULL-COVERAGE_") for c in chunks)
    assert len({c.chunk_id for c in chunks}) == 7  # all unique



def test_chunk_content_stays_scoped_to_its_own_section():
    text = render_policy_document("CA", "liability_only")

    chunks = chunk_policy_document("CA-LIABILITY-ONLY", "CA", "liability_only", text)

    physical_damage_chunk = next(
        c for c in chunks if c.section_title == "Section 3. Physical Damage Coverage"
    )
    assert "does NOT include Collision or Comprehensive" in physical_damage_chunk.content
    other_chunks = [c for c in chunks if c is not physical_damage_chunk]
    assert all("does NOT include Collision" not in c.content for c in other_chunks)


def test_chunk_metadata_fields_are_populated():
    text = render_policy_document("TX", "comprehensive_collision")

    chunks = chunk_policy_document(
        "TX-COMPREHENSIVE-COLLISION", "TX", "comprehensive_collision", text
    )

    assert all(c.form_id == "TX-COMPREHENSIVE-COLLISION" for c in chunks)
    assert all(c.state == "TX" for c in chunks)
    assert all(c.tier == "comprehensive_collision" for c in chunks)

