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

        var vertices = new List<float>((segments + 1) * 6 * 2);
        var indices = new List<uint>(segments * 6);

        for (var i = 0; i <= segments; i++)
        {
            var a = i / (float)segments * MathF.Tau;
            var c = MathF.Cos(a);
            var s = MathF.Sin(a);

            var normal = Vector3.Normalize(new Vector3(c, s, -1f));

            vertices.Add(0f);
            vertices.Add(0f);
            vertices.Add(0f);
            vertices.Add(normal.X);
            vertices.Add(normal.Y);
            vertices.Add(normal.Z);

            vertices.Add(c);
            vertices.Add(s);
            vertices.Add(1f);
            vertices.Add(normal.X);
            vertices.Add(normal.Y);
            vertices.Add(normal.Z);
        }

        for (uint i = 0; i < segments; i++)
        {
            var a = i * 2;
            var b = a + 1;
            var c = a + 2;
            var d = a + 3;
            indices.Add(a);
            indices.Add(b);
            indices.Add(d);
            indices.Add(a);
            indices.Add(d);
            indices.Add(c);
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }
}
