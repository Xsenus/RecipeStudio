using System;
using System.Collections.Generic;
using System.Globalization;
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

public sealed class SimulationPointPair2DControl : Control
{
    public static readonly StyledProperty<IList<RecipePoint>?> PointsProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, IList<RecipePoint>?>(nameof(Points));

    public static readonly StyledProperty<IList<RecipePoint>?> PlotPointsProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, IList<RecipePoint>?>(nameof(PlotPoints));

    public static readonly StyledProperty<RecipePoint?> SelectedPointProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, RecipePoint?>(nameof(SelectedPoint));

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(Progress));

    public static readonly StyledProperty<int> CurrentSegmentIndexProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, int>(nameof(CurrentSegmentIndex), -1);

    public static readonly StyledProperty<double> CurrentSegmentTProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(CurrentSegmentT));

    public static readonly StyledProperty<double> ToolXRawProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ToolXRaw));

    public static readonly StyledProperty<double> ToolZRawProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ToolZRaw));

    public static readonly StyledProperty<double> TargetXRawProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(TargetXRaw));

    public static readonly StyledProperty<double> TargetZRawProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(TargetZRaw));

    public static readonly StyledProperty<AppSettings?> SettingsProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, AppSettings?>(nameof(Settings));

    public static readonly StyledProperty<double> CurrentAlfaProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(CurrentAlfa));

    public static readonly StyledProperty<double> CurrentBettaProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(CurrentBetta));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<double> ReferenceHeightMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ReferenceHeightMm), SimulationBlueprint2DControl.DefaultReferenceHeightMm);

    public static readonly StyledProperty<bool> InvertHorizontalProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(InvertHorizontal), true);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> VerticalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(VerticalOffsetMm), SimulationBlueprint2DControl.DefaultVerticalOffsetMm);

    public static readonly StyledProperty<double> HorizontalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(HorizontalOffsetMm), SimulationBlueprint2DControl.DefaultHorizontalOffsetMm);

    public static readonly StyledProperty<double> PartWidthScalePercentProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(PartWidthScalePercent), SimulationBlueprint2DControl.DefaultPartWidthScalePercent);

    public static readonly StyledProperty<double> ManipulatorAnchorXProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ManipulatorAnchorX), SimulationBlueprint2DControl.DefaultManipulatorAnchorX);

    public static readonly StyledProperty<double> ManipulatorAnchorYProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ManipulatorAnchorY), SimulationBlueprint2DControl.DefaultManipulatorAnchorY);

    public static readonly StyledProperty<string> TargetDisplayModeProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, string>(nameof(TargetDisplayMode), SimulationTargetDisplayModes.Full);

    public static readonly StyledProperty<bool> ShowPairLinkProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(ShowPairLink), true);

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
    private Bitmap? _manipulatorImage;
    private Bitmap? _nozzleImage;
    private string _loadedSpriteVersion = string.Empty;

    public IList<RecipePoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public IList<RecipePoint>? PlotPoints
    {
        get => GetValue(PlotPointsProperty);
        set => SetValue(PlotPointsProperty, value);
    }

    public RecipePoint? SelectedPoint
    {
        get => GetValue(SelectedPointProperty);
        set => SetValue(SelectedPointProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
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

    public double VerticalOffsetMm
    {
        get => GetValue(VerticalOffsetMmProperty);
        set => SetValue(VerticalOffsetMmProperty, value);
    }

    public double HorizontalOffsetMm
    {
        get => GetValue(HorizontalOffsetMmProperty);
        set => SetValue(HorizontalOffsetMmProperty, value);
    }

    public double PartWidthScalePercent
    {
        get => GetValue(PartWidthScalePercentProperty);
        set => SetValue(PartWidthScalePercentProperty, value);
    }

    public double ManipulatorAnchorX
    {
        get => GetValue(ManipulatorAnchorXProperty);
        set => SetValue(ManipulatorAnchorXProperty, value);
    }

    public double ManipulatorAnchorY
    {
        get => GetValue(ManipulatorAnchorYProperty);
        set => SetValue(ManipulatorAnchorYProperty, value);
    }

    public string TargetDisplayMode
    {
        get => GetValue(TargetDisplayModeProperty);
        set => SetValue(TargetDisplayModeProperty, value);
    }

    public bool ShowPairLink
    {
        get => GetValue(ShowPairLinkProperty);
        set => SetValue(ShowPairLinkProperty, value);
    }

    public double ZoomFactor => _zoomFactor;
    public event Action<double>? ZoomChanged;

    static SimulationPointPair2DControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        PlotPointsProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        SelectedPointProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ProgressProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentIndexProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentTProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        ToolXRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        ToolZRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        TargetXRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        TargetZRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        CurrentAlfaProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        CurrentBettaProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        IsPlayingProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ReferenceHeightMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        InvertHorizontalProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ShowGridProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        VerticalOffsetMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        HorizontalOffsetMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        PartWidthScalePercentProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorXProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorYProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        TargetDisplayModeProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ShowPairLinkProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
    }

    public SimulationPointPair2DControl()
    {
        ClipToBounds = true;
        _partImage = TryLoadBitmap(SimulationSpriteAssets.PartUri);
        EnsureSelectedSpriteImagesLoaded();
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

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(18, 22, 30)), Bounds);
        EnsureSelectedSpriteImagesLoaded();

        var settings = Settings ?? new AppSettings();
        var referenceHeightMm = Math.Max(100, ReferenceHeightMm);
        var mmPerPixel = ResolveMmPerPixel(referenceHeightMm);
        var partRectWorld = CreateWorldRectCenteredAtX(HorizontalOffsetMm, 0, _partImage, mmPerPixel, referenceHeightMm, ResolvePartWidthScaleFactor(PartWidthScalePercent));
        var targetPoints = BuildVisibleTargetPoints(settings);
        var allTargetPoints = targetPoints.Select(point => point.Position).ToList();

        var pairState = ResolvePairPoints();
        var point1 = pairState.TargetPoint;
        var point2 = pairState.ToolPoint;
        var nozzleTip = pairState.NozzleTipPoint;
        var manipRectWorld = CreateManipulatorRectFromAnchor(point2, _manipulatorImage, mmPerPixel);
        var nozzleRectWorld = CreateNozzleEnvelopeRect(nozzleTip, point2, _nozzleImage, mmPerPixel);
        var worldBounds = ComputeWorldBounds(partRectWorld, manipRectWorld, point1, point2, nozzleRectWorld, allTargetPoints);

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
            ApplyCurrentView();
        }

        var plotClip = new Rect(Pad, Pad, Math.Max(1, Bounds.Width - 2 * Pad), Math.Max(1, Bounds.Height - 2 * Pad));
        using (context.PushClip(plotClip))
        {
            if (ShowGrid)
                DrawGrid(context, worldBounds, 100);

            DrawImageWorld(context, _partImage, partRectWorld);
            DrawImageWorld(context, _manipulatorImage, manipRectWorld);
            DrawTargetPoints(context, targetPoints, settings);

            // Red reference line should match the visible nozzle segment.
            if (ShowPairLink)
                DrawPairLink(context, nozzleTip, point2);
            DrawNozzleBetweenPoints(context, nozzleTip, point2, mmPerPixel);
            DrawPointMarker(context, point1, "1");
            DrawPointMarker(context, point2, "2");
        }
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

    private void EnsureSelectedSpriteImagesLoaded()
    {
        var spriteVersion = SimulationSpriteVersions.Normalize(Settings?.SimulationPanels?.SpriteVersion);
        if (_loadedSpriteVersion == spriteVersion)
            return;

        _manipulatorImage?.Dispose();
        _nozzleImage?.Dispose();
        _manipulatorImage = TryLoadBitmap(SimulationSpriteAssets.GetManipulatorUri(spriteVersion));
        _nozzleImage = TryLoadBitmap(SimulationSpriteAssets.GetNozzleUri(spriteVersion));
        _loadedSpriteVersion = spriteVersion;
        _needsRefit = true;
    }

    private string GetSpriteVersion()
        => SimulationSpriteVersions.Normalize(Settings?.SimulationPanels?.SpriteVersion);

    private (double X, double Y) ResolveManipulatorAnchors()
    {
        var anchorX = Math.Clamp(ManipulatorAnchorX, 0.0, 1.0);
        var anchorY = Math.Clamp(ManipulatorAnchorY, 0.0, 1.0);
        if (!SimulationSpriteAnchors.UsesDefaultManipulatorPivot(anchorX, anchorY))
            return (anchorX, anchorY);

        var spriteVersion = GetSpriteVersion();
        return (
            SimulationSpriteAnchors.GetManipulatorPivotAnchorX(spriteVersion),
            SimulationSpriteAnchors.GetManipulatorPivotAnchorY(spriteVersion));
    }

    private double ResolveNozzleStartAnchorX()
        => SimulationSpriteAnchors.GetNozzleTipAnchorX(GetSpriteVersion());

    private double ResolveNozzleEndAnchorX()
        => SimulationSpriteAnchors.GetNozzlePivotAnchorX(GetSpriteVersion());

    private double ResolveNozzleAnchorY()
        => SimulationSpriteAnchors.GetNozzlePivotAnchorY(GetSpriteVersion());

    private double ResolveMmPerPixel(double referenceHeightMm)
    {
        if (_partImage is null || _partImage.Size.Height <= 0)
            return 1.0;

        return referenceHeightMm / _partImage.Size.Height;
    }

    private static Rect CreateWorldRectCenteredAtX(double centerX, double bottomZ, Bitmap? image, double mmPerPixel, double fallbackHeightMm, double widthScaleFactor)
    {
        if (image is null || image.Size.Height <= 0 || image.Size.Width <= 0)
            return new Rect(centerX - 250 * widthScaleFactor, bottomZ, 500 * widthScaleFactor, fallbackHeightMm);

        var w = image.Size.Width * mmPerPixel * widthScaleFactor;
        var h = image.Size.Height * mmPerPixel;
        return new Rect(centerX - w / 2.0, bottomZ, w, h);
    }

    private Rect CreateManipulatorRectFromAnchor(Point anchorWorld, Bitmap? image, double mmPerPixel)
    {
        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(anchorWorld.X - 380, anchorWorld.Y - 220, 500, 260);

        var w = image.Size.Width * mmPerPixel;
        var h = image.Size.Height * mmPerPixel;
        var (anchorX, anchorY) = ResolveManipulatorAnchors();
        var left = anchorWorld.X - w * anchorX;
        var bottom = anchorWorld.Y - h * (1.0 - anchorY);
        return new Rect(left, bottom, w, h);
    }

    private (Point TargetPoint, Point ToolPoint, Point NozzleTipPoint) ResolvePairPoints()
    {
        var settings = Settings ?? new AppSettings();
        var selectedPair = !IsPlaying ? TryResolveSelectedPairPoints(settings) : null;
        if (selectedPair is { } selected)
            return selected;

        var source = Points?.ToList() ?? new List<RecipePoint>();
        var hZone = settings.HZone;
        var absoluteRobot = RobotCoordinateResolver.BuildAbsolutePositions(source);
        var animTarget = new List<Point>();
        var animTool = new List<Point>();

        for (var idx = 0; idx < source.Count; idx++)
        {
            var p = source[idx];
            var (xp, zp) = p.GetTargetPoint(hZone);
            animTarget.Add(new Point(ToVisualX(xp), zp));

            var toolX = p.Xr0 + p.DX;
            var toolZ = p.Zr0 + p.DZ;
            if (idx < absoluteRobot.Count)
            {
                var abs = absoluteRobot[idx];
                toolX = abs.X;
                toolZ = abs.Z;
            }

            animTool.Add(new Point(ToVisualX(toolX), toolZ));
        }

        var toolState = GetToolState(animTool, animTarget, Progress, CurrentSegmentIndex, CurrentSegmentT);
        var usePhysicalOrientation = NozzleOrientationPolicy.UsePhysicalOrientation(settings.NozzleOrientationMode);
        var physicalDirection = usePhysicalOrientation
            ? ApplyTransitionLiftOrientation(source, animTool, toolState, settings)
            : default;
        var marker = SimulationOverlayGeometry.ResolvePlotMarkerGeometry(
            toolState.ToolPosition,
            toolState.TargetPosition,
            double.IsFinite(ToolXRaw) && double.IsFinite(ToolZRaw) ? new Point(ToolXRaw, ToolZRaw) : null,
            double.IsFinite(TargetXRaw) && double.IsFinite(TargetZRaw) ? new Point(TargetXRaw, TargetZRaw) : null,
            InvertHorizontal,
            usePhysicalOrientation,
            physicalDirection);
        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            VerticalOffsetMm,
            usePhysicalOrientation,
            ResolveDisplayedNozzleLengthMm(settings));
        return (pair.TargetPoint, pair.ToolPoint, pair.NozzleTipPoint);
    }

    private (Point TargetPoint, Point ToolPoint, Point NozzleTipPoint)? TryResolveSelectedPairPoints(AppSettings settings)
    {
        if (SelectedPoint is null)
            return null;

        var source = (PlotPoints ?? Points)?.ToList() ?? new List<RecipePoint>();
        var index = FindPointIndex(source, SelectedPoint);
        if (index < 0)
        {
            source = new List<RecipePoint> { SelectedPoint };
            index = 0;
        }

        if (source.Count == 0)
            return null;

        var absoluteRobot = RobotCoordinateResolver.BuildAbsolutePositions(source);
        if (absoluteRobot.Count == 0)
            return null;

        var selected = source[index];
        var rawTool = absoluteRobot[Math.Min(index, absoluteRobot.Count - 1)];
        var (targetX, targetZ) = selected.GetTargetPoint(settings.HZone);
        var usePhysicalOrientation = NozzleOrientationPolicy.UsePhysicalOrientation(settings.NozzleOrientationMode);
        var physicalDirection = usePhysicalOrientation
            ? GetPhysicalProjectedVector(settings, selected.Alfa, selected.Betta, selected.Place)
            : default;
        var marker = SimulationOverlayGeometry.ResolvePlotMarkerGeometry(
            default,
            default,
            new Point(rawTool.X, rawTool.Z),
            new Point(targetX, targetZ),
            InvertHorizontal,
            usePhysicalOrientation,
            physicalDirection);
        var pair = SimulationOverlayGeometry.ResolvePairOverlayGeometry(
            marker,
            VerticalOffsetMm,
            usePhysicalOrientation,
            ResolveDisplayedNozzleLengthMm(settings));
        return (pair.TargetPoint, pair.ToolPoint, pair.NozzleTipPoint);
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
            var t = Math.Clamp(segmentTHint, 0.0, 1.0);
            var x = tool[seg].X + (tool[seg + 1].X - tool[seg].X) * t;
            var y = tool[seg].Y + (tool[seg + 1].Y - tool[seg].Y) * t;
            var tx = targetPts[seg].X + (targetPts[seg + 1].X - targetPts[seg].X) * t;
            var ty = targetPts[seg].Y + (targetPts[seg + 1].Y - targetPts[seg].Y) * t;
            var dir = new Point(tx - x, ty - y);
            var travel = new Point(tool[seg + 1].X - tool[seg].X, tool[seg + 1].Y - tool[seg].Y);
            return (new Point(x, y), new Point(tx, ty), dir, travel, seg, t);
        }

        progress = Math.Clamp(progress, 0.0, 1.0);
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

        var t = SmoothStep(Math.Clamp(toolState.SegmentT, 0.0, 1.0));
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
        var zSign = place == 0 ? -1.0 : 1.0;
        var z = zSign * Math.Cos(b) * Math.Sin(a);
        return new Point(x, z);
    }

    private static double ResolveDisplayedNozzleLengthMm(AppSettings settings)
        => Math.Max(1e-6, Math.Abs(settings.Lz));

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
    {
        return x * x * (3 - 2 * x);
    }

    private static double VectorLength(Point value)
        => Math.Sqrt(value.X * value.X + value.Y * value.Y);

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;

    private static Rect ComputeWorldBounds(
        Rect partRect,
        Rect manipRect,
        Point point1,
        Point point2,
        Rect nozzleRect,
        IList<Point>? extraPoints = null)
    {
        var minX = Math.Min(Math.Min(partRect.Left, manipRect.Left), Math.Min(point1.X, point2.X));
        minX = Math.Min(minX, nozzleRect.Left);
        var maxX = Math.Max(Math.Max(partRect.Right, manipRect.Right), Math.Max(point1.X, point2.X));
        maxX = Math.Max(maxX, nozzleRect.Right);
        var minZ = Math.Min(Math.Min(partRect.Top, manipRect.Top), Math.Min(point1.Y, point2.Y));
        minZ = Math.Min(minZ, nozzleRect.Top);
        var maxZ = Math.Max(Math.Max(partRect.Bottom, manipRect.Bottom), Math.Max(point1.Y, point2.Y));
        maxZ = Math.Max(maxZ, nozzleRect.Bottom);

        if (extraPoints is not null)
        {
            foreach (var point in extraPoints)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
                minZ = Math.Min(minZ, point.Y);
                maxZ = Math.Max(maxZ, point.Y);
            }
        }

        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxZ - minZ);
        const double marginX = 220;
        const double marginZ = 420;
        return new Rect(minX - marginX, minZ - marginZ, w + marginX * 2, h + marginZ * 2);
    }

    private static Rect CreateNozzleEnvelopeRect(Point point1, Point point2, Bitmap? image, double mmPerPixel)
    {
        var minX = Math.Min(point1.X, point2.X);
        var maxX = Math.Max(point1.X, point2.X);
        var minZ = Math.Min(point1.Y, point2.Y);
        var maxZ = Math.Max(point1.Y, point2.Y);

        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(minX - 40, minZ - 40, (maxX - minX) + 80, (maxZ - minZ) + 80);

        var h = Math.Max(20, image.Size.Height * mmPerPixel);
        return new Rect(minX - h, minZ - h, (maxX - minX) + h * 2, (maxZ - minZ) + h * 2);
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

    private void DrawNozzleBetweenPoints(DrawingContext context, Point point1World, Point point2World, double mmPerPixel)
    {
        if (_nozzleImage is null || _nozzleImage.Size.Width <= 0 || _nozzleImage.Size.Height <= 0)
            return;

        var start = WorldToScreen(point1World);
        var end = WorldToScreen(point2World);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthPx = Math.Sqrt(dx * dx + dy * dy);
        if (lengthPx <= 1e-3)
            return;

        var angle = Math.Atan2(dy, dx);

        var heightMm = _nozzleImage.Size.Height * mmPerPixel;
        var heightPx = Math.Max(1, heightMm * _scale);
        var sourceWidthPx = _nozzleImage.Size.Width;
        var sourceHeightPx = _nozzleImage.Size.Height;
        var sourceStartX = Math.Clamp(ResolveNozzleStartAnchorX(), 0.0, 1.0) * sourceWidthPx;
        var sourceEndX = Math.Clamp(ResolveNozzleEndAnchorX(), 0.0, 1.0) * sourceWidthPx;
        var sourceSpanX = Math.Max(1.0, sourceEndX - sourceStartX);
        var anchorY = ResolveNozzleAnchorY() * heightPx;

        var transform =
            Matrix.CreateTranslation(0, -anchorY) *
            Matrix.CreateRotation(angle) *
            Matrix.CreateTranslation(start.X, start.Y);

        using (context.PushTransform(transform))
        {
            context.DrawImage(
                _nozzleImage,
                new Rect(sourceStartX, 0, sourceSpanX, sourceHeightPx),
                new Rect(0, 0, lengthPx, heightPx));
        }
    }

    private void DrawPairLink(DrawingContext context, Point point1, Point point2)
    {
        var red = Color.FromRgb(239, 68, 68);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, red.R, red.G, red.B)), 7);
        context.DrawLine(pen, WorldToScreen(point1), WorldToScreen(point2));
    }

    private void DrawTargetPoints(DrawingContext context, IList<StyledTargetPoint> worldPoints, AppSettings settings)
    {
        if (worldPoints.Count == 0)
            return;

        var safeColor = ParseColorOrDefault(settings.PlotColorSafetyZone, Color.FromRgb(156, 163, 175));
        var workColor = ParseColorOrDefault(settings.PlotColorWorkingZone, Color.FromRgb(34, 197, 94));
        var safeFill = new SolidColorBrush(Color.FromArgb(235, safeColor.R, safeColor.G, safeColor.B));
        var workFill = new SolidColorBrush(Color.FromArgb(235, workColor.R, workColor.G, workColor.B));
        var outline = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1.1);
        var radius = Math.Max(3.2, settings.PlotPointRadius);

        foreach (var point in worldPoints)
        {
            var fill = point.Safe ? safeFill : workFill;
            context.DrawEllipse(fill, outline, WorldToScreen(point.Position), radius, radius);
        }
    }

    private void DrawPointMarker(DrawingContext context, Point worldPoint, string label)
    {
        var screenPoint = WorldToScreen(worldPoint);
        context.DrawEllipse(new SolidColorBrush(Color.FromRgb(56, 189, 248)), new Pen(Brushes.White, 1.2), screenPoint, 5.2, 5.2);

        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            14,
            Brushes.White);
        context.DrawText(text, new Point(screenPoint.X + 8, screenPoint.Y - 9));
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
        ClampPanOffset();
        ApplyCurrentView();
    }

    private void ApplyCurrentView()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
            return;

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

        var zoomedWidth = _fitWorldBounds.Width / _zoomFactor;
        var zoomedHeight = _fitWorldBounds.Height / _zoomFactor;
        var maxPanX = Math.Max(0, (_fitWorldBounds.Width - zoomedWidth) * 0.5);
        var maxPanY = Math.Max(0, (_fitWorldBounds.Height - zoomedHeight) * 0.5);
        _panOffset = new Point(
            Math.Clamp(_panOffset.X, -maxPanX, maxPanX),
            Math.Clamp(_panOffset.Y, -maxPanY, maxPanY));
    }

    private Point WorldToScreen(Point p)
    {
        var x = Pad + (p.X - _worldBounds.Left) * _scale;
        var y = Bounds.Height - Pad - (p.Y - _worldBounds.Top) * _scale;
        return new Point(x, y);
    }

    private double ToVisualX(double x) => InvertHorizontal ? -x : x;

    private static double ResolvePartWidthScaleFactor(double partWidthScalePercent)
        => Math.Clamp(partWidthScalePercent, 50.0, 150.0) / 100.0;

    private List<StyledTargetPoint> BuildVisibleTargetPoints(AppSettings settings)
    {
        var points = FilterRenderablePoints(PlotPoints ?? Points)
            .Select(p =>
            {
                var (xp, zp) = p.GetTargetPoint(settings.HZone);
                return new StyledTargetPoint(
                    SimulationOverlayGeometry.ProjectWorldPoint(xp, zp, InvertHorizontal, VerticalOffsetMm),
                    p.Safe);
            })
            .ToList();

        return SimulationOverlayGeometry.BuildDisplayedTargetPoints(points, HorizontalOffsetMm, TargetDisplayMode);
    }

    private static List<RecipePoint> FilterRenderablePoints(IList<RecipePoint>? source)
    {
        var src = source?.ToList() ?? new List<RecipePoint>();
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

    private static bool HasRenderableGeometry(RecipePoint point)
    {
        const double eps = 1e-6;
        return Math.Abs(point.RCrd) > eps
            || Math.Abs(point.ZCrd) > eps
            || Math.Abs(point.Xr0 + point.DX) > eps
            || Math.Abs(point.Zr0 + point.DZ) > eps;
    }

    private static Color ParseColorOrDefault(string? value, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out var parsed))
            return parsed;

        return fallback;
    }

    private void MarkRefit()
    {
        _needsRefit = true;
        InvalidateVisual();
    }
}
