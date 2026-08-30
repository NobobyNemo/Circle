namespace Circle.Core.Domain;

/// <summary>
/// A musical key: a root note plus a major or minor quality.
/// </summary>
public sealed class Key
{
    public Note Note { get; }
    public KeyType Type { get; }

    public Key(Note note, KeyType type)
    {
        Note = note;
        Type = type;
    }

    public override string ToString() => $"{Note.Name} {Type}";

    public override bool Equals(object? obj) => obj is Key other && Equals(other);

    public bool Equals(Key? other)
    {
        if (other is null) return false;
        return Type == other.Type && Note.Equals(other.Note);
    }

    public override int GetHashCode() => HashCode.Combine(Note.Name, Type);
}
