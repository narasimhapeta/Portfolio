# tests/test_policy_documents.py
from claims_assistant.policy_documents import all_policy_forms, render_policy_document
from claims_assistant.seed_data import POLICIES


def test_all_policy_forms_returns_nine_documents():
    forms = all_policy_forms()

    assert len(forms) == 9


def test_generated_forms_cover_every_seeded_policy_form_id():
    forms = all_policy_forms()
    seeded_form_ids = {row["policy_form_id"] for row in POLICIES}

    assert seeded_form_ids.issubset(forms.keys())


def test_liability_only_document_excludes_physical_damage_coverage():
    text = render_policy_document("CA", "liability_only")

    assert "does NOT include Collision or Comprehensive coverage" in text
    assert "Sec. 2.1" in text


def test_full_coverage_document_includes_collision_and_comprehensive():
    text = render_policy_document("TX", "full_coverage")

    assert "Collision Coverage" in text
    assert "Comprehensive Coverage" in text
    assert "$500 deductible" in text


def test_state_endorsement_is_included():
    text = render_policy_document("NY", "comprehensive_collision")

    assert "No-Fault Personal Injury Protection" in text
