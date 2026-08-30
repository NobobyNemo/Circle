using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Circle.Desktop.Converters;

/// <summary>
/// Returns the active list background color if the two strings are equal,
/// otherwise a neutral background resource.
/// </summary>
public sealed class ActiveListBackgroundConverter : IMultiValueConverter
{
    public static readonly ActiveListBackgroundConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var a = values.Count > 0 ? values[0]?.ToString() : null;
        var b = values.Count > 1 ? values[1]?.ToString() : null;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Color.Parse("#6366f1"))
            : new SolidColorBrush(Color.Parse("#1e293b"));
    }
}

public sealed class ActiveListBorderConverter : IMultiValueConverter
{
    public static readonly ActiveListBorderConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var a = values.Count > 0 ? values[0]?.ToString() : null;
        var b = values.Count > 1 ? values[1]?.ToString() : null;
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
            ? new SolidColorBrush(Color.Parse("#818cf8"))
            : new SolidColorBrush(Color.Parse("#334155"));
    }
}
