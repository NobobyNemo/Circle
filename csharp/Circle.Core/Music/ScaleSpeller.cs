using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// Spells a scale using each letter name exactly once (A–G).
/// </summary>
public sealed class ScaleSpeller
{
    private readonly ChromaticPalette _palette;

    public ScaleSpeller(ChromaticPalette? palette = null)
    {
        _palette = palette ?? ChromaticPalette.Default;
    }

    public IReadOnlyList<Note> Spell(Note root, IReadOnlyList<int> intervals, bool preferFlats)
    {
        var startIndex = _palette.IndexOf(root.Name);
        if (startIndex == -1)
            startIndex = 0;

        var scale = new List<Note> { new(root.Name) };
        var letter = root.Name[0];
        var position = startIndex;

        for (var i = 0; i < intervals.Count - 1; i++)
        {
            position = (position + intervals[i]) % _palette.Count;
            letter = NextLetter(letter);

            var names = _palette.NamesAt(position);
            var nameToUse = names.FirstOrDefault(n => char.ToUpperInvariant(n[0]) == char.ToUpperInvariant(letter));

            if (nameToUse is null)
                nameToUse = _palette.SelectName(names, preferFlats);

            scale.Add(new Note(nameToUse));
        }

        return scale;
    }

    private static char NextLetter(char letter)
    {
        var letters = new[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G' };
        var index = Array.IndexOf(letters, char.ToUpperInvariant(letter));
        return letters[(index + 1) % letters.Length];
    }
}
