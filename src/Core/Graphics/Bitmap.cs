namespace Miminus.Graphics;

/// <summary>A CPU-side image with a GL texture kept in sync.
///
/// Paint edits the pixel array directly and flips a dirty flag; the texture is
/// re-uploaded once per frame rather than per stroke, which keeps freehand
/// drawing smooth even at full canvas size.</summary>
public sealed class Bitmap : IDisposable
{
    public readonly int Width, Height;
    public readonly uint[] Pixels;
    Texture _texture;
    bool _dirty = true;

    public Bitmap(int w, int h, uint fill = 0xFFFFFFFF)
    {
        Width = w; Height = h;
        Pixels = new uint[w * h];
        Array.Fill(Pixels, fill);
    }

    public Texture Texture
    {
        get
        {
            _texture ??= new Texture(Width, Height, Pixels, linear: false);
            if (_dirty) { _texture.Update(0, 0, Width, Height, Pixels); _dirty = false; }
            return _texture;
        }
    }

    public void Invalidate() => _dirty = true;

    public uint Get(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height ? Pixels[y * Width + x] : 0;

    public void Set(int x, int y, uint c)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        Pixels[y * Width + x] = c;
    }

    /// <summary>Filled circular brush dab.</summary>
    public void Dab(int cx, int cy, int radius, uint c)
    {
        if (radius <= 0) { Set(cx, cy, c); return; }
        int r2 = radius * radius;
        for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= r2) Set(cx + x, cy + y, c);
    }

    /// <summary>Bresenham line with a round brush, so strokes have no gaps.</summary>
    public void Line(int x0, int y0, int x1, int y1, int radius, uint c)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            Dab(x0, y0, radius, c);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    public void RectOutline(int x0, int y0, int x1, int y1, int radius, uint c)
    {
        Line(x0, y0, x1, y0, radius, c);
        Line(x1, y0, x1, y1, radius, c);
        Line(x1, y1, x0, y1, radius, c);
        Line(x0, y1, x0, y0, radius, c);
    }

    public void RectFilled(int x0, int y0, int x1, int y1, uint c)
    {
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++) Set(x, y, c);
    }

    public void EllipseOutline(int x0, int y0, int x1, int y1, int radius, uint c)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float rx = Math.Abs(x1 - x0) * 0.5f, ry = Math.Abs(y1 - y0) * 0.5f;
        int steps = (int)Math.Max(24, (rx + ry));
        int px = 0, py = 0;
        for (int i = 0; i <= steps; i++)
        {
            float a = i / (float)steps * MathF.PI * 2;
            int x = (int)MathF.Round(cx + MathF.Cos(a) * rx);
            int y = (int)MathF.Round(cy + MathF.Sin(a) * ry);
            if (i > 0) Line(px, py, x, y, radius, c);
            px = x; py = y;
        }
    }

    public void EllipseFilled(int x0, int y0, int x1, int y1, uint c)
    {
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        float rx = MathF.Max(0.5f, Math.Abs(x1 - x0) * 0.5f), ry = MathF.Max(0.5f, Math.Abs(y1 - y0) * 0.5f);
        for (int y = (int)(cy - ry); y <= cy + ry; y++)
            for (int x = (int)(cx - rx); x <= cx + rx; x++)
            {
                float nx = (x - cx) / rx, ny = (y - cy) / ry;
                if (nx * nx + ny * ny <= 1) Set(x, y, c);
            }
    }

    /// <summary>Scanline flood fill — the paint bucket.</summary>
    public void Fill(int sx, int sy, uint c)
    {
        if (sx < 0 || sx >= Width || sy < 0 || sy >= Height) return;
        uint target = Get(sx, sy);
        if (target == c) return;

        var stack = new Stack<(int x, int y)>();
        stack.Push((sx, sy));
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (y < 0 || y >= Height) continue;

            int left = x;
            while (left >= 0 && Get(left, y) == target) left--;
            left++;
            int right = x;
            while (right < Width && Get(right, y) == target) right++;
            right--;

            for (int i = left; i <= right; i++)
            {
                Set(i, y, c);
                if (y > 0 && Get(i, y - 1) == target) stack.Push((i, y - 1));
                if (y < Height - 1 && Get(i, y + 1) == target) stack.Push((i, y + 1));
            }
        }
    }

    public uint[] Clone() => (uint[])Pixels.Clone();

    public void Restore(uint[] snapshot)
    {
        Array.Copy(snapshot, Pixels, Pixels.Length);
        Invalidate();
    }

    public void Dispose() => _texture?.Dispose();
}

/// <summary>The two procedurally generated pictures the filesystem refers to.</summary>
public static class Pictures
{
    static readonly Dictionary<PictureId, Bitmap> _cache = new();

    public static Bitmap Get(PictureId id)
    {
        if (_cache.TryGetValue(id, out var b)) return b;
        b = id switch
        {
            PictureId.Wallpaper => BuildWallpaper(),
            _ => BuildPhoto(),
        };
        _cache[id] = b;
        return b;
    }

    static uint Rgb(float r, float g, float bl) => 0xFF000000u
        | (uint)(Math.Clamp(bl, 0, 1) * 255) << 16
        | (uint)(Math.Clamp(g, 0, 1) * 255) << 8
        | (uint)(Math.Clamp(r, 0, 1) * 255);

    /// <summary>«Эфрате.jpeg» — 454×364, the dimensions Explorer reports in part 2.
    /// Stands in as a plain still life rather than reproducing the original.</summary>
    static Bitmap BuildPhoto()
    {
        const int W = 454, H = 364;
        var b = new Bitmap(W, H);
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                // Warm interior: wall, floor line, soft window light from the left.
                float wall = 0.72f - v * 0.10f;
                float r = wall, g = wall * 0.93f, bl = wall * 0.80f;

                if (v > 0.74f)
                {
                    float t = (v - 0.74f) / 0.26f;
                    r = 0.44f - t * 0.14f; g = 0.34f - t * 0.11f; bl = 0.24f - t * 0.08f;
                }

                float light = MathF.Exp(-MathF.Pow((u - 0.18f) * 2.6f, 2)) * (1 - v * 0.5f) * 0.28f;
                r += light; g += light * 0.95f; bl += light * 0.75f;

                // A vase on a table, roughly centred.
                float dx = (u - 0.5f) * 3.2f, dy = (v - 0.55f) * 2.1f;
                if (dx * dx + dy * dy < 1f && v < 0.78f)
                {
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    float shade = 0.55f + (1 - d) * 0.35f - (u < 0.5f ? 0 : 0.12f);
                    r = 0.30f * shade + 0.10f; g = 0.42f * shade + 0.12f; bl = 0.52f * shade + 0.16f;
                }

                float grain = ((x * 37 + y * 91) % 17) / 17f * 0.03f;
                b.Set(x, y, Rgb(r + grain, g + grain, bl + grain));
            }
        }
        return b;
    }

    static Bitmap BuildWallpaper()
    {
        const int W = 400, H = 300;
        var b = new Bitmap(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float shade = 1f - y / (float)H * 0.05f;
                b.Set(x, y, Rgb(1.0f * shade, 0.824f * shade, 0f));
            }
        return b;
    }
}
