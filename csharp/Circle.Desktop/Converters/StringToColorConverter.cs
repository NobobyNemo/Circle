using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Circle.Desktop.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#eb4b4b") to an Avalonia <see cref="Color"/>.
/// Returns Transparent for null/empty/invalid values.
/// </summary>
public sealed class StringToColorConverter : IValueConverter
{
    public static readonly StringToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return Colors.Transparent;

        try
        {
            return Color.Parse(s);
        }
        catch
        {
            return Colors.Transparent;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
