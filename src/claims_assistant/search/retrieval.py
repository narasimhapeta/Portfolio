# src/claims_assistant/search/retrieval.py
from __future__ import annotations

from azure.search.documents.models import VectorizedQuery
from pydantic import BaseModel

from claims_assistant.config import Settings
from claims_assistant.search.clients import build_search_client
from claims_assistant.search.embeddings import build_embedding_client, embed_texts


class RetrievedChunk(BaseModel):
    chunk_id: str
    form_id: str
    section_title: str
    content: str
    score: float


async def retrieve_policy_chunks(
    settings: Settings, form_id: str, query_text: str, top: int = 4
) -> list[RetrievedChunk]:
    async with build_embedding_client(settings) as embedding_client:
        vectors = await embed_texts(
            embedding_client, settings.azure_openai_embedding_deployment, [query_text]
        )
    vector_query = VectorizedQuery(
        vector=vectors[0], k_nearest_neighbors=top, fields="content_vector"
    )

    async with build_search_client(settings) as search_client:
        results = await search_client.search(
            search_text=query_text,
            vector_queries=[vector_query],
            filter=f"form_id eq '{form_id}'",
            select=["chunk_id", "form_id", "section_title", "content"],
            top=top,
        )
        return [
            RetrievedChunk(
                chunk_id=result["chunk_id"],
                form_id=result["form_id"],
                section_title=result["section_title"],
                content=result["content"],
                score=result["@search.score"],
            )
            async for result in results
        ]
