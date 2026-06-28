using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace RevitMcpAddin;

public static class RevitMcpRuntime
{
    public const string LocalMcpUrl = "http://127.0.0.1:8000/mcp";
    public const int HttpPort = 8000;
    public const int WebSocketPort = 8765;

    private static readonly object LockObject = new();

    public static string RevitVersion { get; private set; } = "";
    public static McpProcessManager McpProcess { get; } = new();

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
            return JsonSerializer.Deserialize<RevitMcpSettings>(json) ?? RevitMcpSettings.CreateDefault();
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

    public static RevitMcpSettings CreateDefault()
    {
        return new RevitMcpSettings
        {
            McpAuthToken = Guid.NewGuid().ToString("N")
        };
    }
}
