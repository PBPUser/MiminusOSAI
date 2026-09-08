using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Paint — the image editor. Part 2 opens Paint.NET on Эфрате.jpeg and
/// scribbles lines over it; this does the same job on a real editable bitmap.
///
/// It wears the face Windows 7 gave Paint: the menu bar is gone and a ribbon
/// stands in its place, with the blue «Файл» tab, the tools gathered into named
/// groups, the palette laid out as two rows of squares beside the two current
/// colours, and a status bar along the foot carrying the pointer position, the
/// image size and a zoom slider. The colour wheel from the older layout is
/// still there — it is what «Изменение цветов» opens, as it was.</summary>
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
    }

    /// <summary>Which ribbon tab is showing, and whether the ribbon is rolled
    /// up — the two pieces of state the strip needs.</summary>
    int _ribbonTab;
    bool _ribbonCollapsed;

    /// <summary>Set while the colour wheel is showing. Seven kept it behind
    /// «Изменение цветов» rather than on the ribbon, and so does this.</summary>
    bool _wheelOpen;

    /// <summary>Where the pointer last was on the image, for the status bar.</summary>
    int _cursorX = -1, _cursorY = -1;

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
        c.R.FillRect(client, Color.Rgb(0xF0F0F0));

        var area = client;
        DrawRibbonTabs(c, area.CutTop(26));
        if (!_ribbonCollapsed) DrawRibbon(c, area.CutTop(96));

        var status = area.CutBottom(24);

        DrawCanvas(c, area, status);
        DrawStatusBar(c, status);
        DrawColourWheel(c, client);

        if (!c.KeyboardHandled && c.In.Ctrl)
        {
            if (c.In.KeyPressed(Keys.Z)) { Undo(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.S)) { Save(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.I)) { Effect(Invert); c.KeyboardHandled = true; }
        }
    }


    // ---- лента (the seven ribbon) -------------------------------------------

    void DrawRibbonTabs(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.Rgb(0xF5F5F5));
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Color.Rgb(0xD8D8D8));

        // The quick-access buttons seven put in the title bar; there is no room
        // up there, so they live at the head of the tab strip.
        float x = bar.X + 4;
        foreach (var (save, key, action, enabled) in new (bool, string, Action, bool)[]
                 {
                     (true, "paint.save", Save, true),
                     (false, "paint.undo", Undo, _undo.Count > 0),
                 })
        {
            var q = new Rect(x, bar.Y + 3, 20, bar.H - 6);
            bool hot = enabled && c.Hovering(q);
            if (hot) c.R.FillRect(q, Color.Rgb(0xE8F1FB));

            Color ink = enabled ? Color.Rgb(0x2A4A6A) : Color.Rgb(0xB0B0B0);
            var g = q.Deflate(4);
            if (save)
            {
                // A diskette, which is what saving still looks like.
                c.R.FillRect(g, ink);
                c.R.FillRect(new Rect(g.X + 3, g.Y, g.W - 6, g.H * 0.42f), Color.White);
                c.R.FillRect(new Rect(g.X + 2, g.Bottom - g.H * 0.42f, g.W - 4, g.H * 0.36f),
                             Color.White);
            }
            else
            {
                // The undo arrow: a hook back to the left.
                c.R.Line(g.X + 1, g.CenterY + 2, g.Right - 2, g.CenterY + 2, ink, 1.8f);
                c.R.Line(g.Right - 2, g.CenterY + 2, g.Right - 2, g.Y + 1, ink, 1.8f);
                c.R.FillTriangle(g.X, g.CenterY + 2, g.X + 5, g.CenterY - 2,
                                 g.X + 5, g.CenterY + 6, ink);
            }

            c.Tooltip(q, L.T(key));
            if (enabled && c.Clicked(q)) action();
            x += 22;
        }
        c.R.FillRect(new Rect(x + 2, bar.Y + 5, 1, bar.H - 10), Color.Rgb(0xD8D8D8));
        x += 8;

        string[] tabs = { "paint.file", "paint.home", "paint.view" };
        for (int i = 0; i < tabs.Length; i++)
        {
            string label = W.StripAccess(L.T(tabs[i]));
            var tab = new Rect(x, bar.Y, c.F.Ui.Measure(label) + 26, bar.H);
            bool file = i == 0;
            bool sel = !file && i == _ribbonTab && !_ribbonCollapsed;

            if (file) c.R.FillRect(tab, Color.Rgb(0x2D89EF));
            else if (sel) c.R.FillRect(tab, Color.White);
            else if (c.Hovering(tab)) c.R.FillRect(tab, Color.Rgb(0xE8F1FB));
            if (sel) c.R.FillRect(new Rect(tab.X, tab.Y, tab.W, 2), Color.Rgb(0x2D89EF));

            c.F.Ui.DrawCentered(c.R, label, tab, file ? Color.White : c.Theme.Text);

            if (c.Clicked(tab))
            {
                if (file) ShowFileMenu(c, tab);
                else if (i == _ribbonTab) _ribbonCollapsed = !_ribbonCollapsed;
                else { _ribbonTab = i; _ribbonCollapsed = false; }
                c.SoundAt(Sfx.Click, tab, 0.4f);
            }
            x = tab.Right;
        }

        var chevron = new Rect(bar.Right - 22, bar.Y + 4, 18, bar.H - 8);
        if (c.Hovering(chevron)) c.R.FillRect(chevron, Color.Rgb(0xE8F1FB));
        W.Arrow(c, chevron, _ribbonCollapsed ? 2 : 0, c.Theme.Text);
        if (c.Clicked(chevron)) _ribbonCollapsed = !_ribbonCollapsed;
    }

    void ShowFileMenu(UiContext c, Rect anchor)
    {
        Shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("paint.new"), NewImage, IconId.ImageFile),
            MenuItem.Of(L.T("paint.open"), OpenPicture, IconId.FolderOpen),
            MenuItem.Of(L.T("paint.save"), Save, IconId.TextFile, "Ctrl+S"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("paint.set_as_wallpaper"), () =>
            {
                Shell.SetWallpaper(WallpaperId.MiminusYellow);
                _ctx.Sound(Sfx.Navigate, 0.6f);
            }, IconId.Display),
            MenuItem.Sep(),
            MenuItem.Of(L.T("paint.about"), () =>
                Shell.MessageBox(_ctx, L.T("paint.title"), L.T("paint.zevshte_about"),
                    MsgButtons.Ok, IconId.Paint, null, Sfx.Info), IconId.DlgInfo),
            MenuItem.Of(L.T("paint.exit"), Close),
        }, anchor.X, anchor.Bottom, this, c);
    }

    void NewImage()
    {
        PushUndo();
        _bitmap.RectFilled(0, 0, _bitmap.Width, _bitmap.Height, 0xFFFFFFFF);
        _bitmap.Invalidate();
    }

    void DrawRibbon(UiContext c, Rect ribbon)
    {
        c.R.FillRect(ribbon, Color.White);
        c.R.FillRect(new Rect(ribbon.X, ribbon.Bottom - 1, ribbon.W, 1), Color.Rgb(0xD8D8D8));

        var area = ribbon.Deflate(6, 4, 6, 18);
        if (_ribbonTab == 2) DrawViewTab(c, ribbon, area);
        else DrawHomeTab(c, ribbon, area);
    }

    /// <summary>«Главная»: everything that changes the picture.</summary>
    void DrawHomeTab(UiContext c, Rect ribbon, Rect area)
    {
        // ---- изображение ----------------------------------------------------
        var group = RibbonGroup(c, ribbon, ref area, 164, "paint.group_image");
        if (RibbonBig(c, group.CutLeft(78), IconId.ImageFile, "paint.clear_image"))
        {
            PushUndo();
            _bitmap.RectFilled(0, 0, _bitmap.Width, _bitmap.Height, _secondary);
            _bitmap.Invalidate();
        }
        if (RibbonBig(c, group.CutLeft(78), IconId.FolderOpen, "paint.undo", _undo.Count > 0))
            Undo();

        // ---- инструменты ----------------------------------------------------
        group = RibbonGroup(c, ribbon, ref area, 150, "paint.group_tools");
        var tools = new (Tool tool, string tip)[]
        {
            (Tool.Pencil, "paint.pencil"), (Tool.Brush, "paint.brush"),
            (Tool.Eraser, "paint.eraser"), (Tool.Fill, "paint.paint_bucket"),
            (Tool.Line, "paint.line"), (Tool.Rectangle, "paint.rectangle"),
            (Tool.Ellipse, "paint.ellipse"), (Tool.Picker, "paint.color_picker"),
        };
        for (int i = 0; i < tools.Length; i++)
        {
            var r = new Rect(group.X + (i % 4) * 34, group.Y + 6 + (i / 4) * 30, 30, 28);
            bool active = _tool == tools[i].tool;
            bool hot = c.Hovering(r);

            if (active) c.R.FillRect(r, Color.Rgb(0xCFE4F7));
            else if (hot) c.R.FillRect(r, Color.Rgb(0xE8F1FB));
            if (active || hot) c.R.DrawRect(r, Color.Rgb(0x3C7FB1));

            DrawToolGlyph(c, tools[i].tool, r.Deflate(7));
            c.Tooltip(r, L.T(tools[i].tip));
            if (c.Clicked(r)) { _tool = tools[i].tool; c.SoundAt(Sfx.Click, r, 0.4f); }
        }

        // ---- толщина --------------------------------------------------------
        group = RibbonGroup(c, ribbon, ref area, 84, "paint.group_size");
        for (int i = 0; i < 4; i++)
        {
            int size = i == 0 ? 1 : i * 3;
            var r = new Rect(group.X + 4, group.Y + 4 + i * 15, group.W - 12, 14);
            bool active = _size == size;

            if (active) c.R.FillRect(r, Color.Rgb(0xCFE4F7));
            else if (c.Hovering(r)) c.R.FillRect(r, Color.Rgb(0xE8F1FB));
            if (active) c.R.DrawRect(r, Color.Rgb(0x3C7FB1));

            c.R.FillRect(new Rect(r.X + 6, r.CenterY - MathF.Max(1, size * 0.6f) * 0.5f,
                                  r.W - 12, MathF.Max(1, size * 0.6f)), Color.Rgb(0x202020));
            if (c.Clicked(r)) { _size = size; c.SoundAt(Sfx.Tick, r, 0.3f); }
        }

        // ---- цвета ----------------------------------------------------------
        group = RibbonGroup(c, ribbon, ref area, MathF.Max(190, area.W), "paint.group_colours");

        // The two current colours, one behind the other, as seven drew them.
        var sec = new Rect(group.X + 14, group.Y + 20, 26, 26);
        var pri = new Rect(group.X + 2, group.Y + 8, 26, 26);
        c.R.FillRect(sec, FromPacked(_secondary));
        c.R.DrawRect(sec, Color.Rgb(0x808080));
        c.R.FillRect(pri, FromPacked(_primary));
        c.R.DrawRect(pri, Color.Rgb(0x404040));
        c.Tooltip(pri, L.T("paint.primary_colour"));
        c.Tooltip(sec, L.T("paint.secondary_colour"));
        if (c.Clicked(sec)) (_primary, _secondary) = (_secondary, _primary);

        // Two rows of squares, which is the palette.
        float px = group.X + 56;
        for (int i = 0; i < Palette.Length; i++)
        {
            var sw = new Rect(px + (i % 10) * 17, group.Y + 6 + (i / 10) * 19, 15, 17);
            if (sw.Right > group.Right - 60) continue;

            c.R.FillRect(sw, FromPacked(Palette[i]));
            c.R.DrawRect(sw, c.Hovering(sw) ? Color.Rgb(0x3C7FB1) : Color.Rgb(0x909090));

            if (c.Clicked(sw)) { _primary = Palette[i]; c.SoundAt(Sfx.Click, sw, 0.35f); }
            else if (c.RightClicked(sw)) _secondary = Palette[i];
        }

        var more = new Rect(group.Right - 56, group.Y + 8, 52, 34);
        if (RibbonBig(c, more, IconId.Paint, "paint.edit_colours")) _wheelOpen = !_wheelOpen;
    }

    /// <summary>«Вид»: the zoom, and nothing else — there is nothing else.</summary>
    void DrawViewTab(UiContext c, Rect ribbon, Rect area)
    {
        var group = RibbonGroup(c, ribbon, ref area, 250, "paint.group_zoom");
        foreach (float zoom in new[] { 0.5f, 1f, 2f, 4f })
        {
            var r = group.CutLeft(60);
            bool active = MathF.Abs(_zoom - zoom) < 0.01f;
            if (RibbonBig(c, r, IconId.Search, null, true, active,
                          (zoom * 100).ToString("0") + "%"))
                _zoom = zoom;
        }
    }

    /// <summary>Cuts one titled group out of the ribbon, draws its name under it
    /// and the hairline after it.</summary>
    Rect RibbonGroup(UiContext c, Rect ribbon, ref Rect area, float width, string titleKey)
    {
        var group = area.CutLeft(MathF.Min(width, MathF.Max(0, area.W)));

        c.F.Small.DrawCentered(c.R, L.T(titleKey),
                               new Rect(group.X, ribbon.Bottom - 17, group.W, 14),
                               c.Theme.TextDisabled);
        c.R.FillRect(new Rect(group.Right + 1, ribbon.Y + 6, 1, ribbon.H - 14), Color.Rgb(0xE4E4E4));
        area.CutLeft(4);
        return group;
    }

    /// <summary>A ribbon button: picture over caption.</summary>
    bool RibbonBig(UiContext c, Rect r, IconId icon, string key, bool enabled = true,
                   bool active = false, string literal = null)
    {
        bool hover = enabled && c.Hovering(r);
        bool clicked = enabled && c.Clicked(r);

        if (active) c.R.FillRect(r, Color.Rgb(0xCFE4F7));
        if (hover) c.R.FillRect(r, Color.Rgb(0xE8F1FB));
        if (hover || active) c.R.DrawRect(r, Color.Rgb(0x3C7FB1));

        var ic = new Rect(r.CenterX - 14, r.Y + 4, 28, 28);
        Icons.Draw(c.R, icon, ic);
        if (!enabled) c.R.FillRect(ic, Color.Rgba(0xFFFFFF, 150));

        c.R.PushClip(r);
        string label = literal ?? L.T(key);
        foreach (string line in c.F.Small.Wrap(label, r.W - 4).Take(2))
        {
            float w = c.F.Small.Measure(line);
            c.F.Small.Draw(c.R, line, r.CenterX - w * 0.5f, ic.Bottom + 2,
                           enabled ? c.Theme.Text : c.Theme.TextDisabled);
            ic = new Rect(ic.X, ic.Y + c.F.Small.Height, ic.W, ic.H);
        }
        c.R.PopClip();

        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);
        return clicked;
    }

    /// <summary>The twenty squares of the palette, in seven's two rows.</summary>
    static readonly uint[] Palette =
    {
        // Packed 0xAABBGGRR, like every other colour in the pipeline.
        0xFF000000, 0xFF404040, 0xFF808080, 0xFFC0C0C0, 0xFFFFFFFF,
        0xFF202080, 0xFF2020E0, 0xFF2060F0, 0xFF20A0F0, 0xFF20D0FF,
        0xFF206020, 0xFF20A020, 0xFF20E020, 0xFF80E080, 0xFF20E0E0,
        0xFFC02020, 0xFFE06020, 0xFFE0A020, 0xFF802080, 0xFFE020E0,
    };

    // ---- the colour wheel, behind «Изменение цветов» -------------------------

    void DrawColourWheel(UiContext c, Rect client)
    {
        if (!_wheelOpen) return;

        var panel = new Rect(client.Right - 210, client.Y + 122, 200, 150);
        panel.X = MathF.Max(client.X + 4, panel.X);

        c.R.FillRect(panel.Offset(3, 3), Color.Rgba(0x000000, 60));
        c.R.FillRect(panel, Color.Rgb(0xF7F7F7));
        c.R.DrawRect(panel, Color.Rgb(0xA0A0A0));

        var head = panel.Deflate(8, 6, 8, 0).CutTop(18);
        c.F.UiBold.Draw(c.R, L.T("paint.edit_colours"), head.X, head.Y, Color.Rgb(0x1A1A1A));

        var wheel = new Rect(panel.X + 12, head.Bottom + 6, 100, 100);
        DrawWheel(c, wheel);

        var preview = new Rect(wheel.Right + 16, wheel.Y + 8, 60, 40);
        c.R.FillRect(preview, FromPacked(_primary));
        c.R.DrawRect(preview, Color.Rgb(0x606060));

        var close = new Rect(wheel.Right + 16, preview.Bottom + 10, 60, 22);
        if (W.Button(c, Id + ".wheelok", close, L.T("win.ok"))) _wheelOpen = false;

        bool outside = !panel.Contains(c.MouseX, c.MouseY);
        if (c.In.Pressed(MouseButton.Left) && outside && !c.MouseHandled) _wheelOpen = false;
    }

    // ---- status bar ----------------------------------------------------------

    /// <summary>The strip seven put along the foot: where the pointer is on the
    /// image, how big the image is, and the zoom.</summary>
    void DrawStatusBar(UiContext c, Rect r)
    {
        c.R.FillRect(r, Color.Rgb(0xF0F0F0));
        c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Color.Rgb(0xE0E0E0));

        string position = _cursorX >= 0 ? _cursorX + ", " + _cursorY : "—";
        c.F.Small.Draw(c.R, position, r.X + 10, r.CenterY - c.F.Small.Height * 0.5f,
                       Color.Rgb(0x404040));
        c.F.Small.Draw(c.R, _bitmap.Width + " × " + _bitmap.Height, r.X + 110,
                       r.CenterY - c.F.Small.Height * 0.5f, Color.Rgb(0x404040));

        // The zoom slider, at the right end where it belongs.
        var slider = new Rect(r.Right - 140, r.CenterY - 8, 100, 16);
        float zoom = _zoom;
        if (W.Slider(c, Id + ".zoom", slider, ref zoom, 0.25f, 4f))
            _zoom = MathF.Round(zoom * 4) / 4;

        c.F.Small.Draw(c.R, (_zoom * 100).ToString("0") + "%", slider.Right + 8,
                       r.CenterY - c.F.Small.Height * 0.5f, Color.Rgb(0x404040));
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

    /// <summary>The colour wheel, which is the same picture the old palette
    /// strip carried: a hue ring built once into a texture and sampled where it
    /// is clicked. Dragging across it picks continuously.</summary>
    void DrawWheel(UiContext c, Rect wr)
    {
        _wheel ??= BuildWheel(64);
        c.R.DrawTexture(_wheel, wr);

        if (!c.Hovering(wr)) return;

        c.Cursor = CursorShape.Cross;
        if (!c.In.IsDown(MouseButton.Left)) return;

        int px = (int)((c.MouseX - wr.X) / wr.W * 64);
        int py = (int)((c.MouseY - wr.Y) / wr.H * 64);
        uint col = WheelPixel(px, py, 64);
        if ((col >> 24) > 0) _primary = col;
        c.MouseHandled = true;
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
