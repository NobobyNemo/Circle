using System.Globalization;
using Avalonia.Data.Converters;

namespace Circle.Desktop.Converters;

/// <summary>
/// Returns true if the two string values are equal (case-insensitive, ordinal).
/// Used to highlight the active list in the ribbon.
/// </summary>
public sealed class StringEqualsConverter : IMultiValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        var a = values[0]?.ToString();
        var b = values[1]?.ToString();
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
