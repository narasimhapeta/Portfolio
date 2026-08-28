# src/claims_assistant/frontend/pages/submit.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Submit FNOL")

policy_number = st.text_input("Policy number", key="policy_number")
vin = st.text_input("VIN", key="vin")
narrative_text = st.text_area("Narrative", key="narrative_text")

if st.button("Submit claim", key="submit_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    with st.spinner("Running the claim intake pipeline (10-30s)..."):
        result = client.submit_claim(
            policy_number=policy_number, vin=vin, narrative_text=narrative_text
        )
    st.session_state["last_claim_id"] = result["id"]
    if result["status"] == "completed":
        st.success(f"Claim {result['id']} completed.")
        st.json(result["recommendation"])
    elif result["status"] == "needs_clarification":
        st.warning(f"Claim {result['id']} needs clarification.")
        st.json(result["clarification"])
    else:
        st.error(f"Claim {result['id']} failed.")
        st.write(result.get("error"))
