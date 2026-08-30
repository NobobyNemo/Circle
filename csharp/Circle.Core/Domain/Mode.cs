namespace Circle.Core.Domain;

/// <summary>
/// A diatonic mode: a name and the whole-step/half-step interval pattern.
/// </summary>
public sealed class Mode
{
    public string Name { get; }
    public IReadOnlyList<int> Intervals { get; }

    public Mode(string name, int[] intervals)
    {
        Name = name;
        Intervals = intervals;
    }

    public override string ToString() => Name;
}
