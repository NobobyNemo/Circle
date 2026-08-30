using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Circle.Desktop.Helpers;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Views;

public partial class WheelOfFortuneView : UserControl
{
    private static readonly Color[] Palette =
    [
        Color.Parse("#334155"),
        Color.Parse("#1e293b")
    ];

    private WheelListManagerWindow? _managerWindow;
    private readonly PlinkoBoardRenderer _plinkoRenderer = new();
    private bool _renderPending;

    public WheelOfFortuneView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
        SizeChanged += (_, _) => ScheduleRender();
        WheelCanvas.SizeChanged += (_, _) => ScheduleRender();
        PlinkoCanvas.SizeChanged += (_, _) => ScheduleRender();
        StripCanvas.SizeChanged += (_, _) => ScheduleRender();
    }

    private void AttachViewModel()
    {
        if (DataContext is WheelOfFortuneViewModel vm)
        {
            vm.StorageProviderResolver = () => TopLevel.GetTopLevel(this)!.StorageProvider;
            vm.ClipboardSetText = async text =>
            {
                if (string.IsNullOrEmpty(text))
                    return;
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is not null)
                    await clipboard.SetTextAsync(text);
            };

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(WheelOfFortuneViewModel.WheelItems)
                    or nameof(WheelOfFortuneViewModel.RotationAngle)
                    or nameof(WheelOfFortuneViewModel.PointerAngle)
                    or nameof(WheelOfFortuneViewModel.GameType)
                    or nameof(WheelOfFortuneViewModel.BallX)
                    or nameof(WheelOfFortuneViewModel.BallY)
                    or nameof(WheelOfFortuneViewModel.IsBallVisible)
                    or nameof(WheelOfFortuneViewModel.PlinkoScene)
                    or nameof(WheelOfFortuneViewModel.StripOffset)
                    or nameof(WheelOfFortuneViewModel.PlinkoSelectionOffset)
                    or nameof(WheelOfFortuneViewModel.PlinkoSourceIndex)
                    or nameof(WheelOfFortuneViewModel.IsPlinkoSelecting)
                    or nameof(WheelOfFortuneViewModel.PlinkoFilledCount)
                    or nameof(WheelOfFortuneViewModel.IsPlinkoFillingBins)
                    or nameof(WheelOfFortuneViewModel.ChestOpenProgress)
                    or nameof(WheelOfFortuneViewModel.StripReveal)
                    or nameof(WheelOfFortuneViewModel.IsChestOpening))
                {
                    if (e.PropertyName == nameof(WheelOfFortuneViewModel.WheelItems))
                        AttachWheelItemsChanged(vm);
                    ScheduleRender();
                }
                else if (e.PropertyName == nameof(WheelOfFortuneViewModel.Result))
                {
                    OnResultChanged(vm.Result);
                }
            };

            AttachWheelItemsChanged(vm);
            ScheduleRender();
        }
    }

    private void AttachWheelItemsChanged(WheelOfFortuneViewModel vm)
    {
        vm.WheelItems.CollectionChanged += (_, _) => ScheduleRender();
    }

    private void ScheduleRender()
    {
        if (DataContext is not WheelOfFortuneViewModel vm)
            return;

        if (_renderPending)
            return;

        _renderPending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _renderPending = false;
            if (vm.IsPlinkoMode)
                RenderPlinko(vm);
            else if (vm.IsStripMode)
                RenderStrip(vm);
            else
                RenderWheel(vm);
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void RenderWheel(WheelOfFortuneViewModel vm)
    {
        var canvas = WheelCanvas;
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;

        if (width < 10 || height < 10)
            return;

        canvas.Children.Clear();

        if (vm.WheelItems.Count == 0)
            return;

        var size = Math.Min(width, height);
        var center = size / 2.0;
        var wheelRadius = center * 0.92;
        var offsetX = (width - size) / 2.0;
        var offsetY = (height - size) / 2.0;

        var count = vm.WheelItems.Count;
        var segmentAngle = 360.0 / count;
        var rotation = vm.RotationAngle;

        // Mechanical wheel sprite is the base; segment fills are intentionally omitted.
        var wheelSprite = new Image
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://Circle.Desktop/Assets/WheelOfFortune/Wheel.png"))),
            Width = wheelRadius * 2,
            Height = wheelRadius * 2,
            Stretch = Stretch.Uniform,
            RenderTransform = new RotateTransform(rotation, wheelRadius, wheelRadius),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(wheelSprite, center + offsetX - wheelRadius);
        Canvas.SetTop(wheelSprite, center + offsetY - wheelRadius);
        canvas.Children.Add(wheelSprite);

        var metalStroke = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#e2e8f0"), 0),
                new GradientStop(Color.Parse("#64748b"), 0.35),
                new GradientStop(Color.Parse("#f8fafc"), 0.5),
                new GradientStop(Color.Parse("#334155"), 1)
            }
        };

        for (var i = 0; i < count; i++)
        {
            var item = vm.WheelItems[i];
            var startAngle = i * segmentAngle - 90 + rotation;
            var endPoint = CircleGeometry.PolarToCartesian(
                center + offsetX, center + offsetY, wheelRadius, startAngle);

            var separator = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(center + offsetX, center + offsetY),
                EndPoint = endPoint,
                Stroke = metalStroke,
                StrokeThickness = Math.Max(1.5, size / 170.0),
                IsHitTestVisible = false
            };
            canvas.Children.Add(separator);

            var midAngle = startAngle + segmentAngle / 2.0;
            var labelPos = CircleGeometry.PolarToCartesian(
                center + offsetX, center + offsetY, wheelRadius * 0.65, midAngle);

            if (item.HasImage)
            {
                try
                {
                    var bitmap = new Bitmap(item.ImagePath);
                    var imgSize = size * (count > 12 ? 0.075 : 0.105);
                    var image = new Image
                    {
                        Source = bitmap,
                        Width = imgSize,
                        Height = imgSize,
                        Stretch = Stretch.Uniform,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(image, labelPos.X - imgSize / 2.0);
                    Canvas.SetTop(image, labelPos.Y - imgSize / 2.0);
                    canvas.Children.Add(image);
                }
                catch
                {
                    AddTextLabel(canvas, item.DisplayName, labelPos, count, size);
                }
            }
            else
            {
                AddTextLabel(canvas, item.DisplayName, labelPos, count, size);
            }
        }

        var centerCircleSize = size * 0.07;
        var centerCircle = new Avalonia.Controls.Shapes.Ellipse
        {
            Width = centerCircleSize,
            Height = centerCircleSize,
            Fill = new SolidColorBrush(Color.Parse("#1e293b")),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = Math.Max(1, size / 140.0)
        };
        Canvas.SetLeft(centerCircle, center + offsetX - centerCircleSize / 2.0);
        Canvas.SetTop(centerCircle, center + offsetY - centerCircleSize / 2.0);
        canvas.Children.Add(centerCircle);

        // Pointer rotates around the wheel based on PointerAngle
        var pointerAngleRad = (vm.PointerAngle - 90) * Math.PI / 180.0;
        var pointerOrbitRadius = wheelRadius + size * 0.02;
        var pointerWidth = size * 0.055;
        var pointerHeight = size * 0.055;

        var px = center + offsetX + pointerOrbitRadius * Math.Cos(pointerAngleRad);
        var py = center + offsetY + pointerOrbitRadius * Math.Sin(pointerAngleRad);

        // Tangent direction for the pointer base, inward normal for the tip
        var inwardX = -Math.Cos(pointerAngleRad);
        var inwardY = -Math.Sin(pointerAngleRad);
        var tangentX = -inwardY;
        var tangentY = inwardX;

        var baseLeft = new Point(px + tangentX * pointerWidth / 2, py + tangentY * pointerWidth / 2);
        var baseRight = new Point(px - tangentX * pointerWidth / 2, py - tangentY * pointerWidth / 2);
        var tip = new Point(px + inwardX * pointerHeight, py + inwardY * pointerHeight);

        var pointer = new Avalonia.Controls.Shapes.Polygon
        {
            Points = new Points { baseLeft, baseRight, tip },
            Fill = new SolidColorBrush(Color.Parse("#fde047")),
            Stroke = new SolidColorBrush(Color.Parse("#1e293b")),
            StrokeThickness = Math.Max(1, size / 210.0)
        };
        canvas.Children.Add(pointer);
    }

    private void RenderPlinko(WheelOfFortuneViewModel vm)
    {
        _plinkoRenderer.Render(PlinkoCanvas, vm);
    }

    /* Legacy implementation moved to PlinkoBoardRenderer.
    private void RenderPlinkoLegacy(WheelOfFortuneViewModel vm)
    {
        var canvas = PlinkoCanvas;
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;

        if (width < 10 || height < 10)
            return;

        canvas.Children.Clear();

        var count = vm.WheelItems.Count;
        if (count == 0)
            return;

        var rows = vm.PlinkoRowCount;
        var binWidth = width / count;

        // Upper source cells show where the ball will start.
        var sourceCellSize = Math.Min(binWidth * 0.86, height * 0.12);
        var sourceGap = Math.Max(4.0, height * 0.018);
        var sourceTop = Math.Max(4.0, (height * 0.16 - sourceCellSize) / 2.0);
        var boardTop = sourceTop + sourceCellSize + sourceGap;

        // Board is split: pegs on top, bins at the bottom
        var binHeight = Math.Min(height * 0.22, binWidth * 1.6);
        var pegAreaHeight = height - boardTop - binHeight;
        var rowGap = pegAreaHeight / (rows + 1);
        var pegRadius = Math.Max(2.0, Math.Min(binWidth * 0.11, rowGap * 0.26));

        for (var i = 0; i < count; i++)
        {
            var selected = i == ((vm.PlinkoSelectionOffset % count) + count) % count;
            var source = new Border
            {
                Width = sourceCellSize,
                Height = sourceCellSize,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(selected ? "#334155" : "#1e293b")),
                BorderBrush = new SolidColorBrush(selected ? Color.Parse("#fde047") : Color.Parse("#475569")),
                BorderThickness = new Thickness(selected ? 3 : 1),
                Opacity = vm.IsPlinkoSelecting || i == vm.PlinkoSourceIndex ? 1 : 0.9,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(source, i * binWidth + (binWidth - sourceCellSize) / 2);
            Canvas.SetTop(source, sourceTop);
            canvas.Children.Add(source);
        }

        var pegBrush = new SolidColorBrush(Color.Parse("#94a3b8"));
        var dividerBrush = new SolidColorBrush(Color.Parse("#475569"));

        // Pegs: even rows sit on bin centers, odd rows on bin borders (staggered lattice)
        for (var r = 0; r < rows; r++)
        {
            var y = boardTop + rowGap * (r + 1);
            var isSpringRow = r == 4 || r == rows - 1;
            var onCenters = isSpringRow || r % 2 == 0;
            var pegCount = onCenters ? count : count + 1;

            for (var p = 0; p < pegCount; p++)
            {
                var binX = onCenters ? p + 0.5 : p;
                var x = binX * binWidth;

                // Skip pegs exactly on the outer walls
                if (x < pegRadius * 0.5 || x > width - pegRadius * 0.5)
                    continue;

                if (isSpringRow)
                {
                    var spring = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = pegRadius * 2.4,
                        Height = pegRadius * 1.4,
                        Fill = new SolidColorBrush(Color.Parse("#fbbf24")),
                        Stroke = new SolidColorBrush(Color.Parse("#92400e")),
                        StrokeThickness = 1,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(spring, x - spring.Width / 2);
                    Canvas.SetTop(spring, y - spring.Height / 2);
                    canvas.Children.Add(spring);
                }
                else
                {
                    var peg = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = pegRadius * 2,
                        Height = pegRadius * 2,
                        Fill = pegBrush,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(peg, x - pegRadius);
                    Canvas.SetTop(peg, y - pegRadius);
                    canvas.Children.Add(peg);
                }
            }
        }

        // Visible boundaries match the physical play area.
        var boundaryBrush = new SolidColorBrush(Color.Parse("#475569"));
        var topBoundary = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = width,
            Height = 2,
            Fill = boundaryBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(topBoundary, 0);
        Canvas.SetTop(topBoundary, boardTop);
        canvas.Children.Add(topBoundary);

        var leftBoundary = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = 3,
            Height = pegAreaHeight,
            Fill = boundaryBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(leftBoundary, 0);
        Canvas.SetTop(leftBoundary, boardTop);
        canvas.Children.Add(leftBoundary);

        var rightBoundary = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = 3,
            Height = pegAreaHeight,
            Fill = boundaryBrush,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rightBoundary, width - 3);
        Canvas.SetTop(rightBoundary, boardTop);
        canvas.Children.Add(rightBoundary);

        // Bins
        for (var i = 0; i < count; i++)
        {
            // Each slot has one stable item index, so image and text always come from the same participant.
            var itemIndex = vm.GetPlinkoBinItemIndex(i);
            var left = i * binWidth;

            var bin = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = Math.Max(1, binWidth - 2),
                Height = binHeight,
                Fill = new SolidColorBrush(Palette[i % Palette.Length]),
                Stroke = dividerBrush,
                StrokeThickness = 1,
                RadiusX = 4,
                RadiusY = 4,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(bin, left + 1);
            Canvas.SetTop(bin, boardTop + pegAreaHeight);
            canvas.Children.Add(bin);

            // Divider walls rising above the bins
            if (i > 0)
            {
                var wall = new Avalonia.Controls.Shapes.Rectangle
                {
                    Width = Math.Max(1.0, binWidth * 0.04),
                    Height = binHeight * 0.55,
                    Fill = dividerBrush,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(wall, left - Math.Max(1.0, binWidth * 0.04) / 2);
                Canvas.SetTop(wall, boardTop + pegAreaHeight - binHeight * 0.45);
                canvas.Children.Add(wall);
            }

            if (itemIndex >= 0 && itemIndex < vm.WheelItems.Count)
            {
                var item = vm.WheelItems[itemIndex];
                AddBinLabel(canvas, item, left, boardTop + pegAreaHeight, binWidth, binHeight);
            }
        }

        // Ball
        if (vm.IsBallVisible)
        {
            var ballRadius = Math.Max(3.0, Math.Min(binWidth * 0.18, rowGap * 0.28));
            var ball = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = ballRadius * 2,
                Height = ballRadius * 2,
                Fill = new SolidColorBrush(Color.Parse("#fde047")),
                Stroke = new SolidColorBrush(Color.Parse("#1e293b")),
                StrokeThickness = Math.Max(1, ballRadius * 0.18),
                IsHitTestVisible = false
            };

            // BallY is in path space: peg rows occupy [0, rows/(rows+1)], the bin drop the rest
            var rowsFrac = rows / (double)(rows + 1);
            var lastPegY = boardTop + rowGap * rows;
            var binCenterY = boardTop + pegAreaHeight + binHeight * 0.5;

            var bx = vm.BallX * width;
            var by = vm.BallY <= rowsFrac
                ? boardTop + vm.BallY * pegAreaHeight
                : lastPegY + (vm.BallY - rowsFrac) / (1 - rowsFrac) * (binCenterY - lastPegY);

            Canvas.SetLeft(ball, bx - ballRadius);
            Canvas.SetTop(ball, by - ballRadius);
            canvas.Children.Add(ball);
        }
    }

    */

    /* Plinko label helpers moved to PlinkoBoardRenderer.
    private static void AddSourceLabel(Canvas canvas, Models.WheelItem item, double left, double top, double size)
    {
        if (item.HasImage)
        {
            try
            {
                var image = new Image
                {
                    Source = new Bitmap(item.ImagePath!),
                    Width = size * 0.72,
                    Height = size * 0.72,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(image, left + size * 0.14);
                Canvas.SetTop(image, top + size * 0.14);
                canvas.Children.Add(image);
                return;
            }
            catch
            {
                // fall through to text
            }
        }

        var label = new TextBlock
        {
            Text = item.DisplayName,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = Math.Max(8, size * 0.14),
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Width = size - 4,
            MaxHeight = size - 4,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, left + 2);
        Canvas.SetTop(label, top + 2);
        canvas.Children.Add(label);
    }

    private static void AddBinLabel(
        Canvas canvas, Models.WheelItem item, double left, double top, double binWidth, double binHeight)
    {
        if (item.HasImage)
        {
            try
            {
                var imgSize = Math.Min(binWidth * 0.7, binHeight * 0.6);
                var image = new Image
                {
                    Source = new Bitmap(item.ImagePath!),
                    Width = imgSize,
                    Height = imgSize,
                    Stretch = Stretch.Uniform,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(image, left + (binWidth - imgSize) / 2);
                Canvas.SetTop(image, top + binHeight * 0.1);
                canvas.Children.Add(image);
                return;
            }
            catch
            {
                // fall through to the text label
            }
        }

        var fontSize = Math.Max(8.0, Math.Min(binWidth * 0.2, binHeight * 0.22));
        var label = new TextBlock
        {
            Text = item.DisplayName,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Width = Math.Max(1, binWidth - 4),
            MaxHeight = binHeight,
            ClipToBounds = true,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, left + 2);
        Canvas.SetTop(label, top + binHeight * 0.15);
        canvas.Children.Add(label);
    }

    */

    private static void AddTextLabel(Canvas canvas, string text, Point position, int count, double size)
    {
        var fontSize = size * (count > 12 ? 0.026 : 0.033);
        var labelWidth = size * 0.19;
        var label = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = fontSize,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            Width = labelWidth,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, position.X - labelWidth / 2.0);
        Canvas.SetTop(label, position.Y - fontSize / 2.0);
        canvas.Children.Add(label);
    }

    private void RenderStrip(WheelOfFortuneViewModel vm)
    {
        var canvas = StripCanvas;
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;

        if (width < 10 || height < 10)
            return;

        canvas.Children.Clear();

        var count = vm.WheelItems.Count;
        if (count == 0)
        {
            if (vm.IsStripMode)
                RenderChest(canvas, vm, width, height, Math.Min(height * 0.8, width / 5.5));
            return;
        }

        // While idle (chest closed, no spin) — show only the chest
        if (vm.IsStripMode && !vm.IsSpinning && vm.ChestOpenProgress < 0.01)
        {
            RenderChest(canvas, vm, width, height, Math.Min(height * 0.8, width / 5.5));
            return;
        }

        // Strip fades in as it reveals (0 = invisible, 1 = fully visible)
        var stripOpacity = Math.Clamp(vm.StripReveal, 0.0, 1.0);

        // Cell geometry — square cells that fit the canvas
        var cellSize = Math.Min(height * 0.8, width / 5.5);   // ~5-6 cells visible
        var cellWidth = cellSize;
        var cellHeight = cellSize;
        var cellGap = cellSize * 0.08;
        var cellFull = cellWidth + cellGap;
        var stripY = (height - cellHeight) / 2.0;

        var visibleCells = (int)Math.Ceiling(width / cellFull) + 2;
        // StripOffset is the absolute cell index that should sit under the center marker.
        // Convert to a left-edge scroll offset so that cell `offset` lands at centerX.
        var centerCellOffset = vm.StripOffset - (width / 2.0) / cellFull + 0.5;
        var firstVisibleIndex = (int)Math.Floor(centerCellOffset) - 1;

        var cellBrushFallback = new SolidColorBrush(Palette[0 % Palette.Length]);
        var borderBrush = new SolidColorBrush(Color.Parse("#475569"));

        for (var i = firstVisibleIndex; i < firstVisibleIndex + visibleCells + 2; i++)
        {
            if (i < 0)
                continue;

            var itemIndex = ((i % count) + count) % count;
            var item = vm.WheelItems[itemIndex];

            var x = (i - centerCellOffset) * cellFull;
            if (x > width + cellFull || x < -cellFull)
                continue;

            // Rarity color: precomputed in ViewModel, falls back to neutral palette
            var rarityHex = vm.GetStripRarityColor(i);
            var cellBrush = rarityHex is not null
                ? new SolidColorBrush(Color.Parse(rarityHex))
                : cellBrushFallback;

            var cell = new Border
            {
                Width = cellWidth,
                Height = cellHeight,
                CornerRadius = new CornerRadius(8),
                Background = cellBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                Opacity = stripOpacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(cell, x);
            Canvas.SetTop(cell, stripY);
            canvas.Children.Add(cell);

            // Content: image or text, centered in the cell
            if (item.HasImage)
            {
                try
                {
                    var imgSize = cellWidth * 0.7;
                    var image = new Image
                    {
                        Source = new Bitmap(item.ImagePath!),
                        Width = imgSize,
                        Height = imgSize,
                        Stretch = Stretch.Uniform,
                        Opacity = stripOpacity,
                        IsHitTestVisible = false
                    };
                    Canvas.SetLeft(image, x + (cellWidth - imgSize) / 2);
                    Canvas.SetTop(image, stripY + cellHeight * 0.12);
                    canvas.Children.Add(image);
                    continue;
                }
                catch
                {
                    // fall through to text label
                }
            }

            var fontSize = Math.Max(10.0, cellWidth * 0.16);
            var label = new TextBlock
            {
                Text = item.DisplayName,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = fontSize,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Width = cellWidth - 6,
                MaxHeight = cellHeight * 0.7,
                ClipToBounds = true,
                Opacity = stripOpacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + 3);
            Canvas.SetTop(label, stripY + cellHeight * 0.15);
            canvas.Children.Add(label);
        }

        // Center marker — vertical line + triangle top/bottom, height matches the strip
        var centerX = width / 2.0;
        var markerColor = new SolidColorBrush(Color.Parse("#fde047"));
        var markerOpacity = stripOpacity;

        var line = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = Math.Max(2.0, cellWidth * 0.04),
            Height = cellHeight,
            Fill = markerColor,
            Opacity = markerOpacity,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(line, centerX - line.Width / 2);
        Canvas.SetTop(line, stripY);
        canvas.Children.Add(line);

        // Top triangle pointing down
        var triSize = cellWidth * 0.18;
        var topTri = new Avalonia.Controls.Shapes.Polygon
        {
            Points = new Points
            {
                new(centerX - triSize, stripY - triSize),
                new(centerX + triSize, stripY - triSize),
                new(centerX, stripY)
            },
            Fill = markerColor,
            Opacity = markerOpacity,
            IsHitTestVisible = false
        };
        canvas.Children.Add(topTri);

        // Bottom triangle pointing up
        var bottomTri = new Avalonia.Controls.Shapes.Polygon
        {
            Points = new Points
            {
                new(centerX - triSize, stripY + cellHeight + triSize),
                new(centerX + triSize, stripY + cellHeight + triSize),
                new(centerX, stripY + cellHeight)
            },
            Fill = markerColor,
            Opacity = markerOpacity,
            IsHitTestVisible = false
        };
        canvas.Children.Add(bottomTri);

        // Chest overlay — shown while opening or when closed (before spin)
        RenderChest(canvas, vm, width, height, cellSize);
    }

    private static void RenderChest(Canvas canvas, WheelOfFortuneViewModel vm, double width, double height, double cellSize)
    {
        // Show chest when: idle (before spin), or during opening phase.
        // Once the strip starts revealing, the chest fades out with StripReveal.
        var showChest = vm.IsChestOpening
                        || (vm.IsStripMode && !vm.IsSpinning && vm.ChestOpenProgress < 1.0);
        if (!showChest)
            return;

        var progress = vm.ChestOpenProgress;
        var reveal = vm.StripReveal;

        // Chest dimensions — wider than a cell, fits the strip area
        var chestW = cellSize * 2.2;
        var chestH = cellSize * 1.6;
        var chestX = (width - chestW) / 2.0;
        var chestY = (height - chestH) / 2.0;

        // As the strip reveals, the chest fades and shrinks upward
        var fade = Math.Clamp(1.0 - reveal, 0.0, 1.0);
        var liftY = chestY - reveal * chestH * 0.3;

        var chestBody = new SolidColorBrush(Color.Parse("#78350f"));
        var chestDark = new SolidColorBrush(Color.Parse("#451a03"));
        var chestTrim = new SolidColorBrush(Color.Parse("#fbbf24"));
        var chestLid = new SolidColorBrush(Color.Parse("#92400e"));

        // Chest body (bottom half) — stays in place
        var bodyH = chestH * 0.55;
        var body = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = chestW,
            Height = bodyH,
            Fill = chestBody,
            Stroke = chestDark,
            StrokeThickness = 2,
            RadiusX = 6,
            RadiusY = 6,
            Opacity = fade,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(body, chestX);
        Canvas.SetTop(body, chestY + chestH - bodyH);
        canvas.Children.Add(body);

        // Metal bands on the body
        for (var band = 0; band < 2; band++)
        {
            var bandY = chestY + chestH - bodyH + bodyH * (0.3 + band * 0.4);
            var bandRect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = chestW,
                Height = Math.Max(2.0, chestH * 0.04),
                Fill = chestTrim,
                Opacity = fade,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(bandRect, chestX);
            Canvas.SetTop(bandRect, bandY);
            canvas.Children.Add(bandRect);
        }

        // Lock plate on the front
        var lockW = chestW * 0.12;
        var lockH = chestH * 0.15;
        var lockPlate = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = lockW,
            Height = lockH,
            Fill = chestTrim,
            Stroke = chestDark,
            StrokeThickness = 1,
            RadiusX = 3,
            RadiusY = 3,
            Opacity = fade * (1.0 - Math.Clamp(progress * 2, 0, 1)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(lockPlate, chestX + (chestW - lockW) / 2);
        Canvas.SetTop(lockPlate, chestY + chestH - bodyH * 0.5 - lockH / 2);
        canvas.Children.Add(lockPlate);

        // Lid (top half) — rotates open around its bottom edge (hinge at top of body)
        var lidH = chestH * 0.45;
        var lidHingeY = chestY + chestH - bodyH;
        var lidHingeX = chestX;

        // We simulate rotation by lifting the lid and tilting it back
        // angle goes from 0 (closed) to ~110 degrees (fully open)
        var angleDeg = progress * 110;
        var angleRad = angleDeg * Math.PI / 180.0;

        // Lid position: hinge stays, lid swings backward
        var lidOffsetX = -lidH * 0.5 * Math.Sin(angleRad);
        var lidOffsetY = -lidH * 0.5 * (1 - Math.Cos(angleRad)) - liftY + chestY;

        var lid = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = chestW,
            Height = lidH,
            Fill = chestLid,
            Stroke = chestDark,
            StrokeThickness = 2,
            RadiusX = 6,
            RadiusY = 6,
            Opacity = fade,
            IsHitTestVisible = false
        };

        // Use RotateTransform around the bottom-center of the lid
        var hingeRelX = chestW / 2;
        var hingeRelY = lidH;
        lid.RenderTransform = new RotateTransform(angleDeg, hingeRelX, hingeRelY);
        Canvas.SetLeft(lid, lidHingeX + lidOffsetX);
        Canvas.SetTop(lid, lidHingeY - lidH + lidOffsetY);
        canvas.Children.Add(lid);

        // Trim band on the lid
        var lidTrim = new Avalonia.Controls.Shapes.Rectangle
        {
            Width = chestW,
            Height = Math.Max(2.0, chestH * 0.04),
            Fill = chestTrim,
            Opacity = fade,
            IsHitTestVisible = false
        };
        lidTrim.RenderTransform = new RotateTransform(angleDeg, hingeRelX, hingeRelY);
        Canvas.SetLeft(lidTrim, lidHingeX + lidOffsetX);
        Canvas.SetTop(lidTrim, lidHingeY - lidH + lidH * 0.7 + lidOffsetY);
        canvas.Children.Add(lidTrim);

        // "Open" hint text when idle
        if (!vm.IsSpinning && progress < 0.01)
        {
            var hint = new TextBlock
            {
                Text = "🎁 Нажми «Открыть кейс!»",
                Foreground = new SolidColorBrush(Color.Parse("#fbbf24")),
                FontSize = Math.Max(12, cellSize * 0.14),
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                Width = chestW * 1.6,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(hint, chestX + chestW / 2 - hint.Width / 2);
            Canvas.SetTop(hint, chestY + chestH + cellSize * 0.1);
            canvas.Children.Add(hint);
        }
    }

    private async void OnResultChanged(string? result)
    {
        if (string.IsNullOrEmpty(result))
        {
            HideResultOverlay();
            return;
        }

        // Strip mode uses its own CS:GO-style popup, skip the text overlay
        if (DataContext is WheelOfFortuneViewModel vm && vm.IsStripMode)
            return;

        ShowResultOverlay();

        // Keep result visible for 10 seconds, then hide
        await Task.Delay(TimeSpan.FromSeconds(10));
        if (DataContext is WheelOfFortuneViewModel vm2 && vm2.Result == result)
        {
            vm2.Result = null;
        }
    }

    private void ShowResultOverlay()
    {
        DimOverlay.Opacity = 1;
        DimOverlay.IsVisible = true;
        ResultOverlay.Opacity = 1;
        ResultOverlay.IsVisible = true;
    }

    private void HideResultOverlay()
    {
        DimOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
    }

    private DispatcherTimer? _clearResetTimer;
    private bool _clearPending;

    private void OnClearWheelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not WheelOfFortuneViewModel vm)
            return;

        if (_clearPending)
        {
            ResetClearPending();
            vm.ClearWheelCommand.Execute(null);
            return;
        }

        _clearPending = true;
        ClearWheelBtn.Content = "✓?";
        ClearWheelBtn.Foreground = new SolidColorBrush(Color.Parse("#ef4444"));
        ClearWheelBtn.FontWeight = FontWeight.Bold;

        _clearResetTimer?.Stop();
        _clearResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _clearResetTimer.Tick += (_, _) => ResetClearPending();
        _clearResetTimer.Start();
    }

    private void ResetClearPending()
    {
        if (!_clearPending)
            return;

        _clearPending = false;
        ClearWheelBtn.Content = "🗑";
        ClearWheelBtn.Foreground = new SolidColorBrush(Color.Parse("#94a3b8"));
        ClearWheelBtn.FontWeight = FontWeight.Normal;
        _clearResetTimer?.Stop();
        _clearResetTimer = null;
    }

    private void OnOpenTeamSettingsPopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsTeamSettingsPopupOpen = true;
    }

    private void OnCloseTeamSettingsPopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsTeamSettingsPopupOpen = false;
    }

    private void OnOpenAddItemPopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
        {
            vm.NewItemPopupText = string.Empty;
            vm.NewItemPopupImagePath = null;
            vm.IsAddItemPopupOpen = true;
        }
    }

    private void OnAddItemKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WheelOfFortuneViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.NewItemPopupText) || !string.IsNullOrEmpty(vm.NewItemPopupImagePath))
                vm.ConfirmAddItemCommand.Execute(null);
        }
    }

    private void OnCancelAddItemPopup(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is WheelOfFortuneViewModel vm)
            vm.IsAddItemPopupOpen = false;
    }

    private void OnOpenListManager(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not WheelOfFortuneViewModel vm)
            return;

        if (_managerWindow is { IsVisible: true })
        {
            _managerWindow.Activate();
            return;
        }

        _managerWindow = new WheelListManagerWindow
        {
            DataContext = vm,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        _managerWindow.Closed += (_, _) => _managerWindow = null;
        _managerWindow.Show(TopLevel.GetTopLevel(this) as Window);
    }
}
