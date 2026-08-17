# tests/test_search_clients.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.clients import build_search_index_client
from claims_assistant.search.index_schema import build_policy_index

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_create_or_update_index_round_trips_the_schema():
    settings = get_settings()
    index = build_policy_index(settings.azure_search_index_name)

    async with build_search_index_client(settings) as index_client:
        created = await index_client.create_or_update_index(index)
        fetched = await index_client.get_index(settings.azure_search_index_name)

    assert created.name == settings.azure_search_index_name
    fetched_field_names = {f.name for f in fetched.fields}
    assert fetched_field_names == {
        "chunk_id",
        "form_id",
        "state",
        "tier",
        "section_title",
        "content",
        "content_vector",
    }
