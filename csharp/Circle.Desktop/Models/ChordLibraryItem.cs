using CommunityToolkit.Mvvm.ComponentModel;

namespace Circle.Desktop.Models;

public partial class ChordLibraryItem : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _directoryPath;

    public ChordLibraryItem(string name, string directoryPath)
    {
        _name = name;
        _directoryPath = directoryPath;
    }

    public override string ToString() => Name;
}
