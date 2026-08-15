# src/claims_assistant/search/index_schema.py
from __future__ import annotations

from azure.search.documents.indexes.models import (
    HnswAlgorithmConfiguration,
    SearchableField,
    SearchField,
    SearchFieldDataType,
    SearchIndex,
    SimpleField,
    VectorSearch,
    VectorSearchProfile,
)

VECTOR_DIMENSIONS = 1536
HNSW_ALGORITHM_NAME = "policy-hnsw"
VECTOR_PROFILE_NAME = "policy-vector-profile"


def build_policy_index(index_name: str) -> SearchIndex:
    fields = [
        SimpleField(name="chunk_id", type=SearchFieldDataType.STRING, key=True),
        SimpleField(name="form_id", type=SearchFieldDataType.STRING, filterable=True),
        SimpleField(
            name="state", type=SearchFieldDataType.STRING, filterable=True, facetable=True
        ),
        SimpleField(
            name="tier", type=SearchFieldDataType.STRING, filterable=True, facetable=True
        ),
        SearchableField(name="section_title"),
        SearchableField(name="content"),
        SearchField(
            name="content_vector",
            type=SearchFieldDataType.Collection(SearchFieldDataType.SINGLE),  # type: ignore[operator]
            searchable=True,
            vector_search_dimensions=VECTOR_DIMENSIONS,
            vector_search_profile_name=VECTOR_PROFILE_NAME,
        ),
    ]
    vector_search = VectorSearch(
        profiles=[
            VectorSearchProfile(
                name=VECTOR_PROFILE_NAME, algorithm_configuration_name=HNSW_ALGORITHM_NAME
            )
        ],
        algorithms=[HnswAlgorithmConfiguration(name=HNSW_ALGORITHM_NAME)],
    )
    return SearchIndex(name=index_name, fields=fields, vector_search=vector_search)
