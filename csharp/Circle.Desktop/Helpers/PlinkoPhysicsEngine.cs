namespace Circle.Desktop.Helpers;

/// <summary>Deterministic normalized-coordinate Plinko simulation.</summary>
public sealed class PlinkoPhysicsEngine
{
    public const int Rows = 9;
    public const double TimeStep = 0.004;
    public const double PegCollisionRadius = 0.29;
    public const double SpringCollisionRadius = 0.34;
    public const double BucketDepth = 0.3;
    public const double PlayfieldBottom = 1.0 + BucketDepth;

    public sealed record SimulationResult(
        IReadOnlyList<(double X, double Y)> Path,
        IReadOnlyList<double> SegmentDurations,
        int ResultSlot,
        PlinkoScene Scene)
    {
        public IReadOnlyList<PlinkoGameObject> Objects => Scene.Objects;
    }

    public SimulationResult Simulate(int binCount, int sourceIndex, Random random)
    {
        var n = Math.Max(1, binCount);
        var scene = PlinkoSceneFactory.Create(n);
        var ball = scene.Ball;
        var x = sourceIndex + 0.5;
        var y = 0.0;
        ball.X = x;
        ball.Y = y;
        var vx = (random.NextDouble() - 0.5) * 0.5;
        var vy = 0.15;
        const double maxHorizontalSpeed = 3.0;
        const double maxVerticalSpeed = 8.0;
        var springCooldown = new Dictionary<PlinkoGameObject, double>();
        var path = new List<(double X, double Y)> { (x, y) };
        var durations = new List<double>();

        for (var step = 0; step < 3000 && y < PlayfieldBottom; step++)
        {
            var previousX = x;
            var previousY = y;
            vy += 1.8 * TimeStep;
            vy = Math.Clamp(vy, -maxVerticalSpeed / n, maxVerticalSpeed / n);
            vx = Math.Clamp(vx, -maxHorizontalSpeed, maxHorizontalSpeed);
            x += vx * TimeStep;
            y += vy * TimeStep;

            var velocityY = vy * n;
            foreach (var key in springCooldown.Keys.ToList())
                springCooldown[key] = Math.Max(0, springCooldown[key] - TimeStep);

            foreach (var obstacle in scene.Objects)
            {
                if (obstacle.Kind is Kind.Peg or Kind.Spring)
                {
                    // Swept circle test: check the whole movement segment, not only its endpoint.
                    var moveX = x - previousX;
                    var moveY = (y - previousY) * n;
                    var moveLengthSquared = moveX * moveX + moveY * moveY;
                    var segmentT = moveLengthSquared < 0.000001
                        ? 1.0
                        : Math.Clamp(((obstacle.X - previousX) * moveX +
                                      (obstacle.Y * n - previousY * n) * moveY) / moveLengthSquared, 0.0, 1.0);
                    var closestX = previousX + moveX * segmentT;
                    var closestY = previousY * n + moveY * segmentT;
                    var dx = closestX - obstacle.X;
                    var dy = closestY - obstacle.Y * n;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance >= obstacle.Radius)
                        continue;

                    var normalX = dx / Math.Max(distance, 0.001);
                    var normalY = dy / Math.Max(distance, 0.001);
                    var velocityAlongNormal = vx * normalX + velocityY * normalY;
                    if (velocityAlongNormal >= 0)
                        continue;

                    if (obstacle.Kind == Kind.Spring &&
                        springCooldown.TryGetValue(obstacle, out var cooldown) && cooldown > 0)
                        continue;

                    x = obstacle.X + normalX * obstacle.Radius;
                    y = obstacle.Y + normalY * obstacle.Radius / n;

                    if (obstacle.Kind == Kind.Spring)
                    {
                        velocityY = -2.5 * n - random.NextDouble() * 0.65 * n;
                        vx += (random.NextDouble() - 0.5) * 1.25;
                        springCooldown[obstacle] = 0.35;
                        break;
                    }

                    const double restitution = 0.58;
                    vx -= (1 + restitution) * velocityAlongNormal * normalX;
                    velocityY -= (1 + restitution) * velocityAlongNormal * normalY;
                    // A hit from above must leave the peg with an upward component.
                    if (normalY < -0.15)
                        velocityY = Math.Min(velocityY, -0.45);
                    vx += (random.NextDouble() - 0.5) * 0.35;
                    break;
                }
                else if (obstacle.Kind == Kind.Wall)
                {
                    var halfWidth = obstacle.Width / 2.0 + ball.Radius;
                    if (x < obstacle.X + halfWidth && x > obstacle.X - halfWidth &&
                        y >= obstacle.Y - obstacle.Height / 2 && y <= obstacle.Y + obstacle.Height / 2)
                    {
                        var left = obstacle.X < n / 2.0;
                        x = obstacle.X + (left ? halfWidth : -halfWidth);
                        vx = left ? Math.Abs(vx) * 0.9 + 0.12 : -Math.Abs(vx) * 0.9 - 0.12;
                    }
                }
                else if (obstacle.Kind == Kind.Ceiling && y < obstacle.Y && vy < 0)
                {
                    y = obstacle.Y;
                    vy = Math.Abs(vy) * 0.55 + 0.12;
                    velocityY = vy * n;
                }
                else if (obstacle.Kind == Kind.Separator &&
                         y >= 1.0 && y <= PlayfieldBottom &&
                         Math.Abs(x - obstacle.X) < obstacle.Width / 2 + ball.Radius)
                {
                    var left = x < obstacle.X;
                    x = obstacle.X + (left ? -1 : 1) * (obstacle.Width / 2 + ball.Radius);
                    vx = (left ? -1 : 1) * Math.Max(Math.Abs(vx) * 0.65, 0.35);
                }
            }

            vx = Math.Clamp(vx, -maxHorizontalSpeed, maxHorizontalSpeed);
            velocityY = Math.Clamp(velocityY, -maxVerticalSpeed, maxVerticalSpeed);
            vy = velocityY / n;
            vx *= 0.997;
            x = Math.Clamp(x, 0.22, n - 0.22);
            y = Math.Clamp(y, 0.0, PlayfieldBottom);
            ball.X = x;
            ball.Y = y;
            path.Add((x, y));
            durations.Add(TimeStep * 1000);

            if (Math.Abs(x - previousX) < 0.0001 && Math.Abs(y - previousY) < 0.0001)
                break;
        }

        var resultSlot = Math.Clamp((int)Math.Floor(x), 0, n - 1);
        ball.X = resultSlot + 0.5;
        ball.Y = PlayfieldBottom;
        path.Add((ball.X, ball.Y));
        durations.Add(400);
        return new SimulationResult(path, durations, resultSlot, scene);
    }
}
