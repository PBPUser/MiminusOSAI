using Miminus.Graphics;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

public enum WallpaperId
{
    MiminusWave,     // part 1: the blue water desktop
    MiminusYellow,   // part 2: yellow, "МИМИНУС ОС / Copyright Попов"
    Miminus7Blue,    // part 3: light blue with the flag logo
    Miminus7Dark,    // part 3: black with the big "7"
    Miminus7Green,   // part 3: olive/green ribbon
    Bliss,           // the green hill
    Azure,           // aurora over a deep blue field
    Sunset,          // sun sinking into a haze
    Matrix,          // falling green glyph rain
    Space,           // starfield with a nebula
    Plaid,           // woven checked cloth
    Blueprint,       // technical grid
    Miminus8,        // version 8: flat accent with the tile weave
    Miminus8Dark,    // version 8: charcoal, the eight in outline
    Plain,           // flat colour
}

/// <summary>One desktop background: a procedurally generated texture plus an
/// optional overlay drawn live (logos and captions stay crisp and follow the
/// language that way, instead of being baked into pixels).</summary>
public sealed class Wallpaper : IDisposable
{
    public WallpaperId Id;
    public Texture Texture;
    public Color Fallback;
    public Action<UiContext, Rect> Overlay;

    public string Name => Id switch
    {
        WallpaperId.MiminusWave => L.T("wall.miminus_wave"),
        WallpaperId.MiminusYellow => L.T("wall.miminus_os_yellow"),
        WallpaperId.Miminus7Blue => L.T("wall.miminus_7_sky"),
        WallpaperId.Miminus7Dark => L.T("wall.miminus_7_dark"),
        WallpaperId.Miminus7Green => L.T("wall.miminus_7_field"),
        WallpaperId.Bliss => L.T("wall.bliss"),
        WallpaperId.Azure => L.T("wall.azure"),
        WallpaperId.Sunset => L.T("wall.sunset"),
        WallpaperId.Matrix => L.T("wall.matrix"),
        WallpaperId.Space => L.T("wall.space"),
        WallpaperId.Plaid => L.T("wall.plaid"),
        WallpaperId.Blueprint => L.T("wall.blueprint"),
        WallpaperId.Miminus8 => L.T("wall.miminus_8_tiles"),
        WallpaperId.Miminus8Dark => L.T("wall.miminus_8_dark"),
        _ => L.T("wall.solid_colour"),
    };

    public void Dispose() => Texture?.Dispose();
}

/// <summary>Builds and caches every wallpaper.
///
/// Backgrounds are rasterised on the CPU into an RGBA buffer and uploaded once.
/// Nothing is loaded from disk — the gradients, waves, vignettes and light rays
/// are all evaluated per pixel here.</summary>
public sealed class WallpaperLibrary : IDisposable
{
    const int W = 1024, H = 640;

    readonly Dictionary<WallpaperId, Wallpaper> _cache = new();

    public IEnumerable<WallpaperId> All => new[]
    {
        WallpaperId.Miminus8, WallpaperId.Miminus8Dark,
        WallpaperId.MiminusYellow, WallpaperId.MiminusWave, WallpaperId.Miminus7Blue,
        WallpaperId.Miminus7Dark, WallpaperId.Miminus7Green, WallpaperId.Bliss,
        WallpaperId.Azure, WallpaperId.Sunset, WallpaperId.Matrix,
        WallpaperId.Space, WallpaperId.Plaid, WallpaperId.Blueprint,
    };

    public Wallpaper Get(WallpaperId id)
    {
        if (_cache.TryGetValue(id, out var w)) return w;
        w = Build(id);
        _cache[id] = w;
        return w;
    }

    // ---- pixel helpers ---------------------------------------------------

    static uint Pack(float r, float g, float b)
        => 0xFF000000u
         | (uint)(Math.Clamp(b, 0, 1) * 255) << 16
         | (uint)(Math.Clamp(g, 0, 1) * 255) << 8
         | (uint)(Math.Clamp(r, 0, 1) * 255);

    static float Smooth(float t) => t * t * (3 - 2 * t);

    static float Hash(int x, int y, int seed)
    {
        int n = x * 374761393 + y * 668265263 + seed * 1442695040;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0x7FFFFFF) / (float)0x7FFFFFF;
    }

    /// <summary>Value noise with smoothstep interpolation.</summary>
    static float Noise(float x, float y, int seed)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = Smooth(xf), v = Smooth(yf);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float cc = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return (a + (b - a) * u) + ((cc + (d - cc) * u) - (a + (b - a) * u)) * v;
    }

    static float Fbm(float x, float y, int seed, int octaves = 4)
    {
        float sum = 0, amp = 0.5f, freq = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(x * freq, y * freq, seed + i * 71) * amp;
            amp *= 0.5f;
            freq *= 2.03f;
        }
        return sum;
    }

    // ---- builders --------------------------------------------------------

    Wallpaper Build(WallpaperId id) => id switch
    {
        WallpaperId.MiminusYellow => BuildYellow(),
        WallpaperId.MiminusWave => BuildWave(),
        WallpaperId.Miminus7Blue => BuildSevenBlue(),
        WallpaperId.Miminus7Dark => BuildSevenDark(),
        WallpaperId.Miminus7Green => BuildSevenGreen(),
        WallpaperId.Bliss => BuildBliss(),
        WallpaperId.Azure => BuildAzure(),
        WallpaperId.Sunset => BuildSunset(),
        WallpaperId.Matrix => BuildMatrix(),
        WallpaperId.Space => BuildSpace(),
        WallpaperId.Plaid => BuildPlaid(),
        WallpaperId.Blueprint => BuildBlueprint(),
        WallpaperId.Miminus8 => BuildMetro(false),
        WallpaperId.Miminus8Dark => BuildMetro(true),
        _ => BuildPlain(),
    };

    /// <summary>Part 2: flat school-bus yellow with the branding painted on top.</summary>
    Wallpaper BuildYellow()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                // Barely-there vertical shading keeps it from looking like a flat fill.
                float shade = 1f - v * 0.045f;
                px[y * W + x] = Pack(1.0f * shade, 0.824f * shade, 0.0f);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.MiminusYellow,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0xFFD200),
            Overlay = DrawMiminusOsCaption,
        };
    }

    /// <summary>The wallpaper caption from part 2, drawn live so it can be
    /// translated and stays sharp at any resolution.</summary>
    static void DrawMiminusOsCaption(UiContext c, Rect screen)
    {
        string big = L.T("wall.miminus_os");
        string sub = L.T("wall.copyright_popov");

        var f = c.F.Huge;
        float w = f.Measure(big);
        // The caption sits right-of-centre, as in the video.
        float x = screen.X + screen.W * 0.62f - w * 0.5f;
        float y = screen.Y + screen.H * 0.30f;
        if (x + w > screen.Right - 20) x = screen.Right - 20 - w;
        if (x < screen.X + 20) x = screen.X + 20;

        f.Draw(c.R, big, x, y, Color.Black);

        float sw = c.F.Ui.Measure(sub);
        c.F.Ui.Draw(c.R, sub, x + (w - sw) * 0.5f, y + f.Height + 22, Color.Black);
    }

    /// <summary>Part 1: deep blue water with caustics and a wave crest.</summary>
    Wallpaper BuildWave()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                // Base depth gradient.
                float r = 0.36f - v * 0.30f;
                float g = 0.60f - v * 0.42f;
                float b = 0.86f - v * 0.40f;

                // Broad swell.
                float swell = MathF.Sin(u * 7.5f + v * 2.2f) * 0.5f + 0.5f;
                float light = swell * (1 - v) * 0.16f;

                // Caustic filaments.
                float n = Fbm(u * 6f, v * 4f + u * 1.4f, 11, 5);
                float caustic = MathF.Pow(MathF.Max(0, n - 0.42f) * 2.6f, 2.1f) * (1.05f - v) * 0.75f;

                // The dark trough sweeping through the lower right.
                float trough = MathF.Exp(-MathF.Pow((v - 0.62f - u * 0.22f) * 4.4f, 2));
                float dark = trough * 0.30f;

                r += light + caustic * 0.85f - dark;
                g += light + caustic * 0.95f - dark;
                b += light * 0.7f + caustic - dark * 0.8f;

                // Vignette.
                float dx = u - 0.5f, dy = v - 0.5f;
                float vig = 1f - (dx * dx + dy * dy) * 0.55f;
                px[y * W + x] = Pack(r * vig, g * vig, b * vig);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.MiminusWave,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x2E6FA8),
        };
    }

    /// <summary>Part 3: pale blue sky with light rays and the four-pane logo.</summary>
    Wallpaper BuildSevenBlue()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float r = 0.05f + v * 0.10f;
                float g = 0.28f + v * 0.22f;
                float b = 0.52f + v * 0.28f;

                // Glow centred behind where the logo sits.
                float dx = (u - 0.42f) * 1.35f, dy = (v - 0.40f);
                float glow = MathF.Exp(-(dx * dx + dy * dy) * 5.2f);
                r += glow * 0.45f; g += glow * 0.55f; b += glow * 0.45f;

                // Radiating light streaks.
                float ang = MathF.Atan2(v - 0.40f, u - 0.42f);
                float rays = MathF.Pow(MathF.Max(0, MathF.Sin(ang * 9f + 1.2f)), 6f) * glow * 0.5f;
                r += rays; g += rays; b += rays * 0.9f;

                // Wispy cloud texture.
                float n = Fbm(u * 3.2f, v * 2.4f, 29, 4);
                float wisp = MathF.Max(0, n - 0.55f) * 0.6f * (1 - v * 0.5f);
                r += wisp; g += wisp; b += wisp;

                px[y * W + x] = Pack(r, g, b);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Miminus7Blue,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x1C4B7C),
            Overlay = (c, s) => DrawSevenBranding(c, s, false),
        };
    }

    /// <summary>Part 3: near-black with the outlined "7".</summary>
    Wallpaper BuildSevenDark()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                float dx = u - 0.5f, dy = v - 0.45f;
                float glow = MathF.Exp(-(dx * dx * 1.6f + dy * dy) * 3.4f);
                float base_ = 0.03f + glow * 0.22f;
                float n = Fbm(u * 5f, v * 5f, 47, 3) * 0.035f;
                px[y * W + x] = Pack(base_ + n, base_ + n * 1.05f, base_ * 1.15f + n * 1.1f);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Miminus7Dark,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x0A0A0C),
            Overlay = (c, s) => DrawSevenBranding(c, s, true),
        };
    }

    /// <summary>Part 3: the olive/lime ribbon variant.</summary>
    Wallpaper BuildSevenGreen()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float r = 0.33f + v * 0.20f;
                float g = 0.42f + v * 0.26f;
                float b = 0.05f + v * 0.08f;

                // Diagonal light ribbon.
                float band = MathF.Exp(-MathF.Pow((v - 0.30f - u * 0.35f) * 5.0f, 2));
                r += band * 0.42f; g += band * 0.44f; b += band * 0.14f;

                float band2 = MathF.Exp(-MathF.Pow((v - 0.72f + u * 0.20f) * 6.5f, 2));
                r += band2 * 0.22f; g += band2 * 0.26f; b += band2 * 0.06f;

                float n = Fbm(u * 4f, v * 3f, 63, 4) * 0.10f;
                r += n; g += n; b += n * 0.5f;

                float dx = u - 0.5f, dy = v - 0.5f;
                float vig = 1f - (dx * dx + dy * dy) * 0.6f;
                px[y * W + x] = Pack(r * vig, g * vig, b * vig);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Miminus7Green,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x6D8438),
            Overlay = (c, s) => DrawSevenBranding(c, s, false),
        };
    }

    /// <summary>The green hill under a blue sky.</summary>
    Wallpaper BuildBliss()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                // Horizon runs across roughly two thirds down, with a gentle crown.
                float horizon = 0.58f - MathF.Cos(u * 2.6f - 0.5f) * 0.10f;

                float r, g, b;
                if (v < horizon)
                {
                    float t = v / MathF.Max(0.001f, horizon);
                    r = 0.24f + t * 0.42f;
                    g = 0.46f + t * 0.36f;
                    b = 0.78f + t * 0.16f;

                    float cloud = Fbm(u * 3.4f + 5f, v * 4.5f, 91, 5);
                    float amt = MathF.Max(0, cloud - 0.52f) * 2.0f * MathF.Min(1, t * 1.6f);
                    r += amt * 0.5f; g += amt * 0.45f; b += amt * 0.35f;
                }
                else
                {
                    float t = (v - horizon) / MathF.Max(0.001f, 1 - horizon);
                    r = 0.36f - t * 0.20f;
                    g = 0.62f - t * 0.26f;
                    b = 0.18f - t * 0.10f;

                    float grass = Fbm(u * 22f, v * 34f, 13, 3);
                    float shade = (grass - 0.5f) * 0.16f;
                    r += shade; g += shade * 1.2f; b += shade * 0.5f;

                    // Sun grazing the crown of the hill.
                    float lit = MathF.Exp(-MathF.Pow((v - horizon) * 9f, 2)) * 0.22f;
                    r += lit; g += lit; b += lit * 0.4f;
                }

                float dx = u - 0.5f, dy = v - 0.5f;
                float vig = 1f - (dx * dx + dy * dy) * 0.45f;
                px[y * W + x] = Pack(r * vig, g * vig, b * vig);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Bliss,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x3C7A2E),
        };
    }

    /// <summary>Ribbons of aurora light drifting over a deep blue field.</summary>
    Wallpaper BuildAzure()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float r = 0.02f + v * 0.04f;
                float g = 0.10f + v * 0.16f;
                float b = 0.26f + v * 0.30f;

                // Three sweeping ribbons at different frequencies.
                for (int i = 0; i < 3; i++)
                {
                    float phase = i * 1.7f;
                    float centre = 0.36f + i * 0.16f
                                 + MathF.Sin(u * (2.1f + i * 0.7f) + phase) * 0.11f;
                    float band = MathF.Exp(-MathF.Pow((v - centre) * (13f + i * 4f), 2));
                    float tint = 0.5f + 0.5f * MathF.Sin(u * 3f + phase);
                    r += band * 0.10f * tint;
                    g += band * (0.42f - i * 0.05f);
                    b += band * (0.34f + tint * 0.22f);
                }

                float shimmer = Fbm(u * 5f, v * 3f, 131, 4) * 0.06f;
                r += shimmer; g += shimmer; b += shimmer;

                float dx = u - 0.5f, dy = v - 0.5f;
                float vig = 1f - (dx * dx + dy * dy) * 0.7f;
                px[y * W + x] = Pack(r * vig, g * vig, b * vig);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Azure,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x0B2A55),
        };
    }

    /// <summary>A low sun over banded haze.</summary>
    Wallpaper BuildSunset()
    {
        var px = new uint[W * H];
        float sunX = 0.62f, sunY = 0.62f;
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                // Sky ramps from violet at the top to amber at the horizon.
                float t = MathF.Min(1f, v / 0.72f);
                float r = 0.16f + t * 0.82f;
                float g = 0.09f + t * 0.46f;
                float b = 0.30f - t * 0.16f;

                float dx = (u - sunX) * 1.6f, dy = v - sunY;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                float glow = MathF.Exp(-d * 7f);
                r += glow * 0.9f; g += glow * 0.62f; b += glow * 0.18f;
                if (d < 0.055f) { r += 0.6f; g += 0.45f; b += 0.15f; }

                // Horizontal haze bands across the sun.
                float bands = MathF.Sin(v * 46f) * 0.5f + 0.5f;
                float bandMask = MathF.Exp(-MathF.Pow((v - 0.62f) * 5.5f, 2));
                r -= bands * bandMask * 0.10f;
                g -= bands * bandMask * 0.07f;

                if (v > 0.76f)
                {
                    // Dark foreground with the sun reflected in it.
                    float k = (v - 0.76f) / 0.24f;
                    float refl = MathF.Exp(-MathF.Abs(u - sunX) * 9f) * (1 - k) * 0.5f;
                    r = 0.16f - k * 0.10f + refl;
                    g = 0.09f - k * 0.06f + refl * 0.6f;
                    b = 0.14f - k * 0.09f + refl * 0.2f;
                }

                px[y * W + x] = Pack(r, g, b);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Sunset,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x88401E),
        };
    }

    /// <summary>Columns of falling glyphs, drawn as bright cells with tails.</summary>
    Wallpaper BuildMatrix()
    {
        var px = new uint[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = Pack(0.01f, 0.03f, 0.02f);

        const int CellW = 12, CellH = 15;
        int cols = W / CellW, rows = H / CellH;

        for (int c = 0; c < cols; c++)
        {
            // Each column runs its own drop at its own speed.
            int head = (int)(Hash(c, 0, 7) * rows * 2) - rows / 2;
            int tail = 6 + (int)(Hash(c, 1, 11) * 14);

            for (int k = 0; k < tail; k++)
            {
                int row = head - k;
                if (row < 0 || row >= rows) continue;

                float bright = k == 0 ? 1.0f : MathF.Pow(1f - k / (float)tail, 1.6f) * 0.75f;

                // A 5x7 blob of "glyph" pixels inside the cell.
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (Hash(c * 97 + gx, row * 31 + gy, 3) < 0.42f) continue;
                        int sx = c * CellW + 3 + gx * 2;
                        int sy = row * CellH + 3 + gy * 2;
                        if (sx + 1 >= W || sy + 1 >= H) continue;

                        float g = 0.15f + bright * 0.85f;
                        float r = k == 0 ? 0.75f : bright * 0.12f;
                        for (int oy = 0; oy < 2; oy++)
                            for (int ox = 0; ox < 2; ox++)
                                px[(sy + oy) * W + sx + ox] = Pack(r, g, r * 0.6f);
                    }
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Matrix,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x021005),
        };
    }

    /// <summary>Starfield with a soft nebula.</summary>
    Wallpaper BuildSpace()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float n = Fbm(u * 3.2f, v * 2.4f, 211, 5);
                float cloud = MathF.Max(0, n - 0.46f) * 1.5f;
                float r = 0.03f + cloud * 0.42f;
                float g = 0.02f + cloud * 0.14f;
                float b = 0.06f + cloud * 0.52f;

                float dx = u - 0.34f, dy = v - 0.42f;
                float core = MathF.Exp(-(dx * dx + dy * dy) * 12f);
                r += core * 0.30f; g += core * 0.16f; b += core * 0.38f;

                px[y * W + x] = Pack(r, g, b);
            }
        }

        // Stars: mostly faint, a few bright with cross flares.
        for (int i = 0; i < 1400; i++)
        {
            int x = (int)(Hash(i, 1, 5) * W);
            int y = (int)(Hash(i, 2, 9) * H);
            if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) continue;

            float mag = Hash(i, 3, 13);
            float bright = 0.35f + mag * 0.65f;
            px[y * W + x] = Pack(bright, bright, bright * 0.95f + 0.05f);

            if (mag > 0.94f)
            {
                for (int d = 1; d <= 2; d++)
                {
                    float f = bright * (0.5f / d);
                    px[y * W + x + d] = Pack(f, f, f);
                    px[y * W + x - d] = Pack(f, f, f);
                    px[(y + d) * W + x] = Pack(f, f, f);
                    px[(y - d) * W + x] = Pack(f, f, f);
                }
            }
        }

        return new Wallpaper
        {
            Id = WallpaperId.Space,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x070512),
        };
    }

    /// <summary>Woven check, with the threads of the weave visible.</summary>
    Wallpaper BuildPlaid()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Two stripe systems crossing, multiplied like real cloth.
                float sx = Stripe(x);
                float sy = Stripe(y);
                float shade = sx * sy;

                float r = 0.16f + shade * 0.52f;
                float g = 0.10f + shade * 0.26f;
                float b = 0.12f + shade * 0.22f;

                // Thread texture: alternate over/under every other pixel.
                bool over = ((x / 2) + (y / 2)) % 2 == 0;
                float weave = over ? 1.06f : 0.93f;

                px[y * W + x] = Pack(r * weave, g * weave, b * weave);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Plaid,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x5A2A20),
        };
    }

    /// <summary>Stripe intensity for the plaid, on a 64-pixel repeat.</summary>
    static float Stripe(int p)
    {
        int m = p % 64;
        if (m < 4) return 1.15f;
        if (m < 22) return 0.55f;
        if (m < 26) return 0.95f;
        if (m < 44) return 0.35f;
        if (m < 48) return 1.05f;
        return 0.62f;
    }

    /// <summary>Engineering blueprint: fine grid, heavy grid, and a drawn frame.</summary>
    Wallpaper BuildBlueprint()
    {
        var px = new uint[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float r = 0.05f + v * 0.02f;
                float g = 0.16f + v * 0.06f;
                float b = 0.34f + v * 0.12f;

                bool fine = x % 16 == 0 || y % 16 == 0;
                bool heavy = x % 80 == 0 || y % 80 == 0;
                if (fine) { r += 0.05f; g += 0.09f; b += 0.12f; }
                if (heavy) { r += 0.11f; g += 0.18f; b += 0.22f; }

                // Border rule, inset like a drawing sheet.
                int bx = Math.Min(x, W - 1 - x), by = Math.Min(y, H - 1 - y);
                int inset = Math.Min(bx, by);
                if (inset is 24 or 25 or 30 or 31) { r += 0.18f; g += 0.26f; b += 0.30f; }

                float vig = 1f - (MathF.Pow(u - 0.5f, 2) + MathF.Pow(v - 0.5f, 2)) * 0.5f;
                px[y * W + x] = Pack(r * vig, g * vig, b * vig);
            }
        }
        return new Wallpaper
        {
            Id = WallpaperId.Blueprint,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x0E2A55),
        };
    }

    Wallpaper BuildPlain()
    {
        var px = new uint[W * H];
        for (int i = 0; i < px.Length; i++) px[i] = Pack(0.23f, 0.43f, 0.65f);
        return new Wallpaper
        {
            Id = WallpaperId.Plain,
            Texture = new Texture(W, H, px),
            Fallback = Color.Rgb(0x3A6EA5),
        };
    }


    /// <summary>Version 8: one flat colour and a lattice of the same colour a
    /// shade off it.
    ///
    /// The backgrounds of this era stopped pretending to be photographs, so
    /// this one does not either: a field, a weave of tiles across it, and a
    /// slow fall of light from the top left. Everything else on the desktop is
    /// meant to be the picture.</summary>
    Wallpaper BuildMetro(bool dark)
    {
        var px = new uint[W * H];

        // The accent, and the ground it sits on.
        float br = dark ? 0.08f : 0.06f;
        float bg = dark ? 0.09f : 0.22f;
        float bb = dark ? 0.11f : 0.40f;

        for (int y = 0; y < H; y++)
        {
            float v = y / (float)H;
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;

                float lift = MathF.Exp(-((u - 0.18f) * (u - 0.18f) + (v - 0.10f) * (v - 0.10f)) * 2.2f);
                float r = br + lift * (dark ? 0.10f : 0.22f);
                float g = bg + lift * (dark ? 0.11f : 0.26f);
                float b = bb + lift * (dark ? 0.14f : 0.30f);

                // The lattice: squares of two sizes, each a touch lighter than
                // the field, which is the whole of the pattern.
                int cell = 64;
                int cx = x % cell, cy = y % cell;
                bool edge = cx < 2 || cy < 2;
                bool block = (x / cell + y / cell) % 3 == 0 && cx > 6 && cy > 6 &&
                             cx < cell - 6 && cy < cell - 6;

                if (edge) { r += 0.030f; g += 0.034f; b += 0.040f; }
                if (block) { r += 0.016f; g += 0.018f; b += 0.024f; }

                // A little grain, so a flat field is not a flat file.
                float n = (Noise(u * 90f, v * 90f, 61) - 0.5f) * 0.012f;

                px[y * W + x] = Pack(r + n, g + n, b + n);
            }
        }

        return new Wallpaper
        {
            Id = dark ? WallpaperId.Miminus8Dark : WallpaperId.Miminus8,
            Texture = new Texture(W, H, px),
            Fallback = dark ? Color.Rgb(0x1A1A1E) : Color.Rgb(0x1F5AA8),
            Overlay = (c, s2) => DrawEightBranding(c, s2, dark),
        };
    }

    /// <summary>"Миминус 8": four flat squares — no shear, no gloss, no
    /// rounding — and the wordmark beside them.</summary>
    static void DrawEightBranding(UiContext c, Rect s, bool dark)
    {
        float scale = MathF.Min(s.W / 1024f, s.H / 640f);
        float cx = s.X + s.W * 0.62f;
        float cy = s.Y + s.H * 0.36f;
        float size = 150 * scale;
        float half = size * 0.5f, gap = size * 0.06f;

        var panes = new (Color col, float ox, float oy)[]
        {
            (Color.Rgb(0x2D89EF), -1, -1),
            (Color.Rgb(0x00ABA9),  1, -1),
            (Color.Rgb(0x00A300), -1,  1),
            (Color.Rgb(0xE3A21A),  1,  1),
        };

        foreach (var (col, ox, oy) in panes)
        {
            float px = cx + ox * (half * 0.5f + gap * 0.5f);
            float py = cy + oy * (half * 0.5f + gap * 0.5f);
            c.R.FillRect(new Rect(px - half * 0.5f, py - half * 0.5f, half - gap, half - gap),
                         col.WithAlpha((byte)(dark ? 235 : 250)));
        }

        string word = L.T("wall.miminus_8");
        var f = c.F.Big;
        float tw = f.Measure(word);
        float tx = cx + size * 0.72f;
        float ty = cy - f.Height * 0.5f;
        if (tx + tw > s.Right - 20) tx = s.Right - 20 - tw;

        f.Draw(c.R, word, tx + 2, ty + 2, Color.Rgba(0x000000, 110));
        f.Draw(c.R, word, tx, ty, Color.White);
    }

    /// <summary>"Миминус 7" logo: the four-pane flag in perspective plus the
    /// wordmark, matching the branding shown in part 3.</summary>
    static void DrawSevenBranding(UiContext c, Rect s, bool darkVariant)
    {
        float scale = MathF.Min(s.W / 1024f, s.H / 640f);
        // Sits right of centre so the desktop icon grid, which fills the left
        // side, does not collide with it — the same placement as in part 3.
        float cx = s.X + s.W * 0.60f;
        float cy = s.Y + s.H * 0.38f;
        float size = 150 * scale;

        if (!darkVariant)
        {
            // Four panes, sheared to suggest the waving flag.
            var panes = new (Color col, float ox, float oy)[]
            {
                (Color.Rgb(0xF25022), -1, -1),
                (Color.Rgb(0x7FBA00),  1, -1),
                (Color.Rgb(0x00A4EF), -1,  1),
                (Color.Rgb(0xFFB900),  1,  1),
            };
            float half = size * 0.5f, gap = size * 0.07f;
            foreach (var (col, ox, oy) in panes)
            {
                float px = cx + ox * (half * 0.5f + gap * 0.5f);
                float py = cy + oy * (half * 0.5f + gap * 0.5f);
                float skew = oy * size * 0.06f;
                var r = new Rect(px - half * 0.5f + skew, py - half * 0.5f, half - gap, half - gap);
                c.R.RoundedRect(r, size * 0.02f, col.WithAlpha((byte)225));
                c.R.RoundedRect(r.Deflate(half * 0.12f), size * 0.02f, Color.Rgba(0xFFFFFF, 45));
            }
        }
        else
        {
            // A large outlined 7 struck through the middle of the screen.
            float w = size * 1.5f, h = size * 1.9f;
            float x0 = cx - w * 0.35f, y0 = cy - h * 0.5f;
            Color ink = Color.Rgba(0xFFFFFF, 210);
            c.R.Line(x0, y0, x0 + w, y0, ink, 13 * scale);
            c.R.Line(x0 + w, y0, x0 + w * 0.34f, y0 + h, ink, 13 * scale);
        }

        string word = L.T("wall.miminus_7");
        var f = c.F.Big;
        float tw = f.Measure(word);
        float tx = cx + size * 0.75f;
        float ty = cy - f.Height * 0.5f;
        if (tx + tw > s.Right - 20) tx = s.Right - 20 - tw;

        Color textCol = darkVariant ? Color.Rgba(0xFFFFFF, 235) : Color.White;
        f.Draw(c.R, word, tx + 2, ty + 2, Color.Rgba(0x000000, 110));
        f.Draw(c.R, word, tx, ty, textCol);
    }

    public void Dispose()
    {
        foreach (var w in _cache.Values) w.Dispose();
        _cache.Clear();
    }
}
