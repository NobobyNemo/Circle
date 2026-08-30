using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Circle.Desktop.Audio;

/// <summary>
/// Microphone capture using MediaCapture.StartRecordToStreamAsync.
/// This is the highest-level WinRT recording API and may bypass
/// low-level WASAPI interception by security software.
/// </summary>
public sealed class MediaCaptureStreamService : IAudioCaptureService
{
    private MediaCapture? _mediaCapture;
    private InMemoryRandomAccessStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private int _channelCount = 1;

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }
    public int SampleRate { get; private set; } = 48000;

    public MediaCaptureStreamService()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var collection = Task.Run(async () =>
                await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector()).AsTask())
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var index = 0;
            foreach (var device in collection.Where(d => d.IsEnabled))
            {
                devices.Add(new AudioDevice(index, device.Name, false, false, device.Id));
                index++;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not enumerate capture devices: {ex.Message}");
        }

        if (devices.Count == 0)
            devices.Add(new AudioDevice(0, "Default microphone", false, false));

        AvailableInputDevices = devices;
        SelectedInputDevice = devices.FirstOrDefault();
    }

    public async Task<bool> RequestAccessAsync()
    {
        try
        {
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            };

            using var capture = new MediaCapture();
            await capture.InitializeAsync(settings).AsTask();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch
        {
            return true;
        }
    }

    public void Start()
    {
        Stop();
        _ = InitializeAndStartAsync();
    }

    private async Task InitializeAndStartAsync()
    {
        try
        {
            DebugMessage?.Invoke(this, "StreamCapture: Initializing MediaCapture...");

            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
                MediaCategory = MediaCategory.Speech,
            };

            if (!string.IsNullOrEmpty(SelectedInputDevice?.DeviceId))
            {
                settings.AudioDeviceId = SelectedInputDevice.DeviceId;
                DebugMessage?.Invoke(this, $"StreamCapture: Using device: {SelectedInputDevice.Name}");
            }

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(settings).AsTask().ConfigureAwait(false);
            DebugMessage?.Invoke(this, "StreamCapture: MediaCapture initialized");

            // Log MediaCapture settings
            try
            {
                var mcSettings = _mediaCapture.MediaCaptureSettings;
                DebugMessage?.Invoke(this, $"StreamCapture: Settings - AudioDeviceId={mcSettings.AudioDeviceId}, StreamingCaptureMode={mcSettings.StreamingCaptureMode}, MediaCategory={mcSettings.MediaCategory}");
            }
            catch (Exception ex) { DebugMessage?.Invoke(this, $"StreamCapture: Settings error: {ex.Message}"); }

            // Try default WAV profile first (don't override audio properties)
            var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.High);
            DebugMessage?.Invoke(this, $"StreamCapture: Profile audio: {profile.Audio?.Subtype}, {profile.Audio?.SampleRate}Hz, {profile.Audio?.ChannelCount}ch, {profile.Audio?.BitsPerSample}bit");

            _stream = new InMemoryRandomAccessStream();
            DebugMessage?.Invoke(this, "StreamCapture: Starting recording to stream...");

            await _mediaCapture.StartRecordToStreamAsync(profile, _stream).AsTask().ConfigureAwait(false);
            DebugMessage?.Invoke(this, "StreamCapture: Recording started!");

            SampleRate = (int)(profile.Audio?.SampleRate ?? 48000);
            _channelCount = (int)(profile.Audio?.ChannelCount ?? 1);

            // Start reading from the stream in a background task
            _cts = new CancellationTokenSource();
            _readTask = ReadStreamAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            DebugMessage?.Invoke(this, $"StreamCapture: Init error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"StreamCapture failed: {ex.Message}");
        }
    }

    private async Task ReadStreamAsync(CancellationToken ct)
    {
        try
        {
            if (_stream is null) return;

            // Poll stream size every second for 10 seconds to see if data arrives
            for (int i = 1; i <= 10; i++)
            {
                await Task.Delay(1000, ct).ConfigureAwait(false);
                var size = _stream.Size;
                DebugMessage?.Invoke(this, $"StreamCapture: Stream size after {i}s: {size}");
                
                if (size > 0)
                    break;
            }

            // If stream is still empty, try stopping and restarting recording to flush
            if (_stream.Size == 0 && _mediaCapture is not null)
            {
                DebugMessage?.Invoke(this, "StreamCapture: Stream empty, trying StopRecord to flush...");
                try
                {
                    await _mediaCapture.StopRecordAsync().AsTask().ConfigureAwait(false);
                    await Task.Delay(500, ct).ConfigureAwait(false);
                    DebugMessage?.Invoke(this, $"StreamCapture: After StopRecord, stream size: {_stream.Size}");
                }
                catch (Exception ex)
                {
                    DebugMessage?.Invoke(this, $"StreamCapture: StopRecord error: {ex.Message}");
                }
                
                // If data appeared after stop, restart recording
                if (_stream.Size > 0)
                {
                    DebugMessage?.Invoke(this, "StreamCapture: Data flushed after stop! Reading from stream...");
                }
                else
                {
                    // Try with M4a profile instead of WAV
                    DebugMessage?.Invoke(this, "StreamCapture: Trying M4a profile...");
                    try
                    {
                        _stream.Dispose();
                        _stream = new InMemoryRandomAccessStream();
                        var m4aProfile = MediaEncodingProfile.CreateM4a(AudioEncodingQuality.High);
                        await _mediaCapture.StartRecordToStreamAsync(m4aProfile, _stream).AsTask().ConfigureAwait(false);
                        DebugMessage?.Invoke(this, "StreamCapture: M4a recording started, waiting 3s...");
                        for (int i = 1; i <= 3; i++)
                        {
                            await Task.Delay(1000, ct).ConfigureAwait(false);
                            DebugMessage?.Invoke(this, $"StreamCapture: M4a stream size after {i}s: {_stream.Size}");
                        }
                        if (_stream.Size > 0)
                        {
                            await _mediaCapture.StopRecordAsync().AsTask().ConfigureAwait(false);
                            DebugMessage?.Invoke(this, $"StreamCapture: M4a after stop, size: {_stream.Size}");
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugMessage?.Invoke(this, $"StreamCapture: M4a error: {ex.Message}");
                    }
                }
            }

            if (_stream.Size == 0)
            {
                DebugMessage?.Invoke(this, "StreamCapture: No audio data received. Kaspersky may be blocking audio capture at the engine level.");
                ErrorOccurred?.Invoke(this, "No audio data from microphone. Kaspersky Endpoint Security is blocking all audio capture APIs (WASAPI, MME, DirectSound, WinRT). Contact IT to add an exception for this application.");
                return;
            }

            // Skip 44-byte WAV header
            var readPos = 44UL;
            var frameCount = 0;
            const int readSize = 4096;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);

                var streamSize = _stream.Size;
                var available = (long)(streamSize - readPos);
                if (available < readSize)
                    continue;

                using var inputStream = _stream.GetInputStreamAt(readPos);
                using var reader = new DataReader(inputStream);
                await reader.LoadAsync(readSize).AsTask().ConfigureAwait(false);
                readPos += readSize;

                var sampleCount = readSize / 2;
                var samples = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    short s16 = reader.ReadInt16();
                    samples[i] = s16 / 32768f;
                }

                frameCount++;
                if (frameCount <= 5 || frameCount % 100 == 0)
                {
                    var maxAbs = 0f;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        var abs = Math.Abs(samples[i]);
                        if (abs > maxAbs) maxAbs = abs;
                    }
                    DebugMessage?.Invoke(this, $"StreamCapture: Frame#{frameCount}, {sampleCount} samples, max={maxAbs:F4}");
                }

                SamplesCaptured?.Invoke(this, samples);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            DebugMessage?.Invoke(this, $"StreamCapture: Read error: {ex.Message}");
            ErrorOccurred?.Invoke(this, $"StreamCapture read error: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _cts?.Cancel();
            _readTask?.Wait(2000);
        }
        catch { /* ignored */ }

        _cts?.Dispose();
        _cts = null;
        _readTask = null;

        if (_mediaCapture is not null)
        {
            try
            {
                _ = _mediaCapture.StopRecordAsync().AsTask().Wait(2000);
            }
            catch { /* ignored */ }
            try { _mediaCapture.Dispose(); } catch { /* ignored */ }
            _mediaCapture = null;
        }

        _stream?.Dispose();
        _stream = null;
    }
}
