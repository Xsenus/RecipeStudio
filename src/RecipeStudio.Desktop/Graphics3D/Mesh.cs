using Silk.NET.OpenGL;

namespace RecipeStudio.Desktop.Graphics3D;

public sealed class Mesh
{
    public float[] Vertices { get; }
    public uint[] Indices { get; }

    public Mesh(float[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public uint Vao { get; private set; }
    public uint Vbo { get; private set; }
    public uint Ebo { get; private set; }

    public unsafe void Upload(GL gl)
    {
        if (Vao != 0)
            return;

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, Vbo);
        fixed (float* v = Vertices)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Length * sizeof(float)), v, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, Ebo);
        fixed (uint* idx = Indices)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), idx, BufferUsageARB.StaticDraw);

        const uint stride = 6 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        gl.BindVertexArray(0);
    }

    public unsafe void Draw(GL gl)
    {
        if (Vao == 0)
            return;

        gl.BindVertexArray(Vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)Indices.Length, DrawElementsType.UnsignedInt, (void*)0);
        gl.BindVertexArray(0);
    }

    public void Dispose(GL gl)
    {
        if (Ebo != 0) gl.DeleteBuffer(Ebo);
        if (Vbo != 0) gl.DeleteBuffer(Vbo);
        if (Vao != 0) gl.DeleteVertexArray(Vao);
        Ebo = 0;
        Vbo = 0;
        Vao = 0;
    }
}
