using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Circle.Core.Domain;
using Circle.Desktop.Helpers;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class CirclePanelView : UserControl
{
    private static readonly Color HeaderBackground = Color.Parse("#23272f");
    private static readonly Color CellBackground = Color.Parse("#23272f");
    private static readonly Color CellHover = Color.Parse("#27303a");
    private static readonly Color ModeNameColor = Color.Parse("#31d68f");
    private static readonly Color ActiveColor = Color.Parse("#ffd54f");

    public CirclePanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (DataContext is not CirclePanelViewModel vm)
            return;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CirclePanelViewModel.ModeRows)
                or nameof(CirclePanelViewModel.ActiveStepIndex)
                or nameof(CirclePanelViewModel.PlayingRow)
                or nameof(CirclePanelViewModel.NoteProgress)
                or nameof(CirclePanelViewModel.ModeProgress))
            {
                RenderGrid(vm);
            }
        };

        RenderGrid(vm);
    }

    private void RenderGrid(CirclePanelViewModel vm)
    {
        var grid = ModesGrid;
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        const int columns = 8;
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));
        for (var i = 1; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(72)));

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var i = 0; i < vm.ModeRows.Count; i++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Header
        AddHeaderCell(grid, "Mode", 0, HeaderBackground);
        for (var i = 0; i < RainbowColors.DegreeLabels.Length; i++)
            AddHeaderCell(grid, RainbowColors.DegreeLabels[i], 1 + i, RainbowColors.Colors[i]);

        // Rows
        for (var rowIndex = 0; rowIndex < vm.ModeRows.Count; rowIndex++)
        {
            var row = vm.ModeRows[rowIndex];
            var isPlayingRow = vm.PlayingRow == rowIndex;
            var isActiveModeRow = vm.ActiveModeRowIndex == rowIndex;

            var modeCell = CreateCell(row.ModeName, CellBackground, ModeNameColor, rowIndex + 1, 0);
            modeCell.Cursor = new Cursor(StandardCursorType.Hand);
            modeCell.PointerPressed += (_, _) => vm.PlayModeCommand.Execute(rowIndex);
            grid.Children.Add(modeCell);

            for (var degreeIndex = 0; degreeIndex < row.Scale.Count; degreeIndex++)
            {
                var note = row.Scale[degreeIndex];
                var chordType = row.ScaleWithChords[degreeIndex].ChordType;
                var suffix = chordType switch
                {
                    "min" => "m",
                    "dim" => "dim",
                    _ => ""
                };

                var isActiveCell = isPlayingRow && vm.ActiveStepIndex == degreeIndex;
                var degreeColor = RainbowColors.Colors[degreeIndex];
                var baseColor = isActiveCell
                    ? ActiveColor
                    : isActiveModeRow
                        ? degreeColor
                        : CellBackground;
                var textColor = isActiveCell || isActiveModeRow ? Color.Parse("#222222") : Colors.White;

                var cell = CreateCell(note.Name + suffix, baseColor, textColor, rowIndex + 1, degreeIndex + 1);
                cell.Cursor = new Cursor(StandardCursorType.Hand);

                if (!isActiveCell && !isActiveModeRow)
                {
                    cell.PointerEntered += (_, _) => cell.Background = new SolidColorBrush(CellHover);
                    cell.PointerExited += (_, _) => cell.Background = new SolidColorBrush(CellBackground);
                }

                var capturedDegree = degreeIndex;
                cell.PointerPressed += (_, _) => vm.PlayDegreeCommand.Execute(capturedDegree);

                grid.Children.Add(cell);
            }
        }
    }

    private static void AddHeaderCell(Grid grid, string text, int column, Color backgroundColor)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(backgroundColor),
            BorderBrush = new SolidColorBrush(Color.Parse("#333a44")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 12),
            CornerRadius = new CornerRadius(6, 6, 0, 0)
        };

        var textColor = backgroundColor == HeaderBackground ? Colors.White : Color.Parse("#222222");

        border.Child = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(textColor),
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        Grid.SetRow(border, 0);
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
    }

    private static Border CreateCell(string text, Color background, Color foreground, int row, int column)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(Color.Parse("#333a44")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 12),
            CornerRadius = new CornerRadius(6)
        };

        border.Child = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(foreground),
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        return border;
    }

    private static bool IsDarkColor(Color color)
    {
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance < 128;
    }
}
