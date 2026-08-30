using System.Text;
using System.Text.RegularExpressions;

namespace Circle.Desktop.Models;

public static partial class SongTextCodec
{
    public const double CharacterWidth = 10;

    private static readonly Regex ChordProToken = ChordProTokenRegex();
    private static readonly Regex ChordToken = ChordTokenRegex();

    public static SongTextFormat DetectFormat(string text)
    {
        if (text.Contains('[', StringComparison.Ordinal) && text.Contains(']', StringComparison.Ordinal))
            return SongTextFormat.ChordPro;

        var lines = Normalize(text).Split('\n');
        return lines.Any(IsChordLine) || lines.Any(IsSectionLine)
            ? SongTextFormat.ChordsAboveLyrics
            : SongTextFormat.ChordPro;
    }

    public static SongDocument Parse(string text, SongTextFormat format, string title = "Без названия")
    {
        var document = new SongDocument { Title = string.IsNullOrWhiteSpace(title) ? "Без названия" : title };
        var lines = Normalize(text).Split('\n');

        if (format == SongTextFormat.ChordPro)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (TryParseTabReference(lines[i], out var tabReference))
                {
                    document.Lines.Add(new SongLine { TabReference = tabReference });
                }
                else if (TryParseTabBlock(lines, i, out var tabText))
                {
                    document.Lines.Add(new SongLine { TabText = tabText });
                    i += 5;
                }
                else
                {
                    document.Lines.Add(ParseChordProLine(lines[i]));
                }
            }
        }
        else
        {
            for (var i = 0; i < lines.Length; i++)
            {
                if (TryParseTabReference(lines[i], out var tabReference))
                {
                    document.Lines.Add(new SongLine { TabReference = tabReference });
                }
                else if (TryParseTabBlock(lines, i, out var tabText))
                {
                    document.Lines.Add(new SongLine { TabText = tabText });
                    i += 5;
                }
                else if (TryParseSection(lines[i], out var sectionTitle, out var repeatCount, out var sectionDetails))
                {
                    document.Lines.Add(new SongLine { SectionTitle = sectionTitle, RepeatCount = repeatCount, SectionDetails = sectionDetails });
                }
                else if (IsChordLine(lines[i]) && i + 1 < lines.Length && !IsChordLine(lines[i + 1]))
                {
                    document.Lines.Add(ParseClassicPair(lines[i], lines[++i]));
                }
                else if (IsChordLine(lines[i]))
                {
                    document.Lines.Add(ParseChordLine(lines[i]));
                }
                else
                {
                    document.Lines.Add(new SongLine { Lyrics = lines[i] });
                }
            }
        }

        if (document.Lines.Count == 0)
            document.Lines.Add(new SongLine());

        return document;
    }

    public static string Serialize(SongDocument document, SongTextFormat format)
    {
        var lines = document.Lines.Select(line => format == SongTextFormat.ChordPro
            ? SerializeChordProLine(line)
            : SerializeClassicLine(line));
        return string.Join(Environment.NewLine, lines);
    }

    private static SongLine ParseChordProLine(string line)
    {
        if (TryParseSection(line, out var sectionTitle, out var repeatCount, out var sectionDetails))
            return new SongLine { SectionTitle = sectionTitle, RepeatCount = repeatCount, SectionDetails = sectionDetails };

        var songLine = new SongLine();
        var lyrics = new StringBuilder();
        var removedCharacters = 0;
        var lastEnd = 0;

        foreach (Match match in ChordProToken.Matches(line))
        {
            lyrics.Append(line[lastEnd..match.Index]);
            songLine.Chords.Add(new SongChord
            {
                Name = match.Groups["chord"].Value,
                Position = match.Index - removedCharacters
            });
            removedCharacters += match.Length;
            lastEnd = match.Index + match.Length;
        }

        lyrics.Append(line[lastEnd..]);
        songLine.Lyrics = lyrics.ToString();
        return songLine;
    }

    private static SongLine ParseClassicPair(string chordLine, string lyrics)
    {
        var songLine = ParseChordLine(chordLine);
        songLine.Lyrics = lyrics;
        return songLine;
    }

    private static SongLine ParseChordLine(string chordLine)
    {
        var songLine = new SongLine();
        foreach (Match match in ChordToken.Matches(chordLine))
        {
            songLine.Chords.Add(new SongChord
            {
                Name = match.Value,
                Position = match.Index
            });
        }
        return songLine;
    }

    public static string SerializeSongTextWithTabMarkers(SongDocument document, SongTextFormat format)
    {
        var tabIndex = 0;
        var lines = document.Lines.Select(line =>
        {
            if (line.TabReference is null && !line.IsTabBlock)
                return format == SongTextFormat.ChordPro
                    ? SerializeChordProLine(line)
                    : SerializeClassicLine(line);

            var index = line.TabReference ?? ++tabIndex;
            line.TabReference = index;
            tabIndex = Math.Max(tabIndex, index);
            return $"{{TAB:{index}}}";
        });
        return string.Join(Environment.NewLine, lines);
    }

    public static string SerializeTabFile(SongDocument document)
    {
        var tabIndex = 0;
        var blocks = document.Lines
            .Where(line => line.TabReference is not null || line.IsTabBlock)
            .Select(line =>
            {
                var index = line.TabReference ?? ++tabIndex;
                line.TabReference = index;
                tabIndex = Math.Max(tabIndex, index);
                return $"[TAB:{index}]" + Environment.NewLine + line.TabText;
            });
        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    public static IReadOnlyDictionary<int, string> ParseTabFile(string text)
    {
        var lines = Normalize(text).Split('\n');
        var result = new Dictionary<int, string>();
        int? currentIndex = null;
        var currentLines = new List<string>();

        void Flush()
        {
            if (currentIndex is not null)
                result[currentIndex.Value] = string.Join(Environment.NewLine, currentLines).Trim('\r', '\n');
            currentLines.Clear();
        }

        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^\s*\[TAB:(?<index>\d+)\]\s*$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                Flush();
                currentIndex = int.Parse(match.Groups["index"].Value);
            }
            else if (currentIndex is not null)
            {
                currentLines.Add(line);
            }
        }
        Flush();
        return result;
    }

    private static bool TryParseTabReference(string line, out int index)
    {
        var match = Regex.Match(line, @"^\s*\{TAB:(?<index>\d+)\}\s*$", RegexOptions.IgnoreCase);
        return int.TryParse(match.Groups["index"].Value, out index);
    }

    private static bool TryParseTabBlock(string[] lines, int startIndex, out string tabText)
    {
        tabText = "";
        if (startIndex + 5 >= lines.Length)
            return false;

        var expectedStrings = new[] { "e", "B", "G", "D", "A", "E" };
        for (var i = 0; i < expectedStrings.Length; i++)
        {
            var line = lines[startIndex + i];
            var trimmed = line.TrimStart();
            if (!TabLineRegex().IsMatch(line) || trimmed.Length == 0 || trimmed[0].ToString() != expectedStrings[i])
                return false;
        }

        tabText = string.Join(Environment.NewLine, lines.Skip(startIndex).Take(6));
        return true;
    }

    private static string SerializeChordProLine(SongLine line)
    {
        if (line.IsTabBlock)
            return line.TabText;
        if (line.IsSectionHeader)
            return $"[{line.SectionTitle}{(string.IsNullOrWhiteSpace(line.SectionDetails) ? "" : $": ({line.SectionDetails})")}]" + (line.RepeatCount > 1 ? $" x{line.RepeatCount}" : "");

        var result = new StringBuilder(line.Lyrics);
        var offset = 0;
        foreach (var chord in line.Chords.OrderBy(chord => chord.Position))
        {
            var position = Math.Clamp((int)Math.Round(chord.Position) + offset, 0, result.Length);
            result.Insert(position, $"[{chord.Name}]");
            offset += chord.Name.Length + 2;
        }
        return result.ToString();
    }

    private static string SerializeClassicLine(SongLine line)
    {
        if (line.IsTabBlock)
            return line.TabText;
        if (line.IsSectionHeader)
            return $"|{line.SectionTitle}{(string.IsNullOrWhiteSpace(line.SectionDetails) ? "" : $": ({line.SectionDetails})")}|" + (line.RepeatCount > 1 ? $" x{line.RepeatCount}" : "");

        var chordLine = new StringBuilder();
        foreach (var chord in line.Chords.OrderBy(chord => chord.Position))
        {
            var position = Math.Max(0, (int)Math.Round(chord.Position));
            PadTo(chordLine, position);
            chordLine.Append(chord.Name);
        }

        if (chordLine.Length == 0)
            return line.Lyrics;
        if (line.Lyrics.Length == 0)
            return chordLine.ToString();
        return chordLine + Environment.NewLine + line.Lyrics;
    }

    private static bool TryParseSection(string line, out string title, out int repeatCount, out string details)
    {
        var candidate = line.Trim();
        repeatCount = 1;
        details = "";

        var repeatMatch = Regex.Match(candidate, @"\s+x(?<count>\d+)\s*$", RegexOptions.IgnoreCase);
        if (repeatMatch.Success && int.TryParse(repeatMatch.Groups["count"].Value, out var parsedCount))
        {
            repeatCount = Math.Max(1, parsedCount);
            candidate = candidate[..repeatMatch.Index].TrimEnd();
        }

        if (candidate.Length >= 2 && candidate[0] == '[' && candidate[^1] == ']')
            candidate = candidate[1..^1].Trim();
        else if (candidate.Length >= 2 && candidate[0] == '|' && candidate[^1] == '|')
            candidate = candidate[1..^1].Trim();

        var colonIndex = candidate.IndexOf(':');
        if (colonIndex >= 0)
        {
            details = candidate[(colonIndex + 1)..].Trim();
            candidate = candidate[..colonIndex].Trim();
            if (details.Length >= 2 && details[0] == '(' && details[^1] == ')')
                details = details[1..^1].Trim();
        }

        var normalized = candidate.ToLowerInvariant();
        var isSection = normalized is "куплет" or "припев" or "бридж" or "вступление" or "аутро" or "verse" or "chorus" or "bridge" or "intro" or "outro"
            || normalized.StartsWith("куплет ", StringComparison.Ordinal)
            || normalized.StartsWith("припев ", StringComparison.Ordinal)
            || normalized.StartsWith("verse ", StringComparison.Ordinal)
            || normalized.StartsWith("chorus ", StringComparison.Ordinal)
            || normalized.StartsWith("bridge ", StringComparison.Ordinal)
            || normalized.StartsWith("вступление ", StringComparison.Ordinal)
            || normalized.StartsWith("аутро ", StringComparison.Ordinal)
            || normalized.StartsWith("intro ", StringComparison.Ordinal)
            || normalized.StartsWith("outro ", StringComparison.Ordinal);

        title = candidate;
        return isSection;
    }

    private static bool IsSectionLine(string line) => TryParseSection(line, out _, out _, out _);

    private static bool IsChordLine(string line)
    {
        var tokens = ChordToken.Matches(line);
        if (tokens.Count == 0)
            return false;

        var nonWhitespace = line.Count(c => !char.IsWhiteSpace(c));
        return tokens.Cast<Match>().Sum(match => match.Length) == nonWhitespace;
    }

    private static void PadTo(StringBuilder builder, int length)
    {
        while (builder.Length < length)
            builder.Append(' ');
    }

    public static string NormalizeInsertedText(string text)
    {
        var normalized = (text ?? "")
            .Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal)
            .Replace("\u2028", "\n", StringComparison.Ordinal)
            .Replace("\u2029", "\n", StringComparison.Ordinal);

        return Regex.Replace(normalized, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string Normalize(string text) => NormalizeInsertedText(text);

    [GeneratedRegex(@"\[(?<chord>[^\]]+)\]")]
    private static partial Regex ChordProTokenRegex();

    [GeneratedRegex(@"^\s*[eBGDAE][|].*$")]
    private static partial Regex TabLineRegex();

    [GeneratedRegex(@"(?i)(?<!\S)[A-H](?:#|b)?(?:maj|min|m|dim|aug|sus|add)?\d*(?:/[A-H](?:#|b)?)?(?!\S)")]
    private static partial Regex ChordTokenRegex();
}
