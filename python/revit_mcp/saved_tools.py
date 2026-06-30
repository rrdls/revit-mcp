from __future__ import annotations

import json
import os
import re
import shutil
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


SCHEMA_VERSION = 1
DEFAULT_LIBRARY_NAME = "Revit MCP"
TOOL_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9_-]{1,80}$")
CS_IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


@dataclass(frozen=True)
class SavedTool:
    id: str
    root: Path
    metadata: dict[str, Any]
    code: str


def default_library_root() -> Path:
    configured = os.getenv("REVIT_MCP_TOOL_LIBRARY", "").strip()
    if configured:
        return Path(configured).expanduser()
    return Path.home() / "Documents" / DEFAULT_LIBRARY_NAME


def ensure_library(root: Path | str | None = None) -> Path:
    library_root = Path(root).expanduser() if root else default_library_root()
    (library_root / "config").mkdir(parents=True, exist_ok=True)
    (library_root / "tools").mkdir(parents=True, exist_ok=True)
    (library_root / "history").mkdir(parents=True, exist_ok=True)
    (library_root / "runs").mkdir(parents=True, exist_ok=True)
    workspace_path = library_root / "config" / "workspace.json"
    if not workspace_path.exists():
        workspace_path.write_text(
            json.dumps(
                {
                    "schemaVersion": SCHEMA_VERSION,
                    "name": DEFAULT_LIBRARY_NAME,
                    "createdAt": _utc_now(),
                },
                indent=2,
                sort_keys=True,
            )
            + "\n",
            encoding="utf-8",
        )
    return library_root


def save_tool(
    *,
    tool_id: str,
    name: str,
    description: str,
    code: str,
    parameters: dict[str, Any] | None = None,
    version: str = "1.0.0",
    tags: list[str] | None = None,
    revit_versions: list[str] | None = None,
    requires_transaction: bool | None = None,
    library_root: Path | str | None = None,
    overwrite: bool = False,
) -> dict[str, Any]:
    tool_id = normalize_tool_id(tool_id)
    if not name.strip():
        raise ValueError("name must not be empty")
    if not code.strip():
        raise ValueError("code must not be empty")
    parameters = validate_parameters(parameters or {})

    root = ensure_library(library_root)
    tool_root = root / "tools" / tool_id
    if tool_root.exists() and not overwrite:
        raise FileExistsError(f"Saved tool already exists: {tool_id}")

    if tool_root.exists():
        _snapshot_version(tool_root)
    tool_root.mkdir(parents=True, exist_ok=True)

    metadata = {
        "schemaVersion": SCHEMA_VERSION,
        "id": tool_id,
        "name": name.strip(),
        "description": description.strip(),
        "version": version.strip() or "1.0.0",
        "entrypoint": "code.cs",
        "parameters": parameters,
        "revitVersions": revit_versions or [],
        "tags": tags or [],
        "requiresTransaction": requires_transaction,
        "createdAt": _read_created_at(tool_root) or _utc_now(),
        "updatedAt": _utc_now(),
    }

    (tool_root / "tool.json").write_text(
        json.dumps(metadata, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    (tool_root / "code.cs").write_text(code.strip() + "\n", encoding="utf-8")
    readme = tool_root / "README.md"
    if not readme.exists():
        readme.write_text(f"# {metadata['name']}\n\n{metadata['description']}\n", encoding="utf-8")
    return metadata


def list_tools(library_root: Path | str | None = None) -> list[dict[str, Any]]:
    root = ensure_library(library_root)
    tools_root = root / "tools"
    tools = []
    for metadata_path in sorted(tools_root.glob("*/tool.json")):
        try:
            metadata = _read_json(metadata_path)
            tools.append(_summary(metadata))
        except (OSError, ValueError, json.JSONDecodeError) as exc:
            tools.append(
                {
                    "id": metadata_path.parent.name,
                    "name": metadata_path.parent.name,
                    "error": str(exc),
                }
            )
    return tools


def load_tool(tool_id: str, library_root: Path | str | None = None) -> SavedTool:
    tool_id = normalize_tool_id(tool_id)
    root = ensure_library(library_root)
    tool_root = root / "tools" / tool_id
    metadata_path = tool_root / "tool.json"
    if not metadata_path.exists():
        raise FileNotFoundError(f"Saved tool not found: {tool_id}")
    metadata = _read_json(metadata_path)
    entrypoint = metadata.get("entrypoint", "code.cs")
    code_path = tool_root / entrypoint
    if not code_path.exists():
        raise FileNotFoundError(f"Saved tool code not found: {code_path}")
    return SavedTool(
        id=tool_id,
        root=tool_root,
        metadata=metadata,
        code=code_path.read_text(encoding="utf-8"),
    )


def delete_tool(tool_id: str, library_root: Path | str | None = None) -> dict[str, Any]:
    tool = load_tool(tool_id, library_root)
    trash_root = ensure_library(library_root) / "trash"
    trash_root.mkdir(parents=True, exist_ok=True)
    target = trash_root / f"{tool.id}-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"
    shutil.move(str(tool.root), str(target))
    return {"id": tool.id, "deleted": True, "movedTo": str(target)}


def build_runnable_code(saved_tool: SavedTool, parameter_values: dict[str, Any] | None = None) -> str:
    values = resolve_parameter_values(saved_tool.metadata.get("parameters", {}), parameter_values or {})
    prelude_lines = [
        f"// Saved Revit MCP tool: {saved_tool.id}",
        "// Parameter variables are injected by run_saved_tool.",
    ]
    for name, value in values.items():
        if not CS_IDENTIFIER_PATTERN.match(name):
            raise ValueError(f"Parameter name is not a valid C# identifier: {name}")
        prelude_lines.append(f"var {name} = {_to_csharp_literal(value)};")
    prelude = "\n".join(prelude_lines)
    return f"{prelude}\n\n{saved_tool.code.strip()}\n"


def resolve_parameter_values(schema: dict[str, Any], provided: dict[str, Any]) -> dict[str, Any]:
    values: dict[str, Any] = {}
    for name, spec in schema.items():
        if not CS_IDENTIFIER_PATTERN.match(name):
            raise ValueError(f"Parameter name is not a valid C# identifier: {name}")
        spec = spec or {}
        if name in provided:
            value = provided[name]
        elif "default" in spec:
            value = spec["default"]
        elif spec.get("required"):
            raise ValueError(f"Missing required parameter: {name}")
        else:
            continue
        values[name] = _coerce_parameter(name, spec, value)

    extra = sorted(set(provided) - set(schema))
    if extra:
        raise ValueError(f"Unknown parameter(s): {', '.join(extra)}")
    return values


def normalize_tool_id(tool_id: str) -> str:
    normalized = tool_id.strip().lower().replace(" ", "-")
    if not TOOL_ID_PATTERN.match(normalized):
        raise ValueError(
            "tool_id must start with a lowercase letter or number and contain only lowercase letters, numbers, '_' or '-'"
        )
    return normalized


def record_run(
    *,
    tool_id: str,
    parameters: dict[str, Any],
    result: str | None = None,
    error: str | None = None,
    library_root: Path | str | None = None,
) -> dict[str, Any]:
    root = ensure_library(library_root)
    now = datetime.now(timezone.utc)
    run = {
        "toolId": tool_id,
        "parameters": parameters,
        "result": result,
        "error": error,
        "ok": error is None,
        "timestamp": now.isoformat(),
    }
    run_dir = root / "runs" / f"{now:%Y}" / f"{now:%m}"
    run_dir.mkdir(parents=True, exist_ok=True)
    run_path = run_dir / f"{tool_id}-{now:%Y%m%d%H%M%S%f}.json"
    run_path.write_text(json.dumps(run, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    run["path"] = str(run_path)
    return run


def record_history(
    *,
    code: str,
    result: str | None = None,
    error: str | None = None,
    library_root: Path | str | None = None,
) -> dict[str, Any]:
    root = ensure_library(library_root)
    now = datetime.now(timezone.utc)
    history_id = f"hist-{now:%Y%m%d%H%M%S%f}"
    entry = {
        "id": history_id,
        "code": code,
        "result": result,
        "error": error,
        "ok": error is None,
        "timestamp": now.isoformat(),
    }
    history_dir = root / "history" / f"{now:%Y}" / f"{now:%m}"
    history_dir.mkdir(parents=True, exist_ok=True)
    history_path = history_dir / f"{now:%Y-%m-%d}.jsonl"
    with history_path.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(entry, sort_keys=True) + "\n")
    entry["path"] = str(history_path)
    return entry


def list_history(limit: int = 50, library_root: Path | str | None = None) -> list[dict[str, Any]]:
    root = ensure_library(library_root)
    history_root = root / "history"
    entries: list[dict[str, Any]] = []
    if not history_root.exists():
        return entries

    for path in sorted(history_root.glob("*/*/*.jsonl"), reverse=True):
        for line in reversed(path.read_text(encoding="utf-8").splitlines()):
            if not line.strip():
                continue
            entry = json.loads(line)
            if isinstance(entry, dict):
                entry["path"] = str(path)
                entries.append(entry)
                if len(entries) >= limit:
                    return entries
    return entries


def load_history_entry(history_id: str, library_root: Path | str | None = None) -> dict[str, Any]:
    for entry in list_history(limit=10_000, library_root=library_root):
        if entry.get("id") == history_id:
            return entry
    raise FileNotFoundError(f"History entry not found: {history_id}")


def promote_history_entry(
    *,
    history_id: str,
    tool_id: str,
    name: str,
    description: str,
    parameters: dict[str, Any] | None = None,
    version: str = "1.0.0",
    tags: list[str] | None = None,
    revit_versions: list[str] | None = None,
    requires_transaction: bool | None = None,
    library_root: Path | str | None = None,
    overwrite: bool = False,
) -> dict[str, Any]:
    entry = load_history_entry(history_id, library_root)
    code = str(entry.get("code") or "")
    if not code.strip():
        raise ValueError(f"History entry has no code: {history_id}")
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
        library_root=library_root,
        overwrite=overwrite,
    )


def validate_parameters(parameters: dict[str, Any]) -> dict[str, Any]:
    if not isinstance(parameters, dict):
        raise ValueError("parameters must be an object")
    validated: dict[str, Any] = {}
    for name, spec in parameters.items():
        if not CS_IDENTIFIER_PATTERN.match(name):
            raise ValueError(f"Parameter name is not a valid C# identifier: {name}")
        if spec is None:
            spec = {}
        if not isinstance(spec, dict):
            raise ValueError(f"Parameter spec must be an object: {name}")
        expected = spec.get("type", "any")
        if expected not in {"any", "string", "integer", "number", "boolean"}:
            raise ValueError(f"Unsupported parameter type for {name}: {expected}")
        validated[name] = dict(spec)
    return validated


def _snapshot_version(tool_root: Path) -> None:
    metadata_path = tool_root / "tool.json"
    code_path = tool_root / "code.cs"
    if not metadata_path.exists() or not code_path.exists():
        return
    metadata = _read_json(metadata_path)
    version = str(metadata.get("version") or datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S"))
    version_root = tool_root / "versions" / version
    if version_root.exists():
        version_root = tool_root / "versions" / f"{version}-{datetime.now(timezone.utc).strftime('%Y%m%d%H%M%S')}"
    version_root.mkdir(parents=True, exist_ok=True)
    shutil.copy2(metadata_path, version_root / "tool.json")
    shutil.copy2(code_path, version_root / "code.cs")


def _read_created_at(tool_root: Path) -> str | None:
    metadata_path = tool_root / "tool.json"
    if not metadata_path.exists():
        return None
    try:
        return str(_read_json(metadata_path).get("createdAt") or "")
    except (OSError, ValueError, json.JSONDecodeError):
        return None


def _read_json(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"Expected JSON object in {path}")
    return data


def _summary(metadata: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": metadata.get("id"),
        "name": metadata.get("name"),
        "description": metadata.get("description"),
        "version": metadata.get("version"),
        "parameters": metadata.get("parameters", {}),
        "tags": metadata.get("tags", []),
        "revitVersions": metadata.get("revitVersions", []),
        "requiresTransaction": metadata.get("requiresTransaction"),
        "updatedAt": metadata.get("updatedAt"),
    }


def _coerce_parameter(name: str, spec: dict[str, Any], value: Any) -> Any:
    expected = spec.get("type")
    if expected in {None, "any"}:
        return value
    if expected == "string":
        return str(value)
    if expected == "integer":
        if isinstance(value, bool):
            raise ValueError(f"Parameter {name} must be an integer")
        return int(value)
    if expected == "number":
        if isinstance(value, bool):
            raise ValueError(f"Parameter {name} must be a number")
        return float(value)
    if expected == "boolean":
        if isinstance(value, bool):
            return value
        if isinstance(value, str):
            lowered = value.strip().lower()
            if lowered in {"true", "1", "yes"}:
                return True
            if lowered in {"false", "0", "no"}:
                return False
        raise ValueError(f"Parameter {name} must be a boolean")
    raise ValueError(f"Unsupported parameter type for {name}: {expected}")


def _to_csharp_literal(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return repr(value)
    if isinstance(value, str):
        return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'
    raise ValueError(f"Unsupported C# literal value: {value!r}")


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()
