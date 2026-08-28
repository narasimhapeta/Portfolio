# src/claims_assistant/frontend/pages/status.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Claim Status")

default_id = st.session_state.get("last_claim_id", "")
claim_id = st.text_input("Claim ID", value=default_id, key="lookup_claim_id")

if st.button("Look up", key="lookup_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    result = client.get_claim(claim_id)
    st.write(f"Status: **{result['status']}**")
    if result.get("recommendation"):
        st.json(result["recommendation"])
    if result.get("clarification"):
        st.json(result["clarification"])
    if result.get("error"):
        st.error(result["error"])
