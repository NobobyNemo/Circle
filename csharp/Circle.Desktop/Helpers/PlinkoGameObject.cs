namespace Circle.Desktop.Helpers;

public enum Kind
{
    Ball,
    Peg,
    Spring,
    Wall,
    Ceiling,
    Bin,
    Separator
}

/// <summary>A mutable normalized-coordinate entity in the Plinko scene.</summary>
public sealed class PlinkoGameObject
{
    public Kind Kind { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public int Index { get; init; } = -1;
    public bool IsStatic { get; init; }
    public bool IsDynamic { get; init; }

    public PlinkoGameObject(Kind kind, double x, double y, double radius = 0,
        double width = 0, double height = 0, int index = -1,
        bool isStatic = false, bool isDynamic = false)
    {
        Kind = kind;
        X = x;
        Y = y;
        Radius = radius;
        Width = width;
        Height = height;
        Index = index;
        IsStatic = isStatic;
        IsDynamic = isDynamic;
    }
}

public sealed class PlinkoScene
{
    public IReadOnlyList<PlinkoGameObject> Objects { get; }
    public PlinkoGameObject Ball { get; }

    public PlinkoScene(IReadOnlyList<PlinkoGameObject> objects, PlinkoGameObject ball)
    {
        Objects = objects;
        Ball = ball;
    }
}

public static class PlinkoSceneFactory
{
    public static PlinkoScene Create(int binCount)
    {
        var n = Math.Max(1, binCount);
        var objects = new List<PlinkoGameObject>();
        var springRows = new[] { 4, PlinkoPhysicsEngine.Rows - 1 };

        for (var r = 0; r < PlinkoPhysicsEngine.Rows; r++)
        {
            var springRow = springRows.Contains(r);
            var onCenters = springRow || r % 2 == 0;
            var count = onCenters ? n : n + 1;
            var y = (r + 1) / (PlinkoPhysicsEngine.Rows + 1.0);
            for (var p = 0; p < count; p++)
            {
                var x = onCenters ? p + 0.5 : p;
                if (x <= 0 || x >= n)
                    continue;
                objects.Add(new PlinkoGameObject(springRow ? Kind.Spring : Kind.Peg, x, y,
                    springRow ? PlinkoPhysicsEngine.SpringCollisionRadius : PlinkoPhysicsEngine.PegCollisionRadius,
                    isStatic: true));
            }
        }

        // Narrow visual/physical side walls, inset so the ball can collide with them.
        const double wallWidth = 0.08;
        objects.Add(new PlinkoGameObject(Kind.Wall, wallWidth / 2, 0.512,
            width: wallWidth, height: 1.024, isStatic: true));
        objects.Add(new PlinkoGameObject(Kind.Wall, n - wallWidth / 2, 0.512,
            width: wallWidth, height: 1.024, isStatic: true));
        objects.Add(new PlinkoGameObject(Kind.Ceiling, n / 2.0, 0.025, width: n, height: 0.05, isStatic: true));

        for (var i = 0; i < n; i++)
            objects.Add(new PlinkoGameObject(Kind.Bin, i + 0.5, PlinkoPhysicsEngine.PlayfieldBottom,
                width: 1, height: PlinkoPhysicsEngine.BucketDepth, index: i, isStatic: true));
        for (var i = 1; i < n; i++)
            objects.Add(new PlinkoGameObject(Kind.Separator, i, 1.0 + PlinkoPhysicsEngine.BucketDepth / 2,
                width: 0.04, height: PlinkoPhysicsEngine.BucketDepth, index: i, isStatic: true));

        var ball = new PlinkoGameObject(Kind.Ball, n / 2.0, 0, radius: 0.18, isDynamic: true);
        objects.Add(ball);
        return new PlinkoScene(objects, ball);
    }
}
