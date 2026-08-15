# tests/test_retrieval.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.retrieval import retrieve_policy_chunks

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_is_scoped_to_the_requested_form_id():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert len(results) > 0
    assert all(r.form_id == "CA-LIABILITY-ONLY" for r in results)


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_surfaces_the_relevant_clause():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert any("does NOT include Collision" in r.content for r in results)


@pytest.mark.asyncio
async def test_retrieve_policy_chunks_does_not_leak_other_documents():
    settings = get_settings()

    results = await retrieve_policy_chunks(
        settings,
        form_id="CA-LIABILITY-ONLY",
        query_text="Does my policy cover collision damage to my own car?",
        top=4,
    )

    assert all(r.chunk_id.startswith("CA-LIABILITY-ONLY_") for r in results)
