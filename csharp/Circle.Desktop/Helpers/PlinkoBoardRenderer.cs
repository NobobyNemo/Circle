using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Circle.Desktop.Models;
using Circle.Desktop.ViewModels;

namespace Circle.Desktop.Helpers;

/// <summary>Draws the Plinko scene; geometry comes exclusively from its GameObjects.</summary>
public sealed class PlinkoBoardRenderer
{
    private static readonly Color[] Palette = [Color.Parse("#334155"), Color.Parse("#1e293b")];
    private static readonly Bitmap BallSprite = new(AssetLoader.Open(
        new Uri("avares://Circle.Desktop/Assets/WheelOfFortune/Plinko/ball.png")));
    private readonly PlinkoSpriteFactory _sprites = new();

    public void Render(Canvas canvas, WheelOfFortuneViewModel vm)
    {
        var width = canvas.Bounds.Width;
        var height = canvas.Bounds.Height;
        if (width < 10 || height < 10) return;
        canvas.Children.Clear();
        vm.EnsurePlinkoScene();
        var scene = vm.PlinkoScene;
        var bins = scene.Objects.Where(o => o.Kind == Kind.Bin).OrderBy(o => o.Index).ToList();
        if (bins.Count == 0) return;
        var count = bins.Count;
        var binWidth = width / count;
        var sourceCellSize = Math.Min(binWidth * 0.86, height * 0.12);
        var sourceGap = Math.Max(4.0, height * 0.018);
        var sourceTop = Math.Max(4.0, (height * 0.16 - sourceCellSize) / 2.0);
        var boardTop = sourceTop + sourceCellSize + sourceGap;
        // Bottom options are compact square cells, leaving more room for the board.
        var binHeight = Math.Min(binWidth * 0.78, height * 0.18);
        var pegAreaHeight = height - boardTop - binHeight;

        foreach (var bin in bins)
        {
            var i = bin.Index;
            var left = (bin.X - 0.5) * binWidth;
            var isSelectable = count <= 2 || (i > 0 && i < count - 1);
            if (!isSelectable)
                continue;

            var selected = i == ((vm.PlinkoSelectionOffset % count) + count) % count;
            var source = new Border { Width = sourceCellSize, Height = sourceCellSize, CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.Parse(selected ? "#334155" : "#1e293b")),
                BorderBrush = new SolidColorBrush(Color.Parse(selected ? "#fde047" : "#475569")),
                BorderThickness = new Thickness(selected ? 3 : 1), Opacity = vm.IsPlinkoSelecting || i == vm.PlinkoSourceIndex ? 1 : 0.9,
                IsHitTestVisible = false };
            Canvas.SetLeft(source, left + (binWidth - sourceCellSize) / 2); Canvas.SetTop(source, sourceTop); canvas.Children.Add(source);
        }

        var dividerBrush = new SolidColorBrush(Color.Parse("#475569"));
        foreach (var obj in scene.Objects)
        {
            var x = obj.X * binWidth;
            var y = boardTop + obj.Y * pegAreaHeight;
            switch (obj.Kind)
            {
                case Kind.Peg:
                    var pegVisualRadius = obj.Radius * binWidth;
                    AddControl(canvas, _sprites.CreatePeg(pegVisualRadius * 2),
                        x - pegVisualRadius, y - pegVisualRadius);
                    break;
                case Kind.Spring:
                    var springVisualRadius = obj.Radius * binWidth;
                    var springWidth = springVisualRadius * 2.4;
                    var springHeight = springVisualRadius * 1.4;
                    AddControl(canvas, _sprites.CreateSpring(springWidth, springHeight),
                        x - springWidth / 2, y - springHeight / 2);
                    break;
                case Kind.Wall:
                    AddRectangle(canvas, obj.Width * binWidth, pegAreaHeight, dividerBrush, (obj.X - obj.Width / 2) * binWidth, boardTop);
                    break;
                case Kind.Ceiling:
                    AddRectangle(canvas, width, 2, dividerBrush, 0, boardTop);
                    break;
            }
        }

        foreach (var bin in bins)
        {
            var left = (bin.X - 0.5) * binWidth + (binWidth - binHeight) / 2.0;
            var top = boardTop + pegAreaHeight;
            AddBucket(canvas, binHeight, binHeight,
                new SolidColorBrush(Palette[bin.Index % Palette.Length]), dividerBrush, left, top);
            var itemIndex = vm.GetPlinkoBinItemIndex(bin.Index);
            if (itemIndex >= 0 && itemIndex < vm.WheelItems.Count)
                AddBinLabel(canvas, vm.WheelItems[itemIndex], left, top, binHeight, binHeight);
        }

        if (!vm.IsBallVisible) return;
        var ballRadius = Math.Max(3.0, scene.Ball.Radius * binWidth);
        var ball = new Image
        {
            Source = BallSprite,
            Width = ballRadius * 2,
            Height = ballRadius * 2,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        var bucketTop = boardTop + pegAreaHeight;
        var bx = scene.Ball.X * binWidth;
        var by = scene.Ball.Y <= 1.0
            ? boardTop + scene.Ball.Y * pegAreaHeight
            : bucketTop + Math.Clamp((scene.Ball.Y - 1.0) / PlinkoPhysicsEngine.BucketDepth, 0.0, 1.0) * binHeight;
        Canvas.SetLeft(ball, bx - ballRadius); Canvas.SetTop(ball, by - ballRadius); canvas.Children.Add(ball);
    }

    private static void AddBucket(Canvas canvas, double width, double height, IBrush background,
        IBrush wallBrush, double left, double top)
    {
        // Open-top bucket: two independent side walls and a closed bottom.
        var bucket = new Border
        {
            Width = width,
            Height = height,
            Background = background,
            BorderBrush = wallBrush,
            BorderThickness = new Thickness(3, 0, 3, 5),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(bucket, left);
        Canvas.SetTop(bucket, top);
        canvas.Children.Add(bucket);
    }

    private static void AddControl(Canvas canvas, Control control, double left, double top)
    {
        control.IsHitTestVisible = false;
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
        canvas.Children.Add(control);
    }

    private static void AddRectangle(Canvas canvas, double width, double height, IBrush fill, double left, double top)
    {
        var shape = new Rectangle { Width = width, Height = height, Fill = fill, IsHitTestVisible = false };
        Canvas.SetLeft(shape, left); Canvas.SetTop(shape, top); canvas.Children.Add(shape);
    }

    private static void AddBinLabel(Canvas canvas, WheelItem item, double left, double top, double binWidth, double binHeight)
    {
        if (item.HasImage)
        {
            try
            {
                var image = new Image
                {
                    Source = new Bitmap(item.ImagePath!),
                    Width = binWidth,
                    Height = binHeight,
                    Stretch = Stretch.UniformToFill,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(image, left);
                Canvas.SetTop(image, top);
                canvas.Children.Add(image);
                return;
            }
            catch { }
        }
        var label = new TextBlock { Text = item.DisplayName, Foreground = new SolidColorBrush(Colors.White), FontSize = Math.Max(8.0, Math.Min(binWidth * 0.2, binHeight * 0.22)), FontWeight = FontWeight.Bold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Width = Math.Max(1, binWidth - 4), MaxHeight = binHeight, ClipToBounds = true, IsHitTestVisible = false };
        Canvas.SetLeft(label, left + 2); Canvas.SetTop(label, top + binHeight * 0.15); canvas.Children.Add(label);
    }
}
