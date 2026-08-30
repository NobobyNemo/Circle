using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Circle.Core.Domain;
using Circle.Core.Extensions;
using Circle.Desktop.Helpers;
using Circle.Desktop.ViewModels;
using Key = Circle.Core.Domain.Key;
using Path = Avalonia.Controls.Shapes.Path;

namespace Circle.Desktop.Views;

public partial class CircleView : UserControl
{
    private const double LabelWidth = 44;
    private const double LabelHeight = 22;
    private const double BadgeSize = 22;
    private const double BadgeOffset = 14;

    private static readonly Color SectorColor = Color.Parse("#232b36");
    private static readonly Color StrokeInactive = Color.Parse("#111827");
    private static readonly Color ActiveStroke = Color.Parse("#ffffff");
    private static readonly Color SelectedFill = Color.Parse("#FF595E");
    private static readonly Color BadgeTextColor = Color.Parse("#111827");

    public CircleView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (DataContext is CircleViewModel vm)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(CircleViewModel.SelectedKey)
                    or nameof(CircleViewModel.DegreeHighlights)
                    or nameof(CircleViewModel.RotationAngle))
                {
                    RenderCircle(vm);
                }
            };

            RenderCircle(vm);
        }
    }

    private void RenderCircle(CircleViewModel vm)
    {
        CircleCanvas.Children.Clear();

        var rotation = vm.RotationAngle;
        var segmentAngle = vm.SegmentAngle;

        RenderRingSectors(vm.MajorKeys, CircleGeometry.MajorInnerRadius, CircleGeometry.OuterRadius, KeyType.Major, rotation, segmentAngle, vm);
        RenderRingSectors(vm.MinorKeys, CircleGeometry.MinorInnerRadius, CircleGeometry.MinorOuterRadius, KeyType.Minor, rotation, segmentAngle, vm);

        RenderRingLabels(vm.MajorKeys, CircleGeometry.OuterLabelRadius, KeyType.Major, rotation, segmentAngle, vm);
        RenderRingLabels(vm.MinorKeys, CircleGeometry.InnerLabelRadius, KeyType.Minor, rotation, segmentAngle, vm);

        RenderDegreeBadges(vm.MajorKeys, CircleGeometry.OuterRadius, KeyType.Major, rotation, segmentAngle, vm);
        RenderDegreeBadges(vm.MinorKeys, CircleGeometry.MinorOuterRadius, KeyType.Minor, rotation, segmentAngle, vm);
    }

    private void RenderRingSectors(
        IReadOnlyList<Key> keys,
        double innerRadius,
        double outerRadius,
        KeyType ringType,
        double rotation,
        double segmentAngle,
        CircleViewModel vm)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var startAngle = i * segmentAngle - 90 + rotation;
            var endAngle = (i + 1) * segmentAngle - 90 + rotation;

            var geometry = CircleGeometry.DescribeAnnularSector(
                CircleGeometry.CenterX,
                CircleGeometry.CenterY,
                innerRadius,
                outerRadius,
                startAngle,
                endAngle);

            var isSelected = vm.SelectedKey?.Equals(key) == true;
            var fill = isSelected ? new SolidColorBrush(SelectedFill) : ResolveFill(key, ringType, vm);

            var path = new Path
            {
                Data = geometry,
                Fill = fill,
                Stroke = new SolidColorBrush(StrokeInactive),
                StrokeThickness = 2,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            path.PointerPressed += (_, _) => vm.SelectKeyCommand.Execute(key);

            CircleCanvas.Children.Add(path);
        }
    }

    private void RenderRingLabels(
        IReadOnlyList<Key> keys,
        double labelRadius,
        KeyType ringType,
        double rotation,
        double segmentAngle,
        CircleViewModel vm)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var startAngle = i * segmentAngle - 90 + rotation;
            var midAngle = startAngle + segmentAngle / 2.0;
            var position = CircleGeometry.PolarToCartesian(CircleGeometry.CenterX, CircleGeometry.CenterY, labelRadius, midAngle);
            var fillColor = ResolveFillColor(key, ringType, vm);

            var isColored = fillColor != SectorColor;
            var label = new TextBlock
            {
                Text = key.Label(),
                Foreground = new SolidColorBrush(isColored ? Color.Parse("#111827") : Colors.White),
                FontSize = ringType == KeyType.Major ? 17 : 15,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Width = LabelWidth,
                Height = LabelHeight,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(label, position.X - LabelWidth / 2.0);
            Canvas.SetTop(label, position.Y - LabelHeight / 2.0);
            CircleCanvas.Children.Add(label);
        }
    }

    private void RenderDegreeBadges(
        IReadOnlyList<Key> keys,
        double ringOuterRadius,
        KeyType ringType,
        double rotation,
        double segmentAngle,
        CircleViewModel vm)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var highlight = vm.DegreeHighlights.Values.FirstOrDefault(h =>
                h.Ring == ringType && key.Note.IsEnharmonicWith(h.Note));

            if (highlight is null)
                continue;

            var midAngle = i * segmentAngle - 90 + rotation + segmentAngle / 2.0;
            var badgeRadius = ringOuterRadius + BadgeOffset;
            var position = CircleGeometry.PolarToCartesian(CircleGeometry.CenterX, CircleGeometry.CenterY, badgeRadius, midAngle);

            var badgeColor = RainbowColors.Colors[highlight.DegreeIndex];

            var badge = new Border
            {
                Width = BadgeSize,
                Height = BadgeSize,
                CornerRadius = new CornerRadius(BadgeSize / 2.0),
                Background = new SolidColorBrush(badgeColor),
                BorderBrush = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(1.5),
                Padding = new Thickness(0),
                IsHitTestVisible = false
            };

            badge.Child = new TextBlock
            {
                Text = highlight.DegreeLabel,
                Foreground = new SolidColorBrush(BadgeTextColor),
                FontSize = 10,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            Canvas.SetLeft(badge, position.X - BadgeSize / 2.0);
            Canvas.SetTop(badge, position.Y - BadgeSize / 2.0);
            CircleCanvas.Children.Add(badge);
        }
    }

    private IBrush ResolveFill(Key key, KeyType ringType, CircleViewModel vm)
    {
        return new SolidColorBrush(ResolveFillColor(key, ringType, vm));
    }

    private Color ResolveFillColor(Key key, KeyType ringType, CircleViewModel vm)
    {
        var highlight = vm.DegreeHighlights.Values.FirstOrDefault(h =>
            h.Ring == ringType && key.Note.IsEnharmonicWith(h.Note));

        if (highlight is not null)
            return RainbowColors.Colors[highlight.DegreeIndex];

        return SectorColor;
    }

    private static bool IsDarkColor(Color color)
    {
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance < 170;
    }
}
