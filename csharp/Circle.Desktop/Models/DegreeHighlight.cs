using Circle.Core.Domain;

namespace Circle.Desktop.Models;

/// <summary>
/// Information used to highlight a scale degree on the circle.
/// </summary>
public sealed class DegreeHighlight
{
    public KeyType Ring { get; }
    public int DegreeIndex { get; }
    public string DegreeLabel { get; }
    public Note Note { get; }

    public DegreeHighlight(KeyType ring, int degreeIndex, string degreeLabel, Note note)
    {
        Ring = ring;
        DegreeIndex = degreeIndex;
        DegreeLabel = degreeLabel;
        Note = note;
    }
}
