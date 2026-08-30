using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Circle.Desktop.Converters;

public sealed class BoolToHorizontalAlignmentConverter : IValueConverter
{
    public HorizontalAlignment TrueValue { get; set; } = HorizontalAlignment.Left;
    public HorizontalAlignment FalseValue { get; set; } = HorizontalAlignment.Center;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? TrueValue : FalseValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
