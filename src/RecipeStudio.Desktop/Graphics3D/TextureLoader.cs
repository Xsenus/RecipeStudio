using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Silk.NET.OpenGL;

namespace RecipeStudio.Desktop.Graphics3D;

public static class TextureLoader
{
    public static uint LoadFromResource(GL gl, string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));
        using var bitmap = new Bitmap(stream);

        var size = bitmap.PixelSize;
        var pixels = new byte[size.Width * size.Height * 4];
        using (var fb = bitmap.Lock())
        {
            var rowBytes = Math.Min(fb.RowBytes, size.Width * 4);
            for (var y = 0; y < size.Height; y++)
            {
                var src = fb.Address + y * fb.RowBytes;
                Marshal.Copy(src, pixels, y * size.Width * 4, rowBytes);
            }
        }

        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        unsafe
        {
            fixed (byte* ptr = pixels)
                gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba, (uint)size.Width, (uint)size.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }
}
