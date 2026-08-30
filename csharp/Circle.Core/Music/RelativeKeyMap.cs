using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// Maps major keys to their relative minor keys and vice versa.
/// </summary>
public sealed class RelativeKeyMap
{
    public static RelativeKeyMap Default { get; } = new();

    private readonly Dictionary<string, string> _majorToMinor = new()
    {
        ["C"] = "A",
        ["G"] = "E",
        ["D"] = "B",
        ["A"] = "F#",
        ["E"] = "C#",
        ["B"] = "G#",
        ["F#"] = "D#",
        ["Db"] = "Bb",
        ["Ab"] = "F",
        ["Eb"] = "C",
        ["Bb"] = "G",
        ["F"] = "D"
    };

    private readonly Dictionary<string, string> _minorToMajor;

    public RelativeKeyMap()
    {
        _minorToMajor = _majorToMinor
            .ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    public Key GetRelative(Key key)
    {
        var cleanName = key.Note.Name
            .Replace('♯', '#')
            .Replace('♭', 'b');

        if (key.Type == KeyType.Major)
        {
            var relativeName = _majorToMinor.GetValueOrDefault(cleanName, cleanName);
            return new Key(new Note(relativeName), KeyType.Minor);
        }

        var relativeMajor = _minorToMajor.GetValueOrDefault(cleanName, cleanName);
        return new Key(new Note(relativeMajor), KeyType.Major);
    }
}
