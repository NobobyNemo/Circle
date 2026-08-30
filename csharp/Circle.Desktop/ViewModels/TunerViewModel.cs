using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Circle.Desktop.Audio;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class TunerViewModel : ViewModelBase
{
    private readonly IAudioCaptureService _captureService;
    private PitchDetector? _pitchDetector;

    [ObservableProperty]
    private string _detectedNote = "-";

    [ObservableProperty]
    private double _detectedFrequency;

    [ObservableProperty]
    private double _cents;

    [ObservableProperty]
    private double _needleAngle;

    [ObservableProperty]
    private string _toggleButtonText = "Start Tuner";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isTuning;

    public TunerViewModel(IAudioCaptureService captureService)
    {
        _captureService = captureService;

        _captureService.SamplesCaptured += OnSamplesCaptured;
        _captureService.ErrorOccurred += OnErrorOccurred;
        _captureService.DebugMessage += OnDebugMessage;

        ToggleTunerCommand = new AsyncRelayCommand(OnToggleTunerAsync);
    }

    public ICommand ToggleTunerCommand { get; }

    private async Task OnToggleTunerAsync()
    {
        if (IsTuning)
        {
            _captureService.Stop();
            IsTuning = false;
            _pitchDetector = null;
            ToggleButtonText = "Start Tuner";
            StatusMessage = "";
            DetectedNote = "-";
            DetectedFrequency = 0;
            Cents = 0;
            NeedleAngle = 0;
        }
        else
        {
            StatusMessage = "Requesting microphone access...";
            var allowed = await _captureService.RequestAccessAsync();

            if (!allowed)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = "Microphone access denied. Opening privacy settings...";
                    try
                    {
                        Process.Start(new ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute = true });
                    }
                    catch
                    {
                        StatusMessage = "Microphone access denied. Enable it in Settings → Privacy & Security → Microphone.";
                    }
                });
                return;
            }

            StatusMessage = "";
            _pitchDetector = null;
            _captureService.Start();

            IsTuning = true;
            ToggleButtonText = "Stop Tuner";
        }
    }

    private void OnSamplesCaptured(object? sender, float[] samples)
    {
        if (_pitchDetector is null)
        {
            if (_captureService.SampleRate <= 0)
                return;
            _pitchDetector = new PitchDetector(_captureService.SampleRate);
        }

        var frequency = _pitchDetector.DetectPitch(samples);

        if (frequency is null || frequency < 60 || frequency > 1300)
        {
            Dispatcher.UIThread.Post(() =>
            {
                DetectedNote = "-";
                DetectedFrequency = 0;
                Cents = 0;
                NeedleAngle = 0;
            });
            return;
        }

        var analysis = PitchDetector.Analyze(frequency.Value);

        Dispatcher.UIThread.Post(() =>
        {
            DetectedNote = analysis.NoteName;
            DetectedFrequency = analysis.Frequency;
            Cents = analysis.Cents;
            NeedleAngle = Math.Clamp(analysis.Cents * 1.5, -45, 45);
            StatusMessage = "";
        });
    }

    private void OnErrorOccurred(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = message;
            IsTuning = false;
            ToggleButtonText = "Start Tuner";
            DetectedNote = "-";
            DetectedFrequency = 0;
            Cents = 0;
            NeedleAngle = 0;
        });
    }

    private void OnDebugMessage(object? sender, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage = message;
        });
    }
}
