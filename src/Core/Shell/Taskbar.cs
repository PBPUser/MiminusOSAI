using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>A tray balloon — the "Сервисное сообщение" popup from part 1.</summary>
public sealed class Balloon
{
    public string Title;
    public string Text;
    public IconId Icon = IconId.DlgWarning;
    public double Shown;
    public double Duration = 9;
    public Action OnClick;
}

/// <summary>The taskbar: Start button, quick launch, window buttons, and the
/// notification area with its clock, language indicator and balloons.</summary>
public sealed class Taskbar
{
    readonly ShellHost _shell;
    readonly List<Balloon> _balloons = new();

    public bool StartOpen;
    double _startClosedAt = -1;

    public Taskbar(ShellHost shell) => _shell = shell;

    public void Notify(Balloon b, UiContext c)
    {
        b.Shown = c.Time;
        _balloons.Add(b);
        c.Sound(Sfx.Balloon, 0.8f);
    }

    /// <summary>How far the bar has slid off the bottom, 0 (out) to 1 (hidden).
    /// Animated so auto-hide reads as movement rather than a jump.</summary>
    float _hidden;

    /// <summary>Whether the chevron has been used to show the hidden icons.</summary>
    bool _trayExpanded;

    /// <summary>True while an auto-hidden bar is out of the way.</summary>
    public bool Retracted => _hidden > 0.5f;

    /// <summary>Height windows must leave clear: nothing, when the bar is
    /// hiding itself.</summary>
    public float Reserve(UiContext c)
        => _shell.Settings.AutoHideTaskbar ? 0 : c.Theme.TaskbarHeight;

    public Rect Bounds(UiContext c)
    {
        float h = c.Theme.TaskbarHeight;
        // Two pixels stay on screen when hidden, which is the strip the pointer
        // has to find — the same trick the original uses.
        return new Rect(0, c.ScreenH - h + _hidden * (h - 2), c.ScreenW, h);
    }

    /// <summary>Slides the bar in and out. The pointer being near the bottom
    /// edge, or the Start menu being open, keeps it out.</summary>
    void UpdateAutoHide(UiContext c)
    {
        if (!_shell.Settings.AutoHideTaskbar)
        {
            _hidden = 0;
            return;
        }

        float h = c.Theme.TaskbarHeight;
        bool wanted = StartOpen
                   || _balloons.Count > 0
                   || c.MouseY >= c.ScreenH - (Retracted ? 3 : h);

        float target = wanted ? 0 : 1;
        _hidden += Math.Clamp(target - _hidden, -1f, 1f) * MathF.Min(1, c.Dt * 9);
        if (MathF.Abs(target - _hidden) < 0.01f) _hidden = target;
    }

    public void Draw(UiContext c)
    {
        var t = c.Theme;
        UpdateAutoHide(c);
        var bar = Bounds(c);

        // Background.
        c.R.FillRectV(new Rect(bar.X, bar.Y, bar.W, bar.H * 0.5f), t.TaskbarTop, t.TaskbarMid);
        c.R.FillRectV(new Rect(bar.X, bar.Y + bar.H * 0.5f, bar.W, bar.H * 0.5f), t.TaskbarMid, t.TaskbarBottom);
        c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 1), t.TaskbarEdge);
        if (t.Id != ThemeId.Seven && t.Id != ThemeId.Classic)
            c.R.FillRect(new Rect(bar.X, bar.Y + 1, bar.W, 1), Color.Rgba(0xFFFFFF, 90));

        float x = bar.X + 2;

        // ---- Start button ------------------------------------------------
        float startW = t.Id == ThemeId.Seven ? bar.H - 2 : 92;
        var startRect = new Rect(x, bar.Y + 2, startW, bar.H - 4);
        DrawStartButton(c, startRect);
        x = startRect.Right + 6;

        // ---- quick launch ------------------------------------------------
        // An unlocked bar shows the ridged handle you would drag it by.
        if (!_shell.Settings.LockTaskbar) x += DrawGripper(c, bar, x);

        if (_shell.Settings.ShowQuickLaunch)
        {
        var quick = new (IconId icon, string app, string tip)[]
        {
            (IconId.Firefox, "browser", "taskbar.firefox_web_browser"),
            (IconId.Notepad, "notepad", "taskbar.notepad"),
            (IconId.MediaPlayer, "player", "taskbar.media_player"),
            (IconId.Display, "display", "taskbar.display_properties"),
        };
        float qs = MathF.Min(bar.H - 10, 22);
        foreach (var (icon, app, tip) in quick)
        {
            var qr = new Rect(x, bar.Y + (bar.H - qs) * 0.5f, qs, qs);
            if (c.Hovering(qr)) c.R.RoundedRect(qr.Inflate(2), 2, Color.Rgba(0xFFFFFF, 45));
            Icons.Draw(c.R, icon, qr);
            c.Tooltip(qr, L.T(tip));
            if (c.Clicked(qr)) _shell.Launch(c, app, null);
            x += qs + 4;
        }
        // Separator
        c.R.FillRect(new Rect(x, bar.Y + 5, 1, bar.H - 10), Color.Rgba(0x000000, 60));
        c.R.FillRect(new Rect(x + 1, bar.Y + 5, 1, bar.H - 10), Color.Rgba(0xFFFFFF, 60));
        x += 7;
        }

        if (!_shell.Settings.LockTaskbar) x += DrawGripper(c, bar, x);

        // ---- notification area (measured first, buttons fill the rest) ----
        float trayW = DrawTray(c, bar, measureOnly: true);
        float taskAreaRight = bar.Right - trayW - 6;

        DrawTaskButtons(c, new Rect(x, bar.Y + 2, MathF.Max(0, taskAreaRight - x), bar.H - 4));
        DrawTray(c, bar, measureOnly: false);

        // Right-click on empty taskbar space.
        if (c.RightClicked(bar)) ShowTaskbarMenu(c);

        DrawBalloons(c, bar);
    }

    /// <summary>The two ridges XP puts at the start of each unlocked band.</summary>
    static float DrawGripper(UiContext c, Rect bar, float x)
    {
        for (int i = 0; i < 2; i++)
        {
            var line = new Rect(x + i * 3, bar.Y + 6, 1, bar.H - 12);
            c.R.FillRect(line, Color.Rgba(0xFFFFFF, 110));
            c.R.FillRect(new Rect(line.X + 1, line.Y, 1, line.H), Color.Rgba(0x000000, 60));
        }
        return 10;
    }

    void DrawStartButton(UiContext c, Rect r)
    {
        var t = c.Theme;
        bool hover = c.Hovering(r);
        bool held = StartOpen || (hover && c.In.IsDown(MouseButton.Left));

        if (t.Id == ThemeId.Seven)
        {
            // Round orb.
            float rad = r.H * 0.5f;
            c.R.FillCircle(r.CenterX, r.CenterY, rad, Color.Rgba(0x000000, 90));
            c.R.FillCircle(r.CenterX, r.CenterY, rad - 1,
                held ? t.StartBottom : hover ? t.StartMid.Shade(1.25f) : t.StartMid);
            c.R.FillCircle(r.CenterX, r.CenterY - rad * 0.25f, rad * 0.62f, Color.Rgba(0xFFFFFF, 70));

            // Four-pane glyph.
            float s = rad * 0.30f, g = s * 0.22f;
            Color[] cols = { Color.Rgb(0xF25022), Color.Rgb(0x7FBA00), Color.Rgb(0x00A4EF), Color.Rgb(0xFFB900) };
            for (int i = 0; i < 4; i++)
            {
                float ox = (i % 2 == 0 ? -1 : 1) * (s * 0.5f + g * 0.5f);
                float oy = (i < 2 ? -1 : 1) * (s * 0.5f + g * 0.5f);
                c.R.FillRect(new Rect(r.CenterX + ox - s * 0.5f, r.CenterY + oy - s * 0.5f, s, s), cols[i]);
            }
        }
        else if (t.Id == ThemeId.Classic)
        {
            c.R.FillRect(r, t.Face);
            W.Bevel(c, r, !held);
            Icons.Draw(c.R, IconId.Flag, new Rect(r.X + 5, r.CenterY - 8, 16, 16));
            c.F.UiBold.Draw(c.R, L.T("taskbar.start"), r.X + 25, r.CenterY - c.F.UiBold.Height * 0.5f, Color.Black);
        }
        else
        {
            // Luna's green pill, rounded hard on the right.
            Color top = held ? t.StartBottom : hover ? t.StartTop.Shade(1.15f) : t.StartTop;
            Color bot = held ? t.StartMid : t.StartBottom;
            c.R.RoundedRectV(r, r.H * 0.46f, top, bot, t.StartEdge, 1);
            c.R.FillRect(new Rect(r.X, r.Y + 1, r.W * 0.55f, r.H * 0.42f), Color.Transparent);
            c.R.RoundedRectV(new Rect(r.X + 3, r.Y + 2, r.W - 6, r.H * 0.42f), r.H * 0.24f,
                             Color.Rgba(0xFFFFFF, 95), Color.Rgba(0xFFFFFF, 10));

            var flag = new Rect(r.X + 9, r.CenterY - 9, 18, 18);
            Icons.Draw(c.R, IconId.Flag, flag);

            string label = L.T("taskbar.start_2");
            c.F.Caption.Draw(c.R, label, flag.Right + 6, r.CenterY - c.F.Caption.Height * 0.5f + 1,
                             Color.Rgba(0x0A2A08, 150));
            c.F.Caption.Draw(c.R, label, flag.Right + 5, r.CenterY - c.F.Caption.Height * 0.5f, Color.White);
        }

        c.Tooltip(r, L.T("taskbar.click_here_to_begin"));

        if (c.Clicked(r))
        {
            // Ignore the click that just dismissed the menu.
            if (c.Time - _startClosedAt > 0.15) StartOpen = !StartOpen;
            c.Sound(StartOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.6f);
        }
    }

    /// <summary>Opens or closes the Start menu, as the Start button and the
    /// Windows key both do.</summary>
    public void ToggleStart(UiContext c)
    {
        StartOpen = !StartOpen;
        if (!StartOpen) _startClosedAt = c.Time;
        c.Sound(StartOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.6f);
    }

    public void CloseStart(UiContext c)
    {
        if (!StartOpen) return;
        StartOpen = false;
        _startClosedAt = c.Time;
    }

    void DrawTaskButtons(UiContext c, Rect area)
    {
        var t = c.Theme;
        var wins = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();
        if (wins.Count == 0 || area.W <= 20) return;

        float gap = 3;
        float maxW = t.Id == ThemeId.Seven ? 168 : 160;

        // Grouping only kicks in once the buttons would be too narrow to read,
        // which is when XP does it too.
        var groups = _shell.Settings.GroupSimilar && (area.W / wins.Count) < 90
            ? wins.GroupBy(w => w.ProgramId ?? w.Title).Where(g => g.Count() > 1).ToList()
            : null;

        if (groups is { Count: > 0 })
        {
            DrawGroupedButtons(c, area, wins, groups, gap, maxW);
            return;
        }

        float w = MathF.Min(maxW, (area.W - gap * (wins.Count - 1)) / wins.Count);
        if (w < 26) w = 26;

        float x = area.X;
        foreach (var win in wins)
        {
            if (x + w > area.Right + 1) break;
            var r = new Rect(x, area.Y, w - (t.Id == ThemeId.Seven ? 2 : 0), area.H);
            bool active = _shell.Wm.Focused == win && win.State != WindowState.Minimized;
            bool hover = c.Hovering(r);

            if (t.Id == ThemeId.Seven)
            {
                Color face = active ? t.TaskButtonActive : hover ? Color.Rgba(0xFFFFFF, 45) : t.TaskButtonFace;
                c.R.RoundedRect(r, 3, face, t.TaskButtonBorder, 1);
                if (active) c.R.FillRect(new Rect(r.X + 2, r.Bottom - 3, r.W - 4, 2), Color.Rgba(0x7FD0F5, 220));
            }
            else if (t.Id == ThemeId.Classic)
            {
                c.R.FillRect(r, t.TaskButtonFace);
                W.Bevel(c, r, !active);
            }
            else
            {
                Color top = active ? t.TaskButtonActive : hover ? t.TaskButtonFace.Shade(1.2f) : t.TaskButtonFace;
                Color bot = active ? t.TaskButtonActive.Shade(1.15f) : t.TaskButtonFace.Shade(0.82f);
                c.R.RoundedRectV(r, 3, top, bot, t.TaskButtonBorder, 1);
                if (!active)
                    c.R.FillRect(new Rect(r.X + 2, r.Y + 1, r.W - 4, 1), Color.Rgba(0xFFFFFF, 110));
            }

            float pad = 4;
            var ic = new Rect(r.X + pad, r.CenterY - 8, 16, 16);
            Icons.Draw(c.R, win.Icon, ic);

            var textArea = new Rect(ic.Right + 4, r.Y, r.Right - ic.Right - 8, r.H);
            if (textArea.W > 8)
            {
                c.R.PushClip(textArea);
                string label = c.F.Ui.Ellipsize(win.TaskbarTitle, textArea.W);
                c.F.Ui.Draw(c.R, label, textArea.X, r.CenterY - c.F.Ui.Height * 0.5f, t.TaskbarText);
                c.R.PopClip();
            }

            c.Tooltip(r, win.TaskbarTitle);

            if (c.Clicked(r)) { _shell.Wm.RestoreOrFocus(win, c); CloseStart(c); }
            else if (c.RightClicked(r)) ShowWindowMenu(c, win);

            x += w + gap;
        }
    }

    /// <summary>Draws one button per program instead of per window, the way the
    /// taskbar does once it runs short of room. The button opens a menu of the
    /// windows it stands for.</summary>
    void DrawGroupedButtons(UiContext c, Rect area, List<OsWindow> wins,
                            List<IGrouping<string, OsWindow>> groups, float gap, float maxW)
    {
        var t = c.Theme;

        // Grouped programs first, then whatever was left ungrouped.
        var buttons = new List<(string label, IconId icon, List<OsWindow> windows)>();
        foreach (var group in groups)
            buttons.Add((group.First().TaskbarTitle, group.First().Icon, group.ToList()));
        foreach (var win in wins.Where(w => !groups.Any(g => g.Contains(w))))
            buttons.Add((win.TaskbarTitle, win.Icon, new List<OsWindow> { win }));

        float w = MathF.Min(maxW, (area.W - gap * (buttons.Count - 1)) / buttons.Count);
        if (w < 26) w = 26;

        float x = area.X;
        foreach (var (label, icon, windows) in buttons)
        {
            if (x + w > area.Right + 1) break;
            var r = new Rect(x, area.Y, w, area.H);
            bool active = windows.Contains(_shell.Wm.Focused);
            bool hover = c.Hovering(r);

            Color top = active ? t.TaskButtonActive : hover ? t.TaskButtonFace.Shade(1.2f) : t.TaskButtonFace;
            Color bot = active ? t.TaskButtonActive.Shade(1.15f) : t.TaskButtonFace.Shade(0.82f);
            c.R.RoundedRectV(r, 3, top, bot, t.TaskButtonBorder, 1);

            var ic = new Rect(r.X + 4, r.CenterY - 8, 16, 16);
            Icons.Draw(c.R, icon, ic);

            // The count sits where the window title would, as XP writes it.
            string text = windows.Count > 1 ? $"{windows.Count}  {label}" : label;
            var textArea = new Rect(ic.Right + 4, r.Y, r.Right - ic.Right - 8, r.H);
            if (textArea.W > 8)
            {
                c.R.PushClip(textArea);
                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(text, textArea.W), textArea.X,
                            r.CenterY - c.F.Ui.Height * 0.5f, t.TaskbarText);
                c.R.PopClip();
            }

            c.Tooltip(r, text);

            if (c.Clicked(r))
            {
                if (windows.Count == 1) { _shell.Wm.RestoreOrFocus(windows[0], c); CloseStart(c); }
                else
                {
                    var group = windows;
                    _shell.Menus.Open(group.Select(win => MenuItem.Of(win.TaskbarTitle,
                        () => _shell.Wm.RestoreOrFocus(win, c), win.Icon)).ToList(),
                        r.X, r.Y, this, c);
                }
            }

            x += w + gap;
        }
    }

    /// <summary>Draws the notification area. When <paramref name="measureOnly"/> is
    /// set nothing is painted and only the required width is returned, so the task
    /// button strip knows where to stop.</summary>
    float DrawTray(UiContext c, Rect bar, bool measureOnly)
    {
        var t = c.Theme;
        var settings = _shell.Settings;
        string clock = L.Time(_shell.Now);
        float clockW = settings.ShowClock ? c.F.Ui.Measure(clock) + 12 : 0;

        // Hidden icons collapse to a single chevron until it is clicked.
        bool collapsed = settings.HideInactiveIcons && !_trayExpanded;
        int iconCount = collapsed ? 1 : 3;
        float iconArea = iconCount * 18 + 8 + (settings.HideInactiveIcons ? 14 : 0);
        float langW = c.F.Ui.Measure("RU") + 12;
        float total = clockW + iconArea + langW + 14;

        if (measureOnly) return total;

        var tray = new Rect(bar.Right - total, bar.Y + 2, total - 2, bar.H - 4);
        if (t.Id != ThemeId.Classic)
        {
            c.R.FillRectV(tray, t.TrayBack, t.TrayBack.Shade(0.85f));
            c.R.FillRect(new Rect(tray.X, tray.Y, 1, tray.H), t.TrayEdge);
        }
        else W.Bevel(c, tray, false);

        float x = tray.X + 6;

        // Language indicator — clicking it flips the whole UI language.
        var langRect = new Rect(x, tray.Y + 2, langW - 4, tray.H - 4);
        bool langHover = c.Hovering(langRect);
        if (langHover) c.R.RoundedRect(langRect, 2, Color.Rgba(0xFFFFFF, 55));
        c.R.DrawRect(langRect, Color.Rgba(0xFFFFFF, 90));
        c.F.UiBold.DrawCentered(c.R, L.TrayTag, langRect, t.TaskbarText);
        c.Tooltip(langRect, L.T("taskbar.input_language_click_to_switch"));
        if (c.Clicked(langRect))
        {
            L.Toggle();
            c.Sound(Sfx.Click, 0.6f);
        }
        x = langRect.Right + 6;

        // The chevron that shows or hides the rest of the icons.
        if (settings.HideInactiveIcons)
        {
            var chevron = new Rect(x, tray.CenterY - 8, 12, 16);
            if (c.Hovering(chevron)) c.R.RoundedRect(chevron, 2, Color.Rgba(0xFFFFFF, 55));
            W.Arrow(c, chevron, _trayExpanded ? 1 : 3, t.TaskbarText);
            c.Tooltip(chevron, L.T(_trayExpanded ? "tray.hide_icons" : "tray.show_hidden_icons"));
            if (c.Clicked(chevron)) { _trayExpanded = !_trayExpanded; c.Sound(Sfx.Click, 0.5f); }
            x += 14;
        }

        // Status icons.
        var trayIcons = new (IconId id, string tip, Action click)[]
        {
            (IconId.TrayNetwork, "tray.local_area_connection",
                () => _shell.ShowNetworkBalloon(c)),
            (IconId.Volume, "tray.volume", () => _shell.ToggleMute(c)),
            (IconId.Shield, "tray.security_center",
                () => _shell.Launch(c, "notepad", _shell.Fs.AntivirusFile)),
        };
        foreach (var (id, tip, click) in collapsed ? trayIcons[..1] : trayIcons)
        {
            var ir = new Rect(x, tray.CenterY - 8, 16, 16);
            Icons.Draw(c.R, id, ir);
            if (id == IconId.Volume && _shell.Audio.Muted)
                c.R.Line(ir.X + 2, ir.Y + 2, ir.Right - 2, ir.Bottom - 2, Color.Rgb(0xE04040), 2f);
            c.Tooltip(ir, L.T(tip));
            if (c.Clicked(ir)) click();
            x += 18;
        }

        if (settings.ShowClock)
        {
            var clockRect = new Rect(tray.Right - clockW, tray.Y, clockW, tray.H);
            c.F.Ui.DrawCentered(c.R, clock, clockRect, t.TaskbarText);
            c.Tooltip(clockRect, L.LongDate(_shell.Now));
            if (c.Clicked(clockRect)) _shell.Launch(c, "clock", null);
        }

        return total;
    }

    void DrawBalloons(UiContext c, Rect bar)
    {
        for (int i = _balloons.Count - 1; i >= 0; i--)
        {
            var b = _balloons[i];
            double age = c.Time - b.Shown;
            if (age > b.Duration) { _balloons.RemoveAt(i); continue; }

            float w = 268;
            var lines = c.F.Ui.Wrap(b.Text, w - 46);
            float h = 30 + lines.Count * (c.F.Ui.Height + 2) + 10;

            // Slide up on appear, and back down as it expires.
            float appear = (float)Math.Clamp(age / 0.28, 0, 1);
            float fade = (float)Math.Clamp((b.Duration - age) / 0.4, 0, 1);
            float slide = (1 - appear) * 16;

            var r = new Rect(c.ScreenW - w - 14, bar.Y - h - 12 + slide, w, h);
            byte alpha = (byte)(255 * MathF.Min(appear, fade));

            // Pointer tail toward the tray.
            c.R.FillRect(r.Offset(3, 3), Color.Rgba(0x000000, (byte)(alpha / 4)));
            c.R.RoundedRect(r, 5, Color.Rgba(0xFFFFE1, alpha), Color.Rgba(0x000000, alpha), 1);
            c.R.FillTriangle(r.Right - 42, r.Bottom, r.Right - 22, r.Bottom,
                             r.Right - 30, r.Bottom + 11, Color.Rgba(0xFFFFE1, alpha));

            Icons.Draw(c.R, b.Icon, new Rect(r.X + 8, r.Y + 8, 16, 16));
            c.F.UiBold.Draw(c.R, b.Title, r.X + 30, r.Y + 8, Color.Rgba(0x000080, alpha));

            float ty = r.Y + 28;
            foreach (string line in lines)
            {
                c.F.Ui.Draw(c.R, line, r.X + 30, ty, Color.Rgba(0x000000, alpha));
                ty += c.F.Ui.Height + 2;
            }

            // Close box.
            var close = new Rect(r.Right - 18, r.Y + 6, 12, 12);
            if (c.Hovering(close)) c.R.RoundedRect(close, 2, Color.Rgba(0x000000, 30));
            c.R.Line(close.X + 3, close.Y + 3, close.Right - 3, close.Bottom - 3, Color.Rgba(0x404040, alpha), 1.4f);
            c.R.Line(close.Right - 3, close.Y + 3, close.X + 3, close.Bottom - 3, Color.Rgba(0x404040, alpha), 1.4f);

            if (c.Clicked(close)) _balloons.RemoveAt(i);
            else if (c.Clicked(r)) { b.OnClick?.Invoke(); _balloons.RemoveAt(i); }
        }
    }

    // ---- menus -----------------------------------------------------------

    void ShowWindowMenu(UiContext c, OsWindow win)
    {
        var items = new List<MenuItem>
        {
            MenuItem.Of(L.T("taskbar.restore"), () => _shell.Wm.RestoreOrFocus(win, c),
                        enabled: win.State != WindowState.Normal),
            MenuItem.Of(L.T("taskbar.move"), null, enabled: false),
            MenuItem.Of(L.T("taskbar.size"), null, enabled: false),
            MenuItem.Of(L.T("taskbar.minimize"), () => _shell.Wm.Minimize(win, c), enabled: win.Minimizable),
            MenuItem.Of(L.T("taskbar.maximize"), () => _shell.Wm.ToggleMaximize(win, c), enabled: win.Maximizable),
            MenuItem.Sep(),
            MenuItem.Of(L.T("taskbar.close"), () => _shell.Wm.RequestClose(win, c), shortcut: "Alt+F4"),
        };
        _shell.Menus.Open(items, c.MouseX, c.MouseY, this, c);
    }

    void ShowTaskbarMenu(UiContext c)
    {
        var items = new List<MenuItem>
        {
            MenuItem.Of(L.T("taskbar.cascade_windows"), () => _shell.CascadeWindows(c)),
            MenuItem.Of(L.T("taskbar.tile_windows_horizontally"), () => _shell.TileWindows(c, false)),
            MenuItem.Of(L.T("taskbar.tile_windows_vertically"), () => _shell.TileWindows(c, true)),
            MenuItem.Of(L.T("taskbar.show_the_desktop"), () => _shell.Wm.MinimizeAll(c)),
            MenuItem.Sep(),
            MenuItem.Of(L.T("taskbar.task_manager"), () => _shell.Launch(c, "taskmgr", null), IconId.Settings),
            MenuItem.Sep(),
            new MenuItem
            {
                Text = L.T("taskbar.lock_the_taskbar"),
                Checked = _shell.Settings.LockTaskbar,
                Click = () => _shell.Settings.LockTaskbar = !_shell.Settings.LockTaskbar,
            },
            MenuItem.Sep(),
            MenuItem.Of(L.T("taskbar.properties"), () => _shell.Launch(c, "taskbarprops", null),
                        IconId.Settings),
        };
        _shell.Menus.Open(items, c.MouseX, c.MouseY, this, c);
    }
}
