using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Paint — the image editor. Part 2 opens Paint.NET on Эфрате.jpeg and
/// scribbles lines over it; this does the same job with a tool box, a colour
/// wheel palette and a real editable bitmap.</summary>
public sealed class PaintWindow : OsWindow
{
    enum Tool { Pencil, Brush, Eraser, Line, Rectangle, Ellipse, Fill, Picker }

    Bitmap _bitmap;
    VNode _file;

    Tool _tool = Tool.Brush;
    uint _primary = 0xFF000000;   // black, ABGR-packed like the rest of the pipeline
    uint _secondary = 0xFFFFFFFF;
    int _size = 2;
    float _zoom = 1;

    bool _drawing;
    int _lastX, _lastY, _startX, _startY;
    uint[] _snapshot;
    readonly List<uint[]> _undo = new();

    static Texture _wheel;

    public override string Title
        => (_file?.Name ?? L.T("paint.untitled")) + " - " + L.T("paint.title");

    public override float MinWidth => 520;
    public override float MinHeight => 360;

    public PaintWindow(VNode file)
    {
        _file = file is { Kind: NodeKind.ImageFile } ? file : null;
        Icon = IconId.Paint;
        Bounds = new Rect(0, 0, 760, 560);

        _bitmap = LoadInto(_file) ?? new Bitmap(480, 360);

        BuildMenu();
    }

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("paint.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("paint.new"), () => { PushUndo(); _bitmap.RectFilled(0, 0, _bitmap.Width, _bitmap.Height, 0xFFFFFFFF); _bitmap.Invalidate(); }),
            MenuItem.Of(L.T("paint.open"), OpenPicture),
            MenuItem.Of(L.T("paint.save"), Save, shortcut: "Ctrl+S"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("paint.set_as_wallpaper"), () =>
            {
                Shell.SetWallpaper(WallpaperId.MiminusYellow);
                _ctx.Sound(Sfx.Navigate, 0.6f);
            }, IconId.Display),
            MenuItem.Sep(),
            MenuItem.Of(L.T("paint.exit"), Close),
        });

        Menu.Add(L.T("paint.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("paint.undo"), Undo, shortcut: "Ctrl+Z", enabled: _undo.Count > 0),
            MenuItem.Sep(),
            MenuItem.Of(L.T("paint.clear_image"), () =>
            {
                PushUndo();
                _bitmap.RectFilled(0, 0, _bitmap.Width, _bitmap.Height, _secondary);
                _bitmap.Invalidate();
            }),
        });

        Menu.Add(L.T("paint.view"), () => new List<MenuItem>
        {
            new() { Text = "50%", IsRadio = true, Checked = MathF.Abs(_zoom - 0.5f) < 0.01f, Click = () => _zoom = 0.5f },
            new() { Text = "100%", IsRadio = true, Checked = MathF.Abs(_zoom - 1f) < 0.01f, Click = () => _zoom = 1f },
            new() { Text = "200%", IsRadio = true, Checked = MathF.Abs(_zoom - 2f) < 0.01f, Click = () => _zoom = 2f },
            new() { Text = "400%", IsRadio = true, Checked = MathF.Abs(_zoom - 4f) < 0.01f, Click = () => _zoom = 4f },
        });

        Menu.Add(L.T("paint.effe_cts"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("paint.black_and_white"), () => Effect(GrayScale)),
            MenuItem.Of(L.T("paint.invert_colors"), () => Effect(Invert), shortcut: "Ctrl+I"),
            MenuItem.Of(L.T("paint.blur"), () => Effect(Blur)),
        });

        Menu.Add(L.T("paint.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("paint.about"), () =>
                Shell.MessageBox(_ctx, L.T("paint.title"),
                    L.T("paint.zevshte_about"),
                    MsgButtons.Ok, IconId.Paint, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    /// <summary>Builds the canvas for a node: a real file from a mount when we
    /// can decode it, otherwise the procedurally generated stand-in.</summary>
    static Bitmap LoadInto(VNode file)
    {
        if (file == null) return null;

        if (file.IsHosted && ImageLoader.IsSupported(file.HostPath))
        {
            uint[] px = ImageLoader.Load(file.HostPath, out int w, out int h);
            if (px != null && w > 0 && h > 0)
            {
                var loaded = new Bitmap(w, h);
                Array.Copy(px, loaded.Pixels, px.Length);
                loaded.Invalidate();
                return loaded;
            }
            return null;
        }

        if (file.Picture != PictureId.None)
        {
            var src = Pictures.Get(file.Picture);
            var copy = new Bitmap(src.Width, src.Height);
            Array.Copy(src.Pixels, copy.Pixels, src.Pixels.Length);
            copy.Invalidate();
            return copy;
        }
        return null;
    }

    void OpenPicture()
    {
        Wm.Open(new FilePickerWindow(Shell.Fs, L.T("paint.open_2"), node =>
        {
            if (node is not { Kind: NodeKind.ImageFile }) return;

            var loaded = LoadInto(node);
            if (loaded == null)
            {
                Shell.MessageBox(_ctx, L.T("paint.title"),
                    L.F("paint.cannot_open_format", node.Name),
                    MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
                return;
            }

            _file = node;
            _bitmap.Dispose();
            _bitmap = loaded;
            _undo.Clear();
        }), _ctx);
    }

    void Save()
    {
        if (_file == null)
            _file = Shell.Fs.CreateChild(Shell.Fs.MyPictures,
                L.T("paint.drawing_bmp"), NodeKind.ImageFile, IconId.ImageFile);
        _file.ExplicitSize = _bitmap.Width * _bitmap.Height * 3;
        _file.Modified = Shell.Now;
        _ctx.Sound(Sfx.Click, 0.6f);
    }

    void PushUndo()
    {
        _undo.Add(_bitmap.Clone());
        if (_undo.Count > 12) _undo.RemoveAt(0);
    }

    void Undo()
    {
        if (_undo.Count == 0) return;
        _bitmap.Restore(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
    }

    void Effect(Func<uint[], int, int, uint[]> f)
    {
        PushUndo();
        var result = f(_bitmap.Pixels, _bitmap.Width, _bitmap.Height);
        Array.Copy(result, _bitmap.Pixels, result.Length);
        _bitmap.Invalidate();
        _ctx.Sound(Sfx.Navigate, 0.5f);
    }

    static uint[] GrayScale(uint[] px, int w, int h)
    {
        var outp = new uint[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            uint p = px[i];
            uint r = p & 0xFF, g = (p >> 8) & 0xFF, b = (p >> 16) & 0xFF;
            uint y = (uint)(0.299 * r + 0.587 * g + 0.114 * b);
            outp[i] = 0xFF000000u | y | y << 8 | y << 16;
        }
        return outp;
    }

    static uint[] Invert(uint[] px, int w, int h)
    {
        var outp = new uint[px.Length];
        for (int i = 0; i < px.Length; i++)
            outp[i] = px[i] ^ 0x00FFFFFFu;
        return outp;
    }

    static uint[] Blur(uint[] px, int w, int h)
    {
        var outp = new uint[px.Length];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint sr = 0, sg = 0, sb = 0;
                int n = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        uint p = px[ny * w + nx];
                        sr += p & 0xFF; sg += (p >> 8) & 0xFF; sb += (p >> 16) & 0xFF;
                        n++;
                    }
                outp[y * w + x] = 0xFF000000u | (sr / (uint)n) | (sg / (uint)n) << 8 | (sb / (uint)n) << 16;
            }
        return outp;
    }

    UiContext _ctx;

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        c.R.FillRect(client, c.Theme.Face);

        var area = client;
        var status = area.CutBottom(20);
        var tools = area.CutLeft(52);
        var palette = area.CutBottom(64);

        DrawToolbox(c, tools);
        DrawPalette(c, palette);
        DrawCanvas(c, area, status);

        if (!c.KeyboardHandled && c.In.Ctrl)
        {
            if (c.In.KeyPressed(Keys.Z)) { Undo(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.S)) { Save(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.I)) { Effect(Invert); c.KeyboardHandled = true; }
        }
    }

    void DrawToolbox(UiContext c, Rect box)
    {
        c.R.FillRectV(box, Color.Rgb(0xF4F4F4), c.Theme.Face);
        c.R.FillRect(new Rect(box.Right - 1, box.Y, 1, box.H), c.Theme.ControlBorder);

        var tools = new (Tool tool, string tip)[]
        {
            (Tool.Pencil, "paint.pencil"),
            (Tool.Brush, "paint.brush"),
            (Tool.Eraser, "paint.eraser"),
            (Tool.Line, "paint.line"),
            (Tool.Rectangle, "paint.rectangle"),
            (Tool.Ellipse, "paint.ellipse"),
            (Tool.Fill, "paint.paint_bucket"),
            (Tool.Picker, "paint.color_picker"),
        };

        float bs = 22, gap = 3;
        for (int i = 0; i < tools.Length; i++)
        {
            var (tool, tip) = tools[i];
            var r = new Rect(box.X + 3 + (i % 2) * (bs + gap), box.Y + 4 + (i / 2) * (bs + gap), bs, bs);
            bool active = _tool == tool;
            bool hover = c.Hovering(r);

            W.DrawButtonFace(c, r, true, hover, active);
            DrawToolGlyph(c, tool, r.Deflate(4));
            c.Tooltip(r, L.T(tip));
            if (c.Clicked(r)) { _tool = tool; c.SoundAt(Sfx.Click, r, 0.4f); }
        }

        // Brush size ramp.
        float sy = box.Y + 4 + 4 * (bs + gap) + 8;
        c.F.Small.Draw(c.R, L.T("paint.size"), box.X + 4, sy, c.Theme.Text);
        sy += c.F.Small.Height + 3;
        for (int i = 0; i < 5; i++)
        {
            int size = i == 0 ? 1 : i * 3;
            var r = new Rect(box.X + 5, sy + i * 18, box.W - 12, 16);
            bool active = _size == size;
            if (active) c.R.FillRect(r, c.Theme.Selection);
            else if (c.Hovering(r)) c.R.FillRect(r, c.Theme.Hot.WithAlpha((byte)80));
            c.R.FillRect(new Rect(r.X + 4, r.CenterY - MathF.Max(1, size * 0.5f) * 0.5f,
                                  r.W - 8, MathF.Max(1, size * 0.6f)),
                         active ? Color.White : Color.Black);
            if (c.Clicked(r)) { _size = size; c.SoundAt(Sfx.Tick, r, 0.3f); }
        }
    }

    void DrawToolGlyph(UiContext c, Tool tool, Rect r)
    {
        Color ink = Color.Rgb(0x203040);
        switch (tool)
        {
            case Tool.Pencil:
                c.R.Line(r.X + 1, r.Bottom - 1, r.Right - 3, r.Y + 3, ink, 2f);
                c.R.FillTriangle(r.X, r.Bottom, r.X + 4, r.Bottom - 1, r.X + 1, r.Bottom - 4, ink);
                break;
            case Tool.Brush:
                c.R.Line(r.X + 2, r.Bottom - 2, r.Right - 3, r.Y + 3, Color.Rgb(0xA0703C), 2.6f);
                c.R.FillCircle(r.X + 2.5f, r.Bottom - 2.5f, 3, Color.Rgb(0xC02020));
                break;
            case Tool.Eraser:
                c.R.RoundedRect(new Rect(r.X + 1, r.CenterY - 3, r.W - 3, 7), 1.5f, Color.Rgb(0xF0C0C0), ink, 1);
                break;
            case Tool.Line:
                c.R.Line(r.X + 1, r.Bottom - 1, r.Right - 1, r.Y + 1, ink, 1.6f);
                break;
            case Tool.Rectangle:
                c.R.DrawRect(r.Deflate(1), ink);
                break;
            case Tool.Ellipse:
                c.R.DrawCircle(r.CenterX, r.CenterY, r.W * 0.42f, ink, 1.4f);
                break;
            case Tool.Fill:
                c.R.FillTriangle(r.X + 1, r.CenterY, r.CenterX + 2, r.Y + 1, r.Right - 1, r.CenterY, Color.Rgb(0x3070C0));
                c.R.FillRect(new Rect(r.X + 2, r.CenterY, r.W - 4, r.H * 0.32f), Color.Rgb(0x3070C0));
                c.R.FillCircle(r.Right - 2, r.Bottom - 2, 2, Color.Rgb(0x3070C0));
                break;
            case Tool.Picker:
                c.R.Line(r.X + 1, r.Bottom - 1, r.Right - 3, r.Y + 3, ink, 1.8f);
                c.R.FillCircle(r.Right - 3, r.Y + 3, 2.6f, Color.Rgb(0x3070C0));
                break;
        }
    }

    void DrawPalette(UiContext c, Rect pal)
    {
        c.R.FillRectV(pal, Color.Rgb(0xF4F4F4), c.Theme.Face);
        c.R.FillRect(new Rect(pal.X, pal.Y, pal.W, 1), c.Theme.ControlBorder);
        var area = pal.Deflate(6, 5, 6, 5);

        // Current primary / secondary swatches.
        var swatches = area.CutLeft(46);
        var sec = new Rect(swatches.X + 14, swatches.Y + 14, 24, 24);
        var pri = new Rect(swatches.X + 2, swatches.Y + 2, 24, 24);
        c.R.FillRect(sec, FromPacked(_secondary));
        c.R.DrawRect(sec, Color.Black);
        c.R.FillRect(pri, FromPacked(_primary));
        c.R.DrawRect(pri, Color.Black);
        c.Tooltip(pri, L.T("paint.primary_colour"));
        c.Tooltip(sec, L.T("paint.secondary_colour"));
        if (c.Clicked(sec)) (_primary, _secondary) = (_secondary, _primary);

        // Colour wheel.
        var wheelRect = area.CutLeft(52);
        _wheel ??= BuildWheel(64);
        var wr = new Rect(wheelRect.X, wheelRect.CenterY - 24, 48, 48);
        c.R.DrawTexture(_wheel, wr);
        if (c.Hovering(wr))
        {
            c.Cursor = CursorShape.Cross;
            if (c.In.IsDown(MouseButton.Left))
            {
                int px = (int)((c.MouseX - wr.X) / wr.W * 64);
                int py = (int)((c.MouseY - wr.Y) / wr.H * 64);
                uint col = WheelPixel(px, py, 64);
                if ((col >> 24) > 0) _primary = col;
                c.MouseHandled = true;
            }
        }

        // Standard swatch grid.
        uint[] row1 =
        {
            0xFF000000, 0xFF404040, 0xFF808080, 0xFFC0C0C0, 0xFFFFFFFF,
            0xFF000080, 0xFF0000FF, 0xFF00FFFF, 0xFF008000, 0xFF00FF00,
        };
        uint[] row2 =
        {
            0xFF004080, 0xFF0080FF, 0xFF00D2FF, 0xFF008080, 0xFF80FF80,
            0xFF800080, 0xFFFF00FF, 0xFF8000FF, 0xFF404080, 0xFF80C0FF,
        };
        float cell = 16, gap = 2;
        for (int i = 0; i < row1.Length; i++)
        {
            Swatch(c, new Rect(area.X + i * (cell + gap), area.Y + 4, cell, cell), row1[i]);
            Swatch(c, new Rect(area.X + i * (cell + gap), area.Y + 4 + cell + gap, cell, cell), row2[i]);
        }

        // Live RGB readout.
        float rx = area.X + row1.Length * (cell + gap) + 12;
        var col2 = FromPacked(_primary);
        c.F.Small.Draw(c.R, $"R {col2.R}", rx, area.Y + 2, c.Theme.Text);
        c.F.Small.Draw(c.R, $"G {col2.G}", rx, area.Y + 2 + c.F.Small.Height + 1, c.Theme.Text);
        c.F.Small.Draw(c.R, $"B {col2.B}", rx, area.Y + 2 + (c.F.Small.Height + 1) * 2, c.Theme.Text);
    }

    void Swatch(UiContext c, Rect r, uint col)
    {
        c.R.FillRect(r, FromPacked(col));
        c.R.DrawRect(r, c.Hovering(r) ? Color.White : Color.Rgb(0x808080));
        if (c.Clicked(r)) { _primary = col; c.SoundAt(Sfx.Tick, r, 0.3f); }
        else if (c.RightClicked(r)) { _secondary = col; c.SoundAt(Sfx.Tick, r, 0.3f); }
    }

    static Color FromPacked(uint p)
        => new((byte)(p & 0xFF), (byte)((p >> 8) & 0xFF), (byte)((p >> 16) & 0xFF), (byte)((p >> 24) & 0xFF));

    static uint WheelPixel(int x, int y, int size)
    {
        float cx = size * 0.5f, cy = size * 0.5f;
        float dx = (x - cx) / cx, dy = (y - cy) / cy;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > 1) return 0;

        float hue = (MathF.Atan2(dy, dx) / (MathF.PI * 2) + 1) % 1;
        return HsvToPacked(hue, Math.Clamp(d, 0, 1), 1f);
    }

    static Texture BuildWheel(int size)
    {
        var px = new uint[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = WheelPixel(x, y, size);
        return new Texture(size, size, px);
    }

    static uint HsvToPacked(float h, float s, float v)
    {
        float r = 0, g = 0, b = 0;
        int i = (int)(h * 6) % 6;
        float f = h * 6 - MathF.Floor(h * 6);
        float p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return 0xFF000000u | (uint)(r * 255) | (uint)(g * 255) << 8 | (uint)(b * 255) << 16;
    }

    void DrawCanvas(UiContext c, Rect area, Rect status)
    {
        var t = c.Theme;
        c.R.FillRect(area, Color.Rgb(0x8894A6));

        float dw = _bitmap.Width * _zoom, dh = _bitmap.Height * _zoom;
        var canvas = new Rect(
            area.X + MathF.Max(6, (area.W - dw) * 0.5f),
            area.Y + MathF.Max(6, (area.H - dh) * 0.5f),
            dw, dh);

        c.R.PushClip(area);
        c.R.FillRect(canvas.Offset(3, 3), Color.Rgba(0x000000, 70));
        c.R.FillRect(canvas, Color.White);
        c.R.DrawTexture(_bitmap.Texture, canvas);
        c.R.DrawRect(canvas.Inflate(1), Color.Black);

        HandleCanvas(c, canvas);
        c.R.PopClip();

        // Live preview of a shape while the button is still down.
        if (_drawing && _tool is Tool.Line or Tool.Rectangle or Tool.Ellipse)
        {
            var col = FromPacked(_primary);
            float x0 = canvas.X + _startX * _zoom, y0 = canvas.Y + _startY * _zoom;
            float x1 = canvas.X + _lastX * _zoom, y1 = canvas.Y + _lastY * _zoom;
            if (_tool == Tool.Line) c.R.Line(x0, y0, x1, y1, col, MathF.Max(1, _size * _zoom));
            else if (_tool == Tool.Rectangle)
                c.R.DrawRect(new Rect(MathF.Min(x0, x1), MathF.Min(y0, y1), MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)),
                             col, MathF.Max(1, _size * _zoom));
            else
                c.R.DrawCircle((x0 + x1) * 0.5f, (y0 + y1) * 0.5f,
                               MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) * 0.5f, col,
                               MathF.Max(1, _size * _zoom));
        }

        int mx = (int)((c.MouseX - canvas.X) / _zoom);
        int my = (int)((c.MouseY - canvas.Y) / _zoom);
        bool over = canvas.Contains(c.MouseX, c.MouseY);

        W.StatusBar(c, status,
            over ? $"{mx}, {my}" : "",
            $"{_bitmap.Width} x {_bitmap.Height}",
            L.F("paint.zoom_0_f0", _zoom * 100));
    }

    void HandleCanvas(UiContext c, Rect canvas)
    {
        bool over = c.Hovering(canvas);
        if (over) c.Cursor = _tool == Tool.Picker ? CursorShape.Cross : CursorShape.Cross;

        int mx = (int)MathF.Floor((c.MouseX - canvas.X) / _zoom);
        int my = (int)MathF.Floor((c.MouseY - canvas.Y) / _zoom);

        if (over && c.In.WheelDelta != 0 && c.In.Ctrl)
        {
            _zoom = Math.Clamp(_zoom * (c.In.WheelDelta > 0 ? 1.25f : 0.8f), 0.25f, 8f);
            c.MouseHandled = true;
        }

        if (!_drawing && c.Clicked(canvas))
        {
            _drawing = true;
            _startX = _lastX = mx;
            _startY = _lastY = my;
            _snapshot = _bitmap.Clone();
            PushUndo();

            switch (_tool)
            {
                case Tool.Fill:
                    _bitmap.Fill(mx, my, _primary);
                    _bitmap.Invalidate();
                    c.Sound(Sfx.Navigate, 0.5f);
                    _drawing = false;
                    break;
                case Tool.Picker:
                    _primary = _bitmap.Get(mx, my);
                    c.Sound(Sfx.Tick, 0.5f);
                    _drawing = false;
                    break;
                case Tool.Pencil:
                    _bitmap.Dab(mx, my, 0, _primary);
                    _bitmap.Invalidate();
                    break;
                case Tool.Brush:
                    _bitmap.Dab(mx, my, _size, _primary);
                    _bitmap.Invalidate();
                    break;
                case Tool.Eraser:
                    _bitmap.Dab(mx, my, _size + 1, _secondary);
                    _bitmap.Invalidate();
                    break;
            }
        }

        if (!_drawing) return;

        c.MouseHandled = true;

        if (c.In.IsDown(MouseButton.Left))
        {
            switch (_tool)
            {
                case Tool.Pencil: _bitmap.Line(_lastX, _lastY, mx, my, 0, _primary); _bitmap.Invalidate(); break;
                case Tool.Brush: _bitmap.Line(_lastX, _lastY, mx, my, _size, _primary); _bitmap.Invalidate(); break;
                case Tool.Eraser: _bitmap.Line(_lastX, _lastY, mx, my, _size + 1, _secondary); _bitmap.Invalidate(); break;
            }
            _lastX = mx;
            _lastY = my;
            return;
        }

        // Release: commit shape tools.
        switch (_tool)
        {
            case Tool.Line: _bitmap.Line(_startX, _startY, mx, my, _size, _primary); break;
            case Tool.Rectangle: _bitmap.RectOutline(_startX, _startY, mx, my, _size, _primary); break;
            case Tool.Ellipse: _bitmap.EllipseOutline(_startX, _startY, mx, my, _size, _primary); break;
        }
        _bitmap.Invalidate();
        _drawing = false;
        _snapshot = null;
    }

    public override void OnClosed() => _bitmap?.Dispose();
}
