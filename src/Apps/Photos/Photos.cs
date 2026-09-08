using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Фотографии» — the full-screen picture viewer.
///
/// It walks the whole filesystem once for anything that is an image, shows them
/// as a wall of thumbnails, and opens one at a time filling the screen with an
/// app bar under it. Mounted host folders are included, so a real directory
/// grafted in with --mount is browsable here as well as in the folder window.
///
/// It is the shortest program in the system that does something the shell
/// cannot: nothing else puts every picture on the machine in one place.</summary>
public sealed class PhotosWindow : OsWindow
{
    public override string Title => L.T("photos.title");
    public override float MinWidth => 520;
    public override float MinHeight => 360;

    public PhotosWindow()
    {
        Icon = IconId.MyPictures;
        Bounds = new Rect(0, 0, 860, 560);
        Immersive = true;
    }

    readonly List<VNode> _images = new();
    int _open = -1;
    float _scroll;
    bool _appBar = true;

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        Collect(Shell.Fs.MyComputer, 0);
    }

    /// <summary>Depth-first through the filesystem, gathering pictures. The
    /// depth is capped because a mounted directory can be arbitrarily deep and
    /// this is a picture viewer, not a search.</summary>
    void Collect(VNode node, int depth)
    {
        if (node == null || depth > 5 || _images.Count > 200) return;

        foreach (var child in node.Entries)
        {
            if (child.Kind == NodeKind.ImageFile) _images.Add(child);
            else if (child.IsContainer) Collect(child, depth + 1);
        }
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Color.Rgb(0x1B1B1B));

        if (_open >= 0 && _open < _images.Count) DrawViewer(c, client);
        else DrawWall(c, client);

        if (!c.KeyboardHandled)
        {
            if (c.In.KeyPressed(Keys.Escape) && _open >= 0) { _open = -1; c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.Right)) { Step(c, 1); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.Left)) { Step(c, -1); c.KeyboardHandled = true; }
        }
    }

    void Step(UiContext c, int delta)
    {
        if (_open < 0 || _images.Count == 0) return;
        _open = (_open + delta + _images.Count) % _images.Count;
        c.Sound(Sfx.Navigate, 0.4f);
    }

    // ---- the wall ----------------------------------------------------------

    void DrawWall(UiContext c, Rect client)
    {
        var head = client.CutTop(72);
        c.F.Big.Draw(c.R, L.T("photos.title"), head.X + 40, head.CenterY - c.F.Big.Height * 0.5f,
                     Color.White);
        c.F.Ui.Draw(c.R, L.F("photos.count", _images.Count),
                    head.X + 56 + c.F.Big.Measure(L.T("photos.title")), head.CenterY - 2,
                    Color.Rgba(0xFFFFFF, 150));

        if (_images.Count == 0)
        {
            c.F.Ui.Draw(c.R, L.T("photos.empty"), client.X + 44, client.Y + 20,
                        Color.Rgba(0xFFFFFF, 160));
            return;
        }

        var area = client.Deflate(40, 8, 40, 20);
        const float tile = 148, gap = 10;
        int cols = Math.Max(1, (int)((area.W + gap) / (tile + gap)));
        int rows = (_images.Count + cols - 1) / cols;
        float contentH = rows * (tile + gap);

        if (c.Hovering(area) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 70, 0, MathF.Max(0, contentH - area.H));
            c.In.WheelDelta = 0;
        }

        c.R.PushClip(area);
        for (int i = 0; i < _images.Count; i++)
        {
            var r = new Rect(area.X + (i % cols) * (tile + gap),
                             area.Y - _scroll + (i / cols) * (tile + gap), tile, tile);
            if (r.Bottom < area.Y || r.Y > area.Bottom) continue;

            bool hot = c.Hovering(r);
            c.R.FillRect(r, Color.Rgb(0x2A2A2A));
            DrawImage(c, r.Deflate(4), _images[i]);
            if (hot) c.R.DrawRect(r, Theme.MetroAccent, 2);

            c.R.PushClip(new Rect(r.X, r.Bottom - 20, r.W, 20));
            c.R.FillRect(new Rect(r.X, r.Bottom - 20, r.W, 20), Color.Rgba(0x000000, 170));
            c.F.Small.Draw(c.R, c.F.Small.Ellipsize(_images[i].Name, r.W - 10), r.X + 5,
                           r.Bottom - 16, Color.White);
            c.R.PopClip();

            if (c.Clicked(r)) { _open = i; c.SoundAt(Sfx.Navigate, r, 0.5f); }
        }
        c.R.PopClip();
    }

    // ---- one picture --------------------------------------------------------

    void DrawViewer(UiContext c, Rect client)
    {
        var node = _images[_open];

        var bar = _appBar ? client.CutBottom(64) : default;
        DrawImage(c, client.Deflate(30), node);

        // Left and right, over the picture, as the original had.
        var left = new Rect(client.X + 6, client.CenterY - 30, 40, 60);
        var right = new Rect(client.Right - 46, client.CenterY - 30, 40, 60);
        if (ArrowButton(c, left, 3)) Step(c, -1);
        if (ArrowButton(c, right, 1)) Step(c, 1);

        if (!_appBar) return;

        c.R.FillRect(bar, Color.Rgba(0x2A2A2A, 244));
        c.F.Ui.Draw(c.R, node.Name, bar.X + 24, bar.CenterY - c.F.Ui.Height * 0.5f, Color.White);
        c.F.Small.Draw(c.R, L.F("photos.position", _open + 1, _images.Count),
                       bar.X + 24, bar.CenterY + c.F.Ui.Height * 0.5f, Color.Rgba(0xFFFFFF, 150));

        float x = bar.Right - 120;
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.Paint, "photos.edit"))
        {
            Shell.Launch(c, "paint", node);
            Close();
        }
        x -= 110;
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.MyPictures, "photos.all"))
        {
            _open = -1;
            c.Sound(Sfx.Navigate, 0.5f);
        }
        x -= 110;
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.Display, "photos.set_wallpaper"))
        {
            Shell.SetWallpaper(WallpaperId.MiminusYellow);
            Shell.MessageBox(c, L.T("photos.title"), L.T("photos.wallpaper_note"),
                             MsgButtons.Ok, IconId.Display, null, Sfx.Info);
        }
    }

    bool ArrowButton(UiContext c, Rect r, int direction)
    {
        bool hot = c.Hovering(r);
        c.R.FillRect(r, Color.Rgba(0x000000, hot ? (byte)150 : (byte)70));
        W.Arrow(c, r, direction, Color.White, 6f);
        return c.Clicked(r);
    }

    bool BarButton(UiContext c, Rect r, IconId icon, string key)
    {
        bool hot = c.Hovering(r);
        if (hot) c.R.FillRect(r, Color.Rgba(0xFFFFFF, 35));

        var ring = new Rect(r.CenterX - 12, r.Y + 2, 24, 24);
        c.R.DrawCircle(ring.CenterX, ring.CenterY, 12, Color.White, 1.5f);
        Icons.Draw(c.R, icon, ring.Deflate(5));

        string label = c.F.Small.Ellipsize(L.T(key), r.W - 4);
        float w = c.F.Small.Measure(label);
        c.F.Small.Draw(c.R, label, r.CenterX - w * 0.5f, r.Bottom - c.F.Small.Height - 2, Color.White);
        return c.Clicked(r);
    }

    /// <summary>Draws a picture into a rectangle, keeping its proportions. A
    /// node with no picture behind it gets its icon instead, which is what an
    /// image the system cannot decode really looks like.</summary>
    static void DrawImage(UiContext c, Rect r, VNode node)
    {
        var bitmap = node.Picture != PictureId.None ? Pictures.Get(node.Picture) : null;
        if (bitmap == null)
        {
            Icons.Draw(c.R, node.Icon == IconId.None ? IconId.ImageFile : node.Icon,
                       new Rect(r.CenterX - 24, r.CenterY - 24, 48, 48));
            return;
        }

        float scale = MathF.Min(r.W / bitmap.Width, r.H / bitmap.Height);
        var dest = new Rect(r.CenterX - bitmap.Width * scale * 0.5f,
                            r.CenterY - bitmap.Height * scale * 0.5f,
                            bitmap.Width * scale, bitmap.Height * scale);
        c.R.DrawTexture(bitmap.Texture, dest);
    }
}
