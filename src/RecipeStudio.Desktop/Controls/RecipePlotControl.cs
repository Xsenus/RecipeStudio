using System;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.Controls;

public sealed class RecipePlotControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<RecipePlotControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<RecipePoint?> SelectedPointProperty =
        AvaloniaProperty.Register<RecipePlotControl, RecipePoint?>(nameof(SelectedPoint), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(Progress));

    public static readonly StyledProperty<AppSettings?> SettingsProperty =
        AvaloniaProperty.Register<RecipePlotControl, AppSettings?>(nameof(Settings));

    private INotifyCollectionChanged? _collectionChanged;
    private readonly Dictionary<RecipePoint, PropertyChangedEventHandler> _pointHandlers = new();

    private bool _isDragging;
    private RecipePoint? _dragPoint;

    // Cached transform
    private Rect _worldBounds;
    private double _scale;
    private double _pad;

    public IList<RecipePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public RecipePoint? SelectedPoint
    {
        get => GetValue(SelectedPointProperty);
        set => SetValue(SelectedPointProperty, value);
    }

    /// <summary>
    /// 0..1 tool progress.
    /// </summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public AppSettings? Settings
    {
        get => GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    static RecipePlotControl()
    {
        // Avoid relying on GetObservable/AffectsRender helpers (can vary between Avalonia versions).
        // Instead, hook property changes directly.
        PointsProperty.Changed.AddClassHandler<RecipePlotControl>((c, e) =>
            c.OnPointsChanged((IList<RecipePoint>?)e.NewValue));

        SelectedPointProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ProgressProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
    }

    public RecipePlotControl()
    {
        // no-op (handlers are attached in the static ctor)
    }

    private void OnPointsChanged(IList<RecipePoint>? points)
    {
        // detach old
        if (_collectionChanged is not null)
        {
            _collectionChanged.CollectionChanged -= OnCollectionChanged;
            _collectionChanged = null;
        }

        foreach (var (p, handler) in _pointHandlers.ToArray())
        {
            p.PropertyChanged -= handler;
            _pointHandlers.Remove(p);
        }

        if (points is INotifyCollectionChanged incc)
        {
            _collectionChanged = incc;
            incc.CollectionChanged += OnCollectionChanged;
        }

        if (points is not null)
        {
            foreach (var p in points)
                HookPoint(p);
        }

        InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var it in e.OldItems)
            {
                if (it is RecipePoint p && _pointHandlers.TryGetValue(p, out var h))
                {
                    p.PropertyChanged -= h;
                    _pointHandlers.Remove(p);
                }
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var it in e.NewItems)
            {
                if (it is RecipePoint p)
                    HookPoint(p);
            }
        }

        InvalidateVisual();
    }

    private void HookPoint(RecipePoint p)
    {
        PropertyChangedEventHandler handler = (_, __) => InvalidateVisual();
        p.PropertyChanged += handler;
        _pointHandlers[p] = handler;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Prevent leaks if the control is removed.
        OnPointsChanged(null);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var settings = Settings ?? new AppSettings();
        var points = Points;

        // background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), new Rect(Bounds.Size));

        if (points is null || points.Count == 0)
        {
            DrawCenteredText(context, "Нет точек", Bounds);
            return;
        }

        // Collect world points
        var target = new List<Point>();
        var tool = new List<Point>();
        foreach (var p in points)
        {
            var (xp, zp) = p.GetTargetPoint(settings.HZone);
            target.Add(new Point(xp, zp));

            var xr = p.Xr0 + p.DX;
            var zr = p.Zr0 + p.DZ;
            tool.Add(new Point(xr, zr));
        }

        // Determine bounds including clamp rectangles
        var dClamp = points[0].Container ? points[0].DClampCont : points[0].DClampForm;
        var halfClamp = Math.Max(10, dClamp / 2.0);

        var hFreeZ = (settings.HZone - (settings.HContMax + settings.HBokMax)) / 2.0 + settings.HContMax;

        var xs = target.Select(p => p.X).Concat(tool.Select(p => p.X)).Concat(new[] { -halfClamp, halfClamp, 0.0 });
        var ys = target.Select(p => p.Y).Concat(tool.Select(p => p.Y)).Concat(new[] { 0.0, settings.HZone, settings.HContMax, hFreeZ });

        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();

        _worldBounds = new Rect(new Point(minX, minY), new Point(maxX, maxY)).Normalize();

        // padding in pixels
        _pad = 36;

        var w = Math.Max(1, Bounds.Width - 2 * _pad);
        var h = Math.Max(1, Bounds.Height - 2 * _pad);

        var worldW = Math.Max(1e-6, _worldBounds.Width);
        var worldH = Math.Max(1e-6, _worldBounds.Height);

        var sx = w / worldW;
        var sy = h / worldH;
        _scale = Math.Min(sx, sy);

        // Expand world bounds to keep aspect
        var scaledWorldW = w / _scale;
        var scaledWorldH = h / _scale;

        var extraW = scaledWorldW - worldW;
        var extraH = scaledWorldH - worldH;

        _worldBounds = new Rect(
            _worldBounds.X - extraW / 2.0,
            _worldBounds.Y - extraH / 2.0,
            _worldBounds.Width + extraW,
            _worldBounds.Height + extraH);

        // Grid
        DrawGrid(context);

        // Clamp rectangles (visual reference)
        DrawClamp(context, halfClamp, settings.HContMax, hFreeZ, settings.HZone);

        // Paths
        var opacity = Math.Clamp(settings.PlotOpacity, 0.05, 0.90);
        var thickness = Math.Max(1, settings.PlotStrokeThickness);

        var penTool = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 245, 158, 11)), thickness);
        var penTarget = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 34, 197, 94)), thickness);

        // Dotted pen for target polyline ("строки точек")
        var penTargetDotted = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 34, 197, 94)), 1.5,
            dashStyle: new DashStyle(new double[] { 4, 4 }, 0));

        if (settings.PlotShowPolyline)
        {
            DrawPolyline(context, tool, penTool);
            DrawPolyline(context, target, penTargetDotted);
        }

        if (settings.PlotShowSmooth)
        {
            var smooth = Spline.CatmullRom(tool, settings.SmoothSegmentsPerSpan);
            DrawPolyline(context, smooth, penTool);
        }

        // Target points
        if (settings.PlotShowTargetPoints)
        {
            DrawPoints(context, points, settings, settings.HZone);
        }

        // Tool marker
        var toolPos = GetToolPosition(tool, Progress);
        DrawToolMarker(context, toolPos);

        // Legend
        DrawLegend(context);
    }

    private void DrawGrid(DrawingContext ctx)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1);
        var axisPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)), 1);

        // choose a step (world units) based on visible range
        var range = Math.Max(_worldBounds.Width, _worldBounds.Height);
        var step = range switch
        {
            > 2000 => 200,
            > 1000 => 100,
            > 500 => 50,
            _ => 25
        };

        // vertical lines
        var startX = Math.Floor(_worldBounds.Left / step) * step;
        for (double x = startX; x <= _worldBounds.Right; x += step)
        {
            var p1 = WorldToScreen(new Point(x, _worldBounds.Bottom));
            var p2 = WorldToScreen(new Point(x, _worldBounds.Top));
            ctx.DrawLine(gridPen, p1, p2);
        }

        // horizontal lines
        var startY = Math.Floor(_worldBounds.Top / step) * step;
        for (double y = startY; y <= _worldBounds.Bottom; y += step)
        {
            var p1 = WorldToScreen(new Point(_worldBounds.Left, y));
            var p2 = WorldToScreen(new Point(_worldBounds.Right, y));
            ctx.DrawLine(gridPen, p1, p2);
        }

        // axes at X=0 and Z=0
        if (_worldBounds.Left <= 0 && _worldBounds.Right >= 0)
        {
            var p1 = WorldToScreen(new Point(0, _worldBounds.Bottom));
            var p2 = WorldToScreen(new Point(0, _worldBounds.Top));
            ctx.DrawLine(axisPen, p1, p2);
        }

        if (_worldBounds.Top <= 0 && _worldBounds.Bottom >= 0)
        {
            var p1 = WorldToScreen(new Point(_worldBounds.Left, 0));
            var p2 = WorldToScreen(new Point(_worldBounds.Right, 0));
            ctx.DrawLine(axisPen, p1, p2);
        }
    }

    private void DrawClamp(DrawingContext ctx, double halfClamp, double hCont, double hFreeZ, double hZone)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 2);
        var penDashed = new Pen(new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)), 1,
            dashStyle: new DashStyle(new double[] { 6, 6 }, 0));

        // Bottom (right side)
        var r1 = new Rect(
            WorldToScreen(new Point(0, 0)),
            WorldToScreen(new Point(halfClamp, hCont))).Normalize();
        ctx.DrawRectangle(null, pen, r1);

        // Top (left side)
        var r2 = new Rect(
            WorldToScreen(new Point(-halfClamp, hFreeZ)),
            WorldToScreen(new Point(0, hZone))).Normalize();
        ctx.DrawRectangle(null, pen, r2);

        // reference lines
        var y1 = WorldToScreen(new Point(_worldBounds.Left, hCont)).Y;
        ctx.DrawLine(penDashed, new Point(_pad, y1), new Point(Bounds.Width - _pad, y1));

        var y2 = WorldToScreen(new Point(_worldBounds.Left, hFreeZ)).Y;
        ctx.DrawLine(penDashed, new Point(_pad, y2), new Point(Bounds.Width - _pad, y2));
    }

    private void DrawPolyline(DrawingContext ctx, IList<Point> worldPoints, Pen pen)
    {
        if (worldPoints.Count < 2) return;

        var geom = new StreamGeometry();
        using (var gctx = geom.Open())
        {
            gctx.BeginFigure(WorldToScreen(worldPoints[0]), false);
            for (var i = 1; i < worldPoints.Count; i++)
            {
                gctx.LineTo(WorldToScreen(worldPoints[i]));
            }
            gctx.EndFigure(false);
        }

        ctx.DrawGeometry(null, pen, geom);
    }

    private void DrawPoints(DrawingContext ctx, IList<RecipePoint> points, AppSettings settings, double hZone)
    {
        var r = Math.Max(2, settings.PlotPointRadius);

        foreach (var p in points)
        {
            var (xp, zp) = p.GetTargetPoint(hZone);
            var sp = WorldToScreen(new Point(xp, zp));

            // Working vs safety colors
            var color = p.Safe
                ? Color.FromRgb(6, 182, 212)   // cyan
                : Color.FromRgb(34, 197, 94);  // green

            var brush = new SolidColorBrush(color);

            ctx.DrawEllipse(brush, null, sp, r, r);

            if (p == SelectedPoint)
            {
                var selPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2);
                ctx.DrawEllipse(null, selPen, sp, r + 3, r + 3);
            }
        }
    }

    private void DrawToolMarker(DrawingContext ctx, Point world)
    {
        var sp = WorldToScreen(world);
        var brush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        ctx.DrawEllipse(brush, null, sp, 5, 5);
    }

    private void DrawLegend(DrawingContext ctx)
    {
        var x = 14;
        var y = 14;
        var lineH = 18;

        void Entry(Color c, string text)
        {
            ctx.FillRectangle(new SolidColorBrush(c), new Rect(x, y + 4, 10, 10));
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, Brushes.White);
            ctx.DrawText(ft, new Point(x + 16, y));
            y += lineH;
        }

        Entry(Color.FromRgb(34, 197, 94), "Working Zone (Safe=0)");
        Entry(Color.FromRgb(6, 182, 212), "Safety Zone (Safe=1)");
        Entry(Color.FromRgb(245, 158, 11), "Robot/Tool Path");
        Entry(Color.FromRgb(239, 68, 68), "Tool");
    }

    private Point WorldToScreen(Point w)
    {
        // X: left->right, Z(Y): bottom->top
        var x = _pad + (w.X - _worldBounds.Left) * _scale;
        var y = _pad + (_worldBounds.Bottom - w.Y) * _scale;
        return new Point(x, y);
    }

    private Point ScreenToWorld(Point s)
    {
        var x = (s.X - _pad) / _scale + _worldBounds.Left;
        var y = _worldBounds.Bottom - (s.Y - _pad) / _scale;
        return new Point(x, y);
    }

    private static Point GetToolPosition(IList<Point> tool, double progress)
    {
        if (tool.Count == 0) return default;
        if (tool.Count == 1) return tool[0];

        progress = Math.Clamp(progress, 0, 1);

        // length along polyline
        double total = 0;
        var seg = new double[tool.Count - 1];
        for (var i = 0; i < tool.Count - 1; i++)
        {
            var dx = tool[i + 1].X - tool[i].X;
            var dy = tool[i + 1].Y - tool[i].Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            seg[i] = d;
            total += d;
        }

        if (total <= 1e-9) return tool[0];

        var target = total * progress;
        double acc = 0;
        for (var i = 0; i < seg.Length; i++)
        {
            var next = acc + seg[i];
            if (target <= next)
            {
                var t = (target - acc) / Math.Max(1e-9, seg[i]);
                var x = tool[i].X + (tool[i + 1].X - tool[i].X) * t;
                var y = tool[i].Y + (tool[i + 1].Y - tool[i].Y) * t;
                return new Point(x, y);
            }
            acc = next;
        }

        return tool[^1];
    }

    private void DrawCenteredText(DrawingContext ctx, string text, Rect bounds)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 14, Brushes.White);
        var p = new Point(bounds.Width / 2 - ft.Width / 2, bounds.Height / 2 - ft.Height / 2);
        ctx.DrawText(ft, p);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var settings = Settings;
        if (settings is null) return;
        if (Points is null || Points.Count == 0) return;

        var pos = e.GetPosition(this);

        // select nearest target point
        var hit = HitTestTargetPoint(pos, settings);
        if (hit is not null)
        {
            SelectedPoint = hit;

            if (settings.PlotEnableDrag && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _dragPoint = hit;
                e.Pointer.Capture(this);
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var settings = Settings;
        if (settings is null) return;

        if (!_isDragging || _dragPoint is null) return;

        var pos = e.GetPosition(this);
        var w = ScreenToWorld(pos);

        // Update underlying RCrd/ZCrd based on place.
        if (_dragPoint.Place == 0)
        {
            _dragPoint.RCrd = w.X;
            _dragPoint.ZCrd = w.Y;
        }
        else
        {
            _dragPoint.RCrd = -w.X;
            _dragPoint.ZCrd = settings.HZone - w.Y;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDragging)
        {
            _isDragging = false;
            _dragPoint = null;
            e.Pointer.Capture(null);
        }
    }

    private RecipePoint? HitTestTargetPoint(Point screen, AppSettings settings)
    {
        if (Points is null) return null;

        var best = (p: (RecipePoint?)null, dist: double.MaxValue);

        var r = Math.Max(4, settings.PlotPointRadius + 4);
        var r2 = r * r;

        foreach (var p in Points)
        {
            var (xp, zp) = p.GetTargetPoint(settings.HZone);
            var sp = WorldToScreen(new Point(xp, zp));
            var dx = sp.X - screen.X;
            var dy = sp.Y - screen.Y;
            var d2 = dx * dx + dy * dy;
            if (d2 <= r2 && d2 < best.dist)
            {
                best = (p, d2);
            }
        }

        return best.p;
    }

    private static class Spline
    {
        /// <summary>
        /// Catmull-Rom spline sampling (smooth interpolation between points).
        /// </summary>
        public static List<Point> CatmullRom(IList<Point> pts, int segmentsPerSpan)
        {
            segmentsPerSpan = Math.Clamp(segmentsPerSpan, 4, 64);

            if (pts.Count < 2)
                return pts.ToList();

            if (pts.Count == 2)
                return new List<Point>(pts);

            var res = new List<Point>();

            for (var i = 0; i < pts.Count - 1; i++)
            {
                var p0 = i == 0 ? pts[i] : pts[i - 1];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = (i + 2 < pts.Count) ? pts[i + 2] : pts[i + 1];

                for (var s = 0; s < segmentsPerSpan; s++)
                {
                    var t = s / (double)segmentsPerSpan;
                    res.Add(Catmull(p0, p1, p2, p3, t));
                }
            }

            res.Add(pts[^1]);
            return res;
        }

        private static Point Catmull(Point p0, Point p1, Point p2, Point p3, double t)
        {
            // Standard Catmull-Rom (tension=0.5)
            var t2 = t * t;
            var t3 = t2 * t;

            var x = 0.5 * ((2 * p1.X) + (-p0.X + p2.X) * t + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);
            var y = 0.5 * ((2 * p1.Y) + (-p0.Y + p2.Y) * t + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

            return new Point(x, y);
        }
    }
}
