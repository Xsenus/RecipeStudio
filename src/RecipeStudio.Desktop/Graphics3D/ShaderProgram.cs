using System;
using System.Numerics;
using Silk.NET.OpenGL;

namespace RecipeStudio.Desktop.Graphics3D;

public sealed class ShaderProgram
{
    public uint Handle { get; private set; }

    public void Create(GL gl, string vert, string frag, params (string Name, uint Location)[] attributeBindings)
    {
        var v = Compile(gl, ShaderType.VertexShader, vert);
        var f = Compile(gl, ShaderType.FragmentShader, frag);
        Handle = gl.CreateProgram();
        gl.AttachShader(Handle, v);
        gl.AttachShader(Handle, f);

        foreach (var binding in attributeBindings)
            gl.BindAttribLocation(Handle, binding.Location, binding.Name);

        gl.LinkProgram(Handle);
        gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out var ok);
        if (ok == 0)
            throw new InvalidOperationException(gl.GetProgramInfoLog(Handle));
        gl.DeleteShader(v);
        gl.DeleteShader(f);
    }

    private static uint Compile(GL gl, ShaderType type, string src)
    {
        var h = gl.CreateShader(type);
        gl.ShaderSource(h, src);
        gl.CompileShader(h);
        gl.GetShader(h, ShaderParameterName.CompileStatus, out var ok);
        if (ok == 0)
            throw new InvalidOperationException(gl.GetShaderInfoLog(h));
        return h;
    }

    public void Use(GL gl) => gl.UseProgram(Handle);

    public unsafe void SetMatrix(GL gl, string name, Matrix4x4 m)
    {
        var loc = gl.GetUniformLocation(Handle, name);
        gl.UniformMatrix4(loc, 1, false, (float*)&m);
    }

    public void SetVec3(GL gl, string name, Vector3 v)
    {
        var loc = gl.GetUniformLocation(Handle, name);
        gl.Uniform3(loc, v.X, v.Y, v.Z);
    }

    public void SetFloat(GL gl, string name, float value)
    {
        var loc = gl.GetUniformLocation(Handle, name);
        gl.Uniform1(loc, value);
    }

    public void SetInt(GL gl, string name, int value)
    {
        var loc = gl.GetUniformLocation(Handle, name);
        gl.Uniform1(loc, value);
    }

    public void Dispose(GL gl)
    {
        if (Handle != 0)
            gl.DeleteProgram(Handle);
        Handle = 0;
    }
}
