using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Platform.Storage;
using Circle.Desktop.Models;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class SongsView : UserControl
{
    private readonly DispatcherTimer _autoScrollTimer;
    private SongChord? _draggedChord;
    private Canvas? _dragCanvas;
    private double _dragPointerOffset;
    private SongLine? _draggedLine;
    private bool _isDraggingLine;
    private double _dragStartY;
    private const double DragThreshold = 5;

    public SongsView()
    {
        InitializeComponent();
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _autoScrollTimer.Start();
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (DataContext is not SongsViewModel vm || !vm.IsAutoScrollEnabled)
            return;

        var maximumOffset = Math.Max(0, GraphScrollViewer.Extent.Height - GraphScrollViewer.Viewport.Height);
        var nextOffset = Math.Min(maximumOffset, GraphScrollViewer.Offset.Y + 0.7 * vm.AutoScrollSpeed);
        GraphScrollViewer.Offset = new Vector(GraphScrollViewer.Offset.X, nextOffset);
        if (nextOffset >= maximumOffset)
            vm.IsAutoScrollEnabled = false;
    }

    private void ChordPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm
            || sender is not Control control
            || control.DataContext is not SongChord chord)
            return;

        if (e.GetCurrentPoint(control).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            if (vm.IsEditMode)
                ShowChordContextMenu(control, chord);
            e.Handled = true;
            return;
        }

        if (!vm.IsEditMode)
        {
            ShowChordVoicingPopup(control, chord);
            e.Handled = true;
            return;
        }

        _dragCanvas = control.FindAncestorOfType<Canvas>();
        if (_dragCanvas is null)
            return;

        _draggedChord = chord;
        var pointerX = e.GetPosition(_dragCanvas).X;
        _dragPointerOffset = pointerX - chord.PixelPosition;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private static void ShowChordVoicingPopup(Control target, SongChord chord)
    {
        var popup = new Flyout
        {
            Content = new ChordVoicingPopupView
            {
                DataContext = new ChordVoicingPopupViewModel(chord.Name)
            },
            Placement = PlacementMode.Bottom
        };
        popup.ShowAt(target);
    }

    private void ShowChordContextMenu(Control target, SongChord chord)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        var menu = new ContextMenu();
        var selectChordItem = new MenuItem { Header = "Выбрать аккорд" };
        var chordTypes = new (string Suffix, string Label)[]
        {
            ("", "Мажор"),
            ("m", "Минор"),
            ("7", "Септаккорд"),
            ("m7", "Минорный септаккорд"),
            ("dim", "Уменьшённый")
        };

        foreach (var letter in new[] { "A", "B", "C", "D", "E", "F", "G" })
        {
            var letterItem = new MenuItem { Header = letter };
            foreach (var (suffix, label) in chordTypes)
            {
                var typeItem = new MenuItem { Header = label };
                var chordName = letter + suffix;
                typeItem.Click += (_, _) => vm.ChangeChord(chord, chordName);
                letterItem.Items.Add(typeItem);
            }
            selectChordItem.Items.Add(letterItem);
        }

        menu.Items.Add(selectChordItem);
        menu.Items.Add(new Separator());
        var removeItem = new MenuItem { Header = "Удалить аккорд" };
        removeItem.Click += (_, _) => vm.RemoveChord(chord);
        menu.Items.Add(removeItem);
        menu.Open(target);
    }

    private void ChordPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedChord is null || _dragCanvas is null || e.Pointer.Captured is null)
            return;

        var characterWidth = _draggedChord.FontSize * 0.6;
        var pointerX = e.GetPosition(_dragCanvas).X;
        _draggedChord.Position = Math.Max(0, (pointerX - _dragPointerOffset) / characterWidth);
        e.Handled = true;
    }

    private void ChordPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedChord is null)
            return;

        e.Pointer.Capture(null);
        _draggedChord = null;
        _dragCanvas = null;
        e.Handled = true;
    }

    private async void ChooseLibraryFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Выберите папку с аккордами",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is not null)
            vm.SetLibraryRoot(folder.Path.LocalPath);
    }

    private static SongLine? FindSongLine(object? sender)
    {
        if (sender is not Control control)
            return null;
        return control.DataContext as SongLine;
    }

    private void LinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm || !vm.IsEditMode)
            return;

        var line = FindSongLine(sender);
        if (line is null || sender is not Control control)
            return;

        if (e.GetCurrentPoint(control).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            // Show line context menu manually — only when right-clicking the line itself,
            // not a chord (chord handler stops propagation with e.Handled = true)
            vm.LineContextMenu.DataContext = line;
            vm.LineContextMenu.Open(control);
            e.Handled = true;
            return;
        }

        _draggedLine = line;
        _dragStartY = e.GetPosition(GraphScrollViewer).Y;
        // Capture on ScrollViewer so events keep firing even if line controls get recycled
        e.Pointer.Capture(GraphScrollViewer);
        e.Handled = true;
    }

    private void ScrollViewerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedLine is null || DataContext is not SongsViewModel vm)
            return;

        if (!_isDraggingLine)
        {
            var pos = e.GetPosition(GraphScrollViewer);
            if (Math.Abs(pos.Y - _dragStartY) < DragThreshold)
                return;
            _isDraggingLine = true;
            HighlightDraggedLine();
        }

        // Find which line the cursor is over using direct coordinate comparison
        var targetIndex = FindHoverIndexByPosition(e);
        if (targetIndex >= 0)
        {
            var sourceIndex = vm.Lines.IndexOf(_draggedLine);
            if (targetIndex != sourceIndex)
                vm.Lines.Move(sourceIndex, targetIndex);
        }
        e.Handled = true;
    }

    private void ScrollViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedLine is null)
            return;

        // Release pointer capture
        e.Pointer.Capture(null);

        UnhighlightDraggedLine();
        _draggedLine = null;
        _isDraggingLine = false;
        e.Handled = true;
    }

    private void HighlightDraggedLine()
    {
        var controls = FindAllLineControls();
        foreach (var c in controls)
        {
            if (c is Grid grid && c.DataContext is SongLine sl && sl == _draggedLine)
            {
                grid.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x40, 0x3b, 0x82, 0xf6));
                grid.Opacity = 0.7;
            }
        }
    }

    private void UnhighlightDraggedLine()
    {
        var controls = FindAllLineControls();
        foreach (var c in controls)
        {
            if (c is Grid grid && c.DataContext is SongLine sl && sl == _draggedLine)
            {
                grid.Background = null;
                grid.Opacity = 1;
            }
        }
    }

    private List<Control> FindAllLineControls()
    {
        var result = new List<Control>();
        if (GraphScrollViewer.Content is not Visual root)
            return result;
        FindAllLineControls(root, result);
        return result;
    }

    private static void FindAllLineControls(Visual visual, List<Control> result)
    {
        if (visual is Control c && c.Name == "LineRoot")
            result.Add(c);
        foreach (var child in visual.GetVisualChildren())
            FindAllLineControls(child, result);
    }

    private int FindHoverIndexByPosition(PointerEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return -1;

        var lineControls = FindAllLineControls();
        if (lineControls.Count == 0)
            return -1;

        // Use GetPosition for each control to avoid offset issues
        foreach (var control in lineControls)
        {
            var posInControl = e.GetPosition(control);
            var height = control.Bounds.Height;
            if (height <= 0) continue;
            // If cursor is in the top half of this line, insert before it
            if (posInControl.Y >= 0 && posInControl.Y < height / 2 && control.DataContext is SongLine sl)
                return vm.Lines.IndexOf(sl);
            // If cursor is in the bottom half, insert after (next iteration will catch it)
        }

        // Cursor is past all lines — return last index
        if (lineControls.Count > 0 && lineControls[^1].DataContext is SongLine last)
            return vm.Lines.IndexOf(last);
        return -1;
    }

    private void OpenTablatures(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        var window = new TablaturesWindow { DataContext = vm };
        if (TopLevel.GetTopLevel(this) is Window owner)
            window.Show(owner);
        else
            window.Show();
    }

    private void AddTab(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        var window = new TabEditorWindow();
        window.TabSubmitted += (_, text) => vm.AddTab(text);
        if (TopLevel.GetTopLevel(this) is Window owner)
            window.Show(owner);
        else
            window.Show();
    }

    private async void OpenFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Открыть текст песни",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Текстовые файлы") { Patterns = ["*.txt", "*.pro", "*.chordpro"] },
                new FilePickerFileType("Все файлы") { Patterns = ["*.*"] }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
            return;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync();
        var format = GuessFormat(text);
        var title = Path.GetFileNameWithoutExtension(file.Name);
        vm.ImportText(text, format, title);
        vm.ExportFormat = format;
    }

    private async void SaveFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SongsViewModel vm)
            return;

        if (vm.SaveCurrentSong())
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var extension = vm.ExportFormat == SongTextFormat.ChordPro ? ".chordpro.txt" : ".txt";
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить песню",
            SuggestedFileName = MakeFileName(vm.Title, extension),
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Текстовые файлы") { Patterns = ["*.txt", "*.pro", "*.chordpro"] }
            ]
        });

        if (file is null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(vm.ExportText());
        vm.StatusMessage = "Файл сохранён.";
    }

    private static SongTextFormat GuessFormat(string text) => SongTextCodec.DetectFormat(text);

    private static string MakeFileName(string title, string extension)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeTitle = new string((title ?? "Без названия")
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());
        return (string.IsNullOrWhiteSpace(safeTitle) ? "Без названия" : safeTitle) + extension;
    }
}
