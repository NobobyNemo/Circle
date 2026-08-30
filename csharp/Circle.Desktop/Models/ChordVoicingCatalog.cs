namespace Circle.Desktop.Models;

public static class ChordVoicingCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Notations =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = [
                "6/E:-1 5/A:0 4/D:2 3/G:2 2/B:2 1/E:0",
                "6/E:5 5/A:7 4/D:7 3/G:6 2/B:5 1/E:5"],
            ["Am"] = [
                "6/E:-1 5/A:0 4/D:2 3/G:2 2/B:1 1/E:0",
                "6/E:5 5/A:7 4/D:7 3/G:5 2/B:5 1/E:5"],
            ["B"] = ["6/E:2 5/A:2 4/D:4 3/G:4 2/B:4 1/E:2"],
            ["Bm"] = ["6/E:2 5/A:2 4/D:4 3/G:4 2/B:3 1/E:2"],
            ["C"] = [
                "6/E:-1 5/A:3 4/D:2 3/G:0 2/B:1 1/E:0",
                "6/E:8 5/A:10 4/D:10 3/G:9 2/B:8 1/E:8"],
            ["Cm"] = ["6/E:-1 5/A:3 4/D:5 3/G:5 2/B:4 1/E:3"],
            ["D"] = [
                "6/E:-1 5/A:-1 4/D:0 3/G:2 2/B:3 1/E:2",
                "6/E:5 5/A:5 4/D:7 3/G:7 2/B:7 1/E:5"],
            ["Dm"] = ["6/E:-1 5/A:-1 4/D:0 3/G:2 2/B:3 1/E:1"],
            ["E"] = [
                "6/E:0 5/A:2 4/D:2 3/G:1 2/B:0 1/E:0",
                "6/E:12 5/A:14 4/D:14 3/G:13 2/B:12 1/E:12"],
            ["Em"] = [
                "6/E:0 5/A:2 4/D:2 3/G:0 2/B:0 1/E:0",
                "6/E:12 5/A:14 4/D:14 3/G:12 2/B:12 1/E:12"],
            ["F"] = ["6/E:1 5/A:3 4/D:3 3/G:2 2/B:1 1/E:1"],
            ["Fm"] = ["6/E:1 5/A:3 4/D:3 3/G:1 2/B:1 1/E:1"],
            ["G"] = [
                "6/E:3 5/A:2 4/D:0 3/G:0 2/B:0 1/E:3",
                "6/E:3 5/A:5 4/D:5 3/G:4 2/B:3 1/E:3"],
            ["Gm"] = ["6/E:3 5/A:5 4/D:5 3/G:3 2/B:3 1/E:3"]
        };

    public static IReadOnlyList<ChordVoicing> Get(string chordName)
    {
        if (!Notations.TryGetValue(chordName, out var notations))
            return [];

        return notations
            .Select((notation, index) => new ChordVoicing(notation, $"Вариант {index + 1}"))
            .ToArray();
    }
}
