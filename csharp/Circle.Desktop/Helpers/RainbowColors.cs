using Avalonia.Media;

namespace Circle.Desktop.Helpers;

/// <summary>
/// Softened rainbow palette for scale degrees I–VII (50% blended with white).
/// </summary>
public static class RainbowColors
{
    public static IReadOnlyList<IBrush> Brushes { get; } = new[]
    {
        new SolidColorBrush(Lighten(Color.FromRgb(0xFF, 0x00, 0x00))), // I - Red
        new SolidColorBrush(Lighten(Color.FromRgb(0xFF, 0x7F, 0x00))), // II - Orange
        new SolidColorBrush(Lighten(Color.FromRgb(0xFF, 0xFF, 0x00))), // III - Yellow
        new SolidColorBrush(Lighten(Color.FromRgb(0x00, 0xFF, 0x00))), // IV - Green
        new SolidColorBrush(Lighten(Color.FromRgb(0x00, 0xFF, 0xFF))), // V - Cyan
        new SolidColorBrush(Lighten(Color.FromRgb(0x00, 0x00, 0xFF))), // VI - Blue
        new SolidColorBrush(Lighten(Color.FromRgb(0x8B, 0x00, 0xFF)))  // VII - Violet
    };

    public static IReadOnlyList<Color> Colors { get; } = new[]
    {
        Lighten(Color.FromRgb(0xFF, 0x00, 0x00)),
        Lighten(Color.FromRgb(0xFF, 0x7F, 0x00)),
        Lighten(Color.FromRgb(0xFF, 0xFF, 0x00)),
        Lighten(Color.FromRgb(0x00, 0xFF, 0x00)),
        Lighten(Color.FromRgb(0x00, 0xFF, 0xFF)),
        Lighten(Color.FromRgb(0x00, 0x00, 0xFF)),
        Lighten(Color.FromRgb(0x8B, 0x00, 0xFF))
    };

    public static string[] DegreeLabels { get; } = ["I", "II", "III", "IV", "V", "VI", "VII"];

    private static Color Lighten(Color color)
    {
        return Color.FromRgb(
            (byte)((color.R + 255) / 2),
            (byte)((color.G + 255) / 2),
            (byte)((color.B + 255) / 2));
    }
}
