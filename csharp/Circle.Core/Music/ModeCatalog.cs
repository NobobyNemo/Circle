using Circle.Core.Domain;

namespace Circle.Core.Music;

/// <summary>
/// Predefined diatonic modes.
/// </summary>
public static class ModeCatalog
{
    public static IReadOnlyList<Mode> Modes { get; } = new[]
    {
        new Mode("Ionian (Major)", [2, 2, 1, 2, 2, 2, 1]),
        new Mode("Dorian", [2, 1, 2, 2, 2, 1, 2]),
        new Mode("Phrygian", [1, 2, 2, 2, 1, 2, 2]),
        new Mode("Lydian", [2, 2, 2, 1, 2, 2, 1]),
        new Mode("Mixolydian", [2, 2, 1, 2, 2, 1, 2]),
        new Mode("Aeolian (Minor)", [2, 1, 2, 2, 1, 2, 2]),
        new Mode("Locrian", [1, 2, 2, 1, 2, 2, 2])
    };

    public static Mode Ionian => Modes[0];
    public static Mode Dorian => Modes[1];
    public static Mode Phrygian => Modes[2];
    public static Mode Lydian => Modes[3];
    public static Mode Mixolydian => Modes[4];
    public static Mode Aeolian => Modes[5];
    public static Mode Locrian => Modes[6];

    public static int IndexOf(Mode mode)
    {
        for (var i = 0; i < Modes.Count; i++)
            if (Modes[i].Equals(mode))
                return i;

        return -1;
    }
}
