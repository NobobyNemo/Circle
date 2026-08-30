namespace Circle.Core.Utils;

/// <summary>
/// Calculates note frequencies and shifts notes by semitones.
/// </summary>
public static class FrequencyCalculator
{
    private static readonly Dictionary<string, int> NoteIndex = new()
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1,
        ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4, ["Fb"] = 4, ["E#"] = 5,
        ["F"] = 5, ["F#"] = 6, ["Gb"] = 6,
        ["G"] = 7, ["G#"] = 8, ["Ab"] = 8,
        ["A"] = 9, ["A#"] = 10, ["Bb"] = 10,
        ["B"] = 11, ["Cb"] = 11, ["B#"] = 0
    };

    private static readonly string[] SharpNotes =
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    /// <summary>
    /// Frequency of a note in a given octave using A4 = 440 Hz.
    /// </summary>
    public static double GetFrequency(string note, int octave)
    {
        var normalized = NormalizeNote(note, ref octave);
        var semitones = 12 * (octave - 4) + (NoteIndex[normalized] - NoteIndex["A"]);
        return 440.0 * Math.Pow(2, semitones / 12.0);
    }

    /// <summary>
    /// Shifts a note by a number of semitones, adjusting octave as needed.
    /// </summary>
    public static (string Note, int Octave) ShiftNote(string note, int octave, int semitones)
    {
        var idx = NoteIndex[note];
        var newIdx = idx + semitones;
        var newOctave = octave;

        while (newIdx < 0)
        {
            newIdx += 12;
            newOctave -= 1;
        }

        while (newIdx > 11)
        {
            newIdx -= 12;
            newOctave += 1;
        }

        return (SharpNotes[newIdx], newOctave);
    }

    private static string NormalizeNote(string note, ref int octave)
    {
        return note switch
        {
            "B#" => octave++ is var _ ? "C" : "C",
            "E#" => "F",
            "Fb" => "E",
            "Cb" => octave-- is var _ ? "B" : "B",
            _ => note
        };
    }
}
