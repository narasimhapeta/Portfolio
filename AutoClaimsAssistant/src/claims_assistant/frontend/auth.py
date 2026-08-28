# src/claims_assistant/frontend/auth.py
from __future__ import annotations

import os

import streamlit as st


def verify_password(attempt: str) -> bool:
    expected = os.environ.get("FRONTEND_ACCESS_PASSWORD", "")
    return bool(expected) and attempt == expected


def require_login() -> None:
    """Blocks page rendering until the correct password is entered.
    Call at the top of app.py before st.navigation runs.
    """
    if st.session_state.get("authenticated"):
        return
    st.title("Claims Assistant")
    password = st.text_input("Access password", type="password")
    if st.button("Log in"):
        if verify_password(password):
            st.session_state["authenticated"] = True
            st.rerun()
        else:
            st.error("Incorrect password")
    st.stop()
