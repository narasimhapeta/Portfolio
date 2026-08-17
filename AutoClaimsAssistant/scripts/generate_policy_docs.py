# scripts/generate_policy_docs.py
"""Generate the synthetic policy document corpus into data/policy_documents/."""

from pathlib import Path

from claims_assistant.policy_documents import all_policy_forms

OUTPUT_DIR = Path(__file__).resolve().parents[1] / "data" / "policy_documents"


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    forms = all_policy_forms()
    for form_id, content in forms.items():
        (OUTPUT_DIR / f"{form_id}.md").write_text(content, encoding="utf-8")
    print(f"Wrote {len(forms)} policy documents to {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
