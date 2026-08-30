using System.Runtime.InteropServices;
using ManagedBass;
using Windows.Media.Capture;

namespace Circle.Desktop.Audio;

/// <summary>
/// Microphone capture using the BASS audio library (un4seen).
/// Tries multiple devices, sample rates, channel counts and sample formats.
/// </summary>
public sealed class BassAudioCaptureService : IAudioCaptureService
{
    private readonly object _lock = new();
    private int _recordHandle;
    private int _channels = 1;
    private SampleFormat _format = SampleFormat.Float;
    private RecordProcedure _recordProc = null!;
    private const Configuration RecWasapiConfig = (Configuration)66;

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }
    public int SampleRate { get; private set; } = 44100;

    public BassAudioCaptureService()
    {
        var devices = new List<AudioDevice>();

        try
        {
            // A dummy output init helps BASS internal timing even if we only record.
            Bass.Init();
        }
        catch { /* ignored */ }

        try
        {
            for (var i = 0; Bass.RecordGetDeviceInfo(i, out var info); i++)
            {
                if (info.IsEnabled)
                    devices.Add(new AudioDevice(i, info.Name, false, false));
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not enumerate BASS recording devices: {ex.Message}");
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
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Microphone access check failed: {ex.Message}");
            return true;
        }
    }

    public void Start()
    {
        Stop();

        var candidates = new List<int>();

        if (SelectedInputDevice is not null)
            candidates.Add(SelectedInputDevice.DeviceNumber);

        foreach (var device in AvailableInputDevices)
        {
            if (!candidates.Contains(device.DeviceNumber))
                candidates.Add(device.DeviceNumber);
        }

        if (candidates.Count == 0)
            candidates.Add(0);

        var errors = new List<string>();

        foreach (var device in candidates)
        {
            try
            {
                if (TryStartRecording(device, useWasapi: true))
                    return;
            }
            catch (Exception ex)
            {
                errors.Add($"device {device} WASAPI: {ex.Message}");
            }

            try { Bass.RecordFree(); }
            catch { /* ignored */ }

            try
            {
                if (TryStartRecording(device, useWasapi: false))
                    return;
            }
            catch (Exception ex)
            {
                errors.Add($"device {device} DirectSound: {ex.Message}");
            }

            try { Bass.RecordFree(); }
            catch { /* ignored */ }
        }

        ErrorOccurred?.Invoke(this, $"BASS could not start recording any device. Ensure microphone access is allowed in Settings → Privacy & Security → Microphone. Errors: {string.Join(", ", errors)}");
    }

    private bool TryStartRecording(int device, bool useWasapi)
    {
        _recordProc = OnRecordData;

        try { Bass.Configure(RecWasapiConfig, useWasapi ? 1 : 0); }
        catch { /* ignored */ }

        if (!Bass.RecordInit(device))
            return false;

        Bass.CurrentRecordingDevice = device;

        var info = Bass.RecordingInfo;
        var defaultRate = info.Frequency > 0 ? info.Frequency : 44100;
        var defaultChans = info.Channels > 0 ? info.Channels : 2;

        var sampleRates = new[] { defaultRate, 44100, 48000, 22050, 96000, 192000, 11025 };
        var channelCounts = new[] { defaultChans, 1, 2 };
        var formats = new[] { SampleFormat.Float, SampleFormat.Int16, SampleFormat.Byte };

        foreach (var format in formats)
        {
            var flag = format switch
            {
                SampleFormat.Int16 => BassFlags.Default,
                SampleFormat.Byte => BassFlags.Byte,
                _ => BassFlags.Float
            };

            foreach (var rate in sampleRates)
            {
                foreach (var chans in channelCounts)
                {
                    var handle = Bass.RecordStart(rate, chans, flag, _recordProc, IntPtr.Zero);
                    if (handle != 0)
                    {
                        lock (_lock)
                        {
                            _recordHandle = handle;
                            _channels = chans;
                            _format = format;
                        }

                        SampleRate = rate;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool OnRecordData(int handle, IntPtr buffer, int length, IntPtr user)
    {
        try
        {
            var bytes = new byte[length];
            Marshal.Copy(buffer, bytes, 0, length);

            int channels;
            SampleFormat format;
            lock (_lock)
            {
                channels = _channels;
                format = _format;
            }

            var samples = format switch
            {
                SampleFormat.Byte => ConvertByteToMonoFloat(bytes, channels),
                SampleFormat.Int16 => ConvertInt16ToMonoFloat(bytes, channels),
                _ => ConvertFloatToMonoFloat(bytes, channels)
            };

            if (samples.Length > 0)
                SamplesCaptured?.Invoke(this, samples);
        }
        catch
        {
            // ignored
        }

        return true; // continue recording
    }

    private static float[] ConvertFloatToMonoFloat(byte[] bytes, int channels)
    {
        if (channels <= 0)
            channels = 1;

        var totalFloats = bytes.Length / sizeof(float);
        var frameCount = totalFloats / channels;
        var mono = new float[frameCount];

        unsafe
        {
            fixed (byte* p = bytes)
            {
                var src = (float*)p;
                for (var i = 0; i < frameCount; i++)
                {
                    var sum = 0f;
                    for (var ch = 0; ch < channels; ch++)
                        sum += src[i * channels + ch];

                    mono[i] = sum / channels;
                }
            }
        }

        return mono;
    }

    private static float[] ConvertInt16ToMonoFloat(byte[] bytes, int channels)
    {
        if (channels <= 0)
            channels = 1;

        var totalShorts = bytes.Length / sizeof(short);
        var frameCount = totalShorts / channels;
        var mono = new float[frameCount];

        unsafe
        {
            fixed (byte* p = bytes)
            {
                var src = (short*)p;
                for (var i = 0; i < frameCount; i++)
                {
                    var sum = 0f;
                    for (var ch = 0; ch < channels; ch++)
                        sum += src[i * channels + ch] / 32768f;

                    mono[i] = sum / channels;
                }
            }
        }

        return mono;
    }

    private static float[] ConvertByteToMonoFloat(byte[] bytes, int channels)
    {
        if (channels <= 0)
            channels = 1;

        var totalBytes = bytes.Length;
        var frameCount = totalBytes / channels;
        var mono = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            var sum = 0f;
            for (var ch = 0; ch < channels; ch++)
                sum += (bytes[i * channels + ch] - 128) / 128f;

            mono[i] = sum / channels;
        }

        return mono;
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_recordHandle != 0)
            {
                try { Bass.ChannelStop(_recordHandle); }
                catch { /* ignored */ }
                _recordHandle = 0;
            }

            try { Bass.RecordFree(); }
            catch { /* ignored */ }
        }
    }

    private enum SampleFormat
    {
        Float,
        Int16,
        Byte
    }
}
