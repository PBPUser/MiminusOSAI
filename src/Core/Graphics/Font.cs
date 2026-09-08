using Miminus.Platform;

namespace Miminus.Graphics;

/// <summary>How glyph edges are smoothed when rasterised.</summary>
public enum FontSmoothing
{
    /// <summary>Hard edges, no antialiasing — the Windows 95 look.</summary>
    None,
    /// <summary>Greyscale antialiasing, the XP default.</summary>
    Standard,
    /// <summary>Subpixel antialiasing; coloured fringes on an LCD panel.</summary>
    ClearType,
}

/// <summary>A bitmap font baked into a GL texture atlas.
///
/// Glyphs are rasterised on first use: GDI draws the character white-on-black
/// into a scratch DIB, the ink bounding box is measured, and the coverage is
/// copied into the atlas as the alpha channel of an otherwise-white texel. The
/// shader then multiplies by the vertex colour, so one atlas serves every text
/// colour in the app. This keeps Cyrillic and Latin working without shipping
/// any font files.</summary>
public sealed unsafe class Font : IDisposable
{
    struct Glyph
    {
        public float U0, V0, U1, V1;
        public int W, H;
        public int BearingX, BearingY;   // offset from the pen position (top-left origin)
        public int Advance;
        public bool Valid;
    }

    readonly Dictionary<char, Glyph> _glyphs = new();
    readonly Dictionary<string, float> _widths = new(StringComparer.Ordinal);
    readonly GlyphAtlas _atlas;
    readonly bool _italic;
    int _atlasVersion;

    /// <summary>Every live font, so a smoothing change can rebuild them all.</summary>
    static readonly List<Font> All = new();

    static FontSmoothing _smoothing = FontSmoothing.Standard;

    /// <summary>Global glyph smoothing. Changing it re-rasterises every font.</summary>
    static float _deviceScale = 1;

    /// <summary>How many device pixels there are per logical unit.
    ///
    /// Layout is done in logical units, which is what lets the DPI setting make
    /// every window and control bigger without a single measurement changing.
    /// Text cannot simply be magnified with them, though: a glyph is a texture,
    /// and stretching it is what blurring looks like. So the glyph is baked at
    /// the device size and drawn at the logical one — a 144-DPI system rasterises
    /// its 11-pixel Tahoma at 16 real pixels and puts it in an 11-unit box.</summary>
    public static float DeviceScale
    {
        get => _deviceScale;
        set
        {
            float scale = Math.Clamp(value, 0.5f, 4f);
            if (MathF.Abs(scale - _deviceScale) < 0.001f) return;

            _deviceScale = scale;

            // Every glyph in the atlas was baked at the old size.
            GlyphAtlas.Shared.Reset();
            foreach (var f in All) f.RebuildForDevice();
        }
    }

    public static FontSmoothing Smoothing
    {
        get => _smoothing;
        set
        {
            if (_smoothing == value) return;
            _smoothing = value;
            GlyphAtlas.Shared.Reset();
            foreach (var f in All) f.RebuildForSmoothing();
        }
    }

    /// <summary>Steps None to Standard to ClearType and back round.</summary>
    public static void CycleSmoothing() => Smoothing = _smoothing switch
    {
        FontSmoothing.None => FontSmoothing.Standard,
        FontSmoothing.Standard => FontSmoothing.ClearType,
        _ => FontSmoothing.None,
    };

    static uint QualityFlag => _smoothing switch
    {
        FontSmoothing.None => Win32.NONANTIALIASED_QUALITY,
        FontSmoothing.ClearType => Win32.CLEARTYPE_QUALITY,
        _ => Win32.ANTIALIASED_QUALITY,
    };

    IntPtr _hdc, _hfont, _hbmp;
    uint* _bits;
    int _scratchW, _scratchH;

    public string Face { get; }

    /// <summary>Size the face is asked for, in logical units.</summary>
    public int PixelHeight { get; }

    public int Height { get; private set; }      // full line height, logical
    public int Ascent { get; private set; }
    public int Descent { get; private set; }

    /// <summary>Size the glyphs are actually baked at, in device pixels.</summary>
    int DevicePixelHeight => Math.Max(1, (int)MathF.Round(PixelHeight * _deviceScale));
    public bool Bold { get; }

    /// <summary>Extra pixels inserted between characters. XP's Tahoma rendering is
    /// tight; some of our synthetic faces need a hair more room.</summary>
    public int LetterSpacing { get; set; }

    public Font(string face, int pixelHeight, bool bold = false, bool italic = false, GlyphAtlas atlas = null)
    {
        Face = face;
        PixelHeight = pixelHeight;
        Bold = bold;

        _italic = italic;

        // Every face shares one atlas so that mixed text never breaks the batch.
        _atlas = atlas ?? GlyphAtlas.Shared;
        _atlasVersion = _atlas.Version;

        _hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        _hfont = CreateHFont();
        Win32.SelectObject(_hdc, _hfont);
        Win32.SetBkMode(_hdc, Win32.OPAQUE);
        Win32.SetTextColor(_hdc, 0x00FFFFFF);
        Win32.SetBkColor(_hdc, 0x00000000);

        MeasureFace();

        All.Add(this);
    }

    /// <summary>Reads the metrics of the current GDI font and makes a scratch
    /// surface big enough to draw any one glyph of it. Called again whenever the
    /// face is rebuilt at a new size.</summary>
    void MeasureFace()
    {
        Win32.GetTextMetricsW(_hdc, out var tm);

        // Layout works in logical units, so the device metrics come back down.
        Height = (int)MathF.Round(tm.tmHeight / _deviceScale);
        Ascent = (int)MathF.Round(tm.tmAscent / _deviceScale);
        Descent = (int)MathF.Round(tm.tmDescent / _deviceScale);

        // Scratch surface large enough for the widest glyph plus antialiasing bleed.
        _scratchW = Math.Max(tm.tmMaxCharWidth * 2 + 16, DevicePixelHeight * 3 + 16);
        _scratchH = tm.tmHeight + 16;

        var bmi = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(Win32.BITMAPINFOHEADER),
                biWidth = _scratchW,
                biHeight = -_scratchH,      // negative: top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32.BI_RGB,
            }
        };

        IntPtr old = _hbmp;
        _hbmp = Win32.CreateDIBSection(_hdc, ref bmi, Win32.DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
        Win32.SelectObject(_hdc, _hbmp);
        if (old != IntPtr.Zero) Win32.DeleteObject(old);
        _bits = (uint*)bits;
    }

    /// <summary>Bakes the face again at the current device size.</summary>
    void RebuildForDevice()
    {
        IntPtr old = _hfont;
        _hfont = CreateHFont();
        Win32.SelectObject(_hdc, _hfont);
        if (old != IntPtr.Zero) Win32.DeleteObject(old);

        MeasureFace();

        _glyphs.Clear();
        _widths.Clear();
        _atlasVersion = _atlas.Version;
    }

    IntPtr CreateHFont() => Win32.CreateFontW(
        -DevicePixelHeight, 0, 0, 0,
        Bold ? Win32.FW_BOLD : Win32.FW_NORMAL,
        _italic ? 1u : 0u, 0, 0,
        Win32.DEFAULT_CHARSET,
        Win32.OUT_TT_PRECIS, Win32.CLIP_DEFAULT_PRECIS,
        QualityFlag, Win32.DEFAULT_PITCH,
        Face);

    /// <summary>Swaps in a GDI font with the new smoothing quality and drops every
    /// cached glyph and width.</summary>
    void RebuildForSmoothing()
    {
        IntPtr old = _hfont;
        _hfont = CreateHFont();
        Win32.SelectObject(_hdc, _hfont);
        if (old != IntPtr.Zero) Win32.DeleteObject(old);

        _glyphs.Clear();
        _widths.Clear();
        _atlasVersion = _atlas.Version;
    }

    /// <summary>Drops caches if the shared atlas was reset underneath us.</summary>
    void CheckAtlas()
    {
        if (_atlasVersion == _atlas.Version) return;
        _atlasVersion = _atlas.Version;
        _glyphs.Clear();
        _widths.Clear();
    }

    Glyph GetGlyph(char c)
    {
        if (_glyphs.TryGetValue(c, out var g)) return g;
        g = Rasterise(c);
        _glyphs[c] = g;
        return g;
    }

    Glyph Rasterise(char c)
    {
        var g = new Glyph();
        string s = c.ToString();

        Win32.GetTextExtentPoint32W(_hdc, s, 1, out var ext);
        g.Advance = ext.cx;

        if (c == ' ' || c == '\t' || char.IsControl(c))
        {
            g.Valid = true;
            return g;
        }

        // Clear the scratch to black, then draw the glyph in white.
        int total = _scratchW * _scratchH;
        for (int i = 0; i < total; i++) _bits[i] = 0xFF000000;

        Win32.TextOutW(_hdc, 4, 4, s, 1);
        Win32.GdiFlush();

        // Find the ink bounds so the atlas is not full of empty margins.
        int minX = _scratchW, minY = _scratchH, maxX = -1, maxY = -1;
        for (int y = 0; y < _scratchH; y++)
        {
            uint* row = _bits + y * _scratchW;
            for (int x = 0; x < _scratchW; x++)
            {
                if ((row[x] & 0x00FFFFFF) != 0)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0)
        {
            g.Valid = true;      // renders as nothing (e.g. a zero-width mark)
            return g;
        }

        int gw = maxX - minX + 1;
        int gh = maxY - minY + 1;

        if (!_atlas.Allocate(gw, gh, out int ax, out int ay))
        {
            // Atlas exhausted: render as nothing rather than corrupt the packing.
            g.Valid = true;
            return g;
        }

        for (int y = 0; y < gh; y++)
        {
            uint* src = _bits + (minY + y) * _scratchW + minX;
            for (int x = 0; x < gw; x++)
            {
                // The DIB is BGRA; all three channels are kept so ClearType's
                // subpixel coverage survives into the atlas.
                uint px = src[x];
                uint blue = px & 0xFF;
                uint green = (px >> 8) & 0xFF;
                uint red = (px >> 16) & 0xFF;
                _atlas.SetTexel(ax + x, ay + y, red, green, blue);
            }
        }

        float inv = 1f / _atlas.Size;
        g.U0 = ax * inv;
        g.V0 = ay * inv;
        g.U1 = (ax + gw) * inv;
        g.V1 = (ay + gh) * inv;
        g.W = gw;
        g.H = gh;
        g.BearingX = minX - 4;
        g.BearingY = minY - 4;
        g.Valid = true;

        return g;
    }


    // ---- measurement -----------------------------------------------------

    /// <summary>Width of a string, memoised.
    ///
    /// The immediate-mode UI measures the same labels on every frame, so the
    /// per-character walk was pure repeated work; results are cached and thrown
    /// away whenever the glyphs are rebuilt.</summary>
    public float Measure(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        CheckAtlas();

        if (_widths.TryGetValue(text, out float cached)) return cached;

        float w = 0;
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r') continue;
            // Advances are device pixels; letter spacing is a layout figure.
            w += GetGlyph(c).Advance / _deviceScale + LetterSpacing;
        }

        // Keep the table bounded: UI labels are few, document lines are not.
        if (_widths.Count > 8192) _widths.Clear();
        _widths[text] = w;
        return w;
    }

    /// <summary>Width of the first <paramref name="count"/> characters — used by text
    /// boxes to place the caret and the selection highlight.</summary>
    public float MeasureUpTo(string text, int count)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        count = Math.Clamp(count, 0, text.Length);
        float w = 0;
        for (int i = 0; i < count; i++)
        {
            char c = text[i];
            if (c == '\n' || c == '\r') continue;
            w += GetGlyph(c).Advance / _deviceScale + LetterSpacing;
        }
        return w;
    }

    /// <summary>Index of the character boundary nearest to <paramref name="targetX"/>.</summary>
    public int IndexAtX(string text, float targetX)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        float w = 0;
        for (int i = 0; i < text.Length; i++)
        {
            float adv = GetGlyph(text[i]).Advance + LetterSpacing;
            if (targetX < w + adv * 0.5f) return i;
            w += adv;
        }
        return text.Length;
    }

    /// <summary>Truncates with a trailing ellipsis to fit <paramref name="maxWidth"/>.</summary>
    public string Ellipsize(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (Measure(text) <= maxWidth) return text;
        float dots = Measure("...");
        if (dots > maxWidth) return "";
        float w = 0;
        for (int i = 0; i < text.Length; i++)
        {
            float adv = GetGlyph(text[i]).Advance + LetterSpacing;
            if (w + adv + dots > maxWidth) return text[..i] + "...";
            w += adv;
        }
        return text;
    }

    /// <summary>Greedy word wrap. Returns the produced lines.</summary>
    public List<string> Wrap(string text, float maxWidth)
    {
        var lines = new List<string>();
        if (text == null) return lines;

        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (paragraph.Length == 0) { lines.Add(""); continue; }

            string line = "";
            float lineW = 0;
            int wordStart = 0;

            while (wordStart < paragraph.Length)
            {
                int spaceEnd = wordStart;
                while (spaceEnd < paragraph.Length && paragraph[spaceEnd] == ' ') spaceEnd++;
                int wordEnd = spaceEnd;
                while (wordEnd < paragraph.Length && paragraph[wordEnd] != ' ') wordEnd++;

                string chunk = paragraph[wordStart..wordEnd];
                float chunkW = Measure(chunk);

                if (lineW > 0 && lineW + chunkW > maxWidth)
                {
                    lines.Add(line);
                    chunk = chunk.TrimStart(' ');
                    chunkW = Measure(chunk);
                    line = "";
                    lineW = 0;
                }

                // A single word wider than the box has to be broken mid-word.
                while (chunkW > maxWidth && chunk.Length > 1)
                {
                    int fit = 1;
                    float w = 0;
                    while (fit < chunk.Length)
                    {
                        float adv = GetGlyph(chunk[fit - 1]).Advance + LetterSpacing;
                        if (w + adv > maxWidth) break;
                        w += adv;
                        fit++;
                    }
                    lines.Add(chunk[..(fit - 1)]);
                    chunk = chunk[(fit - 1)..];
                    chunkW = Measure(chunk);
                }

                line += chunk;
                lineW += chunkW;
                wordStart = wordEnd;
            }
            lines.Add(line);
        }
        return lines;
    }

    // ---- drawing ---------------------------------------------------------

    /// <summary>Draws a single line with the pen at the top-left of the line box.</summary>
    public float Draw(Renderer2D r, string text, float x, float y, Color color)
    {
        if (string.IsNullOrEmpty(text)) return x;

        // Rasterise anything new before the atlas texture is fetched, so the
        // upload happens once for the whole run.
        foreach (char c in text) GetGlyph(c);
        var texture = _atlas.Texture;

        // Positions snap to whole device pixels rather than whole logical
        // units: at 144 DPI a logical unit is a pixel and a half, and rounding
        // to it would throw away the sharpness the bigger glyphs just bought.
        float scale = _deviceScale;
        float Snap(float v) => MathF.Round(v * scale) / scale;

        float pen = x;
        foreach (char c in text)
        {
            if (c == '\n' || c == '\r') continue;
            var g = GetGlyph(c);
            if (g.W > 0)
            {
                r.DrawTexture(texture,
                    new Rect(Snap(pen + g.BearingX / scale), Snap(y + g.BearingY / scale),
                             g.W / scale, g.H / scale),
                    g.U0, g.V0, g.U1, g.V1, color);
            }
            pen += g.Advance / scale + LetterSpacing;
        }
        return pen;
    }

    public void DrawCentered(Renderer2D r, string text, Rect box, Color color)
    {
        float w = Measure(text);
        Draw(r, text, box.X + (box.W - w) * 0.5f, box.Y + (box.H - Height) * 0.5f, color);
    }

    public void DrawRight(Renderer2D r, string text, Rect box, Color color)
    {
        float w = Measure(text);
        Draw(r, text, box.Right - w, box.Y + (box.H - Height) * 0.5f, color);
    }

    public void DrawVCentered(Renderer2D r, string text, float x, Rect box, Color color)
        => Draw(r, text, x, box.Y + (box.H - Height) * 0.5f, color);

    /// <summary>Text with a 1px offset shadow — the wallpaper captions and the
    /// Start button both use this.</summary>
    public void DrawShadowed(Renderer2D r, string text, float x, float y, Color color, Color shadow, float dx = 1, float dy = 1)
    {
        Draw(r, text, x + dx, y + dy, shadow);
        Draw(r, text, x, y, color);
    }

    public void Dispose()
    {
        All.Remove(this);
        // The atlas is shared and outlives any single font.
        if (_hbmp != IntPtr.Zero) { Win32.DeleteObject(_hbmp); _hbmp = IntPtr.Zero; }
        if (_hfont != IntPtr.Zero) { Win32.DeleteObject(_hfont); _hfont = IntPtr.Zero; }
        if (_hdc != IntPtr.Zero) { Win32.DeleteDC(_hdc); _hdc = IntPtr.Zero; }
    }
}
