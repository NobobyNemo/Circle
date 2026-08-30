namespace Circle.Desktop.Audio;

public sealed class PitchDetector
{
    private readonly int _sampleRate;
    private readonly int _minPeriod;
    private readonly int _maxPeriod;
    private readonly float[] _buffer;
    private int _bufferIndex;

    public PitchDetector(int sampleRate, float minFrequency = 65f, float maxFrequency = 1200f, int bufferSize = 8192)
    {
        _sampleRate = sampleRate;
        _minPeriod = (int)(sampleRate / maxFrequency);
        _maxPeriod = (int)(sampleRate / minFrequency);
        _buffer = new float[bufferSize];
    }

    public float? DetectPitch(float[] samples)
    {
        AppendSamples(samples);

        var nsdfLength = Math.Min(_maxPeriod + 2, _buffer.Length);
        var nsdf = ComputeNsdf(_buffer, nsdfLength);
        var tau = FindPeak(nsdf, _minPeriod, _maxPeriod);
        if (tau is null)
            return null;

        var parabolicTau = ParabolicInterpolation(nsdf, tau.Value);
        return _sampleRate / parabolicTau;
    }

    private void AppendSamples(float[] samples)
    {
        if (samples.Length >= _buffer.Length)
        {
            samples.AsSpan(samples.Length - _buffer.Length).CopyTo(_buffer);
            _bufferIndex = 0;
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            _buffer[_bufferIndex] = samples[i];
            _bufferIndex = (_bufferIndex + 1) % _buffer.Length;
        }
    }

    private static float[] ComputeNsdf(float[] buffer, int nsdfLength)
    {
        var nsdf = new float[nsdfLength];

        for (var tau = 0; tau < nsdfLength; tau++)
        {
            double acf = 0;
            double divisorM = 0;

            for (var i = 0; i < buffer.Length - tau; i++)
            {
                acf += buffer[i] * buffer[i + tau];
                divisorM += buffer[i] * buffer[i] + buffer[i + tau] * buffer[i + tau];
            }

            nsdf[tau] = divisorM > 0 ? (float)(2 * acf / divisorM) : 0f;
        }

        return nsdf;
    }

    private static int? FindPeak(float[] nsdf, int minPeriod, int maxPeriod)
    {
        var threshold = 0.85f;
        var bestTau = (int?)null;
        var bestValue = 0f;

        for (var tau = minPeriod; tau <= maxPeriod && tau < nsdf.Length; tau++)
        {
            if (nsdf[tau] > threshold && nsdf[tau] > bestValue)
            {
                if (IsLocalMax(nsdf, tau))
                {
                    bestTau = tau;
                    bestValue = nsdf[tau];
                }
            }
        }

        return bestTau;
    }

    private static bool IsLocalMax(float[] nsdf, int index)
    {
        if (index <= 0 || index >= nsdf.Length - 1)
            return false;
        return nsdf[index] >= nsdf[index - 1] && nsdf[index] >= nsdf[index + 1];
    }

    private static float ParabolicInterpolation(float[] nsdf, int tau)
    {
        if (tau <= 0 || tau >= nsdf.Length - 1)
            return tau;

        var alpha = nsdf[tau - 1];
        var beta = nsdf[tau];
        var gamma = nsdf[tau + 1];
        var p = 0.5f * (alpha - gamma) / (alpha - 2 * beta + gamma);
        return tau + p;
    }

    public static (string NoteName, int Cents, float Frequency) Analyze(float frequency)
    {
        var noteIndex = 12 * (float)Math.Log2(frequency / 440f) + 69;
        var rounded = (int)Math.Round(noteIndex);

        var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var octave = rounded / 12 - 1;
        var name = noteNames[rounded % 12] + octave;

        var noteFrequency = 440f * (float)Math.Pow(2, (rounded - 69) / 12.0);
        var deviation = (int)Math.Round(1200 * Math.Log2(frequency / noteFrequency));

        return (name, deviation, frequency);
    }
}
