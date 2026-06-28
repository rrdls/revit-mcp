using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace RevitMcpAddin;

public sealed class SettingsWindow : Window
{
    private readonly TextBox _ngrokAuthToken = new();
    private readonly TextBox _ngrokDomain = new();
    private readonly TextBox _mcpAuthToken = new();

    public SettingsWindow()
    {
        Title = "Revit MCP Settings";
        Width = 560;
        Height = 330;
        MinWidth = 480;
        MinHeight = 300;

        var settings = RevitMcpRuntime.LoadSettings();
        _ngrokAuthToken.Text = settings.NgrokAuthToken;
        _ngrokDomain.Text = settings.NgrokDomain;
        _mcpAuthToken.Text = settings.McpAuthToken;

        Content = BuildContent();
    }

    private UIElement BuildContent()
    {
        var root = new DockPanel
        {
            Margin = new Thickness(16)
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        var save = new Button
        {
            Content = "Save",
            MinWidth = 88,
            Margin = new Thickness(0, 16, 8, 0)
        };
        save.Click += (_, _) => Save();

        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 88,
            Margin = new Thickness(0, 16, 0, 0)
        };
        cancel.Click += (_, _) => Close();

        buttons.Children.Add(save);
        buttons.Children.Add(cancel);
        root.Children.Add(buttons);

        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddRow(form, 0, "ngrok authtoken", _ngrokAuthToken);
        AddRow(form, 1, "ngrok domain", _ngrokDomain);
        AddRow(form, 2, "MCP auth token", _mcpAuthToken);

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 16, 0, 0)
        };
        Grid.SetColumn(tools, 1);
        Grid.SetRow(tools, 3);

        tools.Children.Add(ActionButton("Generate secret", (_, _) => GenerateSecret()));
        tools.Children.Add(ActionButton("Copy local URL", (_, _) => CopyLocalUrl()));
        tools.Children.Add(ActionButton("Copy public URL", (_, _) => CopyPublicUrl()));
        tools.Children.Add(ActionButton("ngrok dashboard", (_, _) => OpenNgrokDashboard()));
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.Children.Add(tools);

        var note = new TextBlock
        {
            Text = "Changing the MCP auth token changes both local and public MCP URLs. Update your MCP client after changing it.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0)
        };
        Grid.SetColumnSpan(note, 2);
        Grid.SetRow(note, 4);
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.Children.Add(note);

        root.Children.Add(form);
        return root;
    }

    private static void AddRow(Grid grid, int row, string label, TextBox textBox)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var textBlock = new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, row == 0 ? 0 : 12, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(textBlock, row);
        Grid.SetColumn(textBlock, 0);
        grid.Children.Add(textBlock);

        textBox.Margin = new Thickness(0, row == 0 ? 0 : 12, 0, 0);
        Grid.SetRow(textBox, row);
        Grid.SetColumn(textBox, 1);
        grid.Children.Add(textBox);
    }

    private void Save()
    {
        var domain = RevitMcpRuntime.NormalizeDomain(_ngrokDomain.Text);

        var settings = new RevitMcpSettings
        {
            NgrokAuthToken = _ngrokAuthToken.Text.Trim(),
            NgrokDomain = domain,
            McpAuthToken = string.IsNullOrWhiteSpace(_mcpAuthToken.Text)
                ? Guid.NewGuid().ToString("N")
                : _mcpAuthToken.Text.Trim()
        };

        RevitMcpRuntime.SaveSettings(settings);
        McpLog.Info("Saved Revit MCP settings.");
        Close();
    }

    private static Button ActionButton(string text, RoutedEventHandler onClick)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4)
        };
        button.Click += onClick;
        return button;
    }

    private void GenerateSecret()
    {
        _mcpAuthToken.Text = Guid.NewGuid().ToString("N");
    }

    private void CopyLocalUrl()
    {
        Clipboard.SetText(RevitMcpRuntime.BuildLocalMcpUrl(CurrentSettings()));
    }

    private void CopyPublicUrl()
    {
        var publicUrl = RevitMcpRuntime.BuildPublicMcpUrl(CurrentSettings());
        if (!string.IsNullOrWhiteSpace(publicUrl))
        {
            Clipboard.SetText(publicUrl);
        }
    }

    private static void OpenNgrokDashboard()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://dashboard.ngrok.com/domains",
            UseShellExecute = true
        });
    }

    private RevitMcpSettings CurrentSettings()
    {
        return new RevitMcpSettings
        {
            NgrokAuthToken = _ngrokAuthToken.Text.Trim(),
            NgrokDomain = RevitMcpRuntime.NormalizeDomain(_ngrokDomain.Text),
            McpAuthToken = string.IsNullOrWhiteSpace(_mcpAuthToken.Text)
                ? Guid.NewGuid().ToString("N")
                : _mcpAuthToken.Text.Trim()
        };
    }
}
