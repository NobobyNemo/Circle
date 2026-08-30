namespace Circle.Core.Domain;

/// <summary>
/// A scale degree: a note and the chord quality built on that degree.
/// </summary>
public sealed class ScaleDegree
{
    public Note Note { get; }
    public string ChordType { get; }

    public ScaleDegree(Note note, string chordType)
    {
        Note = note;
        ChordType = chordType;
    }

    public override string ToString() => $"{Note.Name} ({ChordType})";
}
