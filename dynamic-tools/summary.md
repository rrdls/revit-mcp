# Dynamic Saved Tools

This feature adds a local-first registry for reusable Revit MCP automations.

## Goal

When a generated C# snippet proves useful, the assistant or user can promote it into a saved tool. That tool can later be listed, inspected, and executed deterministically without regenerating the automation from scratch.

## Storage

The registry is a normal filesystem folder so it can live inside a Google Drive Desktop synced directory without OAuth/API integration.

```text
Revit MCP/
  config/
    workspace.json
  tools/
    tool-id/
      tool.json
      code.cs
      README.md
      versions/
  history/
  runs/
  trash/
```

## MCP Tools

- `init_saved_tool_library`
- `save_revit_tool`
- `list_saved_revit_tools`
- `get_saved_revit_tool`
- `run_saved_revit_tool`
- `delete_saved_revit_tool`
- `list_revit_code_history`
- `promote_revit_code_history_to_tool`

`run_saved_revit_tool` validates/defaults parameters, injects them as C# local variables, records the run, and sends the final code through the existing Revit bridge.

Raw `run_revit_code` executions are recorded as daily JSONL files under `history/`, so successful one-off snippets can later be promoted into saved tools.

## Revit Add-in

The add-in settings now include `Tool library path`. When the packaged MCP server starts, the add-in passes that path through `REVIT_MCP_TOOL_LIBRARY`.

The ribbon includes a `Saved Tools` window that lists tools from the configured library, renders parameter inputs, executes tools against the active document, records runs, copies IDs, refreshes, and opens the library folder.
