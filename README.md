# Revit MCP

Revit MCP lets ChatGPT or another MCP client execute focused C# snippets inside Autodesk Revit.

The local architecture is:

```text
ChatGPT / MCP client
  -> local MCP server
  -> localhost WebSocket
  -> Revit add-in
  -> ExternalEvent
  -> Roslyn C# runtime
  -> Revit API
```

The project is local-first. There is no hosted backend required.

## Supported Revit Versions

Current target:

```text
Revit 2021-2024 -> .NET Framework 4.8 / net48
Revit 2025-2026 -> .NET 8 / net8.0-windows
```

Build and test each Revit version against that version's own `RevitAPI.dll`.

## MCP Tools

```text
run_revit_code(code: string, timeout_seconds: number = 60) -> string
get_revit_mcp_prompt() -> string
get_revit_context(timeout_seconds: number = 30) -> string
init_saved_tool_library(library_path?: string) -> string
save_revit_tool(...) -> object
list_saved_revit_tools(library_path?: string) -> object[]
get_saved_revit_tool(tool_id: string, library_path?: string) -> object
run_saved_revit_tool(tool_id: string, parameter_values?: object, timeout_seconds: number = 60, library_path?: string) -> string
delete_saved_revit_tool(tool_id: string, library_path?: string) -> object
list_revit_code_history(limit: number = 50, library_path?: string) -> object[]
promote_revit_code_history_to_tool(...) -> object
```

`run_revit_code` expects only the body of a generated C# method. Do not send `using` directives, namespace/class declarations, or a `Run` method.

The add-in already provides:

```csharp
UIApplication app
UIDocument uidoc
Document doc
```

Correct:

```csharp
return doc.Title;
```

Wrong:

```csharp
Document doc = uidoc.Document;
```

For version-sensitive API usage, call `get_revit_context()` first and adapt generated code to the returned Revit version.

## Saved Tools

Saved tools turn useful generated C# snippets into reusable, deterministic Revit MCP commands.

The library is filesystem-based so it works with local folders or a Google Drive Desktop synced folder. Configure it in the Revit ribbon under:

```text
Revit MCP > Settings > Tool library path
```

The packaged MCP process receives that path through:

```text
REVIT_MCP_TOOL_LIBRARY
```

If no path is configured, the default is:

```text
Documents/Revit MCP
```

Recommended Google Drive layout:

```text
Google Drive/
  Revit MCP/
    config/
      workspace.json
    tools/
      renumber-sheets/
        tool.json
        code.cs
        README.md
        versions/
      create-fire-stair/
        tool.json
        code.cs
        README.md
        versions/
    history/
    runs/
    trash/
```

Each tool stores metadata in `tool.json` and the executable method body in `code.cs`.

Example:

```json
{
  "id": "renumber-sheets",
  "name": "Renumber Sheets",
  "description": "Renumber sheets using a prefix and start number.",
  "version": "1.0.0",
  "entrypoint": "code.cs",
  "parameters": {
    "prefix": { "type": "string", "default": "ARQ-" },
    "startNumber": { "type": "integer", "required": true },
    "mode": {
      "type": "choice",
      "options": [
        { "label": "Preview only", "value": "preview" },
        { "label": "Renumber sheets", "value": "renumber" }
      ],
      "default": "preview"
    }
  },
  "revitVersions": ["2025"],
  "tags": ["sheets"],
  "requiresTransaction": true
}
```

`run_revit_code` records raw execution history as JSONL under `history/`. This gives the assistant or user a trail of useful snippets that can later be promoted into saved tools with `promote_revit_code_history_to_tool`.

When `run_saved_revit_tool` executes a saved tool, it validates the provided parameters, applies defaults, injects them as C# local variables, records the run under `runs/`, and then sends the final method body through the same Revit bridge used by `run_revit_code`.

The Revit ribbon also includes `Saved Tools`, which lists tools from the configured library, renders parameter inputs from `tool.json`, executes the selected tool in the active Revit document, records the run under `runs/`, copies tool IDs, refreshes the list, and opens the library folder. Tools marked `requiresTransaction: true` show a confirmation prompt before execution.

Supported parameter field types:

- `string`: text input.
- `integer`: text input parsed as an integer.
- `number`: text input parsed as a floating-point number.
- `boolean`: checkbox.
- `choice`: dropdown from a static `options` array.
- `level`: dropdown populated from levels in the active Revit document.
- `wallType`: dropdown populated from wall types.
- `floorType`: dropdown populated from floor types.
- `material`: dropdown populated from materials.
- `category`: dropdown populated from Revit categories.
- `element`: dropdown from the first currently selected Revit element.
- `elements`: dropdown entry containing all currently selected Revit elements.

For Revit element fields, the selected value is stored as an id and injected into the saved C# body as friendly variables. For example, a `baseLevel` parameter injects `baseLevelId` as an `ElementId` and `baseLevel` as `doc.GetElement(baseLevelId)`. A `category` parameter injects `categoryBuiltInCategory` and `category` from `Category.GetCategory(...)`.

Assistant workflow for reusable automations:

1. Prototype a small method-body snippet with `run_revit_code`.
2. Convert user-changeable values into saved tool parameters.
3. Save the tested code with `save_revit_tool`.
4. Use `list_saved_revit_tools` or `get_saved_revit_tool` to verify it.
5. Re-run it later with `run_saved_revit_tool` or the Revit `Saved Tools` window.

## Tutorial Para Usuários Leigos

This is the intended final experience once an installer release is published.

1. Download `RevitMcpSetup.exe` from Releases.
2. Run the installer.
3. Open or restart Revit.
4. In Revit, open the `Revit MCP` ribbon panel.
5. Click `Start MCP`.
6. Click `Copy Local URL`. It should copy:

```text
http://127.0.0.1:8000/<mcp-secret>/mcp
```

7. Add that MCP URL in your MCP client.
8. Test with:

```csharp
return doc.Title;
```

If something fails, use `Open Logs` in the `Revit MCP` ribbon panel or check:

```text
%LOCALAPPDATA%\RevitMcp\addin.log
```

### Important User Notes

- Revit must be open.
- A project or family document should be active.
- The tool can modify the model. Review requests before allowing destructive changes.
- Large tasks should be split into small steps: levels, walls, doors, windows, floors, then views/rooms/annotations.

## Tutorial Para Devs

Use this path while developing from source.

### Requirements

- Windows
- Revit 2021-2026
- .NET SDK 8
- Python 3.11+
- PowerShell

### 1. Clone And Enter The Repo

```powershell
git clone <repo-url>
cd revit-mcp
```

If working from WSL, prefer copying the project to a normal Windows path such as:

```text
C:\dev\revit-mcp
```

Revit and .NET are more reliable with local Windows paths than `\\wsl$` UNC paths.

### 2. Detect Revit

```powershell
.\scripts\find-revit.ps1
```

Example output:

```text
Version    : 2024
InstallDir : C:\Program Files\Autodesk\Revit 2024
```

### 3. Build And Install The Add-in

For Revit 2024:

```powershell
.\scripts\install-addin.ps1 -RevitVersion 2024 -RevitInstallDir "C:\Program Files\Autodesk\Revit 2024"
```

This creates:

```text
%APPDATA%\Autodesk\Revit\Addins\2024\RevitMcp.addin
```

### 4. Start MCP Over HTTP

For development without the Revit ribbon:

```powershell
.\scripts\run-http-server.ps1
```

This starts the development server with the default path:

```text
http://127.0.0.1:8000/mcp
```

The Revit add-in connects locally to:

```text
ws://127.0.0.1:8765
```

The Revit ribbon flow starts the packaged server with a secret path:

```text
http://127.0.0.1:8000/<mcp-secret>/mcp
```

### 5. Start A Fixed Public URL

The product flow uses ngrok with the user's ngrok account and fixed domain.

In Revit, open `Revit MCP > Settings` and configure:

- `ngrok authtoken`
- `ngrok domain`
- `MCP auth token`

Then click `Start Public URL` and `Copy Public URL`.

```text
https://your-ngrok-domain.ngrok-free.app/<mcp-secret>/mcp
```

### 6. Test

Open or restart Revit, then call:

```csharp
return doc.Title;
```

For local automated Python tests:

```powershell
.\scripts\test-python.ps1
```

### 7. Build This Branch On Windows

Run these from a normal Windows checkout, not from `\\wsl$`.

Check the toolchain:

```powershell
dotnet --info
py --version
```

Run Python tests:

```powershell
.\scripts\test-python.ps1
```

Find the installed Revit path:

```powershell
.\scripts\find-revit.ps1
```

Build and install the add-in for Revit 2025:

```powershell
.\scripts\install-addin.ps1 -RevitVersion 2025 -RevitInstallDir "C:\Program Files\Autodesk\Revit 2025"
```

Then open or restart Revit 2025 and verify:

```text
Revit MCP > Settings > Tool library path
Revit MCP > Saved Tools
```

To package a release that requires the Revit 2025 add-in:

```powershell
.\scripts\package-release.ps1 -RevitVersions 2025 -RequiredAddinVersions 2025
```

To test the Python/WebSocket path without Revit:

Terminal 1:

```powershell
.\scripts\run-server.ps1
```

Terminal 2:

```powershell
.\.venv\Scripts\python.exe -m revit_mcp.fake_revit_client
```

## Packaging For Releases

The release plan is:

```text
RevitMcpLauncher.exe -> fallback/dev launcher
RevitMcpServer.exe   -> hidden MCP server
ngrok.exe            -> fixed public URL tunnel
Revit add-in DLLs    -> one folder per Revit version
Inno Setup installer -> installs app and writes .addin files
```

### Build Python EXEs

```powershell
.\scripts\build-server-exe.ps1
```

Outputs:

```text
dist\RevitMcp\app\RevitMcpServer.exe
dist\RevitMcp\app\RevitMcpLauncher.exe
```

### Package Add-ins

This builds every installed/supported Revit version it can find and skips missing versions:

```powershell
.\scripts\package-addins.ps1
```

Outputs:

```text
dist\RevitMcp\addins\2024\RevitMcpAddin.dll
dist\RevitMcp\addins\2025\RevitMcpAddin.dll
...
```

### Build Full Package

```powershell
.\scripts\package-release.ps1
```

By default this downloads `ngrok.exe` into:

```text
dist\RevitMcp\app\ngrok.exe
```

To skip that download:

```powershell
.\scripts\package-release.ps1 -SkipNgrok
```

To validate an already-built release layout:

```powershell
.\scripts\check-release-layout.ps1 -RequireAddin -RequiredAddinVersions 2025
```

If Inno Setup 6 is installed, the script finds `ISCC.exe` from `PATH` or the default Windows install folders and also builds:

```text
dist\installer\RevitMcpSetup.exe
```

### Publish GitHub Release From Terminal

Install and login with GitHub CLI:

```powershell
gh auth login
```

Create a release with the installer:

```powershell
gh release create v0.2.1 dist\installer\RevitMcpSetup.exe --title "Revit MCP v0.2.1" --notes "Add Revit 2024 release package and improved ribbon icons"
```

If the tag already exists, upload or replace the installer:

```powershell
gh release upload v0.2.1 dist\installer\RevitMcpSetup.exe --clobber
```

The Inno script is:

```text
installer\RevitMcp.iss
```

## Landing Page / GitHub Pages

The public landing page lives in:

```text
docs\index.html
docs\styles.css
```

In GitHub Pages, set the source to the `docs` folder on the default branch.

## Revit Ribbon Behavior

The primary user flow is now inside Revit. The `Revit MCP` ribbon panel:

- starts `RevitMcpServer.exe`;
- stops the server process started by the current Revit session;
- copies the local MCP URL;
- opens the logs/settings folder;
- shows basic connection status.

The same panel also:

- starts and stops a fixed public ngrok URL;
- copies the public MCP URL;
- stores ngrok settings in `%LOCALAPPDATA%\RevitMcp\settings.json`.

The external launcher remains installed as a fallback/development tool:

- starts `RevitMcpServer.exe`;
- starts a temporary tunnel if configured manually;
- captures the public tunnel URL;
- appends the MCP path;
- copies the final URL for ChatGPT;
- opens the logs folder.

`ngrok.exe` is downloaded during `package-release.ps1` and installed next to `RevitMcpServer.exe`.

## Troubleshooting

### Revit Does Not Connect

Check:

```text
%LOCALAPPDATA%\RevitMcp\addin.log
```

Also confirm:

- Revit was restarted after installing the add-in.
- The MCP server is running.
- Port `8765` is free.
- The `.addin` file exists under `%APPDATA%\Autodesk\Revit\Addins\<version>`.

### ChatGPT Cannot Reach MCP

Check:

- MCP HTTP server is running. In the ribbon flow, copy the exact URL from `Copy Local URL`.
- Tunnel points to port `8000`, not `8765`.
- Public URL ends with `/<mcp-secret>/mcp`.

### Revit Code Fails To Compile

Use `get_revit_mcp_prompt()` and follow these rules:

- send only method-body C#;
- do not include `using`;
- do not include classes or methods;
- do not redeclare `doc`, `uidoc`, or `app`;
- split large operations into smaller calls.

## Security

This project executes C# inside Revit. Treat it like a local automation console.

Recommended defaults for public releases:

- local-only MCP server;
- visible launcher status;
- logs for executed code;
- optional read-only mode in the future;
- confirmation UI for destructive operations in the future.

## Useful Commands

Uninstall add-in for one Revit version:

```powershell
.\scripts\uninstall-addin.ps1 -RevitVersion 2024
```

Force dependency install when running the HTTP server:

```powershell
.\scripts\run-http-server.ps1 -InstallDependencies
```

Run a full developer verification pass:

```powershell
.\scripts\verify-windows.ps1 -RevitVersion 2024 -RevitInstallDir "C:\Program Files\Autodesk\Revit 2024"
```
