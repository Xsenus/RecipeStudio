using System.Collections.Generic;
using System.Numerics;

namespace RecipeStudio.Desktop.Graphics3D;

public static class LineStripBuilder
{
    public static float[] Build(IReadOnlyList<Vector3> points)
    {
        var data = new float[points.Count * 3];
        for (var i = 0; i < points.Count; i++)
        {
            data[i * 3] = points[i].X;
            data[i * 3 + 1] = points[i].Y;
            data[i * 3 + 2] = points[i].Z;
        }

        return data;
    }
}
