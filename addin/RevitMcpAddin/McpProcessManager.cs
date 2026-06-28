using System.Diagnostics;
using System.IO;
using System.Net.Sockets;

namespace RevitMcpAddin;

public sealed class McpProcessManager
{
    private readonly object _lockObject = new();
    private Process? _process;

    public bool StartedByAddin
    {
        get
        {
            lock (_lockObject)
            {
                return _process is { HasExited: false };
            }
        }
    }

    public string Status
    {
        get
        {
            var httpOpen = IsPortOpen("127.0.0.1", RevitMcpRuntime.HttpPort);
            var wsOpen = IsPortOpen("127.0.0.1", RevitMcpRuntime.WebSocketPort);

            if (httpOpen && wsOpen)
            {
                return StartedByAddin ? "MCP server is running and was started by this Revit session." : "MCP server appears to be running.";
            }

            if (httpOpen || wsOpen)
            {
                return $"Partial MCP server state. HTTP {OpenClosed(httpOpen)}, WebSocket {OpenClosed(wsOpen)}.";
            }

            return "MCP server is stopped.";
        }
    }

    public McpProcessResult Start()
    {
        lock (_lockObject)
        {
            if (_process is { HasExited: false })
            {
                return McpProcessResult.Success("MCP server is already running from this Revit session.");
            }

            var httpOpen = IsPortOpen("127.0.0.1", RevitMcpRuntime.HttpPort);
            var wsOpen = IsPortOpen("127.0.0.1", RevitMcpRuntime.WebSocketPort);
            if (httpOpen && wsOpen)
            {
                return McpProcessResult.Success("MCP server appears to already be running.");
            }

            if (httpOpen || wsOpen)
            {
                return McpProcessResult.Failure($"Cannot start MCP server because ports are partially busy. HTTP {OpenClosed(httpOpen)}, WebSocket {OpenClosed(wsOpen)}.");
            }

            var serverPath = FindServerExecutable();
            if (serverPath is null)
            {
                return McpProcessResult.Failure("Could not find RevitMcpServer.exe. Reinstall Revit MCP or build the release package.");
            }

            var settings = RevitMcpRuntime.LoadSettings();
            var startInfo = new ProcessStartInfo
            {
                FileName = serverPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.EnvironmentVariables["MCP_TRANSPORT"] = "streamable-http";
            startInfo.EnvironmentVariables["MCP_HTTP_HOST"] = "127.0.0.1";
            startInfo.EnvironmentVariables["MCP_HTTP_PORT"] = RevitMcpRuntime.HttpPort.ToString();
            startInfo.EnvironmentVariables["MCP_HTTP_PATH"] = "/mcp";
            startInfo.EnvironmentVariables["MCP_DISABLE_DNS_REBINDING_PROTECTION"] = "true";
            startInfo.EnvironmentVariables["REVIT_MCP_HOST"] = "127.0.0.1";
            startInfo.EnvironmentVariables["REVIT_MCP_PORT"] = RevitMcpRuntime.WebSocketPort.ToString();
            startInfo.EnvironmentVariables["REVIT_MCP_TOKEN"] = settings.McpAuthToken;

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, args) => LogProcessLine(args.Data);
            process.ErrorDataReceived += (_, args) => LogProcessLine(args.Data);
            process.Exited += (_, _) => McpLog.Info($"RevitMcpServer.exe exited with code {process.ExitCode}.");

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;
                McpLog.Info($"Started MCP server process {process.Id}: {serverPath}");
                return McpProcessResult.Success("MCP server started.");
            }
            catch (Exception ex)
            {
                process.Dispose();
                McpLog.Error("Could not start MCP server.", ex);
                return McpProcessResult.Failure($"Could not start MCP server: {ex.Message}");
            }
        }
    }

    public McpProcessResult Stop()
    {
        lock (_lockObject)
        {
            if (_process is null || _process.HasExited)
            {
                _process?.Dispose();
                _process = null;
                return McpProcessResult.Success("No MCP server process started by this Revit session was found.");
            }

            try
            {
                _process.Kill();
                _process.WaitForExit(3000);
                McpLog.Info("Stopped MCP server process started by this Revit session.");
                _process.Dispose();
                _process = null;
                return McpProcessResult.Success("MCP server stopped.");
            }
            catch (Exception ex)
            {
                McpLog.Error("Could not stop MCP server.", ex);
                return McpProcessResult.Failure($"Could not stop MCP server: {ex.Message}");
            }
        }
    }

    private static string? FindServerExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "RevitMcp", "app", "RevitMcpServer.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RevitMcpServer.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(350));
            if (!success)
            {
                return false;
            }

            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string OpenClosed(bool open)
    {
        return open ? "open" : "closed";
    }

    private static void LogProcessLine(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            McpLog.Info("[server] " + line);
        }
    }
}

public sealed class McpProcessResult
{
    public McpProcessResult(bool ok, string message)
    {
        Ok = ok;
        Message = message;
    }

    public bool Ok { get; }
    public string Message { get; }

    public static McpProcessResult Success(string message)
    {
        return new McpProcessResult(true, message);
    }

    public static McpProcessResult Failure(string message)
    {
        return new McpProcessResult(false, message);
    }
}
