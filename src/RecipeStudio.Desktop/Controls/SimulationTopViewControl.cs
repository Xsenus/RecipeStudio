using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Controls;

public sealed class SimulationTopViewControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<SimulationTopViewControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SimulationTopViewControl, double>(nameof(Progress));

    private Rect _worldBounds;
    private double _scale;
    private const double Pad = 20;

    public IList<RecipePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    static SimulationTopViewControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationTopViewControl>((c, _) => c.InvalidateVisual());
        ProgressProperty.Changed.AddClassHandler<SimulationTopViewControl>((c, _) => c.InvalidateVisual());
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(3, 12, 34)), Bounds);

        var points = Points?.ToList() ?? new List<RecipePoint>();
        if (points.Count == 0)
            return;

        var world = points.Select(p => new Point(p.Xr0 + p.DX, p.Yx0 + p.DY)).ToList();
        Fit(world);

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(76, 180, 255)), 1.6);
        DrawPolyline(context, world, pen);

        foreach (var wp in world)
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(34, 197, 94)), null, WorldToScreen(wp), 2.5, 2.5);

        var tool = Interpolate(world, Progress);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(239, 68, 68)), null, WorldToScreen(tool), 5, 5);
    }

    private void Fit(IList<Point> points)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        _worldBounds = new Rect(new Point(minX, minY), new Point(maxX, maxY)).Normalize();
        if (_worldBounds.Width < 1) _worldBounds = _worldBounds.WithWidth(1);
        if (_worldBounds.Height < 1) _worldBounds = _worldBounds.WithHeight(1);

        var sx = Math.Max(1, (Bounds.Width - Pad * 2) / _worldBounds.Width);
        var sy = Math.Max(1, (Bounds.Height - Pad * 2) / _worldBounds.Height);
        _scale = Math.Min(sx, sy);
    }

    private Point WorldToScreen(Point p)
    {
        var x = Pad + (p.X - _worldBounds.Left) * _scale;
        var y = Bounds.Height - Pad - (p.Y - _worldBounds.Top) * _scale;
        return new Point(x, y);
    }

    private void DrawPolyline(DrawingContext ctx, IList<Point> pts, Pen pen)
    {
        if (pts.Count < 2) return;
        var g = new StreamGeometry();
        using var gc = g.Open();
        gc.BeginFigure(WorldToScreen(pts[0]), false);
        for (var i = 1; i < pts.Count; i++) gc.LineTo(WorldToScreen(pts[i]));
        gc.EndFigure(false);
        ctx.DrawGeometry(null, pen, g);
    }

    private static Point Interpolate(IList<Point> pts, double progress)
    {
        if (pts.Count == 0) return default;
        if (pts.Count == 1) return pts[0];

        progress = Math.Clamp(progress, 0, 1);
        var seg = new double[pts.Count - 1];
        double total = 0;
        for (var i = 0; i < seg.Length; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            var dy = pts[i + 1].Y - pts[i].Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            seg[i] = d;
            total += d;
        }

        var target = total * progress;
        double acc = 0;
        for (var i = 0; i < seg.Length; i++)
        {
            var next = acc + seg[i];
            if (target <= next || i == seg.Length - 1)
            {
                var t = seg[i] <= 1e-6 ? 1 : (target - acc) / seg[i];
                return new Point(
                    pts[i].X + (pts[i + 1].X - pts[i].X) * t,
                    pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t);
            }
            acc = next;
        }

        return pts[^1];
    }
}
