# tests/conftest.py
import socket
import subprocess
import sys
import time
from collections.abc import Iterator

import pytest
import pytest_asyncio

from claims_assistant.database import create_all_tables
from claims_assistant.seed_data import seed_database


@pytest_asyncio.fixture
async def seeded_db() -> None:
    await create_all_tables()
    await seed_database()


_MCP_SERVER_MODULES = [
    "claims_assistant.mcp_servers.policy_db",
    "claims_assistant.mcp_servers.claims_history",
    "claims_assistant.mcp_servers.vin_vehicle",
]
_MCP_SERVER_PORTS = [8101, 8102, 8103]


def _wait_for_port(port: int, timeout: float = 10.0) -> None:
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            if sock.connect_ex(("localhost", port)) == 0:
                return
        time.sleep(0.2)
    raise TimeoutError(f"MCP server on port {port} did not start within {timeout}s")


@pytest.fixture(scope="session")
def mcp_servers() -> Iterator[None]:
    processes = [
        subprocess.Popen([sys.executable, "-m", module]) for module in _MCP_SERVER_MODULES
    ]
    try:
        for port in _MCP_SERVER_PORTS:
            _wait_for_port(port)
        yield
    finally:
        for process in processes:
            process.terminate()
        for process in processes:
            process.wait(timeout=5)
