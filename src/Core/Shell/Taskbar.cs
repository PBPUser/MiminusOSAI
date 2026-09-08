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

/// <summary>The taskbar, in the shape Windows 7 gave it — the superbar.
///
/// The quick-launch strip and the window buttons are the same row now: a
/// program is pinned there whether it is running or not, and running gives it a
/// lit tile rather than a place of its own. Several windows of one program stack
/// behind one button, hovering a button raises a preview of what is behind it,
/// and the far right end of the bar is the sliver that shows the desktop.
///
/// The taskbar properties still decide the two things they always did:
/// «Группировать сходные кнопки» is the combine switch — off, the bar goes back
/// to one labelled button per window — and «Отображать панель быстрого запуска»
/// is now whether the pinned programs are shown at all.
///
/// The colours are still the theme's, so this is a Luna superbar under XP and a
/// flat one under «Миминус 8»; only the arrangement is seven's.</summary>
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

    /// <summary>Whether the chevron's panel of hidden icons is out.</summary>
    bool _trayExpanded;

    /// <summary>The three things that live in the notification area. Which of
    /// them are behind the chevron is the user's business — seven let them be
    /// dragged in and out, and so does this.</summary>
    enum TrayIcon { Network, Volume, Shield }

    static readonly TrayIcon[] AllTrayIcons = { TrayIcon.Network, TrayIcon.Volume, TrayIcon.Shield };

    static IconId IconOf(TrayIcon id) => id switch
    {
        TrayIcon.Network => IconId.TrayNetwork,
        TrayIcon.Volume => IconId.Volume,
        _ => IconId.Shield,
    };

    static string TipOf(TrayIcon id) => id switch
    {
        TrayIcon.Network => "tray.local_area_connection",
        TrayIcon.Volume => "tray.volume",
        _ => "tray.security_center",
    };

    bool Hidden(TrayIcon id) => _shell.Settings.HiddenTrayIcons.Contains(id.ToString());

    IEnumerable<TrayIcon> Shown => AllTrayIcons.Where(i => !Hidden(i));
    IEnumerable<TrayIcon> Tucked => AllTrayIcons.Where(Hidden);

    /// <summary>The icon being carried between the tray and the panel behind
    /// the chevron, which is the whole of "add ability to move it".</summary>
    TrayIcon? _carrying;
    bool _carryMoved;
    float _carryFromX, _carryFromY;

    const float TrayPopupW = 108;

    /// <summary>Where the panel of hidden icons sits, reserved by the shell so
    /// a click in it is not taken by whatever is underneath.</summary>
    public Rect TrayPopupBounds(UiContext c)
    {
        if (!_trayExpanded) return default;

        int rows = Math.Max(1, (Tucked.Count() + 2) / 3);
        return Flyout(c, _chevron, TrayPopupW, rows * 30 + 34);
    }

    Rect _chevron;

    /// <summary>The volume panel that drops out of the speaker, and the icon it
    /// hangs from — tracked every frame so it follows a resize.</summary>
    bool _volumeOpen;
    bool _draggingVolume;
    Rect _speaker;
    float _lastBlip = -1;

    /// <summary>Size of the panel. Small, and taller than it is wide, because
    /// the slider in it stands up.</summary>
    const float VolumeW = 74, VolumeH = 158;

    /// <summary>Where the panel sits, or an empty rect when it is closed. The
    /// shell asks for this before windows run their input, so a click on the
    /// panel is not stolen by whatever is underneath it.</summary>
    public Rect VolumeBounds(UiContext c)
        => _volumeOpen ? Flyout(c, _speaker, VolumeW, VolumeH) : default;

    public void CloseVolume() => _volumeOpen = false;

    // ---- the other two flyouts ------------------------------------------

    /// <summary>The clock panel — the calendar that drops out of the corner —
    /// and the input-method list, which are the other two things the tray can
    /// have open. Only one of the three is ever out at a time.</summary>
    bool _clockOpen, _langOpen;
    Rect _clockAnchor, _langAnchor;

    const float ClockW = 300, ClockH = 330;
    const float LangW = 268;

    public Rect ClockBounds(UiContext c)
        => _clockOpen ? Flyout(c, _clockAnchor, ClockW, ClockH) : default;

    public Rect LanguageBounds(UiContext c)
        => _langOpen ? Flyout(c, _langAnchor, LangW, 44 + Languages.Length * 42 + 34) : default;

    /// <summary>Closes whatever is out. Opening one closes the others, because
    /// the tray only ever showed one at a time.</summary>
    void CloseFlyouts()
    {
        _volumeOpen = false;
        _clockOpen = false;
        _langOpen = false;
        _trayExpanded = false;
    }

    /// <summary>True while an auto-hidden bar is out of the way.</summary>
    public bool Retracted => _hidden > 0.5f;

    // ---- which edge the bar is against ---------------------------------------
    //
    // The bar has always been able to live on any of the four sides, and the
    // arrangement changes with it rather than being rotated: standing up, the
    // Start button is a square at the top, the buttons stack down the middle
    // and the clock goes under the tray icons in two lines. Everything else —
    // the flyouts, the previews, the balloons — comes out of whichever side of
    // the bar faces the screen.

    public TaskbarEdge Edge => _shell.Settings.TaskbarEdge;

    /// <summary>True when the bar is standing up the side of the screen.</summary>
    public bool Vertical => Edge is TaskbarEdge.Left or TaskbarEdge.Right;

    /// <summary>How wide a standing bar is. Wide enough for a square button and
    /// a clock in two lines, and no wider — which is what the real one settles
    /// on as well.</summary>
    const float VerticalWidth = 74;

    /// <summary>How thick the bar is across the edge it is on.</summary>
    public float Thickness(UiContext c) => Thickness(c.Theme);

    public float Thickness(Theme t)
        => (Vertical ? VerticalWidth : t.TaskbarHeight) * SizeScale;

    /// <summary>How much bigger or smaller the bar is than the theme meant it
    /// to be. Windows called the same setting «Использовать маленькие значки»
    /// and only offered two; three is more useful and no harder.</summary>
    float SizeScale => _shell.Settings.TaskbarSize switch
    {
        0 => 0.72f,
        2 => 1.34f,
        _ => 1f,
    };

    /// <summary>How much the bar takes off the foot of the screen, which is
    /// nothing at all unless the foot is the edge it is on. Used where a layout
    /// only cares about the bottom — the desktop's rows of icons, say.</summary>
    public float BottomInset(Theme t)
        => Edge == TaskbarEdge.Bottom && !_shell.Settings.AutoHideTaskbar ? Thickness(t) : 0;

    /// <summary>What the bar takes out of the screen: nothing, when it is
    /// hiding itself.</summary>
    public float Reserve(UiContext c) => Reserve(c.Theme);

    public float Reserve(Theme t)
        => _shell.Settings.AutoHideTaskbar ? 0 : Thickness(t);

    /// <summary>The screen with the bar taken out of it — where the desktop
    /// draws, where a maximised window goes, and where a dialog centres.</summary>
    public Rect WorkArea(UiContext c)
        => WorkAreaOf(c, _shell.Displays.Primary(c.ScreenW, c.ScreenH));

    /// <summary>One monitor with the bar taken out of it. Only the monitor the
    /// bar is on loses anything; the other is free from edge to edge, which is
    /// the whole reason people put the taskbar on the small screen.</summary>
    public Rect WorkAreaOf(UiContext c, Display m)
    {
        var r = m.Bounds;
        if (!m.Primary) return r;

        float k = Reserve(c);
        return Edge switch
        {
            TaskbarEdge.Top => new Rect(r.X, r.Y + k, r.W, r.H - k),
            TaskbarEdge.Left => new Rect(r.X + k, r.Y, r.W - k, r.H),
            TaskbarEdge.Right => new Rect(r.X, r.Y, r.W - k, r.H),
            _ => new Rect(r.X, r.Y, r.W, r.H - k),
        };
    }

    /// <summary>The work area of whichever monitor a rectangle is mostly on.</summary>
    public Rect WorkAreaFor(UiContext c, Rect window)
        => WorkAreaOf(c, _shell.Displays.Of(c.ScreenW, c.ScreenH, window));

    public Rect Bounds(UiContext c)
    {
        float k = Thickness(c);

        // Two pixels stay on screen when hidden, which is the strip the pointer
        // has to find — the same trick the original uses.
        float off = _hidden * (k - 2);

        // The bar belongs to the primary monitor, and to that one only: that is
        // what "primary" means, and it is why people care which one it is.
        var m = _shell.Displays.Primary(c.ScreenW, c.ScreenH).Bounds;

        return Edge switch
        {
            TaskbarEdge.Top => new Rect(m.X, m.Y - off, m.W, k),
            TaskbarEdge.Left => new Rect(m.X - off, m.Y, k, m.H),
            TaskbarEdge.Right => new Rect(m.Right - k + off, m.Y, k, m.H),
            _ => new Rect(m.X, m.Bottom - k + off, m.W, k),
        };
    }

    /// <summary>Slides the bar in and out. The pointer being near the bar's own
    /// edge, or the Start menu being open, keeps it out.</summary>
    void UpdateAutoHide(UiContext c)
    {
        if (!_shell.Settings.AutoHideTaskbar)
        {
            _hidden = 0;
            return;
        }

        float k = Thickness(c);
        float reach = Retracted ? 3 : k;

        bool near = Edge switch
        {
            TaskbarEdge.Top => c.MouseY <= reach,
            TaskbarEdge.Left => c.MouseX <= reach,
            TaskbarEdge.Right => c.MouseX >= c.ScreenW - reach,
            _ => c.MouseY >= c.ScreenH - reach,
        };

        bool wanted = StartOpen || _balloons.Count > 0 || near;

        float target = wanted ? 0 : 1;
        _hidden += Math.Clamp(target - _hidden, -1f, 1f) * MathF.Min(1, c.Dt * 9);
        if (MathF.Abs(target - _hidden) < 0.01f) _hidden = target;
    }

    /// <summary>Where a panel hanging off the bar starts, and which way it
    /// grows: the side of the bar that faces the screen.</summary>
    Rect Flyout(UiContext c, Rect anchor, float w, float h)
    {
        var bar = Bounds(c);
        return Edge switch
        {
            TaskbarEdge.Top => new Rect(Math.Clamp(anchor.CenterX - w * 0.5f, 2, c.ScreenW - w - 2),
                                        bar.Bottom + 2, w, h),
            TaskbarEdge.Left => new Rect(bar.Right + 2,
                                         Math.Clamp(anchor.CenterY - h * 0.5f, 2, c.ScreenH - h - 2),
                                         w, h),
            TaskbarEdge.Right => new Rect(bar.X - w - 2,
                                          Math.Clamp(anchor.CenterY - h * 0.5f, 2, c.ScreenH - h - 2),
                                          w, h),
            _ => new Rect(Math.Clamp(anchor.CenterX - w * 0.5f, 2, c.ScreenW - w - 2),
                          bar.Y - h - 2, w, h),
        };
    }

    public void Draw(UiContext c)
    {
        UpdateAutoHide(c);
        var bar = Bounds(c);

        DrawBarBackground(c, bar);

        if (Vertical) LayOutStanding(c, bar);
        else LayOutLying(c, bar);

        // Dragging the bar from one edge to another, which is what the grab
        // handles on an unlocked bar were always for.
        UpdateEdgeDrag(c, bar);

        // Right-click on empty taskbar space.
        if (c.RightClicked(bar)) ShowTaskbarMenu(c);

        DrawPreview(c, bar);
        DrawVolumePanel(c);
        DrawTrayPopup(c);
        DrawClockPanel(c);
        DrawLanguagePanel(c);
        DrawBalloons(c, bar);
    }

    /// <summary>The bar itself. The gradient runs across the bar whichever way
    /// it lies, and the bright line is always on the side facing the screen.</summary>
    void DrawBarBackground(UiContext c, Rect bar)
    {
        var t = c.Theme;

        if (Vertical)
        {
            c.R.FillRectH(new Rect(bar.X, bar.Y, bar.W * 0.5f, bar.H), t.TaskbarTop, t.TaskbarMid);
            c.R.FillRectH(new Rect(bar.CenterX, bar.Y, bar.W * 0.5f, bar.H),
                          t.TaskbarMid, t.TaskbarBottom);

            float ex = Edge == TaskbarEdge.Left ? bar.Right - 1 : bar.X;
            c.R.FillRect(new Rect(ex, bar.Y, 1, bar.H), t.TaskbarEdge);
        }
        else
        {
            c.R.FillRectV(new Rect(bar.X, bar.Y, bar.W, bar.H * 0.5f), t.TaskbarTop, t.TaskbarMid);
            c.R.FillRectV(new Rect(bar.X, bar.Y + bar.H * 0.5f, bar.W, bar.H * 0.5f),
                          t.TaskbarMid, t.TaskbarBottom);

            float ey = Edge == TaskbarEdge.Top ? bar.Bottom - 1 : bar.Y;
            c.R.FillRect(new Rect(bar.X, ey, bar.W, 1), t.TaskbarEdge);
            if (!t.Flat && t.Id != ThemeId.Classic && Edge == TaskbarEdge.Bottom)
                c.R.FillRect(new Rect(bar.X, bar.Y + 1, bar.W, 1), Color.Rgba(0xFFFFFF, 90));
        }
    }

    /// <summary>The bar lying along the top or the bottom, which is the shape
    /// everything in it was drawn for.</summary>
    void LayOutLying(UiContext c, Rect bar)
    {
        var t = c.Theme;
        float x = bar.X + 2;

        float startW = t.Flat ? bar.H - 2 : 92;
        var startRect = new Rect(x, bar.Y + 2, startW, bar.H - 4);
        DrawStartButton(c, startRect);
        x = startRect.Right + 6;

        // An unlocked bar shows the ridged handle you would drag it by.
        if (!_shell.Settings.LockTaskbar) x += DrawGripper(c, bar, x);

        // ---- notification area (measured first, buttons fill the rest) ----
        float trayW = DrawTray(c, bar, measureOnly: true);
        float taskAreaRight = bar.Right - trayW - ShowDesktopWidth - 6;

        var strip = new Rect(x, bar.Y + 2, MathF.Max(0, taskAreaRight - x), bar.H - 4);
        if (_shell.Settings.GroupSimilar) DrawSuperbar(c, strip);
        else DrawTaskButtons(c, strip);

        DrawTray(c, bar, measureOnly: false);
        DrawShowDesktop(c, bar);
    }

    /// <summary>The bar standing up the left or the right. The pieces are the
    /// same pieces; they are stacked instead of strung out, and the tray goes
    /// to the foot rather than the end.</summary>
    void LayOutStanding(UiContext c, Rect bar)
    {
        var inner = bar.Deflate(3, 2, 3, 2);

        // The Start button is a square at the head of the bar.
        var startRect = inner.CutTop(inner.W);
        DrawStartButton(c, startRect.Deflate(2));
        inner.CutTop(4);

        if (!_shell.Settings.LockTaskbar)
        {
            var grip = inner.CutTop(8);
            for (int i = 0; i < 2; i++)
            {
                var line = new Rect(grip.X + 6, grip.Y + i * 3, grip.W - 12, 1);
                c.R.FillRect(line, Color.Rgba(0xFFFFFF, 110));
                c.R.FillRect(new Rect(line.X, line.Y + 1, line.W, 1), Color.Rgba(0x000000, 60));
            }
            inner.CutTop(2);
        }

        // The show-desktop sliver goes to the very foot, as it does on a bar
        // lying down — it is always the far end.
        var slither = inner.CutBottom(ShowDesktopWidth);
        DrawShowDesktopStanding(c, slither);

        float trayH = DrawTrayStanding(c, inner, measureOnly: true);
        var tray = inner.CutBottom(trayH);
        DrawTrayStanding(c, tray, measureOnly: false);

        inner.CutBottom(4);
        if (_shell.Settings.GroupSimilar) DrawSuperbarStanding(c, inner);
        else DrawTaskButtonsStanding(c, inner);
    }

    // ---- the standing bar's own pieces ----------------------------------------

    /// <summary>The superbar, stacked. The buttons are square and the same
    /// square the lying bar uses, so a program looks the same whichever way the
    /// bar is turned; only the direction they run in changes.</summary>
    void DrawSuperbarStanding(UiContext c, Rect area)
    {
        var buttons = BuildButtons();
        if (area.H <= 20) { _dragApp = null; return; }
        if (buttons.Count == 0)
        {
            OfferDropStanding(c, area, area.Y, 0, area.W, 2);
            return;
        }

        float h = MathF.Min(area.W, 42);
        float gap = 2;
        int shown = 0;
        float y = area.Y;

        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (y + h > area.Bottom + 1) break;
            shown = i + 1;

            var r = new Rect(area.X, y, area.W, h);
            bool running = b.Windows.Count > 0;
            bool active = running && b.Windows.Contains(_shell.Wm.Focused) &&
                          _shell.Wm.Focused.State != WindowState.Minimized;
            bool hover = c.Hovering(r);
            bool carried = _dragMoved && b.App == _dragApp;

            if (running && b.Windows.Count > 1)
                for (int k = 1; k <= MathF.Min(2, b.Windows.Count - 1); k++)
                    DrawTile(c, new Rect(r.X + k * 2, r.Y - k * 2, r.W - k * 2, r.H),
                             false, false, (byte)(90 - k * 24));

            if (running || hover || carried) DrawTile(c, r, active, hover, 255);

            var ic = new Rect(r.CenterX - 12, r.CenterY - 12, 24, 24);
            Icons.Draw(c.R, b.Icon, ic);
            if (!running) c.R.FillRect(ic, Color.Rgba(0x000000, 70));
            if (carried) c.R.FillRect(r, Color.Rgba(0x000000, 90));

            if (hover)
            {
                if (_hoverApp != b.App) { _hoverApp = b.App; _hoverSince = c.Time; }
                _hoverRect = r;
                if (!running && _dragApp == null)
                    c.Tooltip(r, running ? b.Windows[0].TaskbarTitle : b.Label);
            }

            if (hover && !c.MouseHandled && c.In.Pressed(MouseButton.Left)
                && !_shell.Drag.Dragging)
            {
                _dragApp = b.App;
                _dragFromX = c.MouseY;      // standing up, it is the Y that travels
                _dragMoved = false;
                _dropAt = i;
                c.MouseHandled = true;
            }
            else if (c.RightClicked(r)) ShowButtonMenu(c, b);

            y += h + gap;
        }

        float rowEnd = area.Y + shown * (h + gap) - gap;
        OfferDropStanding(c, area, rowEnd, IndexAt(c.MouseY, area.Y, h, gap, shown), h, gap);
        UpdateButtonDragStanding(c, buttons, area, rowEnd, h, gap, shown);

        if (!c.Hovering(area) && !_previewHeld) _hoverApp = null;
    }

    /// <summary>Labelled buttons, stacked: the pinned icons in a block of rows
    /// at the head, then a full-width row per window.</summary>
    void DrawTaskButtonsStanding(UiContext c, Rect area)
    {
        var t = c.Theme;
        if (area.H <= 20) { _dragApp = null; return; }

        var full = area;
        _stripShown = 0;
        _stripRight = area.Y;

        if (_shell.Settings.ShowQuickLaunch && (_pinned.Count > 0 || _shell.Drag.Dragging))
        {
            float cell = MathF.Min(area.W * 0.5f - 2, 34);
            int perRow = Math.Max(1, (int)(area.W / (cell + 2)));
            int rows = (int)MathF.Ceiling(_pinned.Count / (float)perRow);
            var strip = area.CutTop(MathF.Max(cell + 4, rows * (cell + 2) + 4));

            var windows = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();
            for (int i = 0; i < _pinned.Count; i++)
            {
                string app = _pinned[i];
                var (icon, label) = Describe(app);
                var r = new Rect(strip.X + (i % perRow) * (cell + 2),
                                 strip.Y + (i / perRow) * (cell + 2), cell, cell);
                if (r.Bottom > strip.Bottom + 1) break;

                bool hover = c.Hovering(r);
                if (hover) DrawTile(c, r, false, true, 255);
                Icons.Draw(c.R, icon, new Rect(r.CenterX - 8, r.CenterY - 8, 16, 16));
                if (!windows.Any(w => w.ProgramId == app))
                    c.R.FillRect(new Rect(r.CenterX - 8, r.CenterY - 8, 16, 16),
                                 Color.Rgba(0x000000, 70));

                if (hover) c.Tooltip(r, label);

                var button = new Button(app, icon, label,
                                        windows.Where(w => w.ProgramId == app).ToList(), true);
                if (c.Clicked(r)) Activate(c, button);
                else if (c.RightClicked(r)) { _hoverRect = r; ShowButtonMenu(c, button); }
            }

            c.R.FillRect(new Rect(strip.X + 2, strip.Bottom, strip.W - 4, 1), Color.Rgba(0x000000, 60));
            c.R.FillRect(new Rect(strip.X + 2, strip.Bottom + 1, strip.W - 4, 1), Color.Rgba(0xFFFFFF, 70));
            area.CutTop(3);
        }

        OfferDropStanding(c, full, full.Y, 0, 34, 2);

        var wins = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();
        if (wins.Count == 0 || area.H <= 12) return;

        float rowH = MathF.Min(26, (area.H - 3 * (wins.Count - 1)) / wins.Count);
        if (rowH < 18) rowH = 18;

        float y = area.Y;
        foreach (var win in wins)
        {
            if (y + rowH > area.Bottom + 1) break;
            var r = new Rect(area.X, y, area.W, rowH);
            bool active = _shell.Wm.Focused == win && win.State != WindowState.Minimized;
            bool hover = c.Hovering(r);

            DrawTile(c, r, active, hover, 255);
            Icons.Draw(c.R, win.Icon, new Rect(r.X + 4, r.CenterY - 8, 16, 16));

            var textArea = new Rect(r.X + 24, r.Y, r.W - 28, r.H);
            if (textArea.W > 8)
            {
                c.R.PushClip(textArea);
                c.F.Small.Draw(c.R, c.F.Small.Ellipsize(win.TaskbarTitle, textArea.W),
                               textArea.X, r.CenterY - c.F.Small.Height * 0.5f, t.TaskbarText);
                c.R.PopClip();
            }

            c.Tooltip(r, win.TaskbarTitle);
            if (c.Clicked(r)) { CloseFlyouts(); CloseStart(c); _shell.Wm.RestoreOrFocus(win, c); }
            else if (c.RightClicked(r)) ShowWindowMenu(c, win);

            y += rowH + 3;
        }
    }

    /// <summary>The same drop offer as the lying bar, counted downwards.</summary>
    void OfferDropStanding(UiContext c, Rect area, float rowEnd, int at, float h, float gap)
    {
        if (!_shell.Drag.Dragging) return;
        if (ProgramFor(_shell.Drag.Node) == null && _shell.Drag.Launch == null) return;
        if (!_shell.Drag.OfferPin(c, area, this)) return;

        _dropFromDrag = at;
        c.R.FillRect(area, Color.Rgba(0xFFFFFF, 26));

        float y = at <= 0 ? area.Y : MathF.Min(area.Y + at * (h + gap) - gap * 0.5f, rowEnd + 2);
        var caret = new Rect(area.X, y - 1.5f, area.W, 3);
        c.R.FillRect(caret, c.Theme.Id == ThemeId.Metro ? Theme.MetroAccent : Color.Rgba(0xFFFFFF, 220));
    }

    void UpdateButtonDragStanding(UiContext c, List<Button> buttons, Rect area,
                                  float rowEnd, float h, float gap, int shown)
    {
        if (_dragApp == null) return;

        int index = buttons.FindIndex(b => b.App == _dragApp);
        if (index < 0) { _dragApp = null; _dragMoved = false; _dropAt = -1; return; }

        if (c.In.IsDown(MouseButton.Left))
        {
            if (MathF.Abs(c.MouseY - _dragFromX) > DragSlop) _dragMoved = true;
            if (!_dragMoved) return;

            _dropAt = IndexAt(c.MouseY, area.Y, h, gap, shown);
            c.Cursor = CursorShape.Move;
            c.MouseHandled = true;

            float y = _dropAt <= 0 ? area.Y
                    : MathF.Min(area.Y + _dropAt * (h + gap) - gap * 0.5f, rowEnd + 2);
            c.R.FillRect(new Rect(area.X, y - 1.5f, area.W, 3),
                         c.Theme.Id == ThemeId.Metro ? Theme.MetroAccent : Color.Rgba(0xFFFFFF, 220));
            return;
        }

        string app = _dragApp;
        bool moved = _dragMoved;
        int at = _dropAt;

        _dragApp = null;
        _dragMoved = false;
        _dropAt = -1;

        if (!moved) { Activate(c, buttons[index]); return; }
        if (at == index || at == index + 1) return;

        int pinAt = 0;
        for (int i = 0; i < at && i < buttons.Count; i++)
            if (buttons[i].Pinned && buttons[i].App != app) pinAt++;

        Pin(app, c, pinAt);
    }

    /// <summary>The notification area at the foot of a standing bar: the icons
    /// in a row, the language tag under them, and the clock in two lines, which
    /// is the only way a clock fits in a column this narrow.</summary>
    float DrawTrayStanding(UiContext c, Rect area, bool measureOnly)
    {
        var t = c.Theme;
        var settings = _shell.Settings;

        float iconsH = 22;
        float langH = 20;
        float clockH = settings.ShowClock ? c.F.Ui.Height * 2 + 8 : 0;
        float total = iconsH + langH + clockH + 12;

        if (measureOnly) return total;

        var tray = new Rect(area.X, area.Bottom - total, area.W, total);
        if (t.Id != ThemeId.Classic)
        {
            c.R.FillRectH(tray, t.TrayBack, t.TrayBack.Shade(0.85f));
            c.R.FillRect(new Rect(tray.X, tray.Y, tray.W, 1), t.TrayEdge);
        }
        else W.Bevel(c, tray, false);

        var inner = tray.Deflate(4, 5, 4, 4);

        // ---- the three status icons, in a row --------------------------------
        var row = inner.CutTop(iconsH);
        var trayIcons = new (IconId id, string tip, Action click)[]
        {
            (IconId.TrayNetwork, "tray.local_area_connection", () => _shell.ShowNetworkBalloon(c)),
            (IconId.Volume, "tray.volume", null),
            (IconId.Shield, "tray.security_center",
                () => _shell.Launch(c, "notepad", _shell.Fs.AntivirusFile)),
        };

        float step = row.W / trayIcons.Length;
        for (int i = 0; i < trayIcons.Length; i++)
        {
            var (id, tip, click) = trayIcons[i];
            var r = new Rect(row.X + i * step, row.Y, step, row.H);
            var box = new Rect(r.CenterX - 8, r.CenterY - 8, 16, 16);

            if (c.Hovering(r)) c.R.RoundedRect(r.Deflate(1), 2, Color.Rgba(0xFFFFFF, 55));
            Icons.Draw(c.R, id == IconId.Volume && _shell.Audio.Muted ? IconId.None : id, box);
            if (id == IconId.Volume && _shell.Audio.Muted)
            {
                Icons.Draw(c.R, IconId.Volume, box);
                c.R.Line(box.X, box.Bottom, box.Right, box.Y, Color.Rgb(0xE04030), 1.8f);
            }
            c.Tooltip(r, L.T(tip));

            if (id == IconId.Volume)
            {
                _speaker = r;
                if (c.Clicked(r))
                {
                    bool was = _volumeOpen;
                    CloseFlyouts();
                    _volumeOpen = !was;
                    c.Sound(_volumeOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.5f);
                }
                else if (c.RightClicked(r)) _shell.ToggleMute(c);
            }
            else if (click != null && c.Clicked(r)) click();
        }

        // ---- the language tag ------------------------------------------------
        var lang = inner.CutTop(langH);
        var langRect = new Rect(lang.CenterX - 16, lang.Y, 32, lang.H - 2);
        if (c.Hovering(langRect)) c.R.RoundedRect(langRect, 2, Color.Rgba(0xFFFFFF, 55));
        c.R.DrawRect(langRect, Color.Rgba(0xFFFFFF, 90));
        c.F.Small.DrawCentered(c.R, L.TrayTag, langRect, t.TaskbarText);
        c.Tooltip(langRect, L.T("taskbar.input_language_click_to_switch"));
        _langAnchor = langRect;

        if (c.Clicked(langRect))
        {
            bool was = _langOpen;
            CloseFlyouts();
            _langOpen = !was;
            c.Sound(_langOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.5f);
        }
        else if (c.RightClicked(langRect)) { L.Toggle(); c.Sound(Sfx.Click, 0.6f); }

        // ---- the clock, in two lines ------------------------------------------
        if (!settings.ShowClock) return total;

        var clock = inner;
        _clockAnchor = clock;

        string time = L.Time(_shell.Now);
        string date = _shell.Now.ToString("dd.MM");

        if (c.Hovering(clock)) c.R.RoundedRect(clock, 2, Color.Rgba(0xFFFFFF, 40));
        c.F.Ui.DrawCentered(c.R, time, new Rect(clock.X, clock.Y, clock.W, c.F.Ui.Height + 2),
                            t.TaskbarText);
        c.F.Small.DrawCentered(c.R, date,
            new Rect(clock.X, clock.Y + c.F.Ui.Height + 2, clock.W, c.F.Small.Height + 2),
            t.TaskbarText);

        c.Tooltip(clock, L.LongDate(_shell.Now));
        if (c.Clicked(clock))
        {
            bool was = _clockOpen;
            CloseFlyouts();
            _clockOpen = !was;
            c.Sound(_clockOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.5f);
        }

        return total;
    }

    /// <summary>The show-desktop sliver, lying across the foot of a standing
    /// bar rather than up its end.</summary>
    void DrawShowDesktopStanding(UiContext c, Rect r)
    {
        bool hover = c.Hovering(r);
        c.R.FillRect(r, hover ? Color.Rgba(0xFFFFFF, 60) : Color.Rgba(0xFFFFFF, 24));
        c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Color.Rgba(0x000000, 60));

        c.Tooltip(r, L.T("taskbar.show_the_desktop"));
        if (c.Clicked(r)) _shell.Wm.MinimizeAll(c);
    }

    // ---- carrying the bar to another edge --------------------------------------

    /// <summary>Where the bar was picked up, and whether the pointer has left
    /// the bar since. An unlocked bar dragged across the screen lands on
    /// whichever edge the pointer is nearest when it is let go — which is how
    /// every taskbar has been moved since 95.</summary>
    bool _movingBar;
    bool _barTravelled;
    float _barFromX, _barFromY;
    TaskbarEdge _moveTo;

    /// <summary>How far the pointer has to travel before a press on the bar is
    /// a move of it. Without this, a click on an empty stretch of bar would
    /// send it to whichever edge the pointer happened to be nearest.</summary>
    const float BarSlop = 12;

    void UpdateEdgeDrag(UiContext c, Rect bar)
    {
        var s = _shell.Settings;

        if (!_movingBar)
        {
            if (s.LockTaskbar || c.MouseHandled) return;
            if (!c.Clicked(bar)) return;

            _movingBar = true;
            _barTravelled = false;
            _barFromX = c.MouseX;
            _barFromY = c.MouseY;
            _moveTo = Edge;
            return;
        }

        if (s.LockTaskbar) { _movingBar = false; return; }

        if (MathF.Abs(c.MouseX - _barFromX) > BarSlop ||
            MathF.Abs(c.MouseY - _barFromY) > BarSlop)
            _barTravelled = true;

        if (!_barTravelled)
        {
            // Still a click as far as anyone knows. If the button comes up
            // here, nothing happened.
            if (!c.In.IsDown(MouseButton.Left)) _movingBar = false;
            return;
        }

        // Whichever edge the pointer is closest to is where it would go.
        float left = c.MouseX, right = c.ScreenW - c.MouseX;
        float top = c.MouseY, bottom = c.ScreenH - c.MouseY;
        float best = MathF.Min(MathF.Min(left, right), MathF.Min(top, bottom));

        _moveTo = best == left ? TaskbarEdge.Left
                : best == right ? TaskbarEdge.Right
                : best == top ? TaskbarEdge.Top : TaskbarEdge.Bottom;

        if (c.In.IsDown(MouseButton.Left))
        {
            c.Cursor = CursorShape.Move;
            c.MouseHandled = true;

            // The outline of where it would land, so the drag says what it is.
            if (_moveTo != Edge)
            {
                float k = _moveTo is TaskbarEdge.Left or TaskbarEdge.Right
                    ? VerticalWidth : c.Theme.TaskbarHeight;

                var ghost = _moveTo switch
                {
                    TaskbarEdge.Top => new Rect(0, 0, c.ScreenW, k),
                    TaskbarEdge.Left => new Rect(0, 0, k, c.ScreenH),
                    TaskbarEdge.Right => new Rect(c.ScreenW - k, 0, k, c.ScreenH),
                    _ => new Rect(0, c.ScreenH - k, c.ScreenW, k),
                };
                c.R.FillRect(ghost, Color.Rgba(0xFFFFFF, 40));
                c.R.DrawRect(ghost, Color.Rgba(0xFFFFFF, 190), 2);
            }
            return;
        }

        _movingBar = false;
        if (_moveTo == Edge) return;

        s.TaskbarEdge = _moveTo;
        CloseFlyouts();
        c.Sound(Sfx.Snap, 0.6f);
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

        if (t.Id == ThemeId.Metro)
        {
            // A flat corner tile. Version 8 shipped without a Start button at
            // all; this is the one 8.1 put back, square and without the orb.
            c.R.FillRect(r, held ? Theme.MetroAccent.Shade(0.8f)
                          : hover ? Theme.MetroAccent : Color.Rgba(0xFFFFFF, 16));
            Icons.Draw(c.R, IconId.Tiles, r.Deflate(r.H * 0.24f));
        }
        else if (t.Id == ThemeId.Seven)
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

        c.Tooltip(r, L.T(_shell.Settings.UseStartScreen
                             ? "taskbar.start_screen_tip" : "taskbar.click_here_to_begin"));

        if (c.Clicked(r))
        {
            // Ignore the click that just dismissed the menu.
            if (c.Time - _startClosedAt > 0.15) _shell.ToggleStartUi(c);
        }
        else if (c.RightClicked(r)) _shell.Switcher.ShowPowerUserMenu(c, r);
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


    // ---- суперпанель (the seven arrangement) --------------------------------

    /// <summary>What a fresh installation finds on the bar: the four the quick
    /// launch strip used to hold, plus the one the display properties needed.
    /// The nicer names come from here too — «Веб-браузер Firefox» rather than
    /// the program's own short name — for the five that had one.</summary>
    static readonly (IconId icon, string app, string tipKey)[] DefaultPinned =
    {
        (IconId.Firefox, "browser", "taskbar.firefox_web_browser"),
        (IconId.Folder, "mycomputer", "start.my_computer"),
        (IconId.Notepad, "notepad", "taskbar.notepad"),
        (IconId.MediaPlayer, "player", "taskbar.media_player"),
        (IconId.Display, "display", "taskbar.display_properties"),
    };

    /// <summary>The programs that are on the bar whether they are running or
    /// not, in the order they sit in.
    ///
    /// Version 7 made this a list the user owns: a program can be dragged along
    /// the row, dropped onto the bar from the desktop or a folder window to be
    /// pinned, and taken off again from its own menu. The order is kept in
    /// settings.txt, so the bar looks the same after a restart.</summary>
    readonly List<string> _pinned = DefaultPinned.Select(p => p.app).ToList();

    /// <summary>The pinned programs, in order — what the settings file writes
    /// out and reads back.</summary>
    public IReadOnlyList<string> Pinned => _pinned;

    /// <summary>Replaces the whole list, used when settings are loaded. An
    /// empty or unreadable list leaves the defaults alone, so a damaged file
    /// does not produce an empty bar.</summary>
    public void SetPinned(IEnumerable<string> apps)
    {
        var wanted = apps?.Where(a => !string.IsNullOrWhiteSpace(a))
                          .Select(a => a.Trim())
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .ToList();
        if (wanted == null) return;

        _pinned.Clear();
        _pinned.AddRange(wanted);
    }

    public bool IsPinned(string app)
        => app != null && _pinned.Any(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase));

    /// <summary>Puts a program on the bar, at a given place in the row or at
    /// the end. Pinning something already pinned only moves it.</summary>
    public void Pin(string app, UiContext c, int at = -1)
    {
        if (string.IsNullOrWhiteSpace(app)) return;

        _pinned.RemoveAll(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase));
        _pinned.Insert(Math.Clamp(at < 0 ? _pinned.Count : at, 0, _pinned.Count), app);

        // Pinning is pointless while the pinned programs are hidden, so asking
        // for it turns them back on.
        _shell.Settings.ShowQuickLaunch = true;
        c?.Sound(Sfx.Snap, 0.55f);
    }

    public void Unpin(string app, UiContext c)
    {
        if (_pinned.RemoveAll(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase)) > 0)
            c?.Sound(Sfx.Tick, 0.5f);
    }

    /// <summary>The icon and the name a program id is drawn with, running or
    /// not — the five originals keep the names the taskbar always gave them,
    /// and everything else asks the program registry.</summary>
    (IconId icon, string label) Describe(string app)
    {
        foreach (var (icon, id, tipKey) in DefaultPinned)
            if (string.Equals(id, app, StringComparison.OrdinalIgnoreCase))
                return (icon, L.T(tipKey));

        var entry = _shell.Programs.Find(app);
        return entry != null ? (entry.Icon, L.T(entry.NameKey)) : (IconId.Program, app);
    }

    /// <summary>Which program opens a file, for a drop onto the bar. Most nodes
    /// say so themselves; the rest are answered by what kind of file they
    /// are.</summary>
    static string ProgramFor(VNode node)
    {
        if (node == null) return null;
        if (!string.IsNullOrEmpty(node.Launch)) return node.Launch;

        return node.Kind switch
        {
            NodeKind.Folder or NodeKind.Drive or NodeKind.DvdDrive
                or NodeKind.Removable => "explorer",
            NodeKind.TextFile or NodeKind.Document => "notepad",
            NodeKind.ImageFile => "paint",
            NodeKind.Spreadsheet => "spreadsheet",
            NodeKind.Audio or NodeKind.Video => "player",
            _ => null,
        };
    }

    /// <summary>One button: the program it stands for and the windows behind
    /// it. A pinned program with no windows is a button all the same.</summary>
    readonly record struct Button(string App, IconId Icon, string Label,
                                  List<OsWindow> Windows, bool Pinned);

    /// <summary>Which button the pointer is resting on, and since when — the
    /// preview only comes up once it has been there a moment.</summary>
    string _hoverApp;
    double _hoverSince;
    Rect _hoverRect;

    const float ShowDesktopWidth = 12;

    List<Button> BuildButtons()
    {
        var list = new List<Button>();
        var windows = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();

        if (_shell.Settings.ShowQuickLaunch)
            foreach (string app in _pinned)
            {
                var (icon, label) = Describe(app);
                list.Add(new Button(app, icon, label,
                                    windows.Where(w => w.ProgramId == app).ToList(), true));
            }

        // Everything running that is not already pinned, grouped by program.
        foreach (var group in windows
                     .Where(w => !list.Any(b => b.App == w.ProgramId))
                     .GroupBy(w => w.ProgramId ?? w.Id))
        {
            var first = group.First();
            list.Add(new Button(group.Key, first.Icon, first.TaskbarTitle, group.ToList(), false));
        }

        return list;
    }

    // ---- carrying a button along the row --------------------------------------
    //
    // Version 7 let the buttons be rearranged by dragging them, and that is what
    // this is. A press arms a drag; if the pointer travels, a caret shows where
    // the button would land and the click never becomes an activation; if it
    // does not, the release activates the button as it always did.
    //
    // Only the pinned programs keep an order — that order is the whole of what
    // the bar remembers — so dropping a running program that is not pinned into
    // the row pins it where it landed. It is the same gesture either way, and it
    // is the second way to pin something, the first being to drop a file on the
    // bar from the desktop.

    /// <summary>The program being carried, where the pointer picked it up, and
    /// whether it has moved far enough to count as a drag.</summary>
    string _dragApp;
    float _dragFromX;
    bool _dragMoved;

    /// <summary>Where the caret is, in button positions, while a drag is on.</summary>
    int _dropAt = -1;

    /// <summary>How far the pointer has to travel before a click on a button
    /// becomes a drag of it.</summary>
    const float DragSlop = 5;

    /// <summary>The row of square icon buttons. A running program gets a lit
    /// tile, the focused one a brighter tile still, and a program with more than
    /// one window gets the stacked edges behind it that say so.</summary>
    void DrawSuperbar(UiContext c, Rect area)
    {
        var buttons = BuildButtons();
        if (area.W <= 20) { _dragApp = null; return; }
        if (buttons.Count == 0)
        {
            // Nothing on the bar at all is still somewhere to drop a program.
            OfferDrop(c, area, area.X, 0, MathF.Min(area.H, 40) + 12, 2);
            return;
        }

        float size = MathF.Min(area.H, 40);
        float gap = 2;
        float w = MathF.Min(size + 12, (area.W - gap * (buttons.Count - 1)) / buttons.Count);
        if (w < 20) w = 20;

        float x = area.X;
        bool hoveringAny = false;
        int shown = 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            if (x + w > area.Right + 1) break;
            shown = i + 1;

            var r = new Rect(x, area.Y, w, area.H);
            bool running = b.Windows.Count > 0;
            bool active = running && b.Windows.Contains(_shell.Wm.Focused) &&
                          _shell.Wm.Focused.State != WindowState.Minimized;
            bool hover = c.Hovering(r);
            bool carried = _dragMoved && b.App == _dragApp;

            // The stack: one edge per extra window, offset behind the tile.
            if (running && b.Windows.Count > 1)
                for (int k = 1; k <= MathF.Min(2, b.Windows.Count - 1); k++)
                {
                    var back = new Rect(r.X + k * 2, r.Y - k * 2, r.W - k * 2, r.H);
                    DrawTile(c, back, false, false, (byte)(90 - k * 24));
                }

            if (running || hover || carried) DrawTile(c, r, active, hover, 255);

            var ic = new Rect(r.CenterX - 12, r.CenterY - 12, 24, 24);
            Icons.Draw(c.R, b.Icon, ic);

            // A pinned program that is not running is drawn dimmer, which is
            // how seven said "this is a shortcut, not a window". The one being
            // carried is dimmer still: it is on its way somewhere.
            if (!running) c.R.FillRect(ic, Color.Rgba(0x000000, 70));
            if (carried) c.R.FillRect(r, Color.Rgba(0x000000, 90));

            string label = running ? b.Windows[0].TaskbarTitle : b.Label;
            if (hover)
            {
                hoveringAny = true;
                if (_hoverApp != b.App) { _hoverApp = b.App; _hoverSince = c.Time; }
                _hoverRect = r;
                if (!running && _dragApp == null) c.Tooltip(r, label);
            }

            // The press arms a drag rather than doing anything: what it turns
            // out to have been is settled when the button comes back up.
            if (hover && !c.MouseHandled && c.In.Pressed(MouseButton.Left)
                && !_shell.Drag.Dragging)
            {
                _dragApp = b.App;
                _dragFromX = c.MouseX;
                _dragMoved = false;
                _dropAt = i;
                c.MouseHandled = true;
            }
            else if (c.RightClicked(r)) ShowButtonMenu(c, b);

            x += w + gap;
        }

        float rowRight = area.X + shown * (w + gap) - gap;

        // A file carried from the desktop or a folder window: the bar is a
        // place to drop it, and dropping it pins whatever opens it.
        OfferDrop(c, area, rowRight, IndexAt(c.MouseX, area.X, w, gap, shown), w, gap);

        UpdateButtonDrag(c, buttons, area, rowRight, w, gap, shown);

        if (!hoveringAny && !_previewHeld) _hoverApp = null;
    }

    /// <summary>Which gap in the row a pointer position falls in — 0 before the
    /// first button, <paramref name="count"/> after the last.</summary>
    static int IndexAt(float mouseX, float left, float w, float gap, int count)
    {
        int i = (int)MathF.Floor((mouseX - left + (w + gap) * 0.5f) / (w + gap));
        return Math.Clamp(i, 0, count);
    }

    /// <summary>Carries the picked-up button, and drops it. A drag that never
    /// travelled is a click, which is where a button is activated from.</summary>
    void UpdateButtonDrag(UiContext c, List<Button> buttons, Rect area,
                          float rowRight, float w, float gap, int shown)
    {
        if (_dragApp == null) return;

        int index = buttons.FindIndex(b => b.App == _dragApp);
        if (index < 0) { _dragApp = null; _dragMoved = false; _dropAt = -1; return; }

        if (c.In.IsDown(MouseButton.Left))
        {
            if (MathF.Abs(c.MouseX - _dragFromX) > DragSlop) _dragMoved = true;
            if (!_dragMoved) return;

            _dropAt = IndexAt(c.MouseX, area.X, w, gap, shown);
            c.Cursor = CursorShape.Move;
            c.MouseHandled = true;

            DrawDropCaret(c, area, w, gap, _dropAt, rowRight);
            return;
        }

        // The button is back up: either it went somewhere, or it was a click.
        string app = _dragApp;
        bool moved = _dragMoved;
        int at = _dropAt;

        _dragApp = null;
        _dragMoved = false;
        _dropAt = -1;

        if (!moved) { Activate(c, buttons[index]); return; }
        if (at == index || at == index + 1) return;   // put back where it was

        // The caret counts every button; the list only holds the pinned ones,
        // so the landing place is counted in those.
        int pinAt = 0;
        for (int i = 0; i < at && i < buttons.Count; i++)
            if (buttons[i].Pinned && buttons[i].App != app) pinAt++;

        Pin(app, c, pinAt);
    }

    /// <summary>The bar between two buttons that says where the one in hand
    /// would land.</summary>
    void DrawDropCaret(UiContext c, Rect area, float w, float gap, int at, float rowRight)
    {
        float x = at <= 0 ? area.X
                : MathF.Min(area.X + at * (w + gap) - gap * 0.5f, rowRight + 2);
        var caret = new Rect(x - 1.5f, area.Y, 3, area.H);

        c.R.FillRect(caret, c.Theme.Id == ThemeId.Metro ? Theme.MetroAccent : Color.Rgba(0xFFFFFF, 220));
        c.R.FillRect(new Rect(caret.X - 2, caret.Y, 7, 3), Color.Rgba(0xFFFFFF, 220));
        c.R.FillRect(new Rect(caret.X - 2, caret.Bottom - 3, 7, 3), Color.Rgba(0xFFFFFF, 220));
    }

    /// <summary>Offers the button row as a destination for a file being carried
    /// from the desktop or a folder window, and shows where it would go.</summary>
    void OfferDrop(UiContext c, Rect area, float rowRight, int at, float w, float gap)
    {
        if (!_shell.Drag.Dragging) return;
        if (ProgramFor(_shell.Drag.Node) == null && _shell.Drag.Launch == null) return;
        if (!_shell.Drag.OfferPin(c, area, this)) return;

        // Where the caret is drawn is where the drop lands: the same number is
        // used for both, so the two can never disagree.
        _dropFromDrag = at;

        c.R.FillRect(area, Color.Rgba(0xFFFFFF, 26));
        DrawDropCaret(c, area, w, gap, at, rowRight);
    }

    /// <summary>The place the caret was last showing for a file being carried
    /// over the bar, which is where that file's program is pinned when it is
    /// let go.</summary>
    int _dropFromDrag;

    /// <summary>Takes a dropped file: what opens it goes on the bar, where it
    /// was let go. Called by the drag host once every layer has had its say.</summary>
    public void AcceptDrop(UiContext c, VNode node, string launch)
    {
        string app = launch ?? ProgramFor(node);
        if (app == null) return;

        // The caret counted every button in the row it was drawn in; the list
        // only holds the pinned ones, so the landing place is counted in those.
        var buttons = BuildButtons();
        int pinAt = 0;
        for (int i = 0; i < _dropFromDrag && i < buttons.Count; i++)
            if (buttons[i].Pinned &&
                !string.Equals(buttons[i].App, app, StringComparison.OrdinalIgnoreCase))
                pinAt++;

        Pin(app, c, pinAt);
    }

    /// <summary>The lit tile behind a running button: a frame, a fill, and the
    /// brighter wash the focused one gets. The theme supplies the colours, so
    /// this is glass under «Миминус 7» and a flat block under «Миминус 8».</summary>
    void DrawTile(UiContext c, Rect r, bool active, bool hover, byte alpha)
    {
        var t = c.Theme;
        Color face = active ? t.TaskButtonActive : hover ? t.TaskButtonFace.Shade(1.4f)
                                                         : t.TaskButtonFace;

        if (t.Id == ThemeId.Metro)
        {
            c.R.FillRect(r, face.WithAlpha(alpha));
            if (active) c.R.FillRect(new Rect(r.X, r.Bottom - 2, r.W, 2), Theme.MetroAccent);
        }
        else if (t.Id == ThemeId.Classic)
        {
            c.R.FillRect(r, t.TaskButtonFace);
            W.Bevel(c, r, !active);
        }
        else
        {
            c.R.RoundedRect(r, 3, face.WithAlpha(alpha), t.TaskButtonBorder.WithAlpha(alpha), 1);
            // The highlight along the top that made these look like glass.
            c.R.FillRectV(new Rect(r.X + 1, r.Y + 1, r.W - 2, r.H * 0.45f),
                          Color.Rgba(0xFFFFFF, (byte)(alpha / 5)), Color.Transparent);
        }
    }

    /// <summary>What clicking a button does: start the program if it is not
    /// running, raise or hide the window if there is one, and offer the list if
    /// there are several.</summary>
    void Activate(UiContext c, Button b)
    {
        CloseFlyouts();
        CloseStart(c);

        if (b.Windows.Count == 0) { _shell.Launch(c, b.App, null); return; }
        if (b.Windows.Count == 1) { _shell.Wm.RestoreOrFocus(b.Windows[0], c); return; }

        _hoverApp = b.App;
        _previewHeld = true;
    }

    void ShowButtonMenu(UiContext c, Button b)
    {
        var items = new List<MenuItem>
        {
            MenuItem.Of(b.Label, () => _shell.Launch(c, b.App, null), b.Icon),
            MenuItem.Sep(),
        };

        foreach (var win in b.Windows)
            items.Add(MenuItem.Of(win.TaskbarTitle, () => _shell.Wm.RestoreOrFocus(win, c), win.Icon));

        if (b.Windows.Count > 0) items.Add(MenuItem.Sep());

        // The jump list's one permanent entry: whether this program stays on
        // the bar after its last window closes.
        string app = b.App;
        bool pinned = IsPinned(app);
        items.Add(MenuItem.Of(L.T(pinned ? "taskbar.unpin" : "taskbar.pin"),
            () => { if (pinned) Unpin(app, c); else Pin(app, c); }, IconId.Star));

        if (b.Windows.Count > 0)
        {
            items.Add(MenuItem.Sep());
            items.Add(MenuItem.Of(L.T("taskbar.close_all"),
                () => { foreach (var win in b.Windows.ToList()) _shell.Wm.RequestClose(win, c); }));
        }

        // The bar is at the foot of the screen, so the list goes up from the
        // button rather than down over it.
        _shell.Menus.Open(items, c.MouseX, _hoverRect.Y, this, c, 0, above: true);
    }

    /// <summary>Whether the preview is being kept up by a click rather than by
    /// the pointer resting on the button.</summary>
    bool _previewHeld;

    /// <summary>Aero Peek, as far as a system that cannot capture a window can
    /// take it: a card per window with its caption bar, its icon, its title and
    /// a close cross. Hovering one is enough to raise the window behind it.</summary>
    void DrawPreview(UiContext c, Rect bar)
    {
        if (_hoverApp == null) return;
        if (!_previewHeld && c.Time - _hoverSince < 0.55) return;

        var buttons = BuildButtons();
        int index = buttons.FindIndex(b => b.App == _hoverApp);
        if (index < 0) { _hoverApp = null; _previewHeld = false; return; }

        var button = buttons[index];
        if (button.Windows.Count == 0) return;

        const float cardW = 168, cardH = 116, pad = 8;
        float w = button.Windows.Count * cardW + pad * (button.Windows.Count + 1);
        float h = cardH + pad * 2;

        float px = Math.Clamp(_hoverRect.CenterX - w * 0.5f, 4, MathF.Max(4, c.ScreenW - w - 4));
        var panel = new Rect(px, bar.Y - h - 8, w, h);

        c.R.FillRect(panel.Offset(3, 3), Color.Rgba(0x000000, 60));
        c.R.FillRect(panel, Color.Rgba(0x2A2A2A, 240));
        c.R.DrawRect(panel, Color.Rgba(0xFFFFFF, 60));

        for (int i = 0; i < button.Windows.Count; i++)
        {
            var win = button.Windows[i];
            var card = new Rect(panel.X + pad + i * (cardW + pad), panel.Y + pad, cardW, cardH);
            bool hot = c.Hovering(card);

            if (hot) c.R.FillRect(card.Inflate(3), Color.Rgba(0xFFFFFF, 40));

            // A schematic of the window rather than a picture of it: the caption
            // in the theme's colour, a client area, and the program's icon.
            var t = c.Theme;
            c.R.FillRect(card, t.Face);
            c.R.FillRectV(new Rect(card.X, card.Y, card.W, 16),
                          t.CaptionActiveTop, t.CaptionActiveBottom);
            c.R.DrawRect(card, Color.Rgba(0x000000, 90));
            Icons.Draw(c.R, win.Icon, new Rect(card.X + 3, card.Y + 2, 12, 12));

            c.R.PushClip(new Rect(card.X + 18, card.Y, card.W - 22, 16));
            c.F.Small.Draw(c.R, win.TaskbarTitle, card.X + 18, card.Y + 2, t.CaptionTextActive);
            c.R.PopClip();

            Icons.Draw(c.R, win.Icon,
                       new Rect(card.CenterX - 18, card.CenterY - 10, 36, 36));

            // The close cross seven put in the corner of every thumbnail.
            var close = new Rect(card.Right - 16, card.Y + 1, 14, 14);
            if (c.Hovering(close)) c.R.FillRect(close, Color.Rgb(0xE81123));
            c.R.Line(close.X + 4, close.Y + 4, close.Right - 4, close.Bottom - 4,
                     Color.Rgba(0x202020, 200), 1.4f);
            c.R.Line(close.Right - 4, close.Y + 4, close.X + 4, close.Bottom - 4,
                     Color.Rgba(0x202020, 200), 1.4f);

            if (c.Clicked(close))
            {
                _shell.Wm.RequestClose(win, c);
                _hoverApp = null;
                _previewHeld = false;
                return;
            }
            if (c.Clicked(card))
            {
                _shell.Wm.RestoreOrFocus(win, c);
                _hoverApp = null;
                _previewHeld = false;
                return;
            }
        }

        // The panel keeps itself up while the pointer is on it.
        if (panel.Contains(c.MouseX, c.MouseY)) _hoverSince = MathF.Min((float)_hoverSince, 0);
        else if (!_hoverRect.Contains(c.MouseX, c.MouseY) && !_previewHeld) _hoverApp = null;

        if (_previewHeld && c.In.Pressed(MouseButton.Left) && !c.MouseHandled)
        {
            _previewHeld = false;
            _hoverApp = null;
        }
    }

    /// <summary>The sliver at the far right that shows the desktop: seven's
    /// last button, and the only one with no picture on it.</summary>
    void DrawShowDesktop(UiContext c, Rect bar)
    {
        var r = new Rect(bar.Right - ShowDesktopWidth, bar.Y + 1, ShowDesktopWidth - 1, bar.H - 2);
        bool hot = c.Hovering(r);

        c.R.FillRect(r, hot ? Color.Rgba(0xFFFFFF, 60) : Color.Rgba(0xFFFFFF, 12));
        c.R.FillRect(new Rect(r.X, r.Y, 1, r.H), Color.Rgba(0xFFFFFF, 70));

        c.Tooltip(r, L.T("taskbar.show_the_desktop"));
        if (c.Clicked(r)) _shell.Wm.MinimizeAll(c);
    }

    /// <summary>How wide the strip of pinned icons is when the bar is showing
    /// labelled buttons rather than the superbar. It is the quick launch strip,
    /// which is where the pinned programs lived before seven merged the two
    /// rows — and it is still a place to drop something even when it is
    /// empty.</summary>
    float PinStripWidth(Rect area)
    {
        if (!_shell.Settings.ShowQuickLaunch) return 0;

        float w = MathF.Min(area.H, 34);
        float used = _pinned.Count * (w + 2);
        if (_shell.Drag.Dragging) used = MathF.Max(used, w + 2);
        return used > 0 ? MathF.Min(used + 6, area.W * 0.6f) : 0;
    }

    /// <summary>The pinned programs as bare icons. They are dragged, dropped on
    /// and taken off exactly as they are on the superbar — the same list, drawn
    /// smaller and without the window tiles behind it.</summary>
    void DrawPinStrip(UiContext c, Rect area)
    {
        _stripW = MathF.Min(area.H, 34);
        _stripGap = 2;
        _stripShown = 0;
        _stripRight = area.X;
        if (area.W <= 4) return;

        float w = _stripW;
        float gap = _stripGap;

        var windows = _shell.Wm.Windows.Where(win => win.ShowInTaskbar).ToList();
        var buttons = new List<Button>();
        foreach (string app in _pinned)
        {
            var (icon, label) = Describe(app);
            buttons.Add(new Button(app, icon, label,
                                   windows.Where(win => win.ProgramId == app).ToList(), true));
        }

        int shown = 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            var r = new Rect(area.X + i * (w + gap), area.Y, w, area.H);
            if (r.Right > area.Right + 1) break;
            shown = i + 1;

            bool hover = c.Hovering(r);
            bool carried = _dragMoved && b.App == _dragApp;

            if (hover || carried) DrawTile(c, r, false, hover, 255);

            var ic = new Rect(r.CenterX - 8, r.CenterY - 8, 16, 16);
            Icons.Draw(c.R, b.Icon, ic);
            if (b.Windows.Count == 0) c.R.FillRect(ic, Color.Rgba(0x000000, 70));
            if (carried) c.R.FillRect(r, Color.Rgba(0x000000, 90));

            if (hover && _dragApp == null) c.Tooltip(r, b.Label);

            if (hover && !c.MouseHandled && c.In.Pressed(MouseButton.Left)
                && !_shell.Drag.Dragging)
            {
                _dragApp = b.App;
                _dragFromX = c.MouseX;
                _dragMoved = false;
                _dropAt = i;
                _hoverRect = r;
                c.MouseHandled = true;
            }
            else if (c.RightClicked(r)) { _hoverRect = r; ShowButtonMenu(c, b); }
        }

        float rowRight = area.X + shown * (w + gap) - gap;
        _stripShown = shown;
        _stripRight = rowRight;
        UpdateButtonDrag(c, buttons, area, rowRight, w, gap, shown);

        // The ridge that separated quick launch from the buttons.
        c.R.FillRect(new Rect(area.Right - 3, area.Y + 3, 1, area.H - 6), Color.Rgba(0x000000, 60));
        c.R.FillRect(new Rect(area.Right - 2, area.Y + 3, 1, area.H - 6), Color.Rgba(0xFFFFFF, 70));
    }

    /// <summary>What the quick-launch strip looked like when it was last
    /// drawn, so the drop caret can be placed in it while the whole task area
    /// is what accepts the drop.</summary>
    int _stripShown;
    float _stripRight, _stripW = 34, _stripGap = 2;

    void DrawTaskButtons(UiContext c, Rect area)
    {
        var t = c.Theme;
        var wins = _shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();
        if (area.W <= 20) { _dragApp = null; return; }

        // The pinned programs keep their strip whether anything is running or
        // not, so the bar is never empty and never refuses a drop.
        var full = area;
        _stripShown = 0;
        _stripRight = area.X;

        float pinW = PinStripWidth(area);
        if (pinW > 0) DrawPinStrip(c, area.CutLeft(pinW));

        // The whole row takes a drop, not only the strip: a file let go
        // anywhere along the bar is a file let go on the bar.
        OfferDrop(c, full, _stripRight,
                  IndexAt(c.MouseX, full.X, _stripW, _stripGap, _stripShown),
                  _stripW, _stripGap);

        if (wins.Count == 0 || area.W <= 20) return;

        float gap = 3;
        float maxW = t.Flat ? 168 : 160;

        float w = MathF.Min(maxW, (area.W - gap * (wins.Count - 1)) / wins.Count);
        if (w < 26) w = 26;

        float x = area.X;
        foreach (var win in wins)
        {
            if (x + w > area.Right + 1) break;
            var r = new Rect(x, area.Y, w - (t.Flat ? 2 : 0), area.H);
            bool active = _shell.Wm.Focused == win && win.State != WindowState.Minimized;
            bool hover = c.Hovering(r);

            if (t.Id == ThemeId.Metro)
            {
                Color face = active ? t.TaskButtonActive : hover ? Color.Rgba(0xFFFFFF, 40) : t.TaskButtonFace;
                c.R.FillRect(r, face);
                // The accent underline is how version 8 said "this one".
                if (active) c.R.FillRect(new Rect(r.X, r.Bottom - 2, r.W, 2), Theme.MetroAccent);
            }
            else if (t.Id == ThemeId.Seven)
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

    /// <summary>Draws the notification area. When <paramref name="measureOnly"/> is
    /// set nothing is painted and only the required width is returned, so the task
    /// button strip knows where to stop.</summary>
    float DrawTray(UiContext c, Rect bar, bool measureOnly)
    {
        var t = c.Theme;
        var settings = _shell.Settings;
        string clock = L.Time(_shell.Now);
        float clockW = settings.ShowClock ? c.F.Ui.Measure(clock) + 12 : 0;

        // Only what is not tucked away takes room in the bar; the chevron is
        // there whenever something is.
        int iconCount = Shown.Count();
        bool anyHidden = Tucked.Any();
        float iconArea = iconCount * 18 + 8 + (anyHidden ? 14 : 0);
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
        _langAnchor = langRect;

        // Version 8 stopped flipping the language on a click and started
        // showing the list instead; the middle button still just flips it.
        if (c.Clicked(langRect))
        {
            bool wasOpen = _langOpen;
            CloseFlyouts();
            _langOpen = !wasOpen;
            c.Sound(_langOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.5f);
        }
        else if (c.RightClicked(langRect))
        {
            L.Toggle();
            c.Sound(Sfx.Click, 0.6f);
        }
        x = langRect.Right + 6;

        // The chevron that opens the panel of everything tucked away.
        if (anyHidden || _trayExpanded)
        {
            _chevron = new Rect(x, tray.CenterY - 8, 12, 16);
            if (c.Hovering(_chevron)) c.R.RoundedRect(_chevron, 2, Color.Rgba(0xFFFFFF, 55));
            W.Arrow(c, _chevron, _trayExpanded ? 2 : 0, t.TaskbarText);
            c.Tooltip(_chevron, L.T(_trayExpanded ? "tray.hide_icons" : "tray.show_hidden_icons"));
            if (c.Clicked(_chevron))
            {
                bool was = _trayExpanded;
                CloseFlyouts();
                _trayExpanded = !was;
                c.Sound(Sfx.Click, 0.5f);
            }
            x += 14;
        }
        else _chevron = default;

        // The strip of icons the bar is showing. Dragging one down into the
        // panel behind the chevron tucks it away; dragging one back out brings
        // it into the bar, which is what seven let people do and what everybody
        // did within a week of installing it.
        _trayStrip = new Rect(x, tray.Y, iconCount * 18 + 4, tray.H);

        foreach (var slot in Shown.ToList())
        {
            var ir = new Rect(x, tray.CenterY - 8, 16, 16);
            var id = IconOf(slot);
            string tip = TipOf(slot);

            if (_carrying == slot && _carryMoved) c.R.FillRect(ir, Color.Rgba(0x000000, 90));
            Icons.Draw(c.R, id, ir);

            if (c.Hovering(ir) && !c.MouseHandled && c.In.Pressed(MouseButton.Left)
                && _carrying == null)
            {
                _carrying = slot;
                _carryMoved = false;
                _carryFromX = c.MouseX;
                _carryFromY = c.MouseY;
            }

            if (id == IconId.Volume)
            {
                _speaker = ir;
                if (_shell.Audio.Muted)
                    c.R.Line(ir.X + 2, ir.Y + 2, ir.Right - 2, ir.Bottom - 2, Color.Rgb(0xE04040), 2f);

                c.Tooltip(ir, _shell.Audio.Muted
                    ? L.T("tray.volume_muted")
                    : L.F("tray.volume_level", (int)MathF.Round(_shell.Audio.MasterVolume * 100)));

                // The wheel over the speaker moves the level without opening
                // anything, which is how the real tray behaves.
                if (c.Hovering(ir) && MathF.Abs(c.In.WheelDelta) > 0.01f)
                {
                    SetVolume(c, _shell.Audio.MasterVolume + c.In.WheelDelta * 0.05f);
                    c.In.WheelDelta = 0;
                }

                if (c.Clicked(ir))
                {
                    bool wasOpen = _volumeOpen;
                    CloseFlyouts();
                    _volumeOpen = !wasOpen;
                    c.Sound(Sfx.Click, 0.5f);
                }
                else if (c.RightClicked(ir)) ShowVolumeMenu(c);
            }
            else
            {
                c.Tooltip(ir, L.T(tip));
                if (c.Clicked(ir))
                {
                    if (slot == TrayIcon.Network) _shell.ShowNetworkBalloon(c);
                    else _shell.Launch(c, "notepad", _shell.Fs.AntivirusFile);
                }
            }

            x += 18;
        }

        if (settings.ShowClock)
        {
            var clockRect = new Rect(tray.Right - clockW, tray.Y, clockW, tray.H);
            c.F.Ui.DrawCentered(c.R, clock, clockRect, t.TaskbarText);
            c.Tooltip(clockRect, L.LongDate(_shell.Now));
            _clockAnchor = clockRect;

            if (c.Clicked(clockRect))
            {
                bool wasOpen = _clockOpen;
                CloseFlyouts();
                _clockOpen = !wasOpen;
                c.Sound(_clockOpen ? Sfx.MenuOpen : Sfx.MenuClose, 0.5f);
            }
        }

        UpdateTrayDrag(c);
        return total;
    }

    /// <summary>Where the shown icons are, so a drop on them means "put it
    /// back in the bar".</summary>
    Rect _trayStrip;

    /// <summary>Carries an icon between the bar and the panel. A press that
    /// never travels is still a click, so the icons keep working.</summary>
    void UpdateTrayDrag(UiContext c)
    {
        if (_carrying is not { } slot) return;

        if (c.In.IsDown(MouseButton.Left))
        {
            if (MathF.Abs(c.MouseX - _carryFromX) > 4 || MathF.Abs(c.MouseY - _carryFromY) > 4)
                _carryMoved = true;

            if (_carryMoved)
            {
                c.Cursor = CursorShape.Move;
                c.MouseHandled = true;

                var ghost = new Rect(c.MouseX - 8, c.MouseY - 8, 16, 16);
                Icons.Draw(c.R, IconOf(slot), ghost);
            }
            return;
        }

        bool moved = _carryMoved;
        _carrying = null;
        _carryMoved = false;
        if (!moved) return;

        var popup = TrayPopupBounds(c);
        bool intoPopup = !popup.IsEmpty && popup.Contains(c.MouseX, c.MouseY);
        bool intoStrip = _trayStrip.Contains(c.MouseX, c.MouseY)
                         || _chevron.Contains(c.MouseX, c.MouseY);

        var hidden = _shell.Settings.HiddenTrayIcons;
        if (intoPopup && !hidden.Contains(slot.ToString()))
        {
            hidden.Add(slot.ToString());
            c.Sound(Sfx.Tick, 0.5f);
        }
        else if (intoStrip && hidden.Contains(slot.ToString()))
        {
            hidden.Remove(slot.ToString());
            c.Sound(Sfx.Snap, 0.5f);
        }
    }

    /// <summary>The panel behind the chevron: the icons that are not in the bar,
    /// three to a row, on the small light square seven dropped out of the
    /// notification area. Anything in it can be dragged back into the bar, and
    /// anything in the bar can be dragged into it.</summary>
    void DrawTrayPopup(UiContext c)
    {
        if (!_trayExpanded) return;

        var t = c.Theme;
        var panel = TrayPopupBounds(c);
        if (panel.IsEmpty) return;

        c.R.RoundedRect(panel.Offset(2, 2), 4, Color.Rgba(0x000000, 45));
        c.R.RoundedRect(panel, 4, Color.Rgb(0xF7F9FC), Color.Rgb(0xA8BCD0), 1);
        c.R.RoundedRect(new Rect(panel.X + 1, panel.Y + 1, panel.W - 2, panel.H * 0.45f), 3,
                        Color.Rgba(0xFFFFFF, 150));

        var area = panel.Deflate(8, 6, 8, 6);
        var grid = area.CutTop(area.H - 20);

        int i = 0;
        foreach (var slot in Tucked)
        {
            var cell = new Rect(grid.X + (i % 3) * 30, grid.Y + (i / 3) * 30, 28, 28);
            bool hot = c.Hovering(cell);

            if (hot) c.R.RoundedRect(cell, 3, Color.Rgba(0x2D89EF, 40), Color.Rgb(0x7DA2C8), 1);
            Icons.Draw(c.R, IconOf(slot), new Rect(cell.CenterX - 8, cell.CenterY - 8, 16, 16));
            c.Tooltip(cell, L.T(TipOf(slot)));

            if (hot && !c.MouseHandled && c.In.Pressed(MouseButton.Left) && _carrying == null)
            {
                _carrying = slot;
                _carryMoved = false;
                _carryFromX = c.MouseX;
                _carryFromY = c.MouseY;
            }

            i++;
        }

        if (i == 0)
            c.F.Small.DrawCentered(c.R, L.T("tray.nothing_hidden"), grid, t.TextDisabled);

        c.F.Small.DrawCentered(c.R, L.T("tray.drag_hint"),
                               new Rect(area.X, area.Bottom - 16, area.W, 14), t.TextDisabled);

        bool outside = !panel.Contains(c.MouseX, c.MouseY) && !_chevron.Contains(c.MouseX, c.MouseY);
        if (_carrying == null && c.In.Pressed(MouseButton.Left) && outside) _trayExpanded = false;
    }

    /// <summary>Applies a new level and lets it be heard: a short tick at the
    /// level being set is the only way to judge it.</summary>
    void SetVolume(UiContext c, float level)
    {
        level = Math.Clamp(level, 0, 1);
        if (MathF.Abs(level - _shell.Audio.MasterVolume) < 1e-4f) return;

        _shell.Audio.MasterVolume = level;
        if (_shell.Audio.Muted) return;

        // Only every few steps, or dragging the slider would be a rattle.
        if (_lastBlip < 0 || MathF.Abs(level - _lastBlip) >= 0.06f)
        {
            _lastBlip = level;
            c.Sound(Sfx.Tick, 0.7f);
        }
    }

    void ShowVolumeMenu(UiContext c)
    {
        var bar = Bounds(c);
        _shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("tray.open_volume_control"), () => _volumeOpen = true, IconId.Volume),
            MenuItem.Sep(),
            new MenuItem
            {
                Text = L.T("tray.mute"),
                Checked = _shell.Audio.Muted,
                Click = () => _shell.ToggleMute(c),
            },
            MenuItem.Sep(),
            MenuItem.Of(L.T("tray.adjust_audio_properties"), () => _shell.Launch(c, "sound", null),
                        IconId.Settings),
        }, _speaker.CenterX, bar.Y, this, c);
    }

    /// <summary>The little panel the speaker drops: a standing slider and a
    /// mute box, the whole of the tray volume control.</summary>
    void DrawVolumePanel(UiContext c)
    {
        if (!_volumeOpen) return;

        var t = c.Theme;
        var panel = VolumeBounds(c);

        // Seven's flyout: a pale glass card with a rounded edge, the device
        // named at the top, the level standing up the middle of it, and the
        // mute button as a box at the foot. Nothing in it is a dialog control —
        // it is its own small thing, and that is what made it look modern.
        c.R.RoundedRect(panel.Offset(2, 3), 5, Color.Rgba(0x000000, 50));
        c.R.RoundedRect(panel, 5, Color.Rgb(0xF7F9FC), Color.Rgb(0xA8BCD0), 1);
        c.R.RoundedRect(new Rect(panel.X + 1, panel.Y + 1, panel.W - 2, panel.H * 0.42f), 4,
                        Color.Rgba(0xFFFFFF, 160));

        var area = panel.Deflate(8, 7, 8, 8);

        var head = area.CutTop(c.F.Small.Height + 6);
        c.F.Small.DrawCentered(c.R, L.T("tray.volume_label"), head, Color.Rgb(0x1E4E79));
        c.R.FillRect(new Rect(area.X + 4, head.Bottom, area.W - 8, 1), Color.Rgb(0xD8E2EC));

        var button = area.CutBottom(30);
        var reading = area.CutBottom(c.F.Small.Height + 4);

        // ---- the level ------------------------------------------------------
        float level = _shell.Audio.MasterVolume;
        var column = new Rect(area.CenterX - 13, area.Y + 6, 26, area.H - 10);

        // The groove, with everything below the thumb filled in — which is the
        // one thing seven's slider did that XP's did not.
        var groove = new Rect(column.CenterX - 3, column.Y, 6, column.H);
        c.R.RoundedRect(groove, 3, Color.Rgb(0xE4E9EF), Color.Rgb(0xB6C2CE), 1);

        float thumbY = groove.Bottom - level * groove.H;
        if (!_shell.Audio.Muted && level > 0.001f)
            c.R.RoundedRect(new Rect(groove.X + 1, thumbY, groove.W - 2, groove.Bottom - thumbY), 2,
                            Color.Rgb(0x3C9CE8));

        var thumb = new Rect(column.X, thumbY - 6, column.W, 12);
        bool onThumb = c.Hovering(column);
        c.R.RoundedRectV(thumb, 2,
                         onThumb ? Color.Rgb(0xFFFFFF) : Color.Rgb(0xF2F5F8),
                         onThumb ? Color.Rgb(0xD6E6F4) : Color.Rgb(0xDCE3EA),
                         Color.Rgb(0x7A93A8), 1);
        c.R.FillRect(new Rect(thumb.X + 4, thumb.CenterY, thumb.W - 8, 1), Color.Rgb(0xB6C2CE));

        // Dragging anywhere in the column sets the level, which is how a
        // flyout this small has to behave.
        if (c.Hovering(column) && c.In.Pressed(MouseButton.Left)) _draggingVolume = true;
        if (_draggingVolume)
        {
            if (!c.In.IsDown(MouseButton.Left)) _draggingVolume = false;
            else
            {
                SetVolume(c, (groove.Bottom - c.MouseY) / groove.H);
                c.MouseHandled = true;
            }
        }
        if (c.Hovering(panel) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            SetVolume(c, _shell.Audio.MasterVolume + c.In.WheelDelta * 0.05f);
            c.In.WheelDelta = 0;
        }

        c.F.Small.DrawCentered(c.R, _shell.Audio.Muted
                ? L.T("tray.volume_off")
                : L.F("tray.volume_percent", (int)MathF.Round(level * 100)),
            reading, Color.Rgb(0x404040));

        // ---- the mute button -------------------------------------------------
        var box = new Rect(button.CenterX - 13, button.Y + 2, 26, 26);
        bool hotBox = c.Hovering(box);
        c.R.RoundedRectV(box, 3,
                         hotBox ? Color.Rgb(0xEAF4FD) : Color.Rgb(0xF6F8FA),
                         hotBox ? Color.Rgb(0xCDE6FA) : Color.Rgb(0xE4E9EF),
                         Color.Rgb(0x8FA6B8), 1);
        Icons.Draw(c.R, IconId.Volume, new Rect(box.CenterX - 8, box.CenterY - 8, 16, 16));
        if (_shell.Audio.Muted)
            c.R.Line(box.X + 5, box.Y + 5, box.Right - 5, box.Bottom - 5, Color.Rgb(0xD03028), 2f);

        c.Tooltip(box, L.T(_shell.Audio.Muted ? "tray.unmute" : "tray.mute_short"));
        if (c.Clicked(box)) _shell.ToggleMute(c);

        // Anything outside the panel and off the speaker puts it away.
        bool outside = !_draggingVolume
                       && !panel.Contains(c.MouseX, c.MouseY)
                       && !_speaker.Contains(c.MouseX, c.MouseY);
        if ((c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) && outside)
            _volumeOpen = false;
        else if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape))
        {
            _volumeOpen = false;
            c.KeyboardHandled = true;
        }
    }


    // ---- часы и календарь -------------------------------------------------

    /// <summary>The panel the clock drops: a face with three hands, the long
    /// date under it, and the month laid out as a grid with today ringed.
    ///
    /// Seven drew this with an analogue clock and eight kept it flat and square;
    /// this is the pair of them — a round face on a flat white panel, which is
    /// where the two versions actually met.</summary>
    void DrawClockPanel(UiContext c)
    {
        if (!_clockOpen) return;

        var panel = ClockBounds(c);
        var now = _shell.Now;

        c.R.FillRect(panel.Offset(3, 3), Color.Rgba(0x000000, 60));
        c.R.FillRect(panel, Color.Rgb(0xF7F7F7));
        c.R.DrawRect(panel, Color.Rgb(0xA0A0A0));

        var area = panel.Deflate(12);

        // ---- the face -------------------------------------------------------
        var face = area.CutTop(112);
        float cx = face.CenterX, cy = face.CenterY, rad = 48;

        c.R.FillCircle(cx, cy, rad, Color.White);
        c.R.DrawCircle(cx, cy, rad, Color.Rgb(0x9AA4B0), 1.5f);

        for (int i = 0; i < 12; i++)
        {
            float a = i * MathF.PI / 6;
            float inner = i % 3 == 0 ? rad - 9 : rad - 5;
            c.R.Line(cx + MathF.Sin(a) * inner, cy - MathF.Cos(a) * inner,
                     cx + MathF.Sin(a) * (rad - 2), cy - MathF.Cos(a) * (rad - 2),
                     Color.Rgb(0x6A7480), i % 3 == 0 ? 2f : 1f);
        }

        double seconds = now.Second;
        double minutes = now.Minute + seconds / 60.0;
        double hours = now.Hour % 12 + minutes / 60.0;

        Hand(c, cx, cy, hours / 12.0, rad * 0.52f, 3.4f, Color.Rgb(0x2A2A2A));
        Hand(c, cx, cy, minutes / 60.0, rad * 0.76f, 2.4f, Color.Rgb(0x2A2A2A));
        Hand(c, cx, cy, seconds / 60.0, rad * 0.82f, 1.2f, Color.Rgb(0xC03030));
        c.R.FillCircle(cx, cy, 3, Color.Rgb(0x2A2A2A));

        // ---- the date -------------------------------------------------------
        var dateRow = area.CutTop(24);
        c.F.Caption.DrawCentered(c.R, L.Time(now), dateRow, Color.Rgb(0x1A1A1A));
        var longRow = area.CutTop(20);
        c.F.Small.DrawCentered(c.R, L.LongDate(now), longRow, Color.Rgb(0x606060));

        area.CutTop(6);

        // ---- the month ------------------------------------------------------
        var head = area.CutTop(22);
        c.F.UiBold.DrawCentered(c.R, L.MonthAndYear(now), head, Color.Rgb(0x1A1A1A));

        var week = area.CutTop(18);
        float cw = week.W / 7;
        // Monday first: this is a Russian calendar.
        string[] initials = { "пн", "вт", "ср", "чт", "пт", "сб", "вс" };
        for (int i = 0; i < 7; i++)
            c.F.Small.DrawCentered(c.R, initials[i],
                                   new Rect(week.X + i * cw, week.Y, cw, week.H),
                                   i >= 5 ? Color.Rgb(0xB05050) : Color.Rgb(0x808080));

        var first = new DateTime(now.Year, now.Month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7;          // Monday = 0
        int days = DateTime.DaysInMonth(now.Year, now.Month);

        float ch = MathF.Min(24, area.H / 6);
        for (int day = 1; day <= days; day++)
        {
            int cell = offset + day - 1;
            var r = new Rect(area.X + (cell % 7) * cw, area.Y + (cell / 7) * ch, cw, ch);
            if (r.Bottom > area.Bottom) break;

            bool today = day == now.Day;
            bool weekend = cell % 7 >= 5;

            if (today)
            {
                c.R.FillRect(r.Deflate(2), Color.Rgba(0x2D89EF, 40));
                c.R.DrawRect(r.Deflate(2), Theme.MetroAccent);
            }
            else if (c.Hovering(r)) c.R.FillRect(r.Deflate(2), Color.Rgb(0xE8F1FB));

            c.F.Ui.DrawCentered(c.R, day.ToString(), r,
                                today ? Theme.MetroAccent
                                      : weekend ? Color.Rgb(0xB05050) : Color.Rgb(0x2A2A2A));
        }

        // ---- the link seven put at the foot ---------------------------------
        var link = new Rect(panel.X + 12, panel.Bottom - 26, panel.W - 24, 18);
        bool hot = c.Hovering(link);
        c.F.Small.DrawCentered(c.R, L.T("tray.change_date_and_time"), link,
                               hot ? Theme.MetroAccent : Color.Rgb(0x1E4E79));
        if (c.Clicked(link)) { _clockOpen = false; _shell.Launch(c, "clock", null); }

        DismissFlyout(c, panel, _clockAnchor, ref _clockOpen);
    }

    static void Hand(UiContext c, float cx, float cy, double turn, float length,
                     float thickness, Color colour)
    {
        float a = (float)(turn * Math.PI * 2);
        c.R.Line(cx - MathF.Sin(a) * length * 0.18f, cy + MathF.Cos(a) * length * 0.18f,
                 cx + MathF.Sin(a) * length, cy - MathF.Cos(a) * length, colour, thickness);
    }

    // ---- язык ввода -------------------------------------------------------

    /// <summary>What the indicator offers. Two, because the system has two.</summary>
    static readonly (Lang lang, string tag, string nameKey, string layoutKey)[] Languages =
    {
        (Lang.Ru, "РУС", "lang.russian", "lang.russian_layout"),
        (Lang.En, "ENG", "lang.english", "lang.english_layout"),
    };

    /// <summary>The input-method list version 8 put behind the tray indicator:
    /// a flat white card, one row per language with its three-letter tag in
    /// grey down the left, and the current one marked with the accent bar.</summary>
    void DrawLanguagePanel(UiContext c)
    {
        if (!_langOpen) return;

        var panel = LanguageBounds(c);

        // Version 8's switcher was the flattest thing in the whole shell: one
        // white rectangle with a hairline round it, no shadow to speak of, the
        // heading in ordinary weight and the current language marked by a block
        // of the accent colour down its left edge and nothing else. It is the
        // list, drawn as plainly as a list can be drawn.
        c.R.FillRect(panel.Offset(0, 2), Color.Rgba(0x000000, 30));
        c.R.FillRect(panel, Color.Rgb(0xFFFFFF));
        c.R.DrawRect(panel, Color.Rgb(0xCCCCCC));

        var area = panel.Deflate(1);

        var head = area.CutTop(38);
        c.F.Ui.Draw(c.R, L.T("lang.input_method"), head.X + 16,
                    head.CenterY - c.F.Ui.Height * 0.5f, Color.Rgb(0x666666));

        foreach (var (lang, tag, nameKey, layoutKey) in Languages)
        {
            var row = area.CutTop(42);
            bool current = L.Current == lang;
            bool hot = c.Hovering(row);

            if (current) c.R.FillRect(row, Color.Rgb(0xF2F2F2));
            if (hot) c.R.FillRect(row, Color.Rgba(0x000000, 12));

            // The marker: a bar of the accent, and the tag set in it rather
            // than beside it, which is how that flyout read at a glance.
            var tab = new Rect(row.X, row.Y, 36, row.H);
            if (current)
            {
                c.R.FillRect(new Rect(row.X, row.Y, 3, row.H), Theme.MetroAccent);
                c.F.UiBold.DrawCentered(c.R, tag, tab, Theme.MetroAccent);
            }
            else c.F.Ui.DrawCentered(c.R, tag, tab, Color.Rgb(0x999999));

            c.F.Ui.Draw(c.R, L.T(nameKey), row.X + 46, row.Y + 7, Color.Rgb(0x1A1A1A));
            c.F.Small.Draw(c.R, L.T(layoutKey), row.X + 46, row.Y + 9 + c.F.Ui.Height,
                           Color.Rgb(0x999999));

            if (c.Clicked(row))
            {
                L.Current = lang;
                _langOpen = false;
                c.Sound(Sfx.Click, 0.6f);
            }
        }

        // The one link at the foot, in the accent colour, with a hairline over
        // it — the shape version 8 ended every one of these panels with.
        var link = area.CutTop(34);
        bool linkHot = c.Hovering(link);
        c.R.FillRect(new Rect(link.X + 12, link.Y, link.W - 24, 1), Color.Rgb(0xE8E8E8));
        if (linkHot) c.R.FillRect(link, Color.Rgba(0x000000, 10));
        c.F.Ui.Draw(c.R, L.T("lang.preferences"), link.X + 16,
                    link.CenterY - c.F.Ui.Height * 0.5f,
                    linkHot ? Theme.MetroAccent : Color.Rgb(0x4A4A4A));
        if (c.Clicked(link)) { _langOpen = false; _shell.Launch(c, "language", null); }

        DismissFlyout(c, panel, _langAnchor, ref _langOpen);
    }

    /// <summary>A click outside a flyout — or Escape — puts it away. Clicking
    /// the thing it hangs from is left alone, so that click can close it.</summary>
    void DismissFlyout(UiContext c, Rect panel, Rect anchor, ref bool open)
    {
        bool outside = !panel.Contains(c.MouseX, c.MouseY) && !anchor.Contains(c.MouseX, c.MouseY);
        if ((c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) && outside)
            open = false;
        else if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape))
        {
            open = false;
            c.KeyboardHandled = true;
        }
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
            MenuItem.Of(L.T(win.Immersive ? "win.leave_full_screen" : "win.full_screen"),
                        () => _shell.Wm.ToggleImmersive(win, c), shortcut: "F11"),
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
            MenuItem.Of(L.T("start8.start_screen"), () => _shell.Start.Open(c), IconId.Tiles),
            MenuItem.Of(L.T("charm.charms"), () => _shell.Charms.Show(c), IconId.Settings),
            MenuItem.Of(L.T("switch.title"), () => _shell.Switcher.Show(), IconId.Tiles),
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
