using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// The Circle of Fifths: an ordered ring of major and minor keys.
/// </summary>
public sealed class CircleOfFifths
{
    public static readonly string[] MajorOrder =
    {
        "C", "G", "D", "A", "E", "B", "F#", "Db", "Ab", "Eb", "Bb", "F"
    };

    public static readonly string[] MinorOrder =
    {
        "A", "E", "B", "F#", "C#", "G#", "D#", "Bb", "F", "C", "G", "D"
    };

    private readonly List<Key> _majors;
    private readonly List<Key> _minors;
    private readonly ScaleBuilder _scaleBuilder;
    private readonly RelativeKeyMap _relativeKeyMap;

    public CircleOfFifths(
        ScaleBuilder? scaleBuilder = null,
        RelativeKeyMap? relativeKeyMap = null)
    {
        _scaleBuilder = scaleBuilder ?? new ScaleBuilder();
        _relativeKeyMap = relativeKeyMap ?? RelativeKeyMap.Default;

        _majors = MajorOrder.Select(n => new Key(new Note(n), KeyType.Major)).ToList();
        _minors = MinorOrder.Select(n => new Key(new Note(n), KeyType.Minor)).ToList();
    }

    public IReadOnlyList<Key> MajorKeys => _majors;
    public IReadOnlyList<Key> MinorKeys => _minors;

    public double SegmentAngle => 360.0 / MajorOrder.Length;

    public Key GetKey(KeyType type, int index)
    {
        var list = type == KeyType.Major ? _majors : _minors;
        return list[index];
    }

    public int IndexOf(Key key)
    {
        var list = key.Type == KeyType.Major ? _majors : _minors;
        return list.FindIndex(k => k.Note.Name == key.Note.Name);
    }

    public Key GetRelativeKey(Key key) => _relativeKeyMap.GetRelative(key);

    public IReadOnlyList<Note> GetTriad(Key key)
    {
        var mode = key.Type == KeyType.Major ? ModeCatalog.Ionian : ModeCatalog.Aeolian;
        var scale = _scaleBuilder.Build(mode, key.Note);
        return new[] { scale[0], scale[2], scale[4] };
    }

    public IReadOnlyList<string> GetProgression(Key key)
    {
        var list = key.Type == KeyType.Major ? _majors : _minors;
        var index = list.FindIndex(k => k.Note.Name == key.Note.Name);

        if (index == -1)
            return Array.Empty<string>();

        var suffix = key.Type == KeyType.Minor ? "m" : "";
        var iv = list[(index + 9) % list.Count].Note.Name + suffix;
        var v = list[(index + 1) % list.Count].Note.Name + suffix;
        var i = list[index].Note.Name + suffix;

        return new[] { i, iv, v, i };
    }
}
