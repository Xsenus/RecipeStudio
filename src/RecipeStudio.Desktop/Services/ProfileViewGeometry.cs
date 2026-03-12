using System;
using System.Collections.Generic;
using Avalonia;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public readonly record struct ProfilePairGeometry(
    Point TargetPoint,
    Point NozzleTipPoint,
    Point ToolPoint,
    Point TargetToToolDirection);

public readonly record struct ProfileSegmentPoints(Point A1, Point B0);

public static class ProfileViewGeometry
{
    public static Point ResolveDisplayedTargetPoint(
        RecipePoint point,
        AppSettings? settings,
        bool invertHorizontal,
        double verticalOffsetMm = 0)
    {
        settings ??= new AppSettings();
        var (xp, zp) = point.GetTargetPoint(settings.HZone);
        var x = invertHorizontal ? -xp : xp;
        return new Point(x, zp + verticalOffsetMm);
    }

    public static Point ResolveTargetToToolDirection(double alfaDeg, int place, bool invertHorizontal)
    {
        // Profile/2D-pair geometry is drawn in the X-Z plane from alpha only.
        // Beta affects the 3D pose, but must not shorten the visible 2D segment.
        var displayAlfa = place == 0 ? alfaDeg : -alfaDeg;
        var radians = displayAlfa * Math.PI / 180.0;
        var x = Math.Cos(radians);
        var z = Math.Sin(radians);

        if (!invertHorizontal)
            x = -x;

        return new Point(x, z);
    }

    public static ProfilePairGeometry ResolvePairGeometry(
        Point targetPoint,
        double alfaDeg,
        int place,
        bool invertHorizontal,
        double aNozzleMm,
        double nozzleLengthMm)
    {
        var direction = ResolveTargetToToolDirection(alfaDeg, place, invertHorizontal);
        var segment = ResolveSegmentPoints(targetPoint, aNozzleMm, place == 0 ? alfaDeg : -alfaDeg, nozzleLengthMm);

        return new ProfilePairGeometry(targetPoint, segment.A1, segment.B0, direction);
    }

    public static ProfileSegmentPoints ResolveSegmentPoints(Point a0, double aNozzleMm, double alfaDisplayDeg, double nozzleLengthMm)
    {
        var a = Math.Max(0, Math.Abs(aNozzleMm));
        var lz = Math.Max(1e-6, Math.Abs(nozzleLengthMm));
        var radians = alfaDisplayDeg * Math.PI / 180.0;
        var dx = Math.Cos(radians);
        var dy = Math.Sin(radians);

        var a1 = new Point(a0.X + dx * a, a0.Y + dy * a);
        var b0 = new Point(a1.X + dx * lz, a1.Y + dy * lz);
        return new ProfileSegmentPoints(a1, b0);
    }

    public static double ResolvePointANozzle(RecipePoint point, IList<RecipePoint>? source, AppSettings? settings)
    {
        var mode = ANozzleKinematicsModes.Normalize(settings?.ANozzleKinematicsMode);
        if (mode == ANozzleKinematicsModes.CurrentPerPoint || source is null || source.Count == 0)
            return point.ANozzle;

        return source[0].ANozzle;
    }

    public static double ResolveInterpolatedANozzle(
        IList<RecipePoint> source,
        AppSettings? settings,
        int segmentIndex,
        double segmentT)
    {
        if (source.Count == 0)
            return 0;

        var mode = ANozzleKinematicsModes.Normalize(settings?.ANozzleKinematicsMode);
        if (mode != ANozzleKinematicsModes.CurrentPerPoint || source.Count == 1)
            return source[0].ANozzle;

        var seg = Math.Clamp(segmentIndex, 0, Math.Max(0, source.Count - 2));
        var t = Math.Clamp(segmentT, 0.0, 1.0);
        var start = source[seg].ANozzle;
        var end = source[Math.Min(source.Count - 1, seg + 1)].ANozzle;
        return start + (end - start) * t;
    }
}
