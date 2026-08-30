using System.Text.RegularExpressions;

namespace Circle.Desktop.Models;

public sealed record StringFret(int StringNumber, string Note, int Fret);

public sealed class ChordVoicing
{
    public IReadOnlyList<StringFret> Positions { get; }
    public string Label { get; }

    public ChordVoicing(string notation, string label)
    {
        Label = label;
        Positions = Regex.Matches(notation, @"(?<string>\d+)\s*[/\\]\s*(?<note>[A-Ga-g](?:#|b)?)\s*:\s*(?<fret>-?\d+)")
            .Select(match => new StringFret(
                int.Parse(match.Groups["string"].Value),
                match.Groups["note"].Value,
                int.Parse(match.Groups["fret"].Value)))
            .OrderByDescending(position => position.StringNumber)
            .ToArray();
    }

    public override string ToString() => Label;
}
