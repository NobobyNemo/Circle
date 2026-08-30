using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// Builds a scale of notes from a mode and a root note.
/// </summary>
public sealed class ScaleBuilder
{
    private readonly ChromaticPalette _palette;

    public ScaleBuilder(ChromaticPalette? palette = null)
    {
        _palette = palette ?? ChromaticPalette.Default;
    }

    /// <summary>
    /// Builds the 7-note scale using the mode's interval pattern.
    /// </summary>
    public IReadOnlyList<Note> Build(Mode mode, Note root)
    {
        var startIndex = _palette.IndexOf(root.Name);
        if (startIndex == -1)
            startIndex = 0;

        var useFlats = root.Name.Contains('b');
        var scale = new List<Note> { root };
        var position = startIndex;

        for (var i = 0; i < mode.Intervals.Count - 1; i++)
        {
            position = (position + mode.Intervals[i]) % _palette.Count;
            var names = _palette.NamesAt(position);
            var name = _palette.SelectName(names, useFlats);
            var enharmonic = _palette.SelectEnharmonic(names, useFlats);
            scale.Add(new Note(name, root.Octave, enharmonic));
        }

        return scale;
    }

    /// <summary>
    /// Builds the scale with a chord quality for each degree.
    /// </summary>
    public IReadOnlyList<ScaleDegree> BuildWithChords(Mode mode, Note root, ModeChords? chords = null)
    {
        chords ??= ModeChords.Default;
        var scale = Build(mode, root);
        var chordTypes = chords.ForMode(ModeCatalog.IndexOf(mode));

        return scale
            .Select((note, index) => new ScaleDegree(note, chordTypes[index]))
            .ToList();
    }
}
