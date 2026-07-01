using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RevitMcpAddin;

public static class RevitMcpRuntime
{
    public const int HttpPort = 8000;
    public const int WebSocketPort = 8765;

    private static readonly object LockObject = new();

    public static string RevitVersion { get; private set; } = "";
    public static McpProcessManager McpProcess { get; } = new();
    public static NgrokProcessManager NgrokProcess { get; } = new();

    public static string AppDataDirectory
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RevitMcp");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");
    public static string RuntimePath => Path.Combine(AppDataDirectory, "runtime.json");
    public static string NgrokConfigPath => Path.Combine(AppDataDirectory, "ngrok.yml");
    public static string DefaultToolLibraryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Revit MCP");

    public static string LocalMcpUrl
    {
        get
        {
            return BuildLocalMcpUrl(LoadSettings());
        }
    }

    public static string BuildMcpPath(RevitMcpSettings settings)
    {
        return "/" + settings.EffectiveMcpAuthToken.Trim('/') + "/mcp";
    }

    public static string BuildLocalMcpUrl(RevitMcpSettings settings)
    {
        return "http://127.0.0.1:" + HttpPort + BuildMcpPath(settings);
    }

    public static string? BuildPublicMcpUrl(RevitMcpSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.NgrokDomain))
        {
            return null;
        }

        return "https://" + NormalizeDomain(settings.NgrokDomain) + BuildMcpPath(settings);
    }

    public static void Initialize(string revitVersion)
    {
        RevitVersion = revitVersion;
        EnsureSettings();
    }

    public static RevitMcpSettings LoadSettings()
    {
        lock (LockObject)
        {
            EnsureSettings();
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<RevitMcpSettings>(json) ?? RevitMcpSettings.CreateDefault();
            if (string.IsNullOrWhiteSpace(settings.ToolLibraryPath))
            {
                settings.ToolLibraryPath = DefaultToolLibraryPath;
            }

            return settings;
        }
    }

    public static void SaveSettings(RevitMcpSettings settings)
    {
        lock (LockObject)
        {
            Directory.CreateDirectory(AppDataDirectory);
            var json = JsonSerializer.Serialize(settings, RevitMcpSettings.JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
    }

    public static void OpenAppDataDirectory()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = AppDataDirectory,
            UseShellExecute = true
        });
    }

    public static RevitMcpRuntimeState LoadRuntimeState()
    {
        lock (LockObject)
        {
            if (!File.Exists(RuntimePath))
            {
                return new RevitMcpRuntimeState();
            }

            try
            {
                var json = File.ReadAllText(RuntimePath);
                return JsonSerializer.Deserialize<RevitMcpRuntimeState>(json, RevitMcpSettings.JsonOptions) ?? new RevitMcpRuntimeState();
            }
            catch
            {
                return new RevitMcpRuntimeState();
            }
        }
    }

    public static void SaveRuntimeState(RevitMcpRuntimeState state)
    {
        lock (LockObject)
        {
            Directory.CreateDirectory(AppDataDirectory);
            var json = JsonSerializer.Serialize(state, RevitMcpSettings.JsonOptions);
            File.WriteAllText(RuntimePath, json);
        }
    }

    public static void ClearRuntimeState()
    {
        lock (LockObject)
        {
            if (File.Exists(RuntimePath))
            {
                File.Delete(RuntimePath);
            }
        }
    }

    public static string NormalizeDomain(string value)
    {
        var domain = value.Trim();
        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            domain = domain.Substring("https://".Length);
        }
        else if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            domain = domain.Substring("http://".Length);
        }

        return domain.Trim().TrimEnd('/');
    }

    private static void EnsureSettings()
    {
        if (File.Exists(SettingsPath))
        {
            return;
        }

        SaveSettings(RevitMcpSettings.CreateDefault());
    }
}

public sealed class RevitMcpSettings
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string NgrokAuthToken { get; set; } = "";
    public string NgrokDomain { get; set; } = "";
    public string McpAuthToken { get; set; } = "";
    public string ToolLibraryPath { get; set; } = RevitMcpRuntime.DefaultToolLibraryPath;

    public string EffectiveMcpAuthToken
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(McpAuthToken))
            {
                return McpAuthToken.Trim();
            }

            McpAuthToken = Guid.NewGuid().ToString("N");
            return McpAuthToken;
        }
    }

    public static RevitMcpSettings CreateDefault()
    {
        return new RevitMcpSettings
        {
            McpAuthToken = Guid.NewGuid().ToString("N"),
            ToolLibraryPath = RevitMcpRuntime.DefaultToolLibraryPath
        };
    }
}

public sealed class RevitMcpRuntimeState
{
    public int ServerProcessId { get; set; }
    public string ServerPath { get; set; } = "";
    public DateTimeOffset ServerStartedAt { get; set; }
    public int NgrokProcessId { get; set; }
    public string NgrokPath { get; set; } = "";
    public DateTimeOffset NgrokStartedAt { get; set; }
}
