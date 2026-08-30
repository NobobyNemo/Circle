using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Media.Capture;

namespace Circle.Desktop.Audio;

/// <summary>
/// Microphone capture using WASAPI (shared mode) with MME fallback.
/// WASAPI is the native Windows audio API and works with most devices.
/// </summary>
public sealed class WaveInEventCaptureService : IAudioCaptureService
{
    private WasapiCapture? _wasapiCapture;
    private WaveInEvent? _waveIn;
    private int _activeChannels = 1;
    private bool _useWasapi;
    private int _dataCount;

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }
    public int SampleRate { get; private set; } = 44100;

    public WaveInEventCaptureService()
    {
        var devices = new List<AudioDevice>();

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var collection = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var device in collection)
                devices.Add(new AudioDevice(0, device.FriendlyName, false, false, device.ID));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"WASAPI enumeration failed: {ex.Message}");
        }

        try
        {
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                var name = caps.ProductName;
                if (!devices.Any(d => d.Name == name))
                    devices.Add(new AudioDevice(i, name, true, false));
            }
        }
        catch { /* ignored */ }

        if (devices.Count == 0)
            devices.Add(new AudioDevice(0, "Default Microphone", true, false));

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
            ErrorOccurred?.Invoke(this, $"Microphone access check: {ex.Message}");
            return true;
        }
    }

    public void Start()
    {
        Stop();
        _dataCount = 0;

        var errors = new List<string>();

        DebugMessage?.Invoke(this, $"DEBUG: Starting capture. SelectedDevice={SelectedInputDevice?.Name}, DeviceId={SelectedInputDevice?.DeviceId}");

        if (TryStartWasapi(errors))
        {
            DebugMessage?.Invoke(this, $"DEBUG: WASAPI started. Format={_wasapiCapture?.WaveFormat}");
            return;
        }

        DebugMessage?.Invoke(this, $"DEBUG: WASAPI failed, trying MME. Errors: {string.Join("; ", errors)}");

        if (TryStartMme(errors))
        {
            DebugMessage?.Invoke(this, $"DEBUG: MME started. Format={_waveIn?.WaveFormat}");
            return;
        }

        ErrorOccurred?.Invoke(this, $"Could not start recording: {string.Join("; ", errors)}");
    }

    private bool TryStartWasapi(List<string> errors)
    {
        try
        {
            MMDevice? mmDevice = null;
            var deviceId = SelectedInputDevice?.DeviceId;

            if (!string.IsNullOrEmpty(deviceId))
            {
                var enumerator = new MMDeviceEnumerator();
                mmDevice = enumerator.GetDevice(deviceId);
            }

            _wasapiCapture = mmDevice is not null
                ? new WasapiCapture(mmDevice)
                : new WasapiCapture();

            _wasapiCapture.ShareMode = AudioClientShareMode.Shared;
            _wasapiCapture.DataAvailable += OnWasapiDataAvailable;
            _wasapiCapture.RecordingStopped += OnRecordingStopped;
            _wasapiCapture.StartRecording();

            _useWasapi = true;
            var fmt = _wasapiCapture.WaveFormat;
            _activeChannels = fmt.Channels;
            SampleRate = fmt.SampleRate;
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"WASAPI: {ex.Message}");
            try { _wasapiCapture?.Dispose(); _wasapiCapture = null; }
            catch { /* ignored */ }
            return false;
        }
    }

    private bool TryStartMme(List<string> errors)
    {
        var deviceNumber = 0;
        var mmeDevice = AvailableInputDevices.FirstOrDefault(d => d.IsMme && d.Name == SelectedInputDevice?.Name);
        if (mmeDevice is not null)
            deviceNumber = mmeDevice.DeviceNumber;
        else if (SelectedInputDevice?.IsMme == true)
            deviceNumber = SelectedInputDevice.DeviceNumber;

        var formats = new WaveFormat[]
        {
            new(44100, 1),
            new(44100, 2),
            new(48000, 1),
            new(48000, 2),
            new(22050, 1),
            WaveFormat.CreateIeeeFloatWaveFormat(44100, 1),
            WaveFormat.CreateIeeeFloatWaveFormat(44100, 2),
        };

        foreach (var fmt in formats)
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = fmt,
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += OnMmeDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveIn.StartRecording();

                _useWasapi = false;
                _activeChannels = fmt.Channels;
                SampleRate = fmt.SampleRate;
                return true;
            }
            catch (Exception ex)
            {
                try { _waveIn?.Dispose(); _waveIn = null; }
                catch { /* ignored */ }

                errors.Add($"MME {fmt.SampleRate}/{fmt.Channels}ch: {ex.Message}");
            }
        }

        return false;
    }

    private void OnWasapiDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (e.BytesRecorded == 0)
                return;

            _dataCount++;

            var fmt = _wasapiCapture!.WaveFormat;
            var samples = ConvertToMonoFloat(e.Buffer, e.BytesRecorded, fmt);

            if (_dataCount <= 3 || _dataCount % 100 == 0)
            {
                var maxAbs = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    var abs = Math.Abs(samples[i]);
                    if (abs > maxAbs) maxAbs = abs;
                }
                DebugMessage?.Invoke(this, $"DEBUG: WASPI data#{_dataCount}, Bytes={e.BytesRecorded}, Samples={samples.Length}, MaxAbs={maxAbs:F6}");
            }

            if (samples.Length > 0)
                SamplesCaptured?.Invoke(this, samples);
        }
        catch (Exception ex) { DebugMessage?.Invoke(this, $"DEBUG: WASPI data error: {ex.Message}"); }
    }

    private void OnMmeDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            if (e.BytesRecorded == 0)
                return;

            _dataCount++;

            var fmt = _waveIn!.WaveFormat;
            var samples = ConvertToMonoFloat(e.Buffer, e.BytesRecorded, fmt);

            if (_dataCount <= 3 || _dataCount % 100 == 0)
            {
                var maxAbs = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    var abs = Math.Abs(samples[i]);
                    if (abs > maxAbs) maxAbs = abs;
                }
                DebugMessage?.Invoke(this, $"DEBUG: MME data#{_dataCount}, Bytes={e.BytesRecorded}, Samples={samples.Length}, MaxAbs={maxAbs:F6}");
            }

            if (samples.Length > 0)
                SamplesCaptured?.Invoke(this, samples);
        }
        catch (Exception ex) { DebugMessage?.Invoke(this, $"DEBUG: MME data error: {ex.Message}"); }
    }

    private float[] ConvertToMonoFloat(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var totalFloats = bytesRecorded / 4;
            var channels = format.Channels;
            var frameCount = totalFloats / channels;
            var mono = new float[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                var sum = 0f;
                for (var ch = 0; ch < channels; ch++)
                {
                    var idx = (i * channels + ch) * 4;
                    sum += BitConverter.ToSingle(buffer, idx);
                }
                mono[i] = sum / channels;
            }
            return mono;
        }

        if (format.BitsPerSample == 16)
        {
            var channels = format.Channels;
            var totalShorts = bytesRecorded / 2;
            var frameCount = totalShorts / channels;
            var mono = new float[frameCount];

            for (var i = 0; i < frameCount; i++)
            {
                var sum = 0f;
                for (var ch = 0; ch < channels; ch++)
                {
                    var idx = (i * channels + ch) * 2;
                    short sample = (short)((buffer[idx + 1] << 8) | buffer[idx]);
                    sum += sample / 32768f;
                }
                mono[i] = sum / channels;
            }
            return mono;
        }

        return Array.Empty<float>();
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
            ErrorOccurred?.Invoke(this, $"Recording stopped unexpectedly: {e.Exception.Message}");
    }

    public void Stop()
    {
        if (_wasapiCapture is not null)
        {
            try
            {
                _wasapiCapture.DataAvailable -= OnWasapiDataAvailable;
                _wasapiCapture.RecordingStopped -= OnRecordingStopped;
                _wasapiCapture.StopRecording();
            }
            catch { /* ignored */ }
            try { _wasapiCapture.Dispose(); } catch { /* ignored */ }
            _wasapiCapture = null;
        }

        if (_waveIn is not null)
        {
            try
            {
                _waveIn.DataAvailable -= OnMmeDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.StopRecording();
            }
            catch { /* ignored */ }
            try { _waveIn.Dispose(); } catch { /* ignored */ }
            _waveIn = null;
        }
    }
}
