namespace Circle.Desktop.Audio;

public record AudioDevice(int DeviceNumber, string Name, bool IsMme = false, bool IsAsio = false, string? DeviceId = null);

public interface IAudioCaptureService
{
    event EventHandler<float[]>? SamplesCaptured;
    event EventHandler<string>? ErrorOccurred;
    event EventHandler<string>? DebugMessage;

    IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    AudioDevice? SelectedInputDevice { get; set; }

    int SampleRate { get; }

    Task<bool> RequestAccessAsync();

    void Start();
    void Stop();
}
