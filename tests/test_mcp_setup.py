# tests/test_mcp_setup.py
from mcp.server import MCPServer


def test_mcpserver_importable():
    server = MCPServer("smoke-test")

    assert server.name == "smoke-test"
