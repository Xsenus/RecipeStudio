using System;
using System.Numerics;

namespace RecipeStudio.Desktop.Graphics3D;

public sealed class CameraOrbit
{
    public float Yaw { get; private set; } = 0.6f;
    public float Pitch { get; private set; } = 0.4f;
    public float Distance { get; private set; } = 700f;
    public Vector3 Target { get; private set; } = new(0, 0, 200);

    public Matrix4x4 GetView()
    {
        var dir = new Vector3(
            (float)(Math.Cos(Pitch) * Math.Cos(Yaw)),
            (float)(Math.Cos(Pitch) * Math.Sin(Yaw)),
            (float)Math.Sin(Pitch));
        var eye = Target - dir * Distance;
        return Matrix4x4.CreateLookAt(eye, Target, Vector3.UnitZ);
    }

    public Matrix4x4 GetProjection(float width, float height)
        => Matrix4x4.CreatePerspectiveFieldOfView((float)(Math.PI / 4), MathF.Max(0.1f, width / MathF.Max(1, height)), 0.1f, 5000f);

    public void Rotate(float dx, float dy)
    {
        Yaw += dx * 0.01f;
        Pitch = Math.Clamp(Pitch - dy * 0.01f, -1.45f, 1.45f);
    }

    public void Pan(float dx, float dy)
    {
        var view = GetView();
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);
        Target += (-right * dx + up * dy) * (Distance * 0.0015f);
    }

    public void Zoom(float delta)
        => Distance = Math.Clamp(Distance * (1f - delta * 0.001f), 80f, 3500f);

    public void Fit(Vector3 center, float radius)
    {
        Target = center;
        Distance = Math.Clamp(radius * 3f, 120f, 3200f);
    }
}
