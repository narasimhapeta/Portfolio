# tests/test_index_schema.py
from claims_assistant.search.index_schema import build_policy_index


def test_build_policy_index_has_expected_fields():
    index = build_policy_index("policy-documents")

    field_names = {f.name for f in index.fields}
    assert field_names == {
        "chunk_id",
        "form_id",
        "state",
        "tier",
        "section_title",
        "content",
        "content_vector",
    }


def test_chunk_id_is_the_key_field():
    index = build_policy_index("policy-documents")

    key_fields = [f for f in index.fields if f.key]
    assert len(key_fields) == 1
    assert key_fields[0].name == "chunk_id"


def test_content_vector_field_is_configured_for_vector_search():
    index = build_policy_index("policy-documents")

    vector_field = next(f for f in index.fields if f.name == "content_vector")
    assert vector_field.vector_search_dimensions == 1536
    assert vector_field.vector_search_profile_name is not None
    assert index.vector_search is not None
    profile_names = {p.name for p in index.vector_search.profiles}
    assert vector_field.vector_search_profile_name in profile_names


def test_form_id_state_tier_are_filterable():
    index = build_policy_index("policy-documents")

    filterable = {f.name for f in index.fields if f.filterable}
    assert {"form_id", "state", "tier"} <= filterable
