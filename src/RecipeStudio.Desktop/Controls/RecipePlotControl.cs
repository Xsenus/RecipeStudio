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

    public static readonly StyledProperty<IList<RecipePoint>?> AnimationPointsProperty =
        AvaloniaProperty.Register<RecipePlotControl, IList<RecipePoint>?>(nameof(AnimationPoints));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(Progress));

    public static readonly StyledProperty<AppSettings?> SettingsProperty =
        AvaloniaProperty.Register<RecipePlotControl, AppSettings?>(nameof(Settings));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<bool> ShowPairLinksProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowPairLinks), false);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowGrid), true);

    private INotifyCollectionChanged? _collectionChanged;
    private readonly Dictionary<RecipePoint, PropertyChangedEventHandler> _pointHandlers = new();

    private bool _isDragging;
    private RecipePoint? _dragPoint;

    // Cached transform
    private Rect _worldBounds;
    private double _scale;
    private double _pad;
    private double _zoomFactor = 1.0;

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

    public IList<RecipePoint>? AnimationPoints
    {
        get => GetValue(AnimationPointsProperty);
        set => SetValue(AnimationPointsProperty, value);
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

    public bool ShowLegend
    {
        get => GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    public bool ShowPairLinks
    {
        get => GetValue(ShowPairLinksProperty);
        set => SetValue(ShowPairLinksProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    static RecipePlotControl()
    {
        // Avoid relying on GetObservable/AffectsRender helpers (can vary between Avalonia versions).
        // Instead, hook property changes directly.
        PointsProperty.Changed.AddClassHandler<RecipePlotControl>((c, e) =>
            c.OnPointsChanged((IList<RecipePoint>?)e.NewValue));

        SelectedPointProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        AnimationPointsProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ProgressProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowLegendProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowPairLinksProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowGridProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
    }

    public RecipePlotControl()
    {
        // no-op (handlers are attached in the static ctor)
    }


    public void ZoomIn()
    {
        _zoomFactor = Math.Clamp(_zoomFactor * 1.2, 1.0, 20.0);
        InvalidateVisual();
    }

    public void ZoomOut()
    {
        _zoomFactor = Math.Clamp(_zoomFactor / 1.2, 1.0, 20.0);
        InvalidateVisual();
    }

    public void ResetZoom()
    {
        _zoomFactor = 1.0;
        InvalidateVisual();
    }

    public double ZoomFactor => _zoomFactor;

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
        var points = FilterRenderablePoints(Points);

        // background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), new Rect(Bounds.Size));

        if (points.Count == 0)
        {
            DrawCenteredText(context, "Нет точек", Bounds);
            return;
        }

        // Collect world points (full profile for drawing)
        var target = new List<Point>();
        foreach (var p in points)
        {
            var (xp, zp) = p.GetTargetPoint(settings.HZone);
            target.Add(new Point(xp, zp));
        }

        var robotPoints = points.Where(p => !p.Safe).ToList();
        if (robotPoints.Count == 0)
            robotPoints = points;
        var absoluteRobot = RobotCoordinateResolver.BuildAbsolutePositions(robotPoints);
        var tool = absoluteRobot.Select(v => new Point(v.X, v.Z)).ToList();
        var robotToolMap = robotPoints.Select((p, i) => new { p, pt = tool[i] }).ToDictionary(x => x.p, x => x.pt);

        // Separate set for animation (usually working cleaning points only).
        var animSrc = SelectToolPoints(FilterRenderablePoints(AnimationPoints, fallback: robotPoints));
        var animTarget = new List<Point>();
        var animTool = new List<Point>();
        foreach (var p in animSrc)
        {
            var (xp, zp) = p.GetTargetPoint(settings.HZone);
            animTarget.Add(new Point(xp, zp));

            if (robotToolMap.TryGetValue(p, out var absPoint))
            {
                animTool.Add(absPoint);
            }
            else
            {
                var abs = RobotCoordinateResolver.BuildAbsolutePositions(new List<RecipePoint> { p })[0];
                animTool.Add(new Point(abs.X, abs.Z));
            }
        }

        // Determine bounds including clamp rectangles
        var dClamp = points[0].Container ? points[0].DClampCont : points[0].DClampForm;
        var halfClamp = Math.Max(10, dClamp / 2.0);

        var hFreeZ = Math.Clamp(settings.HFreeZ, Math.Min(settings.HContMax, settings.HZone), Math.Max(settings.HContMax, settings.HZone));

        var xs = target.Select(p => p.X).Concat(tool.Select(p => p.X)).Concat(new[] { -halfClamp, halfClamp, 0.0 });
        var ys = target.Select(p => p.Y).Concat(tool.Select(p => p.Y)).Concat(new[] { 0.0, settings.HZone, settings.HContMax, hFreeZ });

        var minX = xs.Min();
        var maxX = xs.Max();
        var minY = ys.Min();
        var maxY = ys.Max();

        _worldBounds = new Rect(new Point(minX, minY), new Point(maxX, maxY)).Normalize();

        // adaptive padding in pixels (keeps plot readable on smaller popups)
        _pad = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) * 0.06, 16, 36);

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

        // Zoom around current center (1.0 = fit-to-data)
        if (_zoomFactor > 1.0)
        {
            var centerX = _worldBounds.Center.X;
            var centerY = _worldBounds.Center.Y;
            var zoomedWidth = _worldBounds.Width / _zoomFactor;
            var zoomedHeight = _worldBounds.Height / _zoomFactor;
            _worldBounds = new Rect(centerX - zoomedWidth / 2.0, centerY - zoomedHeight / 2.0, zoomedWidth, zoomedHeight);
            _scale *= _zoomFactor;
        }

        // Grid
        if (ShowGrid)
            DrawGrid(context);

        // Clamp rectangles (visual reference)
        DrawClamp(context, halfClamp, settings.HContMax, hFreeZ, settings.HZone);

        // Paths
        var opacity = Math.Clamp(settings.PlotOpacity, 0.05, 0.90);
        var thickness = Math.Max(1, settings.PlotStrokeThickness);

        var workColor = ParseColorOrDefault(settings.PlotColorWorkingZone, Color.FromRgb(34, 197, 94));
        var safetyColor = ParseColorOrDefault(settings.PlotColorSafetyZone, Color.FromRgb(156, 163, 175));
        var robotColor = ParseColorOrDefault(settings.PlotColorRobotPath, Color.FromRgb(245, 158, 11));
        var linksColor = ParseColorOrDefault(settings.PlotColorPairLinks, Color.FromRgb(251, 146, 60));
        var penTool = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), robotColor.R, robotColor.G, robotColor.B)), thickness);
        var penTargetWork = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), workColor.R, workColor.G, workColor.B)), thickness);
        var penTargetSafe = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), safetyColor.R, safetyColor.G, safetyColor.B)), thickness);
        var penTargetToTool = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), linksColor.R, linksColor.G, linksColor.B)), Math.Max(1, thickness - 1));

        // Summary: keep series parity with Excel charts while avoiding artificial cross-group joins.
        if (settings.PlotShowPolyline)
        {
            // Robot path is split by (Safe, Place) the same way as Excel series,
            // so unrelated groups are not connected by artificial long segments.
            DrawPolyline(context, SelectTool(robotPoints, robotToolMap, safe: false, place: 0), penTool);
            DrawPolyline(context, SelectTool(robotPoints, robotToolMap, safe: false, place: 1), penTool);

            // Step 2: target polylines split by (Safe, Place) and pair links Xp/Zp <-> Xr/Zr for cleaning points.
            DrawPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 0), penTargetWork);
            DrawPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 1), penTargetWork);
            DrawWorkTransitionLinks(context, points, settings.HZone, penTargetWork);
            DrawPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 0), penTargetSafe);
            DrawPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 1), penTargetSafe);

            if (ShowPairLinks)
                DrawTargetToToolLinks(context, points, robotToolMap, settings.HZone, penTargetToTool);
        }

        if (settings.PlotShowSmooth)
        {
            var smooth = Spline.CatmullRom(tool, settings.SmoothSegmentsPerSpan);
            DrawPolyline(context, smooth, penTool);
        }

        // Target points are always drawn to keep point markers visible in the editor.
        DrawPoints(context, points, settings, settings.HZone);
        DrawRobotPoints(context, robotPoints, robotToolMap, settings);

        // Tool marker rendered as a smooth nozzle link Target->Robot.
        var toolState = GetToolState(animTool, animTarget, Progress);
        DrawToolMarker(context, toolState.ToolPosition, toolState.TargetPosition, toolState.Direction);

        // Legend
        if (ShowLegend)
            DrawLegend(context);
    }


    /// <summary>
    /// Selects rows that can be rendered on charts, preferring active + visible + geometric rows,
    /// with graceful fallback to less strict subsets when source data is sparse.
    /// </summary>
    private static List<RecipePoint> FilterRenderablePoints(IList<RecipePoint>? source, IList<RecipePoint>? fallback = null)
    {
        var src = source?.ToList() ?? new List<RecipePoint>();
        if (src.Count == 0 && fallback is not null)
            src = fallback.ToList();

        if (src.Count == 0)
            return src;

        var activeRenderable = src.Where(p => p.Act && !p.Hidden && HasRenderableGeometry(p)).ToList();
        if (activeRenderable.Count > 0)
            return activeRenderable;

        var activeVisible = src.Where(p => p.Act && !p.Hidden).ToList();
        if (activeVisible.Count > 0)
            return activeVisible;

        var active = src.Where(p => p.Act).ToList();
        return active.Count > 0 ? active : src;
    }

    /// <summary>
    /// Chooses animation points for the tool path: working rows first, otherwise the full source.
    /// </summary>
    private static List<RecipePoint> SelectToolPoints(IList<RecipePoint> source)
    {
        var working = source.Where(p => !p.Safe).ToList();
        return working.Count > 0 ? working : source.ToList();
    }

    /// <summary>
    /// Returns true when a row contains non-zero geometry either in target or robot columns.
    /// </summary>
    private static bool HasRenderableGeometry(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.RCrd) > eps
            || Math.Abs(p.ZCrd) > eps
            || Math.Abs(p.Xr0 + p.DX) > eps
            || Math.Abs(p.Zr0 + p.DZ) > eps;
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
        var r = Math.Max(4, settings.PlotPointRadius);

        foreach (var p in points)
        {
            var (xp, zp) = p.GetTargetPoint(hZone);
            var sp = WorldToScreen(new Point(xp, zp));

            // Working vs safety colors
            var color = p.Safe
                ? ParseColorOrDefault(settings.PlotColorSafetyZone, Color.FromRgb(156, 163, 175))
                : ParseColorOrDefault(settings.PlotColorWorkingZone, Color.FromRgb(34, 197, 94));

            var brush = new SolidColorBrush(color);

            var outline = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.2);
            ctx.DrawEllipse(brush, outline, sp, r, r);

            if (p == SelectedPoint)
            {
                var selPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2);
                ctx.DrawEllipse(null, selPen, sp, r + 3, r + 3);
            }
        }
    }

    private void DrawRobotPoints(DrawingContext ctx, IList<RecipePoint> points, Dictionary<RecipePoint, Point> robotToolMap, AppSettings settings)
    {
        var r = Math.Max(3, settings.PlotPointRadius - 1);
        var brush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        var outline = new Pen(new SolidColorBrush(ParseColorOrDefault(settings.PlotColorRobotPath, Color.FromRgb(245, 158, 11))), 1.2);

        foreach (var p in points)
        {
            if (!robotToolMap.TryGetValue(p, out var toolPoint))
                continue;

            var sp = WorldToScreen(toolPoint);
            ctx.DrawEllipse(brush, outline, sp, r, r);
        }
    }

    /// <summary>
    /// Builds target polyline points for a specific (Safe, Place) series.
    /// </summary>
    private static List<Point> SelectTarget(IList<RecipePoint> points, double hZone, bool safe, int place)
        => points
            .Where(p => p.Safe == safe && p.Place == place)
            .Select(p =>
            {
                var (xp, zp) = p.GetTargetPoint(hZone);
                return new Point(xp, zp);
            })
            .ToList();

    /// <summary>
    /// Builds robot/tool polyline points for a specific (Safe, Place) series.
    /// </summary>
    private static List<Point> SelectTool(IList<RecipePoint> points, Dictionary<RecipePoint, Point> robotToolMap, bool safe, int place)
        => points
            .Where(p => p.Safe == safe && p.Place == place)
            .Where(robotToolMap.ContainsKey)
            .Select(p => robotToolMap[p])
            .ToList();

    /// <summary>
    /// Restores workbook-style green bridge segments between consecutive working points
    /// when sequence transitions from one Place group to another.
    /// </summary>
    private void DrawWorkTransitionLinks(DrawingContext ctx, IList<RecipePoint> points, double hZone, Pen pen)
    {
        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1];
            var cur = points[i];

            if (prev.Safe || cur.Safe || prev.Place == cur.Place)
                continue;

            var (x1, z1) = prev.GetTargetPoint(hZone);
            var (x2, z2) = cur.GetTargetPoint(hZone);
            ctx.DrawLine(pen, WorldToScreen(new Point(x1, z1)), WorldToScreen(new Point(x2, z2)));
        }
    }

    /// <summary>
    /// Draws pair links Xp/Zp -> Xr/Zr for working rows (Safe=0).
    /// </summary>
    private void DrawTargetToToolLinks(DrawingContext ctx, IList<RecipePoint> points, Dictionary<RecipePoint, Point> robotToolMap, double hZone, Pen pen)
    {
        foreach (var p in points.Where(x => !x.Safe))
        {
            var (xp, zp) = p.GetTargetPoint(hZone);
            if (!robotToolMap.TryGetValue(p, out var toolPoint))
                continue;

            ctx.DrawLine(pen, WorldToScreen(new Point(xp, zp)), WorldToScreen(toolPoint));
        }
    }

    private static Point GetToolPointForPlot(RecipePoint point, AppSettings _)
    {
        var xr = point.Xr0 + point.DX;
        var zr = point.Zr0 + point.DZ;
        return new Point(xr, zr);
    }

    private void DrawToolMarker(DrawingContext ctx, Point toolWorld, Point targetWorld, Point direction)
    {
        var toolSp = WorldToScreen(toolWorld);
        var targetSp = WorldToScreen(targetWorld);

        var toolColor = ParseColorOrDefault((Settings ?? new AppSettings()).PlotColorTool, Color.FromRgb(239, 68, 68));
        var linkPen = new Pen(new SolidColorBrush(Color.FromArgb(230, toolColor.R, toolColor.G, toolColor.B)), 7);
        ctx.DrawLine(linkPen, targetSp, toolSp);

        // Base joint on robot side
        ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(10, 16, 30)), new Pen(new SolidColorBrush(toolColor), 2), toolSp, 6, 6);

        // Tip at cleaning target side
        ctx.DrawEllipse(new SolidColorBrush(toolColor), new Pen(Brushes.White, 1.2), targetSp, 4.2, 4.2);

        // Tiny direction arrow to emphasize smooth rotation
        var len = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        var dir = len <= 1e-6 ? new Point(1, 0) : new Point(direction.X / len, direction.Y / len);
        var perp = new Point(-dir.Y, dir.X);
        var tip = new Point(targetSp.X + dir.X * 10, targetSp.Y + dir.Y * 10);
        var left = new Point(targetSp.X - dir.X * 3 + perp.X * 3, targetSp.Y - dir.Y * 3 + perp.Y * 3);
        var right = new Point(targetSp.X - dir.X * 3 - perp.X * 3, targetSp.Y - dir.Y * 3 - perp.Y * 3);

        var g = new StreamGeometry();
        using (var gc = g.Open())
        {
            gc.BeginFigure(tip, true);
            gc.LineTo(left);
            gc.LineTo(right);
            gc.EndFigure(true);
        }

        ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(230, toolColor.R, toolColor.G, toolColor.B)), null, g);
    }

    private void DrawLegend(DrawingContext ctx)
    {
        var x = _pad + 4;
        var y = _pad - 6;
        var lineH = 16;

        var entries = 5;
        var legendHeight = entries * lineH + 8;
        var legendWidth = 250;
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(110, 2, 6, 23)), new Rect(x - 8, y - 6, legendWidth, legendHeight));

        void Entry(Color c, string text)
        {
            ctx.FillRectangle(new SolidColorBrush(c), new Rect(x, y + 4, 10, 10));
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.White);
            ctx.DrawText(ft, new Point(x + 16, y));
            y += lineH;
        }

                var settings = Settings ?? new AppSettings();
        Entry(ParseColorOrDefault(settings.PlotColorWorkingZone, Color.FromRgb(34, 197, 94)), "Рабочая зона (Safe=0)");
        Entry(ParseColorOrDefault(settings.PlotColorSafetyZone, Color.FromRgb(156, 163, 175)), "Безопасная зона (Safe=1)");
        Entry(ParseColorOrDefault(settings.PlotColorRobotPath, Color.FromRgb(245, 158, 11)), "Траектория/точки робота (Xr,Zr)");
        Entry(ParseColorOrDefault(settings.PlotColorPairLinks, Color.FromRgb(251, 146, 60)), "Связи Xp/Zp ↔ Xr/Zr (Safe=0)");
        Entry(ParseColorOrDefault(settings.PlotColorTool, Color.FromRgb(239, 68, 68)), "Текущий инструмент");
    }

    private static Color ParseColorOrDefault(string? value, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out var parsed))
            return parsed;

        return fallback;
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

    private static (Point ToolPosition, Point TargetPosition, Point Direction) GetToolState(IList<Point> tool, IList<Point> targetPts, double progress)
    {
        if (tool.Count == 0 || targetPts.Count == 0) return (default, default, new Point(1, 0));
        if (tool.Count == 1 || targetPts.Count == 1) return (tool[0], targetPts[0], new Point(1, 0));

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

        if (total <= 1e-9)
            return (tool[0], targetPts[0], new Point(tool[^1].X - tool[0].X, tool[^1].Y - tool[0].Y));

        var targetLen = total * progress;
        double acc = 0;
        for (var i = 0; i < seg.Length; i++)
        {
            var next = acc + seg[i];
            if (targetLen <= next)
            {
                var t = (targetLen - acc) / Math.Max(1e-9, seg[i]);
                var x = tool[i].X + (tool[i + 1].X - tool[i].X) * t;
                var y = tool[i].Y + (tool[i + 1].Y - tool[i].Y) * t;
                var tx = targetPts[i].X + (targetPts[i + 1].X - targetPts[i].X) * t;
                var ty = targetPts[i].Y + (targetPts[i + 1].Y - targetPts[i].Y) * t;
                var dir = new Point(tx - x, ty - y);
                return (new Point(x, y), new Point(tx, ty), dir);
            }
            acc = next;
        }

        var fallbackDir = new Point(targetPts[^1].X - tool[^1].X, targetPts[^1].Y - tool[^1].Y);
        return (tool[^1], targetPts[^1], fallbackDir);
    }

    private void DrawCenteredText(DrawingContext ctx, string text, Rect bounds)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 14, Brushes.White);
        var p = new Point(bounds.Width / 2 - ft.Width / 2, bounds.Height / 2 - ft.Height / 2);
        ctx.DrawText(ft, p);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Delta.Y > 0)
            ZoomIn();
        else if (e.Delta.Y < 0)
            ZoomOut();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var settings = Settings ?? new AppSettings();
        if (Points is null || Points.Count == 0) return;

        var pos = e.GetPosition(this);

        // select nearest target point
        var hit = HitTestTargetPoint(pos, settings);
        if (hit is not null)
        {
            SelectedPoint = hit;

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

        var settings = Settings ?? new AppSettings();

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

        var r = Math.Max(10, settings.PlotPointRadius + 6);
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
