using System.Windows.Input;
using Circle.Desktop.Audio;
using Circle.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public CircleViewModel CircleViewModel { get; } = new();
    public CirclePanelViewModel CirclePanelViewModel { get; } = new();
    public TunerViewModel TunerViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    public SongsViewModel SongsViewModel { get; } = new();
    public WheelOfFortuneViewModel WheelOfFortuneViewModel { get; } = new();
    public CirclePageViewModel CirclePageViewModel { get; }

    [ObservableProperty]
    private object _currentPage = null!;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private double _sidebarWidth = 220;

    [ObservableProperty]
    private int _selectedSettingsTabIndex;

    public bool IsCircleSelected => SelectedTabIndex == 0;
    public bool IsTunerSelected => SelectedTabIndex == 1;
    public bool IsSongsSelected => SelectedTabIndex == 2;
    public bool IsFortuneSelected => SelectedTabIndex == 3;

    public ICommand ToggleSidebarCommand { get; }

    public ICommand ShowCircleCommand { get; }
    public ICommand ShowTunerCommand { get; }
    public ICommand ShowSongsCommand { get; }
    public ICommand ShowSettingsCommand { get; }

    public event EventHandler? SettingsRequested;

    private readonly FortuneWorkspaceService _workspaceService = new();
    private bool _workspaceLoaded;

    public MainViewModel()
    {
        var audioCaptureService = new MediaCaptureStreamService();
        TunerViewModel = new TunerViewModel(audioCaptureService);
        SettingsViewModel = new SettingsViewModel(audioCaptureService);
        CirclePageViewModel = new CirclePageViewModel(CircleViewModel, CirclePanelViewModel);

        ShowCircleCommand = new RelayCommand(() => SelectPage(0, CirclePageViewModel));
        ShowTunerCommand = new RelayCommand(() => SelectPage(1, TunerViewModel));
        ShowSongsCommand = new RelayCommand(() => SelectPage(2, SongsViewModel));
        ShowSettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty));
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        CurrentPage = CirclePageViewModel;

        CircleViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CircleViewModel.SelectedKey))
                CirclePanelViewModel.SelectedKey = CircleViewModel.SelectedKey;
        };

        CirclePanelViewModel.SelectedKey = CircleViewModel.SelectedKey;

        var workspace = _workspaceService.Load();
        if (workspace is not null)
        {
            WheelOfFortuneViewModel.RestoreWorkspaceState(workspace);
            SelectedTabIndex = Math.Clamp(workspace.SelectedTabIndex, 0, 3);
        }
        _workspaceLoaded = true;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        CurrentPage = value switch
        {
            0 => CirclePageViewModel,
            1 => TunerViewModel,
            2 => SongsViewModel,
            3 => WheelOfFortuneViewModel,
            _ => CurrentPage
        };

        OnPropertyChanged(nameof(IsCircleSelected));
        OnPropertyChanged(nameof(IsTunerSelected));
        OnPropertyChanged(nameof(IsSongsSelected));
        OnPropertyChanged(nameof(IsFortuneSelected));
        if (_workspaceLoaded)
            SaveWorkspace();
    }

    public void SaveWorkspace() =>
        WheelOfFortuneViewModel.SaveWorkspace(_workspaceService, SelectedTabIndex);

    partial void OnIsSidebarExpandedChanged(bool value) => SidebarWidth = value ? 220 : 80;

    private void SelectPage(int index, object page)
    {
        SelectedTabIndex = index;
        CurrentPage = page;
    }
}
