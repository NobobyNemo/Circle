using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;

namespace Circle.Desktop.Audio;

/// <summary>
/// Microphone capture using MediaCapture + MediaFrameReader (WinRT).
/// Uses a different audio pipeline than AudioGraph.
/// </summary>
public sealed class MediaFrameReaderCaptureService : IAudioCaptureService
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private int _channelCount = 1;
    private string _formatSubtype = "";
    private int _frameCount;
    private int _errorCount;

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }
    public int SampleRate { get; private set; } = 48000;

    public MediaFrameReaderCaptureService()
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
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio
            };

            if (!string.IsNullOrEmpty(SelectedInputDevice?.DeviceId))
            {
                settings.AudioDeviceId = SelectedInputDevice.DeviceId;
            }

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(settings).AsTask().ConfigureAwait(false);

            var audioFrameSources = _mediaCapture.FrameSources
                .Where(x => x.Value.Info.MediaStreamType == MediaStreamType.Audio)
                .ToList();

            if (audioFrameSources.Count == 0)
            {
                ErrorOccurred?.Invoke(this, "No audio frame source found after MediaCapture init.");
                _mediaCapture.Dispose();
                _mediaCapture = null;
                return;
            }

            var frameSource = audioFrameSources.First().Value;
            var format = frameSource.CurrentFormat;

            _formatSubtype = format.Subtype ?? "unknown";
            SampleRate = (int)(format.AudioEncodingProperties?.SampleRate ?? 48000);
            _channelCount = (int)(format.AudioEncodingProperties?.ChannelCount ?? 1);

            DebugMessage?.Invoke(this, $"DEBUG: Format={_formatSubtype}, Rate={SampleRate}, Ch={_channelCount}");

            _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource, MediaEncodingSubtypes.Float).AsTask().ConfigureAwait(false);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Buffered;
            _frameReader.FrameArrived += OnFrameArrived;

            var status = await _frameReader.StartAsync().AsTask().ConfigureAwait(false);

            DebugMessage?.Invoke(this, $"DEBUG: FrameReader start status: {status}");

            if (status != MediaFrameReaderStartStatus.Success)
            {
                ErrorOccurred?.Invoke(this, $"MediaFrameReader failed to start: {status}");
                _frameReader.Dispose();
                _frameReader = null;
                _mediaCapture.Dispose();
                _mediaCapture = null;
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"MediaFrameReader capture failed: {ex.Message}");
        }
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        _frameCount++;

        if (_frameCount <= 5 || _frameCount % 100 == 0)
        {
            DebugMessage?.Invoke(this, $"DEBUG: FrameArrived #{_frameCount}");
        }

        try
        {
            using var frameRef = sender.TryAcquireLatestFrame();
            if (frameRef is null)
            {
                if (_frameCount <= 5)
                    DebugMessage?.Invoke(this, $"DEBUG: Frame#{_frameCount} - frameRef is null");
                return;
            }

            var audioMediaFrame = frameRef.AudioMediaFrame;
            if (audioMediaFrame is null)
            {
                if (_frameCount <= 5)
                    DebugMessage?.Invoke(this, $"DEBUG: Frame#{_frameCount} - audioMediaFrame is null");
                return;
            }

            using var audioFrame = audioMediaFrame.GetAudioFrame();
            if (audioFrame is null)
            {
                if (_frameCount <= 5)
                    DebugMessage?.Invoke(this, $"DEBUG: Frame#{_frameCount} - audioFrame is null");
                return;
            }

            var samples = ConvertAudioFrameToMonoFloat(audioFrame, _channelCount);

            if (_frameCount <= 5 || _frameCount % 100 == 0)
            {
                var maxAbs = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    var abs = Math.Abs(samples[i]);
                    if (abs > maxAbs) maxAbs = abs;
                }
                DebugMessage?.Invoke(this, $"DEBUG: Frame#{_frameCount}, Samples={samples.Length}, MaxAbs={maxAbs:F6}, Ch={_channelCount}");
            }

            if (samples.Length > 0)
                SamplesCaptured?.Invoke(this, samples);
        }
        catch (Exception ex)
        {
            _errorCount++;
            if (_errorCount <= 3)
                DebugMessage?.Invoke(this, $"DEBUG: OnFrameArrived error #{_errorCount}: {ex.Message}");
        }
    }

    private static unsafe float[] ConvertAudioFrameToMonoFloat(Windows.Media.AudioFrame frame, int channelCount)
    {
        if (channelCount <= 0)
            channelCount = 1;

        using var buffer = frame.LockBuffer(AudioBufferAccessMode.Read);
        using var reference = buffer.CreateReference();

        ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);

        var totalFloats = (int)capacityInBytes / sizeof(float);
        var frameSampleCount = totalFloats / channelCount;
        var mono = new float[frameSampleCount];

        var src = (float*)dataInBytes;
        for (var i = 0; i < frameSampleCount; i++)
        {
            var sum = 0f;
            for (var ch = 0; ch < channelCount; ch++)
                sum += src[i * channelCount + ch];

            mono[i] = sum / channelCount;
        }

        return mono;
    }

    public void Stop()
    {
        if (_frameReader is not null)
        {
            try
            {
                _frameReader.FrameArrived -= OnFrameArrived;
                _frameReader.StopAsync().AsTask().Wait();
            }
            catch { /* ignored */ }
            try { _frameReader.Dispose(); } catch { /* ignored */ }
            _frameReader = null;
        }

        if (_mediaCapture is not null)
        {
            try { _mediaCapture.Dispose(); } catch { /* ignored */ }
            _mediaCapture = null;
        }
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
}
