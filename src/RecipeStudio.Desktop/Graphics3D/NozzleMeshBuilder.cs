using System;
using System.Collections.Generic;

namespace RecipeStudio.Desktop.Graphics3D;

public static class NozzleMeshBuilder
{
    public static Mesh Build(float bodyLength = 80, float bodyRadius = 10, float tipLength = 30, int segments = 24)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        void AddRing(float z, float r, float nz)
        {
            for (var i = 0; i <= segments; i++)
            {
                var a = i / (float)segments * MathF.Tau;
                var c = MathF.Cos(a);
                var s = MathF.Sin(a);
                vertices.Add(r * c);
                vertices.Add(r * s);
                vertices.Add(z);
                vertices.Add(c);
                vertices.Add(s);
                vertices.Add(nz);
            }
        }

        AddRing(0, bodyRadius, 0);
        AddRing(bodyLength, bodyRadius, 0);
        AddRing(bodyLength + tipLength, 1, 1);

        uint ring = (uint)(segments + 1);
        for (uint i = 0; i < 2; i++)
        {
            for (uint s = 0; s < segments; s++)
            {
                var a = i * ring + s;
                var b = a + ring;
                indices.Add(a); indices.Add(b); indices.Add(a + 1);
                indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
            }
        }

        return new Mesh(vertices.ToArray(), indices.ToArray());
    }
}
