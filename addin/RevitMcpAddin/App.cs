using System.Collections.Concurrent;
using System.Reflection;
using Autodesk.Revit.UI;

namespace RevitMcpAddin;

public sealed class App : IExternalApplication
{
    private readonly ConcurrentQueue<RevitCommandRequest> _commandQueue = new();
    private McpExternalEventHandler? _handler;
    private ExternalEvent? _externalEvent;
    private McpWebSocketClient? _webSocketClient;

    public Result OnStartup(UIControlledApplication application)
    {
        McpLog.Info("Starting Revit MCP add-in.");
        RevitMcpRuntime.Initialize(application.ControlledApplication.VersionNumber);
        CreateRibbon(application);

        _handler = new McpExternalEventHandler(_commandQueue);
        _externalEvent = ExternalEvent.Create(_handler);
        _webSocketClient = new McpWebSocketClient(_commandQueue, _externalEvent);
        _handler.SetWebSocketClient(_webSocketClient);
        _webSocketClient.Start();

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        McpLog.Info("Stopping Revit MCP add-in.");
        RevitMcpRuntime.NgrokProcess.StopOnShutdown();
        RevitMcpRuntime.McpProcess.StopOnShutdown();
        _webSocketClient?.Dispose();
        _externalEvent?.Dispose();
        return Result.Succeeded;
    }

    private static void CreateRibbon(UIControlledApplication application)
    {
        const string tabName = "Revit MCP";
        const string panelName = "Connection";

        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch
        {
            // The tab may already exist if another Revit MCP component created it.
        }

        var panel = application.CreateRibbonPanel(tabName, panelName);
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        panel.AddItem(Button("StartMcp", "Start\nMCP", "Start MCP", "start", assemblyPath, typeof(StartMcpCommand)));
        panel.AddItem(Button("StopMcp", "Stop\nMCP", "Stop MCP", "stop", assemblyPath, typeof(StopMcpCommand)));
        panel.AddSeparator();
        panel.AddItem(Button("StartPublicUrl", "Start\nPublic URL", "Start Public URL", "public", assemblyPath, typeof(StartPublicUrlCommand)));
        panel.AddItem(Button("StopPublicUrl", "Stop\nPublic URL", "Stop Public URL", "stop", assemblyPath, typeof(StopPublicUrlCommand)));
        panel.AddSeparator();
        panel.AddItem(Button("CopyLocalUrl", "Copy\nLocal URL", "Copy Local URL", "copy", assemblyPath, typeof(CopyLocalUrlCommand)));
        panel.AddItem(Button("CopyPublicUrl", "Copy\nPublic URL", "Copy Public URL", "copy", assemblyPath, typeof(CopyPublicUrlCommand)));
        panel.AddItem(Button("Status", "Status", "Status", "status", assemblyPath, typeof(ShowStatusCommand)));
        panel.AddSeparator();
        panel.AddItem(Button("Settings", "Settings", "Settings", "settings", assemblyPath, typeof(SettingsCommand)));
        panel.AddItem(Button("OpenLogs", "Open\nLogs", "Open Logs", "logs", assemblyPath, typeof(OpenLogsCommand)));
    }

    private static PushButtonData Button(string name, string text, string toolTip, string icon, string assemblyPath, Type commandType)
    {
        var commandName = commandType.FullName ?? throw new InvalidOperationException($"Command type has no full name: {commandType}");
        return new PushButtonData(name, text, assemblyPath, commandName)
        {
            ToolTip = toolTip,
            Image = RibbonIconFactory.Create(icon),
            LargeImage = RibbonIconFactory.Create(icon)
        };
    }
}
