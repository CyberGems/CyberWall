using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace CyberWall.UI.Controls;

/// <summary>
/// Caches frozen spline geometries (curved area fill and stroked line) until samples,
/// scale or drawing bounds change. Generates organic GlassWire-style Bézier curves with baseline protection.
/// </summary>
internal sealed class TrafficSeriesDrawing
{
    private float[]? _data;
    private Rect _bounds;
    private double _maximum;
    private int _capacity;
    private IReadOnlyList<float>? _prefix;
    private StreamGeometry? _fill;
    private StreamGeometry? _line;

    internal int BuildCount { get; private set; }

    internal void Reset()
    {
        _data = null;
        _prefix = null;
        _fill = null;
        _line = null;
    }

    internal void Draw(
        DrawingContext context,
        float[]? data,
        Rect bounds,
        double maximum,
        int capacity,
        IReadOnlyList<float> prefix,
        Brush? fill,
        Pen? pen)
    {
        if (data is not { Length: > 0 } || maximum <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return;

        if (!ReferenceEquals(_data, data) ||
            _bounds != bounds ||
            Math.Abs(_maximum - maximum) > 0.001 ||
            _capacity != capacity ||
            !ReferenceEquals(_prefix, prefix))
        {
            _data = data;
            _bounds = bounds;
            _maximum = maximum;
            _capacity = capacity;
            _prefix = prefix;
            Build(data, bounds, maximum, capacity, prefix);
        }

        if (fill is not null && _fill is not null && (data.Length + prefix.Count) >= 2)
        {
            context.DrawGeometry(fill, null, _fill);
        }
        if (pen is not null && _line is not null)
        {
            context.DrawGeometry(null, pen, _line);
        }
    }

    private void Build(float[] data, Rect bounds, double maximum, int capacity, IReadOnlyList<float> prefix)
    {
        var count = data.Length + prefix.Count;
        if (count < 2) return;

        var step = bounds.Width / Math.Max(1, capacity - 1);
        var startX = bounds.Right - (count - 1) * step;
        var baselineY = bounds.Bottom - 1.0;
        var usableHeight = Math.Max(1.0, bounds.Height - 6.0);

        var points = new Point[count];
        for (var i = 0; i < count; i++)
        {
            var val = i < prefix.Count ? prefix[i] : data[i - prefix.Count];
            var norm = Math.Clamp(val / maximum, 0.0, 1.0);
            var x = startX + i * step;
            var y = baselineY - (norm * usableHeight);
            points[i] = new Point(x, y);
        }

        // Calculate smooth cubic Bezier control points with Catmull-Rom tension
        // and baseline protection to prevent dipping below zero.
        var c1 = new Point[count - 1];
        var c2 = new Point[count - 1];

        // Smooth tension (0.42 provides soft organic curves like GlassWire)
        const double tension = 0.42;
        var dxThird = step / 3.0;

        for (var i = 0; i < count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < count ? points[i + 2] : p2;

            double t1Y;
            if (i == 0)
            {
                t1Y = (p2.Y - p1.Y) * (1.0 - tension);
            }
            else
            {
                t1Y = (p2.Y - p0.Y) * 0.5 * (1.0 - tension);
            }

            double t2Y;
            if (i == count - 2)
            {
                t2Y = (p2.Y - p1.Y) * (1.0 - tension);
            }
            else
            {
                t2Y = (p3.Y - p1.Y) * 0.5 * (1.0 - tension);
            }

            var ctrl1X = p1.X + dxThird;
            var ctrl1Y = p1.Y + (t1Y / 3.0);

            var ctrl2X = p2.X - dxThird;
            var ctrl2Y = p2.Y - (t2Y / 3.0);

            // Baseline clamping: network speeds cannot be negative.
            // When points are at the baseline, control points must stay at the baseline.
            if (Math.Abs(p1.Y - baselineY) < 1.0 && Math.Abs(p2.Y - baselineY) < 1.0)
            {
                ctrl1Y = baselineY;
                ctrl2Y = baselineY;
            }
            else
            {
                if (Math.Abs(p1.Y - baselineY) < 1.0) ctrl1Y = baselineY;
                if (Math.Abs(p2.Y - baselineY) < 1.0) ctrl2Y = baselineY;

                ctrl1Y = Math.Clamp(ctrl1Y, bounds.Top, baselineY);
                ctrl2Y = Math.Clamp(ctrl2Y, bounds.Top, baselineY);
            }

            c1[i] = new Point(ctrl1X, ctrl1Y);
            c2[i] = new Point(ctrl2X, ctrl2Y);
        }

        _fill = new StreamGeometry();
        _line = new StreamGeometry();

        using (var fctx = _fill.Open())
        using (var lctx = _line.Open())
        {
            // Build Area Fill figure
            fctx.BeginFigure(new Point(startX, baselineY), isFilled: true, isClosed: true);
            fctx.LineTo(points[0], isStroked: false, isSmoothJoin: true);

            // Build Stroke Line figure
            lctx.BeginFigure(points[0], isFilled: false, isClosed: false);

            for (var i = 0; i < count - 1; i++)
            {
                fctx.BezierTo(c1[i], c2[i], points[i + 1], isStroked: false, isSmoothJoin: true);
                lctx.BezierTo(c1[i], c2[i], points[i + 1], isStroked: true, isSmoothJoin: true);
            }

            fctx.LineTo(new Point(points[^1].X, baselineY), isStroked: false, isSmoothJoin: true);
            fctx.LineTo(new Point(startX, baselineY), isStroked: false, isSmoothJoin: true);
        }

        _fill.Freeze();
        _line.Freeze();
        BuildCount++;
    }
}
