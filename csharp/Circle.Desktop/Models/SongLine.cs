using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Circle.Desktop.Models;

public partial class SongLine : ObservableObject
{
    public ObservableCollection<SongChord> Chords { get; } = new();

    [ObservableProperty]
    private string _sectionTitle = "";

    [ObservableProperty]
    private int _repeatCount = 1;

    [ObservableProperty]
    private string _sectionDetails = "";

    [ObservableProperty]
    private string _lyrics = "";

    [ObservableProperty]
    private string _tabText = "";

    [ObservableProperty]
    private int? _tabReference;

    [ObservableProperty]
    private bool _isTabTextMode;

    [ObservableProperty]
    private string _selectedChord = "Am";

    [ObservableProperty]
    private int _caretIndex;

    [ObservableProperty]
    private double _fontSize = 13;

    public bool IsSectionHeader => !string.IsNullOrWhiteSpace(SectionTitle);
    public bool IsTabBlock => !string.IsNullOrWhiteSpace(TabText) || TabReference is not null;
    public bool IsRegularLine => !IsSectionHeader && !IsTabBlock;
    public string SectionDisplayTitle =>
        $"{SectionTitle}{(string.IsNullOrWhiteSpace(SectionDetails) ? "" : $": ({SectionDetails})")}{(RepeatCount > 1 ? $" ×{RepeatCount}" : "")}";
    public double ChordLaneHeight => SongChord.DefaultBlockHeight;
    public double LineHeight => FontSize + 8;

    public double DisplayWidth
    {
        get
        {
            var characterWidth = FontSize * 0.6;
            var lyricsWidth = IsTabBlock
                ? TabText.Split('\n').DefaultIfEmpty("").Max(line => line.Length) * characterWidth
                : Lyrics.Length * characterWidth;
            var chordsWidth = Chords.Count == 0
                ? 0
                : Chords.Max(chord => chord.Position * characterWidth + chord.BlockWidth);
            return Math.Max(40, Math.Max(lyricsWidth, chordsWidth) + 8);
        }
    }

    public SongLine()
    {
        Chords.CollectionChanged += OnChordsChanged;
    }

    partial void OnSectionTitleChanged(string value)
    {
        OnPropertyChanged(nameof(IsSectionHeader));
        OnPropertyChanged(nameof(SectionDisplayTitle));
    }

    partial void OnRepeatCountChanged(int value) => OnPropertyChanged(nameof(SectionDisplayTitle));

    partial void OnSectionDetailsChanged(string value) => OnPropertyChanged(nameof(SectionDisplayTitle));

    partial void OnLyricsChanged(string value) => OnPropertyChanged(nameof(DisplayWidth));

    partial void OnTabReferenceChanged(int? value)
    {
        OnPropertyChanged(nameof(IsTabBlock));
        OnPropertyChanged(nameof(IsRegularLine));
        OnPropertyChanged(nameof(DisplayWidth));
    }

    partial void OnTabTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsTabBlock));
        OnPropertyChanged(nameof(IsRegularLine));
        OnPropertyChanged(nameof(DisplayWidth));
    }

    partial void OnFontSizeChanged(double value)
    {
        foreach (var chord in Chords)
            chord.FontSize = value;
        OnPropertyChanged(nameof(ChordLaneHeight));
        OnPropertyChanged(nameof(LineHeight));
        OnPropertyChanged(nameof(DisplayWidth));
    }

    private void OnChordsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SongChord chord in e.OldItems)
                chord.PropertyChanged -= OnChordChanged;
        if (e.NewItems is not null)
            foreach (SongChord chord in e.NewItems)
            {
                chord.FontSize = FontSize;
                chord.PropertyChanged += OnChordChanged;
            }
        OnPropertyChanged(nameof(DisplayWidth));
    }

    private void OnChordChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(DisplayWidth));
}
