# src/claims_assistant/search/indexer.py
from __future__ import annotations

from claims_assistant.config import Settings
from claims_assistant.policy_documents import STATE_MINIMUMS, TIER_TEXT, render_policy_document
from claims_assistant.search.chunking import PolicyChunk, chunk_policy_document
from claims_assistant.search.clients import build_search_client, build_search_index_client
from claims_assistant.search.embeddings import build_embedding_client, embed_texts
from claims_assistant.search.index_schema import build_policy_index


def _chunk_full_corpus() -> list[PolicyChunk]:
    chunks = []
    for state in STATE_MINIMUMS:
        for tier in TIER_TEXT:
            form_id = f"{state}-{tier.upper().replace('_', '-')}"
            document_text = render_policy_document(state, tier)
            chunks.extend(chunk_policy_document(form_id, state, tier, document_text))
    return chunks


async def index_policy_corpus(settings: Settings) -> int:
    chunks = _chunk_full_corpus()

    async with build_search_index_client(settings) as index_client:
        await index_client.create_or_update_index(
            build_policy_index(settings.azure_search_index_name)
        )

    async with build_embedding_client(settings) as embedding_client:
        vectors = await embed_texts(
            embedding_client,
            settings.azure_openai_embedding_deployment,
            [chunk.content for chunk in chunks],
        )

    documents = [
        {
            "chunk_id": chunk.chunk_id,
            "form_id": chunk.form_id,
            "state": chunk.state,
            "tier": chunk.tier,
            "section_title": chunk.section_title,
            "content": chunk.content,
            "content_vector": vector,
        }
        for chunk, vector in zip(chunks, vectors, strict=True)
    ]

    async with build_search_client(settings) as search_client:
        await search_client.upload_documents(documents=documents)

    return len(documents)
