namespace Circle.Core.Music;

/// <summary>
/// Chord quality for each scale degree for every predefined mode.
/// </summary>
public sealed class ModeChords
{
    public static ModeChords Default { get; } = new();

    private readonly string[][] _chordTypes =
    {
        // Ionian (Major)
        ["maj", "min", "min", "maj", "maj", "min", "dim"],
        // Dorian
        ["min", "min", "maj", "maj", "min", "dim", "maj"],
        // Phrygian
        ["min", "maj", "maj", "min", "dim", "maj", "min"],
        // Lydian
        ["maj", "maj", "min", "dim", "maj", "min", "min"],
        // Mixolydian
        ["maj", "min", "dim", "maj", "min", "min", "maj"],
        // Aeolian (Minor)
        ["min", "dim", "maj", "min", "min", "maj", "maj"],
        // Locrian
        ["dim", "maj", "min", "min", "maj", "maj", "min"]
    };

    public IReadOnlyList<string> ForMode(int modeIndex)
    {
        return _chordTypes[modeIndex];
    }
}
