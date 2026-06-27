from __future__ import annotations

import os
import queue
import re
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk


HTTP_PORT = "8000"
HTTP_PATH = "/mcp"
REVIT_WS_PORT = "8765"


class ProcessPump:
    def __init__(self, command: list[str], env: dict[str, str], output: "queue.Queue[str]") -> None:
        self.command = command
        self.env = env
        self.output = output
        self.process: subprocess.Popen[str] | None = None
        self.thread: threading.Thread | None = None

    def start(self) -> None:
        if self.process and self.process.poll() is None:
            return

        self.process = subprocess.Popen(
            self.command,
            env=self.env,
            stdout=subprocess.PIPE,
            stderr=subprocess.STDOUT,
            text=True,
            bufsize=1,
            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
        )
        self.thread = threading.Thread(target=self._read_output, daemon=True)
        self.thread.start()

    def stop(self) -> None:
        if self.process and self.process.poll() is None:
            self.process.terminate()

    def running(self) -> bool:
        return self.process is not None and self.process.poll() is None

    def _read_output(self) -> None:
        if not self.process or not self.process.stdout:
            return
        for line in self.process.stdout:
            self.output.put(line.rstrip())


class Launcher(tk.Tk):
    def __init__(self) -> None:
        super().__init__()
        self.title("Revit MCP")
        self.geometry("720x440")
        self.minsize(640, 380)

        self.output: "queue.Queue[str]" = queue.Queue()
        self.server: ProcessPump | None = None
        self.tunnel: ProcessPump | None = None

        self.server_status = tk.StringVar(value="MCP server: stopped")
        self.tunnel_status = tk.StringVar(value="Tunnel: stopped")
        self.public_url = tk.StringVar(value="")
        self.tunnel_kind = tk.StringVar(value="cloudflared")

        self._build_ui()
        self.after(200, self._drain_output)

    def _build_ui(self) -> None:
        root = ttk.Frame(self, padding=16)
        root.pack(fill=tk.BOTH, expand=True)

        ttk.Label(root, text="Revit MCP", font=("Segoe UI", 18, "bold")).pack(anchor=tk.W)
        ttk.Label(root, text="Start the local MCP server, create a tunnel, then copy the /mcp URL into ChatGPT.").pack(
            anchor=tk.W, pady=(2, 14)
        )

        status = ttk.Frame(root)
        status.pack(fill=tk.X)
        ttk.Label(status, textvariable=self.server_status).pack(side=tk.LEFT)
        ttk.Label(status, textvariable=self.tunnel_status).pack(side=tk.LEFT, padx=(24, 0))

        controls = ttk.Frame(root)
        controls.pack(fill=tk.X, pady=12)
        ttk.Button(controls, text="Start MCP", command=self.start_server).pack(side=tk.LEFT)
        ttk.Button(controls, text="Stop MCP", command=self.stop_server).pack(side=tk.LEFT, padx=(8, 0))

        ttk.Separator(controls, orient=tk.VERTICAL).pack(side=tk.LEFT, fill=tk.Y, padx=14)
        ttk.Radiobutton(controls, text="Cloudflare", variable=self.tunnel_kind, value="cloudflared").pack(side=tk.LEFT)
        ttk.Radiobutton(controls, text="ngrok", variable=self.tunnel_kind, value="ngrok").pack(side=tk.LEFT, padx=(8, 0))
        ttk.Button(controls, text="Start Tunnel", command=self.start_tunnel).pack(side=tk.LEFT, padx=(12, 0))
        ttk.Button(controls, text="Stop Tunnel", command=self.stop_tunnel).pack(side=tk.LEFT, padx=(8, 0))

        url_row = ttk.Frame(root)
        url_row.pack(fill=tk.X, pady=(4, 10))
        ttk.Label(url_row, text="ChatGPT MCP URL:").pack(anchor=tk.W)
        url_entry = ttk.Entry(url_row, textvariable=self.public_url)
        url_entry.pack(side=tk.LEFT, fill=tk.X, expand=True, pady=(4, 0))
        ttk.Button(url_row, text="Copy", command=self.copy_url).pack(side=tk.LEFT, padx=(8, 0), pady=(4, 0))

        ttk.Label(root, text="Log:").pack(anchor=tk.W)
        self.log = tk.Text(root, height=12, wrap=tk.WORD)
        self.log.pack(fill=tk.BOTH, expand=True)

        bottom = ttk.Frame(root)
        bottom.pack(fill=tk.X, pady=(10, 0))
        ttk.Button(bottom, text="Open Logs Folder", command=self.open_logs_folder).pack(side=tk.LEFT)
        ttk.Button(bottom, text="Quit", command=self.destroy).pack(side=tk.RIGHT)

    def start_server(self) -> None:
        if self.server and self.server.running():
            return

        env = os.environ.copy()
        env.update(
            {
                "MCP_TRANSPORT": "streamable-http",
                "MCP_HTTP_HOST": "127.0.0.1",
                "MCP_HTTP_PORT": HTTP_PORT,
                "MCP_HTTP_PATH": HTTP_PATH,
                "MCP_DISABLE_DNS_REBINDING_PROTECTION": "true",
                "REVIT_MCP_HOST": "127.0.0.1",
                "REVIT_MCP_PORT": REVIT_WS_PORT,
            }
        )

        if getattr(sys, "frozen", False):
            server_exe = Path(sys.executable).with_name("RevitMcpServer.exe")
            command = [str(server_exe)]
        else:
            command = [sys.executable, "-m", "revit_mcp.server"]

        self.server = ProcessPump(command, env, self.output)
        self.server.start()
        self.server_status.set("MCP server: starting")

    def stop_server(self) -> None:
        if self.server:
            self.server.stop()
        self.server_status.set("MCP server: stopped")

    def start_tunnel(self) -> None:
        if self.tunnel and self.tunnel.running():
            return

        tool = self.tunnel_kind.get()
        if tool == "cloudflared":
            command = [self._tool_path("cloudflared.exe", "cloudflared"), "tunnel", "--url", f"http://127.0.0.1:{HTTP_PORT}"]
        else:
            command = [self._tool_path("ngrok.exe", "ngrok"), "http", HTTP_PORT]

        self.tunnel = ProcessPump(command, os.environ.copy(), self.output)
        try:
            self.tunnel.start()
            self.tunnel_status.set(f"Tunnel: starting ({tool})")
        except FileNotFoundError:
            messagebox.showerror("Tunnel tool not found", f"Could not find {tool}. Install it or place it next to this app.")

    def stop_tunnel(self) -> None:
        if self.tunnel:
            self.tunnel.stop()
        self.tunnel_status.set("Tunnel: stopped")

    def copy_url(self) -> None:
        value = self.public_url.get()
        if not value:
            return
        self.clipboard_clear()
        self.clipboard_append(value)

    def open_logs_folder(self) -> None:
        folder = Path(os.getenv("LOCALAPPDATA", str(Path.home()))) / "RevitMcp"
        folder.mkdir(parents=True, exist_ok=True)
        if os.name == "nt":
            os.startfile(folder)  # type: ignore[attr-defined]

    def _drain_output(self) -> None:
        while True:
            try:
                line = self.output.get_nowait()
            except queue.Empty:
                break
            self._handle_line(line)
        self.after(200, self._drain_output)

    def _handle_line(self, line: str) -> None:
        self.log.insert(tk.END, line + "\n")
        self.log.see(tk.END)

        if "Uvicorn running on" in line or "Application startup complete" in line:
            self.server_status.set("MCP server: running")
        if "Revit add-in connected" in line:
            self.server_status.set("MCP server: running, Revit connected")

        url = self._extract_public_url(line)
        if url:
            self.public_url.set(url.rstrip("/") + HTTP_PATH)
            self.tunnel_status.set("Tunnel: running")

    def _extract_public_url(self, line: str) -> str | None:
        match = re.search(r"https://[a-zA-Z0-9.-]+(?:trycloudflare\.com|ngrok-free\.app)", line)
        if match:
            return match.group(0)
        return None

    def _tool_path(self, exe_name: str, fallback: str) -> str:
        base = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path.cwd()
        candidate = base / exe_name
        return str(candidate) if candidate.exists() else fallback


def main() -> None:
    app = Launcher()
    app.mainloop()


if __name__ == "__main__":
    main()

