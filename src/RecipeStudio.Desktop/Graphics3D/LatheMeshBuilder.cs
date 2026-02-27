using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace RecipeStudio.Desktop.Graphics3D;

public static class LatheMeshBuilder
{
    public static Mesh Build(IReadOnlyList<Vector2> profile, int segments = 64)
    {
        if (profile.Count < 2 || segments < 3)
            return new Mesh(Array.Empty<float>(), Array.Empty<uint>());

        var vertices = new List<float>(profile.Count * (segments + 1) * 6);
        var indices = new List<uint>((profile.Count - 1) * segments * 6);

        for (var i = 0; i < profile.Count; i++)
        {
            var current = profile[i];
            var prev = profile[Math.Max(0, i - 1)];
            var next = profile[Math.Min(profile.Count - 1, i + 1)];
            var dr = MathF.Abs(next.X) - MathF.Abs(prev.X);
            var dz = next.Y - prev.Y;
            var meridianNormal = new Vector2(dz, -dr);
            if (meridianNormal.LengthSquared() < 1e-8f)
                meridianNormal = Vector2.UnitX;
            meridianNormal = Vector2.Normalize(meridianNormal);

            var r = MathF.Abs(current.X);
            var z = current.Y;
            for (var s = 0; s <= segments; s++)
            {
                var a = s / (float)segments * MathF.Tau;
                var c = MathF.Cos(a);
                var sn = MathF.Sin(a);
                vertices.Add(r * c);
                vertices.Add(r * sn);
                vertices.Add(z);
                vertices.Add(meridianNormal.X * c);
                vertices.Add(meridianNormal.X * sn);
                vertices.Add(meridianNormal.Y);
            }
        }

        uint ring = (uint)(segments + 1);
        for (uint i = 0; i < profile.Count - 1; i++)
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
