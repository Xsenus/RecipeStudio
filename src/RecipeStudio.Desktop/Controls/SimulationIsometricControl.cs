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

        var state = Interpolate(path, Progress);

        // passed trajectory highlight
        var passed3d = path.Take(state.SegmentIndex + 1).ToList();
        passed3d.Add(state.Position);
        var passed = passed3d.Select(Project).ToList();
        var passedGeom = new StreamGeometry();
        using (var pg = passedGeom.Open())
        {
            pg.BeginFigure(ToScreen(passed[0]), false);
            for (var i = 1; i < passed.Count; i++)
                pg.LineTo(ToScreen(passed[i]));
            pg.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(74, 222, 128)), 2.4), passedGeom);

        DrawNozzle(context, ToScreen(Project(state.Position)), ToScreen(Project(new Point3(state.Position.X + state.Direction.X, state.Position.Y + state.Direction.Y, state.Position.Z + state.Direction.Z))));
    }

    private static Point Project(Point3 p)
    {
        var x = p.X - p.Y * 0.45;
        var y = -p.Z + (p.X + p.Y) * 0.18;
        return new Point(x, y);
    }

    private static (Point3 Position, Point3 Direction, int SegmentIndex) Interpolate(IList<Point3> pts, double progress)
    {
        if (pts.Count == 0) return (default, new Point3(1, 0, 0), 0);
        if (pts.Count == 1) return (pts[0], new Point3(1, 0, 0), 0);

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
                return (
                    new Point3(pts[i].X + (pts[i + 1].X - pts[i].X) * t, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t, pts[i].Z + (pts[i + 1].Z - pts[i].Z) * t),
                    new Point3(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y, pts[i + 1].Z - pts[i].Z),
                    i);
            }
            acc = next;
        }

        return (pts[^1], new Point3(pts[^1].X - pts[^2].X, pts[^1].Y - pts[^2].Y, pts[^1].Z - pts[^2].Z), pts.Count - 2);
    }

    private static void DrawNozzle(DrawingContext context, Point position, Point ahead)
    {
        var dir = new Point(ahead.X - position.X, ahead.Y - position.Y);
        var len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        var n = len <= 1e-6 ? new Point(1, 0) : new Point(dir.X / len, dir.Y / len);
        var perp = new Point(-n.Y, n.X);

        var tip = new Point(position.X + n.X * 16, position.Y + n.Y * 16);
        var left = new Point(position.X + perp.X * 5, position.Y + perp.Y * 5);
        var right = new Point(position.X - perp.X * 5, position.Y - perp.Y * 5);

        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(tip, true);
            gc.LineTo(left);
            gc.LineTo(right);
            gc.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(Color.FromRgb(248, 113, 113)), new Pen(Brushes.White, 1), g);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(220, 38, 38)), new Pen(Brushes.White, 1), position, 4.5, 4.5);
    }

    private readonly record struct Point3(double X, double Y, double Z);
}
