namespace Miminus.Graphics;

/// <summary>Straight 8-bit-per-channel colour, packed the way the vertex buffer
/// wants it (R in the low byte) so uploading is a single store.</summary>
public readonly struct Color : IEquatable<Color>
{
    public readonly byte R, G, B, A;

    public Color(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

    public static Color Rgb(int hex) => new((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF));
    public static Color Rgba(int hex, byte a) => new((byte)((hex >> 16) & 0xFF), (byte)((hex >> 8) & 0xFF), (byte)(hex & 0xFF), a);
    public static Color FromFloat(float r, float g, float b, float a = 1f)
        => new((byte)(Math.Clamp(r, 0, 1) * 255), (byte)(Math.Clamp(g, 0, 1) * 255),
               (byte)(Math.Clamp(b, 0, 1) * 255), (byte)(Math.Clamp(a, 0, 1) * 255));

    public uint Packed => (uint)R | ((uint)G << 8) | ((uint)B << 16) | ((uint)A << 24);

    public Color WithAlpha(byte a) => new(R, G, B, a);
    public Color WithAlpha(float a) => new(R, G, B, (byte)(Math.Clamp(a, 0, 1) * 255));

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    /// <summary>Scales RGB toward black (f&lt;1) or white (f&gt;1). Used all over the
    /// XP chrome, where highlights and shadows are tints of one base colour.</summary>
    public Color Shade(float f)
    {
        if (f <= 1f)
            return new Color((byte)(R * f), (byte)(G * f), (byte)(B * f), A);
        float t = f - 1f;
        return new Color(
            (byte)(R + (255 - R) * t),
            (byte)(G + (255 - G) * t),
            (byte)(B + (255 - B) * t), A);
    }

    public float Luminance => (0.2126f * R + 0.7152f * G + 0.0722f * B) / 255f;

    public bool Equals(Color o) => R == o.R && G == o.G && B == o.B && A == o.A;
    public override bool Equals(object o) => o is Color c && Equals(c);
    public override int GetHashCode() => (int)Packed;
    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);

    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color Green = new(0, 128, 0);
    public static readonly Color Blue = new(0, 0, 255);
    public static readonly Color Yellow = new(255, 255, 0);
    public static readonly Color Gray = new(128, 128, 128);
    public static readonly Color LightGray = new(212, 208, 200);
    public static readonly Color DarkGray = new(64, 64, 64);
}

/// <summary>Axis-aligned rectangle in pixels, top-left origin.</summary>
public struct Rect
{
    public float X, Y, W, H;

    public Rect(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }

    public float Left => X;
    public float Top => Y;
    public float Right => X + W;
    public float Bottom => Y + H;
    public float CenterX => X + W * 0.5f;
    public float CenterY => Y + H * 0.5f;

    public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y && py < Y + H;
    public bool IsEmpty => W <= 0 || H <= 0;

    public Rect Inflate(float d) => new(X - d, Y - d, W + d * 2, H + d * 2);
    public Rect Deflate(float d) => new(X + d, Y + d, W - d * 2, H - d * 2);
    public Rect Deflate(float l, float t, float r, float b) => new(X + l, Y + t, W - l - r, H - t - b);
    public Rect Offset(float dx, float dy) => new(X + dx, Y + dy, W, H);

    public Rect Intersect(Rect o)
    {
        float l = Math.Max(Left, o.Left), t = Math.Max(Top, o.Top);
        float r = Math.Min(Right, o.Right), b = Math.Min(Bottom, o.Bottom);
        return new Rect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
    }

    public bool Intersects(Rect o) => Left < o.Right && Right > o.Left && Top < o.Bottom && Bottom > o.Top;

    /// <summary>Cuts <paramref name="amount"/> off the top and returns it, shrinking this rect.
    /// Layout in the shell is almost all successive slices like this.</summary>
    public Rect CutTop(float amount)
    {
        amount = Math.Min(amount, H);
        var r = new Rect(X, Y, W, amount);
        Y += amount; H -= amount;
        return r;
    }

    public Rect CutBottom(float amount)
    {
        amount = Math.Min(amount, H);
        H -= amount;
        return new Rect(X, Y + H, W, amount);
    }

    public Rect CutLeft(float amount)
    {
        amount = Math.Min(amount, W);
        var r = new Rect(X, Y, amount, H);
        X += amount; W -= amount;
        return r;
    }

    public Rect CutRight(float amount)
    {
        amount = Math.Min(amount, W);
        W -= amount;
        return new Rect(X + W, Y, amount, H);
    }

    public override string ToString() => $"({X},{Y} {W}x{H})";
}
