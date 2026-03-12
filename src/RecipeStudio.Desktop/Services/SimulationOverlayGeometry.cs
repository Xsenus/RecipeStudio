using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace RecipeStudio.Desktop.Services;

public readonly record struct PlotMarkerGeometry(Point ToolPoint, Point TargetPoint, Point Direction);

public readonly record struct PairOverlayGeometry(Point TargetPoint, Point ToolPoint, Point NozzleTipPoint);

public readonly record struct StyledTargetPoint(Point Position, bool Safe);

public static class SimulationOverlayGeometry
{
    public static Point ProjectWorldPoint(double x, double z, bool invertHorizontal, double verticalOffsetMm = 0)
        => new(invertHorizontal ? -x : x, z + verticalOffsetMm);

    public static bool ShouldDrawOriginalTarget(string mode)
        => SimulationTargetDisplayModes.Normalize(mode) != SimulationTargetDisplayModes.Mirrored;

    public static bool ShouldDrawMirroredTarget(string mode)
        => SimulationTargetDisplayModes.Normalize(mode) != SimulationTargetDisplayModes.Original;

    public static PlotMarkerGeometry ResolvePlotMarkerGeometry(
        Point toolPosition,
        Point targetPosition,
        Point? rawToolWorld,
        Point? rawTargetWorld,
        bool invertHorizontal,
        bool usePhysicalOrientation,
        Point physicalDirection)
    {
        var toolPoint = rawToolWorld is { } rawTool
            ? ProjectWorldPoint(rawTool.X, rawTool.Y, invertHorizontal)
            : toolPosition;
        var targetPoint = rawTargetWorld is { } rawTarget
            ? ProjectWorldPoint(rawTarget.X, rawTarget.Y, invertHorizontal)
            : targetPosition;

        var direction = usePhysicalOrientation
            ? ApplyHorizontalInversion(physicalDirection, invertHorizontal)
            : new Point(targetPoint.X - toolPoint.X, targetPoint.Y - toolPoint.Y);

        return new PlotMarkerGeometry(toolPoint, targetPoint, direction);
    }

    public static PairOverlayGeometry ResolvePairOverlayGeometry(
        PlotMarkerGeometry marker,
        double verticalOffsetMm,
        bool usePhysicalOrientation,
        double nozzleLengthMm)
    {
        var targetPoint = OffsetVertical(marker.TargetPoint, verticalOffsetMm);
        var toolPoint = OffsetVertical(marker.ToolPoint, verticalOffsetMm);
        var nozzleTipPoint = usePhysicalOrientation
            ? new Point(toolPoint.X + marker.Direction.X * nozzleLengthMm, toolPoint.Y + marker.Direction.Y * nozzleLengthMm)
            : targetPoint;

        return new PairOverlayGeometry(targetPoint, toolPoint, nozzleTipPoint);
    }

    public static List<Point> BuildDisplayedTargetPoints(IList<Point> source, double mirrorAxisX, string mode)
    {
        var normalizedMode = SimulationTargetDisplayModes.Normalize(mode);
        if (normalizedMode == SimulationTargetDisplayModes.Original)
            return source.ToList();

        if (normalizedMode == SimulationTargetDisplayModes.Mirrored)
            return source.Select(point => MirrorPoint(point, mirrorAxisX)).ToList();

        var result = new List<Point>(source.Count * 2);
        foreach (var point in source)
            AddDistinctPoint(result, point);

        foreach (var point in source)
            AddDistinctPoint(result, MirrorPoint(point, mirrorAxisX));

        return result;
    }

    public static List<StyledTargetPoint> BuildDisplayedTargetPoints(IList<StyledTargetPoint> source, double mirrorAxisX, string mode)
    {
        var normalizedMode = SimulationTargetDisplayModes.Normalize(mode);
        if (normalizedMode == SimulationTargetDisplayModes.Original)
            return source.ToList();

        if (normalizedMode == SimulationTargetDisplayModes.Mirrored)
            return source
                .Select(point => point with { Position = MirrorPoint(point.Position, mirrorAxisX) })
                .ToList();

        var result = new List<StyledTargetPoint>(source.Count * 2);
        foreach (var point in source)
        {
            result.Add(point);
            result.Add(point with { Position = MirrorPoint(point.Position, mirrorAxisX) });
        }

        return result;
    }

    public static Point MirrorPoint(Point point, double mirrorAxisX)
        => new(mirrorAxisX * 2.0 - point.X, point.Y);

    public static ProfileDisplayPath MirrorProfileDisplayPath(ProfileDisplayPath source)
    {
        static Point Mirror(Point point) => MirrorPoint(point, 0);

        return new ProfileDisplayPath(
            source.Polylines
                .Select(polyline => new ProfilePolylineData(
                    polyline.GroupName,
                    polyline.ControlPoints.Select(Mirror).ToList(),
                    polyline.CurvePoints.Select(Mirror).ToList(),
                    polyline.PointNumbers.ToList()))
                .ToList(),
            source.PathNodes
                .Select(node => node with
                {
                    A0 = Mirror(node.A0),
                    A1 = Mirror(node.A1),
                    B0 = Mirror(node.B0)
                })
                .ToList(),
            source.B0PolylinePoints.Select(Mirror).ToList(),
            source.B0PointNumbers.ToList(),
            source.FrameSamples
                .Select(sample => sample with
                {
                    A1 = Mirror(sample.A1),
                    B0 = Mirror(sample.B0)
                })
                .ToList(),
            source.TotalPathLength,
            source.TotalDurationSec);
    }

    public static IEnumerable<Point> EnumerateProfileDisplayPoints(ProfileDisplayPath displayPath)
        => displayPath.Polylines.SelectMany(polyline => polyline.ControlPoints.Concat(polyline.CurvePoints))
            .Concat(displayPath.B0PolylinePoints)
            .Concat(displayPath.PathNodes.Select(node => node.A1))
            .Concat(displayPath.PathNodes.Select(node => node.B0))
            .Concat(displayPath.FrameSamples.Select(frame => frame.A1))
            .Concat(displayPath.FrameSamples.Select(frame => frame.B0));

    private static Point ApplyHorizontalInversion(Point direction, bool invertHorizontal)
        => invertHorizontal ? new Point(-direction.X, direction.Y) : direction;

    private static Point OffsetVertical(Point point, double verticalOffsetMm)
        => new(point.X, point.Y + verticalOffsetMm);

    private static void AddDistinctPoint(List<Point> points, Point candidate)
    {
        const double eps = 1e-3;
        foreach (var point in points)
        {
            if (System.Math.Abs(point.X - candidate.X) <= eps && System.Math.Abs(point.Y - candidate.Y) <= eps)
                return;
        }

        points.Add(candidate);
    }
}
