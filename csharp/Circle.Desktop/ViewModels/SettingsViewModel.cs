using System.Collections.ObjectModel;
using System.Windows.Input;
using Circle.Desktop.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IAudioCaptureService _captureService;

    public ObservableCollection<AudioDevice> InputDevices { get; } = new();

    [ObservableProperty]
    private AudioDevice? _selectedInputDevice;

    public SettingsViewModel(IAudioCaptureService captureService)
    {
        _captureService = captureService;

        foreach (var device in _captureService.AvailableInputDevices)
            InputDevices.Add(device);

        _selectedInputDevice = _captureService.SelectedInputDevice;
    }

    partial void OnSelectedInputDeviceChanged(AudioDevice? value)
    {
        _captureService.SelectedInputDevice = value;
    }
}
