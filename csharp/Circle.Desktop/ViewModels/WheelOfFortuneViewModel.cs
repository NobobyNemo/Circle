using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Circle.Desktop.Helpers;
using Circle.Desktop.Models;
using Circle.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public enum WheelGameMode
{
    Classic,      // Обычный режим — просто крутим
    Elimination,  // На выбывание — выпавший удаляется
    TeamPick      // Набери в команду — выпавший попадает в команду
}

public enum FortuneGameType
{
    Wheel,   // Колесо фортуны — вращение сегментов и стрелки
    Plinko,  // Плинко — шарик падает через штырьки в лунку
    Strip    // Лента — горизонтальная полоса ячеек едет и замедляется (как скины в CS)
}

public partial class WheelOfFortuneViewModel : ViewModelBase
{
    private readonly WheelListService _listService = new();
    private DispatcherTimer? _spinTimer;
    private long _spinStartTimeMs;
    private double _spinStartAngle;
    private double _spinTotalRotation;
    private double _pointerStartAngle;
    private double _pointerTotalRotation;
    private const double SpinDurationMs = 6000;
    private const int MaxItems = 24;
    private bool _suppressAutoSave;

    // Plinko state
    private DispatcherTimer? _plinkoTimer;
    private long _plinkoStartTimeMs;
    private readonly PlinkoPhysicsEngine _plinkoPhysics = new();
    private PlinkoScene _plinkoScene = PlinkoSceneFactory.Create(1);
    private IReadOnlyList<(double X, double Y)> _plinkoPath = [];
    private IReadOnlyList<double> _plinkoSegmentDurations = [];
    private int _plinkoResultIndex = -1;
    private const double PlinkoSelectionDurationMs = 2200;
    private const double PlinkoSelectionCycleMs = 90;
    private const double PlinkoBinFillDurationMs = 180;

    private int _plinkoSourceIndex = -1;
    private int _plinkoSelectionStart;
    private int _plinkoSelectionDistance;
    private int[] _plinkoBinOrder = [];

    [ObservableProperty]
    private bool _isPlinkoFillingBins;

    [ObservableProperty]
    private int _plinkoFilledCount;

    public PlinkoScene PlinkoScene
    {
        get => _plinkoScene;
        private set => SetProperty(ref _plinkoScene, value);
    }

    public void EnsurePlinkoScene()
    {
        var binCount = PlinkoScene.Objects.Count(o => o.Kind == Kind.Bin);
        if (binCount != WheelItems.Count)
            PlinkoScene = PlinkoSceneFactory.Create(WheelItems.Count);
    }

    public int GetPlinkoBinItemIndex(int slot)
    {
        if (slot < 0 || slot >= _plinkoBinOrder.Length || slot >= PlinkoFilledCount)
            return -1;
        return _plinkoBinOrder[slot];
    }

    // Strip (case-opening) state
    private DispatcherTimer? _stripTimer;
    private long _stripStartTimeMs;
    private double _stripStartOffset;
    private double _stripTotalOffset;
    private int _stripResultIndex = -1;
    private const double StripDurationMs = 5000;
    private const double ChestOpenDurationMs = 1200;
    private const int StripRepetitions = 30;

    // CS:GO-style rarity tiers: (color hex, weight). Weights are approximate CS:GO odds.
    public static readonly (string Color, double Weight)[] Rarities =
    [
        ("#b0c3d9", 79.92),  // Consumer Grade   — greyish white
        ("#5e98d9", 15.98),  // Industrial Grade  — light blue
        ("#4b69ff", 3.20),   // Mil-Spec          — blue
        ("#8847ff", 0.64),   // Restricted        — purple
        ("#d32ce6", 0.16),   // Classified        — pink
        ("#eb4b4b", 0.032),  // Covert            — red
        ("#e4ae39", 0.0026)  // Rare Special      — gold
    ];

    // Precomputed rarity color per absolute cell index (set in RunStrip)
    private string[] _stripRarityColors = [];

    /// <summary>Returns the precomputed rarity color for an absolute cell index, or null if not set.</summary>
    public string? GetStripRarityColor(int absoluteIndex)
    {
        if (absoluteIndex < 0 || absoluteIndex >= _stripRarityColors.Length)
            return null;
        return _stripRarityColors[absoluteIndex];
    }

    [ObservableProperty]
    private ObservableCollection<WheelItem> _wheelItems = new();

    [ObservableProperty]
    private ObservableCollection<WheelItem> _savedItems = new();

    [ObservableProperty]
    private ObservableCollection<string> _savedLists = new();

    [ObservableProperty]
    private string? _currentListName;

    [ObservableProperty]
    private string _newItemText = string.Empty;

    [ObservableProperty]
    private string _createListName = string.Empty;

    [ObservableProperty]
    private bool _isCreateListPopupOpen;

    [ObservableProperty]
    private string _newItemPopupText = string.Empty;

    [ObservableProperty]
    private string? _newItemPopupImagePath;

    [ObservableProperty]
    private bool _isAddItemPopupOpen;

    [ObservableProperty]
    private bool _isAddItemPopupOpenManager;

    [ObservableProperty]
    private string _renameListName = string.Empty;

    [ObservableProperty]
    private bool _isRenameListPopupOpen;

    private string? _renameOldName;

    [ObservableProperty]
    private double _rotationAngle;

    [ObservableProperty]
    private double _pointerAngle;

    [ObservableProperty]
    private bool _isSpinning;

    [ObservableProperty]
    private string? _result;

    [ObservableProperty]
    private WheelGameMode _gameMode = WheelGameMode.Classic;

    [ObservableProperty]
    private FortuneGameType _gameType = FortuneGameType.Wheel;

    /// <summary>Ball position in normalized board space (0..1 horizontally, 0..1 vertically).</summary>
    [ObservableProperty]
    private double _ballX;

    [ObservableProperty]
    private double _ballY;

    [ObservableProperty]
    private bool _isBallVisible;

    [ObservableProperty]
    private bool _isPlinkoSelecting;

    /// <summary>Currently selected top source cell for the next Plinko drop.</summary>
    public int PlinkoSourceIndex
    {
        get => _plinkoSourceIndex;
        private set => SetProperty(ref _plinkoSourceIndex, value);
    }

    /// <summary>Current absolute index shown in the animated source selector.</summary>
    [ObservableProperty]
    private int _plinkoSelectionOffset;

    /// <summary>Horizontal scroll offset of the strip in cell units (0 = first cell at left edge).</summary>
    [ObservableProperty]
    private double _stripOffset;

    /// <summary>Chest lid open progress: 0 = closed, 1 = fully open.</summary>
    [ObservableProperty]
    private double _chestOpenProgress;

    /// <summary>Strip reveal progress: 0 = hidden behind chest, 1 = fully visible.</summary>
    [ObservableProperty]
    private double _stripReveal;

    /// <summary>True while the chest opening animation plays (before the strip).</summary>
    [ObservableProperty]
    private bool _isChestOpening;

    // Strip result popup (CS:GO-style)
    [ObservableProperty]
    private bool _isStripResultPopupOpen;

    [ObservableProperty]
    private string? _stripResultColor;

    [ObservableProperty]
    private string? _stripResultText;

    [ObservableProperty]
    private string? _stripResultImagePath;

    [ObservableProperty]
    private int _teamPickTargetCount = 3;

    [ObservableProperty]
    private int _team2TargetCount = 3;

    [ObservableProperty]
    private bool _useTwoTeams;

    [ObservableProperty]
    private bool _isTeamSettingsPopupOpen;

    [ObservableProperty]
    private bool _isFortuneControlsExpanded = true;

    [ObservableProperty]
    private ObservableCollection<WheelItem> _team1 = new();

    [ObservableProperty]
    private ObservableCollection<WheelItem> _team2 = new();



    public bool IsTeamPickMode => GameMode == WheelGameMode.TeamPick;

    public bool IsWheelMode => GameType == FortuneGameType.Wheel;

    public bool IsPlinkoMode => GameType == FortuneGameType.Plinko;

    public bool IsStripMode => GameType == FortuneGameType.Strip;

    public int GameTypeIndex
    {
        get => (int)GameType;
        set
        {
            if (value >= 0 && value <= 2 && !IsSpinning)
                GameType = (FortuneGameType)value;
        }
    }

    public int PlinkoRowCount => PlinkoPhysicsEngine.Rows;

    public string GameTitle => GameType switch
    {
        FortuneGameType.Wheel => "Колесо Фортуны",
        FortuneGameType.Plinko => "Плинко",
        FortuneGameType.Strip => "Лента",
        _ => "Фортуна"
    };

    public string GameTypeIcon => GameType switch
    {
        FortuneGameType.Wheel => "🎡",
        FortuneGameType.Plinko => "🔻",
        FortuneGameType.Strip => "🎁",
        _ => "🎡"
    };

    public string GameTypeTooltip => GameType switch
    {
        FortuneGameType.Wheel => "Игра: Колесо — переключить на Плинко",
        FortuneGameType.Plinko => "Игра: Плинко — переключить на Ленту",
        FortuneGameType.Strip => "Игра: Лента — переключить на Колесо",
        _ => "Переключить игру"
    };

    public string ItemsHeader => GameType == FortuneGameType.Strip ? "В ленте:" : (IsWheelMode ? "На колесе:" : "На доске:");

    public string SpinButtonText => GameType switch
    {
        FortuneGameType.Wheel => "Крутить!",
        FortuneGameType.Plinko => "Бросить гранату",
        FortuneGameType.Strip => "Открыть кейс",
        _ => "Крутить!"
    };

    partial void OnWheelItemsChanged(ObservableCollection<WheelItem> value)
    {
        if (_plinkoTimer is null)
            PlinkoScene = PlinkoSceneFactory.Create(value.Count);
    }

    partial void OnGameTypeChanged(FortuneGameType value)
    {
        OnPropertyChanged(nameof(IsWheelMode));
        OnPropertyChanged(nameof(IsPlinkoMode));
        OnPropertyChanged(nameof(IsStripMode));
        OnPropertyChanged(nameof(GameTypeIndex));
        OnPropertyChanged(nameof(GameTitle));
        OnPropertyChanged(nameof(GameTypeIcon));
        OnPropertyChanged(nameof(GameTypeTooltip));
        OnPropertyChanged(nameof(ItemsHeader));
        OnPropertyChanged(nameof(SpinButtonText));
        Result = null;
        IsBallVisible = false;
        IsPlinkoSelecting = false;
        IsPlinkoFillingBins = false;
        PlinkoFilledCount = 0;
        StripOffset = 0;
        ChestOpenProgress = 0;
        StripReveal = 0;
        IsChestOpening = false;
        RefreshCanSpin();
    }

    public int GameModeIndex
    {
        get => (int)GameMode;
        set
        {
            if (GameMode != (WheelGameMode)value)
                GameMode = (WheelGameMode)value;
        }
    }

    public int TotalTeamPicks => Team1.Count + Team2.Count;

    public int TeamTargetTotal => UseTwoTeams
        ? TeamPickTargetCount + Team2TargetCount
        : TeamPickTargetCount;

    public bool CanSpinInCurrentMode =>
        GameMode != WheelGameMode.TeamPick || TotalTeamPicks < TeamTargetTotal;

    partial void OnGameModeChanged(WheelGameMode value)
    {
        OnPropertyChanged(nameof(IsTeamPickMode));
        OnPropertyChanged(nameof(GameModeIndex));
        OnPropertyChanged(nameof(CanSpinInCurrentMode));
        RefreshCanSpin();
    }

    partial void OnTeamPickTargetCountChanged(int value)
    {
        OnPropertyChanged(nameof(TeamTargetTotal));
        OnPropertyChanged(nameof(CanSpinInCurrentMode));
        RefreshCanSpin();
    }

    partial void OnTeam2TargetCountChanged(int value)
    {
        OnPropertyChanged(nameof(TeamTargetTotal));
        OnPropertyChanged(nameof(CanSpinInCurrentMode));
        RefreshCanSpin();
    }

    partial void OnUseTwoTeamsChanged(bool value)
    {
        if (!value)
            Team2.Clear();
        OnPropertyChanged(nameof(TeamTargetTotal));
        OnPropertyChanged(nameof(CanSpinInCurrentMode));
        RefreshCanSpin();
    }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveWheelItemCommand { get; }
    public ICommand AddFromSavedCommand { get; }
    public ICommand RemoveSavedItemCommand { get; }
    public ICommand SpinCommand { get; }
    public ICommand ClearWheelCommand { get; }
    public ICommand LoadListCommand { get; }
    public ICommand DeleteListCommand { get; }
    public ICommand PickImageCommand { get; }
    public ICommand SaveCurrentListCommand { get; }
    public ICommand CreateListCommand { get; }
    public ICommand OpenCreateListPopupCommand { get; }
    public ICommand OpenAddItemPopupCommand { get; }
    public ICommand OpenAddItemPopupManagerCommand { get; }
    public ICommand ConfirmAddItemCommand { get; }
    public ICommand ConfirmAddItemManagerCommand { get; }
    public ICommand PickNewItemImageCommand { get; }
    public ICommand AddAllToWheelCommand { get; }
    public ICommand ClearTeamsCommand { get; }
    public ICommand CopyTeamsCommand { get; }
    public ICommand ToggleGameTypeCommand { get; }
    public ICommand CloseStripResultPopupCommand { get; }
    public ICommand ToggleFortuneControlsCommand { get; }

    public WheelOfFortuneViewModel()
    {
        AddItemCommand = new RelayCommand(AddItem);
        RemoveWheelItemCommand = new RelayCommand<WheelItem>(RemoveWheelItem);
        AddFromSavedCommand = new RelayCommand<WheelItem>(AddFromSaved);
        RemoveSavedItemCommand = new RelayCommand<WheelItem>(RemoveSavedItem);
        SpinCommand = new RelayCommand(Spin, () => !IsSpinning && WheelItems.Count >= 2 && CanSpinInCurrentMode);
        ClearWheelCommand = new RelayCommand(ClearWheel);
        LoadListCommand = new RelayCommand<string>(LoadList);
        DeleteListCommand = new RelayCommand<string>(DeleteList);
        PickImageCommand = new RelayCommand<WheelItem>(PickImageForItem);
        SaveCurrentListCommand = new RelayCommand(SaveCurrentList, () => !string.IsNullOrEmpty(CurrentListName));
        CreateListCommand = new RelayCommand(CreateList, () => !string.IsNullOrWhiteSpace(CreateListName));
        OpenCreateListPopupCommand = new RelayCommand(() => IsCreateListPopupOpen = true);
        OpenAddItemPopupCommand = new RelayCommand(() =>
        {
            NewItemPopupText = string.Empty;
            NewItemPopupImagePath = null;
            IsAddItemPopupOpen = true;
        });
        OpenAddItemPopupManagerCommand = new RelayCommand(() =>
        {
            NewItemPopupText = string.Empty;
            NewItemPopupImagePath = null;
            IsAddItemPopupOpenManager = true;
        });
        ConfirmAddItemCommand = new RelayCommand(() => ConfirmAddItem(false), () =>
            !string.IsNullOrWhiteSpace(NewItemPopupText) || !string.IsNullOrEmpty(NewItemPopupImagePath));
        ConfirmAddItemManagerCommand = new RelayCommand(() => ConfirmAddItem(true));
        PickNewItemImageCommand = new RelayCommand(PickNewItemImage);
        AddAllToWheelCommand = new RelayCommand(AddAllToWheel, () => SavedItems.Count > 0);
        ClearTeamsCommand = new RelayCommand(ClearTeams);
        CopyTeamsCommand = new RelayCommand(CopyTeams);
        ToggleGameTypeCommand = new RelayCommand(() =>
            GameType = GameType switch
            {
                FortuneGameType.Wheel => FortuneGameType.Plinko,
                FortuneGameType.Plinko => FortuneGameType.Strip,
                _ => FortuneGameType.Wheel
            },
            () => !IsSpinning);
        CloseStripResultPopupCommand = new RelayCommand(() => IsStripResultPopupOpen = false);
        ToggleFortuneControlsCommand = new RelayCommand(() => IsFortuneControlsExpanded = !IsFortuneControlsExpanded);

        SavedItems.CollectionChanged += OnSavedItemsChanged;
        Team1.CollectionChanged += OnTeamCollectionChanged;
        Team2.CollectionChanged += OnTeamCollectionChanged;
        RefreshSavedLists();
    }

    public Func<IStorageProvider>? StorageProviderResolver { get; set; }

    private void OnSavedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressAutoSave && !string.IsNullOrEmpty(CurrentListName))
            SaveCurrentList();
        ((RelayCommand)AddAllToWheelCommand).NotifyCanExecuteChanged();
    }

    partial void OnCreateListNameChanged(string value) =>
        ((RelayCommand)CreateListCommand).NotifyCanExecuteChanged();

    partial void OnCurrentListNameChanged(string? value) =>
        ((RelayCommand)SaveCurrentListCommand).NotifyCanExecuteChanged();

    partial void OnNewItemPopupTextChanged(string value)
    {
        ((RelayCommand)ConfirmAddItemCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ConfirmAddItemManagerCommand).NotifyCanExecuteChanged();
    }

    partial void OnNewItemPopupImagePathChanged(string? value)
    {
        ((RelayCommand)ConfirmAddItemCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ConfirmAddItemManagerCommand).NotifyCanExecuteChanged();
    }

    public FortuneWorkspaceState ExportWorkspaceState(int selectedTabIndex) => new()
    {
        SelectedTabIndex = selectedTabIndex,
        GameType = GameType,
        GameMode = GameMode,
        CurrentListName = CurrentListName,
        WheelItems = WheelItems.ToList(),
        SavedItems = SavedItems.ToList(),
        Team1 = Team1.ToList(),
        Team2 = Team2.ToList(),
        UseTwoTeams = UseTwoTeams,
        TeamPickTargetCount = TeamPickTargetCount,
        Team2TargetCount = Team2TargetCount
    };

    public void RestoreWorkspaceState(FortuneWorkspaceState state)
    {
        _suppressAutoSave = true;
        try
        {
            GameType = Enum.IsDefined(state.GameType) ? state.GameType : FortuneGameType.Wheel;
            GameMode = Enum.IsDefined(state.GameMode) ? state.GameMode : WheelGameMode.Classic;
            CurrentListName = state.CurrentListName;
            WheelItems = new ObservableCollection<WheelItem>(state.WheelItems ?? []);
            SavedItems = new ObservableCollection<WheelItem>(state.SavedItems ?? []);
            Team1 = new ObservableCollection<WheelItem>(state.Team1 ?? []);
            Team2 = new ObservableCollection<WheelItem>(state.Team2 ?? []);
            Team1.CollectionChanged += OnTeamCollectionChanged;
            Team2.CollectionChanged += OnTeamCollectionChanged;
            UseTwoTeams = state.UseTwoTeams;
            TeamPickTargetCount = Math.Max(1, state.TeamPickTargetCount);
            Team2TargetCount = Math.Max(1, state.Team2TargetCount);
            SavedItems.CollectionChanged += OnSavedItemsChanged;
            ((RelayCommand)AddAllToWheelCommand).NotifyCanExecuteChanged();
            RefreshCanSpin();
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    public void SaveWorkspace(FortuneWorkspaceService service, int selectedTabIndex) =>
        service.Save(ExportWorkspaceState(selectedTabIndex));

    private void RefreshSavedLists()
    {
        SavedLists.Clear();
        foreach (var name in _listService.GetSavedListNames())
            SavedLists.Add(name);
    }

    private void AddItem()
    {
        var text = NewItemText.Trim();
        if (string.IsNullOrEmpty(text) || WheelItems.Count >= MaxItems)
            return;

        WheelItems.Add(new WheelItem(text));
        NewItemText = string.Empty;
        RefreshCanSpin();
    }

    private void RemoveWheelItem(WheelItem? item)
    {
        if (item is not null && WheelItems.Remove(item))
            RefreshCanSpin();
    }

    private void AddFromSaved(WheelItem? item)
    {
        if (item is null || WheelItems.Count >= MaxItems)
            return;
        WheelItems.Add(new WheelItem(item.Text, item.ImagePath));
        RefreshCanSpin();
    }

    private void AddAllToWheel()
    {
        foreach (var item in SavedItems)
        {
            if (WheelItems.Count >= MaxItems)
                break;
            WheelItems.Add(new WheelItem(item.Text, item.ImagePath));
        }
        RefreshCanSpin();
    }

    private void ClearTeams()
    {
        Team1.Clear();
        Team2.Clear();
    }

    private void CopyTeams()
    {
        var lines = new List<string>();
        if (UseTwoTeams)
        {
            lines.Add("Команда 1:");
            foreach (var item in Team1)
                lines.Add($"  • {item.DisplayName}");
            lines.Add("");
            lines.Add("Команда 2:");
            foreach (var item in Team2)
                lines.Add($"  • {item.DisplayName}");
        }
        else
        {
            lines.Add("Команда:");
            foreach (var item in Team1)
                lines.Add($"  • {item.DisplayName}");
        }

        ClipboardSetText?.Invoke(string.Join(Environment.NewLine, lines));
    }

    public Action<string?>? ClipboardSetText { get; set; }

    private void RemoveSavedItem(WheelItem? item)
    {
        if (item is not null)
            SavedItems.Remove(item);
    }

    private void ClearWheel()
    {
        WheelItems.Clear();
        Result = null;
        RefreshCanSpin();
    }

    private void LoadList(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        _suppressAutoSave = true;
        try
        {
            SavedItems = _listService.LoadList(name);
            SavedItems.CollectionChanged += OnSavedItemsChanged;
            CurrentListName = name;
            ((RelayCommand)AddAllToWheelCommand).NotifyCanExecuteChanged();
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    private void SaveCurrentList()
    {
        if (string.IsNullOrEmpty(CurrentListName))
            return;
        _listService.SaveList(CurrentListName, SavedItems);
    }

    private void CreateList()
    {
        var name = CreateListName.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        _suppressAutoSave = true;
        try
        {
            _listService.SaveList(name, Array.Empty<WheelItem>());
            RefreshSavedLists();
            SavedItems = new ObservableCollection<WheelItem>();
            SavedItems.CollectionChanged += OnSavedItemsChanged;
            CurrentListName = name;
            ((RelayCommand)AddAllToWheelCommand).NotifyCanExecuteChanged();
        }
        finally
        {
            _suppressAutoSave = false;
        }

        CreateListName = string.Empty;
        IsCreateListPopupOpen = false;
    }

    private void DeleteList(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return;
        _listService.DeleteList(name);

        if (CurrentListName == name)
        {
            _suppressAutoSave = true;
            try
            {
                SavedItems.Clear();
                CurrentListName = null;
            }
            finally
            {
                _suppressAutoSave = false;
            }
        }

        RefreshSavedLists();
    }

    public void StartRenameList(string oldName)
    {
        _renameOldName = oldName;
        RenameListName = oldName;
        IsRenameListPopupOpen = true;
    }

    public void ConfirmRenameList()
    {
        var oldName = _renameOldName;
        var newName = RenameListName.Trim();

        IsRenameListPopupOpen = false;
        _renameOldName = null;

        if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            return;

        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return;

        _listService.RenameList(oldName, newName);

        if (CurrentListName == oldName)
        {
            _suppressAutoSave = true;
            try
            {
                CurrentListName = newName;
                SavedItems = _listService.LoadList(newName);
                SavedItems.CollectionChanged += OnSavedItemsChanged;
                ((RelayCommand)AddAllToWheelCommand).NotifyCanExecuteChanged();
            }
            finally
            {
                _suppressAutoSave = false;
            }
        }

        RefreshSavedLists();
    }

    private async void PickImageForItem(WheelItem? item)
    {
        if (item is null || StorageProviderResolver is null)
            return;

        var storage = StorageProviderResolver();
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Изображения")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is not null)
        {
            var sourcePath = file.Path.LocalPath;
            item.ImagePath = string.IsNullOrEmpty(CurrentListName)
                ? sourcePath
                : _listService.CopyImageToList(CurrentListName, sourcePath);
            SaveCurrentList();
        }
    }

    private void ConfirmAddItem(bool toManager)
    {
        var text = string.IsNullOrWhiteSpace(NewItemPopupText) ? null : NewItemPopupText.Trim();
        var imagePath = string.IsNullOrEmpty(NewItemPopupImagePath) ? null : NewItemPopupImagePath;

        if (text is null && imagePath is null)
            return;

        var item = new WheelItem(text, imagePath);
        if (toManager)
        {
            SavedItems.Add(item);
            IsAddItemPopupOpenManager = false;
        }
        else if (WheelItems.Count < MaxItems)
        {
            WheelItems.Add(item);
            RefreshCanSpin();
            IsAddItemPopupOpen = false;
        }

        NewItemPopupText = string.Empty;
        NewItemPopupImagePath = null;
    }

    private async void PickNewItemImage()
    {
        if (StorageProviderResolver is null)
            return;

        var storage = StorageProviderResolver();
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Изображения")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                }
            }
        });

        var file = files.FirstOrDefault();
        if (file is not null)
        {
            var sourcePath = file.Path.LocalPath;
            NewItemPopupImagePath = string.IsNullOrEmpty(CurrentListName)
                ? sourcePath
                : _listService.CopyImageToList(CurrentListName, sourcePath);
        }
    }

    private void OnTeamCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TotalTeamPicks));
        OnPropertyChanged(nameof(CanSpinInCurrentMode));
        RefreshCanSpin();
    }

    private void RefreshCanSpin()
    {
        ((RelayCommand)SpinCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ToggleGameTypeCommand).NotifyCanExecuteChanged();
    }

    private void Spin()
    {
        if (WheelItems.Count < 2)
            return;

        if (GameType == FortuneGameType.Plinko)
            DropBall();
        else if (GameType == FortuneGameType.Strip)
            RunStrip();
        else
            SpinWheel();
    }

    private void SpinWheel()
    {
        Result = null;
        IsSpinning = true;
        RefreshCanSpin();

        var random = new Random();
        var extraSpins = random.Next(5, 8);
        var randomOffset = random.NextDouble() * 360.0;
        _spinTotalRotation = extraSpins * 360.0 + randomOffset;
        _spinStartAngle = RotationAngle;

        // Pointer spins in opposite direction, fewer rotations
        var pointerSpins = random.Next(3, 6);
        var pointerOffset = random.NextDouble() * 360.0;
        _pointerTotalRotation = -(pointerSpins * 360.0 + pointerOffset);
        _pointerStartAngle = PointerAngle;

        _spinStartTimeMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;

        _spinTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _spinTimer.Tick += OnSpinTick;
        _spinTimer.Start();
    }

    private void OnSpinTick(object? sender, EventArgs e)
    {
        var nowMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
        var elapsed = nowMs - _spinStartTimeMs;
        var t = Math.Clamp(elapsed / SpinDurationMs, 0.0, 1.0);

        var eased = EaseInOutQuart(t);
        RotationAngle = _spinStartAngle + _spinTotalRotation * eased;
        PointerAngle = _pointerStartAngle + _pointerTotalRotation * eased;

        if (t >= 1.0)
        {
            _spinTimer!.Stop();
            _spinTimer.Tick -= OnSpinTick;
            _spinTimer = null;

            // Result determined by relative angle between pointer and wheel
            var segmentAngle = 360.0 / WheelItems.Count;
            var relativeAngle = PointerAngle - RotationAngle;
            var normalized = ((relativeAngle % 360) + 360) % 360;
            var selectedIndex = (int)(normalized / segmentAngle) % WheelItems.Count;

            ApplyResult(selectedIndex);
        }
    }

    /// <summary>
    /// First cycles the top source cells, then drops the ball from the selected source.
    /// </summary>
    private void DropBall()
    {
        Result = null;
        IsSpinning = true;
        IsBallVisible = false;
        IsPlinkoFillingBins = true;
        IsPlinkoSelecting = false;
        PlinkoFilledCount = 0;
        RefreshCanSpin();

        var random = new Random();
        var n = WheelItems.Count;
        _plinkoBinOrder = Enumerable.Range(0, n).ToArray();
        ShufflePlinkoBins(random);

        PlinkoSourceIndex = n > 2 ? random.Next(1, n - 1) : random.Next(n);
        _plinkoSelectionStart = PlinkoSelectionOffset;
        _plinkoSelectionDistance = n * random.Next(5, 9) +
                                   ((PlinkoSourceIndex - _plinkoSelectionStart) % n + n) % n;
        _plinkoStartTimeMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;

        _plinkoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _plinkoTimer.Tick += OnPlinkoTick;
        _plinkoTimer.Start();
    }

    private void ShufflePlinkoBins(Random random)
    {
        for (var i = _plinkoBinOrder.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (_plinkoBinOrder[i], _plinkoBinOrder[j]) = (_plinkoBinOrder[j], _plinkoBinOrder[i]);
        }
        OnPropertyChanged(nameof(PlinkoSelectionOffset));
    }

    private void StartPlinkoDrop()
    {
        var random = new Random();
        var result = _plinkoPhysics.Simulate(WheelItems.Count, PlinkoSourceIndex, random);
        PlinkoScene = result.Scene;
        _plinkoPath = result.Path;
        _plinkoSegmentDurations = result.SegmentDurations;
        _plinkoResultIndex = result.ResultSlot;
        var n = Math.Max(1, WheelItems.Count);
        BallX = _plinkoPath[0].X / n;
        BallY = 0.0;
        UpdatePlinkoBallObject();
        IsBallVisible = true;
        IsPlinkoSelecting = false;
        _plinkoStartTimeMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
    }

    private void OnPlinkoTick(object? sender, EventArgs e)
    {
        var n = Math.Max(1, WheelItems.Count);
        var nowMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;

        if (IsPlinkoFillingBins)
        {
            var fillElapsed = nowMs - _plinkoStartTimeMs;
            var filled = Math.Min(n, (int)(fillElapsed / PlinkoBinFillDurationMs) + 1);
            if (filled != PlinkoFilledCount)
                PlinkoFilledCount = filled;

            if (filled >= n)
            {
                IsPlinkoFillingBins = false;
                IsPlinkoSelecting = true;
                _plinkoSelectionStart = PlinkoSelectionOffset;
                _plinkoStartTimeMs = nowMs;
            }
            return;
        }

        if (IsPlinkoSelecting)
        {
            var elapsed = nowMs - _plinkoStartTimeMs;
            var t = Math.Clamp(elapsed / PlinkoSelectionDurationMs, 0.0, 1.0);
            var eased = 1 - Math.Pow(1 - t, 3);
            PlinkoSelectionOffset = _plinkoSelectionStart +
                                    (int)Math.Floor(_plinkoSelectionDistance * eased);

            // The bottom row cycles its contents while the source cell is selected.
            if (PlinkoSelectionOffset != _plinkoSelectionStart)
                OnPropertyChanged(nameof(PlinkoSelectionOffset));

            if (t >= 1.0)
            {
                PlinkoSelectionOffset = _plinkoSelectionStart + _plinkoSelectionDistance;
                StartPlinkoDrop();
            }
            return;
        }

        var segments = _plinkoPath.Count - 1;
        if (segments < 1)
        {
            StopPlinko();
            return;
        }

        var elapsedDrop = (double)(nowMs - _plinkoStartTimeMs);
        var totalMs = _plinkoSegmentDurations.Sum();

        if (elapsedDrop >= totalMs)
        {
            var last = _plinkoPath[^1];
            BallX = last.X / n;
            BallY = last.Y;
            UpdatePlinkoBallObject();
            StopPlinko();
            // _plinkoResultIndex is a visual bin slot; resolve it through the randomized bin mapping.
            var resultItemIndex = GetPlinkoBinItemIndex(_plinkoResultIndex);
            ApplyResult(resultItemIndex);
            return;
        }

        var acc = 0.0;
        var index = 0;
        for (; index < segments; index++)
        {
            var duration = _plinkoSegmentDurations[index];
            if (elapsedDrop < acc + duration)
                break;
            acc += duration;
        }

        var segDuration = _plinkoSegmentDurations[Math.Min(index, _plinkoSegmentDurations.Count - 1)];
        var local = Math.Clamp((elapsedDrop - acc) / segDuration, 0.0, 1.0);
        var from = _plinkoPath[index];
        var to = _plinkoPath[index + 1];
        var vertical = local * local;
        var lateral = 1 - Math.Pow(1 - local, 3);
        BallX = (from.X + (to.X - from.X) * lateral) / n;
        BallY = from.Y + (to.Y - from.Y) * vertical;
        UpdatePlinkoBallObject();
    }

    private void UpdatePlinkoBallObject()
    {
        var n = Math.Max(1, WheelItems.Count);
        PlinkoScene.Ball.X = BallX * n;
        PlinkoScene.Ball.Y = BallY;
    }

    private void StopPlinko()
    {
        if (_plinkoTimer is null)
            return;

        _plinkoTimer.Stop();
        _plinkoTimer.Tick -= OnPlinkoTick;
        _plinkoTimer = null;
    }

    /// <summary>
    /// Runs the case-opening strip. The strip is a long horizontal band of repeated items
    /// (the wheel items cycled many times). It scrolls right-to-left and decelerates so the
    /// center marker lands on a random cell.
    /// </summary>
    private void RunStrip()
    {
        Result = null;
        IsSpinning = true;
        RefreshCanSpin();

        var n = WheelItems.Count;
        var random = new Random();

        // Pick the winning cell inside the final repetition band.
        // We lay out StripRepetitions copies of the items; the winner is in the last copy.
        // StripOffset is the absolute cell index that the renderer centers under the marker.
        _stripResultIndex = random.Next(n);
        var winnerAbsoluteIndex = (StripRepetitions - 1) * n + _stripResultIndex;

        // Always start from 0 so the strip flies in from the right
        StripOffset = 0;
        _stripStartOffset = 0;
        _stripTotalOffset = winnerAbsoluteIndex;

        // Precompute rarity colors for all visible cells (absolute indices 0..winnerAbsoluteIndex+margin)
        var totalCells = winnerAbsoluteIndex + n + 2;
        _stripRarityColors = new string[totalCells];
        var totalWeight = 0.0;
        foreach (var (_, w) in Rarities)
            totalWeight += w;

        for (var i = 0; i < totalCells; i++)
        {
            var roll = random.NextDouble() * totalWeight;
            var acc = 0.0;
            for (var r = 0; r < Rarities.Length; r++)
            {
                acc += Rarities[r].Weight;
                if (roll < acc)
                {
                    _stripRarityColors[i] = Rarities[r].Color;
                    break;
                }
            }
            _stripRarityColors[i] ??= Rarities[0].Color;
        }

        // Phase 1: chest opens, strip hidden
        ChestOpenProgress = 0;
        StripReveal = 0;
        IsChestOpening = true;

        _stripStartTimeMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
        _stripTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _stripTimer.Tick += OnStripTick;
        _stripTimer.Start();
    }

    private void OnStripTick(object? sender, EventArgs e)
    {
        var nowMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
        var elapsed = nowMs - _stripStartTimeMs;

        if (IsChestOpening)
        {
            // Phase 1: chest lid opens
            var t = Math.Clamp(elapsed / ChestOpenDurationMs, 0.0, 1.0);
            ChestOpenProgress = EaseOutBack(t);

            if (t >= 1.0)
            {
                IsChestOpening = false;
                // Reset timer for phase 2
                _stripStartTimeMs = nowMs;
            }
            return;
        }

        // Phase 2: strip reveals and scrolls
        var scrollT = Math.Clamp(elapsed / StripDurationMs, 0.0, 1.0);

        // Reveal the strip in the first 15% of scroll time
        StripReveal = Math.Clamp(scrollT / 0.15, 0.0, 1.0);

        // EaseOutQuart — fast start, long gentle tail, like a real case opening
        var eased = 1 - Math.Pow(1 - scrollT, 4);
        StripOffset = _stripStartOffset + _stripTotalOffset * eased;

        if (scrollT >= 1.0)
        {
            _stripTimer!.Stop();
            _stripTimer.Tick -= OnStripTick;
            _stripTimer = null;

            ApplyResult(Math.Clamp(_stripResultIndex, 0, WheelItems.Count - 1));
        }
    }

    private void StopStrip()
    {
        if (_stripTimer is null)
            return;

        _stripTimer.Stop();
        _stripTimer.Tick -= OnStripTick;
        _stripTimer = null;
    }

    /// <summary>Announces the winner and applies the active game mode (shared by wheel and plinko).</summary>
    private void ApplyResult(int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= WheelItems.Count)
        {
            IsSpinning = false;
            RefreshCanSpin();
            return;
        }

        var selectedItem = WheelItems[selectedIndex];
        Result = selectedItem.DisplayName;

        // Strip mode: show CS:GO-style popup with rarity color
        if (GameType == FortuneGameType.Strip)
        {
            StripResultText = selectedItem.DisplayName;
            StripResultImagePath = selectedItem.ImagePath;
            StripResultColor = GetStripRarityColor(_stripResultIndex >= 0
                ? (StripRepetitions - 1) * WheelItems.Count + _stripResultIndex
                : selectedIndex) ?? Rarities[0].Color;
            IsStripResultPopupOpen = true;
        }

        // Removing an item re-lays out the board, so a resting ball would point at the wrong bin
        var itemRemoved = false;

        switch (GameMode)
        {
            case WheelGameMode.Elimination:
                WheelItems.RemoveAt(selectedIndex);
                itemRemoved = true;
                break;

            case WheelGameMode.TeamPick when TotalTeamPicks < TeamTargetTotal:
            {
                // Pick which team to add to: fill team1 first, then team2
                var target = UseTwoTeams && Team1.Count >= TeamPickTargetCount ? Team2 : Team1;
                target.Add(selectedItem);
                WheelItems.RemoveAt(selectedIndex);
                itemRemoved = true;

                if (TotalTeamPicks >= TeamTargetTotal)
                {
                    var lines = new List<string>();
                    if (UseTwoTeams)
                    {
                        lines.Add("Команда 1:");
                        foreach (var i in Team1)
                            lines.Add($"  • {i.DisplayName}");
                        lines.Add("");
                        lines.Add("Команда 2:");
                        foreach (var i in Team2)
                            lines.Add($"  • {i.DisplayName}");
                    }
                    else
                    {
                        lines.Add("Команда:");
                        foreach (var i in Team1)
                            lines.Add($"  • {i.DisplayName}");
                    }
                    Result = string.Join("\n", lines);
                }
                break;
            }
        }

        IsSpinning = false;
        if (itemRemoved)
            IsBallVisible = false;
        RefreshCanSpin();
    }

    private static double EaseInOutQuart(double t)
    {
        return t < 0.5
            ? 8 * t * t * t * t
            : 1 - Math.Pow(-2 * t + 2, 4) / 2;
    }

    private static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }
}
