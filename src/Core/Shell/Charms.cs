using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Whether the pointer has been moved by a person yet.
///
/// A fresh window puts the pointer at the origin, which is a corner, and a
/// corner is a gesture in version 8 — so without this the system would come up
/// with the switcher already open. One movement anywhere is enough.</summary>
static class EdgeGuard
{
    static float _lastX = -1, _lastY = -1;
    static bool _moved;

    public static bool PointerHasMoved(UiContext c)
    {
        if (_moved) return true;
        if (_lastX < 0) { _lastX = c.MouseX; _lastY = c.MouseY; return false; }
        if (MathF.Abs(c.MouseX - _lastX) > 2 || MathF.Abs(c.MouseY - _lastY) > 2) _moved = true;
        return _moved;
    }
}

/// <summary>Чудо-кнопки — the strip that comes out of the right edge.
///
/// Version 8 moved the things a shell used to keep in a Start menu onto five
/// buttons hidden off the right-hand side of the screen, and told nobody. That
/// is reproduced exactly: the strip appears when the pointer finds a corner it
/// was not looking for, and the only way to turn the computer off is three
/// clicks inside it — «Параметры», then the power symbol, then the answer.</summary>
public sealed class Charms
{
    readonly ShellHost _shell;

    public Charms(ShellHost shell) => _shell = shell;

    public enum Pane { None, Settings, Search, Share, Devices }

    /// <summary>Which flyout is showing, if any. The strip stays out while one
    /// is open, however far away the pointer wanders.</summary>
    public Pane Open { get; private set; }

    /// <summary>How far out the strip is, 0 to 1. Animated, because the whole
    /// point of the gesture is that the strip slides.</summary>
    float _out;

    /// <summary>Set while the pointer is being held in a corner, so brushing
    /// past the edge on the way somewhere else does not summon it.</summary>
    double _cornerSince = -1;

    /// <summary>Which control in the settings pane has been expanded.</summary>
    string _expanded;

    const float BarW = 84;
    const float PaneW = 384;

    public bool Showing => _out > 0.01f || Open != Pane.None;

    /// <summary>The screen this covers, which the shell reserves before windows
    /// run their input, so a click on a charm is never taken by what is under it.</summary>
    public Rect Bounds(UiContext c)
    {
        if (!Showing) return default;
        float w = Open != Pane.None ? PaneW : BarW;
        return new Rect(c.ScreenW - w, 0, w, c.ScreenH);
    }

    public void Close(UiContext c)
    {
        if (Open == Pane.None && _out <= 0) return;
        Open = Pane.None;
        _out = 0;
        _expanded = null;
        c.Sound(Sfx.MenuClose, 0.5f);
    }

    public void Show(UiContext c, Pane pane = Pane.None)
    {
        if (_out <= 0) c.Sound(Sfx.Charm, 0.6f);
        _out = 1;
        Open = pane;
    }

    // ---- frame -----------------------------------------------------------

    /// <summary>Watches the corners. Called before windows take input, because
    /// the answer decides whether they get any.</summary>
    public void Poll(UiContext c)
    {
        if (Open != Pane.None) { _out = 1; return; }

        // A pointer that has not moved yet is not in a corner on purpose: it is
        // simply where the window opened. Until it moves once, the edges are
        // quiet — otherwise the system starts with the charms already out.
        if (!EdgeGuard.PointerHasMoved(c)) { _out = MathF.Max(0, _out - c.Dt * 4); return; }

        bool inCorner = c.MouseX >= c.ScreenW - 4 &&
                        (c.MouseY <= 6 || c.MouseY >= c.ScreenH - 6);
        bool onStrip = _out > 0.5f && c.MouseX >= c.ScreenW - BarW;

        if (inCorner)
        {
            if (_cornerSince < 0) _cornerSince = c.Time;
            if (c.Time - _cornerSince > 0.18)
            {
                if (_out <= 0) c.Sound(Sfx.Charm, 0.5f);
                _out = 1;
            }
        }
        else
        {
            _cornerSince = -1;
            if (!onStrip) _out = MathF.Max(0, _out - c.Dt * 4);
        }
    }

    public void Draw(UiContext c)
    {
        if (!Showing) return;

        var screen = new Rect(0, 0, c.ScreenW, c.ScreenH);
        float slide = (1 - _out) * BarW;

        if (Open != Pane.None) DrawPane(c, screen);
        DrawBar(c, screen, slide);
        DrawCornerClock(c, screen);

        // Anywhere else puts the whole thing away.
        if ((c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) && !c.MouseHandled)
            Close(c);
        else if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape))
        {
            Close(c);
            c.KeyboardHandled = true;
        }
    }

    void DrawBar(UiContext c, Rect screen, float slide)
    {
        var bar = new Rect(screen.Right - BarW + slide, 0, BarW, screen.H);
        c.R.FillRect(bar, Color.Rgba(0x1C1C1C, (byte)(238 * MathF.Min(1, _out + 0.001f))));

        (IconId icon, string key, Pane pane)[] charms =
        {
            (IconId.Search, "charm.search", Pane.Search),
            (IconId.Share, "charm.share", Pane.Share),
            (IconId.Tiles, "charm.start", Pane.None),
            (IconId.Devices, "charm.devices", Pane.Devices),
            (IconId.Settings, "charm.settings", Pane.Settings),
        };

        float h = 92;
        float y = bar.CenterY - charms.Length * h * 0.5f;

        foreach (var (icon, key, pane) in charms)
        {
            var r = new Rect(bar.X, y, bar.W, h);
            bool hot = c.Hovering(r);
            bool isStart = pane == Pane.None;

            if (hot) c.R.FillRect(r, Color.Rgba(0xFFFFFF, 26));

            float size = isStart ? 40 : 30;
            var ic = new Rect(r.CenterX - size * 0.5f, r.CenterY - size * 0.5f - 8, size, size);

            if (isStart)
            {
                // The Start charm keeps the accent square behind it, so the
                // middle of the strip is the one button you can find blind.
                c.R.FillRect(ic.Inflate(8), (hot ? Theme.MetroAccent.Shade(1.15f) : Theme.MetroAccent)
                             .WithAlpha((byte)(255 * _out)));
            }
            Icons.Draw(c.R, icon, ic);

            string label = c.F.Small.Ellipsize(L.T(key), r.W - 6);
            float w = c.F.Small.Measure(label);
            c.F.Small.Draw(c.R, label, r.CenterX - w * 0.5f, r.Bottom - 26,
                           Color.Rgba(0xFFFFFF, (byte)((hot ? 255 : 190) * _out)));

            if (c.Clicked(r))
            {
                if (isStart) { Close(c); _shell.Start.Toggle(c); }
                else if (Open == pane) Open = Pane.None;
                else { Open = pane; _expanded = null; c.Sound(Sfx.Navigate, 0.5f); }
            }

            y += h;
        }
    }

    /// <summary>The clock block version 8 put in the opposite corner whenever
    /// the strip came out — the date, the time and how the network is doing.</summary>
    void DrawCornerClock(UiContext c, Rect screen)
    {
        byte alpha = (byte)(255 * _out);
        if (alpha < 20) return;

        // Sized to what is in it: the time is set in the largest face the
        // system has, and the box has to be whatever that turns out to be.
        string time = L.Time(_shell.Now);
        string date = L.LongDate(_shell.Now);
        float w = MathF.Max(c.F.Huge.Measure(time), c.F.Caption.Measure(date)) + 40;
        // The face has a lot of air above and below its digits, so the box is
        // laid out against where the ink actually falls rather than the line
        // height: a fifth of it above, nine tenths of it below.
        float huge = c.F.Huge.Height;
        float h = huge * 0.74f + c.F.Caption.Height + 62;

        var box = new Rect(40, screen.Bottom - h - 56, w, h);
        c.R.FillRect(box, Color.Rgba(0x1C1C1C, (byte)(alpha * 0.82f)));

        c.F.Huge.Draw(c.R, time, box.X + 20, box.Y - huge * 0.25f, Color.Rgba(0xFFFFFF, alpha));
        c.F.Caption.Draw(c.R, date, box.X + 22, box.Y + huge * 0.72f,
                         Color.Rgba(0xFFFFFF, (byte)(alpha * 0.8f)));

        var netIcon = new Rect(box.X + 20, box.Bottom - 34, 20, 20);
        Icons.Draw(c.R, IconId.TrayNetwork, netIcon);
        c.F.Small.Draw(c.R, L.T("charm.network_state"), netIcon.Right + 8,
                       netIcon.CenterY - c.F.Small.Height * 0.5f, Color.Rgba(0xFFFFFF, (byte)(alpha * 0.8f)));
    }

    // ---- the flyouts -------------------------------------------------------

    void DrawPane(UiContext c, Rect screen)
    {
        var pane = new Rect(screen.Right - PaneW, 0, PaneW - BarW, screen.H);
        c.R.FillRect(pane, Color.Rgba(0x2A2A2A, 244));

        var area = pane.Deflate(20, 22, 20, 20);

        switch (Open)
        {
            case Pane.Settings: DrawSettingsPane(c, area); break;
            case Pane.Search: DrawSearchPane(c, area); break;
            case Pane.Share: DrawSharePane(c, area); break;
            case Pane.Devices: DrawDevicesPane(c, area); break;
        }
    }

    void PaneTitle(UiContext c, ref Rect area, string title, string subtitle = null)
    {
        var head = area.CutTop(subtitle == null ? 44 : 62);
        c.F.Big.Draw(c.R, title, head.X, head.Y - 6, Color.White);
        if (subtitle != null)
            c.F.Small.Draw(c.R, subtitle, head.X + 2, head.Y + c.F.Big.Height - 4,
                           Color.Rgba(0xFFFFFF, 150));
    }

    /// <summary>«Параметры» — the pane the power button lives at the bottom of.</summary>
    void DrawSettingsPane(UiContext c, Rect area)
    {
        PaneTitle(c, ref area, L.T("charm.settings"), Registry.ComputerName);

        // The links along the top, the ones that open the old applets.
        (string key, string app)[] links =
        {
            ("start.control_panel", "controlpanel"),
            ("charm.personalise", "display"),
            ("charm.pc_info", "about"),
            ("start.help_and_support", "help"),
        };
        foreach (var (key, app) in links)
        {
            var row = area.CutTop(30);
            bool hot = c.Hovering(row);
            if (hot) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 26));
            c.F.Ui.Draw(c.R, L.T(key), row.X + 4, row.CenterY - c.F.Ui.Height * 0.5f,
                        Color.Rgba(0xFFFFFF, hot ? (byte)255 : (byte)200));
            if (c.Clicked(row)) { Close(c); _shell.Launch(c, app, null); }
        }

        // The six squares. Everything the pane can actually change is here,
        // and every one of them changes something real.
        var grid = area.CutBottom(190);
        var expanded = grid.CutTop(58);
        DrawExpanded(c, expanded);

        float cell = (grid.W - 16) / 3;
        (string id, IconId icon, string key)[] cells =
        {
            ("net", IconId.TrayNetwork, "charm.network"),
            ("vol", IconId.Volume, "charm.volume"),
            ("bright", IconId.Display, "charm.brightness"),
            ("notify", IconId.DlgInfo, "charm.notifications"),
            ("power", IconId.Power, "charm.power"),
            ("lang", IconId.Flag, "charm.language"),
        };

        for (int i = 0; i < cells.Length; i++)
        {
            var (id, icon, key) = cells[i];
            var r = new Rect(grid.X + (i % 3) * (cell + 8), grid.Y + (i / 3) * 76, cell, 68);
            bool hot = c.Hovering(r);
            if (hot || _expanded == id) c.R.FillRect(r, Color.Rgba(0xFFFFFF, hot ? (byte)34 : (byte)20));

            Icons.Draw(c.R, icon, new Rect(r.CenterX - 13, r.Y + 8, 26, 26));

            string label = c.F.Small.Ellipsize(L.T(key), r.W - 4);
            string value = CellValue(id);
            float lw = c.F.Small.Measure(label);
            c.F.Small.Draw(c.R, label, r.CenterX - lw * 0.5f, r.Bottom - 26, Color.Rgba(0xFFFFFF, 210));
            if (value != null)
            {
                value = c.F.Small.Ellipsize(value, r.W - 4);
                float vw = c.F.Small.Measure(value);
                c.F.Small.Draw(c.R, value, r.CenterX - vw * 0.5f, r.Bottom - 13,
                               Color.Rgba(0xFFFFFF, 140));
            }

            if (c.Clicked(r)) CellClicked(c, id, r);
        }

        // The link the whole pane exists to hide.
        var link = area.CutBottom(34);
        bool linkHot = c.Hovering(link);
        c.F.Ui.Draw(c.R, L.T("charm.change_pc_settings"), link.X + 4,
                    link.CenterY - c.F.Ui.Height * 0.5f,
                    Color.Rgba(0xFFFFFF, linkHot ? (byte)255 : (byte)190));
        if (c.Clicked(link)) { Close(c); _shell.Launch(c, "pcsettings", null); }
    }

    string CellValue(string id) => id switch
    {
        "vol" => _shell.Audio.Muted ? L.T("tray.volume_off")
                                    : (int)MathF.Round(_shell.Audio.MasterVolume * 100) + "%",
        "bright" => (int)MathF.Round(_shell.Settings.Brightness * 100) + "%",
        "lang" => L.TrayTag,
        "net" => L.T("charm.network_state"),
        _ => null,
    };

    void CellClicked(UiContext c, string id, Rect r)
    {
        switch (id)
        {
            case "vol":
            case "bright":
                _expanded = _expanded == id ? null : id;
                c.Sound(Sfx.Click, 0.5f);
                break;

            case "lang":
                L.Toggle();
                c.Sound(Sfx.Click, 0.6f);
                break;

            case "net":
                Close(c);
                _shell.ShowNetworkBalloon(c);
                break;

            case "notify":
                Close(c);
                _shell.ShowUpdateNotice(c);
                break;

            case "power":
                // The action the power page picked is offered first, in bold.
                var sleep = MenuItem.Of(L.T("charm.sleep"), () => _shell.LockScreenNow(c), IconId.Lock);
                var restart = MenuItem.Of(L.T("charm.restart"),
                                          () => { Close(c); _shell.Restart(c); }, IconId.Logoff);
                var off = MenuItem.Of(L.T("charm.shutdown"),
                                      () => { Close(c); _shell.BeginShutdown(c); }, IconId.Shutdown);

                var items = _shell.Settings.PowerButtonAction switch
                {
                    1 => new List<MenuItem> { sleep, restart, off },
                    2 => new List<MenuItem>
                    {
                        MenuItem.Of(L.T("start.log_off"), () => _shell.BeginLogOff(c), IconId.Logoff),
                        sleep, restart, off,
                    },
                    _ => new List<MenuItem> { off, restart, sleep },
                };
                items[0].Bold = true;

                _shell.Menus.Open(items, r.X, r.Y - 8, this, c);
                break;
        }
    }

    /// <summary>The slider one of the six squares opens. Volume and brightness
    /// are both real: this one is the only place brightness can be changed, and
    /// it dims the whole screen when it is.</summary>
    void DrawExpanded(UiContext c, Rect r)
    {
        if (_expanded == null) return;

        c.F.Small.Draw(c.R, L.T(_expanded == "vol" ? "charm.volume" : "charm.brightness"),
                       r.X + 4, r.Y, Color.Rgba(0xFFFFFF, 190));

        var groove = new Rect(r.X + 4, r.Y + 24, r.W - 8, 22);

        if (_expanded == "vol")
        {
            float level = _shell.Audio.MasterVolume;
            if (W.Slider(c, "charm.volume.slider", groove, ref level, 0, 1))
            {
                _shell.Audio.MasterVolume = level;
                c.Sound(Sfx.Tick, 0.5f);
            }
        }
        else
        {
            float level = _shell.Settings.Brightness;
            if (W.Slider(c, "charm.bright.slider", groove, ref level, 0.35f, 1f))
                _shell.Settings.Brightness = level;
        }
    }

    void DrawSearchPane(UiContext c, Rect area)
    {
        PaneTitle(c, ref area, L.T("charm.search"));

        var box = area.CutTop(34);
        c.R.FillRect(box, Color.White);
        c.F.Ui.Draw(c.R, L.T("charm.search_hint"), box.X + 8, box.CenterY - c.F.Ui.Height * 0.5f,
                    Color.Rgb(0x909090));
        Icons.Draw(c.R, IconId.Search, new Rect(box.Right - 26, box.CenterY - 9, 18, 18));

        area.CutTop(14);
        foreach (string line in c.F.Ui.Wrap(L.T("charm.search_body"), area.W))
        {
            var row = area.CutTop(c.F.Ui.Height + 4);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, Color.Rgba(0xFFFFFF, 190));
        }

        area.CutTop(12);
        var go = area.CutTop(30);
        bool hot = c.Hovering(go);
        c.F.Ui.Draw(c.R, L.T("start8.all_apps"), go.X + 4, go.CenterY - c.F.Ui.Height * 0.5f,
                    Color.Rgba(0xFFFFFF, hot ? (byte)255 : (byte)200));
        if (c.Clicked(go)) { Close(c); _shell.Start.Open(c); }
    }

    void DrawSharePane(UiContext c, Rect area)
    {
        PaneTitle(c, ref area, L.T("charm.share"));

        var w = _shell.Wm.Focused;
        string body = w == null ? L.T("charm.share_nothing") : L.F("charm.share_from", w.Title);

        foreach (string line in c.F.Ui.Wrap(body, area.W))
        {
            var row = area.CutTop(c.F.Ui.Height + 4);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, Color.Rgba(0xFFFFFF, 200));
        }
    }

    void DrawDevicesPane(UiContext c, Rect area)
    {
        PaneTitle(c, ref area, L.T("charm.devices"));

        (IconId icon, string key)[] devices =
        {
            (IconId.Printer, "charm.device_printer"),
            (IconId.Devices, "charm.device_second_screen"),
            (IconId.Phone, "charm.device_phone"),
        };

        foreach (var (icon, key) in devices)
        {
            var row = area.CutTop(46);
            bool hot = c.Hovering(row);
            if (hot) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 22));
            Icons.Draw(c.R, icon, new Rect(row.X + 4, row.CenterY - 14, 28, 28));
            c.F.Ui.Draw(c.R, L.T(key), row.X + 40, row.CenterY - c.F.Ui.Height * 0.5f,
                        Color.Rgba(0xFFFFFF, 210));
            if (c.Clicked(row))
            {
                Close(c);
                _shell.MessageBox(c, L.T("charm.devices"), L.T("charm.devices_none"),
                                  MsgButtons.Ok, IconId.Devices, null, Sfx.Warning);
            }
        }
    }
}

/// <summary>The other two edges: the corner that opens «Пуск», the corner that
/// holds the power-user menu, and the strip of running programs down the left.
///
/// Windows 8 replaced the Start button with a place where a Start button used
/// to be, and this is that place — a thumbnail that appears when the pointer
/// reaches the very bottom-left pixel of the screen, and nothing at all when it
/// does not.</summary>
public sealed class SwitcherBar
{
    readonly ShellHost _shell;

    public SwitcherBar(ShellHost shell) => _shell = shell;

    float _cornerHint;      // 0..1: the Start thumbnail in the bottom-left corner
    float _strip;           // 0..1: the switcher down the left edge

    const float StripW = 96;
    const float ThumbH = 68;

    public bool Showing => _strip > 0.01f;

    /// <summary>What the switcher covers, reserved before windows take input.</summary>
    public Rect Bounds(UiContext c)
    {
        if (!Showing) return default;
        return new Rect(0, 0, StripW, c.ScreenH);
    }

    public void Show() => _strip = 1;

    public void Poll(UiContext c)
    {
        if (!EdgeGuard.PointerHasMoved(c))
        {
            _cornerHint = 0;
            _strip = MathF.Max(0, _strip - c.Dt * 4);
            return;
        }

        bool bottomLeft = c.MouseX <= 6 && c.MouseY >= c.ScreenH - 6;
        bool topLeft = c.MouseX <= 6 && c.MouseY <= 6;
        bool onStrip = _strip > 0.5f && c.MouseX <= StripW;

        _cornerHint = bottomLeft ? MathF.Min(1, _cornerHint + c.Dt * 8)
                                 : MathF.Max(0, _cornerHint - c.Dt * 6);

        if (topLeft) _strip = 1;
        else if (!onStrip) _strip = MathF.Max(0, _strip - c.Dt * 4);
    }

    public void Draw(UiContext c)
    {
        DrawCorner(c);
        DrawStrip(c);
    }

    /// <summary>The Start thumbnail. Clicking it opens the board; the right
    /// button opens the list of everything an administrator actually wanted,
    /// which is where version 8 put it after being asked often enough.</summary>
    void DrawCorner(UiContext c)
    {
        if (_cornerHint < 0.02f || _shell.Start.IsOpen) return;

        byte alpha = (byte)(240 * _cornerHint);
        var thumb = new Rect(6, c.ScreenH - 82, 108, 74);

        c.R.FillRect(thumb, Color.Rgba(0x1C1C1C, alpha));
        c.R.DrawRect(thumb, Color.Rgba(0xFFFFFF, (byte)(alpha * 0.35f)));

        // A miniature of the board: four little tiles and the word.
        var mini = thumb.Deflate(10, 8, 10, 22);
        for (int i = 0; i < 4; i++)
        {
            var col = Theme.TileColors[i * 2 % Theme.TileColors.Length].WithAlpha(alpha);
            c.R.FillRect(new Rect(mini.X + (i % 2) * 22, mini.Y + (i / 2) * 18, 19, 15), col);
        }
        c.F.Small.Draw(c.R, L.T("start8.start"), thumb.X + 10, thumb.Bottom - 18,
                       Color.Rgba(0xFFFFFF, alpha));

        if (c.Clicked(thumb)) _shell.Start.Toggle(c);
        else if (c.RightClicked(thumb)) ShowPowerUserMenu(c, thumb);
    }

    /// <summary>Win+X: the flat list of administrative places, opened from the
    /// corner the Start button used to be in.</summary>
    public void ShowPowerUserMenu(UiContext c, Rect anchor)
    {
        var items = new List<MenuItem>
        {
            MenuItem.Of(L.T("taskbar.task_manager"), () => _shell.Launch(c, "taskmgr", null), IconId.Settings),
            MenuItem.Of(L.T("devmgr.title"), () => _shell.Launch(c, "devmgr", null), IconId.MyComputer),
            MenuItem.Of(L.T("start.control_panel"), () => _shell.Launch(c, "controlpanel", null),
                        IconId.ControlPanel),
            MenuItem.Of(L.T("pcs.title"), () => _shell.Launch(c, "pcsettings", null), IconId.PcSettings),
            MenuItem.Of(L.T("start.display_properties"), () => _shell.Launch(c, "display", null), IconId.Display),
            MenuItem.Sep(),
            MenuItem.Of(L.T("start.windows_explorer"), () => _shell.Launch(c, "mycomputer", null), IconId.Folder),
            MenuItem.Of(L.T("start.command_prompt"), () => _shell.Launch(c, "terminal", null), IconId.Terminal),
            MenuItem.Of(L.T("start.search"), () => _shell.Launch(c, "search", null), IconId.Search),
            MenuItem.Of(L.T("start.run"), () => _shell.Launch(c, "run", null), IconId.Run),
            MenuItem.Sep(),
            MenuItem.Sub(L.T("winx.shut_down_or_sign_out"), new List<MenuItem>
            {
                MenuItem.Of(L.T("start.log_off"), () => _shell.BeginLogOff(c), IconId.Logoff),
                MenuItem.Of(L.T("charm.sleep"), () => _shell.LockScreenNow(c), IconId.Lock),
                MenuItem.Of(L.T("charm.shutdown"), () => _shell.BeginShutdown(c), IconId.Shutdown),
                MenuItem.Of(L.T("charm.restart"), () => _shell.Restart(c), IconId.Power),
            }, IconId.Power),
            MenuItem.Of(L.T("start8.desktop"), () => _shell.Wm.MinimizeAll(c), IconId.Display),
        };

        _shell.Menus.Open(items, anchor.X + 2, anchor.Bottom - 4, this, c);
    }

    /// <summary>The running programs, down the left edge, newest at the top —
    /// the list Alt+Tab used to be and Win+Tab became.</summary>
    void DrawStrip(UiContext c)
    {
        if (_strip < 0.02f) return;

        byte alpha = (byte)(244 * _strip);
        float slide = (1 - _strip) * StripW;
        var strip = new Rect(-slide, 0, StripW, c.ScreenH);
        c.R.FillRect(strip, Color.Rgba(0x1C1C1C, alpha));

        var windows = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).Reverse().ToList();

        float y = 10;
        // The board itself is the first thing in the list, as it was.
        var startCard = new Rect(strip.X + 8, y, strip.W - 16, ThumbH);
        DrawCard(c, startCard, IconId.Tiles, L.T("start8.start"), alpha, false);
        if (c.Clicked(startCard)) { _strip = 0; _shell.Start.Toggle(c); }
        y += ThumbH + 8;

        foreach (var w in windows)
        {
            if (y + ThumbH > c.ScreenH - 10) break;
            var card = new Rect(strip.X + 8, y, strip.W - 16, ThumbH);
            DrawCard(c, card, w.Icon, w.TaskbarTitle, alpha, w == _shell.Wm.Focused);

            if (c.Clicked(card))
            {
                _carrying = w;
                _carriedFrom = c.MouseX;
            }
            else if (c.RightClicked(card))
            {
                _shell.Menus.Open(new List<MenuItem>
                {
                    MenuItem.Of(L.T("win.snap_left"), () => Snap(w, c, true), IconId.Tiles),
                    MenuItem.Of(L.T("win.snap_right"), () => Snap(w, c, false), IconId.Tiles),
                    MenuItem.Of(L.T("win.unsnap"), () => _shell.Wm.UnsnapImmersive(c),
                                IconId.None, null, _shell.Wm.Snapped != null),
                    MenuItem.Sep(),
                    MenuItem.Of(L.T("switch.close"), () => _shell.Wm.RequestClose(w, c)),
                }, card.Right + 4, card.Y, this, c);
            }

            y += ThumbH + 8;
        }

        UpdateCarry(c);

        if (windows.Count == 0)
        {
            c.R.PushClip(strip);
            c.F.Small.Draw(c.R, L.T("switch.nothing_running"), strip.X + 8, y + 6,
                           Color.Rgba(0xFFFFFF, (byte)(alpha * 0.6f)));
            c.R.PopClip();
        }

        if (_carrying == null &&
            (c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) && !c.MouseHandled)
            _strip = 0;
    }

    /// <summary>The card being carried out of the strip, and where it was
    /// picked up. Version 8 snapped a program by dragging its thumbnail out of
    /// this strip and letting it go against a side of the screen; a card let go
    /// where it was picked up is just a click, and raises the program.</summary>
    OsWindow _carrying;
    float _carriedFrom;

    void UpdateCarry(UiContext c)
    {
        if (_carrying == null) return;

        var w = _carrying;
        bool travelled = c.MouseX - _carriedFrom > StripW * 0.6f;

        if (c.In.IsDown(MouseButton.Left))
        {
            if (!travelled) return;

            c.Cursor = CursorShape.Move;
            c.MouseHandled = true;

            // Where it would land: the near third of the screen, or the far
            // third, and nothing in between.
            bool left = c.MouseX < c.ScreenW * 0.5f;
            float column = MathF.Round(c.ScreenW * 0.32f);
            var ghost = left ? new Rect(0, 0, column, c.ScreenH)
                             : new Rect(c.ScreenW - column, 0, column, c.ScreenH);

            c.R.FillRect(ghost, Color.Rgba(0xFFFFFF, 45));
            c.R.DrawRect(ghost, Color.Rgba(0xFFFFFF, 190), 2);
            return;
        }

        _carrying = null;
        _strip = 0;

        if (!travelled) { _shell.Wm.RestoreOrFocus(w, c); return; }

        Snap(w, c, c.MouseX < c.ScreenW * 0.5f);
    }

    /// <summary>Puts a program into the column beside whatever is showing. It
    /// has to be full screen to be snapped, so one that is not becomes one.</summary>
    void Snap(OsWindow w, UiContext c, bool left)
    {
        _strip = 0;
        if (!w.Immersive) _shell.Wm.ToggleImmersive(w, c);
        _shell.Wm.RestoreOrFocus(w, c);
        _shell.Wm.SnapImmersive(w, c, left);
    }

    void DrawCard(UiContext c, Rect r, IconId icon, string label, byte alpha, bool active)
    {
        bool hot = c.Hovering(r);
        c.R.FillRect(r, Color.Rgba(active ? 0x3A3A3A : 0x2A2A2A, alpha));
        if (hot || active)
            c.R.DrawRect(r, Color.Rgba(0xFFFFFF, (byte)(alpha * (hot ? 0.7f : 0.35f))));

        Icons.Draw(c.R, icon, new Rect(r.CenterX - 14, r.Y + 8, 28, 28));

        c.R.PushClip(r);
        c.F.Small.Draw(c.R, c.F.Small.Ellipsize(label, r.W - 8), r.X + 5, r.Bottom - 18,
                       Color.Rgba(0xFFFFFF, alpha));
        c.R.PopClip();
    }
}
