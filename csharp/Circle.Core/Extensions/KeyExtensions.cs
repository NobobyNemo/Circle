using Circle.Core.Domain;
using Circle.Core.Music;

namespace Circle.Core.Extensions;

/// <summary>
/// Cross-cutting helpers for keys.
/// </summary>
public static class KeyExtensions
{
    private static readonly string[] MajorFlatKeys = { "F", "Bb", "Eb", "Ab", "Db", "Gb", "Cb" };
    private static readonly string[] MinorFlatKeys = { "Dm", "Gm", "Cm", "Fm", "Bbm", "Ebm", "Abm" };

    public static string Label(this Key key)
        => key.Type == KeyType.Major ? key.Note.Name : key.Note.Name + "m";

    public static Key GetRelative(this Key key)
        => RelativeKeyMap.Default.GetRelative(key);

    public static bool IsFlat(this Key key)
    {
        var label = key.Label();
        return key.Type == KeyType.Major
            ? MajorFlatKeys.Contains(label)
            : MinorFlatKeys.Contains(label);
    }

    public static bool MatchesScaleNote(this Key key, Note scaleNote)
        => key.Note.Name == scaleNote.Name;

    public static bool MatchesScaleChord(this Key key, Note scaleNote, string chordType)
    {
        return chordType switch
        {
            "maj" => key.Type == KeyType.Major && key.Note.Name == scaleNote.Name,
            "min" => key.Type == KeyType.Minor && key.Note.Name == scaleNote.Name,
            "dim" => key.Type == KeyType.Minor && key.Note.Name == scaleNote.Name,
            _ => false
        };
    }
}
