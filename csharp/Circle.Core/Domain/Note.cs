namespace Circle.Core.Domain;

/// <summary>
/// A musical note with a name, octave and optional enharmonic alias.
/// </summary>
public sealed class Note
{
    public string Name { get; }
    public int Octave { get; }
    public string? Enharmonic { get; }

    public Note(string name, int octave = 4, string? enharmonic = null)
    {
        Name = name;
        Octave = octave;
        Enharmonic = enharmonic;
    }

    public override string ToString() => Name;

    public bool Equals(Note? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            || (Enharmonic is not null && Enharmonic == other.Name)
            || (other.Enharmonic is not null && other.Enharmonic == Name);
    }

    public override bool Equals(object? obj) => obj is Note other && Equals(other);

    public override int GetHashCode() => Name.GetHashCode();
}
