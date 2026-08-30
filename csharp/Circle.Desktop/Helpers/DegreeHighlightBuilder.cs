using Circle.Core.Domain;
using Circle.Core.Extensions;
using Circle.Core.Music;
using Circle.Desktop.Models;

namespace Circle.Desktop.Helpers;

/// <summary>
/// Builds a map of note name to degree highlight for the currently selected key.
/// </summary>
public sealed class DegreeHighlightBuilder
{
    private readonly ScaleSpeller _scaleSpeller;

    public DegreeHighlightBuilder(ScaleSpeller? scaleSpeller = null)
    {
        _scaleSpeller = scaleSpeller ?? new ScaleSpeller();
    }

    public IReadOnlyDictionary<string, DegreeHighlight> BuildForKey(Key key)
    {
        var result = new Dictionary<string, DegreeHighlight>();
        if (key is null)
            return result;

        var modesInfo = new ModeService().GetModesForKey(key);
        var modeIndex = key.Type == KeyType.Major ? 0 : 5;
        var modeInfo = modesInfo[modeIndex];
        var preferFlats = key.IsFlat();
        var spelledScale = _scaleSpeller.Spell(key.Note, modeInfo.Mode.Intervals, preferFlats);

        for (var i = 0; i < spelledScale.Count; i++)
        {
            var chordType = modeInfo.ScaleWithChords[i].ChordType;
            var ring = chordType == "maj" ? KeyType.Major : KeyType.Minor;
            var note = spelledScale[i];
            result[note.Name] = new DegreeHighlight(ring, i, RainbowColors.DegreeLabels[i], note);
        }

        return result;
    }
}
