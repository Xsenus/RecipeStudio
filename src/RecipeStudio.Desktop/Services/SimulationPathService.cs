using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public sealed class SimulationPathService
{
    private const double PlaybackScale = 4.0;
    private const int ArcSamples = 32;

    public SimulationPath Build(IReadOnlyList<RecipePoint> points, bool smoothMotion)
    {
        var waypoints = points.Select((p, i) => new PathWaypoint(i, p, new Vector3((float)(p.Xr0 + p.DX), (float)(p.Yx0 + p.DY), (float)(p.Zr0 + p.DZ)))).ToList();
        if (waypoints.Count <= 1)
            return new SimulationPath(waypoints, Array.Empty<PathSegment>(), 0);

        var segments = new List<PathSegment>(waypoints.Count - 1);
        var elapsed = 0.0;

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var lengthMap = smoothMotion ? BuildArcMap(waypoints, i) : Array.Empty<ArcSample>();
            var length = smoothMotion ? lengthMap[^1].Length : Vector3.Distance(waypoints[i].BasePosition, waypoints[i + 1].BasePosition);
            var speedMmSec = Math.Max(1, waypoints[i].Point.NozzleSpeedMmMin / 60.0);
            var duration = length / speedMmSec / PlaybackScale;

            segments.Add(new PathSegment(i, elapsed, elapsed + duration, length, lengthMap));
            elapsed += duration;
        }

        return new SimulationPath(waypoints, segments, elapsed);
    }

    public PathSample Evaluate(SimulationPath path, double elapsedSec, bool smoothMotion)
    {
        if (path.Waypoints.Count == 0)
            return PathSample.Empty;

        if (path.Waypoints.Count == 1 || path.Segments.Count == 0 || path.TotalDurationSec <= 1e-9)
        {
            var wp = path.Waypoints[0];
            return new PathSample(wp.BasePosition, Vector3.UnitX, wp.Point.Alfa, wp.Point.Betta, 0, 0);
        }

        var clamped = Math.Clamp(elapsedSec, 0, path.TotalDurationSec);
        var segment = path.Segments[^1];
        foreach (var s in path.Segments)
        {
            if (clamped <= s.EndSec + 1e-9)
            {
                segment = s;
                break;
            }
        }

        var u = segment.DurationSec <= 1e-9 ? 1f : (float)Math.Clamp((clamped - segment.StartSec) / segment.DurationSec, 0, 1);
        var t = smoothMotion ? ToArcLengthParameter(segment, u) : u;

        var a = path.Waypoints[segment.Index];
        var b = path.Waypoints[segment.Index + 1];
        var position = smoothMotion ? Catmull(path.Waypoints, segment.Index, t) : Vector3.Lerp(a.BasePosition, b.BasePosition, t);

        var tAhead = MathF.Min(1, t + 0.02f);
        var ahead = smoothMotion ? Catmull(path.Waypoints, segment.Index, tAhead) : Vector3.Lerp(a.BasePosition, b.BasePosition, tAhead);
        var direction = ahead - position;
        if (direction.LengthSquared() < 1e-6f)
            direction = b.BasePosition - a.BasePosition;
        if (direction.LengthSquared() < 1e-6f)
            direction = Vector3.UnitX;

        direction = Vector3.Normalize(direction);
        var alfa = Lerp(a.Point.Alfa, b.Point.Alfa, u);
        var betta = Lerp(a.Point.Betta, b.Point.Betta, u);

        return new PathSample(position, direction, alfa, betta, segment.Index, clamped / path.TotalDurationSec);
    }

    private static ArcSample[] BuildArcMap(IReadOnlyList<PathWaypoint> waypoints, int segment)
    {
        var map = new ArcSample[ArcSamples + 1];
        var prev = Catmull(waypoints, segment, 0);
        map[0] = new ArcSample(0, 0);
        var length = 0f;

        for (var i = 1; i <= ArcSamples; i++)
        {
            var t = i / (float)ArcSamples;
            var p = Catmull(waypoints, segment, t);
            length += Vector3.Distance(prev, p);
            map[i] = new ArcSample(t, length);
            prev = p;
        }

        return map;
    }

    private static float ToArcLengthParameter(PathSegment segment, float normalizedLength)
    {
        if (segment.ArcMap.Length == 0)
            return normalizedLength;

        var target = normalizedLength * segment.Length;
        var map = segment.ArcMap;
        var lo = 0;
        var hi = map.Length - 1;

        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (map[mid].Length < target)
                lo = mid + 1;
            else
                hi = mid;
        }

        var idx = Math.Clamp(lo, 1, map.Length - 1);
        var a = map[idx - 1];
        var b = map[idx];
        if (Math.Abs(b.Length - a.Length) < 1e-6f)
            return b.T;

        var local = (target - a.Length) / (b.Length - a.Length);
        return a.T + (b.T - a.T) * local;
    }

    private static float Lerp(double a, double b, float t) => (float)(a + (b - a) * t);

    private static Vector3 Catmull(IReadOnlyList<PathWaypoint> waypoints, int segment, float t)
    {
        var p0 = waypoints[Math.Max(0, segment - 1)].BasePosition;
        var p1 = waypoints[segment].BasePosition;
        var p2 = waypoints[Math.Min(waypoints.Count - 1, segment + 1)].BasePosition;
        var p3 = waypoints[Math.Min(waypoints.Count - 1, segment + 2)].BasePosition;
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
}

public sealed record PathWaypoint(int Index, RecipePoint Point, Vector3 BasePosition);
public readonly record struct ArcSample(float T, float Length);

public sealed record PathSegment(int Index, double StartSec, double EndSec, double Length, ArcSample[] ArcMap)
{
    public double DurationSec => EndSec - StartSec;
}

public sealed record SimulationPath(IReadOnlyList<PathWaypoint> Waypoints, IReadOnlyList<PathSegment> Segments, double TotalDurationSec);

public readonly record struct PathSample(Vector3 Position, Vector3 Direction, double Alfa, double Betta, int SegmentIndex, double Progress)
{
    public static PathSample Empty => new(Vector3.Zero, Vector3.UnitX, 0, 0, 0, 0);
}
