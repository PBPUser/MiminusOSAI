using Miminus.Platform;

namespace Miminus.Graphics;

/// <summary>An RGBA8 texture. Pixels always arrive as a managed uint[] in
/// 0xAABBGGRR order, which is what both the GL upload and our software
/// rasterisers use, so nothing has to swizzle.</summary>
public sealed unsafe class Texture : IDisposable
{
    public uint Id { get; private set; }
    public int Width { get; }
    public int Height { get; }

    public Texture(int width, int height, uint[] pixels, bool linear = true, bool mipmap = false)
    {
        Width = width;
        Height = height;
        Id = GL.GenTexture();
        GL.BindTexture(GL.TEXTURE_2D, Id);
        GL.PixelStore(GL.UNPACK_ALIGNMENT, 4);

        fixed (uint* p = pixels)
            GL.TexImage2D(GL.TEXTURE_2D, 0, (int)GL.RGBA8, width, height, 0, GL.RGBA, GL.UNSIGNED_BYTE, p);

        if (mipmap) GL.GenerateMipmap(GL.TEXTURE_2D);

        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER,
            (int)(mipmap ? GL.LINEAR_MIPMAP_LINEAR : linear ? GL.LINEAR : GL.NEAREST));
        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, (int)(linear ? GL.LINEAR : GL.NEAREST));
        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, (int)GL.CLAMP_TO_EDGE);
        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, (int)GL.CLAMP_TO_EDGE);
        GL.BindTexture(GL.TEXTURE_2D, 0);
    }

    /// <summary>Replaces a sub-rectangle. The font atlas grows this way as new
    /// glyphs are needed rather than being rebuilt.</summary>
    public void Update(int x, int y, int w, int h, uint[] pixels)
    {
        GL.BindTexture(GL.TEXTURE_2D, Id);
        GL.PixelStore(GL.UNPACK_ALIGNMENT, 4);
        fixed (uint* p = pixels)
            GL.TexSubImage2D(GL.TEXTURE_2D, 0, x, y, w, h, GL.RGBA, GL.UNSIGNED_BYTE, p);
        GL.BindTexture(GL.TEXTURE_2D, 0);
    }

    public void SetWrap(bool repeat)
    {
        GL.BindTexture(GL.TEXTURE_2D, Id);
        int mode = (int)(repeat ? GL.REPEAT : GL.CLAMP_TO_EDGE);
        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, mode);
        GL.TexParameter(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, mode);
        GL.BindTexture(GL.TEXTURE_2D, 0);
    }

    public static Texture White1x1() => new(1, 1, new uint[] { 0xFFFFFFFF }, linear: false);

    public void Dispose()
    {
        if (Id != 0) { GL.DeleteTexture(Id); Id = 0; }
    }
}
