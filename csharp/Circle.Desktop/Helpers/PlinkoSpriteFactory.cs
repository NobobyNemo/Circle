using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Circle.Desktop.Helpers;

/// <summary>Creates lightweight vector sprites for Plinko GameObjects.</summary>
public sealed class PlinkoSpriteFactory
{
    public Control CreatePeg(double size)
    {
        var root = new Grid { Width = size, Height = size };
        root.Children.Add(new Ellipse
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#dbeafe"), 0),
                    new GradientStop(Color.Parse("#94a3b8"), 0.45),
                    new GradientStop(Color.Parse("#475569"), 1)
                }
            },
            Stroke = new SolidColorBrush(Color.Parse("#e2e8f0")),
            StrokeThickness = Math.Max(0.5, size * 0.06)
        });
        root.Children.Add(new Ellipse
        {
            Width = size * 0.34,
            Height = size * 0.2,
            Fill = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(size * 0.2, size * 0.15, 0, 0)
        });
        return root;
    }

    public Control CreateSpring(double width, double height)
    {
        var root = new Grid { Width = width, Height = height };
        root.Children.Add(new Ellipse
        {
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#fef08a"), 0),
                    new GradientStop(Color.Parse("#fbbf24"), 0.45),
                    new GradientStop(Color.Parse("#b45309"), 1)
                }
            },
            Stroke = new SolidColorBrush(Color.Parse("#78350f")),
            StrokeThickness = Math.Max(0.5, height * 0.08)
        });
        root.Children.Add(new Ellipse
        {
            Width = width * 0.35,
            Height = height * 0.3,
            Fill = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(width * 0.18, height * 0.12, 0, 0)
        });
        return root;
    }

    public Control CreateBall(double size)
    {
        var root = new Grid { Width = size, Height = size };
        root.Children.Add(new Ellipse
        {
            Fill = new RadialGradientBrush
            {
                Center = new RelativePoint(0.32, 0.26, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.28, 0.2, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#fff7ae"), 0),
                    new GradientStop(Color.Parse("#fde047"), 0.35),
                    new GradientStop(Color.Parse("#ca8a04"), 1)
                }
            },
            Stroke = new SolidColorBrush(Color.Parse("#713f12")),
            StrokeThickness = Math.Max(0.8, size * 0.07)
        });
        root.Children.Add(new Ellipse
        {
            Width = size * 0.22,
            Height = size * 0.14,
            Fill = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Avalonia.Thickness(size * 0.24, size * 0.18, 0, 0)
        });
        return root;
    }
}
