using System;
using System.Collections.Generic;
using System.Numerics;
using RecipeStudio.Desktop.Models;

namespace RecipeStudio.Desktop.Services;

public static class RobotCoordinateResolver
{
    public static List<Vector3> BuildAbsolutePositions(IReadOnlyList<RecipePoint> points)
    {
        var direct = new List<Vector3>(points.Count);
        var incremental = new List<Vector3>(points.Count);

        var hasWorking = false;
        var current = Vector3.Zero;

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            var basePos = new Vector3((float)p.Xr0, (float)p.Yx0, (float)p.Zr0);
            var delta = new Vector3((float)p.DX, (float)p.DY, (float)p.DZ);
            var directPos = basePos + delta;
            direct.Add(directPos);

            if (!p.Safe)
            {
                if (!hasWorking)
                {
                    current = directPos;
                    hasWorking = true;
                }
                else
                {
                    current += delta;
                }

                incremental.Add(current);
            }
            else
            {
                incremental.Add(directPos);
            }
        }

        if (!hasWorking)
            return direct;

        var lenDirect = PathLength(direct);
        var lenIncremental = PathLength(incremental);

        return lenIncremental + 1e-3f < lenDirect * 0.8f
            ? incremental
            : direct;
    }

    private static float PathLength(IReadOnlyList<Vector3> path)
    {
        if (path.Count < 2)
            return 0f;

        var len = 0f;
        for (var i = 1; i < path.Count; i++)
            len += Vector3.Distance(path[i - 1], path[i]);

        return len;
    }
}
