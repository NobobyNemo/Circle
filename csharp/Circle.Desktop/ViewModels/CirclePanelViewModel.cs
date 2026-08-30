using System.Windows.Input;
using Avalonia.Threading;
using Circle.Core.Domain;
using Circle.Core.Extensions;
using Circle.Core.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class CirclePanelViewModel : ViewModelBase
{
    private readonly ModeService _modeService;
    private readonly ScaleSpeller _scaleSpeller;

    [ObservableProperty]
    private Key? _selectedKey;

    [ObservableProperty]
    private int _noteDuration = 400;

    [ObservableProperty]
    private IReadOnlyList<ModeRowViewModel> _modeRows = [];

    [ObservableProperty]
    private int? _playingRow;

    [ObservableProperty]
    private int? _activeStepIndex;

    [ObservableProperty]
    private double _noteProgress;

    [ObservableProperty]
    private double _modeProgress;

    [ObservableProperty]
    private int? _activeModeRowIndex;

    [ObservableProperty]
    private Key? _relativeKey;

    [ObservableProperty]
    private string _selectedKeyLabel = "-";

    [ObservableProperty]
    private string _relativeKeyLabel = "-";

    public CirclePanelViewModel()
    {
        _modeService = new ModeService();
        _scaleSpeller = new ScaleSpeller();

        PlayDegreeCommand = new RelayCommand<int>(OnPlayDegree);
        PlayModeCommand = new RelayCommand<int>(OnPlayMode);
    }

    public ICommand PlayDegreeCommand { get; }
    public ICommand PlayModeCommand { get; }

    partial void OnSelectedKeyChanged(Key? value)
    {
        RelativeKey = value?.GetRelative();
        SelectedKeyLabel = value?.Label() ?? "-";
        RelativeKeyLabel = RelativeKey?.Label() ?? "-";
        ActiveModeRowIndex = value?.Type == KeyType.Major ? 0 : 5;
        RebuildModeRows();
    }

    partial void OnNoteDurationChanged(int value)
    {
        RebuildModeRows();
    }

    private void RebuildModeRows()
    {
        if (SelectedKey is null)
        {
            ModeRows = [];
            return;
        }

        var preferFlats = SelectedKey.IsFlat();
        var modeInfos = _modeService.GetModesForKey(SelectedKey);

        ModeRows = modeInfos
            .Select((info, index) => new ModeRowViewModel(
                info.Mode.Name,
                _scaleSpeller.Spell(SelectedKey.Note, info.Mode.Intervals, preferFlats),
                info.ScaleWithChords,
                index))
            .ToList();
    }

    private void OnPlayDegree(int degreeIndex)
    {
        if (SelectedKey is null)
            return;

        _ = AnimateProgressAsync(
            durationMs: NoteDuration,
            progressCallback: p => NoteProgress = p,
            completionCallback: () =>
            {
                NoteProgress = 0;
                ActiveStepIndex = null;
                PlayingRow = null;
            });
    }

    private void OnPlayMode(int rowIndex)
    {
        _ = AnimateProgressAsync(
            durationMs: NoteDuration * 7,
            progressCallback: p => ModeProgress = p,
            completionCallback: () =>
            {
                ModeProgress = 0;
                ActiveStepIndex = null;
                PlayingRow = null;
            });
    }

    private static async Task AnimateProgressAsync(
        int durationMs,
        Action<double> progressCallback,
        Action completionCallback)
    {
        const int steps = 30;
        var delay = durationMs / steps;

        for (var i = 1; i <= steps; i++)
        {
            await Task.Delay(delay);
            await Dispatcher.UIThread.InvokeAsync(() => progressCallback(i / (double)steps));
        }

        await Dispatcher.UIThread.InvokeAsync(completionCallback);
    }
}

public sealed record ModeRowViewModel(
    string ModeName,
    IReadOnlyList<Note> Scale,
    IReadOnlyList<ScaleDegree> ScaleWithChords,
    int RowIndex);
