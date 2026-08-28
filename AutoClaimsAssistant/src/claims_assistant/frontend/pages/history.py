# src/claims_assistant/frontend/pages/history.py
from __future__ import annotations

import os

import pandas as pd
import streamlit as st

from claims_assistant.frontend.api_client import ClaimsApiClient

st.title("Claim History")

client = ClaimsApiClient(base_url=os.environ.get("CLAIMS_API_BASE_URL", "http://localhost:8000"))
claims = client.list_claims(limit=50, offset=0)
st.dataframe(
    pd.DataFrame(claims)[["id", "status", "policy_number", "vin", "created_at"]]
    if claims
    else pd.DataFrame(columns=["id", "status", "policy_number", "vin", "created_at"])
)
