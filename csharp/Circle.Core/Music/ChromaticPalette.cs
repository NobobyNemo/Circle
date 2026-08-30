namespace Circle.Core.Music;

/// <summary>
/// The 12 pitch classes with their enharmonic spellings.
/// </summary>
public sealed class ChromaticPalette
{
    public static ChromaticPalette Default { get; } = new();

    private readonly string[][] _pitchClasses =
    {
        ["C"],
        ["C#", "Db"],
        ["D"],
        ["D#", "Eb"],
        ["E", "Fb"],
        ["F", "E#"],
        ["F#", "Gb"],
        ["G"],
        ["G#", "Ab"],
        ["A"],
        ["A#", "Bb"],
        ["B", "Cb"]
    };

    /// <summary>
    /// Returns the index of the pitch class that contains the given note name.
    /// </summary>
    public int IndexOf(string noteName)
    {
        for (var i = 0; i < _pitchClasses.Length; i++)
        {
            if (_pitchClasses[i].Contains(noteName))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Returns all names for a pitch class at the given index.
    /// </summary>
    public IReadOnlyList<string> NamesAt(int index)
    {
        var safeIndex = ((index % _pitchClasses.Length) + _pitchClasses.Length) % _pitchClasses.Length;
        return _pitchClasses[safeIndex];
    }

    /// <summary>
    /// Picks the primary or secondary spelling based on the user's preference for flats.
    /// </summary>
    public string SelectName(IReadOnlyList<string> names, bool preferFlats)
    {
        if (names.Count > 1)
            return preferFlats ? names[1] : names[0];

        return names[0];
    }

    /// <summary>
    /// Returns the enharmonic alias for the chosen spelling, if any.
    /// </summary>
    public string? SelectEnharmonic(IReadOnlyList<string> names, bool preferFlats)
    {
        if (names.Count > 1)
            return preferFlats ? names[0] : names[1];

        return null;
    }

    public int Count => _pitchClasses.Length;
}
