using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
    public const double BaseReferenceHeightMm = 1309.49;
    public const double DefaultReferenceHeightMm = 1361.8696;
    public const double DefaultNozzleAnchorX = SimulationSpriteAnchors.NozzleTipAnchorX;
    public const double DefaultNozzleAnchorY = SimulationSpriteAnchors.NozzlePivotAnchorY;
    public const double DefaultManipulatorAnchorX = SimulationSpriteAnchors.ManipulatorPivotAnchorX;
    public const double DefaultManipulatorAnchorY = SimulationSpriteAnchors.ManipulatorPivotAnchorY;
    public const double DefaultVerticalOffsetMm = 195.0;
    public const double DefaultHorizontalOffsetMm = -55.0;
    public const double DefaultPartHeightScalePercent = 104.0;
    public const double DefaultPartWidthScalePercent = 98.0;
    private const double MinPartScalePercent = 50.0;
    private const double MaxPartScalePercent = 150.0;
    private const double ApproachPhase = 0.15;
    private const double CenterTransferPhase = 0.10;
    private const double ApproachDistanceFactor = 0.45;
    private const double RenderSubsampleThresholdMm = 80.0;
    private const double AutoAlignHorizontalBiasMm = 0.0;
    private const double AutoAlignVerticalBiasMm = 0.0;
    private const double UpperZoneBlendStartMm = 40.0;
    private const double UpperZoneBlendSpanMm = 220.0;
    private const double UpperZoneExtraPivotDropMm = 120.0;
    private const double UpperZoneExtraPivotRightMm = 70.0;
    private const double BasePivotDropMm = 35.0;
    private const double PivotFollowSmoothing = 0.55;
    private const double AutoAlignPathSubsampleMm = 24.0;
    private const double AutoAlignReferenceHeightMinMm = 900.0;
    private const double AutoAlignReferenceHeightMaxMm = 1800.0;
    private const double AutoAlignReferenceHeightSearchWindowMm = 160.0;
    private const double AutoAlignReferenceHeightCoarseStepMm = 20.0;
    private const double AutoAlignReferenceHeightFineWindowMm = 24.0;
    private const double AutoAlignReferenceHeightFineStepMm = 4.0;
    private const double AutoAlignHorizontalSearchWindowMm = 180.0;
    private const double AutoAlignVerticalSearchWindowMm = 180.0;
    private const double AutoAlignOffsetCoarseStepMm = 12.0;
    private const double AutoAlignOffsetFineWindowMm = 24.0;
    private const double AutoAlignOffsetFineStepMm = 2.0;

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
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(ReferenceHeightMm), DefaultReferenceHeightMm);

    public static readonly StyledProperty<double> PartHeightScalePercentProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(PartHeightScalePercent), DefaultPartHeightScalePercent);

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

    public static readonly StyledProperty<double> HorizontalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(HorizontalOffsetMm), DefaultHorizontalOffsetMm);

    public static readonly StyledProperty<double> PartWidthScalePercentProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, double>(nameof(PartWidthScalePercent), DefaultPartWidthScalePercent);

    public static readonly StyledProperty<bool> ReversePathProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, bool>(nameof(ReversePath));

    public static readonly StyledProperty<bool> UseFactTelemetryProperty =
        AvaloniaProperty.Register<SimulationBlueprint2DControl, bool>(nameof(UseFactTelemetry));

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
    private bool _hasFactPivot;
    private bool _syncingHeightScale;
    private Point _factPivotWorld;

    private readonly Bitmap? _partImage;
    private Bitmap? _manipulatorImage;
    private Bitmap? _nozzleImage;
    private readonly EdgeMap? _partEdgeMap;
    private string _loadedSpriteVersion = string.Empty;

    private sealed class EdgeMap
    {
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required double[] Magnitude { get; init; }
    }

    private readonly record struct AutoAlignSolution(double ReferenceHeightMm, double Horizontal, double Vertical, double Score);
    private readonly record struct OffsetAlignSolution(double Horizontal, double Vertical, double Score);

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

    public double PartHeightScalePercent
    {
        get => GetValue(PartHeightScalePercentProperty);
        set => SetValue(PartHeightScalePercentProperty, value);
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

    public bool ReversePath
    {
        get => GetValue(ReversePathProperty);
        set => SetValue(ReversePathProperty, value);
    }

    public bool UseFactTelemetry
    {
        get => GetValue(UseFactTelemetryProperty);
        set => SetValue(UseFactTelemetryProperty, value);
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
        ReferenceHeightMmProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) =>
        {
            c.SyncPartHeightScalePercentFromReferenceHeight();
            c.MarkRefit();
        });
        PartHeightScalePercentProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.SyncReferenceHeightFromPartHeightScalePercent());
        InvertHorizontalProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ShowGridProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.InvalidateVisual());
        NozzleAnchorXProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorXProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        NozzleAnchorYProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorYProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        VerticalOffsetMmProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        HorizontalOffsetMmProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        PartWidthScalePercentProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        ReversePathProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
        UseFactTelemetryProperty.Changed.AddClassHandler<SimulationBlueprint2DControl>((c, _) => c.MarkRefit());
    }

    public SimulationBlueprint2DControl()
    {
        ClipToBounds = true;
        _partImage = TryLoadBitmap(SimulationSpriteAssets.PartUri);
        _partEdgeMap = TryBuildEdgeMap(_partImage);
        EnsureSelectedSpriteImagesLoaded();
        SyncPartHeightScalePercentFromReferenceHeight();
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

    public void AutoAlignCalibration()
    {
        var points = SelectRenderablePoints(Points?.ToList() ?? new List<RecipePoint>());
        var hZone = Settings?.HZone ?? 1200;
        var pathSource = SelectPathSource(points);
        if (ReversePath && pathSource.Count > 1)
            pathSource.Reverse();

        var fitSource = pathSource.Where(p => !p.Safe).ToList();
        if (fitSource.Count < 2)
            fitSource = pathSource;

        var path = BuildDisplayPath(fitSource, hZone);
        if (path.Count == 0)
            return;

        path = BuildRenderPath(path, AutoAlignPathSubsampleMm);
        var referenceHeightMm = Math.Clamp(Math.Max(100, ReferenceHeightMm), AutoAlignReferenceHeightMinMm, AutoAlignReferenceHeightMaxMm);
        var currentHorizontal = HorizontalOffsetMm;
        var currentVertical = VerticalOffsetMm;
        var partWidthScaleFactor = ResolvePartWidthScaleFactor(PartWidthScalePercent);

        if (_partEdgeMap is not null)
        {
            var optimized = OptimizeAutoAlignment(path, _partEdgeMap, referenceHeightMm, currentHorizontal, currentVertical, partWidthScaleFactor);
            ReferenceHeightMm = optimized.ReferenceHeightMm;
            HorizontalOffsetMm = optimized.Horizontal;
            VerticalOffsetMm = optimized.Vertical;
        }
    }

    private void SyncPartHeightScalePercentFromReferenceHeight()
    {
        if (_syncingHeightScale)
            return;

        _syncingHeightScale = true;
        try
        {
            var percent = BaseReferenceHeightMm <= 1e-6
                ? DefaultPartHeightScalePercent
                : ReferenceHeightMm / BaseReferenceHeightMm * 100.0;
            percent = Math.Clamp(percent, MinPartScalePercent, MaxPartScalePercent);

            if (Math.Abs(percent - PartHeightScalePercent) > 1e-6)
                SetCurrentValue(PartHeightScalePercentProperty, percent);
        }
        finally
        {
            _syncingHeightScale = false;
        }
    }

    private void SyncReferenceHeightFromPartHeightScalePercent()
    {
        if (_syncingHeightScale)
            return;

        _syncingHeightScale = true;
        try
        {
            var percent = Math.Clamp(PartHeightScalePercent, MinPartScalePercent, MaxPartScalePercent);
            if (Math.Abs(percent - PartHeightScalePercent) > 1e-6)
                SetCurrentValue(PartHeightScalePercentProperty, percent);

            var referenceHeightMm = BaseReferenceHeightMm * percent / 100.0;
            if (Math.Abs(referenceHeightMm - ReferenceHeightMm) > 1e-6)
                SetCurrentValue(ReferenceHeightMmProperty, referenceHeightMm);
        }
        finally
        {
            _syncingHeightScale = false;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(18, 22, 30)), Bounds);
        EnsureSelectedSpriteImagesLoaded();

        var points = SelectRenderablePoints(Points?.ToList() ?? new List<RecipePoint>());
        var hZone = Settings?.HZone ?? 1200;
        var pathSource = SelectPathSource(points);
        if (ReversePath && pathSource.Count > 1)
            pathSource.Reverse();

        var rawPath = BuildDisplayPath(pathSource, hZone);
        var path = BuildRenderPath(ApplyTrajectoryOffsets(rawPath), RenderSubsampleThresholdMm);
        var referenceHeightMm = Math.Max(100, ReferenceHeightMm);
        var mmPerPixel = ResolveMmPerPixel(referenceHeightMm);
        var partRectWorld = CreateWorldRectCenteredAtX(HorizontalOffsetMm, 0, _partImage, mmPerPixel, referenceHeightMm, ResolvePartWidthScaleFactor(PartWidthScalePercent));

        var pathBounds = ComputePathBounds(path.Count > 0
            ? path
            : new List<Point> { new Point(partRectWorld.Center.X, partRectWorld.Center.Y) });
        var centerTip = new Point(partRectWorld.Center.X, pathBounds.Center.Y);
        var centerNozzlePivot = ResolveNozzlePivot(centerTip, new Point(1, 0), mmPerPixel);
        var processPivotPath = BuildManipulatorFollowPath(path, centerNozzlePivot, mmPerPixel, centerTip.Y);

        var approachDistance = ResolveApproachDistance(partRectWorld);
        var approachStartTip = new Point(centerTip.X + approachDistance, centerTip.Y);
        var approachStartPivot = new Point(centerNozzlePivot.X + approachDistance, centerNozzlePivot.Y);
        var timelineProgress = Math.Clamp(Progress, 0.0, 1.0);
        var motion = ResolveMotionState(
            timelineProgress,
            path,
            processPivotPath,
            centerTip,
            centerNozzlePivot,
            approachStartTip,
            approachStartPivot);

        var nozzlePoseWorld = motion.Tip;
        var nozzlePivotWorld = motion.Pivot;
        var passedSegmentIndex = motion.ProcessSegmentIndex;
        var passedSegmentT = motion.ProcessSegmentT;
        var showPassedPath = motion.IsProcessing;
        if (UseFactTelemetry)
        {
            nozzlePoseWorld = ResolveFactTipWorld(motion.Tip);
            var nearest = FindNearestPathSample(path, nozzlePoseWorld);
            if (nearest.IsValid)
            {
                passedSegmentIndex = nearest.SegmentIndex;
                passedSegmentT = nearest.SegmentT;
                showPassedPath = true;
                var targetPivot = ResolvePivotFromTip(nozzlePoseWorld, nearest.Position, mmPerPixel, centerTip.Y);
                _factPivotWorld = _hasFactPivot
                    ? LerpPoint(_factPivotWorld, targetPivot, PivotFollowSmoothing)
                    : targetPivot;
                _hasFactPivot = true;
                nozzlePivotWorld = _factPivotWorld;
            }
            else
            {
                showPassedPath = false;
                var targetPivot = ResolvePivotFromTip(nozzlePoseWorld, nozzlePoseWorld, mmPerPixel, centerTip.Y);
                _factPivotWorld = _hasFactPivot
                    ? LerpPoint(_factPivotWorld, targetPivot, PivotFollowSmoothing)
                    : targetPivot;
                _hasFactPivot = true;
                nozzlePivotWorld = _factPivotWorld;
            }
        }
        else
        {
            _hasFactPivot = false;
        }

        var nozzleWorkDirection = ResolveNozzleDirection(new Point(nozzlePivotWorld.X - nozzlePoseWorld.X, nozzlePivotWorld.Y - nozzlePoseWorld.Y));
        var manipRectWorld = CreateManipulatorRectFromNozzlePivot(nozzlePivotWorld, _manipulatorImage, mmPerPixel);

        var tipMotionPath = BuildMotionPath(path, centerTip, approachStartTip);
        var pivotMotionPath = BuildMotionPath(processPivotPath, centerNozzlePivot, approachStartPivot);
        var (manipAnchorX, manipAnchorY) = ResolveManipulatorAnchors();
        var manipEnvelope = BuildMovingImageEnvelope(
            pivotMotionPath,
            _manipulatorImage,
            mmPerPixel,
            manipAnchorX,
            manipAnchorY);
        var nozzleEnvelope = BuildMovingImageEnvelope(
            pivotMotionPath,
            _nozzleImage,
            mmPerPixel,
            ResolveNozzlePivotAnchorX(),
            ResolveNozzlePivotAnchorY());
        var worldBounds = ComputeWorldBounds(
            tipMotionPath.Count > 0 ? tipMotionPath : path,
            partRectWorld,
            manipEnvelope,
            nozzleEnvelope,
            new Rect(nozzlePoseWorld.X - 1.0, nozzlePoseWorld.Y - 1.0, 2.0, 2.0),
            new Rect(nozzlePivotWorld.X - 1.0, nozzlePivotWorld.Y - 1.0, 2.0, 2.0));
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
                if (showPassedPath)
                {
                    var passed = BuildPassedPath(path, passedSegmentIndex, passedSegmentT, nozzlePoseWorld);
                    DrawPolyline(context, passed, new Pen(new SolidColorBrush(Color.FromRgb(52, 211, 153)), 2.4));
                }
            }

            DrawImageWorld(context, _manipulatorImage, manipRectWorld);
            DrawNozzleImageWorld(context, _nozzleImage, nozzlePivotWorld, nozzleWorkDirection, mmPerPixel);
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

    private double ResolveNozzleTipAnchorX()
    {
        var anchorX = Math.Clamp(NozzleAnchorX, 0.0, 1.0);
        if (Math.Abs(anchorX - DefaultNozzleAnchorX) > 1e-6)
            return anchorX;

        return SimulationSpriteAnchors.GetNozzleTipAnchorX(GetSpriteVersion());
    }

    private double ResolveNozzlePivotAnchorX()
        => SimulationSpriteAnchors.GetNozzlePivotAnchorX(GetSpriteVersion());

    private double ResolveNozzlePivotAnchorY()
    {
        var anchorY = Math.Clamp(NozzleAnchorY, 0.0, 1.0);
        if (Math.Abs(anchorY - DefaultNozzleAnchorY) > 1e-6)
            return anchorY;

        return SimulationSpriteAnchors.GetNozzlePivotAnchorY(GetSpriteVersion());
    }

    private static EdgeMap? TryBuildEdgeMap(Bitmap? image)
    {
        if (image is null)
            return null;

        var width = image.PixelSize.Width;
        var height = image.PixelSize.Height;
        if (width < 3 || height < 3)
            return null;

        var stride = width * 4;
        var raw = new byte[stride * height];
        var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            image.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), raw.Length, stride);
        }
        catch
        {
            return null;
        }
        finally
        {
            handle.Free();
        }

        var luma = new double[width * height];
        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * stride;
            var pixOffset = y * width;
            for (var x = 0; x < width; x++)
            {
                var src = rowOffset + x * 4;
                var b = raw[src];
                var g = raw[src + 1];
                var r = raw[src + 2];
                luma[pixOffset + x] = 0.114 * b + 0.587 * g + 0.299 * r;
            }
        }

        var magnitude = new double[width * height];
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var idx = y * width + x;
                var gx = luma[idx + 1] - luma[idx - 1];
                var gy = luma[idx + width] - luma[idx - width];
                magnitude[idx] = Math.Sqrt(gx * gx + gy * gy);
            }
        }

        return new EdgeMap
        {
            Width = width,
            Height = height,
            Magnitude = magnitude
        };
    }

    private static AutoAlignSolution OptimizeAutoAlignment(
        IReadOnlyList<Point> path,
        EdgeMap edgeMap,
        double currentReferenceHeightMm,
        double currentHorizontalMm,
        double currentVerticalMm,
        double partWidthScaleFactor)
    {
        var currentReferenceHeight = Math.Clamp(currentReferenceHeightMm, AutoAlignReferenceHeightMinMm, AutoAlignReferenceHeightMaxMm);
        var best = OptimizeAutoAlignmentForReferenceHeight(
            path,
            edgeMap,
            currentReferenceHeight,
            currentReferenceHeight,
            currentHorizontalMm,
            currentVerticalMm,
            partWidthScaleFactor);

        var coarseStart = Math.Max(AutoAlignReferenceHeightMinMm, currentReferenceHeight - AutoAlignReferenceHeightSearchWindowMm);
        var coarseEnd = Math.Min(AutoAlignReferenceHeightMaxMm, currentReferenceHeight + AutoAlignReferenceHeightSearchWindowMm);
        for (var referenceHeight = coarseStart; referenceHeight <= coarseEnd; referenceHeight += AutoAlignReferenceHeightCoarseStepMm)
        {
            var candidate = OptimizeAutoAlignmentForReferenceHeight(
                path,
                edgeMap,
                referenceHeight,
                currentReferenceHeight,
                currentHorizontalMm,
                currentVerticalMm,
                partWidthScaleFactor);
            if (candidate.Score > best.Score)
                best = candidate;
        }

        var fineStart = Math.Max(AutoAlignReferenceHeightMinMm, best.ReferenceHeightMm - AutoAlignReferenceHeightFineWindowMm);
        var fineEnd = Math.Min(AutoAlignReferenceHeightMaxMm, best.ReferenceHeightMm + AutoAlignReferenceHeightFineWindowMm);
        for (var referenceHeight = fineStart; referenceHeight <= fineEnd; referenceHeight += AutoAlignReferenceHeightFineStepMm)
        {
            var candidate = OptimizeAutoAlignmentForReferenceHeight(
                path,
                edgeMap,
                referenceHeight,
                currentReferenceHeight,
                currentHorizontalMm,
                currentVerticalMm,
                partWidthScaleFactor);
            if (candidate.Score > best.Score)
                best = candidate;
        }

        return best;
    }

    private static AutoAlignSolution OptimizeAutoAlignmentForReferenceHeight(
        IReadOnlyList<Point> path,
        EdgeMap edgeMap,
        double referenceHeightMm,
        double baseReferenceHeightMm,
        double baseHorizontal,
        double baseVertical,
        double partWidthScaleFactor)
    {
        var mmPerPixel = ResolveMmPerPixel(referenceHeightMm, edgeMap.Height);
        var offsets = OptimizeAutoAlignmentOffsets(path, edgeMap, mmPerPixel, partWidthScaleFactor, baseHorizontal, baseVertical);
        var score = offsets.Score - 0.02 * Math.Abs(referenceHeightMm - baseReferenceHeightMm);
        return new AutoAlignSolution(referenceHeightMm, offsets.Horizontal, offsets.Vertical, score);
    }

    private static OffsetAlignSolution OptimizeAutoAlignmentOffsets(
        IReadOnlyList<Point> path,
        EdgeMap edgeMap,
        double mmPerPixel,
        double partWidthScaleFactor,
        double baseHorizontal,
        double baseVertical)
    {
        var bestHorizontal = baseHorizontal;
        var bestVertical = baseVertical;
        var bestScore = EvaluateAutoAlignScore(path, edgeMap, mmPerPixel, partWidthScaleFactor, baseHorizontal, baseVertical, bestHorizontal, bestVertical);

        for (var h = baseHorizontal - AutoAlignHorizontalSearchWindowMm; h <= baseHorizontal + AutoAlignHorizontalSearchWindowMm; h += AutoAlignOffsetCoarseStepMm)
        {
            for (var v = baseVertical - AutoAlignVerticalSearchWindowMm; v <= baseVertical + AutoAlignVerticalSearchWindowMm; v += AutoAlignOffsetCoarseStepMm)
            {
                var score = EvaluateAutoAlignScore(path, edgeMap, mmPerPixel, partWidthScaleFactor, baseHorizontal, baseVertical, h, v);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHorizontal = h;
                    bestVertical = v;
                }
            }
        }

        for (var h = bestHorizontal - AutoAlignOffsetFineWindowMm; h <= bestHorizontal + AutoAlignOffsetFineWindowMm; h += AutoAlignOffsetFineStepMm)
        {
            for (var v = bestVertical - AutoAlignOffsetFineWindowMm; v <= bestVertical + AutoAlignOffsetFineWindowMm; v += AutoAlignOffsetFineStepMm)
            {
                var score = EvaluateAutoAlignScore(path, edgeMap, mmPerPixel, partWidthScaleFactor, baseHorizontal, baseVertical, h, v);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestHorizontal = h;
                    bestVertical = v;
                }
            }
        }

        return new OffsetAlignSolution(bestHorizontal, bestVertical, bestScore);
    }

    private static double EvaluateAutoAlignScore(
        IReadOnlyList<Point> path,
        EdgeMap edgeMap,
        double mmPerPixel,
        double partWidthScaleFactor,
        double baseHorizontal,
        double baseVertical,
        double horizontal,
        double vertical)
    {
        if (path.Count == 0 || mmPerPixel <= 1e-9)
            return double.NegativeInfinity;

        var imageWidthMm = edgeMap.Width * mmPerPixel * Math.Max(1e-6, partWidthScaleFactor);
        var leftWorld = horizontal - imageWidthMm * 0.5;
        var step = Math.Max(1, path.Count / 240);
        var sum = 0.0;
        var hits = 0;

        for (var i = 0; i < path.Count; i += step)
        {
            var worldX = path[i].X;
            var worldY = path[i].Y + vertical;
            var px = (worldX - leftWorld) / mmPerPixel;
            var py = edgeMap.Height - (worldY / mmPerPixel);
            var sample = SampleEdgeMagnitude(edgeMap, px, py);
            if (sample < 0)
                continue;

            sum += sample;
            hits++;
        }

        if (hits < 24)
            return double.NegativeInfinity;

        var average = sum / hits;
        // Refine around the current calibration instead of jumping to distant false maxima on the PNG.
        var deviationPenalty = 0.02 * Math.Abs(horizontal - baseHorizontal)
            + 0.018 * Math.Abs(vertical - baseVertical);
        return average - deviationPenalty;
    }

    private static double SampleEdgeMagnitude(EdgeMap edgeMap, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return -1;

        if (x < 1 || y < 1 || x >= edgeMap.Width - 2 || y >= edgeMap.Height - 2)
            return -1;

        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = x - x0;
        var ty = y - y0;

        var i00 = y0 * edgeMap.Width + x0;
        var i10 = i00 + 1;
        var i01 = i00 + edgeMap.Width;
        var i11 = i01 + 1;

        var v00 = edgeMap.Magnitude[i00];
        var v10 = edgeMap.Magnitude[i10];
        var v01 = edgeMap.Magnitude[i01];
        var v11 = edgeMap.Magnitude[i11];

        var top = v00 + (v10 - v00) * tx;
        var bottom = v01 + (v11 - v01) * tx;
        return top + (bottom - top) * ty;
    }

    private double ResolveMmPerPixel(double referenceHeightMm)
    {
        if (_partImage is null || _partImage.Size.Height <= 0)
            return 1.0;

        return ResolveMmPerPixel(referenceHeightMm, _partImage.Size.Height);
    }

    private static double ResolveMmPerPixel(double referenceHeightMm, double pixelHeight)
    {
        if (pixelHeight <= 0)
            return 1.0;

        return referenceHeightMm / pixelHeight;
    }

    private static Rect CreateWorldRectCenteredAtX(double centerX, double bottomZ, Bitmap? image, double mmPerPixel, double fallbackHeightMm, double widthScaleFactor)
    {
        if (image is null || image.Size.Height <= 0 || image.Size.Width <= 0)
            return new Rect(centerX - 250 * widthScaleFactor, bottomZ, 500 * widthScaleFactor, fallbackHeightMm);

        var w = image.Size.Width * mmPerPixel * widthScaleFactor;
        var h = image.Size.Height * mmPerPixel;
        return new Rect(centerX - w / 2.0, bottomZ, w, h);
    }

    private static double ResolvePartWidthScaleFactor(double partWidthScalePercent)
        => Math.Clamp(partWidthScalePercent, 50.0, 150.0) / 100.0;

    private Rect CreateManipulatorRectFromNozzlePivot(Point pivotWorld, Bitmap? image, double mmPerPixel)
    {
        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(pivotWorld.X - 380, pivotWorld.Y - 220, 500, 260);

        var w = image.Size.Width * mmPerPixel;
        var h = image.Size.Height * mmPerPixel;

        // Anchor manipulator to nozzle working point (near lower-left hinge in source image).
        var (tipX, tipY) = ResolveManipulatorAnchors();
        var left = pivotWorld.X - w * tipX;
        var bottom = pivotWorld.Y - h * (1.0 - tipY);
        return new Rect(left, bottom, w, h);
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

    private static Rect ComputePathBounds(IReadOnlyList<Point> path)
    {
        if (path.Count == 0)
            return new Rect(0, 0, 1, 1);

        var minX = path.Min(p => p.X);
        var maxX = path.Max(p => p.X);
        var minZ = path.Min(p => p.Y);
        var maxZ = path.Max(p => p.Y);
        return new Rect(minX, minZ, Math.Max(1, maxX - minX), Math.Max(1, maxZ - minZ));
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

    private void DrawNozzleImageWorld(DrawingContext context, Bitmap? image, Point pivotWorld, Point directionWorld, double mmPerPixel)
    {
        if (image is null)
            return;

        var baseWidthMm = image.Size.Width * mmPerPixel;
        var baseHeightMm = image.Size.Height * mmPerPixel;
        var widthPx = Math.Abs(baseWidthMm * _scale);
        var heightPx = Math.Abs(baseHeightMm * _scale);
        if (widthPx < 1 || heightPx < 1)
            return;

        var pivotScreen = WorldToScreen(pivotWorld);
        var anchorX = Math.Clamp(ResolveNozzlePivotAnchorX(), 0.0, 1.0);
        var anchorY = Math.Clamp(ResolveNozzlePivotAnchorY(), 0.0, 1.0);
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

    private readonly record struct PathSample(bool IsValid, int SegmentIndex, double SegmentT, Point Position);

    private Point ResolveFactTipWorld(Point fallback)
    {
        if (!double.IsFinite(ToolXRaw) || !double.IsFinite(ToolZRaw))
            return fallback;

        return new Point(ToVisualX(ToolXRaw), ToolZRaw + VerticalOffsetMm);
    }

    private Point ResolvePivotFromTip(Point tipWorld, Point referencePathPoint, double mmPerPixel, double centerY)
    {
        var upperBlend = ResolveUpperZoneBlend(referencePathPoint.Y, centerY);
        var dir = ResolvePivotDirection(upperBlend);
        var nozzleTipDistanceMm = ResolveNozzleTipDistanceMm(mmPerPixel);
        var rightShift = upperBlend * UpperZoneExtraPivotRightMm;
        var extraDrop = upperBlend * UpperZoneExtraPivotDropMm;
        return new Point(
            tipWorld.X + dir.X * nozzleTipDistanceMm + rightShift,
            tipWorld.Y + dir.Y * nozzleTipDistanceMm - BasePivotDropMm - extraDrop);
    }

    private static PathSample FindNearestPathSample(IReadOnlyList<Point> path, Point query)
    {
        if (path.Count < 2)
            return new PathSample(false, 0, 0.0, default);

        var bestDist2 = double.PositiveInfinity;
        var bestIndex = 0;
        var bestT = 0.0;
        var bestPoint = path[0];

        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = path[i];
            var b = path[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            if (len2 <= 1e-9)
                continue;

            var t = ((query.X - a.X) * dx + (query.Y - a.Y) * dy) / len2;
            t = Math.Clamp(t, 0.0, 1.0);
            var px = a.X + dx * t;
            var py = a.Y + dy * t;
            var qx = query.X - px;
            var qy = query.Y - py;
            var dist2 = qx * qx + qy * qy;
            if (dist2 >= bestDist2)
                continue;

            bestDist2 = dist2;
            bestIndex = i;
            bestT = t;
            bestPoint = new Point(px, py);
        }

        return double.IsFinite(bestDist2)
            ? new PathSample(true, bestIndex, bestT, bestPoint)
            : new PathSample(false, 0, 0.0, default);
    }

    private static (Point Position, Point Direction, int SegmentIndex, double SegmentT) Interpolate(IReadOnlyList<Point> pts, double progress)
    {
        if (pts.Count == 0)
            return (default, new Point(1, 0), 0, 0);

        if (pts.Count == 1)
            return (pts[0], new Point(1, 0), 0, 0);

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
                    i,
                    t);
            }

            acc = next;
        }

        return (
            pts[^1],
            new Point(pts[^1].X - pts[^2].X, pts[^1].Y - pts[^2].Y),
            pts.Count - 2,
            1.0);
    }

    private static List<RecipePoint> SelectRenderablePoints(List<RecipePoint> source)
    {
        // 2D view consumes animation points as-is so Safe transitions are preserved.
        return source;
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

    private List<Point> ApplyTrajectoryOffsets(IReadOnlyList<Point> source)
    {
        if (source.Count == 0)
            return new List<Point>();

        var verticalOffset = VerticalOffsetMm;
        if (Math.Abs(verticalOffset) <= 1e-6)
            return source.ToList();

        return source.Select(p => new Point(p.X, p.Y + verticalOffset)).ToList();
    }

    private static List<Point> BuildRenderPath(
        IReadOnlyList<Point> sourcePath,
        double thresholdMm)
    {
        if (sourcePath.Count < 2 || thresholdMm <= 1e-6)
            return sourcePath.ToList();

        var densePath = new List<Point> { sourcePath[0] };

        for (var i = 0; i < sourcePath.Count - 1; i++)
        {
            var a = sourcePath[i];
            var b = sourcePath[i + 1];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var pieces = Math.Max(1, (int)Math.Ceiling(dist / thresholdMm));

            for (var piece = 1; piece <= pieces; piece++)
            {
                var t = piece / (double)pieces;
                densePath.Add(new Point(a.X + dx * t, a.Y + dy * t));
            }
        }

        return densePath;
    }

    private Point ResolveNozzleDirection(Point fallbackDirection)
    {
        // For 2D overlay, orient nozzle by trajectory tangent (same visual logic as chart).
        var len = Math.Sqrt(fallbackDirection.X * fallbackDirection.X + fallbackDirection.Y * fallbackDirection.Y);
        return len <= 1e-6 ? new Point(1, 0) : new Point(fallbackDirection.X / len, fallbackDirection.Y / len);
    }

    private static double ResolveApproachDistance(Rect partRect)
        => Math.Max(160, partRect.Width * ApproachDistanceFactor);

    private Point ResolveNozzlePivot(Point tipWorld, Point direction, double mmPerPixel)
    {
        var delta = ResolveNozzleTipDistanceMm(mmPerPixel);
        return new Point(tipWorld.X + direction.X * delta, tipWorld.Y + direction.Y * delta);
    }

    private double ResolveNozzleTipDistanceMm(double mmPerPixel)
    {
        if (_nozzleImage is null || _nozzleImage.Size.Width <= 0)
            return 120;

        var tipAnchor = Math.Clamp(ResolveNozzleTipAnchorX(), 0.0, 1.0);
        var pivotAnchor = Math.Clamp(ResolveNozzlePivotAnchorX(), 0.0, 1.0);
        var widthMm = _nozzleImage.Size.Width * mmPerPixel;
        return Math.Max(1e-6, (pivotAnchor - tipAnchor) * widthMm);
    }

    private void MarkRefit()
    {
        _needsRefit = true;
        InvalidateVisual();
    }

    private List<Point> BuildManipulatorFollowPath(IReadOnlyList<Point> tipPath, Point initialPivot, double mmPerPixel, double centerY)
    {
        var pivots = new List<Point>(tipPath.Count);
        if (tipPath.Count == 0)
            return pivots;

        var currentPivot = initialPivot;
        var nozzleTipDistanceMm = ResolveNozzleTipDistanceMm(mmPerPixel);
        for (var i = 0; i < tipPath.Count; i++)
        {
            var tip = tipPath[i];
            var upperBlend = ResolveUpperZoneBlend(tip.Y, centerY);
            var dir = ResolvePivotDirection(upperBlend);
            var rightShift = upperBlend * UpperZoneExtraPivotRightMm;
            var extraDrop = upperBlend * UpperZoneExtraPivotDropMm;
            var targetPivot = new Point(
                tip.X + dir.X * nozzleTipDistanceMm + rightShift,
                tip.Y + dir.Y * nozzleTipDistanceMm - BasePivotDropMm - extraDrop);
            currentPivot = LerpPoint(currentPivot, targetPivot, PivotFollowSmoothing);
            pivots.Add(currentPivot);
        }

        return pivots;
    }

    private static double ResolveUpperZoneBlend(double tipY, double centerY)
    {
        var start = centerY + UpperZoneBlendStartMm;
        var t = (tipY - start) / Math.Max(1e-6, UpperZoneBlendSpanMm);
        return Math.Clamp(t, 0.0, 1.0);
    }

    private Point ResolvePivotDirection(double upperBlend)
    {
        var lowerDir = new Point(1.0, 0.0);
        // In upper zone rotate nozzle downward while keeping pivot on the right side.
        var upperDir = new Point(0.28, -0.96);
        var blended = new Point(
            lowerDir.X + (upperDir.X - lowerDir.X) * upperBlend,
            lowerDir.Y + (upperDir.Y - lowerDir.Y) * upperBlend);
        return ResolveNozzleDirection(blended);
    }

    private static (Point Tip, Point Pivot, bool IsProcessing, int ProcessSegmentIndex, double ProcessSegmentT) ResolveMotionState(
        double timelineProgress,
        IReadOnlyList<Point> processTipPath,
        IReadOnlyList<Point> processPivotPath,
        Point centerTip,
        Point centerPivot,
        Point approachStartTip,
        Point approachStartPivot)
    {
        var progress = Math.Clamp(timelineProgress, 0.0, 1.0);
        if (progress <= ApproachPhase + 1e-6)
        {
            var t = ApproachPhase <= 1e-6 ? 1.0 : progress / ApproachPhase;
            return (
                LerpPoint(approachStartTip, centerTip, t),
                LerpPoint(approachStartPivot, centerPivot, t),
                false,
                0,
                0);
        }

        if (processTipPath.Count == 0 || processPivotPath.Count == 0)
            return (centerTip, centerPivot, false, 0, 0);

        var transferEnd = Math.Min(1.0, ApproachPhase + CenterTransferPhase);
        if (progress <= transferEnd + 1e-6)
        {
            var transferSpan = Math.Max(1e-6, transferEnd - ApproachPhase);
            var t = (progress - ApproachPhase) / transferSpan;
            return (
                LerpPoint(centerTip, processTipPath[0], t),
                LerpPoint(centerPivot, processPivotPath[0], t),
                false,
                0,
                t);
        }

        var processProgress = transferEnd >= 1.0
            ? 1.0
            : (progress - transferEnd) / (1.0 - transferEnd);
        var processState = Interpolate(processTipPath, processProgress);
        var pivot = InterpolateBySegment(processPivotPath, processState.SegmentIndex, processState.SegmentT);
        return (processState.Position, pivot, true, processState.SegmentIndex, processState.SegmentT);
    }

    private static Point InterpolateBySegment(IReadOnlyList<Point> path, int segmentIndex, double segmentT)
    {
        if (path.Count == 0)
            return default;

        if (path.Count == 1)
            return path[0];

        var seg = Math.Clamp(segmentIndex, 0, path.Count - 2);
        var t = Math.Clamp(segmentT, 0.0, 1.0);
        return LerpPoint(path[seg], path[seg + 1], t);
    }

    private static List<Point> BuildMotionPath(IReadOnlyList<Point> processPath, Point centerPoint, Point approachStartPoint)
    {
        var result = new List<Point>
        {
            approachStartPoint,
            centerPoint
        };

        if (processPath.Count == 0)
            return result;

        result.Add(processPath[0]);
        for (var i = 1; i < processPath.Count; i++)
            result.Add(processPath[i]);

        return result;
    }

    private static List<Point> BuildPassedPath(IReadOnlyList<Point> path, int segmentIndex, double segmentT, Point currentTip)
    {
        if (path.Count < 2)
            return new List<Point>();

        var seg = Math.Clamp(segmentIndex, 0, path.Count - 2);
        var passed = path.Take(seg + 1).ToList();
        var exact = LerpPoint(path[seg], path[seg + 1], segmentT);
        passed.Add(double.IsFinite(currentTip.X) && double.IsFinite(currentTip.Y) ? currentTip : exact);
        return passed;
    }

    private static Point LerpPoint(Point a, Point b, double t)
    {
        var tt = Math.Clamp(t, 0.0, 1.0);
        return new Point(
            a.X + (b.X - a.X) * tt,
            a.Y + (b.Y - a.Y) * tt);
    }
}
