using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.Controls;

public sealed class SimulationBlueprint2DControl : Control
{
    public const double DefaultNozzleAnchorX = 0.04;
    public const double DefaultNozzleAnchorY = 0.50;
    public const double DefaultManipulatorAnchorX = 0.04;
    public const double DefaultManipulatorAnchorY = 0.90;
    public const double DefaultVerticalOffsetMm = 240.0;
    private const double NozzlePivotAnchorX = 0.84;
    private const double NozzleMinStretch = 0.35;
    private const double NozzleMaxStretch = 4.0;

    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(Progress));

    public static readonly StyledProperty<double> ToolXRawProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ToolXRaw));

    public static readonly StyledProperty<double> ToolZRawProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ToolZRaw));

    public static readonly StyledProperty<double> TargetXRawProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(TargetXRaw));

    public static readonly StyledProperty<double> TargetZRawProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(TargetZRaw));

    public static readonly StyledProperty<AppSettings?> SettingsProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, AppSettings?>(nameof(Settings));

    public static readonly StyledProperty<double> CurrentAlfaProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(CurrentAlfa));

    public static readonly StyledProperty<double> ReferenceHeightMmProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ReferenceHeightMm), 1309.49);

    public static readonly StyledProperty<bool> InvertHorizontalProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, bool>(nameof(InvertHorizontal), true);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> NozzleAnchorXProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(NozzleAnchorX), DefaultNozzleAnchorX);

    public static readonly StyledProperty<double> ManipulatorAnchorXProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ManipulatorAnchorX), DefaultManipulatorAnchorX);

    public static readonly StyledProperty<double> NozzleAnchorYProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(NozzleAnchorY), DefaultNozzleAnchorY);

    public static readonly StyledProperty<double> ManipulatorAnchorYProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ManipulatorAnchorY), DefaultManipulatorAnchorY);

    public static readonly StyledProperty<double> VerticalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(VerticalOffsetMm), DefaultVerticalOffsetMm);

    private const double Pad = 20;
    private Rect _fitWorldBounds;
    private Rect _worldBounds;
    private double _scale;
    private double _fitScale;
    private double _zoomFactor = 1.0;
    private bool _needsRefit = true;
    private Size _lastRenderSize;
    private Point _panOffset;
    private bool _isPanning;
    private Point _panStartScreen;
    private Point _panStartOffset;
    private bool _panWithRightButton;

    private readonly Bitmap? _partImage;
    private readonly Bitmap? _manipulatorImage;
    private readonly Bitmap? _nozzleImage;

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

    public double ReferenceHeightMm
    {
        get => GetValue(ReferenceHeightMmProperty);
        set => SetValue(ReferenceHeightMmProperty, value);
    }

    public bool InvertHorizontal
    {
        get => GetValue(InvertHorizontalProperty);
        set => SetValue(InvertHorizontalProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public double NozzleAnchorX
    {
        get => GetValue(NozzleAnchorXProperty);
        set => SetValue(NozzleAnchorXProperty, value);
    }

    public double ManipulatorAnchorX
    {
        get => GetValue(ManipulatorAnchorXProperty);
        set => SetValue(ManipulatorAnchorXProperty, value);
    }

    public double NozzleAnchorY
    {
        get => GetValue(NozzleAnchorYProperty);
        set => SetValue(NozzleAnchorYProperty, value);
    }

    public double ManipulatorAnchorY
    {
        get => GetValue(ManipulatorAnchorYProperty);
        set => SetValue(ManipulatorAnchorYProperty, value);
    }

    public double VerticalOffsetMm
    {
        get => GetValue(VerticalOffsetMmProperty);
        set => SetValue(VerticalOffsetMmProperty, value);
    }

    public double ZoomFactor => _zoomFactor;
    public event Action<double>? ZoomChanged;

    static SimulationBlueprint2DControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ProgressProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        ToolXRawProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        ToolZRawProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        TargetXRawProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        TargetZRawProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        CurrentAlfaProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        ReferenceHeightMmProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        InvertHorizontalProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ShowGridProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        NozzleAnchorXProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorXProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        NozzleAnchorYProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorYProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        VerticalOffsetMmProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
    }

    public SimulationBlueprint2DControl()
    {
        ClipToBounds = true;
        _partImage = TryLoadBitmap("avares://RecipeStudio.Desktop/Assets/Images/H340_KAMA_1.fw.png");
        _manipulatorImage = TryLoadBitmap("avares://RecipeStudio.Desktop/Assets/Images/manipulator.fw.png");
        _nozzleImage = TryLoadBitmap("avares://RecipeStudio.Desktop/Assets/Images/soplo.fw.png");
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
        _needsRefit = true;
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(18, 22, 30)), Bounds);

        var points = SelectRenderablePoints(Points?.ToList() ?? new List<RecipePoint>());
        var hZone = Settings?.HZone ?? 1200;
        var pathSource = SelectPathSource(points);
        var path = BuildDisplayPath(pathSource, hZone);
        var verticalOffset = VerticalOffsetMm;
        if (Math.Abs(verticalOffset) > 1e-6)
            path = path.Select(p => new Point(p.X, p.Y + verticalOffset)).ToList();

        var timelineProgress = Math.Clamp(Progress, 0.0, 1.0);
        const double approachPhase = 0.15;
        var processProgress = timelineProgress <= approachPhase
            ? 0.0
            : (timelineProgress - approachPhase) / (1.0 - approachPhase);
        var state = Interpolate(path, processProgress);
        var segmentSafety = BuildSegmentSafety(pathSource);
        var currentPose = path.Count >= 2
            ? state.Position
            : (double.IsFinite(TargetXRaw) && double.IsFinite(TargetZRaw)
                ? new Point(ToVisualX(TargetXRaw), TargetZRaw)
                : state.Position);
        var calibratedPose = new Point(currentPose.X, currentPose.Y);

        var referenceHeightMm = Math.Max(100, ReferenceHeightMm);
        var mmPerPixel = ResolveMmPerPixel(referenceHeightMm);

        var partRectWorld = CreateWorldRectCenteredAtX(0, 0, _partImage, mmPerPixel, referenceHeightMm);
        var nozzleDirection = ResolveNozzleDirection(state.Direction);
        var startTip = path.Count > 0 ? path[0] : calibratedPose;
        var startDirection = path.Count > 1
            ? ResolveNozzleDirection(new Point(path[1].X - path[0].X, path[1].Y - path[0].Y))
            : nozzleDirection;
        var startNozzlePivot = ResolveNozzlePivot(startTip, startDirection, _nozzleImage, mmPerPixel);
        var approachOffsetX = ResolveApproachOffsetX(partRectWorld, timelineProgress);
        var approachOffset = new Point(approachOffsetX, 0);

        // Platform behavior:
        // 1) approach phase: move from right to first working pose
        // 2) safe transitions: platform can reposition
        // 3) working segments: platform stays at block-start, only nozzle rotates/works
        var manipPivot = ResolveManipulatorPivot(
            timelineProgress,
            approachPhase,
            startNozzlePivot,
            approachOffset,
            state,
            path,
            pathSource,
            segmentSafety,
            mmPerPixel);
        var manipRectWorld = CreateManipulatorRectFromNozzlePivot(manipPivot, _manipulatorImage, mmPerPixel);

        // Nozzle: during approach moves with platform; during processing
        // platform is fixed and only nozzle works along trajectory.
        var nozzlePoseWorld = timelineProgress <= approachPhase
            ? new Point(startTip.X + approachOffset.X, startTip.Y + approachOffset.Y)
            : calibratedPose;
        var nozzlePivotWorld = manipPivot;
        var nozzleWorkDirection = ResolveNozzleDirection(new Point(nozzlePoseWorld.X - nozzlePivotWorld.X, nozzlePoseWorld.Y - nozzlePivotWorld.Y));

        var nozzlePivotPath = BuildNozzlePivotPath(path, mmPerPixel);
        var approachPivotPath = nozzlePivotPath.Select(p => new Point(p.X + Math.Max(0, ResolveApproachOffsetX(partRectWorld, 0)), p.Y)).ToList();
        var manipPivotPath = BuildManipulatorPivotPath(
            path,
            pathSource,
            segmentSafety,
            startNozzlePivot,
            Math.Max(0, ResolveApproachOffsetX(partRectWorld, 0)),
            mmPerPixel);
        var manipEnvelope = BuildCombinedEnvelope(
            BuildMovingImageEnvelope(manipPivotPath, _manipulatorImage, mmPerPixel, Math.Clamp(ManipulatorAnchorX, 0.0, 1.0), Math.Clamp(ManipulatorAnchorY, 0.0, 1.0)),
            BuildMovingImageEnvelope(new List<Point> { startNozzlePivot }, _manipulatorImage, mmPerPixel, Math.Clamp(ManipulatorAnchorX, 0.0, 1.0), Math.Clamp(ManipulatorAnchorY, 0.0, 1.0)));
        var nozzleEnvelope = BuildCombinedEnvelope(
            BuildMovingImageEnvelope(nozzlePivotPath, _nozzleImage, mmPerPixel, NozzlePivotAnchorX, Math.Clamp(NozzleAnchorY, 0.0, 1.0), NozzleMaxStretch),
            BuildMovingImageEnvelope(approachPivotPath, _nozzleImage, mmPerPixel, NozzlePivotAnchorX, Math.Clamp(NozzleAnchorY, 0.0, 1.0), NozzleMaxStretch));
        var worldBounds = ComputeWorldBounds(path, partRectWorld, manipEnvelope, nozzleEnvelope);
        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
            return;

        if (_lastRenderSize != Bounds.Size)
        {
            _lastRenderSize = Bounds.Size;
            _needsRefit = true;
        }

        if (_needsRefit)
        {
            Fit(worldBounds);
            _needsRefit = false;
        }
        else
        {
            // Keep viewport stable during animation; only update camera when explicitly refitting.
            ApplyCurrentView();
        }
        var plotClip = new Rect(Pad, Pad, Math.Max(1, Bounds.Width - 2 * Pad), Math.Max(1, Bounds.Height - 2 * Pad));
        using (context.PushClip(plotClip))
        {
            if (ShowGrid)
                DrawGrid(context, worldBounds, 100);

            DrawImageWorld(context, _partImage, partRectWorld);

            if (path.Count >= 2)
            {
                DrawPolyline(context, path, new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 1.7));
                var passed = path.Take(state.SegmentIndex + 1).ToList();
                passed.Add(state.Position);
                DrawPolyline(context, passed, new Pen(new SolidColorBrush(Color.FromRgb(52, 211, 153)), 2.4));
            }

            DrawImageWorld(context, _manipulatorImage, manipRectWorld);
            DrawNozzleImageWorld(context, _nozzleImage, nozzlePivotWorld, nozzlePoseWorld, nozzleWorkDirection, mmPerPixel);
            DrawNozzleMarker(context, nozzlePoseWorld);
        }
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

    private static Bitmap? TryLoadBitmap(string uri)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(uri));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private double ResolveMmPerPixel(double referenceHeightMm)
    {
        if (_partImage is null || _partImage.Size.Height <= 0)
            return 1.0;

        return referenceHeightMm / _partImage.Size.Height;
    }

    private static Rect CreateWorldRectCenteredAtX(double centerX, double bottomZ, Bitmap? image, double mmPerPixel, double fallbackHeightMm)
    {
        if (image is null || image.Size.Height <= 0 || image.Size.Width <= 0)
            return new Rect(centerX - 250, bottomZ, 500, fallbackHeightMm);

        var w = image.Size.Width * mmPerPixel;
        var h = image.Size.Height * mmPerPixel;
        return new Rect(centerX - w / 2.0, bottomZ, w, h);
    }

    private Rect CreateManipulatorRectFromNozzlePivot(Point pivotWorld, Bitmap? image, double mmPerPixel)
    {
        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(pivotWorld.X - 380, pivotWorld.Y - 220, 500, 260);

        var w = image.Size.Width * mmPerPixel;
        var h = image.Size.Height * mmPerPixel;

        // Anchor manipulator to nozzle working point (near lower-left hinge in source image).
        var tipX = Math.Clamp(ManipulatorAnchorX, 0.0, 1.0);
        var tipY = Math.Clamp(ManipulatorAnchorY, 0.0, 1.0);
        var left = pivotWorld.X - w * tipX;
        var bottom = pivotWorld.Y - h * (1.0 - tipY);
        return new Rect(left, bottom, w, h);
    }

    private static Rect BuildCombinedEnvelope(Rect a, Rect b)
    {
        var left = Math.Min(a.Left, b.Left);
        var top = Math.Min(a.Top, b.Top);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private static Rect ComputeWorldBounds(IReadOnlyList<Point> path, params Rect[] rects)
    {
        var seed = path.Count > 0 ? path[0] : new Point(0, 0);
        var minX = seed.X;
        var maxX = seed.X;
        var minZ = seed.Y;
        var maxZ = seed.Y;

        foreach (var p in path)
        {
            minX = Math.Min(minX, p.X);
            maxX = Math.Max(maxX, p.X);
            minZ = Math.Min(minZ, p.Y);
            maxZ = Math.Max(maxZ, p.Y);
        }

        foreach (var r in rects)
        {
            minX = Math.Min(minX, r.Left);
            maxX = Math.Max(maxX, r.Right);
            minZ = Math.Min(minZ, r.Top);
            maxZ = Math.Max(maxZ, r.Bottom);
        }

        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxZ - minZ);
        const double marginX = 220;
        const double marginZ = 420;
        return new Rect(minX - marginX, minZ - marginZ, w + marginX * 2, h + marginZ * 2);
    }

    private static Rect BuildMovingImageEnvelope(IReadOnlyList<Point> path, Bitmap? image, double mmPerPixel, double anchorX, double anchorY, double widthScale = 1.0)
    {
        if (path.Count == 0 || image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(0, 0, 1, 1);

        var w = image.Size.Width * mmPerPixel * Math.Max(0.01, widthScale);
        var h = image.Size.Height * mmPerPixel;

        var minX = path.Min(p => p.X);
        var maxX = path.Max(p => p.X);
        var minZ = path.Min(p => p.Y);
        var maxZ = path.Max(p => p.Y);

        var left = minX - w * anchorX;
        var right = maxX + w * (1.0 - anchorX);
        var top = minZ - h * (1.0 - anchorY);
        var bottom = maxZ + h * anchorY;
        return new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    private void DrawGrid(DrawingContext context, Rect world, double stepMm)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 148, 163, 184)), 1);
        var minX = Math.Floor(world.Left / stepMm) * stepMm;
        var maxX = Math.Ceiling(world.Right / stepMm) * stepMm;
        var minZ = Math.Floor(world.Top / stepMm) * stepMm;
        var maxZ = Math.Ceiling(world.Bottom / stepMm) * stepMm;

        for (var x = minX; x <= maxX; x += stepMm)
            context.DrawLine(pen, WorldToScreen(new Point(x, minZ)), WorldToScreen(new Point(x, maxZ)));

        for (var z = minZ; z <= maxZ; z += stepMm)
            context.DrawLine(pen, WorldToScreen(new Point(minX, z)), WorldToScreen(new Point(maxX, z)));
    }

    private void DrawImageWorld(DrawingContext context, Bitmap? image, Rect worldRect)
    {
        if (image is null)
            return;

        var p0 = WorldToScreen(new Point(worldRect.Left, worldRect.Top));
        var p1 = WorldToScreen(new Point(worldRect.Right, worldRect.Bottom));
        var dest = new Rect(
            Math.Min(p0.X, p1.X),
            Math.Min(p0.Y, p1.Y),
            Math.Abs(p1.X - p0.X),
            Math.Abs(p1.Y - p0.Y));

        if (dest.Width < 1 || dest.Height < 1)
            return;

        context.DrawImage(
            image,
            new Rect(0, 0, image.Size.Width, image.Size.Height),
            dest);
    }

    private void DrawNozzleImageWorld(DrawingContext context, Bitmap? image, Point pivotWorld, Point tipWorld, Point directionWorld, double mmPerPixel)
    {
        if (image is null)
            return;

        var baseWidthMm = image.Size.Width * mmPerPixel;
        var baseHeightMm = image.Size.Height * mmPerPixel;
        var baseTipDistanceMm = Math.Max(1e-6, (Math.Clamp(NozzlePivotAnchorX, 0.0, 1.0) - Math.Clamp(NozzleAnchorX, 0.0, 1.0)) * baseWidthMm);
        var requiredTipDistanceMm = Math.Sqrt(
            (tipWorld.X - pivotWorld.X) * (tipWorld.X - pivotWorld.X)
            + (tipWorld.Y - pivotWorld.Y) * (tipWorld.Y - pivotWorld.Y));
        var stretch = Math.Clamp(requiredTipDistanceMm / baseTipDistanceMm, NozzleMinStretch, NozzleMaxStretch);
        var widthPx = Math.Abs(baseWidthMm * stretch * _scale);
        var heightPx = Math.Abs(baseHeightMm * _scale);
        if (widthPx < 1 || heightPx < 1)
            return;

        var pivotScreen = WorldToScreen(pivotWorld);
        var anchorX = Math.Clamp(NozzlePivotAnchorX, 0.0, 1.0);
        var anchorY = Math.Clamp(NozzleAnchorY, 0.0, 1.0);
        var dest = new Rect(
            pivotScreen.X - widthPx * anchorX,
            pivotScreen.Y - heightPx * anchorY,
            widthPx,
            heightPx);

        var screenDir = new Point(directionWorld.X, -directionWorld.Y);
        var len = Math.Sqrt(screenDir.X * screenDir.X + screenDir.Y * screenDir.Y);
        var angle = len <= 1e-6 ? 0 : Math.Atan2(screenDir.Y, screenDir.X);
        var transform =
            Matrix.CreateTranslation(-pivotScreen.X, -pivotScreen.Y)
            * Matrix.CreateRotation(angle)
            * Matrix.CreateTranslation(pivotScreen.X, pivotScreen.Y);

        using (context.PushTransform(transform))
        {
            context.DrawImage(
                image,
                new Rect(0, 0, image.Size.Width, image.Size.Height),
                dest);
        }
    }

    private void DrawNozzleMarker(DrawingContext context, Point worldPos)
    {
        var p = WorldToScreen(worldPos);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(56, 189, 248)), new Pen(Brushes.White, 1), p, 4.5, 4.5);
    }

    private void Fit(Rect fitBounds)
    {
        var safe = fitBounds.Normalize();
        if (safe.Width < 1) safe = safe.WithWidth(1);
        if (safe.Height < 1) safe = safe.WithHeight(1);

        var sx = Math.Max(1e-6, (Bounds.Width - Pad * 2) / safe.Width);
        var sy = Math.Max(1e-6, (Bounds.Height - Pad * 2) / safe.Height);
        _fitScale = Math.Min(sx, sy);
        _scale = _fitScale;
        _fitWorldBounds = safe;
        ApplyCurrentView();
    }

    private void ApplyCurrentView()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
            return;

        ClampPanOffset();
        var centerX = _fitWorldBounds.Center.X + _panOffset.X;
        var centerZ = _fitWorldBounds.Center.Y + _panOffset.Y;
        var zoomedWidth = _fitWorldBounds.Width / _zoomFactor;
        var zoomedHeight = _fitWorldBounds.Height / _zoomFactor;
        _worldBounds = new Rect(centerX - zoomedWidth / 2.0, centerZ - zoomedHeight / 2.0, zoomedWidth, zoomedHeight);
        _scale = _fitScale * _zoomFactor;
    }

    private void ClampPanOffset()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
            return;

        var zoomLimitedX = Math.Max(0, (_fitWorldBounds.Width - _fitWorldBounds.Width / _zoomFactor) / 2.0);
        var zoomLimitedY = Math.Max(0, (_fitWorldBounds.Height - _fitWorldBounds.Height / _zoomFactor) / 2.0);
        // Keep wide panning range so users can inspect the detached top part.
        var basePanX = _fitWorldBounds.Width * 3.0;
        var basePanY = _fitWorldBounds.Height * 3.0;
        var maxX = Math.Max(zoomLimitedX, basePanX);
        var maxY = Math.Max(zoomLimitedY, basePanY);

        _panOffset = new Point(Math.Clamp(_panOffset.X, -maxX, maxX), Math.Clamp(_panOffset.Y, -maxY, maxY));
    }

    private Point WorldToScreen(Point p)
    {
        var x = Pad + (p.X - _worldBounds.Left) * _scale;
        var y = Bounds.Height - Pad - (p.Y - _worldBounds.Top) * _scale;
        return new Point(x, y);
    }

    private void DrawPolyline(DrawingContext context, IList<Point> points, Pen pen)
    {
        if (points.Count < 2)
            return;

        var g = new StreamGeometry();
        using var gc = g.Open();
        gc.BeginFigure(WorldToScreen(points[0]), false);
        for (var i = 1; i < points.Count; i++)
            gc.LineTo(WorldToScreen(points[i]));
        gc.EndFigure(false);
        context.DrawGeometry(null, pen, g);
    }

    private double ToVisualX(double x) => InvertHorizontal ? -x : x;

    private static (Point Position, Point Direction, int SegmentIndex) Interpolate(IList<Point> pts, double progress)
    {
        if (pts.Count == 0)
            return (default, new Point(1, 0), 0);

        if (pts.Count == 1)
            return (pts[0], new Point(1, 0), 0);

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
                    new Point(
                        pts[i].X + (pts[i + 1].X - pts[i].X) * t,
                        pts[i].Y + (pts[i + 1].Y - pts[i].Y) * t),
                    new Point(pts[i + 1].X - pts[i].X, pts[i + 1].Y - pts[i].Y),
                    i);
            }

            acc = next;
        }

        return (
            pts[^1],
            new Point(pts[^1].X - pts[^2].X, pts[^1].Y - pts[^2].Y),
            pts.Count - 2);
    }

    private static List<RecipePoint> SelectRenderablePoints(List<RecipePoint> source)
    {
        var activeRobot = source.Where(p => p.Act && !p.Hidden && HasRenderableGeometry(p)).ToList();
        if (activeRobot.Count > 0)
            return activeRobot;

        var activeVisible = source.Where(p => p.Act && !p.Hidden).ToList();
        if (activeVisible.Count > 0)
            return activeVisible;

        var active = source.Where(p => p.Act).ToList();
        return active.Count > 0 ? active : source;
    }

    private static List<RecipePoint> SelectPathSource(List<RecipePoint> source)
    {
        // Keep full animation sequence (including Safe transitions),
        // otherwise moving between lower and upper zones is lost.
        return source;
    }

    private List<Point> BuildDisplayPath(IReadOnlyList<RecipePoint> source, double hZone)
    {
        // Use raw recipe points in 2D kinematics.
        // Smoothed (Catmull) path breaks Safe/Work segment mapping and causes visual glitches.
        return source
            .Select(p =>
            {
                var t = p.GetTargetPoint(hZone);
                return new Point(ToVisualX(t.Xp), t.Zp);
            })
            .ToList();
    }

    private Point ResolveNozzleDirection(Point fallbackDirection)
    {
        // For 2D overlay, orient nozzle by trajectory tangent (same visual logic as chart).
        var len = Math.Sqrt(fallbackDirection.X * fallbackDirection.X + fallbackDirection.Y * fallbackDirection.Y);
        return len <= 1e-6 ? new Point(1, 0) : new Point(fallbackDirection.X / len, fallbackDirection.Y / len);
    }

    private static double ResolveApproachOffsetX(Rect partRect, double progress)
    {
        // Approach from right to left during first 15% of animation, then process along path.
        var approachDistance = partRect.Width * 0.45;
        var phase = 0.15;
        var t = 1.0 - Math.Clamp(progress / phase, 0.0, 1.0);
        return approachDistance * t;
    }

    private Point ResolveNozzlePivot(Point tipWorld, Point direction, Bitmap? nozzleImage, double mmPerPixel)
    {
        if (nozzleImage is null || nozzleImage.Size.Width <= 0)
            return tipWorld;

        var tipAnchor = Math.Clamp(NozzleAnchorX, 0.0, 1.0);
        var pivotAnchor = Math.Clamp(NozzlePivotAnchorX, 0.0, 1.0);
        var widthMm = nozzleImage.Size.Width * mmPerPixel;
        var delta = Math.Max(0, (pivotAnchor - tipAnchor) * widthMm);
        return new Point(tipWorld.X + direction.X * delta, tipWorld.Y + direction.Y * delta);
    }

    private static bool HasRenderableGeometry(RecipePoint p)
    {
        const double eps = 1e-6;
        return Math.Abs(p.RCrd) > eps
            || Math.Abs(p.ZCrd) > eps
            || Math.Abs(p.Xr0 + p.DX) > eps
            || Math.Abs(p.Zr0 + p.DZ) > eps;
    }

    private void MarkRefit()
    {
        _needsRefit = true;
        InvalidateVisual();
    }

    private List<Point> BuildNozzlePivotPath(IReadOnlyList<Point> tipPath, double mmPerPixel)
    {
        if (tipPath.Count == 0)
            return new List<Point>();

        if (tipPath.Count == 1)
            return new List<Point> { tipPath[0] };

        var result = new List<Point>(tipPath.Count);
        for (var i = 0; i < tipPath.Count; i++)
        {
            Point dir;
            if (i == 0)
                dir = new Point(tipPath[1].X - tipPath[0].X, tipPath[1].Y - tipPath[0].Y);
            else if (i == tipPath.Count - 1)
                dir = new Point(tipPath[i].X - tipPath[i - 1].X, tipPath[i].Y - tipPath[i - 1].Y);
            else
                dir = new Point(tipPath[i + 1].X - tipPath[i - 1].X, tipPath[i + 1].Y - tipPath[i - 1].Y);

            result.Add(ResolveNozzlePivot(tipPath[i], ResolveNozzleDirection(dir), _nozzleImage, mmPerPixel));
        }

        return result;
    }

    private static List<bool> BuildSegmentSafety(IReadOnlyList<RecipePoint> source)
    {
        if (source.Count < 2)
            return new List<bool>();

        var flags = new List<bool>(source.Count - 1);
        for (var i = 0; i < source.Count - 1; i++)
            flags.Add(source[i].Safe || source[i + 1].Safe);

        return flags;
    }

    private Point ResolveManipulatorPivot(
        double timelineProgress,
        double approachPhase,
        Point startNozzlePivot,
        Point approachOffset,
        (Point Position, Point Direction, int SegmentIndex) state,
        IReadOnlyList<Point> path,
        IReadOnlyList<RecipePoint> source,
        IReadOnlyList<bool> segmentSafety,
        double mmPerPixel)
    {
        if (timelineProgress <= approachPhase)
            return new Point(startNozzlePivot.X + approachOffset.X, startNozzlePivot.Y + approachOffset.Y);

        var seg = Math.Clamp(state.SegmentIndex, 0, Math.Max(0, path.Count - 2));
        var isSafeSegment = seg < segmentSafety.Count && segmentSafety[seg];

        var tip = state.Position;
        var dir = ResolveNozzleDirection(state.Direction);
        if (isSafeSegment)
            return ResolveNozzlePivot(tip, dir, _nozzleImage, mmPerPixel);

        var blockStartSeg = seg;
        while (blockStartSeg > 0 && blockStartSeg - 1 < segmentSafety.Count && !segmentSafety[blockStartSeg - 1])
            blockStartSeg--;

        if (blockStartSeg >= path.Count)
            return ResolveNozzlePivot(tip, dir, _nozzleImage, mmPerPixel);

        var blockStartTip = path[blockStartSeg];
        var blockDir = blockStartSeg < path.Count - 1
            ? ResolveNozzleDirection(new Point(path[blockStartSeg + 1].X - path[blockStartSeg].X, path[blockStartSeg + 1].Y - path[blockStartSeg].Y))
            : dir;

        return ResolveNozzlePivot(blockStartTip, blockDir, _nozzleImage, mmPerPixel);
    }

    private List<Point> BuildManipulatorPivotPath(
        IReadOnlyList<Point> path,
        IReadOnlyList<RecipePoint> source,
        IReadOnlyList<bool> segmentSafety,
        Point startNozzlePivot,
        double approachOffsetX,
        double mmPerPixel)
    {
        var pivots = new List<Point>();
        pivots.Add(new Point(startNozzlePivot.X + approachOffsetX, startNozzlePivot.Y));
        pivots.Add(startNozzlePivot);

        if (path.Count < 2)
            return pivots;

        for (var i = 0; i < path.Count - 1; i++)
        {
            var tip = path[i];
            var dir = ResolveNozzleDirection(new Point(path[i + 1].X - path[i].X, path[i + 1].Y - path[i].Y));
            var tipPivot = ResolveNozzlePivot(tip, dir, _nozzleImage, mmPerPixel);
            var isSafeSegment = i < segmentSafety.Count && segmentSafety[i];

            if (isSafeSegment)
            {
                pivots.Add(tipPivot);
                continue;
            }

            var blockStartSeg = i;
            while (blockStartSeg > 0 && blockStartSeg - 1 < segmentSafety.Count && !segmentSafety[blockStartSeg - 1])
                blockStartSeg--;

            if (blockStartSeg < path.Count - 1)
            {
                var blockTip = path[blockStartSeg];
                var blockDir = ResolveNozzleDirection(new Point(path[blockStartSeg + 1].X - path[blockStartSeg].X, path[blockStartSeg + 1].Y - path[blockStartSeg].Y));
                pivots.Add(ResolveNozzlePivot(blockTip, blockDir, _nozzleImage, mmPerPixel));
            }
            else
            {
                pivots.Add(tipPivot);
            }
        }

        return pivots;
    }
}
