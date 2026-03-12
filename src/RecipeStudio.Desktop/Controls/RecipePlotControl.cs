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
    private static readonly ProfileDisplayPathService DisplayPathService = new();

    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<RecipePlotControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<RecipePoint?> SelectedPointProperty =
        AvaloniaProperty.Register<RecipePlotControl, RecipePoint?>(nameof(SelectedPoint), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IList<RecipePoint>?> AnimationPointsProperty =
        AvaloniaProperty.Register<RecipePlotControl, IList<RecipePoint>?>(nameof(AnimationPoints));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(Progress));

    public static readonly StyledProperty<double> CurrentAlfaProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(CurrentAlfa));

    public static readonly StyledProperty<double> CurrentBettaProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(CurrentBetta));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<int> CurrentSegmentIndexProperty =
        AvaloniaProperty.Register<RecipePlotControl, int>(nameof(CurrentSegmentIndex), -1);

    public static readonly StyledProperty<double> CurrentSegmentTProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(CurrentSegmentT), 0d);

    public static readonly StyledProperty<double> ToolXRawProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(ToolXRaw), double.NaN);

    public static readonly StyledProperty<double> ToolZRawProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(ToolZRaw), double.NaN);

    public static readonly StyledProperty<double> TargetXRawProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(TargetXRaw), double.NaN);

    public static readonly StyledProperty<double> TargetZRawProperty =
        AvaloniaProperty.Register<RecipePlotControl, double>(nameof(TargetZRaw), double.NaN);

    public static readonly StyledProperty<AppSettings?> SettingsProperty =
        AvaloniaProperty.Register<RecipePlotControl, AppSettings?>(nameof(Settings));

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowLegend), true);

    public static readonly StyledProperty<bool> ShowPairLinksProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowPairLinks), false);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> InvertHorizontalProperty =
        AvaloniaProperty.Register<RecipePlotControl, bool>(nameof(InvertHorizontal));

    public static readonly StyledProperty<string> TargetDisplayModeProperty =
        AvaloniaProperty.Register<RecipePlotControl, string>(nameof(TargetDisplayMode), SimulationTargetDisplayModes.Full);

    private INotifyCollectionChanged? _collectionChanged;
    private readonly Dictionary<RecipePoint, PropertyChangedEventHandler> _pointHandlers = new();

    private bool _isPanning;
    private Point _panStartScreen;
    private Point _panStartOffset;
    private bool _isDraggingInfoBox;
    private Point _infoBoxDragStartScreen;
    private Rect _infoBoxDragStartRect;
    private Rect _lastInfoBoxRect;
    private Rect _lastPlotBoundsRect;
    private Size _lastInfoBoxSize;
    private bool _hasInfoBoxRect;

    // Cached transform
    private Rect _worldBounds;
    private Rect _fitWorldBounds;
    private double _scale;
    private double _pad;
    private double _zoomFactor = 1.0;
    private Point _panOffset;
    private Rect _plotViewportRect;

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

    public double CurrentAlfa
    {
        get => GetValue(CurrentAlfaProperty);
        set => SetValue(CurrentAlfaProperty, value);
    }

    public double CurrentBetta
    {
        get => GetValue(CurrentBettaProperty);
        set => SetValue(CurrentBettaProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public int CurrentSegmentIndex
    {
        get => GetValue(CurrentSegmentIndexProperty);
        set => SetValue(CurrentSegmentIndexProperty, value);
    }

    public double CurrentSegmentT
    {
        get => GetValue(CurrentSegmentTProperty);
        set => SetValue(CurrentSegmentTProperty, value);
    }

    public double ToolXRaw
    {
        get => GetValue(ToolXRawProperty);
        set => SetValue(ToolXRawProperty, value);
    }

    public double ToolZRaw
    {
        get => GetValue(ToolZRawProperty);
        set => SetValue(ToolZRawProperty, value);
    }

    public double TargetXRaw
    {
        get => GetValue(TargetXRawProperty);
        set => SetValue(TargetXRawProperty, value);
    }

    public double TargetZRaw
    {
        get => GetValue(TargetZRawProperty);
        set => SetValue(TargetZRawProperty, value);
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

    public bool InvertHorizontal
    {
        get => GetValue(InvertHorizontalProperty);
        set => SetValue(InvertHorizontalProperty, value);
    }

    public string TargetDisplayMode
    {
        get => GetValue(TargetDisplayModeProperty);
        set => SetValue(TargetDisplayModeProperty, value);
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
        CurrentAlfaProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        CurrentBettaProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        IsPlayingProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentIndexProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentTProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ToolXRawProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ToolZRawProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        TargetXRawProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        TargetZRawProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowLegendProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowPairLinksProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        ShowGridProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        InvertHorizontalProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
        TargetDisplayModeProperty.Changed.AddClassHandler<RecipePlotControl>((c, _) => c.InvalidateVisual());
    }

    public RecipePlotControl()
    {
        // no-op (handlers are attached in the static ctor)
        ClipToBounds = true;
    }


    public void ZoomIn()
    {
        _zoomFactor = Math.Clamp(_zoomFactor * 1.2, 0.2, 20.0);
        ClampPanOffset();
        InvalidateVisual();
        NotifyZoomChanged();
    }

    public void ZoomOut()
    {
        _zoomFactor = Math.Clamp(_zoomFactor / 1.2, 0.2, 20.0);
        ClampPanOffset();
        InvalidateVisual();
        NotifyZoomChanged();
    }

    public void ResetZoom()
    {
        _zoomFactor = 1.0;
        _panOffset = default;
        InvalidateVisual();
        NotifyZoomChanged();
    }

    public double ZoomFactor => _zoomFactor;

    public event Action<double>? ZoomChanged;
    public event Action? InfoBoxPositionChanged;

    private void NotifyZoomChanged()
    {
        ZoomChanged?.Invoke(_zoomFactor);
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
        _hasInfoBoxRect = false;
        _lastInfoBoxRect = default;
        _lastInfoBoxSize = default;
        _lastPlotBoundsRect = default;
        _plotViewportRect = default;

        // background
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), new Rect(Bounds.Size));

        var displayPath = FilterDisplayPathBySettings(
            BuildVisibleDisplayPath(ApplyDisplayMirror(DisplayPathService.Build(Points ?? AnimationPoints, settings))),
            settings);
        if (displayPath.PathNodes.Count > 0 || displayPath.Polylines.Count > 0)
        {
            var selectedSample = !IsPlaying ? TryResolveSelectedDisplaySample(displayPath) : null;
            var sample = selectedSample ?? DisplayPathService.EvaluateByProgress(displayPath, Progress);
            _pad = Math.Clamp(Math.Min(Bounds.Width, Bounds.Height) * 0.06, 16, 36);
            var outerPlotRect = new Rect(_pad, _pad, Math.Max(1, Bounds.Width - 2 * _pad), Math.Max(1, Bounds.Height - 2 * _pad));

            if (settings.PlotProfileUsePythonViewport)
            {
                var viewportBounds = ResolveConfiguredProfileViewport(settings);
                _plotViewportRect = FitAspectRect(outerPlotRect, viewportBounds.Width / viewportBounds.Height);
                _fitWorldBounds = viewportBounds;
                _worldBounds = _fitWorldBounds;
            }
            else
            {
                var allPoints = displayPath.Polylines.SelectMany(x => x.ControlPoints.Concat(x.CurvePoints))
                    .Concat(displayPath.B0PolylinePoints)
                    .Concat(displayPath.PathNodes.Select(node => node.A1))
                    .Concat(displayPath.PathNodes.Select(node => node.B0))
                    .Concat(displayPath.FrameSamples.Select(frame => frame.A1))
                    .Concat(displayPath.FrameSamples.Select(frame => frame.B0))
                    .Append(new Point(0, 0))
                    .Append(new Point(0, settings.HFreeZ))
                    .ToList();

                if (sample.IsValid)
                {
                    allPoints.Add(sample.A0);
                    allPoints.Add(sample.A1);
                    allPoints.Add(sample.B0);
                }

                if (allPoints.Count == 0)
                    allPoints.Add(new Point(0, 0));

                _worldBounds = new Rect(
                    new Point(allPoints.Min(point => point.X), allPoints.Min(point => point.Y)),
                    new Point(allPoints.Max(point => point.X), allPoints.Max(point => point.Y))).Normalize();

                var displayWidth = Math.Max(1, outerPlotRect.Width);
                var displayHeight = Math.Max(1, outerPlotRect.Height);
                var displayWorldWidth = Math.Max(1e-6, _worldBounds.Width);
                var displayWorldHeight = Math.Max(1e-6, _worldBounds.Height);
                var fitScale = Math.Min(displayWidth / displayWorldWidth, displayHeight / displayWorldHeight);

                var displayScaledWorldWidth = displayWidth / fitScale;
                var displayScaledWorldHeight = displayHeight / fitScale;
                var displayExtraWidth = displayScaledWorldWidth - displayWorldWidth;
                var displayExtraHeight = displayScaledWorldHeight - displayWorldHeight;
                _fitWorldBounds = new Rect(
                    _worldBounds.X - displayExtraWidth / 2.0,
                    _worldBounds.Y - displayExtraHeight / 2.0,
                    _worldBounds.Width + displayExtraWidth,
                    _worldBounds.Height + displayExtraHeight);
                _worldBounds = _fitWorldBounds;
                _plotViewportRect = outerPlotRect;
            }

            _lastPlotBoundsRect = _plotViewportRect;
            ClampPanOffset();
            var displayCenterX = _fitWorldBounds.Center.X + _panOffset.X;
            var displayCenterY = _fitWorldBounds.Center.Y + _panOffset.Y;
            var displayZoomedWidth = _fitWorldBounds.Width / _zoomFactor;
            var displayZoomedHeight = _fitWorldBounds.Height / _zoomFactor;
            _worldBounds = new Rect(displayCenterX - displayZoomedWidth / 2.0, displayCenterY - displayZoomedHeight / 2.0, displayZoomedWidth, displayZoomedHeight);
            _scale = Math.Min(
                Math.Max(1, _plotViewportRect.Width) / Math.Max(1e-6, _worldBounds.Width),
                Math.Max(1, _plotViewportRect.Height) / Math.Max(1e-6, _worldBounds.Height));

            using (context.PushClip(_plotViewportRect))
            {
                if (ShowGrid)
                    DrawGrid(context, forcedStep: 50);

                DrawDisplayPolylines(context, displayPath, settings);
                DrawDiscreteBSegmentFootprints(context, displayPath, settings);
                DrawFrameOverlayCloud(context, displayPath, settings);
                DrawDisplayPoints(context, displayPath, settings);
                DrawA1Overlay(context, displayPath, settings);
                DrawB0Polyline(context, displayPath, settings);
                DrawAnimatedSegments(context, sample, settings);
            }

            if (ShowLegend)
                DrawDisplayLegend(context, settings);

            return;
        }

        var points = FilterRenderablePoints(Points);
        var showOriginalTarget = ShouldDrawOriginalTarget();
        var showMirroredTarget = ShouldDrawMirroredTarget();

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
            target.Add(new Point(ToVisualX(xp), zp));
        }
        var mirroredTarget = showMirroredTarget ? target.Select(MirrorWorldX).ToList() : new List<Point>();

        var aNozzleSource = (Points?.ToList() ?? points).ToList();
        var robotPoints = points.Where(p => !p.Safe).ToList();
        if (robotPoints.Count == 0)
            robotPoints = points;

        var robotToolMap = robotPoints.ToDictionary(
            p => p,
            p =>
            {
                var targetPoint = ProfileViewGeometry.ResolveDisplayedTargetPoint(p, settings, InvertHorizontal);
                var aNozzle = ProfileViewGeometry.ResolvePointANozzle(p, aNozzleSource, settings);
                return ProfileViewGeometry.ResolvePairGeometry(
                    targetPoint,
                    p.Alfa,
                    p.Place,
                    InvertHorizontal,
                    aNozzle,
                    ResolveDisplayedNozzleLengthMm(settings)).ToolPoint;
            });
        var tool = robotPoints.Select(p => robotToolMap[p]).ToList();

        // Separate set for animation: must match the timeline source (including Safe points when enabled).
        var animSrc = FilterRenderablePoints(AnimationPoints, fallback: points);
        var animTarget = new List<Point>();
        var animTool = new List<Point>();
        for (var idx = 0; idx < animSrc.Count; idx++)
        {
            var p = animSrc[idx];
            var targetPoint = ProfileViewGeometry.ResolveDisplayedTargetPoint(p, settings, InvertHorizontal);
            var aNozzle = ProfileViewGeometry.ResolvePointANozzle(p, animSrc, settings);
            animTarget.Add(targetPoint);
            animTool.Add(ProfileViewGeometry.ResolvePairGeometry(
                targetPoint,
                p.Alfa,
                p.Place,
                InvertHorizontal,
                aNozzle,
                ResolveDisplayedNozzleLengthMm(settings)).ToolPoint);
        }

        var usePhysicalOrientation = NozzleOrientationPolicy.UsePhysicalOrientation(settings.NozzleOrientationMode);
        var selectedPair = !IsPlaying && usePhysicalOrientation
            ? TryResolveSelectedPairGeometry(settings)
            : null;
        var selectedOverlayXs = selectedPair is { } selectedOverlay
            ? new[] { selectedOverlay.TargetPoint.X, selectedOverlay.ToolPoint.X, selectedOverlay.NozzleTipPoint.X }
            : Array.Empty<double>();
        var selectedOverlayYs = selectedPair is { } selectedOverlayY
            ? new[] { selectedOverlayY.TargetPoint.Y, selectedOverlayY.ToolPoint.Y, selectedOverlayY.NozzleTipPoint.Y }
            : Array.Empty<double>();

        // Determine bounds including clamp rectangles
        var dClamp = points[0].Container ? points[0].DClampCont : points[0].DClampForm;
        var halfClamp = Math.Max(10, dClamp / 2.0);

        var hFreeZ = Math.Clamp(settings.HFreeZ, Math.Min(settings.HContMax, settings.HZone), Math.Max(settings.HContMax, settings.HZone));

        var xs = target.Select(p => p.X)
            .Concat(mirroredTarget.Select(p => p.X))
            .Concat(tool.Select(p => p.X))
            .Concat(selectedOverlayXs)
            .Concat(new[] { -halfClamp, halfClamp, 0.0 });
        var ys = target.Select(p => p.Y)
            .Concat(tool.Select(p => p.Y))
            .Concat(selectedOverlayYs)
            .Concat(new[] { 0.0, settings.HZone, settings.HContMax, hFreeZ });

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

        _fitWorldBounds = new Rect(
            _worldBounds.X - extraW / 2.0,
            _worldBounds.Y - extraH / 2.0,
            _worldBounds.Width + extraW,
            _worldBounds.Height + extraH);

        _worldBounds = _fitWorldBounds;

        // Zoom around current center (1.0 = fit-to-data)
        ClampPanOffset();
        var centerX = _fitWorldBounds.Center.X + _panOffset.X;
        var centerY = _fitWorldBounds.Center.Y + _panOffset.Y;
        var zoomedWidth = _fitWorldBounds.Width / _zoomFactor;
        var zoomedHeight = _fitWorldBounds.Height / _zoomFactor;
        _worldBounds = new Rect(centerX - zoomedWidth / 2.0, centerY - zoomedHeight / 2.0, zoomedWidth, zoomedHeight);
        _scale *= _zoomFactor;

        var plotClip = new Rect(_pad, _pad, Math.Max(1, Bounds.Width - 2 * _pad), Math.Max(1, Bounds.Height - 2 * _pad));
        _plotViewportRect = plotClip;
        _lastPlotBoundsRect = plotClip;
        using (context.PushClip(plotClip))
        {
            // Grid
            if (ShowGrid)
                DrawGrid(context);

            // Clamp rectangles (visual reference)
            DrawClamp(context, halfClamp, settings.HContMax, hFreeZ, settings.HZone, showOriginalTarget, showMirroredTarget);

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
                if (showOriginalTarget)
                {
                    DrawPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 0), penTargetWork);
                    DrawPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 1), penTargetWork);
                    DrawWorkTransitionLinks(context, points, settings.HZone, penTargetWork);
                    DrawPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 0), penTargetSafe);
                    DrawPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 1), penTargetSafe);
                }

                if (showMirroredTarget)
                {
                    DrawMirroredTargetPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 0), penTargetWork);
                    DrawMirroredTargetPolyline(context, SelectTarget(points, settings.HZone, safe: false, place: 1), penTargetWork);
                    DrawMirroredWorkTransitionLinks(context, points, settings.HZone, penTargetWork);
                    DrawMirroredTargetPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 0), penTargetSafe);
                    DrawMirroredTargetPolyline(context, SelectTarget(points, settings.HZone, safe: true, place: 1), penTargetSafe);
                }

                if (ShowPairLinks)
                    DrawTargetToToolLinks(context, points, robotToolMap, settings.HZone, penTargetToTool);
            }

            if (settings.PlotShowSmooth)
            {
                var smooth = Spline.CatmullRom(tool, settings.SmoothSegmentsPerSpan);
                DrawPolyline(context, smooth, penTool);
            }

            // Target points are always drawn to keep point markers visible in the editor.
            DrawPoints(context, points, settings, settings.HZone, showOriginalTarget, showMirroredTarget);
            DrawRobotPoints(context, robotPoints, robotToolMap, settings);

            // In the editor, the overlay follows the selected row when it exists.
            Point visibleTarget;
            Point visibleTool;
            Point visibleNozzleTip;
            Point markerDirection;
            if (selectedPair is { } selectedGeometry)
            {
                visibleTarget = selectedGeometry.TargetPoint;
                visibleTool = selectedGeometry.ToolPoint;
                visibleNozzleTip = selectedGeometry.NozzleTipPoint;
                markerDirection = selectedGeometry.TargetToToolDirection;
            }
            else
            {
                var toolState = GetToolState(animTool, animTarget, Progress, CurrentSegmentIndex, CurrentSegmentT);
                if (usePhysicalOrientation)
                {
                    var animatedPair = ResolveAnimatedPairGeometry(animSrc, toolState, settings);
                    visibleTarget = animatedPair.TargetPoint;
                    visibleTool = animatedPair.ToolPoint;
                    visibleNozzleTip = animatedPair.NozzleTipPoint;
                    markerDirection = animatedPair.TargetToToolDirection;
                }
                else
                {
                    var animatedMarker = SimulationOverlayGeometry.ResolvePlotMarkerGeometry(
                        toolState.ToolPosition,
                        toolState.TargetPosition,
                        double.IsFinite(ToolXRaw) && double.IsFinite(ToolZRaw) ? new Point(ToolXRaw, ToolZRaw) : null,
                        double.IsFinite(TargetXRaw) && double.IsFinite(TargetZRaw) ? new Point(TargetXRaw, TargetZRaw) : null,
                        InvertHorizontal,
                        usePhysicalOrientation,
                        default);
                    var visibleNozzle = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
                        animatedMarker,
                        verticalOffsetMm: 0,
                        usePhysicalOrientation,
                        ResolveDisplayedNozzleLengthMm(settings));
                    visibleTarget = visibleNozzle.TargetPoint;
                    visibleTool = visibleNozzle.ToolPoint;
                    visibleNozzleTip = visibleNozzle.NozzleTipPoint;
                    markerDirection = animatedMarker.Direction;
                }
            }
            DrawToolMarker(context, visibleTool, visibleTarget, visibleNozzleTip, markerDirection);
        }

        // Legend
        if (ShowLegend)
            DrawLegend(context);
    }


    private ProfileAnimationSample? TryResolveSelectedDisplaySample(ProfileDisplayPath displayPath)
    {
        if (SelectedPoint is null)
            return null;

        for (var i = 0; i < displayPath.PathNodes.Count; i++)
        {
            if (displayPath.PathNodes[i].NPoint != SelectedPoint.NPoint)
                continue;

            return DisplayPathService.EvaluateAtNode(displayPath, i);
        }

        return null;
    }

    private ProfileDisplayPath ApplyDisplayMirror(ProfileDisplayPath displayPath)
    {
        if (InvertHorizontal || (displayPath.PathNodes.Count == 0 && displayPath.Polylines.Count == 0))
            return displayPath;

        static Point Mirror(Point point) => new(-point.X, point.Y);

        return new ProfileDisplayPath(
            displayPath.Polylines
                .Select(polyline => new ProfilePolylineData(
                    polyline.GroupName,
                    polyline.ControlPoints.Select(Mirror).ToList(),
                    polyline.CurvePoints.Select(Mirror).ToList(),
                    polyline.PointNumbers.ToList()))
                .ToList(),
            displayPath.PathNodes
                .Select(node => node with
                {
                    A0 = Mirror(node.A0),
                    A1 = Mirror(node.A1),
                    B0 = Mirror(node.B0)
                })
                .ToList(),
            displayPath.B0PolylinePoints.Select(Mirror).ToList(),
            displayPath.B0PointNumbers.ToList(),
            displayPath.FrameSamples
                .Select(sample => sample with
                {
                    A1 = Mirror(sample.A1),
                    B0 = Mirror(sample.B0)
                })
                .ToList(),
            displayPath.TotalPathLength,
            displayPath.TotalDurationSec);
    }

    private static Rect ResolveConfiguredProfileViewport(AppSettings settings)
    {
        var width = Math.Max(1, settings.PlotProfileViewportWidth);
        var height = Math.Max(1, settings.PlotProfileViewportHeight);
        return new Rect(settings.PlotProfileViewportMinX, settings.PlotProfileViewportMinY, width, height);
    }

    private static bool IsDisplayGroupVisible(AppSettings settings, string groupName)
    {
        return groupName switch
        {
            var g when g == ProfileDisplayPathService.Group1Name => settings.PlotProfileShowGroup1,
            var g when g == ProfileDisplayPathService.Group2Name => settings.PlotProfileShowGroup2,
            var g when g == ProfileDisplayPathService.Group3Name => settings.PlotProfileShowGroup3,
            var g when g == ProfileDisplayPathService.Group4Name => settings.PlotProfileShowGroup4,
            _ => true
        };
    }

    private static ProfileDisplayPath FilterDisplayPathBySettings(ProfileDisplayPath displayPath, AppSettings settings)
    {
        var polylines = displayPath.Polylines
            .Where(polyline => IsDisplayGroupVisible(settings, polyline.GroupName))
            .ToList();

        var nodeMask = displayPath.PathNodes
            .Select(node => IsDisplayGroupVisible(settings, node.GroupName))
            .ToList();

        var pathNodes = displayPath.PathNodes
            .Where((_, index) => index < nodeMask.Count && nodeMask[index])
            .ToList();

        var b0Polyline = displayPath.B0PolylinePoints
            .Where((_, index) => index < nodeMask.Count && nodeMask[index])
            .ToList();

        var b0Numbers = displayPath.B0PointNumbers
            .Where((_, index) => index < nodeMask.Count && nodeMask[index])
            .ToList();

        var frameSamples = displayPath.FrameSamples
            .Where(frameSample => IsDisplayGroupVisible(settings, frameSample.GroupName))
            .ToList();

        return new ProfileDisplayPath(
            polylines,
            pathNodes,
            b0Polyline,
            b0Numbers,
            frameSamples,
            displayPath.TotalPathLength,
            displayPath.TotalDurationSec);
    }

    private ProfileDisplayPath BuildVisibleDisplayPath(ProfileDisplayPath displayPath)
    {
        var mode = SimulationTargetDisplayModes.Normalize(TargetDisplayMode);
        if (mode == SimulationTargetDisplayModes.Full)
            return displayPath;

        var showMirroredOnly = mode == SimulationTargetDisplayModes.Mirrored;
        var polylines = displayPath.Polylines
            .Where(polyline => showMirroredOnly
                ? polyline.GroupName == ProfileDisplayPathService.Group4Name
                : polyline.GroupName != ProfileDisplayPathService.Group4Name)
            .ToList();

        var nodeMask = displayPath.PathNodes
            .Select(node => showMirroredOnly
                ? node.GroupName == ProfileDisplayPathService.Group4Name
                : node.GroupName != ProfileDisplayPathService.Group4Name)
            .ToList();

        var pathNodes = displayPath.PathNodes
            .Where((_, index) => nodeMask[index])
            .ToList();

        var b0Polyline = displayPath.B0PolylinePoints
            .Where((_, index) => index < nodeMask.Count && nodeMask[index])
            .ToList();

        var b0Numbers = displayPath.B0PointNumbers
            .Where((_, index) => index < nodeMask.Count && nodeMask[index])
            .ToList();

        var frameSamples = displayPath.FrameSamples
            .Where(frameSample => showMirroredOnly
                ? frameSample.GroupName == ProfileDisplayPathService.Group4Name
                : frameSample.GroupName != ProfileDisplayPathService.Group4Name)
            .ToList();

        return new ProfileDisplayPath(
            polylines,
            pathNodes,
            b0Polyline,
            b0Numbers,
            frameSamples,
            displayPath.TotalPathLength,
            displayPath.TotalDurationSec);
    }

    private void DrawDisplayPolylines(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (!settings.PlotProfileShowGroupCurves)
            return;

        var opacity = Math.Clamp(settings.PlotOpacity, 0.05, 0.90);
        var thickness = Math.Max(1, settings.PlotStrokeThickness);

        foreach (var polyline in displayPath.Polylines)
        {
            var color = ResolveProfileGroupColor(settings, polyline.GroupName);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B)), thickness);
            DrawPolyline(ctx, polyline.CurvePoints.ToList(), pen);
        }
    }

    private void DrawDisplayPoints(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (!settings.PlotProfileShowGroupPoints)
            return;

        var outline = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.1);
        var showLabels = settings.PlotProfileShowGroupPointLabels;

        foreach (var polyline in displayPath.Polylines)
        {
            var fill = new SolidColorBrush(ResolveProfileGroupColor(settings, polyline.GroupName));
            for (var i = 0; i < polyline.ControlPoints.Count; i++)
            {
                var screenPoint = WorldToScreen(polyline.ControlPoints[i]);
                ctx.DrawEllipse(fill, outline, screenPoint, 3.3, 3.3);

                if (!showLabels)
                    continue;

                var text = new FormattedText(
                    polyline.PointNumbers[i].ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    10,
                    fill);
                ctx.DrawText(text, new Point(screenPoint.X + 4, screenPoint.Y - 4));
            }
        }
    }

    private void DrawDiscreteBSegmentFootprints(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (!settings.PlotProfileShowBSegmentFootprints)
            return;

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, 154, 154, 154)), 1.0);
        foreach (var node in displayPath.PathNodes)
            ctx.DrawLine(pen, WorldToScreen(node.A1), WorldToScreen(node.B0));
    }

    private void DrawFrameOverlayCloud(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (!settings.PlotProfileShowA1FrameCloud && !settings.PlotProfileShowB0FrameCloud)
            return;

        var a1Brush = new SolidColorBrush(Color.FromArgb(115, 255, 0, 0));
        var b0Brush = new SolidColorBrush(Color.FromArgb(140, 255, 140, 0));

        foreach (var sample in displayPath.FrameSamples)
        {
            if (settings.PlotProfileShowA1FrameCloud)
                ctx.DrawEllipse(a1Brush, null, WorldToScreen(sample.A1), 1.6, 1.6);

            if (settings.PlotProfileShowB0FrameCloud)
                ctx.DrawEllipse(b0Brush, null, WorldToScreen(sample.B0), 1.6, 1.6);
        }
    }

    private void DrawA1Overlay(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (!settings.PlotProfileShowA1Points)
            return;

        var fill = new SolidColorBrush(Colors.Red);
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(139, 0, 0)), 1.4);
        var textBrush = new SolidColorBrush(Color.FromRgb(179, 0, 0));
        var showLabels = settings.PlotProfileShowA1Labels;

        foreach (var node in displayPath.PathNodes)
        {
            var screenPoint = WorldToScreen(node.A1);
            ctx.DrawEllipse(fill, outline, screenPoint, 5.0, 5.0);

            if (!showLabels)
                continue;

            var text = new FormattedText(
                node.NPoint.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                10,
                textBrush);
            ctx.DrawText(text, new Point(screenPoint.X + 6, screenPoint.Y + 12));
        }
    }

    private void DrawB0Polyline(DrawingContext ctx, ProfileDisplayPath displayPath, AppSettings settings)
    {
        if (displayPath.B0PolylinePoints.Count == 0 || (!settings.PlotProfileShowB0PathLine && !settings.PlotProfileShowB0Points))
            return;

        var orange = ParseColorOrDefault(settings.PlotColorProfileB0Path, Color.FromRgb(245, 158, 11));
        if (settings.PlotProfileShowB0PathLine)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(240, orange.R, orange.G, orange.B)), 2.8);
            DrawPolyline(ctx, displayPath.B0PolylinePoints.ToList(), pen);
        }

        for (var i = 0; i < displayPath.B0PolylinePoints.Count; i++)
        {
            var screenPoint = WorldToScreen(displayPath.B0PolylinePoints[i]);
            if (settings.PlotProfileShowB0Points)
            {
                ctx.DrawEllipse(
                    new SolidColorBrush(orange),
                    new Pen(new SolidColorBrush(Color.FromRgb(179, 90, 0)), 1.4),
                    screenPoint,
                    5,
                    5);
            }

            if (!settings.PlotProfileShowB0Labels)
                continue;

            var text = new FormattedText(
                displayPath.B0PointNumbers[i].ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold),
                10,
                new SolidColorBrush(Color.FromRgb(196, 106, 0)));
            ctx.DrawText(text, new Point(screenPoint.X + 6, screenPoint.Y - 6));
        }
    }

    private void DrawAnimatedSegments(DrawingContext ctx, ProfileAnimationSample sample, AppSettings settings)
    {
        if (!sample.IsValid)
            return;

        var colorA = ParseColorOrDefault(settings.PlotColorProfileSegmentA, Color.FromRgb(46, 92, 147));
        var colorB = ParseColorOrDefault(settings.PlotColorProfileSegmentB, Color.FromRgb(239, 68, 68));
        var penA = new Pen(new SolidColorBrush(colorA), 4.5, lineCap: PenLineCap.Round);
        var penB = new Pen(new SolidColorBrush(colorB), 4.5, lineCap: PenLineCap.Round);
        var selectedPoint = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.5);

        var a0 = WorldToScreen(sample.A0);
        var a1 = WorldToScreen(sample.A1);
        var b0 = WorldToScreen(sample.B0);
        var selA0 = WorldToScreen(sample.SelectedA0);

        ctx.DrawLine(penA, a0, a1);
        ctx.DrawLine(penB, a1, b0);

        ctx.DrawEllipse(null, selectedPoint, selA0, 7, 7);

        ctx.DrawEllipse(new SolidColorBrush(colorA), null, a0, 5.5, 5.5);
        ctx.DrawEllipse(Brushes.Black, null, a1, 4.0, 4.0);
        ctx.DrawEllipse(new SolidColorBrush(colorB), null, b0, 5.5, 5.5);

        DrawLabel(ctx, "A0", a0, 18);
        DrawLabel(ctx, "A1", a1, 18);
        DrawLabel(ctx, "B0", b0, 18);

        if (settings.PlotProfileInfoBoxVisible)
            DrawInfoBox(ctx, sample, settings);
    }

    private void DrawInfoBox(DrawingContext ctx, ProfileAnimationSample sample, AppSettings settings)
    {
        var fontFamily = string.IsNullOrWhiteSpace(settings.PlotProfileInfoBoxFontFamily)
            ? "Segoe UI"
            : settings.PlotProfileInfoBoxFontFamily;
        var fontSize = Math.Clamp(settings.PlotProfileInfoBoxFontSize, 9, 28);
        var opacity = Math.Clamp(settings.PlotProfileInfoBoxOpacity, 0.10, 1.0);
        var backgroundColor = ParseColorOrDefault(settings.PlotProfileInfoBoxBackground, Color.FromRgb(243, 244, 246));
        var borderColor = ParseColorOrDefault(settings.PlotProfileInfoBoxBorder, Color.FromRgb(75, 85, 99));
        var textColor = ParseColorOrDefault(settings.PlotProfileInfoBoxTextColor, Color.FromRgb(17, 24, 39));
        var typeface = new Typeface(fontFamily);
        var lines = new[]
        {
            $"A0=({sample.A0.X:0.0}; {sample.A0.Y:0.0}) mm",
            $"i={sample.NPoint}",
            $"\u0438-\u0442\u043e\u0447\u043a\u0430=({sample.SelectedA0.X:0.0}; {sample.SelectedA0.Y:0.0}) mm",
            $"\u03B1={sample.AlfaDisplay:0.0}\u00B0, \u03B2={sample.Beta:0.0}\u00B0"
        };

        var formattedLines = lines
            .Select(line => new FormattedText(
                line,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                new SolidColorBrush(textColor)))
            .ToList();

        var lineHeight = Math.Max(15.0, fontSize + 3);
        var boxW = Math.Max(190.0, formattedLines.Max(text => text.Width) + 20);
        var boxH = formattedLines.Count * lineHeight + 18;
        var plotBounds = GetPlotBoundsRect();
        var a0Screen = WorldToScreen(sample.A0);
        double tx;
        double ty;

        if (settings.PlotProfileInfoBoxFollowA0)
        {
            tx = a0Screen.X + 12;
            ty = a0Screen.Y - 12;

            if (tx + boxW > plotBounds.Right - 6)
                tx = a0Screen.X - boxW - 12;
            if (ty - 14 < plotBounds.Top + 6)
                ty = a0Screen.Y + 18;
            if (ty + boxH > plotBounds.Bottom - 6)
                ty = plotBounds.Bottom - boxH - 6;
        }
        else
        {
            tx = plotBounds.Left + 12 + Math.Clamp(settings.PlotProfileInfoBoxManualX, 0.0, 1.0) * Math.Max(0, plotBounds.Width - boxW - 18);
            ty = plotBounds.Top + 14 + Math.Clamp(settings.PlotProfileInfoBoxManualY, 0.0, 1.0) * Math.Max(0, plotBounds.Height - boxH - 20);
        }

        var rectX = Math.Clamp(tx - 6, plotBounds.Left + 6, Math.Max(plotBounds.Left + 6, plotBounds.Right - boxW - 6));
        var rectY = Math.Clamp(ty - 14, plotBounds.Top + 6, Math.Max(plotBounds.Top + 6, plotBounds.Bottom - boxH - 6));

        var boxRect = new Rect(rectX, rectY, boxW, boxH);
        _lastInfoBoxRect = boxRect;
        _lastInfoBoxSize = boxRect.Size;
        _hasInfoBoxRect = true;

        ctx.FillRectangle(
            new SolidColorBrush(Color.FromArgb((byte)Math.Round(opacity * 255), backgroundColor.R, backgroundColor.G, backgroundColor.B)),
            boxRect,
            6);
        ctx.DrawRectangle(
            new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Round(Math.Min(1.0, opacity + 0.05) * 255), borderColor.R, borderColor.G, borderColor.B)), 1),
            boxRect,
            6);

        for (var i = 0; i < formattedLines.Count; i++)
            ctx.DrawText(formattedLines[i], new Point(boxRect.X + 10, boxRect.Y + 8 + i * lineHeight));
    }

    private static void DrawLabel(DrawingContext ctx, string label, Point point, double dy)
    {
        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            Brushes.White);
        ctx.DrawText(text, new Point(point.X - text.Width / 2.0, point.Y + dy - text.Height / 2.0));
    }

    private Rect GetPlotBoundsRect()
    {
        if (_lastPlotBoundsRect.Width > 0 && _lastPlotBoundsRect.Height > 0)
            return _lastPlotBoundsRect;

        return new Rect(_pad, _pad, Math.Max(1, Bounds.Width - 2 * _pad), Math.Max(1, Bounds.Height - 2 * _pad));
    }

    private void DrawDisplayLegend(DrawingContext ctx, AppSettings settings)
    {
        var x = _pad + 4;
        var y = _pad - 6;
        var lineH = 16;
        var legendWidth = 220;
        var entries = 4;
        var legendHeight = entries * lineH + 8;
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(110, 2, 6, 23)), new Rect(x - 8, y - 6, legendWidth, legendHeight));

        void Entry(Color c, string text)
        {
            ctx.FillRectangle(new SolidColorBrush(c), new Rect(x, y + 4, 10, 10));
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.White);
            ctx.DrawText(ft, new Point(x + 16, y));
            y += lineH;
        }

        Entry(ResolveProfileGroupColor(settings, ProfileDisplayPathService.Group1Name), "Группы 1 и 4 / A");
        Entry(ResolveProfileGroupColor(settings, ProfileDisplayPathService.Group2Name), "Группы 2 и 3");
        Entry(ParseColorOrDefault(settings.PlotColorProfileB0Path, Color.FromRgb(245, 158, 11)), "Траектория B0");
        Entry(ParseColorOrDefault(settings.PlotColorProfileSegmentB, Color.FromRgb(239, 68, 68)), "Сегмент B");
    }

    private static Color ResolveProfileGroupColor(AppSettings settings, string groupName)
    {
        return groupName switch
        {
            ProfileDisplayPathService.Group1Name => ParseColorOrDefault(settings.PlotColorProfileGroup1, Color.FromRgb(46, 92, 147)),
            ProfileDisplayPathService.Group2Name => ParseColorOrDefault(settings.PlotColorProfileGroup2, Color.FromRgb(126, 135, 156)),
            ProfileDisplayPathService.Group3Name => ParseColorOrDefault(settings.PlotColorProfileGroup3, Color.FromRgb(126, 135, 156)),
            ProfileDisplayPathService.Group4Name => ParseColorOrDefault(settings.PlotColorProfileGroup4, Color.FromRgb(46, 92, 147)),
            _ => ParseColorOrDefault(settings.PlotColorProfileGroup1, Color.FromRgb(46, 92, 147))
        };
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

    private void DrawGrid(DrawingContext ctx, double? forcedStep = null)
    {
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)), 1);
        var axisPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)), 1);

        // choose a step (world units) based on visible range
        var range = Math.Max(_worldBounds.Width, _worldBounds.Height);
        var step = forcedStep ?? range switch
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

    private void DrawClamp(DrawingContext ctx, double halfClamp, double hCont, double hFreeZ, double hZone, bool showOriginalTarget, bool showMirroredTarget)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 2);
        var penDashed = new Pen(new SolidColorBrush(Color.FromArgb(120, 148, 163, 184)), 1,
            dashStyle: new DashStyle(new double[] { 6, 6 }, 0));

        if (showOriginalTarget)
        {
            var r1 = new Rect(
                WorldToScreen(new Point(0, 0)),
                WorldToScreen(new Point(halfClamp, hCont))).Normalize();
            ctx.DrawRectangle(null, pen, r1);

            var r2 = new Rect(
                WorldToScreen(new Point(-halfClamp, hFreeZ)),
                WorldToScreen(new Point(0, hZone))).Normalize();
            ctx.DrawRectangle(null, pen, r2);
        }

        if (showMirroredTarget)
        {
            var r1Mirror = new Rect(
                WorldToScreen(new Point(-halfClamp, 0)),
                WorldToScreen(new Point(0, hCont))).Normalize();
            ctx.DrawRectangle(null, pen, r1Mirror);

            var r2Mirror = new Rect(
                WorldToScreen(new Point(0, hFreeZ)),
                WorldToScreen(new Point(halfClamp, hZone))).Normalize();
            ctx.DrawRectangle(null, pen, r2Mirror);
        }

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

    private void DrawPoints(DrawingContext ctx, IList<RecipePoint> points, AppSettings settings, double hZone, bool showOriginalTarget, bool showMirroredTarget)
    {
        var r = Math.Max(4, settings.PlotPointRadius);

        foreach (var p in points)
        {
            var (xp, zp) = p.GetTargetPoint(hZone);
            var sp = WorldToScreen(new Point(ToVisualX(xp), zp));

            // Working vs safety colors
            var color = p.Safe
                ? ParseColorOrDefault(settings.PlotColorSafetyZone, Color.FromRgb(156, 163, 175))
                : ParseColorOrDefault(settings.PlotColorWorkingZone, Color.FromRgb(34, 197, 94));

            var brush = new SolidColorBrush(color);

            var outline = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.2);
            var mirrored = WorldToScreen(MirrorWorldX(new Point(ToVisualX(xp), zp)));
            if (showOriginalTarget)
                ctx.DrawEllipse(brush, outline, sp, r, r);

            if (showMirroredTarget)
                ctx.DrawEllipse(brush, outline, mirrored, r, r);

            if (p == SelectedPoint)
            {
                var selPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2);
                if (showOriginalTarget)
                    ctx.DrawEllipse(null, selPen, sp, r + 3, r + 3);

                if (showMirroredTarget)
                    ctx.DrawEllipse(null, selPen, mirrored, r + 3, r + 3);
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
    private List<Point> SelectTarget(IList<RecipePoint> points, double hZone, bool safe, int place)
        => points
            .Where(p => p.Safe == safe && p.Place == place)
            .Select(p =>
            {
                var (xp, zp) = p.GetTargetPoint(hZone);
                return new Point(ToVisualX(xp), zp);
            })
            .ToList();

    private void DrawMirroredTargetPolyline(DrawingContext ctx, IList<Point> worldPoints, Pen pen)
        => DrawPolyline(ctx, worldPoints.Select(MirrorWorldX).ToList(), pen);

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
            ctx.DrawLine(pen, WorldToScreen(new Point(ToVisualX(x1), z1)), WorldToScreen(new Point(ToVisualX(x2), z2)));
        }
    }

    private void DrawMirroredWorkTransitionLinks(DrawingContext ctx, IList<RecipePoint> points, double hZone, Pen pen)
    {
        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1];
            var cur = points[i];

            if (prev.Safe || cur.Safe || prev.Place == cur.Place)
                continue;

            var (x1, z1) = prev.GetTargetPoint(hZone);
            var (x2, z2) = cur.GetTargetPoint(hZone);
            var p1 = MirrorWorldX(new Point(ToVisualX(x1), z1));
            var p2 = MirrorWorldX(new Point(ToVisualX(x2), z2));
            ctx.DrawLine(pen, WorldToScreen(p1), WorldToScreen(p2));
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

            ctx.DrawLine(pen, WorldToScreen(new Point(ToVisualX(xp), zp)), WorldToScreen(toolPoint));
        }
    }

    private static Point GetToolPointForPlot(RecipePoint point, AppSettings _)
    {
        var xr = point.Xr0 + point.DX;
        var zr = point.Zr0 + point.DZ;
        return new Point(xr, zr);
    }

    private static Point MirrorWorldX(Point point)
        => new(-point.X, point.Y);

    private ProfilePairGeometry? TryResolveSelectedPairGeometry(AppSettings settings)
    {
        if (SelectedPoint is null)
            return null;

        var source = Points?.ToList() ?? new List<RecipePoint>();
        var index = FindPointIndex(source, SelectedPoint);
        if (index < 0)
        {
            source = new List<RecipePoint> { SelectedPoint };
            index = 0;
        }

        if (source.Count == 0)
            return null;

        var selected = source[index];
        var targetPoint = ProfileViewGeometry.ResolveDisplayedTargetPoint(selected, settings, InvertHorizontal);
        var aNozzle = ProfileViewGeometry.ResolvePointANozzle(selected, source, settings);
        return ProfileViewGeometry.ResolvePairGeometry(
            targetPoint,
            selected.Alfa,
            selected.Place,
            InvertHorizontal,
            aNozzle,
            ResolveDisplayedNozzleLengthMm(settings));
    }

    private ProfilePairGeometry ResolveAnimatedPairGeometry(
        IList<RecipePoint> source,
        (Point ToolPosition, Point TargetPosition, Point Direction, Point ToolSegmentDirection, int SegmentIndex, double SegmentT) toolState,
        AppSettings settings)
    {
        if (source.Count == 0)
        {
            var target = toolState.TargetPosition;
            return ProfileViewGeometry.ResolvePairGeometry(
                target,
                CurrentAlfa,
                place: 0,
                InvertHorizontal,
                aNozzleMm: 0,
                ResolveDisplayedNozzleLengthMm(settings));
        }

        var segmentIndex = Math.Clamp(toolState.SegmentIndex, 0, Math.Max(0, source.Count - 2));
        var segmentT = Math.Clamp(toolState.SegmentT, 0.0, 1.0);
        var alfa = source.Count == 1
            ? source[0].Alfa
            : source[segmentIndex].Alfa + (source[Math.Min(source.Count - 1, segmentIndex + 1)].Alfa - source[segmentIndex].Alfa) * segmentT;
        var place = ResolveCurrentPlace(source, segmentIndex, segmentT);
        var aNozzle = ProfileViewGeometry.ResolveInterpolatedANozzle(source, settings, segmentIndex, segmentT);

        return ProfileViewGeometry.ResolvePairGeometry(
            toolState.TargetPosition,
            alfa,
            place,
            InvertHorizontal,
            aNozzle,
            ResolveDisplayedNozzleLengthMm(settings));
    }

    private static int FindPointIndex(IList<RecipePoint> source, RecipePoint selectedPoint)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (ReferenceEquals(source[i], selectedPoint))
                return i;
        }

        for (var i = 0; i < source.Count; i++)
        {
            if (source[i].NPoint == selectedPoint.NPoint)
                return i;
        }

        return -1;
    }

    private bool ShouldDrawOriginalTarget()
        => SimulationTargetDisplayModes.Normalize(TargetDisplayMode) != SimulationTargetDisplayModes.Mirrored;

    private bool ShouldDrawMirroredTarget()
        => SimulationTargetDisplayModes.Normalize(TargetDisplayMode) != SimulationTargetDisplayModes.Original;

    private static double ResolveDisplayedNozzleLengthMm(AppSettings settings)
        => Math.Max(1e-6, Math.Abs(settings.Lz));

    private void DrawToolMarker(DrawingContext ctx, Point toolWorld, Point targetWorld, Point nozzleTipWorld, Point direction)
    {
        var toolSp = WorldToScreen(toolWorld);
        var targetSp = WorldToScreen(targetWorld);
        var nozzleTipSp = WorldToScreen(nozzleTipWorld);

        var toolColor = ParseColorOrDefault((Settings ?? new AppSettings()).PlotColorTool, Color.FromRgb(239, 68, 68));
        var gapColor = Color.FromRgb(65, 105, 225);
        var gapPen = new Pen(new SolidColorBrush(Color.FromArgb(220, gapColor.R, gapColor.G, gapColor.B)), 5);
        var linkPen = new Pen(new SolidColorBrush(Color.FromArgb(230, toolColor.R, toolColor.G, toolColor.B)), 7);
        ctx.DrawLine(gapPen, targetSp, nozzleTipSp);
        ctx.DrawLine(linkPen, nozzleTipSp, toolSp);

        // Base joint on robot side
        ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(10, 16, 30)), new Pen(new SolidColorBrush(toolColor), 2), toolSp, 6, 6);

        // Visible nozzle tip at displayed L distance from the robot-side joint.
        ctx.DrawEllipse(new SolidColorBrush(toolColor), new Pen(Brushes.White, 1.2), nozzleTipSp, 4.2, 4.2);

        // Keep current target anchored to the actual recipe contour.
        if (Math.Abs(nozzleTipSp.X - targetSp.X) > 1.5 || Math.Abs(nozzleTipSp.Y - targetSp.Y) > 1.5)
            ctx.DrawEllipse(null, new Pen(new SolidColorBrush(toolColor), 1.4), targetSp, 4.6, 4.6);

        // Tiny direction arrow to emphasize smooth rotation
        var len = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        var dirWorld = len <= 1e-6 ? new Point(1, 0) : new Point(direction.X / len, direction.Y / len);
        var dir = new Point(dirWorld.X, -dirWorld.Y);
        var perp = new Point(-dir.Y, dir.X);
        var tip = new Point(nozzleTipSp.X + dir.X * 10, nozzleTipSp.Y + dir.Y * 10);
        var left = new Point(nozzleTipSp.X - dir.X * 3 + perp.X * 3, nozzleTipSp.Y - dir.Y * 3 + perp.Y * 3);
        var right = new Point(nozzleTipSp.X - dir.X * 3 - perp.X * 3, nozzleTipSp.Y - dir.Y * 3 - perp.Y * 3);

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

    private static Rect FitAspectRect(Rect outerRect, double aspectRatio)
    {
        var outerWidth = Math.Max(1.0, outerRect.Width);
        var outerHeight = Math.Max(1.0, outerRect.Height);
        var fittedWidth = outerHeight * aspectRatio;
        var fittedHeight = outerHeight;

        if (fittedWidth > outerWidth)
        {
            fittedWidth = outerWidth;
            fittedHeight = outerWidth / aspectRatio;
        }

        return new Rect(
            outerRect.X + (outerWidth - fittedWidth) / 2.0,
            outerRect.Y + (outerHeight - fittedHeight) / 2.0,
            fittedWidth,
            fittedHeight);
    }

    private Point WorldToScreen(Point w)
    {
        // X: left->right, Z(Y): bottom->top
        var viewport = _plotViewportRect.Width > 0 && _plotViewportRect.Height > 0
            ? _plotViewportRect
            : new Rect(_pad, _pad, Math.Max(1, Bounds.Width - 2 * _pad), Math.Max(1, Bounds.Height - 2 * _pad));
        var x = viewport.Left + (w.X - _worldBounds.Left) * _scale;
        var y = viewport.Top + (_worldBounds.Bottom - w.Y) * _scale;
        return new Point(x, y);
    }

    private Point ScreenToWorld(Point s)
    {
        var viewport = _plotViewportRect.Width > 0 && _plotViewportRect.Height > 0
            ? _plotViewportRect
            : new Rect(_pad, _pad, Math.Max(1, Bounds.Width - 2 * _pad), Math.Max(1, Bounds.Height - 2 * _pad));
        var x = (s.X - viewport.Left) / _scale + _worldBounds.Left;
        var y = _worldBounds.Bottom - (s.Y - viewport.Top) / _scale;
        return new Point(InvertHorizontal ? -x : x, y);
    }

    private static (Point ToolPosition, Point TargetPosition, Point Direction, Point ToolSegmentDirection, int SegmentIndex, double SegmentT) GetToolState(
        IList<Point> tool,
        IList<Point> targetPts,
        double progress,
        int segmentIndexHint,
        double segmentTHint)
    {
        var pairCount = Math.Min(tool.Count, targetPts.Count);
        if (pairCount == 0)
            return (default, default, new Point(1, 0), new Point(1, 0), 0, 0);

        if (pairCount == 1)
            return (tool[0], targetPts[0], new Point(1, 0), new Point(1, 0), 0, 0);

        var maxSeg = pairCount - 2;
        if (segmentIndexHint >= 0 && maxSeg >= 0)
        {
            var seg = Math.Clamp(segmentIndexHint, 0, maxSeg);
            var t = Math.Clamp(segmentTHint, 0, 1);
            var x = tool[seg].X + (tool[seg + 1].X - tool[seg].X) * t;
            var y = tool[seg].Y + (tool[seg + 1].Y - tool[seg].Y) * t;
            var tx = targetPts[seg].X + (targetPts[seg + 1].X - targetPts[seg].X) * t;
            var ty = targetPts[seg].Y + (targetPts[seg + 1].Y - targetPts[seg].Y) * t;
            var dir = new Point(tx - x, ty - y);
            var travel = new Point(tool[seg + 1].X - tool[seg].X, tool[seg + 1].Y - tool[seg].Y);
            return (new Point(x, y), new Point(tx, ty), dir, travel, seg, t);
        }

        progress = Math.Clamp(progress, 0, 1);

        // length along polyline
        double total = 0;
        var segLengths = new double[pairCount - 1];
        for (var i = 0; i < pairCount - 1; i++)
        {
            var dx = tool[i + 1].X - tool[i].X;
            var dy = tool[i + 1].Y - tool[i].Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            segLengths[i] = d;
            total += d;
        }

        if (total <= 1e-9)
        {
            var tail = pairCount - 1;
            var fallbackTravel = new Point(tool[tail].X - tool[0].X, tool[tail].Y - tool[0].Y);
            return (tool[0], targetPts[0], fallbackTravel, fallbackTravel, 0, 0);
        }

        var targetLen = total * progress;
        double acc = 0;
        for (var i = 0; i < segLengths.Length; i++)
        {
            var next = acc + segLengths[i];
            if (targetLen <= next)
            {
                var t = (targetLen - acc) / Math.Max(1e-9, segLengths[i]);
                var x = tool[i].X + (tool[i + 1].X - tool[i].X) * t;
                var y = tool[i].Y + (tool[i + 1].Y - tool[i].Y) * t;
                var tx = targetPts[i].X + (targetPts[i + 1].X - targetPts[i].X) * t;
                var ty = targetPts[i].Y + (targetPts[i + 1].Y - targetPts[i].Y) * t;
                var dir = new Point(tx - x, ty - y);
                var travel = new Point(tool[i + 1].X - tool[i].X, tool[i + 1].Y - tool[i].Y);
                return (new Point(x, y), new Point(tx, ty), dir, travel, i, t);
            }
            acc = next;
        }

        var last = pairCount - 1;
        var fallbackDir = new Point(targetPts[last].X - tool[last].X, targetPts[last].Y - tool[last].Y);
        var fallbackTravelDir = new Point(tool[last].X - tool[last - 1].X, tool[last].Y - tool[last - 1].Y);
        return (tool[last], targetPts[last], fallbackDir, fallbackTravelDir, Math.Max(0, pairCount - 2), 1);
    }

    private Point ApplyTransitionLiftOrientation(
        IList<RecipePoint> animSrc,
        IList<Point> animTool,
        (Point ToolPosition, Point TargetPosition, Point Direction, Point ToolSegmentDirection, int SegmentIndex, double SegmentT) toolState,
        AppSettings settings)
    {
        var currentPlace = ResolveCurrentPlace(animSrc, toolState.SegmentIndex, toolState.SegmentT);
        var currentProjected = GetPhysicalProjectedVector(settings, CurrentAlfa, CurrentBetta, currentPlace);
        var transition = IsTransitionSegment(animSrc, animTool, toolState.SegmentIndex);
        if (!transition)
            return currentProjected;

        if (toolState.SegmentIndex < 0 || toolState.SegmentIndex >= animSrc.Count - 1)
            return currentProjected;

        var a = animSrc[toolState.SegmentIndex];
        var b = animSrc[toolState.SegmentIndex + 1];
        var startProjected = GetPhysicalProjectedVector(settings, a.Alfa, a.Betta, a.Place);
        var endProjected = GetPhysicalProjectedVector(settings, b.Alfa, b.Betta, b.Place);

        var t = SmoothStep(Math.Clamp(toolState.SegmentT, 0, 1));
        var blendedDirection = InterpolateDirectionByAngle(startProjected, endProjected, t);
        var blendedMagnitude = Lerp(VectorLength(startProjected), VectorLength(endProjected), t);
        var blended = new Point(blendedDirection.X * blendedMagnitude, blendedDirection.Y * blendedMagnitude);
        return VectorLength(blended) <= 1e-6 ? currentProjected : blended;
    }

    private static int ResolveCurrentPlace(IList<RecipePoint> animSrc, int segmentIndex, double segmentT)
    {
        if (animSrc.Count == 0)
            return 0;

        var seg = Math.Clamp(segmentIndex, 0, Math.Max(0, animSrc.Count - 2));
        if (seg >= animSrc.Count - 1)
            return animSrc[^1].Place;

        return segmentT >= 0.5 ? animSrc[seg + 1].Place : animSrc[seg].Place;
    }

    private static Point GetPhysicalProjectedVector(AppSettings settings, double alfaDeg, double bettaDeg, int place)
    {
        var a = alfaDeg * Math.PI / 180.0;
        var b = bettaDeg * Math.PI / 180.0;
        var x = Math.Cos(b) * Math.Cos(a);
        // Excel CALC/SAVE uses mirrored sign of angular Z component for Place=1.
        var zSign = place == 0 ? -1.0 : 1.0;
        var z = zSign * Math.Cos(b) * Math.Sin(a);
        return new Point(x, z);
    }

    private static bool IsTransitionSegment(IList<RecipePoint> animSrc, IList<Point> animTool, int segmentIndex)
    {
        if (segmentIndex < 0)
            return false;

        if (segmentIndex >= animSrc.Count - 1 || segmentIndex >= animTool.Count - 1)
            return false;

        var a = animSrc[segmentIndex];
        var b = animSrc[segmentIndex + 1];
        if (a.Place != b.Place || a.Safe != b.Safe)
            return true;

        var dx = animTool[segmentIndex + 1].X - animTool[segmentIndex].X;
        var dz = animTool[segmentIndex + 1].Y - animTool[segmentIndex].Y;
        return Math.Sqrt(dx * dx + dz * dz) >= 180;
    }

    private static Point NormalizeDirection(Point dir, Point fallback)
    {
        var len = Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (len <= 1e-6)
            return fallback;

        return new Point(dir.X / len, dir.Y / len);
    }

    private static Point InterpolateDirectionByAngle(Point from, Point to, double t)
    {
        var a = NormalizeDirection(from, new Point(1, 0));
        var b = NormalizeDirection(to, a);
        var a0 = Math.Atan2(a.Y, a.X);
        var a1 = Math.Atan2(b.Y, b.X);
        var d = NormalizeRadiansPi(a1 - a0);
        var angle = a0 + d * t;
        return new Point(Math.Cos(angle), Math.Sin(angle));
    }

    private static double NormalizeRadiansPi(double angle)
    {
        while (angle > Math.PI)
            angle -= Math.PI * 2;
        while (angle < -Math.PI)
            angle += Math.PI * 2;
        return angle;
    }

    private static double SmoothStep(double x)
        => x * x * (3 - 2 * x);

    private static double VectorLength(Point value)
        => Math.Sqrt(value.X * value.X + value.Y * value.Y);

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;

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
        var isLeftButtonPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;

        if (isLeftButtonPressed && settings.PlotProfileInfoBoxVisible && _hasInfoBoxRect && _lastInfoBoxRect.Contains(pos))
        {
            _isDraggingInfoBox = true;
            _infoBoxDragStartScreen = pos;
            _infoBoxDragStartRect = _lastInfoBoxRect;
            settings.PlotProfileInfoBoxFollowA0 = false;
            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        // select nearest target point
        var hit = HitTestTargetPoint(pos, settings);
        if (hit is not null)
            SelectedPoint = hit;

        if (isLeftButtonPressed)
        {
            _isPanning = true;
            _panStartScreen = pos;
            _panStartOffset = _panOffset;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var pos = e.GetPosition(this);
        var isLeftButtonPressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
        var settings = Settings ?? new AppSettings();

        if (_isDraggingInfoBox && !isLeftButtonPressed)
        {
            StopPointerInteraction(e.Pointer);
            return;
        }

        if (_isDraggingInfoBox)
        {
            var plotBounds = GetPlotBoundsRect();
            var dragDelta = pos - _infoBoxDragStartScreen;
            var rectX = Math.Clamp(
                _infoBoxDragStartRect.X + dragDelta.X,
                plotBounds.Left + 6,
                Math.Max(plotBounds.Left + 6, plotBounds.Right - _infoBoxDragStartRect.Width - 6));
            var rectY = Math.Clamp(
                _infoBoxDragStartRect.Y + dragDelta.Y,
                plotBounds.Top + 6,
                Math.Max(plotBounds.Top + 6, plotBounds.Bottom - _infoBoxDragStartRect.Height - 6));
            var availableWidth = Math.Max(1.0, plotBounds.Width - _infoBoxDragStartRect.Width - 12);
            var availableHeight = Math.Max(1.0, plotBounds.Height - _infoBoxDragStartRect.Height - 12);

            settings.PlotProfileInfoBoxManualX = Math.Clamp((rectX - plotBounds.Left - 6) / availableWidth, 0.0, 1.0);
            settings.PlotProfileInfoBoxManualY = Math.Clamp((rectY - plotBounds.Top - 6) / availableHeight, 0.0, 1.0);
            InvalidateVisual();
            return;
        }

        if (_isPanning && !isLeftButtonPressed)
        {
            StopPointerInteraction(e.Pointer);
            return;
        }

        if (!_isPanning)
            return;

        var delta = pos - _panStartScreen;
        var worldDx = -delta.X / _scale;
        var worldDy = delta.Y / _scale;
        _panOffset = new Point(_panStartOffset.X + worldDx, _panStartOffset.Y + worldDy);
        ClampPanOffset();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isPanning)
            StopPointerInteraction(e.Pointer);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        StopPointerInteraction(e.Pointer);
    }

    private void StopPointerInteraction(IPointer pointer)
    {
        var wasDraggingInfoBox = _isDraggingInfoBox;
        _isDraggingInfoBox = false;
        _isPanning = false;
        pointer.Capture(null);

        if (wasDraggingInfoBox)
            InfoBoxPositionChanged?.Invoke();
    }

    private void ClampPanOffset()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
        {
            _panOffset = default;
            return;
        }

        var safeZoom = Math.Clamp(_zoomFactor, 0.2, 20.0);
        var visibleWidth = _fitWorldBounds.Width / safeZoom;
        var visibleHeight = _fitWorldBounds.Height / safeZoom;

        var zoomPanX = Math.Max(0, (_fitWorldBounds.Width - visibleWidth) / 2.0);
        var zoomPanY = Math.Max(0, (_fitWorldBounds.Height - visibleHeight) / 2.0);

        // Allow panning even at fit zoom (x1.00): user can shift the profile with LMB
        // while keeping movement bounded to a reasonable range.
        var fitPanX = _fitWorldBounds.Width * 0.35;
        var fitPanY = _fitWorldBounds.Height * 0.35;

        var maxOffsetX = Math.Max(zoomPanX, fitPanX);
        var maxOffsetY = Math.Max(zoomPanY, fitPanY);

        _panOffset = new Point(
            Math.Clamp(_panOffset.X, -maxOffsetX, maxOffsetX),
            Math.Clamp(_panOffset.Y, -maxOffsetY, maxOffsetY));
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
            var sp = WorldToScreen(new Point(ToVisualX(xp), zp));
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

    private double ToVisualX(double x) => InvertHorizontal ? -x : x;

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
