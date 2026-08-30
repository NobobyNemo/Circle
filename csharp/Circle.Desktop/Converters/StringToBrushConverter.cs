using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Circle.Desktop.Converters;

/// <summary>
/// Converts a hex color string (e.g. "#eb4b4b") to a <see cref="SolidColorBrush"/>.
/// Returns a Transparent brush for null/empty/invalid values.
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return new SolidColorBrush(Colors.Transparent);

        try
        {
            var color = Color.Parse(s);
            return new SolidColorBrush(color);
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
