from __future__ import annotations

import logging
import os

from mcp.server.fastmcp import FastMCP
from mcp.server.transport_security import TransportSecuritySettings

from .revit_connection import bridge_from_env

logging.basicConfig(level=os.getenv("REVIT_MCP_LOG_LEVEL", "INFO"))

_disable_dns_rebinding_protection = os.getenv("MCP_DISABLE_DNS_REBINDING_PROTECTION", "").lower() in {
    "1",
    "true",
    "yes",
}
_allowed_hosts = [host.strip() for host in os.getenv("MCP_ALLOWED_HOSTS", "").split(",") if host.strip()]
_allowed_origins = [origin.strip() for origin in os.getenv("MCP_ALLOWED_ORIGINS", "").split(",") if origin.strip()]

mcp = FastMCP(
    "revit-mcp",
    host=os.getenv("MCP_HTTP_HOST", "127.0.0.1"),
    port=int(os.getenv("MCP_HTTP_PORT", "8000")),
    streamable_http_path=os.getenv("MCP_HTTP_PATH", "/mcp"),
    transport_security=TransportSecuritySettings(
        enable_dns_rebinding_protection=not _disable_dns_rebinding_protection,
        allowed_hosts=_allowed_hosts,
        allowed_origins=_allowed_origins,
    ),
)
_bridge = None

if os.getenv("REVIT_MCP_START_BRIDGE_ON_IMPORT", "true").lower() in {"1", "true", "yes"}:
    try:
        _bridge = bridge_from_env()
    except RuntimeError:
        logging.exception("Could not start Revit WebSocket bridge during startup. It will retry on tool use.")


def get_bridge():
    global _bridge
    if _bridge is None:
        _bridge = bridge_from_env()
    return _bridge

RUN_REVIT_CODE_DESCRIPTION = """
Execute a C# snippet inside the active Revit document.

Important: send only the body of the generated C# method. Do not send a full
C# file, namespace, class, method declaration, or top-level using directives.
The runtime already injects these variables:

- app: Autodesk.Revit.UI.UIApplication
- uidoc: Autodesk.Revit.UI.UIDocument
- doc: Autodesk.Revit.DB.Document

The runtime already imports common namespaces including Autodesk.Revit.DB,
Autodesk.Revit.UI, System, System.Linq, and System.Collections.Generic.
Do not redeclare doc/app/uidoc. For reads, return a string. For model changes,
open and commit a Revit Transaction inside the snippet.
"""

REVIT_MCP_PROMPT = """
You are using the Revit MCP tool `run_revit_code` to execute C# inside Autodesk Revit.

Rules for every call:

1. Send only the body of the C# method.
2. Do not include `using` directives, namespace declarations, class declarations, or a `Run` method.
3. Do not redeclare `doc`, `uidoc`, or `app`; they are already provided.
4. Available variables:
   - `app`: UIApplication
   - `uidoc`: UIDocument
   - `doc`: Document
5. Common namespaces are already imported: Autodesk.Revit.DB, Autodesk.Revit.UI, System, System.Linq, System.Collections.Generic.
6. Always return a string.
7. Before using version-sensitive Revit API calls, call `get_revit_context()` and adapt the code to the returned Revit version.
8. For read-only operations, use collectors directly.
9. For model modifications, create a `Transaction`, call `Start()`, make changes, then `Commit()`.
10. Keep snippets small and focused. Do not try to generate an entire building/model in one call.
11. For large tasks, split the work into multiple `run_revit_code` calls, for example:
   - create or find levels
   - create wall geometry
   - add doors
   - add windows
   - add floors/roofs
   - create views/rooms/annotations only after the core geometry works
12. Avoid combining model creation, view creation, room creation, annotation, and styling in one snippet.
13. If a script is rejected or fails to compile, retry with a smaller snippet that performs one concrete step.
14. If an operation may be destructive, explain the intended change before running it.

Valid read example:

var levels = new FilteredElementCollector(doc)
    .OfClass(typeof(Level))
    .Cast<Level>()
    .Select(x => x.Name);

return string.Join(", ", levels);

Valid write example:

using (var tx = new Transaction(doc, "MCP change"))
{
    tx.Start();
    // modify the model here
    tx.Commit();
}

return "Done.";

Invalid examples:

using Autodesk.Revit.DB; // invalid: already imported by the wrapper
Document doc = uidoc.Document; // invalid: doc already exists
public class Script { ... } // invalid: wrapper already creates the class

Large-task strategy:

Instead of one huge script that creates walls, floors, rooms, views, sheets, tags, doors, and windows, make several smaller calls. First validate geometry, then add hosted elements, then add documentation objects.
"""


@mcp.tool(description=RUN_REVIT_CODE_DESCRIPTION)
def run_revit_code(code: str, timeout_seconds: float = 60) -> str:
    """Execute a C# method-body snippet inside Revit through the loaded Revit MCP add-in."""
    response = get_bridge().run_code(code, timeout_seconds=timeout_seconds)
    if response.ok:
        return response.result or ""

    details = f"\n\n{response.details}" if response.details else ""
    raise RuntimeError(f"Revit code failed: {response.error or 'Unknown error'}{details}")


@mcp.tool()
def get_revit_mcp_prompt() -> str:
    """Return the usage prompt/instructions for generating valid run_revit_code snippets."""
    return REVIT_MCP_PROMPT.strip()


@mcp.tool()
def get_revit_context(timeout_seconds: float = 30) -> str:
    """Return Revit version/build and active document context for version-aware code generation."""
    code = """
var version = app.Application.VersionNumber;
var build = app.Application.VersionBuild;
var name = app.Application.VersionName;
var title = doc.Title;
var path = string.IsNullOrWhiteSpace(doc.PathName) ? "" : doc.PathName;
var isFamily = doc.IsFamilyDocument;

return "{"
    + "\\\"connected\\\":true,"
    + "\\\"revitVersion\\\":\\\"" + version + "\\\","
    + "\\\"revitVersionName\\\":\\\"" + name.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\","
    + "\\\"revitBuild\\\":\\\"" + build.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\","
    + "\\\"documentTitle\\\":\\\"" + title.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\","
    + "\\\"documentPath\\\":\\\"" + path.Replace("\\\\", "\\\\\\\\").Replace("\\\"", "\\\\\\\"") + "\\\","
    + "\\\"isFamilyDocument\\\":" + isFamily.ToString().ToLowerInvariant()
    + "}";
"""
    response = get_bridge().run_code(code, timeout_seconds=timeout_seconds)
    if response.ok:
        return response.result or "{}"

    details = f"\n\n{response.details}" if response.details else ""
    raise RuntimeError(f"Could not get Revit context: {response.error or 'Unknown error'}{details}")


def main() -> None:
    transport = os.getenv("MCP_TRANSPORT", "stdio")
    if transport not in {"stdio", "sse", "streamable-http"}:
        raise ValueError("MCP_TRANSPORT must be one of: stdio, sse, streamable-http")
    mcp.run(transport=transport)


if __name__ == "__main__":
    main()
