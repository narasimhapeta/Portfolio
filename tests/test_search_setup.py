# tests/test_search_setup.py
from azure.core.credentials import AzureKeyCredential
from azure.search.documents.aio import SearchClient
from azure.search.documents.indexes.aio import SearchIndexClient
from azure.search.documents.indexes.models import SearchFieldDataType, SimpleField


def test_search_index_client_constructs_without_network_call():
    client = SearchIndexClient(
        "https://example.search.windows.net", AzureKeyCredential("test-key")
    )

    assert isinstance(client, SearchIndexClient)


def test_search_client_constructs_without_network_call():
    client = SearchClient(
        "https://example.search.windows.net", "policy-documents", AzureKeyCredential("test-key")
    )

    assert isinstance(client, SearchClient)


def test_simple_field_builds_a_search_field():
    field = SimpleField(name="chunk_id", type=SearchFieldDataType.STRING, key=True)

    assert field.name == "chunk_id"
    assert field.key is True
