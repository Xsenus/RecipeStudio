using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed record ProfilePolylineData(
    string GroupName,
    IReadOnlyList<Point> ControlPoints,
    IReadOnlyList<Point> CurvePoints,
    IReadOnlyList<int> PointNumbers);

public readonly record struct ProfileFrameOverlaySample(
    Point A1,
    Point B0,
    int NPoint,
    string GroupName);

public readonly record struct ProfilePathNode(
    int PathIndex,
    int SourceIndex,
    int NPoint,
    int Place,
    string GroupName,
    Point A0,
    Point A1,
    Point B0,
    double ANozzle,
    double AlfaDisplay,
    double Beta,
    double ArcLength);

public sealed record ProfileDisplayPath(
    IReadOnlyList<ProfilePolylineData> Polylines,
    IReadOnlyList<ProfilePathNode> PathNodes,
    IReadOnlyList<Point> B0PolylinePoints,
    IReadOnlyList<int> B0PointNumbers,
    IReadOnlyList<ProfileFrameOverlaySample> FrameSamples,
    double TotalPathLength,
    double TotalDurationSec)
{
    public static ProfileDisplayPath Empty { get; } = new(
        Array.Empty<ProfilePolylineData>(),
        Array.Empty<ProfilePathNode>(),
        Array.Empty<Point>(),
        Array.Empty<int>(),
        Array.Empty<ProfileFrameOverlaySample>(),
        0,
        0);
}

public readonly record struct ProfileAnimationSample(
    bool IsValid,
    Point A0,
    Point A1,
    Point B0,
    Point SelectedA0,
    Point SelectedB0,
    int SelectedPathIndex,
    int SegmentIndex,
    double SegmentT,
    double Progress,
    int NPoint,
    string GroupName,
    double AlfaDisplay,
    double Beta,
    double ANozzle);

public sealed class ProfileDisplayPathService
{
    public const double DefaultAnimationDurationSec = 20.0;
    public const int DefaultPreviewFrameCount = 2000;
    public const string Group1Name = "\u0413\u0440\u0443\u043F\u043F\u0430 1";
    public const string Group2Name = "\u0413\u0440\u0443\u043F\u043F\u0430 2";
    public const string Group3Name = "\u0413\u0440\u0443\u043F\u043F\u0430 3";
    public const string Group4Name = "\u0413\u0440\u0443\u043F\u043F\u0430 4";

    public ProfileDisplayPath Build(IList<RecipePoint>? source, AppSettings? settings)
    {
        settings ??= new AppSettings();
        var original = source?.ToList() ?? new List<RecipePoint>();
        if (original.Count == 0)
            return ProfileDisplayPath.Empty;

        var points = SelectWorkingPoints(original);
        if (points.Count == 0)
            return ProfileDisplayPath.Empty;

        var hFreeZ = Math.Max(0, settings.HFreeZ);
        var group1 = new List<(RecipePoint Point, int SourceIndex, Point Position)>();
        var group2 = new List<(RecipePoint Point, int SourceIndex, Point Position)>();
        var group3 = new List<(RecipePoint Point, int SourceIndex, Point Position)>();
        var group4 = new List<(RecipePoint Point, int SourceIndex, Point Position)>();

        for (var i = 0; i < original.Count; i++)
        {
            var point = original[i];
            if (!points.Contains(point))
                continue;

            if (point.Place == 0)
            {
                group1.Add((point, i, new Point(-point.RCrd, point.ZCrd)));
                continue;
            }

            group2.Add((point, i, new Point(-point.RCrd, point.ZCrd)));
            group3.Add((point, i, new Point(-point.RCrd, point.ZCrd + hFreeZ)));
            group4.Add((point, i, new Point(point.RCrd, point.ZCrd + hFreeZ)));
        }

        var polylines = new List<ProfilePolylineData>(4)
        {
            ToPolyline(Group1Name, group1),
            ToPolyline(Group2Name, group2),
            ToPolyline(Group3Name, group3),
            ToPolyline(Group4Name, group4)
        };

        var path = new List<ProfilePathNode>(group1.Count + group4.Count);
        var b0Polyline = new List<Point>(group1.Count + group4.Count);
        var b0PointNumbers = new List<int>(group1.Count + group4.Count);
        double length = 0;
        Point? previous = null;

        void AppendPathNode((RecipePoint Point, int SourceIndex, Point Position) item, string groupName)
        {
            var aNozzle = Math.Max(0, item.Point.ANozzle);
            var alfaDisplay = groupName == Group4Name ? -item.Point.Alfa : item.Point.Alfa;
            var segment = ProfileViewGeometry.ResolveSegmentPoints(item.Position, aNozzle, alfaDisplay, settings.Lz);
            if (previous is { } prev)
                length += Distance(prev, item.Position);

            var node = new ProfilePathNode(
                PathIndex: path.Count,
                SourceIndex: item.SourceIndex,
                NPoint: item.Point.NPoint,
                Place: item.Point.Place,
                GroupName: groupName,
                A0: item.Position,
                A1: segment.A1,
                B0: segment.B0,
                ANozzle: aNozzle,
                AlfaDisplay: alfaDisplay,
                Beta: item.Point.Betta,
                ArcLength: length);

            path.Add(node);
            b0Polyline.Add(segment.B0);
            b0PointNumbers.Add(item.Point.NPoint);
            previous = item.Position;
        }

        foreach (var item in group1)
            AppendPathNode(item, Group1Name);

        foreach (var item in group4)
            AppendPathNode(item, Group4Name);

        var previewPath = new ProfileDisplayPath(
            polylines,
            path,
            b0Polyline,
            b0PointNumbers,
            Array.Empty<ProfileFrameOverlaySample>(),
            length,
            path.Count > 1 ? DefaultAnimationDurationSec : 0);

        var frameSamples = BuildFrameSamples(previewPath);

        return previewPath with
        {
            FrameSamples = frameSamples
        };
    }

    public ProfileAnimationSample Evaluate(ProfileDisplayPath path, double elapsedSec)
    {
        if (path.PathNodes.Count == 0)
            return default;

        if (path.PathNodes.Count == 1 || path.TotalDurationSec <= 1e-9 || path.TotalPathLength <= 1e-9)
            return ToExactSample(path, 0, progress: 0);

        var progress = Math.Clamp(elapsedSec / path.TotalDurationSec, 0.0, 1.0);
        return EvaluateByProgress(path, progress);
    }

    public ProfileAnimationSample EvaluateByProgress(ProfileDisplayPath path, double progress)
    {
        if (path.PathNodes.Count == 0)
            return default;

        if (path.PathNodes.Count == 1 || path.TotalPathLength <= 1e-9)
            return ToExactSample(path, 0, Math.Clamp(progress, 0.0, 1.0));

        progress = Math.Clamp(progress, 0.0, 1.0);
        var targetLength = path.TotalPathLength * progress;
        var selectedIndex = ResolveNearestNodeIndex(path, targetLength);

        for (var i = 0; i < path.PathNodes.Count - 1; i++)
        {
            var a = path.PathNodes[i];
            var b = path.PathNodes[i + 1];
            if (targetLength > b.ArcLength && i < path.PathNodes.Count - 2)
                continue;

            var segmentLength = Math.Max(1e-9, b.ArcLength - a.ArcLength);
            var t = Math.Clamp((targetLength - a.ArcLength) / segmentLength, 0.0, 1.0);
            var a0 = LerpPoint(a.A0, b.A0, t);
            var aNozzle = Lerp(a.ANozzle, b.ANozzle, t);
            var alfa = Lerp(a.AlfaDisplay, b.AlfaDisplay, t);
            var beta = Lerp(a.Beta, b.Beta, t);
            var nozzleLength = Distance(a.A1, a.B0);
            var segment = ProfileViewGeometry.ResolveSegmentPoints(a0, aNozzle, alfa, nozzleLength);
            var selected = path.PathNodes[selectedIndex];

            return new ProfileAnimationSample(
                IsValid: true,
                A0: a0,
                A1: segment.A1,
                B0: segment.B0,
                SelectedA0: selected.A0,
                SelectedB0: selected.B0,
                SelectedPathIndex: selectedIndex,
                SegmentIndex: i,
                SegmentT: t,
                Progress: progress,
                NPoint: selected.NPoint,
                GroupName: selected.GroupName,
                AlfaDisplay: alfa,
                Beta: beta,
                ANozzle: aNozzle);
        }

        return ToExactSample(path, path.PathNodes.Count - 1, 1.0);
    }

    public ProfileAnimationSample EvaluateAtNode(ProfileDisplayPath path, int nodeIndex)
    {
        if (path.PathNodes.Count == 0)
            return default;

        var index = Math.Clamp(nodeIndex, 0, path.PathNodes.Count - 1);
        var progress = GetNodeProgress(path, index);
        return ToExactSample(path, index, progress);
    }

    public double GetNodeProgress(ProfileDisplayPath path, int nodeIndex)
    {
        if (path.PathNodes.Count <= 1 || path.TotalPathLength <= 1e-9)
            return 0;

        var index = Math.Clamp(nodeIndex, 0, path.PathNodes.Count - 1);
        return path.PathNodes[index].ArcLength / path.TotalPathLength;
    }

    private static ProfileAnimationSample ToExactSample(ProfileDisplayPath path, int nodeIndex, double progress)
    {
        var index = Math.Clamp(nodeIndex, 0, path.PathNodes.Count - 1);
        var node = path.PathNodes[index];
        return new ProfileAnimationSample(
            IsValid: true,
            A0: node.A0,
            A1: node.A1,
            B0: node.B0,
            SelectedA0: node.A0,
            SelectedB0: node.B0,
            SelectedPathIndex: index,
            SegmentIndex: Math.Clamp(index, 0, Math.Max(0, path.PathNodes.Count - 2)),
            SegmentT: index >= path.PathNodes.Count - 1 ? 1.0 : 0.0,
            Progress: progress,
            NPoint: node.NPoint,
            GroupName: node.GroupName,
            AlfaDisplay: node.AlfaDisplay,
            Beta: node.Beta,
            ANozzle: node.ANozzle);
    }

    private static int ResolveNearestNodeIndex(ProfileDisplayPath path, double targetLength)
    {
        var bestIndex = 0;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < path.PathNodes.Count; i++)
        {
            var delta = Math.Abs(path.PathNodes[i].ArcLength - targetLength);
            if (delta >= bestDistance)
                continue;

            bestDistance = delta;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static ProfilePolylineData ToPolyline(string name, List<(RecipePoint Point, int SourceIndex, Point Position)> source)
        => new(
            name,
            source.Select(item => item.Position).ToList(),
            BuildCurve(source.Select(item => item.Position).ToList()),
            source.Select(item => item.Point.NPoint).ToList());

    private static IReadOnlyList<ProfileFrameOverlaySample> BuildFrameSamples(ProfileDisplayPath path)
    {
        if (path.PathNodes.Count == 0)
            return Array.Empty<ProfileFrameOverlaySample>();

        var frameCount = path.PathNodes.Count == 1 ? 1 : DefaultPreviewFrameCount;
        var samples = new List<ProfileFrameOverlaySample>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            var progress = frameCount == 1 ? 0.0 : (double)i / (frameCount - 1);
            var sample = EvaluateByProgressStatic(path, progress);
            samples.Add(new ProfileFrameOverlaySample(sample.A1, sample.B0, sample.NPoint, sample.GroupName));
        }

        return samples;
    }

    private static ProfileAnimationSample EvaluateByProgressStatic(ProfileDisplayPath path, double progress)
    {
        if (path.PathNodes.Count == 0)
            return default;

        if (path.PathNodes.Count == 1 || path.TotalPathLength <= 1e-9)
            return ToExactSample(path, 0, Math.Clamp(progress, 0.0, 1.0));

        progress = Math.Clamp(progress, 0.0, 1.0);
        var targetLength = path.TotalPathLength * progress;
        var selectedIndex = ResolveNearestNodeIndex(path, targetLength);

        for (var i = 0; i < path.PathNodes.Count - 1; i++)
        {
            var a = path.PathNodes[i];
            var b = path.PathNodes[i + 1];
            if (targetLength > b.ArcLength && i < path.PathNodes.Count - 2)
                continue;

            var segmentLength = Math.Max(1e-9, b.ArcLength - a.ArcLength);
            var t = Math.Clamp((targetLength - a.ArcLength) / segmentLength, 0.0, 1.0);
            var a0 = LerpPoint(a.A0, b.A0, t);
            var aNozzle = Lerp(a.ANozzle, b.ANozzle, t);
            var alfa = Lerp(a.AlfaDisplay, b.AlfaDisplay, t);
            var beta = Lerp(a.Beta, b.Beta, t);
            var nozzleLength = Distance(a.A1, a.B0);
            var segment = ProfileViewGeometry.ResolveSegmentPoints(a0, aNozzle, alfa, nozzleLength);
            var selected = path.PathNodes[selectedIndex];

            return new ProfileAnimationSample(
                IsValid: true,
                A0: a0,
                A1: segment.A1,
                B0: segment.B0,
                SelectedA0: selected.A0,
                SelectedB0: selected.B0,
                SelectedPathIndex: selectedIndex,
                SegmentIndex: i,
                SegmentT: t,
                Progress: progress,
                NPoint: selected.NPoint,
                GroupName: selected.GroupName,
                AlfaDisplay: alfa,
                Beta: beta,
                ANozzle: aNozzle);
        }

        return ToExactSample(path, path.PathNodes.Count - 1, 1.0);
    }

    private static IReadOnlyList<Point> BuildCurve(IReadOnlyList<Point> controlPoints, int sampleCount = 250)
    {
        if (controlPoints.Count < 2)
            return controlPoints.ToList();

        var filtered = new List<Point> { controlPoints[0] };
        for (var i = 1; i < controlPoints.Count; i++)
        {
            if (Distance(controlPoints[i - 1], controlPoints[i]) <= 1e-9)
                continue;

            filtered.Add(controlPoints[i]);
        }

        if (filtered.Count < 2)
            return filtered;

        if (filtered.Count == 2)
            return SampleLinear(filtered[0], filtered[1], sampleCount);

        var nodes = new double[filtered.Count];
        for (var i = 1; i < filtered.Count; i++)
            nodes[i] = nodes[i - 1] + Distance(filtered[i - 1], filtered[i]);

        var xs = SamplePchip(nodes, filtered.Select(point => point.X).ToArray(), sampleCount);
        var ys = SamplePchip(nodes, filtered.Select(point => point.Y).ToArray(), sampleCount);

        var points = new List<Point>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
            points.Add(new Point(xs[i], ys[i]));

        return points;
    }

    private static IReadOnlyList<Point> SampleLinear(Point a, Point b, int sampleCount)
    {
        var points = new List<Point>(sampleCount);
        for (var i = 0; i < sampleCount; i++)
        {
            var t = sampleCount == 1 ? 0.0 : (double)i / (sampleCount - 1);
            points.Add(LerpPoint(a, b, t));
        }

        return points;
    }

    private static double[] SamplePchip(double[] x, double[] y, int sampleCount)
    {
        if (x.Length != y.Length || x.Length == 0)
            return Array.Empty<double>();

        if (x.Length == 1)
            return Enumerable.Repeat(y[0], sampleCount).ToArray();

        var derivatives = ComputePchipDerivatives(x, y);
        var result = new double[sampleCount];
        var segment = 0;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = sampleCount == 1 ? x[0] : x[0] + (x[^1] - x[0]) * i / (sampleCount - 1);
            while (segment < x.Length - 2 && t > x[segment + 1])
                segment++;

            var h = Math.Max(1e-9, x[segment + 1] - x[segment]);
            var local = Math.Clamp((t - x[segment]) / h, 0.0, 1.0);
            var h00 = (2 * local * local * local) - (3 * local * local) + 1;
            var h10 = (local * local * local) - (2 * local * local) + local;
            var h01 = (-2 * local * local * local) + (3 * local * local);
            var h11 = (local * local * local) - (local * local);

            result[i] = (h00 * y[segment])
                + (h10 * h * derivatives[segment])
                + (h01 * y[segment + 1])
                + (h11 * h * derivatives[segment + 1]);
        }

        return result;
    }

    private static double[] ComputePchipDerivatives(double[] x, double[] y)
    {
        var count = x.Length;
        var h = new double[count - 1];
        var delta = new double[count - 1];
        for (var i = 0; i < count - 1; i++)
        {
            h[i] = Math.Max(1e-9, x[i + 1] - x[i]);
            delta[i] = (y[i + 1] - y[i]) / h[i];
        }

        var derivatives = new double[count];
        if (count == 2)
        {
            derivatives[0] = delta[0];
            derivatives[1] = delta[0];
            return derivatives;
        }

        derivatives[0] = ComputeEndpointDerivative(h[0], h[1], delta[0], delta[1]);
        derivatives[^1] = ComputeEndpointDerivative(h[^1], h[^2], delta[^1], delta[^2]);

        for (var i = 1; i < count - 1; i++)
        {
            if (Math.Abs(delta[i - 1]) <= 1e-12 || Math.Abs(delta[i]) <= 1e-12 || Math.Sign(delta[i - 1]) != Math.Sign(delta[i]))
            {
                derivatives[i] = 0;
                continue;
            }

            var w1 = (2 * h[i]) + h[i - 1];
            var w2 = h[i] + (2 * h[i - 1]);
            derivatives[i] = (w1 + w2) / ((w1 / delta[i - 1]) + (w2 / delta[i]));
        }

        return derivatives;
    }

    private static double ComputeEndpointDerivative(double h0, double h1, double delta0, double delta1)
    {
        var derivative = (((2 * h0) + h1) * delta0 - (h0 * delta1)) / (h0 + h1);
        if (Math.Sign(derivative) != Math.Sign(delta0))
            return 0;

        if (Math.Sign(delta0) != Math.Sign(delta1) && Math.Abs(derivative) > Math.Abs(3 * delta0))
            return 3 * delta0;

        return derivative;
    }

    private static List<RecipePoint> SelectWorkingPoints(IList<RecipePoint> source)
    {
        var activeRenderable = source.Where(p => p.Act && !p.Hidden && !p.Safe && HasRenderableTarget(p)).ToList();
        if (activeRenderable.Count > 0)
            return activeRenderable;

        var activeVisible = source.Where(p => p.Act && !p.Hidden && !p.Safe).ToList();
        if (activeVisible.Count > 0)
            return activeVisible;

        var active = source.Where(p => p.Act && !p.Safe).ToList();
        if (active.Count > 0)
            return active;

        return source.Where(p => !p.Safe).ToList();
    }

    private static bool HasRenderableTarget(RecipePoint point)
    {
        const double eps = 1e-6;
        return Math.Abs(point.RCrd) > eps || Math.Abs(point.ZCrd) > eps;
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point LerpPoint(Point a, Point b, double t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Lerp(double a, double b, double t)
        => a + (b - a) * t;
}
