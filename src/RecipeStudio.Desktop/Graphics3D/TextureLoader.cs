using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Silk.NET.OpenGL;

namespace RecipeStudio.Desktop.Graphics3D;

public static class TextureLoader
{
    public static unsafe uint LoadFromResource(GL gl, string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));
        using var bitmap = new Bitmap(stream);

        var size = bitmap.PixelSize;
        var width = size.Width;
        var height = size.Height;
        var stride = width * 4;

        var pixels = Marshal.AllocHGlobal(stride * height);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), pixels, stride * height, stride);

            var texture = gl.GenTexture();
            gl.BindTexture(TextureTarget.Texture2D, texture);
            gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, (uint)width, (uint)height, 0, Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, (void*)pixels);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
            gl.BindTexture(TextureTarget.Texture2D, 0);
            return texture;
        }
        finally
        {
            Marshal.FreeHGlobal(pixels);
        }
    }
}
