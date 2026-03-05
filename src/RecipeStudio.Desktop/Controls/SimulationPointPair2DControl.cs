using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
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

    public static readonly StyledProperty<double> ReferenceHeightMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ReferenceHeightMm), 1309.49);

    public static readonly StyledProperty<bool> InvertHorizontalProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(InvertHorizontal), true);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<double> VerticalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(VerticalOffsetMm), SimulationBlueprint2DControl.DefaultVerticalOffsetMm);

    public static readonly StyledProperty<double> HorizontalOffsetMmProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(HorizontalOffsetMm), SimulationBlueprint2DControl.DefaultHorizontalOffsetMm);

    public static readonly StyledProperty<double> ManipulatorAnchorXProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ManipulatorAnchorX), SimulationBlueprint2DControl.DefaultManipulatorAnchorX);

    public static readonly StyledProperty<double> ManipulatorAnchorYProperty =
        AvaloniaProperty.Register<SimulationPointPair2DControl, double>(nameof(ManipulatorAnchorY), SimulationBlueprint2DControl.DefaultManipulatorAnchorY);

    private const double Pad = 20;
    private Rect _fitWorldBounds;
    private Rect _worldBounds;
    private double _scale;
    private double _fitScale;
    private double _zoomFactor = 1.0;
    private bool _needsRefit = true;
    private Size _lastRenderSize;

    private readonly Bitmap? _partImage;
    private readonly Bitmap? _manipulatorImage;

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

    public double ZoomFactor => _zoomFactor;
    public event Action<double>? ZoomChanged;

    static SimulationPointPair2DControl()
    {
        PointsProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ProgressProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentIndexProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        CurrentSegmentTProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        ToolXRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        ToolZRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        TargetXRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        TargetZRawProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        SettingsProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ReferenceHeightMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        InvertHorizontalProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ShowGridProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.InvalidateVisual());
        VerticalOffsetMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        HorizontalOffsetMmProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorXProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
        ManipulatorAnchorYProperty.Changed.AddClassHandler<SimulationPointPair2DControl>((c, _) => c.MarkRefit());
    }

    public SimulationPointPair2DControl()
    {
        ClipToBounds = true;
        _partImage = TryLoadBitmap("avares://RecipeStudio.Desktop/Assets/Images/H340_KAMA_1.fw.png");
        _manipulatorImage = TryLoadBitmap("avares://RecipeStudio.Desktop/Assets/Images/manipulator.fw.png");
    }

    public void ZoomIn()
    {
        _zoomFactor = Math.Clamp(_zoomFactor * 1.2, 0.2, 20.0);
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public void ZoomOut()
    {
        _zoomFactor = Math.Clamp(_zoomFactor / 1.2, 0.2, 20.0);
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public void ResetZoom()
    {
        _zoomFactor = 1.0;
        _needsRefit = true;
        InvalidateVisual();
        ZoomChanged?.Invoke(_zoomFactor);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(new SolidColorBrush(Color.FromRgb(18, 22, 30)), Bounds);

        var referenceHeightMm = Math.Max(100, ReferenceHeightMm);
        var mmPerPixel = ResolveMmPerPixel(referenceHeightMm);
        var partRectWorld = CreateWorldRectCenteredAtX(HorizontalOffsetMm, 0, _partImage, mmPerPixel, referenceHeightMm);

        var point1 = ResolvePoint1World();
        var point2 = ResolvePoint2World(point1);
        var manipRectWorld = CreateManipulatorRectFromAnchor(point2, _manipulatorImage, mmPerPixel);
        var worldBounds = ComputeWorldBounds(partRectWorld, manipRectWorld, point1, point2);

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
            DrawPairLink(context, point1, point2);
            DrawPointMarker(context, point1, "1");
            DrawPointMarker(context, point2, "2");
            DrawImageWorld(context, _manipulatorImage, manipRectWorld);
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

    private Rect CreateManipulatorRectFromAnchor(Point anchorWorld, Bitmap? image, double mmPerPixel)
    {
        if (image is null || image.Size.Width <= 0 || image.Size.Height <= 0)
            return new Rect(anchorWorld.X - 380, anchorWorld.Y - 220, 500, 260);

        var w = image.Size.Width * mmPerPixel;
        var h = image.Size.Height * mmPerPixel;
        var anchorX = Math.Clamp(ManipulatorAnchorX, 0.0, 1.0);
        var anchorY = Math.Clamp(ManipulatorAnchorY, 0.0, 1.0);
        var left = anchorWorld.X - w * anchorX;
        var bottom = anchorWorld.Y - h * (1.0 - anchorY);
        return new Rect(left, bottom, w, h);
    }

    private Point ResolvePoint1World()
    {
        if (double.IsFinite(TargetXRaw) && double.IsFinite(TargetZRaw))
            return new Point(ToVisualX(TargetXRaw), TargetZRaw + VerticalOffsetMm);

        return EvaluateTargetBySegment();
    }

    private Point ResolvePoint2World(Point fallback)
    {
        if (double.IsFinite(ToolXRaw) && double.IsFinite(ToolZRaw))
            return new Point(ToVisualX(ToolXRaw), ToolZRaw + VerticalOffsetMm);

        return fallback;
    }

    private Point EvaluateTargetBySegment()
    {
        var source = Points?.ToList() ?? new List<RecipePoint>();
        if (source.Count == 0)
            return new Point(ToVisualX(0), VerticalOffsetMm);

        var hZone = Settings?.HZone ?? 1200;
        if (source.Count == 1)
        {
            var only = source[0].GetTargetPoint(hZone);
            return new Point(ToVisualX(only.Xp), only.Zp + VerticalOffsetMm);
        }

        var seg = Math.Clamp(CurrentSegmentIndex, 0, source.Count - 2);
        var t = Math.Clamp(CurrentSegmentT, 0.0, 1.0);
        var a = source[seg].GetTargetPoint(hZone);
        var b = source[seg + 1].GetTargetPoint(hZone);
        var x = a.Xp + (b.Xp - a.Xp) * t;
        var z = a.Zp + (b.Zp - a.Zp) * t;
        return new Point(ToVisualX(x), z + VerticalOffsetMm);
    }

    private static Rect ComputeWorldBounds(Rect partRect, Rect manipRect, Point point1, Point point2)
    {
        var minX = Math.Min(Math.Min(partRect.Left, manipRect.Left), Math.Min(point1.X, point2.X));
        var maxX = Math.Max(Math.Max(partRect.Right, manipRect.Right), Math.Max(point1.X, point2.X));
        var minZ = Math.Min(Math.Min(partRect.Top, manipRect.Top), Math.Min(point1.Y, point2.Y));
        var maxZ = Math.Max(Math.Max(partRect.Bottom, manipRect.Bottom), Math.Max(point1.Y, point2.Y));

        var w = Math.Max(1, maxX - minX);
        var h = Math.Max(1, maxZ - minZ);
        const double marginX = 220;
        const double marginZ = 420;
        return new Rect(minX - marginX, minZ - marginZ, w + marginX * 2, h + marginZ * 2);
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

    private void DrawPairLink(DrawingContext context, Point point1, Point point2)
    {
        var red = Color.FromRgb(239, 68, 68);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, red.R, red.G, red.B)), 7);
        context.DrawLine(pen, WorldToScreen(point1), WorldToScreen(point2));
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
        ApplyCurrentView();
    }

    private void ApplyCurrentView()
    {
        if (_fitWorldBounds.Width <= 0 || _fitWorldBounds.Height <= 0)
            return;

        var centerX = _fitWorldBounds.Center.X;
        var centerZ = _fitWorldBounds.Center.Y;
        var zoomedWidth = _fitWorldBounds.Width / _zoomFactor;
        var zoomedHeight = _fitWorldBounds.Height / _zoomFactor;
        _worldBounds = new Rect(centerX - zoomedWidth / 2.0, centerZ - zoomedHeight / 2.0, zoomedWidth, zoomedHeight);
        _scale = _fitScale * _zoomFactor;
    }

    private Point WorldToScreen(Point p)
    {
        var x = Pad + (p.X - _worldBounds.Left) * _scale;
        var y = Bounds.Height - Pad - (p.Y - _worldBounds.Top) * _scale;
        return new Point(x, y);
    }

    private double ToVisualX(double x) => InvertHorizontal ? -x : x;

    private void MarkRefit()
    {
        _needsRefit = true;
        InvalidateVisual();
    }
}
