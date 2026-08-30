using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public sealed class ChordDiagramView : Control
{
    private static readonly IBrush LineBrush = new SolidColorBrush(Color.Parse("#9ca3af"));
    private static readonly IBrush DotBrush = new SolidColorBrush(Color.Parse("#31d68f"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#d1d5db"));

    public ChordDiagramView()
    {
        DataContextChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (DataContext is not ChordVoicingPopupViewModel { SelectedVoicing: not null } vm)
            return;

        var positions = vm.SelectedVoicing.Positions;
        var fretted = positions.Where(position => position.Fret > 0).ToArray();
        var firstFret = fretted.Length == 0 ? 1 : Math.Max(1, fretted.Min(position => position.Fret) - 1);
        var lastFret = fretted.Length == 0 ? firstFret : fretted.Max(position => position.Fret);
        var fretCount = Math.Max(3, lastFret - firstFret + 3);
        const double left = 34;
        const double top = 30;
        const double stringSpacing = 24;
        const double fretSpacing = 32;

        for (var stringIndex = 0; stringIndex < 6; stringIndex++)
        {
            var y = top + stringIndex * stringSpacing;
            context.DrawLine(new Pen(LineBrush, 1.5), new Point(left, y), new Point(left + fretCount * fretSpacing, y));
        }

        for (var fretIndex = 0; fretIndex <= fretCount; fretIndex++)
        {
            var x = left + fretIndex * fretSpacing;
            context.DrawLine(new Pen(LineBrush, fretIndex == 0 ? 3 : 1), new Point(x, top), new Point(x, top + stringSpacing * 5));
        }

        for (var fretIndex = 0; fretIndex < fretCount; fretIndex++)
        {
            var x = left + (fretIndex + 0.5) * fretSpacing;
            var label = new FormattedText(
                (firstFret + fretIndex).ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                12,
                LabelBrush);
            context.DrawText(label, new Point(x - label.Width / 2, 2));
        }

        foreach (var position in positions)
        {
            var stringIndex = position.StringNumber - 1;
            if (stringIndex is < 0 or > 5)
                continue;

            var y = top + stringIndex * stringSpacing;
            if (position.Fret < 0)
            {
                var x = left + fretSpacing / 2;
                context.DrawLine(new Pen(DotBrush, 2), new Point(x - 6, y - 6), new Point(x + 6, y + 6));
                context.DrawLine(new Pen(DotBrush, 2), new Point(x + 6, y - 6), new Point(x - 6, y + 6));
                continue;
            }

            var label = new FormattedText(
                position.Note,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                12,
                DotBrush);
            context.DrawText(label, new Point(left - label.Width - 8, y - label.Height / 2));

            if (position.Fret == 0)
                continue;

            var fretOffset = position.Fret - firstFret;
            if (fretOffset is >= 0 and < 20)
            {
                var x = left + (fretOffset + 0.5) * fretSpacing;
                context.DrawEllipse(DotBrush, null, new Point(x, y), 8, 8);
            }
        }
    }
}
