from __future__ import annotations

import json
from pathlib import Path

import pytest

import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "python"))

from revit_mcp.saved_tools import (
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


def test_save_list_and_load_tool(tmp_path: Path) -> None:
    root = ensure_library(tmp_path / "Revit MCP")

    metadata = save_tool(
        tool_id="renumber-sheets",
        name="Renumber Sheets",
        description="Renumber project sheets.",
        code='return prefix + startNumber.ToString();',
        parameters={
            "prefix": {"type": "string", "default": "ARQ-"},
            "startNumber": {"type": "integer", "required": True},
        },
        tags=["sheets"],
        revit_versions=["2025"],
        library_root=root,
    )

    assert metadata["id"] == "renumber-sheets"
    assert (root / "tools" / "renumber-sheets" / "tool.json").exists()
    assert (root / "tools" / "renumber-sheets" / "code.cs").exists()

    tools = list_tools(root)
    assert [tool["id"] for tool in tools] == ["renumber-sheets"]

    saved = load_tool("renumber-sheets", root)
    assert saved.metadata["name"] == "Renumber Sheets"
    assert saved.code.strip() == 'return prefix + startNumber.ToString();'


def test_build_runnable_code_injects_typed_parameters(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)
    save_tool(
        tool_id="make-view",
        name="Make View",
        description="Creates a named view.",
        code='return viewName + ":" + includeDrafting.ToString() + ":" + scale.ToString();',
        parameters={
            "viewName": {"type": "string", "required": True},
            "includeDrafting": {"type": "boolean", "default": False},
            "scale": {"type": "number", "default": 1.5},
        },
        library_root=root,
    )

    code = build_runnable_code(
        load_tool("make-view", root),
        {"viewName": 'A "quoted" view', "includeDrafting": "true"},
    )

    assert 'var viewName = "A \\"quoted\\" view";' in code
    assert "var includeDrafting = true;" in code
    assert "var scale = 1.5;" in code
    assert "return viewName" in code


def test_save_tool_overwrite_snapshots_previous_version(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)
    save_tool(
        tool_id="audit-levels",
        name="Audit Levels",
        description="First version.",
        code='return "v1";',
        version="1.0.0",
        library_root=root,
    )
    save_tool(
        tool_id="audit-levels",
        name="Audit Levels",
        description="Second version.",
        code='return "v2";',
        version="1.1.0",
        library_root=root,
        overwrite=True,
    )

    assert (root / "tools" / "audit-levels" / "versions" / "1.0.0" / "code.cs").read_text(
        encoding="utf-8"
    ).strip() == 'return "v1";'
    assert load_tool("audit-levels", root).code.strip() == 'return "v2";'


def test_required_and_unknown_parameters_are_rejected(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)
    save_tool(
        tool_id="needs-param",
        name="Needs Param",
        description="Requires a parameter.",
        code="return value;",
        parameters={"value": {"type": "string", "required": True}},
        library_root=root,
    )

    with pytest.raises(ValueError, match="Missing required parameter"):
        build_runnable_code(load_tool("needs-param", root), {})

    with pytest.raises(ValueError, match="Unknown parameter"):
        build_runnable_code(load_tool("needs-param", root), {"value": "ok", "extra": "no"})


def test_record_run_and_delete_tool(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)
    save_tool(
        tool_id="smoke-test",
        name="Smoke Test",
        description="Smoke test tool.",
        code='return "ok";',
        library_root=root,
    )

    run = record_run(tool_id="smoke-test", parameters={}, result="ok", library_root=root)
    run_path = Path(run["path"])
    assert run_path.exists()
    assert json.loads(run_path.read_text(encoding="utf-8"))["ok"] is True

    deleted = delete_tool("smoke-test", root)
    assert deleted["deleted"] is True
    assert not (root / "tools" / "smoke-test").exists()
    assert Path(deleted["movedTo"]).exists()


def test_record_history_appends_jsonl(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)

    first = record_history(code="return doc.Title;", result="Project1", library_root=root)
    second = record_history(code='return "bad";', error="failed", library_root=root)

    assert first["path"] == second["path"]
    lines = Path(first["path"]).read_text(encoding="utf-8").splitlines()
    assert len(lines) == 2
    assert json.loads(lines[0])["result"] == "Project1"
    assert json.loads(lines[1])["ok"] is False

    history = list_history(library_root=root)
    assert [entry["id"] for entry in history] == [second["id"], first["id"]]


def test_promote_history_entry_to_tool(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)
    entry = record_history(code='return "promoted";', result="promoted", library_root=root)

    promoted = promote_history_entry(
        history_id=entry["id"],
        tool_id="promoted-tool",
        name="Promoted Tool",
        description="Created from history.",
        library_root=root,
    )

    assert promoted["id"] == "promoted-tool"
    assert load_tool("promoted-tool", root).code.strip() == 'return "promoted";'


def test_invalid_parameter_schema_is_rejected(tmp_path: Path) -> None:
    root = ensure_library(tmp_path)

    with pytest.raises(ValueError, match="Unsupported parameter type"):
        save_tool(
            tool_id="bad-param",
            name="Bad Param",
            description="Invalid schema.",
            code='return "bad";',
            parameters={"value": {"type": "array"}},
            library_root=root,
        )
