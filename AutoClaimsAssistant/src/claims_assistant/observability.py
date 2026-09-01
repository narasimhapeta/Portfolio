# src/claims_assistant/observability.py
from __future__ import annotations

import os

from azure.monitor.opentelemetry import configure_azure_monitor
from opentelemetry.sdk.resources import Resource


def configure_observability(service_name: str = "claims-assistant") -> None:
    """Wires OpenTelemetry -> Azure Monitor for this process, if configured.

    No-ops when APPLICATIONINSIGHTS_CONNECTION_STRING is unset (local dev default) --
    call this before constructing the FastAPI app / MCP server / running the workflow
    graph, since it sets the process-wide OTel providers agent_framework's own
    get_tracer()/get_meter() calls read from (see plan Architecture).
    """
    connection_string = os.environ.get("APPLICATIONINSIGHTS_CONNECTION_STRING")
    if not connection_string:
        return
    configure_azure_monitor(
        connection_string=connection_string,
        logger_name="claims_assistant",
        resource=Resource.create({"service.name": service_name}),
    )
