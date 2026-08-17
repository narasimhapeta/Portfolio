# tests/test_indexer.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.clients import build_search_client
from claims_assistant.search.indexer import index_policy_corpus

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_index_policy_corpus_indexes_all_63_chunks():
    settings = get_settings()

    indexed_count = await index_policy_corpus(settings)

    assert indexed_count == 63

    async with build_search_client(settings) as search_client:
        results = await search_client.search(search_text="*", include_total_count=True, top=1)
        total = await results.get_count()

    assert total == 63
