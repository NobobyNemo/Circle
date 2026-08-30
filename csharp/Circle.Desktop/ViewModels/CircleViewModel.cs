using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Circle.Core.Domain;
using Circle.Core.Extensions;
using Circle.Core.Music;
using Circle.Desktop.Helpers;
using Circle.Desktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Circle.Desktop.ViewModels;

public partial class CircleViewModel : ViewModelBase
{
    private readonly CircleOfFifths _circle;
    private readonly DegreeHighlightBuilder _highlightBuilder;

    private DispatcherTimer? _animationTimer;
    private double _animationStartAngle;
    private double _animationTargetAngle;
    private long _animationStartTimeMs;
    private const double AnimationDurationMs = 900;
    private bool _isInitialized;

    [ObservableProperty]
    private Key? _selectedKey;

    [ObservableProperty]
    private double _rotationAngle;

    [ObservableProperty]
    private Key? _relativeKey;

    [ObservableProperty]
    private IReadOnlyDictionary<string, DegreeHighlight> _degreeHighlights = new Dictionary<string, DegreeHighlight>();

    public CircleViewModel()
    {
        _circle = new CircleOfFifths();
        _highlightBuilder = new DegreeHighlightBuilder();
        SelectKeyCommand = new RelayCommand<Key>(OnSelectKey);

        _isInitialized = false;
        SelectedKey = _circle.MajorKeys[0];
        _isInitialized = true;
    }

    public IReadOnlyList<Key> MajorKeys => _circle.MajorKeys;
    public IReadOnlyList<Key> MinorKeys => _circle.MinorKeys;
    public double SegmentAngle => _circle.SegmentAngle;

    public ICommand SelectKeyCommand { get; }

    partial void OnSelectedKeyChanged(Key? value)
    {
        if (value is null)
        {
            RotationAngle = 0;
            RelativeKey = null;
            DegreeHighlights = new Dictionary<string, DegreeHighlight>();
            StopAnimation();
            return;
        }

        var index = _circle.IndexOf(value);
        var targetAngle = -((index + 0.5) * _circle.SegmentAngle);

        RelativeKey = value.GetRelative();
        DegreeHighlights = _highlightBuilder.BuildForKey(value);

        if (!_isInitialized)
        {
            RotationAngle = targetAngle;
            return;
        }

        while (targetAngle - RotationAngle > 180)
            targetAngle -= 360;
        while (targetAngle - RotationAngle < -180)
            targetAngle += 360;

        StartRotationAnimation(RotationAngle, targetAngle);
    }

    private void StartRotationAnimation(double fromAngle, double toAngle)
    {
        StopAnimation();

        _animationStartAngle = fromAngle;
        _animationTargetAngle = toAngle;
        _animationStartTimeMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer is null)
            return;
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var nowMs = Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency;
        var elapsed = nowMs - _animationStartTimeMs;
        var t = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

        var eased = EaseInOutCubic(t);
        RotationAngle = _animationStartAngle + (_animationTargetAngle - _animationStartAngle) * eased;

        if (t >= 1.0)
        {
            RotationAngle = _animationTargetAngle;
            StopAnimation();
        }
    }

    private static double EaseInOutCubic(double t)
    {
        return t < 0.5
            ? 4 * t * t * t
            : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private void OnSelectKey(Key? key)
    {
        if (key is not null)
            SelectedKey = key;
    }
}
