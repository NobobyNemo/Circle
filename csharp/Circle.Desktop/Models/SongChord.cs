using CommunityToolkit.Mvvm.ComponentModel;

namespace Circle.Desktop.Models;

public partial class SongChord : ObservableObject
{
    public const double DefaultBlockWidth = 19;
    public const double DefaultBlockHeight = 15;

    [ObservableProperty]
    private string _name = "Am";

    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private double _fontSize = 13;

    public double PixelPosition => Position * FontSize * 0.6;
    public double BlockHeight => DefaultBlockHeight;
    public double BlockFontSize => Math.Min(FontSize, 12);
    public double BlockWidth => Math.Max(DefaultBlockWidth, Name.Length * BlockFontSize * 0.6 + 4);

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(BlockWidth));

    partial void OnPositionChanged(double value) => OnPropertyChanged(nameof(PixelPosition));

    partial void OnFontSizeChanged(double value)
    {
        OnPropertyChanged(nameof(PixelPosition));
        OnPropertyChanged(nameof(BlockFontSize));
        OnPropertyChanged(nameof(BlockWidth));
    }
}
