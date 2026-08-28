# src/claims_assistant/frontend/app.py
from __future__ import annotations

import streamlit as st

from claims_assistant.frontend.auth import require_login

require_login()

pages = [
    st.Page("pages/submit.py", title="Submit FNOL", icon="📝"),
    st.Page("pages/status.py", title="Claim Status", icon="🔍"),
    st.Page("pages/upload.py", title="Upload Document", icon="📎"),
    st.Page("pages/history.py", title="Claim History", icon="📋"),
]
st.navigation(pages).run()
