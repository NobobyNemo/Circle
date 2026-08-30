using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// Provides information about every mode for a given key.
/// </summary>
public sealed class ModeService
{
    private readonly ScaleBuilder _scaleBuilder;
    private readonly ModeChords _modeChords;

    public ModeService(ScaleBuilder? scaleBuilder = null, ModeChords? modeChords = null)
    {
        _scaleBuilder = scaleBuilder ?? new ScaleBuilder();
        _modeChords = modeChords ?? ModeChords.Default;
    }

    public IReadOnlyList<ModeInfo> GetModesForKey(Key key)
    {
        return ModeCatalog.Modes
            .Select(mode => new ModeInfo(
                mode,
                _scaleBuilder.Build(mode, key.Note),
                _scaleBuilder.BuildWithChords(mode, key.Note, _modeChords)))
            .ToList();
    }

    public IReadOnlyList<Note> GetScaleForKeyAndMode(Key key, Mode mode)
        => _scaleBuilder.Build(mode, key.Note);

    public IReadOnlyList<ScaleDegree> GetChordsForKeyAndMode(Key key, Mode mode)
        => _scaleBuilder.BuildWithChords(mode, key.Note, _modeChords);
}

public sealed record ModeInfo(
    Mode Mode,
    IReadOnlyList<Note> Scale,
    IReadOnlyList<ScaleDegree> ScaleWithChords);
