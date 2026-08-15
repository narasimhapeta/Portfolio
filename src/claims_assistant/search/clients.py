# src/claims_assistant/search/clients.py
from __future__ import annotations

from azure.core.credentials import AzureKeyCredential
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.aio import SearchIndexClient

from claims_assistant.config import Settings


def build_search_index_client(settings: Settings) -> SearchIndexClient:
    return SearchIndexClient(
        settings.azure_search_endpoint, AzureKeyCredential(settings.azure_search_api_key)
    )


def build_search_client(settings: Settings) -> SearchClient:
    return SearchClient(
        settings.azure_search_endpoint,
        settings.azure_search_index_name,
        AzureKeyCredential(settings.azure_search_api_key),
    )
