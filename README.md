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
.\scripts\check-release-layout.ps1 -RequireAddin
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
gh release create v0.1.0 dist\installer\RevitMcpSetup.exe --title "Revit MCP v0.1.0" --notes "Initial release"
```

If the tag already exists, upload or replace the installer:

```powershell
gh release upload v0.1.0 dist\installer\RevitMcpSetup.exe --clobber
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
