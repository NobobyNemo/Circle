using Avalonia.Controls;

namespace Circle.Desktop.Views;

public partial class TabEditorWindow : Window
{
    public event EventHandler<string>? TabSubmitted;

    public TabEditorWindow()
    {
        InitializeComponent();
    }

    private void Submit(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = TabTextBox.Text?.TrimEnd() ?? "";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var expected = new[] { "e", "B", "G", "D", "A", "E" };
        var valid = lines.Length == 6 && lines.Select(line => line.TrimStart())
            .Select(line => line.Length == 0 ? "" : line[0].ToString())
            .SequenceEqual(expected);

        if (!valid)
            return;

        TabSubmitted?.Invoke(this, text);
        Close();
    }

    private void Cancel(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
}
