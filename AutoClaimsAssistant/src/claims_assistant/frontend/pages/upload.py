# src/claims_assistant/frontend/pages/upload.py
from __future__ import annotations

import os

import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Upload Document")

default_id = st.session_state.get("last_claim_id", "")
claim_id = st.text_input("Claim ID", value=default_id, key="upload_claim_id")
uploaded_file = st.file_uploader("Document", key="upload_file")

if uploaded_file is not None and claim_id and st.button("Upload", key="upload_button"):
    client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
    result = client.upload_document(
        claim_id=claim_id,
        filename=uploaded_file.name,
        content=uploaded_file.getvalue(),
        content_type=uploaded_file.type or "application/octet-stream",
    )
    st.success("Uploaded.")
    st.json(result.get("document_urls"))
