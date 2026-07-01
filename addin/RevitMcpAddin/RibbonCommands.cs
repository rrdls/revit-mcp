using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMcpAddin;

[Transaction(TransactionMode.Manual)]
public sealed class StartMcpCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var result = RevitMcpRuntime.McpProcess.Start();
        if (result.Ok)
        {
            App.StartWebSocketClient();
        }

        RevitTaskDialogs.Show("Start MCP", result.Message);
        return result.Ok ? Result.Succeeded : Result.Failed;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class StopMcpCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        App.StopWebSocketClient();
        var result = RevitMcpRuntime.McpProcess.Stop();
        RevitTaskDialogs.Show("Stop MCP", result.Message);
        return result.Ok ? Result.Succeeded : Result.Failed;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class StartPublicUrlCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var result = RevitMcpRuntime.NgrokProcess.Start();
        RevitTaskDialogs.Show("Start Public URL", result.Message);
        return result.Ok ? Result.Succeeded : Result.Failed;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class StopPublicUrlCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var result = RevitMcpRuntime.NgrokProcess.Stop();
        RevitTaskDialogs.Show("Stop Public URL", result.Message);
        return result.Ok ? Result.Succeeded : Result.Failed;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class CopyLocalUrlCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        Clipboard.SetText(RevitMcpRuntime.LocalMcpUrl);
        RevitTaskDialogs.Show("Copy Local URL", $"Copied local MCP URL:{Environment.NewLine}{RevitMcpRuntime.LocalMcpUrl}");
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class CopyPublicUrlCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var settings = RevitMcpRuntime.LoadSettings();
        var publicUrl = RevitMcpRuntime.BuildPublicMcpUrl(settings);
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            RevitTaskDialogs.Show("Copy Public URL", "Configure your fixed ngrok domain in Settings first.");
            return Result.Failed;
        }

        Clipboard.SetText(publicUrl);
        RevitTaskDialogs.Show("Copy Public URL", $"Copied public MCP URL:{Environment.NewLine}{publicUrl}");
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class ShowStatusCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var settings = RevitMcpRuntime.LoadSettings();
        var publicUrl = string.IsNullOrWhiteSpace(settings.NgrokDomain)
            ? "Not configured"
            : RevitMcpRuntime.BuildPublicMcpUrl(settings);

        RevitTaskDialogs.Show(
            "Revit MCP Status",
            $"{RevitMcpRuntime.McpProcess.Status}{Environment.NewLine}{Environment.NewLine}" +
            $"{RevitMcpRuntime.NgrokProcess.Status}{Environment.NewLine}{Environment.NewLine}" +
            $"Local URL: {RevitMcpRuntime.LocalMcpUrl}{Environment.NewLine}" +
            $"Public URL: {publicUrl}{Environment.NewLine}" +
            $"Tool library: {settings.ToolLibraryPath}{Environment.NewLine}" +
            $"Settings: {RevitMcpRuntime.SettingsPath}");
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class OpenLogsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        RevitMcpRuntime.OpenAppDataDirectory();
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var window = new SettingsWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };
        window.ShowDialog();
        return Result.Succeeded;
    }
}

[Transaction(TransactionMode.Manual)]
public sealed class SavedToolsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var window = new SavedToolsWindow(commandData.Application)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Topmost = true
        };
        window.ShowDialog();
        return Result.Succeeded;
    }
}

internal static class RevitTaskDialogs
{
    public static void Show(string title, string message)
    {
        Autodesk.Revit.UI.TaskDialog.Show(title, message);
    }
}
