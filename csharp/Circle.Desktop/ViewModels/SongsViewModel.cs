using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Circle.Desktop.Models;
using Circle.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class SongsViewModel : ViewModelBase
{
    private readonly ChordLibraryService _libraryService = new();

    public ObservableCollection<SongLine> Lines { get; } = new();
    public ObservableCollection<SongLine> TabLines { get; } = new();
    public ObservableCollection<SongDisplayBlock> DisplayBlocks { get; } = new();
    public ObservableCollection<ChordLibraryItem> Artists { get; } = new();
    public ObservableCollection<ChordLibraryItem> Songs { get; } = new();
    public ObservableCollection<ChordLibraryItem> VisibleArtists { get; } = new();
    public ObservableCollection<ChordLibraryItem> VisibleSongs { get; } = new();
    public ObservableCollection<string> AvailableLetters { get; } = new();

    [ObservableProperty]
    private string _libraryRootPath = "";

    [ObservableProperty]
    private ChordLibraryItem? _selectedArtist;

    [ObservableProperty]
    private ChordLibraryItem? _selectedSong;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private string _selectedLetter = "Все";

    [ObservableProperty]
    private bool _isArtistList = true;

    [ObservableProperty]
    private string _title = "Без названия";

    [ObservableProperty]
    private string _sourceText = "";

    [ObservableProperty]
    private string _chordInput = "Am";

    [ObservableProperty]
    private SongTextFormat _exportFormat = SongTextFormat.ChordPro;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isTextEditorMode;

    [ObservableProperty]
    private double _displayFontSize = 13;

    [ObservableProperty]
    private bool _isAutoScrollEnabled;

    [ObservableProperty]
    private double _autoScrollSpeed = 1;

    private bool _synchronizing;
    private bool _refreshingLibrary;

    public IReadOnlyList<SongTextFormat> Formats { get; } = Enum.GetValues<SongTextFormat>();
    public IReadOnlyList<string> Alphabet { get; } = new[] { "Все" }
        .Concat(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()))
        .Concat(Enumerable.Range('А', 32).Select(c => ((char)c).ToString()))
        .ToArray();
    public IReadOnlyList<string> ChordOptions { get; } =
    [
        "A", "Am", "A7", "Am7", "Adim",
        "B", "Bm", "B7", "Bm7", "Bdim",
        "C", "Cm", "C7", "Cm7", "Cdim",
        "D", "Dm", "D7", "Dm7", "Ddim",
        "E", "Em", "E7", "Em7", "Edim",
        "F", "Fm", "F7", "Fm7", "Fdim",
        "G", "Gm", "G7", "Gm7", "Gdim"
    ];
    public string EditModeButtonText => IsEditMode ? "Готово" : "Редактировать";
    public bool IsReadOnly => !IsEditMode;
    public bool IsGraphicalMode => !IsTextEditorMode;
    public string AutoScrollButtonText => IsAutoScrollEnabled ? "Пауза" : "Автопрокрутка";
    public double EditorWidth => Math.Max(600, (Lines.Count == 0 ? 0 : Lines.Max(line => line.DisplayWidth)) + (IsEditMode ? 100 : 0));
    public double EditorContentWidth => EditorWidth + 20;
    public double EditorPanelWidth => EditorWidth + 68;

    public ICommand ImportChordProCommand { get; }
    public ICommand ImportClassicCommand { get; }
    public ICommand NewSongCommand { get; }
    public ICommand AddLineCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand InsertChordCommand { get; }
    public ICommand RemoveChordCommand { get; }
    public ICommand ToggleEditModeCommand { get; }
    public ICommand ShowGraphicalModeCommand { get; }
    public ICommand ShowTextEditorCommand { get; }
    public ICommand ShowDisplayModeCommand { get; }
    public ICommand ToggleAutoScrollCommand { get; }
    public ICommand BackToArtistsCommand { get; }
    public ICommand MoveLineCommand { get; }

    public ContextMenu LineContextMenu { get; }

    public SongsViewModel()
    {
        Lines.CollectionChanged += OnLinesCollectionChanged;
        ImportChordProCommand = new RelayCommand(() => Import(SongTextFormat.ChordPro));
        ImportClassicCommand = new RelayCommand(() => Import(SongTextFormat.ChordsAboveLyrics));
        NewSongCommand = new RelayCommand(NewSong);
        AddLineCommand = new RelayCommand(AddLine);
        RemoveLineCommand = new RelayCommand<SongLine>(RemoveLine);
        InsertChordCommand = new RelayCommand<SongLine>(InsertChord);
        RemoveChordCommand = new RelayCommand<SongChord>(RemoveChord);
        ToggleEditModeCommand = new RelayCommand(ToggleEditMode);
        ShowGraphicalModeCommand = new RelayCommand(ShowGraphicalMode);
        ShowTextEditorCommand = new RelayCommand(ShowTextEditor);
        ShowDisplayModeCommand = new RelayCommand(() => IsEditMode = false);
        ToggleAutoScrollCommand = new RelayCommand(() => IsAutoScrollEnabled = !IsAutoScrollEnabled);
        BackToArtistsCommand = new RelayCommand(BackToArtists);
        MoveLineCommand = new RelayCommand<(SongLine, SongLine)>(pair => MoveLine(pair.Item1, pair.Item2));

        LineContextMenu = new ContextMenu();
        var insertChordItem = new MenuItem { Header = "Добавить аккорд", Command = InsertChordCommand };
        insertChordItem.Bind(MenuItem.CommandParameterProperty, new Avalonia.Data.Binding());
        var removeLineItem = new MenuItem { Header = "Удалить строку", Command = RemoveLineCommand };
        removeLineItem.Bind(MenuItem.CommandParameterProperty, new Avalonia.Data.Binding());
        LineContextMenu.Items.Add(insertChordItem);
        LineContextMenu.Items.Add(removeLineItem);

        AddLine();
    }

    partial void OnSelectedArtistChanged(ChordLibraryItem? value)
    {
        Songs.Clear();
        VisibleSongs.Clear();
        SelectedSong = null;
        if (value is null)
            return;

        ReloadSongs(value);
        IsArtistList = false;
        SearchQuery = "";
        SelectedLetter = "Все";
        RefreshVisibleItems();
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (!_refreshingLibrary)
            RefreshVisibleItems();
    }

    partial void OnSelectedLetterChanged(string value)
    {
        if (!_refreshingLibrary)
            RefreshVisibleItems();
    }

    private void RefreshVisibleItems()
    {
        if (_refreshingLibrary)
            return;

        _refreshingLibrary = true;
        try
        {
            var source = IsArtistList ? Artists : Songs;
            var target = IsArtistList ? VisibleArtists : VisibleSongs;
            var selectedLetter = string.IsNullOrWhiteSpace(SelectedLetter) ? "Все" : SelectedLetter;

            AvailableLetters.Clear();
            AvailableLetters.Add("Все");
            foreach (var letter in source
                         .Select(item => item.Name.FirstOrDefault(char.IsLetter).ToString().ToUpperInvariant())
                         .Where(letter => letter.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(letter => letter, StringComparer.CurrentCultureIgnoreCase))
                AvailableLetters.Add(letter);

            if (!AvailableLetters.Contains(selectedLetter, StringComparer.OrdinalIgnoreCase))
                selectedLetter = "Все";
            if (SelectedLetter != selectedLetter)
                SelectedLetter = selectedLetter;

            target.Clear();
            var query = SearchQuery.Trim();
            foreach (var item in source.Where(item =>
                         (selectedLetter == "Все" || item.Name.StartsWith(selectedLetter, StringComparison.CurrentCultureIgnoreCase))
                         && (query.Length == 0 || item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
                target.Add(item);
        }
        finally
        {
            _refreshingLibrary = false;
        }
    }

    private void BackToArtists()
    {
        SelectedArtist = null;
        SelectedSong = null;
        Songs.Clear();
        VisibleSongs.Clear();
        SearchQuery = "";
        SelectedLetter = "Все";
        IsArtistList = true;
        RefreshVisibleItems();
    }

    partial void OnSelectedSongChanged(ChordLibraryItem? value)
    {
        if (value is null)
            return;

        var file = _libraryService.FindSongFile(value);
        if (file is null)
            return;

        try
        {
            var mainText = File.ReadAllText(file);
            var tabFile = _libraryService.GetTabFilePath(value);
            var tabText = File.Exists(tabFile) ? File.ReadAllText(tabFile) : "";
            ImportLibrarySong(mainText, tabText, value.Name);
            StatusMessage = $"Открыта песня: {value.Name}";
        }
        catch (IOException exception)
        {
            StatusMessage = $"Не удалось открыть песню: {exception.Message}";
        }
    }

    partial void OnIsAutoScrollEnabledChanged(bool value) => OnPropertyChanged(nameof(AutoScrollButtonText));

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EditModeButtonText));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(EditorWidth));
        OnPropertyChanged(nameof(EditorContentWidth));
        OnPropertyChanged(nameof(EditorPanelWidth));
        if (value)
            IsAutoScrollEnabled = false;
        if (!value)
            IsTextEditorMode = false;
    }

    partial void OnIsTextEditorModeChanged(bool value) => OnPropertyChanged(nameof(IsGraphicalMode));

    partial void OnDisplayFontSizeChanged(double value)
    {
        foreach (var line in Lines)
            line.FontSize = value;
        OnPropertyChanged(nameof(EditorWidth));
        OnPropertyChanged(nameof(EditorContentWidth));
        OnPropertyChanged(nameof(EditorPanelWidth));
    }

    private void ToggleEditMode() => IsEditMode = !IsEditMode;

    private void ShowGraphicalMode()
    {
        IsEditMode = true;
        IsTextEditorMode = false;
    }

    private void ShowTextEditor()
    {
        IsEditMode = true;
        IsTextEditorMode = true;
    }

    partial void OnSourceTextChanged(string value)
    {
        if (_synchronizing)
            return;

        var normalized = SongTextCodec.NormalizeInsertedText(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            _synchronizing = true;
            try
            {
                SourceText = normalized;
            }
            finally
            {
                _synchronizing = false;
            }
        }

        var existingTabs = Lines
            .Where(line => line.TabReference is not null && !string.IsNullOrWhiteSpace(line.TabText))
            .GroupBy(line => line.TabReference!.Value)
            .ToDictionary(group => group.Key, group => group.First().TabText);
        var format = SongTextCodec.DetectFormat(normalized);
        var document = SongTextCodec.Parse(normalized, format);
        foreach (var line in document.Lines)
        {
            if (line.TabReference is int reference && existingTabs.TryGetValue(reference, out var tabText))
                line.TabText = tabText;
        }
        ReplaceLines(document.Lines);
    }

    public async Task InitializeLibraryAsync(IStorageProvider storageProvider)
    {
        var savedPath = _libraryService.SavedRootPath;
        if (Directory.Exists(savedPath))
        {
            SetLibraryRoot(savedPath);
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с аккордами",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
            SetLibraryRoot(folder.Path.LocalPath);
    }

    public void SetLibraryRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusMessage = "Папка с аккордами не найдена.";
            return;
        }

        LibraryRootPath = path;
        _libraryService.SaveRootPath(path);
        SelectedArtist = null;
        SelectedSong = null;
        Artists.Clear();
        foreach (var artist in _libraryService.GetArtists(path))
            Artists.Add(artist);
        IsArtistList = true;
        SearchQuery = "";
        SelectedLetter = "Все";
        RefreshVisibleItems();
        StatusMessage = $"Исполнителей: {Artists.Count}";
    }

    public void ImportLibrarySong(string mainText, string tabText, string title)
    {
        var document = SongTextCodec.Parse(mainText, SongTextFormat.ChordPro, title);
        var tabs = SongTextCodec.ParseTabFile(tabText);
        foreach (var line in document.Lines)
        {
            if (line.TabReference is int reference && tabs.TryGetValue(reference, out var value))
                line.TabText = value;
        }

        Title = document.Title;
        ReplaceLines(document.Lines);
        SetSourceFromLines();
    }

    public void ImportText(string text, SongTextFormat format, string? importedTitle = null)
    {
        var normalized = SongTextCodec.NormalizeInsertedText(text);
        var document = SongTextCodec.Parse(normalized, format, importedTitle ?? Title);
        Title = document.Title;
        ReplaceLines(document.Lines);
        SetSourceFromLines();
        StatusMessage = $"Импортировано строк: {Lines.Count}";
    }

    public string ExportText() => ExportText(ExportFormat);

    public string ExportText(SongTextFormat format) => SongTextCodec.Serialize(
        new SongDocument { Title = Title }.WithLines(Lines), format);

    private void Import(SongTextFormat format)
    {
        if (string.IsNullOrWhiteSpace(SourceText))
        {
            StatusMessage = "Вставьте текст песни перед импортом.";
            return;
        }

        ImportText(SourceText, format);
    }

    private void NewSong()
    {
        if (SelectedArtist is null)
        {
            StatusMessage = "Сначала выберите исполнителя.";
            return;
        }

        var songName = "Новая песня";
        var suffix = 1;
        while (Directory.Exists(Path.Combine(SelectedArtist.DirectoryPath, songName)))
            songName = $"Новая песня {++suffix}";

        var songDirectory = Path.Combine(SelectedArtist.DirectoryPath, songName);
        Directory.CreateDirectory(songDirectory);
        File.WriteAllText(Path.Combine(songDirectory, "song.chordpro"), "");
        ReloadSongs(SelectedArtist);
        IsArtistList = false;
        SelectedSong = Songs.FirstOrDefault(song => song.Name == songName);
        StatusMessage = $"Создана песня: {songName}";
    }

    public bool SaveCurrentSong()
    {
        if (SelectedSong is null)
            return false;

        try
        {
            if (!RenameSelectedSong())
                return true;

            var file = _libraryService.FindSongFile(SelectedSong);
            if (file is null)
                return false;

            var document = new SongDocument { Title = Title }.WithLines(Lines);
            File.WriteAllText(file, SongTextCodec.SerializeSongTextWithTabMarkers(document, SongTextFormat.ChordPro));
            File.WriteAllText(_libraryService.GetTabFilePath(SelectedSong), SongTextCodec.SerializeTabFile(document));
            StatusMessage = "Песня сохранена.";
            return true;
        }
        catch (IOException exception)
        {
            StatusMessage = $"Не удалось сохранить песню: {exception.Message}";
            return true;
        }
    }

    private bool RenameSelectedSong()
    {
        if (SelectedSong is null)
            return false;

        var safeName = new string((Title ?? "")
            .Trim()
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
            .ToArray());
        if (string.IsNullOrWhiteSpace(safeName))
        {
            StatusMessage = "Введите название песни.";
            return false;
        }

        var currentDirectory = SelectedSong.DirectoryPath;
        var parentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        if (parentDirectory is null)
            return false;

        var targetDirectory = Path.Combine(parentDirectory, safeName);
        if (!string.Equals(currentDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(targetDirectory))
            {
                StatusMessage = $"Песня с названием «{safeName}» уже существует.";
                return false;
            }
            Directory.Move(currentDirectory, targetDirectory);
        }

        SelectedSong.Name = safeName;
        SelectedSong.DirectoryPath = targetDirectory;
        Title = safeName;
        return true;
    }

    private void ReloadSongs(ChordLibraryItem artist)
    {
        Songs.Clear();
        foreach (var song in _libraryService.GetSongs(artist.DirectoryPath))
            Songs.Add(song);
        RefreshVisibleItems();
    }

    public void AddTab(string tabText)
    {
        var normalized = SongTextCodec.NormalizeInsertedText(tabText).TrimEnd();
        if (normalized.Length == 0)
            return;

        var nextIndex = (Lines.Where(line => line.IsTabBlock && line.TabReference is not null)
            .Select(line => line.TabReference!.Value)
            .DefaultIfEmpty(0)
            .Max()) + 1;
        Lines.Add(new SongLine { TabText = normalized, TabReference = nextIndex });
        StatusMessage = "Табулатура добавлена в конец песни.";
    }

    private void AddLine() => Lines.Add(new SongLine());

    public void MoveLine(SongLine source, SongLine target)
    {
        if (ReferenceEquals(source, target))
            return;
        var sourceIndex = Lines.IndexOf(source);
        var targetIndex = Lines.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0)
            return;
        Lines.Move(sourceIndex, targetIndex);
    }

    private void RemoveLine(SongLine? line)
    {
        if (line is null)
            return;
        if (Lines.Count == 1)
        {
            line.Chords.Clear();
            line.Lyrics = "";
            return;
        }
        Lines.Remove(line);
    }

    private void InsertChord(SongLine? line)
    {
        if (line is null)
            return;

        const string chord = "Am";
        var position = Math.Clamp(line.CaretIndex, 0, line.Lyrics.Length);
        line.Chords.Add(new SongChord { Name = chord, Position = position, FontSize = DisplayFontSize });
        StatusMessage = $"Аккорд {chord} добавлен.";
    }

    public void ChangeChord(SongChord chord, string name)
    {
        chord.Name = name;
    }

    public void RemoveChord(SongChord? chord)
    {
        if (chord is null)
            return;

        var line = Lines.FirstOrDefault(candidate => candidate.Chords.Contains(chord));
        line?.Chords.Remove(chord);
    }

    private void ReplaceLines(IEnumerable<SongLine> lines)
    {
        _synchronizing = true;
        try
        {
            Lines.Clear();
            foreach (var line in lines)
                Lines.Add(line);
            if (Lines.Count == 0)
                Lines.Add(new SongLine());
        }
        finally
        {
            _synchronizing = false;
        }

        OnPropertyChanged(nameof(EditorWidth));
        OnPropertyChanged(nameof(EditorContentWidth));
        OnPropertyChanged(nameof(EditorPanelWidth));
    }

    private void RebuildDisplayBlocks()
    {
        TabLines.Clear();
        foreach (var tabLine in Lines.Where(line => line.IsTabBlock))
            TabLines.Add(tabLine);

        DisplayBlocks.Clear();
        SongDisplayBlock? currentBlock = null;

        foreach (var line in Lines)
        {
            if (line.IsSectionHeader)
            {
                currentBlock = new SongDisplayBlock(line);
                DisplayBlocks.Add(currentBlock);
            }
            else
            {
                currentBlock ??= new SongDisplayBlock(null);
                if (!DisplayBlocks.Contains(currentBlock))
                    DisplayBlocks.Add(currentBlock);
                currentBlock.Lines.Add(line);
            }
        }

        if (DisplayBlocks.Count == 0)
            DisplayBlocks.Add(new SongDisplayBlock(null));
    }

    private void OnLinesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildDisplayBlocks();
        OnPropertyChanged(nameof(EditorWidth));
        OnPropertyChanged(nameof(EditorContentWidth));
        OnPropertyChanged(nameof(EditorPanelWidth));
        if (e.OldItems is not null)
            foreach (SongLine line in e.OldItems)
                DetachLine(line);
        if (e.NewItems is not null)
            foreach (SongLine line in e.NewItems)
                AttachLine(line);

        if (!_synchronizing)
            SetSourceFromLines();
    }

    private void AttachLine(SongLine line)
    {
        line.FontSize = DisplayFontSize;
        line.PropertyChanged += OnLinePropertyChanged;
        line.Chords.CollectionChanged += OnChordsCollectionChanged;
        foreach (var chord in line.Chords)
            chord.PropertyChanged += OnChordPropertyChanged;
    }

    private void DetachLine(SongLine line)
    {
        line.PropertyChanged -= OnLinePropertyChanged;
        line.Chords.CollectionChanged -= OnChordsCollectionChanged;
        foreach (var chord in line.Chords)
            chord.PropertyChanged -= OnChordPropertyChanged;
    }

    private void OnLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SongLine.DisplayWidth))
            OnPropertyChanged(nameof(EditorWidth));
        OnPropertyChanged(nameof(EditorContentWidth));
        OnPropertyChanged(nameof(EditorPanelWidth));
        if (e.PropertyName is nameof(SongLine.Lyrics)
            or nameof(SongLine.TabText)
            or nameof(SongLine.SectionTitle)
            or nameof(SongLine.SectionDetails)
            or nameof(SongLine.RepeatCount))
            SetSourceFromLines();
    }

    private void OnChordsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SongChord chord in e.OldItems)
                chord.PropertyChanged -= OnChordPropertyChanged;
        if (e.NewItems is not null)
            foreach (SongChord chord in e.NewItems)
                chord.PropertyChanged += OnChordPropertyChanged;
        SetSourceFromLines();
    }

    private void OnChordPropertyChanged(object? sender, PropertyChangedEventArgs e) => SetSourceFromLines();

    private void SetSourceFromLines()
    {
        if (_synchronizing)
            return;

        _synchronizing = true;
        try
        {
            SourceText = SongTextCodec.SerializeSongTextWithTabMarkers(
                new SongDocument { Title = Title }.WithLines(Lines),
                SongTextFormat.ChordPro);
        }
        finally
        {
            _synchronizing = false;
        }
    }
}

internal static class SongDocumentExtensions
{
    public static SongDocument WithLines(this SongDocument document, IEnumerable<SongLine> lines)
    {
        foreach (var line in lines)
            document.Lines.Add(line);
        return document;
    }
}
