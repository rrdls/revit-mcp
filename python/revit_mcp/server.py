from __future__ import annotations

import logging
import os
from typing import Any

from mcp.server.fastmcp import FastMCP
from mcp.server.transport_security import TransportSecuritySettings

from .revit_connection import bridge_from_env
from .saved_tools import (
    build_runnable_code,
    delete_tool,
    ensure_library,
    list_history,
    list_tools,
    load_tool,
    promote_history_entry,
    record_history,
    record_run,
    save_tool,
)

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

If the user asks for reusable automation, asks to save the behavior for later,
or the same operation would be useful with different inputs, first validate a
small snippet with this tool and then persist it with save_revit_tool.
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

Saved Tools workflow:

Use Saved Tools when the user asks to keep/reuse an automation, when a task is
likely to be repeated, or when useful C# has been tested successfully. Prefer
this flow:

1. Prototype and validate the C# method body with `run_revit_code`.
2. Identify user-changeable inputs and turn them into parameters.
3. Save the method body with `save_revit_tool`.
4. List it with `list_saved_revit_tools` or inspect it with `get_saved_revit_tool`.
5. Execute it later with `run_saved_revit_tool` or from the Revit Saved Tools window.

Saved tool C# rules:

- Save only the same method body format used by `run_revit_code`.
- Do not include using directives, class declarations, or a Run method.
- Parameter names must be valid C# identifiers, preferably camelCase.
- Do not hardcode values that should vary later; make them parameters.
- If the tool modifies the model, set `requires_transaction` to true and keep the
  Transaction inside the saved C# body.
- Use clear tool IDs such as `renumber-sheets` or `create-fire-stair`; IDs must
  use lowercase letters, numbers, hyphen, or underscore.
- Include a practical description explaining what the tool does and what inputs mean.

Saved tool parameter schema:

Basic types:

{
  "prefix": {"type": "string", "default": "ARQ-"},
  "startNumber": {"type": "integer", "required": true},
  "scale": {"type": "number", "default": 1.0},
  "dryRun": {"type": "boolean", "default": true}
}

Rich UI types:

{
  "mode": {
    "type": "choice",
    "options": [
      {"label": "Preview only", "value": "preview"},
      {"label": "Create elements", "value": "create"}
    ],
    "default": "preview"
  },
  "baseLevel": {"type": "level", "required": true},
  "wallType": {"type": "wallType", "required": true},
  "floorType": {"type": "floorType"},
  "material": {"type": "material"},
  "category": {"type": "category"},
  "selectedElement": {"type": "element"},
  "selectedElements": {"type": "elements"}
}

For rich Revit fields, the Revit UI resolves the user's selection. Saved tool
execution injects friendly variables into the C# body:

- `level`, `wallType`, `floorType`, `material`, and `element` parameters inject
  both `<name>Id` as ElementId and `<name>` as `doc.GetElement(<name>Id)`.
- `category` parameters inject `<name>BuiltInCategory` and `<name>` as
  `Category.GetCategory(doc, <name>BuiltInCategory)`.
- `elements` parameters inject `<name>Ids` and `<name>` as a list of elements.

If a useful snippet is already in history, use
`list_revit_code_history` then `promote_revit_code_history_to_tool` instead of
recreating it from scratch.
"""


@mcp.tool(description=RUN_REVIT_CODE_DESCRIPTION)
def run_revit_code(code: str, timeout_seconds: float = 60) -> str:
    """Execute a C# method-body snippet inside Revit through the loaded Revit MCP add-in."""
    try:
        response = get_bridge().run_code(code, timeout_seconds=timeout_seconds)
    except Exception as exc:
        record_history(code=code, error=str(exc))
        raise

    if response.ok:
        result = response.result or ""
        record_history(code=code, result=result)
        return result

    details = f"\n\n{response.details}" if response.details else ""
    error = f"Revit code failed: {response.error or 'Unknown error'}{details}"
    record_history(code=code, error=error)
    raise RuntimeError(error)


@mcp.tool()
def init_saved_tool_library(library_path: str | None = None) -> str:
    """Create or validate the local saved-tool library folder structure."""
    root = ensure_library(library_path)
    return str(root)


@mcp.tool()
def save_revit_tool(
    tool_id: str,
    name: str,
    description: str,
    code: str,
    parameters: dict[str, Any] | None = None,
    version: str = "1.0.0",
    tags: list[str] | None = None,
    revit_versions: list[str] | None = None,
    requires_transaction: bool | None = None,
    library_path: str | None = None,
    overwrite: bool = False,
) -> dict[str, Any]:
    """Persist a reusable C# Revit automation as a saved tool with optional parameter schema."""
    return save_tool(
        tool_id=tool_id,
        name=name,
        description=description,
        code=code,
        parameters=parameters,
        version=version,
        tags=tags,
        revit_versions=revit_versions,
        requires_transaction=requires_transaction,
        library_root=library_path,
        overwrite=overwrite,
    )


@mcp.tool()
def list_saved_revit_tools(library_path: str | None = None) -> list[dict[str, Any]]:
    """List reusable Revit tools saved in the configured local library."""
    return list_tools(library_path)


@mcp.tool()
def get_saved_revit_tool(tool_id: str, library_path: str | None = None) -> dict[str, Any]:
    """Return saved-tool metadata and C# code."""
    tool = load_tool(tool_id, library_path)
    return {
        "metadata": tool.metadata,
        "code": tool.code,
        "path": str(tool.root),
    }


@mcp.tool()
def run_saved_revit_tool(
    tool_id: str,
    parameter_values: dict[str, Any] | None = None,
    timeout_seconds: float = 60,
    library_path: str | None = None,
) -> str:
    """Execute a saved Revit tool by validating and injecting parameters into its C# body."""
    tool = load_tool(tool_id, library_path)
    parameters = parameter_values or {}
    code = build_runnable_code(tool, parameters)
    try:
        response = get_bridge().run_code(code, timeout_seconds=timeout_seconds)
    except Exception as exc:
        record_run(tool_id=tool.id, parameters=parameters, error=str(exc), library_root=library_path)
        raise

    if response.ok:
        result = response.result or ""
        record_run(tool_id=tool.id, parameters=parameters, result=result, library_root=library_path)
        return result

    details = f"\n\n{response.details}" if response.details else ""
    error = f"Saved Revit tool failed: {response.error or 'Unknown error'}{details}"
    record_run(tool_id=tool.id, parameters=parameters, error=error, library_root=library_path)
    raise RuntimeError(error)


@mcp.tool()
def delete_saved_revit_tool(tool_id: str, library_path: str | None = None) -> dict[str, Any]:
    """Move a saved Revit tool to the library trash folder."""
    return delete_tool(tool_id, library_path)


@mcp.tool()
def list_revit_code_history(limit: int = 50, library_path: str | None = None) -> list[dict[str, Any]]:
    """List recent raw run_revit_code history entries, usually before promoting one into a saved tool."""
    return list_history(limit=limit, library_root=library_path)


@mcp.tool()
def promote_revit_code_history_to_tool(
    history_id: str,
    tool_id: str,
    name: str,
    description: str,
    parameters: dict[str, Any] | None = None,
    version: str = "1.0.0",
    tags: list[str] | None = None,
    revit_versions: list[str] | None = None,
    requires_transaction: bool | None = None,
    library_path: str | None = None,
    overwrite: bool = False,
) -> dict[str, Any]:
    """Create a saved Revit tool from a previous useful run_revit_code history entry."""
    return promote_history_entry(
        history_id=history_id,
        tool_id=tool_id,
        name=name,
        description=description,
        parameters=parameters,
        version=version,
        tags=tags,
        revit_versions=revit_versions,
        requires_transaction=requires_transaction,
        library_root=library_path,
        overwrite=overwrite,
    )


@mcp.tool()
def get_revit_mcp_prompt() -> str:
    """Return usage instructions for Revit code generation and Saved Tools workflows."""
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
