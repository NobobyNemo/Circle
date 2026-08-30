using System.Collections.ObjectModel;

namespace Circle.Desktop.Models;

public sealed class SongDocument
{
    public string Title { get; set; } = "Без названия";
    public ObservableCollection<SongLine> Lines { get; } = new();
}
