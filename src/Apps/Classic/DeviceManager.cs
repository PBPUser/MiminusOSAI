using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Диспетчер устройств» — the console tree, in the shape it has had
/// since it stopped being a tab of the System sheet.
///
/// Half of what it lists is invented — the processor, the disks and the drives
/// come out of the same BIOS text the POST screen prints — and half of it is
/// real: the display adapter is whatever card is actually running this window,
/// read out of the GL driver, and the sound device says whether OpenAL loaded.
/// Which half is which is not marked, and that is the joke: a machine that was
/// written from scratch still has to admit what it is running on.
///
/// There is one device with a yellow mark on it, because there always is.</summary>
public sealed class DeviceManagerWindow : OsWindow
{
    public override string Title => L.T("devmgr.title");
    public override float MinWidth => 460;
    public override float MinHeight => 340;

    public DeviceManagerWindow()
    {
        Icon = IconId.MyComputer;
        Bounds = new Rect(0, 0, 600, 480);
        BuildMenu();
    }

    /// <summary>One device under a heading. <c>Problem</c> is the yellow mark,
    /// and <c>Disabled</c> the little down-arrow.</summary>
    sealed record Device(string Name, IconId Icon, string StatusKey,
                         bool Problem = false, bool Disabled = false);

    sealed record Category(string Key, IconId Icon, Device[] Devices);

    Category[] _tree;
    readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    int _selected = -1;
    float _scroll;

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("devmgr.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("devmgr.exit"), Close),
        });

        Menu.Add(L.T("devmgr.action"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("devmgr.scan"), () => Scan(_ctx), IconId.Search),
            MenuItem.Of(L.T("devmgr.properties"), () => ShowProperties(_ctx),
                        IconId.Settings, enabled: Selected != null),
            MenuItem.Sep(),
            MenuItem.Of(L.T("devmgr.uninstall"), () => Uninstall(_ctx),
                        enabled: Selected != null),
        });

        Menu.Add(L.T("devmgr.view"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("devmgr.expand_all"), () => _closed.Clear()),
            MenuItem.Of(L.T("devmgr.collapse_all"), () =>
            {
                foreach (var cat in _tree) _closed.Add(cat.Key);
            }),
        });

        Menu.Add(L.T("devmgr.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("devmgr.about"), () =>
                Shell.MessageBox(_ctx, L.T("devmgr.title"), L.T("devmgr.about_body"),
                    MsgButtons.Ok, IconId.MyComputer, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    UiContext _ctx;

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        Build();
    }

    /// <summary>Assembles the tree. The two entries that are not invented are
    /// asked for here rather than stored, so a machine with a different card
    /// shows a different card.</summary>
    void Build()
    {
        string card = SystemInfo.Renderer;
        if (string.IsNullOrWhiteSpace(card)) card = L.T("devmgr.unknown_adapter");

        bool audio = Shell.Audio.Status.StartsWith("OpenAL", StringComparison.Ordinal);

        _tree = new[]
        {
            new Category("devmgr.cat_video", IconId.Display, new[]
            {
                new Device(card, IconId.Display, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_audio", IconId.Volume, new[]
            {
                new Device(audio ? "OpenAL " + L.T("devmgr.audio_device")
                                 : L.T("devmgr.audio_missing"),
                           IconId.Volume,
                           audio ? "devmgr.status_ok" : "devmgr.status_no_driver",
                           Problem: !audio),
                new Device(L.T("devmgr.speakers"), IconId.Volume, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_processors", IconId.MyComputer, new[]
            {
                new Device("Миминус Core 2 Duo 2400 MHz", IconId.MyComputer, "devmgr.status_ok"),
                new Device("Миминус Core 2 Duo 2400 MHz", IconId.MyComputer, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_disks", IconId.DriveHdd, new[]
            {
                new Device("МИМИНУС HDD 80GB", IconId.DriveHdd, "devmgr.status_ok"),
                new Device("МИМИНУС HDD 160GB", IconId.DriveHdd, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_dvd", IconId.DriveDvd, new[]
            {
                new Device("МИМИНУС DVD-RW", IconId.DriveDvd, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_monitors", IconId.Display, new[]
            {
                new Device(L.T("devmgr.monitor"), IconId.Display, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_keyboards", IconId.TextFile, new[]
            {
                new Device(L.T("devmgr.keyboard"), IconId.TextFile, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_mice", IconId.Settings, new[]
            {
                new Device(L.T("devmgr.mouse"), IconId.Settings, "devmgr.status_ok"),
            }),
            new Category("devmgr.cat_network", IconId.Network, new[]
            {
                new Device("Миминус Ethernet", IconId.Network, "devmgr.status_checked"),
            }),
            new Category("devmgr.cat_other", IconId.DlgWarning, new[]
            {
                // The one with the mark on it. Every device manager has one.
                new Device(L.T("devmgr.unknown_device"), IconId.UnknownFile,
                           "devmgr.status_no_driver", Problem: true),
                new Device("BOLGENOS", IconId.DlgError, "devmgr.status_disabled",
                           Disabled: true),
            }),
        };
    }

    /// <summary>Every row the tree is showing, flattened, so one index can
    /// select any of them.</summary>
    List<(Category cat, Device dev)> Rows()
    {
        var rows = new List<(Category, Device)>();
        foreach (var cat in _tree)
        {
            rows.Add((cat, null));
            if (_closed.Contains(cat.Key)) continue;
            foreach (var dev in cat.Devices) rows.Add((cat, dev));
        }
        return rows;
    }

    Device Selected
    {
        get
        {
            if (_tree == null) return null;
            var rows = Rows();
            return _selected >= 0 && _selected < rows.Count ? rows[_selected].dev : null;
        }
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        if (_tree == null) Build();

        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client;
        DrawToolbar(c, area.CutTop(28));
        var status = area.CutBottom(20);

        DrawTree(c, area.Deflate(6, 4, 6, 4));

        var device = Selected;
        W.StatusBar(c, status, device != null ? device.Name : L.F("devmgr.devices", CountDevices()));
    }

    int CountDevices()
    {
        int n = 0;
        foreach (var cat in _tree) n += cat.Devices.Length;
        return n;
    }

    void DrawToolbar(UiContext c, Rect bar)
    {
        W.ToolbarBackground(c, bar);
        float x = bar.X + 4;

        if (ToolButton(c, ref x, bar, IconId.Search, "devmgr.scan", true)) Scan(c);
        W.Separator(c, x + 2, bar.Y + 4, bar.H - 8);
        x += 8;

        bool has = Selected != null;
        if (ToolButton(c, ref x, bar, IconId.Settings, "devmgr.properties", has)) ShowProperties(c);
        if (ToolButton(c, ref x, bar, IconId.RecycleBin, "devmgr.uninstall", has)) Uninstall(c);
    }

    bool ToolButton(UiContext c, ref float x, Rect bar, IconId icon, string key, bool enabled)
    {
        var r = new Rect(x, bar.Y + 3, 24, bar.H - 6);
        bool hot = enabled && c.Hovering(r);

        if (hot)
        {
            c.R.FillRect(r, c.Theme.Hot.WithAlpha((byte)110));
            c.R.DrawRect(r, c.Theme.ControlBorderHot);
        }
        Icons.Draw(c.R, icon, r.Deflate(3));
        if (!enabled) c.R.FillRect(r.Deflate(3), c.Theme.Face.WithAlpha((byte)150));

        c.Tooltip(r, L.T(key));
        x += 26;
        return enabled && c.Clicked(r);
    }

    /// <summary>The tree itself: a heading per category with a box-and-line
    /// hinge, and the devices indented under it.</summary>
    void DrawTree(UiContext c, Rect area)
    {
        var t = c.Theme;
        W.SunkenField(c, area);
        var view = area.Deflate(1);
        c.R.FillRect(view, t.FieldBack);

        var rows = Rows();
        const float rowH = 20;
        float contentH = rows.Count * rowH;

        if (contentH > view.H)
        {
            var bar = new Rect(view.Right - W.ScrollBarSize, view.Y, W.ScrollBarSize, view.H);
            _scroll = W.ScrollBarV(c, Id + ".scroll", bar, _scroll, contentH, view.H);
            view.W -= W.ScrollBarSize;
        }
        else _scroll = 0;

        if (c.Hovering(view) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 40, 0, MathF.Max(0, contentH - view.H));
            c.In.WheelDelta = 0;
        }

        c.R.PushClip(view);

        // The root, which is the computer itself.
        float y = view.Y - _scroll;

        for (int i = 0; i < rows.Count; i++)
        {
            var (cat, dev) = rows[i];
            var row = new Rect(view.X, y + i * rowH, view.W, rowH);
            if (row.Bottom < view.Y || row.Y > view.Bottom) continue;

            bool sel = i == _selected;
            if (sel) c.R.FillRect(row, t.Selection);
            else if (c.Hovering(row)) c.R.FillRect(row, t.Hot.WithAlpha((byte)70));

            Color ink = sel ? t.SelectionText : t.Text;

            if (dev == null)
            {
                // A category: the hinge, the icon, the name.
                bool open = !_closed.Contains(cat.Key);
                var hinge = new Rect(row.X + 6, row.CenterY - 5, 10, 10);
                c.R.FillRect(hinge, t.FieldBack);
                c.R.DrawRect(hinge, t.ControlBorder);
                c.R.FillRect(new Rect(hinge.X + 2, hinge.CenterY - 0.5f, 6, 1), t.Text);
                if (!open) c.R.FillRect(new Rect(hinge.CenterX - 0.5f, hinge.Y + 2, 1, 6), t.Text);

                Icons.Draw(c.R, cat.Icon, new Rect(row.X + 20, row.CenterY - 8, 16, 16));
                c.F.Ui.Draw(c.R, L.T(cat.Key), row.X + 40, row.CenterY - c.F.Ui.Height * 0.5f, ink);

                if (c.Clicked(row))
                {
                    _selected = i;
                    if (open) _closed.Add(cat.Key); else _closed.Remove(cat.Key);
                    c.SoundAt(Sfx.Click, row, 0.35f);
                }
            }
            else
            {
                var icon = new Rect(row.X + 40, row.CenterY - 8, 16, 16);
                Icons.Draw(c.R, dev.Icon, icon);

                // The yellow mark, and the arrow on a device that is switched off.
                if (dev.Problem) Badge(c, icon, Color.Rgb(0xF0C020), "!");
                else if (dev.Disabled) Badge(c, icon, Color.Rgb(0x808080), "v");

                c.R.PushClip(row);
                c.F.Ui.Draw(c.R, dev.Name, row.X + 62, row.CenterY - c.F.Ui.Height * 0.5f, ink);
                c.R.PopClip();

                if (c.Clicked(row)) { _selected = i; c.SoundAt(Sfx.Click, row, 0.3f); }
                else if (c.DoubleClicked(row)) { _selected = i; ShowProperties(c); }
            }
        }
        c.R.PopClip();
    }

    static void Badge(UiContext c, Rect icon, Color colour, string glyph)
    {
        var b = new Rect(icon.X - 3, icon.Bottom - 9, 11, 11);
        c.R.FillRect(b, colour);
        c.R.DrawRect(b, Color.Rgb(0x604000));
        c.F.Small.DrawCentered(c.R, glyph, b, Color.Black);
    }

    // ---- what the buttons do -------------------------------------------------

    void Scan(UiContext c)
    {
        Build();
        c.Sound(Sfx.ScanDone, 0.6f);
        Shell.MessageBox(c, L.T("devmgr.title"), L.T("devmgr.scan_done"),
                         MsgButtons.Ok, IconId.MyComputer, null, Sfx.Info);
    }

    void ShowProperties(UiContext c)
    {
        var device = Selected;
        if (device == null) return;

        Shell.MessageBox(c, device.Name,
            L.F("devmgr.properties_body", device.Name, L.T(device.StatusKey)),
            MsgButtons.Ok, device.Problem ? IconId.DlgWarning : IconId.MyComputer, null,
            device.Problem ? Sfx.Warning : Sfx.Info);
    }

    /// <summary>Removing a device is refused, and says why. There is nothing
    /// underneath any of these to remove.</summary>
    void Uninstall(UiContext c)
    {
        var device = Selected;
        if (device == null) return;

        Shell.MessageBox(c, L.T("devmgr.uninstall"),
            L.F("devmgr.uninstall_body", device.Name),
            MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
    }
}
