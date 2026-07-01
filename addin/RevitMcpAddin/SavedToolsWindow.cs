using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Autodesk.Revit.UI;
using WpfBorder = System.Windows.Controls.Border;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfColumnDefinition = System.Windows.Controls.ColumnDefinition;
using WpfDock = System.Windows.Controls.Dock;
using WpfDockPanel = System.Windows.Controls.DockPanel;
using WpfGrid = System.Windows.Controls.Grid;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfRowDefinition = System.Windows.Controls.RowDefinition;
using WpfScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfWrapPanel = System.Windows.Controls.WrapPanel;

namespace RevitMcpAddin;

public sealed class SavedToolsWindow : Window
{
    private static readonly Regex CSharpIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly UIApplication _app;
    private readonly WpfListBox _tools = new();
    private readonly WpfTextBlock _details = new();
    private readonly WpfTextBlock _libraryPath = new();
    private readonly WpfStackPanel _parameterPanel = new();
    private readonly WpfTextBox _result = new();
    private readonly List<SavedToolSummary> _items = new();
    private readonly Dictionary<string, FrameworkElement> _parameterInputs = new();

    public SavedToolsWindow(UIApplication app)
    {
        _app = app;
        Title = "Revit MCP Saved Tools";
        Width = 860;
        Height = 620;
        MinWidth = 720;
        MinHeight = 500;
        Content = BuildContent();
        LoadTools();
    }

    private UIElement BuildContent()
    {
        var root = new WpfDockPanel
        {
            Margin = new Thickness(16)
        };

        var buttons = new WpfWrapPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 14, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        WpfDockPanel.SetDock(buttons, WpfDock.Bottom);
        buttons.Children.Add(ActionButton("Execute", (_, _) => ExecuteSelected()));
        buttons.Children.Add(ActionButton("Refresh", (_, _) => LoadTools()));
        buttons.Children.Add(ActionButton("Copy ID", (_, _) => CopySelectedId()));
        buttons.Children.Add(ActionButton("Open library", (_, _) => OpenLibrary()));
        buttons.Children.Add(ActionButton("Close", (_, _) => Close()));
        root.Children.Add(buttons);

        var header = new WpfStackPanel
        {
            Margin = new Thickness(0, 0, 0, 14)
        };
        WpfDockPanel.SetDock(header, WpfDock.Top);
        header.Children.Add(new WpfTextBlock
        {
            Text = "Saved Tools",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        _libraryPath.TextWrapping = TextWrapping.Wrap;
        _libraryPath.Margin = new Thickness(0, 6, 0, 0);
        header.Children.Add(_libraryPath);
        root.Children.Add(header);

        var grid = new WpfGrid();
        grid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(280) });
        grid.ColumnDefinitions.Add(new WpfColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _tools.DisplayMemberPath = nameof(SavedToolSummary.DisplayName);
        _tools.SelectionChanged += (_, _) => ShowSelectedDetails();
        WpfGrid.SetColumn(_tools, 0);
        grid.Children.Add(_tools);

        var right = new WpfGrid
        {
            Margin = new Thickness(14, 0, 0, 0)
        };
        right.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new WpfRowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new WpfRowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var detailsBorder = new WpfBorder
        {
            BorderThickness = new Thickness(1),
            BorderBrush = SystemColors.ControlDarkBrush,
            Padding = new Thickness(12),
            Child = _details
        };
        _details.TextWrapping = TextWrapping.Wrap;
        WpfGrid.SetRow(detailsBorder, 0);
        right.Children.Add(detailsBorder);

        var parameterBorder = new WpfBorder
        {
            BorderThickness = new Thickness(1),
            BorderBrush = SystemColors.ControlDarkBrush,
            Padding = new Thickness(12),
            Margin = new Thickness(0, 12, 0, 0),
            Child = _parameterPanel
        };
        WpfGrid.SetRow(parameterBorder, 1);
        right.Children.Add(parameterBorder);

        _result.IsReadOnly = true;
        _result.AcceptsReturn = true;
        _result.TextWrapping = TextWrapping.Wrap;
        _result.VerticalScrollBarVisibility = WpfScrollBarVisibility.Auto;
        _result.Margin = new Thickness(0, 12, 0, 0);
        WpfGrid.SetRow(_result, 2);
        right.Children.Add(_result);

        WpfGrid.SetColumn(right, 1);
        grid.Children.Add(right);

        root.Children.Add(grid);
        return root;
    }

    private void LoadTools()
    {
        var root = CurrentLibraryPath();
        var toolsRoot = Path.Combine(root, "tools");

        Directory.CreateDirectory(toolsRoot);
        Directory.CreateDirectory(Path.Combine(root, "runs"));
        _libraryPath.Text = root;
        _items.Clear();

        foreach (var metadataPath in Directory.GetDirectories(toolsRoot)
                     .Select(directory => Path.Combine(directory, "tool.json"))
                     .Where(File.Exists)
                     .OrderBy(x => x))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var tool = JsonSerializer.Deserialize<SavedToolSummary>(json, RevitMcpSettings.JsonOptions);
                if (tool is not null && !string.IsNullOrWhiteSpace(tool.Id))
                {
                    tool.Path = Path.GetDirectoryName(metadataPath) ?? "";
                    _items.Add(tool);
                }
            }
            catch (Exception ex)
            {
                _items.Add(new SavedToolSummary
                {
                    Id = Path.GetFileName(Path.GetDirectoryName(metadataPath)) ?? "unknown",
                    Name = "Invalid tool metadata",
                    Description = ex.Message,
                    Path = Path.GetDirectoryName(metadataPath) ?? ""
                });
            }
        }

        _tools.ItemsSource = null;
        _tools.ItemsSource = _items;
        _result.Text = "";
        if (_items.Count > 0)
        {
            _tools.SelectedIndex = 0;
        }
        else
        {
            _details.Text = "No saved tools found. Use the MCP tool save_revit_tool to create one.";
            RenderParameters(null);
        }
    }

    private void ShowSelectedDetails()
    {
        if (_tools.SelectedItem is not SavedToolSummary tool)
        {
            _details.Text = "";
            RenderParameters(null);
            return;
        }

        _details.Text =
            $"ID: {tool.Id}{Environment.NewLine}" +
            $"Name: {tool.Name}{Environment.NewLine}" +
            $"Version: {tool.Version}{Environment.NewLine}" +
            $"Requires transaction: {tool.RequiresTransaction}{Environment.NewLine}" +
            $"Path: {tool.Path}{Environment.NewLine}{Environment.NewLine}" +
            $"{tool.Description}";
        RenderParameters(tool);
    }

    private void RenderParameters(SavedToolSummary? tool)
    {
        _parameterInputs.Clear();
        _parameterPanel.Children.Clear();

        _parameterPanel.Children.Add(new WpfTextBlock
        {
            Text = "Parameters",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        if (tool is null || tool.Parameters.Count == 0)
        {
            _parameterPanel.Children.Add(new WpfTextBlock { Text = "No parameters." });
            return;
        }

        foreach (var entry in tool.Parameters)
        {
            var name = entry.Key;
            var parameter = entry.Value;
            if (!CSharpIdentifier.IsMatch(name))
            {
                _parameterPanel.Children.Add(new WpfTextBlock { Text = $"Invalid parameter name: {name}" });
                continue;
            }

            var label = new WpfTextBlock
            {
                Text = parameter.Required ? $"{name} *" : name,
                Margin = new Thickness(0, 8, 0, 3)
            };
            _parameterPanel.Children.Add(label);

            if (parameter.Type == "boolean")
            {
                var checkBox = new WpfCheckBox
                {
                    IsChecked = parameter.DefaultValueKind == JsonValueKind.True,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                _parameterInputs[name] = checkBox;
                _parameterPanel.Children.Add(checkBox);
            }
            else
            {
                var textBox = new WpfTextBox
                {
                    Text = parameter.DefaultAsString(),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                _parameterInputs[name] = textBox;
                _parameterPanel.Children.Add(textBox);
            }
        }
    }

    private void ExecuteSelected()
    {
        if (_tools.SelectedItem is not SavedToolSummary tool)
        {
            return;
        }

        if (tool.RequiresTransaction == true)
        {
            var confirmation = MessageBox.Show(
                "This saved tool is marked as requiring a transaction and may modify the active model. Execute it now?",
                "Execute Saved Tool",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }
        }

        Dictionary<string, object?> parameters;
        string code;
        try
        {
            parameters = ReadParameterValues(tool);
            code = BuildRunnableCode(tool, parameters);
        }
        catch (Exception ex)
        {
            _result.Text = ex.Message;
            return;
        }

        try
        {
            var uidoc = _app.ActiveUIDocument;
            if (uidoc is null)
            {
                throw new InvalidOperationException("No active Revit document.");
            }

            var result = CSharpRuntime.Execute(code, _app, uidoc, uidoc.Document);
            RecordRun(tool.Id, parameters, result, null);
            _result.Text = result;
        }
        catch (Exception ex)
        {
            RecordRun(tool.Id, parameters, null, ex.ToString());
            _result.Text = ex.ToString();
        }
    }

    private Dictionary<string, object?> ReadParameterValues(SavedToolSummary tool)
    {
        var values = new Dictionary<string, object?>();
        foreach (var entry in tool.Parameters)
        {
            var name = entry.Key;
            var parameter = entry.Value;
            if (!CSharpIdentifier.IsMatch(name))
            {
                throw new InvalidOperationException($"Parameter name is not a valid C# identifier: {name}");
            }

            if (!_parameterInputs.TryGetValue(name, out var input))
            {
                continue;
            }

            object? value;
            if (input is WpfCheckBox checkBox)
            {
                value = checkBox.IsChecked == true;
            }
            else if (input is WpfTextBox textBox)
            {
                var text = textBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (parameter.Required)
                    {
                        throw new InvalidOperationException($"Missing required parameter: {name}");
                    }

                    continue;
                }

                value = parameter.Type switch
                {
                    "integer" => int.Parse(text, CultureInfo.InvariantCulture),
                    "number" => double.Parse(text, CultureInfo.InvariantCulture),
                    "boolean" => bool.Parse(text),
                    _ => text
                };
            }
            else
            {
                continue;
            }

            values[name] = value;
        }

        return values;
    }

    private static string BuildRunnableCode(SavedToolSummary tool, Dictionary<string, object?> parameters)
    {
        var entrypoint = string.IsNullOrWhiteSpace(tool.Entrypoint) ? "code.cs" : tool.Entrypoint;
        var codePath = Path.Combine(tool.Path, entrypoint);
        if (!File.Exists(codePath))
        {
            throw new FileNotFoundException("Saved tool code was not found.", codePath);
        }

        var lines = new List<string>
        {
            $"// Saved Revit MCP tool: {tool.Id}",
            "// Parameter variables are injected by the Revit add-in."
        };
        foreach (var entry in parameters)
        {
            var name = entry.Key;
            var value = entry.Value;
            lines.Add($"var {name} = {ToCSharpLiteral(value)};");
        }

        lines.Add("");
        lines.Add(File.ReadAllText(codePath).Trim());
        return string.Join(Environment.NewLine, lines);
    }

    private void RecordRun(string toolId, Dictionary<string, object?> parameters, string? result, string? error)
    {
        var now = DateTimeOffset.UtcNow;
        var runRoot = Path.Combine(CurrentLibraryPath(), "runs", now.ToString("yyyy", CultureInfo.InvariantCulture), now.ToString("MM", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(runRoot);
        var path = Path.Combine(runRoot, $"{toolId}-{now:yyyyMMddHHmmssfff}.json");
        var payload = new
        {
            toolId,
            parameters,
            result,
            error,
            ok = error is null,
            timestamp = now
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload, RevitMcpSettings.JsonOptions));
    }

    private void CopySelectedId()
    {
        if (_tools.SelectedItem is SavedToolSummary tool)
        {
            Clipboard.SetText(tool.Id);
        }
    }

    private void OpenLibrary()
    {
        var root = CurrentLibraryPath();
        Directory.CreateDirectory(root);
        Process.Start(new ProcessStartInfo
        {
            FileName = root,
            UseShellExecute = true
        });
    }

    private static string ToCSharpLiteral(object? value)
    {
        return value switch
        {
            null => "null",
            bool boolean => boolean ? "true" : "false",
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            long integer => integer.ToString(CultureInfo.InvariantCulture),
            float number => number.ToString("R", CultureInfo.InvariantCulture),
            double number => number.ToString("R", CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            string text => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"",
            _ => throw new InvalidOperationException($"Unsupported parameter value: {value}")
        };
    }

    private string CurrentLibraryPath()
    {
        var settings = RevitMcpRuntime.LoadSettings();
        return string.IsNullOrWhiteSpace(settings.ToolLibraryPath)
            ? RevitMcpRuntime.DefaultToolLibraryPath
            : settings.ToolLibraryPath;
    }

    private static WpfButton ActionButton(string text, RoutedEventHandler onClick)
    {
        var button = new WpfButton
        {
            Content = text,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 5, 10, 5)
        };
        button.Click += onClick;
        return button;
    }

    private sealed class SavedToolSummary
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "";
        public string Entrypoint { get; set; } = "code.cs";
        public bool? RequiresTransaction { get; set; }
        public Dictionary<string, SavedToolParameter> Parameters { get; set; } = new();
        public string Path { get; set; } = "";

        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Name) ? Id : Name;
            }
        }
    }

    private sealed class SavedToolParameter
    {
        public string Type { get; set; } = "string";
        public bool Required { get; set; }
        public JsonElement Default { get; set; }

        public JsonValueKind DefaultValueKind
        {
            get
            {
                return Default.ValueKind;
            }
        }

        public string DefaultAsString()
        {
            return Default.ValueKind switch
            {
                JsonValueKind.String => Default.GetString() ?? "",
                JsonValueKind.Number => Default.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }
    }
}
