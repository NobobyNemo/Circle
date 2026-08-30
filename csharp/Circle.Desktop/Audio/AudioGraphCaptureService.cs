using System.Runtime.InteropServices;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Media.Render;

namespace Circle.Desktop.Audio;

/// <summary>
/// Microphone capture using the Windows AudioGraph (WinRT) API.
/// Captures the system default audio input device and delivers float samples.
/// </summary>
public sealed class AudioGraphCaptureService : IAudioCaptureService
{
    private readonly object _lock = new();
    private AudioGraph? _audioGraph;
    private AudioFrameOutputNode? _frameOutputNode;
    private int _channelCount = 1;

    public event EventHandler<float[]>? SamplesCaptured;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler<string>? DebugMessage;

    public IReadOnlyList<AudioDevice> AvailableInputDevices { get; }
    public AudioDevice? SelectedInputDevice { get; set; }
    public int SampleRate { get; private set; } = 44100;

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

    public AudioGraphCaptureService()
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

    public void Start()
    {
        Stop();

        _ = InitializeAndStartAsync();
    }

    private async Task InitializeAndStartAsync()
    {
        try
        {
            // Diagnostic: check what devices Windows actually sees
            var diag = new System.Text.StringBuilder();

            // Check WinRT device enumeration
            try
            {
                var winrtDevices = await DeviceInformation.FindAllAsync(MediaDevice.GetAudioCaptureSelector()).AsTask().ConfigureAwait(false);
                diag.AppendLine($"WinRT capture devices: {winrtDevices.Count}");
                foreach (var d in winrtDevices)
                    diag.AppendLine($"  - {d.Name} (Id={d.Id}, IsEnabled={d.IsEnabled})");
            }
            catch (Exception ex) { diag.AppendLine($"WinRT enum error: {ex.Message}"); }

            // Check NAudio WASAPI
            try
            {
                var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
                var all = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.All);
                diag.AppendLine($"WASAPI capture devices (All states): {all.Count}");
                foreach (var d in all)
                    diag.AppendLine($"  - {d.FriendlyName} (State={d.State})");
                var active = enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.Active);
                diag.AppendLine($"WASAPI active capture devices: {active.Count}");
            }
            catch (Exception ex) { diag.AppendLine($"WASAPI enum error: {ex.Message}"); }

            // Check MME
            try
            {
                diag.AppendLine($"MME WaveIn device count: {NAudio.Wave.WaveInEvent.DeviceCount}");
                for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
                {
                    var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                    diag.AppendLine($"  - [{i}] {caps.ProductName} (Channels={caps.Channels})");
                }
            }
            catch (Exception ex) { diag.AppendLine($"MME enum error: {ex.Message}"); }

            // Check default device
            try
            {
                var defaultId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
                diag.AppendLine($"Default capture device ID: {defaultId ?? "(null)"}");
            }
            catch (Exception ex) { diag.AppendLine($"Default device error: {ex.Message}"); }

            var settings = new AudioGraphSettings(AudioRenderCategory.Media);
            DebugMessage?.Invoke(this, "AudioGraph: Creating graph...");

            var graphResult = await AudioGraph.CreateAsync(settings).AsTask().ConfigureAwait(false);
            DebugMessage?.Invoke(this, $"AudioGraph: Create status={graphResult.Status}");
            if (graphResult.Status != AudioGraphCreationStatus.Success)
            {
                ErrorOccurred?.Invoke(this, $"Could not create AudioGraph: {graphResult.Status}\n\nDiagnostics:\n{diag}");
                return;
            }

            var audioGraph = graphResult.Graph;
            var encodingProperties = audioGraph.EncodingProperties;
            DebugMessage?.Invoke(this, $"AudioGraph: Encoding {encodingProperties.SampleRate}Hz {encodingProperties.ChannelCount}ch {encodingProperties.Subtype}");

            // Use the simplest overload - default device, no encoding properties override
            DebugMessage?.Invoke(this, "AudioGraph: Creating device input node (simple)...");
            var inputResult = await audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media)
                .AsTask().ConfigureAwait(false);
            DebugMessage?.Invoke(this, $"AudioGraph: Input node status={inputResult.Status}");

            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Try with explicit encoding properties and default device
                DebugMessage?.Invoke(this, "AudioGraph: Creating device input node (with encoding)...");
                inputResult = await audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media, encodingProperties)
                    .AsTask().ConfigureAwait(false);
                DebugMessage?.Invoke(this, $"AudioGraph: Input node status={inputResult.Status}");
            }

            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                // Try with selected device
                var selectedDeviceInfo = SelectedDeviceInfo();
                if (selectedDeviceInfo is not null)
                {
                    DebugMessage?.Invoke(this, $"AudioGraph: Creating device input node (device={selectedDeviceInfo.Name})...");
                    inputResult = await audioGraph.CreateDeviceInputNodeAsync(MediaCategory.Media, encodingProperties, selectedDeviceInfo)
                        .AsTask().ConfigureAwait(false);
                    DebugMessage?.Invoke(this, $"AudioGraph: Input node status={inputResult.Status}");
                }
            }

            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                var extError = inputResult.ExtendedError?.Message ?? "(none)";
                ErrorOccurred?.Invoke(this, $"Could not create audio input node: {inputResult.Status} ({extError})\n\nDiagnostics:\n{diag}");
                audioGraph.Dispose();
                return;
            }

            var frameOutputNode = audioGraph.CreateFrameOutputNode(encodingProperties);
            inputResult.DeviceInputNode.AddOutgoingConnection(frameOutputNode);
            audioGraph.QuantumStarted += OnQuantumStarted;

            lock (_lock)
            {
                _audioGraph = audioGraph;
                _frameOutputNode = frameOutputNode;
                SampleRate = (int)encodingProperties.SampleRate;
                _channelCount = (int)encodingProperties.ChannelCount;
            }

            audioGraph.Start();
            DebugMessage?.Invoke(this, $"AudioGraph: Started! SampleRate={SampleRate}, Ch={_channelCount}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"AudioGraph capture failed: {ex.Message}");
        }
    }

    private DeviceInformation? SelectedDeviceInfo()
    {
        if (SelectedInputDevice?.DeviceId is null)
            return null;

        try
        {
            return Task.Run(async () =>
                await DeviceInformation.CreateFromIdAsync(SelectedInputDevice.DeviceId).AsTask())
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return null;
        }
    }

    private int _frameCount = 0;

    private void OnQuantumStarted(AudioGraph sender, object args)
    {
        AudioFrame? frame = null;

        try
        {
            int channelCount;
            lock (_lock)
            {
                frame = _frameOutputNode?.GetFrame();
                channelCount = _channelCount;
            }

            if (frame is null)
                return;

            var samples = ConvertFrameToFloat(frame, channelCount);
            if (samples.Length > 0)
            {
                _frameCount++;
                if (_frameCount <= 5)
                {
                    var maxAbs = samples.Max(Math.Abs);
                    DebugMessage?.Invoke(this, $"AudioGraph: Frame {_frameCount}: {samples.Length} samples, max={maxAbs:F4}");
                }
                SamplesCaptured?.Invoke(this, samples);
            }
        }
        catch
        {
            // ignored
        }
        finally
        {
            frame?.Dispose();
        }
    }

    private static unsafe float[] ConvertFrameToFloat(AudioFrame frame, int channelCount)
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
        lock (_lock)
        {
            if (_audioGraph is not null)
            {
                try
                {
                    _audioGraph.QuantumStarted -= OnQuantumStarted;
                    _audioGraph.Stop();
                    _audioGraph.Dispose();
                }
                catch
                {
                    // ignored
                }
                _audioGraph = null;
            }

            _frameOutputNode = null;
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
