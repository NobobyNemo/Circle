using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Circle.Desktop.Models;

/// <summary>
/// An item that can appear on the Wheel of Fortune.
/// Both text and image path are optional — at least one should be present.
/// </summary>
public sealed class WheelItem : INotifyPropertyChanged
{
    private string? _text;
    private string? _imagePath;

    public string? Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasText));
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            if (_imagePath != value)
            {
                _imagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(HasText));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath);

    [JsonIgnore]
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    [JsonIgnore]
    public string DisplayName => Text ?? Path.GetFileNameWithoutExtension(ImagePath ?? "") ?? "—";

    public WheelItem() { }

    public WheelItem(string? text, string? imagePath = null)
    {
        _text = string.IsNullOrWhiteSpace(text) ? null : text;
        _imagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
