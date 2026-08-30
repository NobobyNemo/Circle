using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Circle.Desktop.Converters;

public sealed class BoolToVerticalAlignmentConverter : IValueConverter
{
    public VerticalAlignment TrueValue { get; set; } = VerticalAlignment.Stretch;
    public VerticalAlignment FalseValue { get; set; } = VerticalAlignment.Bottom;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueValue : FalseValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
