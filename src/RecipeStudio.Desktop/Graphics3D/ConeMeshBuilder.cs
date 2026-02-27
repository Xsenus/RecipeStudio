using System;
using System.Collections.Generic;
using System.Numerics;

namespace RecipeStudio.Desktop.Graphics3D;

public static class ConeMeshBuilder
{
    public static Mesh Build(int segments = 24)
    {
        if (segments < 3)
            return new Mesh(Array.Empty<float>(), Array.Empty<uint>());

        var vertices = new List<float>((segments + 2) * 6);
        var indices = new List<uint>(segments * 3);

        // Apex at z=0, base ring at z=1 (side-only cone)
        vertices.AddRange([0f, 0f, 0f, 0f, 0f, -1f]);

        var slope = Vector2.Normalize(new Vector2(1f, 1f));
        for (var i = 0; i <= segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            var c = MathF.Cos(a);
            var s = MathF.Sin(a);

            vertices.Add(c);
            vertices.Add(s);
            vertices.Add(1f);

            vertices.Add(c * slope.X);
            vertices.Add(s * slope.X);
            vertices.Add(slope.Y);
        }

        for (uint i = 1; i <= segments; i++)
        {
            indices.Add(0);
            indices.Add(i);
            indices.Add(i + 1);
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }
}
