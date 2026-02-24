using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Controls;

public sealed class SimulationIsometricControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<SimulationIsometricControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SimulationIsometricControl, double>(nameof(Progress));

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

    static SimulationIsometricControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationIsometricControl>((c, _) => c.InvalidateVisual());
        ProgressProperty.Changed.AddClassHandler<SimulationIsometricControl>((c, _) => c.InvalidateVisual());
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(10, 23, 45)), Bounds);

        var points = Points?.ToList() ?? new List<RecipePoint>();
        if (points.Count == 0)
            return;

        var path = points.Select(p => new Point3(p.Xr0 + p.DX, p.Yx0 + p.DY, p.Zr0 + p.DZ)).ToList();
        var projected = path.Select(Project).ToList();

        var minX = projected.Min(p => p.X);
        var maxX = projected.Max(p => p.X);
        var minY = projected.Min(p => p.Y);
        var maxY = projected.Max(p => p.Y);
        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxY - minY);
        var scale = Math.Min((Bounds.Width - 30) / w, (Bounds.Height - 30) / h);

        Point ToScreen(Point p)
            => new(15 + (p.X - minX) * scale, 15 + (p.Y - minY) * scale);

        var pen = new Pen(new SolidColorBrush(Color.FromRgb(250, 204, 21)), 1.8);
        var geom = new StreamGeometry();
        using (var gc = geom.Open())
        {
            gc.BeginFigure(ToScreen(projected[0]), false);
            for (var i = 1; i < projected.Count; i++)
                gc.LineTo(ToScreen(projected[i]));
            gc.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geom);

        foreach (var p in projected)
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(147, 197, 253)), null, ToScreen(p), 2.5, 2.5);

        var tool = Interpolate(path, Progress);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(248, 113, 113)), null, ToScreen(Project(tool)), 5, 5);
    }

    private static Point Project(Point3 p)
    {
        var x = p.X - p.Y * 0.45;
        var y = -p.Z + (p.X + p.Y) * 0.18;
        return new Point(x, y);
    }

    private static Point3 Interpolate(IList<Point3> pts, double progress)
    {
        if (pts.Count == 0) return default;
        if (pts.Count == 1) return pts[0];

        progress = Math.Clamp(progress, 0, 1);
        var seg = new double[pts.Count - 1];
        var total = 0.0;
        for (var i = 0; i < seg.Length; i++)
        {
            var dx = pts[i + 1].X - pts[i].X;
            var dy = pts[i + 1].Y - pts[i].Y;
            var dz = pts[i + 1].Z - pts[i].Z;
            seg[i] = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            total += seg[i];
        }

        var target = total * progress;
        double acc = 0;
        for (var i = 0; i < seg.Length; i++)
        {
            var next = acc + seg[i];
            if (target <= next || i == seg.Length - 1)
            {
                var t = seg[i] <= 1e-6 ? 1 : (target - acc) / seg[i];
                return new Point3(
                    pts[i].X + (pts[i + 1].X - pts[i].X) * t,
                    pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t,
                    pts[i].Z + (pts[i + 1].Z - pts[i].Z) * t);
            }
            acc = next;
        }

        return pts[^1];
    }

    private readonly record struct Point3(double X, double Y, double Z);
}
