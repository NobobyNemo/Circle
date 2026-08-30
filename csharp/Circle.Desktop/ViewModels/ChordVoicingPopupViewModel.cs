using System.Collections.ObjectModel;
using Circle.Desktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Circle.Desktop.ViewModels;

public partial class ChordVoicingPopupViewModel : ViewModelBase
{
    public string ChordName { get; }
    public ObservableCollection<ChordVoicing> Voicings { get; } = new();

    [ObservableProperty]
    private ChordVoicing? _selectedVoicing;

    public ChordVoicingPopupViewModel(string chordName)
    {
        ChordName = chordName;
        foreach (var voicing in ChordVoicingCatalog.Get(chordName))
            Voicings.Add(voicing);
        SelectedVoicing = Voicings.FirstOrDefault();
    }
}
