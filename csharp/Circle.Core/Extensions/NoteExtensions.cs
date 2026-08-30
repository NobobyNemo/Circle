using Circle.Core.Domain;

namespace Circle.Core.Extensions;

/// <summary>
/// Helpers that operate on notes without changing the Note class itself.
/// </summary>
public static class NoteExtensions
{
    private static readonly string[][] EnharmonicPairs =
    {
        ["C#", "Db"], ["D#", "Eb"], ["F#", "Gb"], ["G#", "Ab"], ["A#", "Bb"],
        ["B", "Cb"], ["E", "Fb"], ["E#", "F"], ["B#", "C"],
        ["D#m", "Ebm"], ["G#m", "Abm"], ["A#m", "Bbm"], ["F#m", "Gbm"], ["C#m", "Dbm"]
    };

    /// <summary>
    /// Checks whether two note names are enharmonic equivalents.
    /// </summary>
    public static bool IsEnharmonicWith(this Note note, Note other)
    {
        if (note.Name == other.Name)
            return true;

        return EnharmonicPairs.Any(pair => pair.Contains(note.Name) && pair.Contains(other.Name));
    }

    /// <summary>
    /// Returns the enharmonic equivalent of a note name, keeping chord suffixes.
    /// </summary>
    public static string GetEnharmonic(this string noteName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(noteName, @"^([A-G][b#]?)(m|dim)?$");
        if (!match.Success)
            return noteName;

        var baseNote = match.Groups[1].Value;
        var suffix = match.Groups[2].Success ? match.Groups[2].Value : "";

        var enharmonics = new Dictionary<string, string>
        {
            ["B#"] = "C",
            ["E#"] = "F",
            ["Cb"] = "B",
            ["Fb"] = "E",
            ["G#"] = "Ab",
            ["D#"] = "Eb",
            ["A#"] = "Bb",
            ["C#"] = "Db"
        };

        return enharmonics.GetValueOrDefault(baseNote, baseNote) + suffix;
    }
}
