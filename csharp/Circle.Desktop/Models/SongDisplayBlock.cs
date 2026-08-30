using System.Collections.ObjectModel;

namespace Circle.Desktop.Models;

public sealed class SongDisplayBlock
{
    public SongLine? HeaderLine { get; }
    public ObservableCollection<SongLine> Lines { get; } = new();

    public bool HasSection => HeaderLine is not null;
    public string SectionDisplayTitle => HeaderLine?.SectionDisplayTitle ?? "";

    public SongDisplayBlock(SongLine? headerLine)
    {
        HeaderLine = headerLine;
    }
}
