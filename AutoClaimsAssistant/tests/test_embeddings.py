# tests/test_embeddings.py
import pytest

from claims_assistant.config import get_settings
from claims_assistant.search.embeddings import build_embedding_client, embed_texts

pytestmark = pytest.mark.integration


@pytest.mark.asyncio
async def test_embed_texts_returns_one_vector_per_input():
    settings = get_settings()
    client = build_embedding_client(settings)

    vectors = await embed_texts(
        client, settings.azure_openai_embedding_deployment, ["hello world", "goodbye world"]
    )

    assert len(vectors) == 2
    assert len(vectors[0]) == 1536
    assert len(vectors[1]) == 1536
    assert vectors[0] != vectors[1]
