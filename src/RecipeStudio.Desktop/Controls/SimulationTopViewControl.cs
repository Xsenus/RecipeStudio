using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.Controls;

public sealed class SimulationTopViewControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<SimulationTopViewControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SimulationTopViewControl, double>(nameof(Progress));

    private Rect _fitWorldBounds;
    private Rect _worldBounds;
    private double _scale;
    private const double Pad = 20;
    private double _zoomFactor = 1.0;
    private Point _panOffset;
    private bool _isPanning;
    private Point _panStartScreen;
    private Point _panStartOffset;
    private bool _panWithRightButton;

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

    public double ZoomFactor => _zoomFactor;
    public event Action<double>? ZoomChanged;

    static SimulationTopViewControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationTopViewControl>((c, _) => c.InvalidateVisual());
        ProgressProperty.Changed.AddClassHandler<SimulationTopViewControl>((c, _) => c.InvalidateVisual());
    }

    public SimulationTopViewControl()
    {
        ClipToBounds = true;
    }

    public void ZoomIn()
    {
        _zoomFactor = Math.Clamp(_zoomFactor * 1.2, 0.2, 20.0);
        ClampPanOffset();
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public void ZoomOut()
    {
        _zoomFactor = Math.Clamp(_zoomFactor / 1.2, 0.2, 20.0);
        ClampPanOffset();
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public void ResetZoom()
    {
        _zoomFactor = 1.0;
        _panOffset = default;
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(3, 12, 34)), Bounds);

        var allPoints = Points?.ToList() ?? new List<RecipePoint>();
        var points = SelectRenderablePoints(allPoints);
        if (points.Count == 0)
            return;

        var absolute = RobotCoordinateResolver.BuildAbsolutePositions(points);
        var world = absolute.Select(p => new Point(p.X, p.Y)).ToList();
        Fit(world);

        var plotClip = new Rect(Pad, Pad, Math.Max(1, Bounds.Width - 2 * Pad), Math.Max(1, Bounds.Height - 2 * Pad));
        using (context.PushClip(plotClip))
        {
            var pathPen = new Pen(new SolidColorBrush(Color.FromRgb(76, 180, 255)), 1.6);
            DrawPolyline(context, world, pathPen);

            foreach (var wp in world)
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(34, 197, 94)), null, WorldToScreen(wp), 2.5, 2.5);

            var state = Interpolate(world, Progress);

            var passed = world.Take(state.SegmentIndex + 1).ToList();
            passed.Add(state.Position);
            DrawPolyline(context, passed, new Pen(new SolidColorBrush(Color.FromRgb(34, 197, 94)), 2.2));

            DrawNozzle(context, state.Position, state.Direction);
        }
    }

    private void Fit(IList<Point> points)
    {
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var fitBounds = new Rect(new Point(minX, minY), new Point(maxX, maxY)).Normalize();
        if (fitBounds.Width < 1) fitBounds = fitBounds.WithWidth(1);
        if (fitBounds.Height < 1) fitBounds = fitBounds.WithHeight(1);

        var sx = Math.Max(1, (Bounds.Width - Pad * 2) / fitBounds.Width);
        var sy = Math.Max(1, (Bounds.Height - Pad * 2) / fitBounds.Height);
        _scale = Math.Min(sx, sy);
        _fitWorldBounds = fitBounds;

        ClampPanOffset();
        var centerX = _fitWorldBounds.Center.X + _panOffset.X;
        var centerY = _fitWorldBounds.Center.Y + _panOffset.Y;
        var zoomedWidth = _fitWorldBounds.Width / _zoomFactor;
        var zoomedHeight = _fitWorldBounds.Height / _zoomFactor;
        _worldBounds = new Rect(centerX - zoomedWidth / 2.0, centerY - zoomedHeight / 2.0, zoomedWidth, zoomedHeight);
        _scale *= _zoomFactor;
    }

    private void ClampPanOffset()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
            return;

        // Keep panning available even at x1.00 so the top-view interaction feels responsive,
        // while still preventing the user from losing the trajectory completely.
        var zoomLimitedX = Math.Max(0, (_fitWorldBounds.Width - _fitWorldBounds.Width / _zoomFactor) / 2.0);
        var zoomLimitedY = Math.Max(0, (_fitWorldBounds.Height - _fitWorldBounds.Height / _zoomFactor) / 2.0);

        var basePanX = _fitWorldBounds.Width * 0.15;
        var basePanY = _fitWorldBounds.Height * 0.15;

        var maxX = Math.Max(zoomLimitedX, basePanX);
        var maxY = Math.Max(zoomLimitedY, basePanY);

        _panOffset = new Point(Math.Clamp(_panOffset.X, -maxX, maxX), Math.Clamp(_panOffset.Y, -maxY, maxY));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed && !props.IsRightButtonPressed)
            return;

        _panWithRightButton = props.IsRightButtonPressed && !props.IsLeftButtonPressed;
        _isPanning = true;
        _panStartScreen = e.GetPosition(this);
        _panStartOffset = _panOffset;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isPanning)
            return;

        var props = e.GetCurrentPoint(this).Properties;
        var panPressed = _panWithRightButton ? props.IsRightButtonPressed : props.IsLeftButtonPressed;
        if (!panPressed)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            return;
        }

        var p = e.GetPosition(this);
        var dx = p.X - _panStartScreen.X;
        var dy = p.Y - _panStartScreen.Y;
        var worldDx = _scale <= 1e-6 ? 0 : dx / _scale;
        var worldDy = _scale <= 1e-6 ? 0 : -dy / _scale;

        _panOffset = new Point(_panStartOffset.X - worldDx, _panStartOffset.Y - worldDy);
        ClampPanOffset();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isPanning)
            return;

        _isPanning = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Delta.Y > 0)
            ZoomIn();
        else if (e.Delta.Y < 0)
            ZoomOut();

        e.Handled = true;
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

    private static (Point Position, Point Direction, int SegmentIndex) Interpolate(IList<Point> pts, double progress)
    {
        if (pts.Count == 0) return (default, new Point(1, 0), 0);
        if (pts.Count == 1) return (pts[0], new Point(1, 0), 0);

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
                return (
                    new Point(pts[i].X + (pts[i + 1].X - pts[i].X) * t, pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t),
                    new Point(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y),
                    i);
            }
            acc = next;
        }

        return (pts[^1], new Point(pts[^1].X - pts[^2].X, pts[^1].Y - pts[^2].Y), pts.Count - 2);
    }

    private void DrawNozzle(DrawingContext ctx, Point worldPos, Point worldDir)
    {
        var p = WorldToScreen(worldPos);
        var d = new Point(worldDir.X * _scale, -worldDir.Y * _scale);
        var len = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        var dir = len <= 1e-6 ? new Point(1, 0) : new Point(d.X / len, d.Y / len);
        var perp = new Point(-dir.Y, dir.X);

        var tip = new Point(p.X + dir.X * 18, p.Y + dir.Y * 18);
        var left = new Point(p.X + perp.X * 6, p.Y + perp.Y * 6);
        var right = new Point(p.X - perp.X * 6, p.Y - perp.Y * 6);

        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(tip, true);
            gc.LineTo(left);
            gc.LineTo(right);
            gc.EndFigure(true);
        }

        ctx.DrawGeometry(new SolidColorBrush(Color.FromRgb(248, 113, 113)), new Pen(Brushes.White, 1), g);
        ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(239, 68, 68)), new Pen(Brushes.White, 1), p, 4.5, 4.5);
    }

    private static List<RecipePoint> SelectRenderablePoints(List<RecipePoint> source)
    {
        var activeRenderable = source.Where(p => p.Act && !p.Hidden && HasRenderableGeometry(p)).ToList();
        if (activeRenderable.Count > 0)
            return activeRenderable;

        var activeVisible = source.Where(p => p.Act && !p.Hidden).ToList();
        if (activeVisible.Count > 0)
            return activeVisible;

        var active = source.Where(p => p.Act).ToList();
        return active.Count > 0 ? active : source;
    }

    private static bool HasRenderableGeometry(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.RCrd) > eps
            || Math.Abs(p.ZCrd) > eps
            || Math.Abs(p.Xr0 + p.DX) > eps
            || Math.Abs(p.Zr0 + p.DZ) > eps;
    }
}
