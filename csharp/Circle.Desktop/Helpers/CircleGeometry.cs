using Avalonia;
using Avalonia.Media;

namespace Circle.Desktop.Helpers;

/// <summary>
/// Geometry helpers for drawing the Circle of Fifths.
/// </summary>
public static class CircleGeometry
{
    public const double Size = 500;
    public const double CenterX = 250;
    public const double CenterY = 250;
    public const double OuterRadius = 220;
    public const double MajorInnerRadius = 155;
    public const double MinorOuterRadius = 145;
    public const double MinorInnerRadius = 80;
    public const double OuterLabelRadius = 185;
    public const double InnerLabelRadius = 110;

    public static double SegmentAngle(int segmentCount) => 360.0 / segmentCount;

    public static Point PolarToCartesian(double cx, double cy, double radius, double angleDegrees)
    {
        var radians = Math.PI / 180.0 * angleDegrees;
        return new Point(
            cx + radius * Math.Cos(radians),
            cy + radius * Math.Sin(radians));
    }

    public static Geometry DescribeAnnularSector(
        double cx,
        double cy,
        double innerRadius,
        double outerRadius,
        double startAngle,
        double endAngle)
    {
        var startOuter = PolarToCartesian(cx, cy, outerRadius, startAngle);
        var endOuter = PolarToCartesian(cx, cy, outerRadius, endAngle);
        var startInner = PolarToCartesian(cx, cy, innerRadius, endAngle);
        var endInner = PolarToCartesian(cx, cy, innerRadius, startAngle);

        var largeArc = endAngle - startAngle > 180 ? 1 : 0;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(startOuter, true);
            context.ArcTo(endOuter, new Size(outerRadius, outerRadius), 0, largeArc == 1, SweepDirection.Clockwise);
            context.LineTo(startInner);
            context.ArcTo(endInner, new Size(innerRadius, innerRadius), 0, largeArc == 1, SweepDirection.CounterClockwise);
            context.LineTo(startOuter);
        }

        return geometry;
    }
}
