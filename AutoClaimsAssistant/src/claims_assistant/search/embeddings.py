# src/claims_assistant/search/embeddings.py
from __future__ import annotations

from openai import AsyncAzureOpenAI

from claims_assistant.config import Settings


def build_embedding_client(settings: Settings) -> AsyncAzureOpenAI:
    return AsyncAzureOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        api_version=settings.azure_openai_api_version,
    )


async def embed_texts(client: AsyncAzureOpenAI, model: str, texts: list[str]) -> list[list[float]]:
    response = await client.embeddings.create(model=model, input=texts)
    return [item.embedding for item in response.data]
