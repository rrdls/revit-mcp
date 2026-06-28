using System.Diagnostics;
using System.IO;

namespace RevitMcpAddin;

public sealed class NgrokProcessManager
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
            var state = RevitMcpRuntime.LoadRuntimeState();
            if (StartedByAddin)
            {
                return "Public URL is running from this Revit session.";
            }

            if (state.NgrokProcessId > 0 && ProcessIsRunning(state.NgrokProcessId))
            {
                return $"Public URL appears to be running on PID {state.NgrokProcessId}.";
            }

            return "Public URL is stopped.";
        }
    }

    public McpProcessResult Start()
    {
        lock (_lockObject)
        {
            if (_process is { HasExited: false })
            {
                return McpProcessResult.Success("Public URL is already running from this Revit session.");
            }

            if (!McpProcessManager.PortIsOpen("127.0.0.1", RevitMcpRuntime.HttpPort))
            {
                return McpProcessResult.Failure("Start MCP before starting the public URL.");
            }

            var settings = RevitMcpRuntime.LoadSettings();
            if (string.IsNullOrWhiteSpace(settings.NgrokAuthToken))
            {
                return McpProcessResult.Failure("Configure your ngrok authtoken in Settings first.");
            }

            if (string.IsNullOrWhiteSpace(settings.NgrokDomain))
            {
                return McpProcessResult.Failure("Configure your fixed ngrok domain in Settings first.");
            }

            var ngrokPath = FindNgrokExecutable();
            if (ngrokPath is null)
            {
                return McpProcessResult.Failure("Could not find ngrok.exe. Rebuild the release package or place ngrok.exe in %LOCALAPPDATA%\\RevitMcp\\app.");
            }

            CleanupStaleRuntimeProcess(ngrokPath);

            var domain = RevitMcpRuntime.NormalizeDomain(settings.NgrokDomain);
            WriteNgrokConfig(settings.NgrokAuthToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = ngrokPath,
                Arguments = $"http {RevitMcpRuntime.HttpPort} --url=https://{domain} --config \"{RevitMcpRuntime.NgrokConfigPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, args) => LogProcessLine(args.Data);
            process.ErrorDataReceived += (_, args) => LogProcessLine(args.Data);
            process.Exited += (_, _) => McpLog.Info($"ngrok.exe exited with code {process.ExitCode}.");

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                _process = process;

                var runtimeState = RevitMcpRuntime.LoadRuntimeState();
                runtimeState.NgrokProcessId = process.Id;
                runtimeState.NgrokPath = ngrokPath;
                runtimeState.NgrokStartedAt = DateTimeOffset.Now;
                RevitMcpRuntime.SaveRuntimeState(runtimeState);

                McpLog.Info($"Started ngrok process {process.Id}: {ngrokPath}");
                return McpProcessResult.Success("Public URL started: " + RevitMcpRuntime.BuildPublicMcpUrl(settings));
            }
            catch (Exception ex)
            {
                process.Dispose();
                McpLog.Error("Could not start ngrok.", ex);
                return McpProcessResult.Failure($"Could not start public URL: {ex.Message}");
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
                var state = RevitMcpRuntime.LoadRuntimeState();
                if (TryStopRuntimeProcess(state.NgrokProcessId, state.NgrokPath))
                {
                    ClearNgrokRuntimeState();
                    return McpProcessResult.Success($"Stopped ngrok process PID {state.NgrokProcessId}.");
                }

                ClearNgrokRuntimeState();
                return McpProcessResult.Success("No public URL process started by this Revit session was found.");
            }

            try
            {
                _process.Kill();
                _process.WaitForExit(3000);
                _process.Dispose();
                _process = null;
                ClearNgrokRuntimeState();
                McpLog.Info("Stopped ngrok process started by this Revit session.");
                return McpProcessResult.Success("Public URL stopped.");
            }
            catch (Exception ex)
            {
                McpLog.Error("Could not stop ngrok.", ex);
                return McpProcessResult.Failure($"Could not stop public URL: {ex.Message}");
            }
        }
    }

    public void StopOnShutdown()
    {
        lock (_lockObject)
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    _process.Kill();
                    _process.WaitForExit(1500);
                    McpLog.Info("Stopped ngrok during Revit shutdown.");
                }
                catch (Exception ex)
                {
                    McpLog.Error("Could not stop ngrok during Revit shutdown.", ex);
                }
                finally
                {
                    _process.Dispose();
                    _process = null;
                    ClearNgrokRuntimeState();
                }
            }
        }
    }

    private static string? FindNgrokExecutable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(localAppData, "RevitMcp", "app", "ngrok.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ngrok.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void WriteNgrokConfig(string authtoken)
    {
        Directory.CreateDirectory(RevitMcpRuntime.AppDataDirectory);
        var lines = new[]
        {
            "version: 3",
            "agent:",
            "  authtoken: " + authtoken.Trim()
        };
        File.WriteAllLines(RevitMcpRuntime.NgrokConfigPath, lines);
    }

    private static void CleanupStaleRuntimeProcess(string expectedNgrokPath)
    {
        var state = RevitMcpRuntime.LoadRuntimeState();
        if (state.NgrokProcessId <= 0)
        {
            return;
        }

        var process = GetProcessById(state.NgrokProcessId);
        if (process is null)
        {
            ClearNgrokRuntimeState();
            return;
        }

        using (process)
        {
            if (!SamePath(SafeProcessPath(process), expectedNgrokPath))
            {
                return;
            }

            try
            {
                process.Kill();
                process.WaitForExit(2000);
                McpLog.Info($"Stopped stale ngrok process PID {state.NgrokProcessId}.");
                ClearNgrokRuntimeState();
            }
            catch (Exception ex)
            {
                McpLog.Error($"Could not stop stale ngrok process PID {state.NgrokProcessId}.", ex);
            }
        }
    }

    private static bool TryStopRuntimeProcess(int processId, string expectedPath)
    {
        if (processId <= 0)
        {
            return false;
        }

        var process = GetProcessById(processId);
        if (process is null)
        {
            return false;
        }

        using (process)
        {
            if (!SamePath(SafeProcessPath(process), expectedPath))
            {
                return false;
            }

            try
            {
                process.Kill();
                process.WaitForExit(3000);
                return true;
            }
            catch (Exception ex)
            {
                McpLog.Error($"Could not stop runtime ngrok process PID {processId}.", ex);
                return false;
            }
        }
    }

    private static bool ProcessIsRunning(int processId)
    {
        var process = GetProcessById(processId);
        if (process is null)
        {
            return false;
        }

        process.Dispose();
        return true;
    }

    private static Process? GetProcessById(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool SamePath(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left).TrimEnd('\\'),
            Path.GetFullPath(right).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearNgrokRuntimeState()
    {
        var state = RevitMcpRuntime.LoadRuntimeState();
        state.NgrokProcessId = 0;
        state.NgrokPath = "";
        state.NgrokStartedAt = default;
        RevitMcpRuntime.SaveRuntimeState(state);
    }

    private static void LogProcessLine(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            McpLog.Info("[ngrok] " + line);
        }
    }
}
