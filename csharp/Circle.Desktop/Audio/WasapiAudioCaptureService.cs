using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace Circle.Desktop.Audio;

/// <summary>
/// Windows audio capture using WASAPI or MME (winmm) with device enumeration.
/// Uses the device's native mix format and converts samples to mono float.
/// </summary>
public sealed class WasapiAudioCaptureService : IAudioCaptureService
{
    private IWaveIn? _capture;
    private AsioOut? _asioOut;
    private readonly List<MMDevice> _wasapiDevices = new();
    private readonly List<int> _mmeDeviceNumbers = new();
    private readonly List<string> _asioDriverNames = new();
    private readonly object _lock = new();

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }

    public int SampleRate { get; private set; } = 44100;

    public Task<bool> RequestAccessAsync() => Task.FromResult(true);

    public WasapiAudioCaptureService()
    {
        var devices = new List<AudioDevice>();

        EnumerateWasapiDevices(devices);
        EnumerateMmeDevices(devices);
        EnumerateAsioDevices(devices);

        AvailableInputDevices = devices;
        SelectedInputDevice ??= devices.FirstOrDefault();
    }

    private void EnumerateWasapiDevices(List<AudioDevice> devices)
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.All))
            {
                _wasapiDevices.Add(device);
                var stateLabel = device.State != DeviceState.Active ? $" ({device.State})" : "";
                devices.Add(new AudioDevice(_wasapiDevices.Count - 1, device.FriendlyName + stateLabel, false));
            }

            try
            {
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
                    ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

                if (defaultDevice is not null)
                {
                    var index = _wasapiDevices.FindIndex(d => d.ID == defaultDevice.ID);
                    if (index >= 0)
                        SelectedInputDevice = devices[index];
                }
            }
            catch
            {
                // ignored
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not enumerate WASAPI microphones: {ex.Message}");
        }
    }

    private void EnumerateMmeDevices(List<AudioDevice> devices)
    {
        try
        {
            var count = waveInGetNumDevs();
            for (var i = 0u; i < count; i++)
            {
                var caps = new WAVEINCAPS();
                if (waveInGetDevCaps(i, ref caps, (uint)Marshal.SizeOf<WAVEINCAPS>()) == 0)
                {
                    _mmeDeviceNumbers.Add((int)i);
                    devices.Add(new AudioDevice(_mmeDeviceNumbers.Count - 1, $"{caps.szPname} (MME)", true));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not enumerate MME microphones: {ex.Message}");
        }
    }

    private void EnumerateAsioDevices(List<AudioDevice> devices)
    {
        try
        {
            foreach (var driverName in AsioOut.GetDriverNames())
            {
                _asioDriverNames.Add(driverName);
                devices.Add(new AudioDevice(_asioDriverNames.Count - 1, $"{driverName} (ASIO)", false, true));
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Could not enumerate ASIO drivers: {ex.Message}");
        }
    }

    public void Start()
    {
        Stop();

        lock (_lock)
        {
            try
            {
                if (SelectedInputDevice is null)
                {
                    ErrorOccurred?.Invoke(this, "No microphone selected or available.");
                    return;
                }

                if (SelectedInputDevice.IsMme)
                    StartMmeCapture(SelectedInputDevice.DeviceNumber);
                else if (SelectedInputDevice.IsAsio)
                    StartAsioCapture(SelectedInputDevice.DeviceNumber);
                else
                    StartWasapiCapture(SelectedInputDevice.DeviceNumber);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to start microphone: {ex.Message}");
                _capture?.Dispose();
                _capture = null;
            }
        }
    }

    private void StartWasapiCapture(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= _wasapiDevices.Count)
        {
            ErrorOccurred?.Invoke(this, "Selected WASAPI device is not available.");
            return;
        }

        var device = _wasapiDevices[deviceIndex];
        _capture = new WasapiCapture(device);
        _capture.DataAvailable += OnDataAvailable;
        _capture.StartRecording();

        SampleRate = _capture.WaveFormat.SampleRate;
    }

    private void StartMmeCapture(int mmeListIndex)
    {
        if (mmeListIndex < 0 || mmeListIndex >= _mmeDeviceNumbers.Count)
        {
            ErrorOccurred?.Invoke(this, "Selected MME device is not available.");
            return;
        }

        var deviceNumber = _mmeDeviceNumbers[mmeListIndex];
        var sampleRates = new[] { 44100, 48000, 88200, 96000, 192000, 22050, 11025 };

        _capture = null;
        foreach (var rate in sampleRates)
        {
            _capture = TryStartMmeWaveIn(deviceNumber, rate, 2, 16) ?? TryStartMmeWaveIn(deviceNumber, rate, 1, 16)
                ?? TryStartMmeWaveIn(deviceNumber, rate, 2, 24) ?? TryStartMmeWaveIn(deviceNumber, rate, 1, 24)
                ?? TryStartMmeWaveInFloat(deviceNumber, rate, 2) ?? TryStartMmeWaveInFloat(deviceNumber, rate, 1)
                ?? TryStartMmeWaveIn(deviceNumber, rate, 2, 8) ?? TryStartMmeWaveIn(deviceNumber, rate, 1, 8);

            if (_capture is not null)
                break;
        }

        if (_capture is null)
        {
            ErrorOccurred?.Invoke(this, $"Could not find a supported PCM/IEEE-float format for MME device {deviceNumber}.");
            return;
        }

        SampleRate = _capture.WaveFormat.SampleRate;
    }

    private WaveInEvent? TryStartMmeWaveIn(int deviceNumber, int sampleRate, int channels, int bitsPerSample)
    {
        var capture = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(sampleRate, bitsPerSample, channels),
            BufferMilliseconds = 50
        };

        return TryStartCapture(capture);
    }

    private WaveInEvent? TryStartMmeWaveInFloat(int deviceNumber, int sampleRate, int channels)
    {
        var capture = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels),
            BufferMilliseconds = 50
        };

        return TryStartCapture(capture);
    }

    private WaveInEvent? TryStartCapture(WaveInEvent capture)
    {
        try
        {
            capture.DataAvailable += OnDataAvailable;
            capture.StartRecording();
            return capture;
        }
        catch
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.Dispose();
            return null;
        }
    }

    private void StartAsioCapture(int asioListIndex)
    {
        if (asioListIndex < 0 || asioListIndex >= _asioDriverNames.Count)
        {
            ErrorOccurred?.Invoke(this, "Selected ASIO driver is not available.");
            return;
        }

        var driverName = _asioDriverNames[asioListIndex];
        var sampleRates = new[] { 44100, 48000, 88200, 96000, 192000, 22050, 11025 };

        AsioOut? asioOut = null;
        foreach (var rate in sampleRates)
        {
            asioOut = TryStartAsio(driverName, rate);
            if (asioOut is not null)
                break;
        }

        if (asioOut is null)
        {
            ErrorOccurred?.Invoke(this, $"Could not start ASIO driver '{driverName}' with any supported sample rate.");
            return;
        }

        _asioOut = asioOut;
    }

    private AsioOut? TryStartAsio(string driverName, int sampleRate)
    {
        AsioOut? asioOut = null;
        try
        {
            asioOut = new AsioOut(driverName);

            if (!asioOut.IsSampleRateSupported(sampleRate))
            {
                asioOut.Dispose();
                return null;
            }

            asioOut.AudioAvailable += OnAsioAudioAvailable;
            asioOut.InitRecordAndPlayback(null, asioOut.DriverInputChannelCount, sampleRate);
            asioOut.Play();

            SampleRate = sampleRate;
            return asioOut;
        }
        catch
        {
            if (asioOut is not null)
            {
                asioOut.AudioAvailable -= OnAsioAudioAvailable;
                asioOut.Dispose();
            }
            return null;
        }
    }

    private void OnAsioAudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
    {
        try
        {
            e.WrittenToOutputBuffers = true;
            var samples = ConvertAsioToMono(e);
            if (samples.Length > 0)
                SamplesCaptured?.Invoke(this, samples);
        }
        catch
        {
            // ignored
        }
    }

    private static unsafe float[] ConvertAsioToMono(AsioAudioAvailableEventArgs e)
    {
        var channelCount = e.InputBuffers.Length;
        var sampleCount = e.SamplesPerBuffer;

        if (channelCount == 0 || sampleCount == 0)
            return Array.Empty<float>();

        var (bytesPerSample, divisor) = e.AsioSampleType switch
        {
            AsioSampleType.Int16LSB => (2, 32768f),
            AsioSampleType.Int24LSB => (3, 8388608f),
            AsioSampleType.Int32LSB => (4, 2147483648f),
            AsioSampleType.Int32LSB16 => (4, 32768f),
            AsioSampleType.Int32LSB18 => (4, 131072f),
            AsioSampleType.Int32LSB20 => (4, 524288f),
            AsioSampleType.Int32LSB24 => (4, 8388608f),
            AsioSampleType.Float32LSB => (4, 1f),
            AsioSampleType.Float64LSB => (8, 1f),
            _ => (0, 0f)
        };

        if (bytesPerSample == 0)
            return Array.Empty<float>();

        var mono = new float[sampleCount];

        for (var i = 0; i < sampleCount; i++)
        {
            var sum = 0f;
            for (var ch = 0; ch < channelCount; ch++)
            {
                var buffer = (byte*)e.InputBuffers[ch];
                sum += ReadAsioSample(buffer + i * bytesPerSample, e.AsioSampleType, divisor);
            }

            mono[i] = sum < -1f ? -1f : sum > 1f ? 1f : sum;
        }

        return mono;
    }

    private static unsafe float ReadAsioSample(byte* ptr, AsioSampleType type, float divisor)
    {
        switch (type)
        {
            case AsioSampleType.Int16LSB:
                return *(short*)ptr / divisor;
            case AsioSampleType.Int24LSB:
                var value24 = ptr[0] | (ptr[1] << 8) | (ptr[2] << 16);
                if ((value24 & 0x800000) != 0)
                    value24 -= 0x1000000;
                return value24 / divisor;
            case AsioSampleType.Int32LSB:
            case AsioSampleType.Int32LSB16:
            case AsioSampleType.Int32LSB18:
            case AsioSampleType.Int32LSB20:
            case AsioSampleType.Int32LSB24:
                return *(int*)ptr / divisor;
            case AsioSampleType.Float32LSB:
                return *(float*)ptr;
            case AsioSampleType.Float64LSB:
                return (float)*(double*)ptr;
            default:
                return 0f;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_capture is not null)
            {
                try
                {
                    _capture.StopRecording();
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.Dispose();
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    _capture = null;
                }
            }

            if (_asioOut is not null)
            {
                try
                {
                    _asioOut.AudioAvailable -= OnAsioAudioAvailable;
                    _asioOut.Stop();
                    _asioOut.Dispose();
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    _asioOut = null;
                }
            }
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture is null)
            return;

        var format = _capture.WaveFormat;
        var samples = ConvertToMonoFloat(e.Buffer, e.BytesRecorded, format);
        if (samples.Length > 0)
            SamplesCaptured?.Invoke(this, samples);
    }

    private static float[] ConvertToMonoFloat(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        var samples = new List<float>();

        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var waveBuffer = new WaveBuffer(buffer);
            var floatBuffer = waveBuffer.FloatBuffer;
            var totalFloats = bytesRecorded / 4;

            if (format.Channels == 1)
            {
                for (var i = 0; i < totalFloats; i++)
                    samples.Add(floatBuffer[i]);
            }
            else if (format.Channels == 2)
            {
                for (var i = 0; i < totalFloats; i += 2)
                    samples.Add((floatBuffer[i] + floatBuffer[i + 1]) / 2f);
            }
        }
        else if (format.BitsPerSample == 16)
        {
            var waveBuffer = new WaveBuffer(buffer);
            var shortBuffer = waveBuffer.ShortBuffer;
            var totalShorts = bytesRecorded / 2;

            if (format.Channels == 1)
            {
                for (var i = 0; i < totalShorts; i++)
                    samples.Add(shortBuffer[i] / 32768f);
            }
            else if (format.Channels == 2)
            {
                for (var i = 0; i < totalShorts; i += 2)
                    samples.Add(((shortBuffer[i] + shortBuffer[i + 1]) / 2f) / 32768f);
            }
        }
        else if (format.BitsPerSample == 24)
        {
            var bytesPerFrame = 3 * format.Channels;
            var frameCount = bytesRecorded / bytesPerFrame;

            for (var i = 0; i < frameCount; i++)
            {
                var offset = i * bytesPerFrame;
                var left = ReadInt24(buffer, offset) / 8388608f;

                if (format.Channels == 1)
                {
                    samples.Add(left);
                }
                else if (format.Channels == 2)
                {
                    var right = ReadInt24(buffer, offset + 3) / 8388608f;
                    samples.Add((left + right) / 2f);
                }
            }
        }
        else if (format.BitsPerSample == 32)
        {
            var totalInts = bytesRecorded / 4;

            if (format.Channels == 1)
            {
                for (var i = 0; i < totalInts; i++)
                    samples.Add(BitConverter.ToInt32(buffer, i * 4) / 2147483648f);
            }
            else if (format.Channels == 2)
            {
                for (var i = 0; i < totalInts; i += 2)
                    samples.Add(((BitConverter.ToInt32(buffer, i * 4) + BitConverter.ToInt32(buffer, (i + 1) * 4)) / 2f) / 2147483648f);
            }
        }
        else if (format.BitsPerSample == 8)
        {
            var totalBytes = bytesRecorded;

            if (format.Channels == 1)
            {
                for (var i = 0; i < totalBytes; i++)
                    samples.Add((buffer[i] - 128) / 128f);
            }
            else if (format.Channels == 2)
            {
                for (var i = 0; i < totalBytes; i += 2)
                    samples.Add(((buffer[i] + buffer[i + 1] - 256) / 2f) / 128f);
            }
        }

        return samples.ToArray();
    }

    private static int ReadInt24(byte[] buffer, int offset)
    {
        var value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        if ((value & 0x800000) != 0)
            value -= 0x1000000;
        return value;
    }

    [DllImport("winmm.dll")]
    private static extern uint waveInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Ansi)]
    private static extern uint waveInGetDevCaps(uint uDeviceID, ref WAVEINCAPS pwic, uint cbwic);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct WAVEINCAPS
    {
        public short wMid;
        public short wPid;
        public uint vDriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwFormats;
        public short wChannels;
        public short wReserved1;
    }
}
