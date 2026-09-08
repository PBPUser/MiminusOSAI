using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Z-order, focus, dragging, resizing and window chrome.
///
/// Input runs topmost-first (so the front window wins a click) while painting
/// runs bottom-first (so the front window covers the others). The two are
/// reconciled by hit-testing once per frame and then, during the paint pass,
/// suppressing pointer input for every window that is not the one under the
/// cursor — which lets each window draw and handle its own widgets in a single
/// immediate-mode pass.</summary>
public sealed class WindowManager
{
    readonly List<OsWindow> _windows = new();

    public IReadOnlyList<OsWindow> Windows => _windows;
    public OsWindow Focused { get; private set; }

    public ShellHost Shell;

    enum DragMode { None, Move, ResizeL, ResizeR, ResizeT, ResizeB, ResizeTL, ResizeTR, ResizeBL, ResizeBR }

    DragMode _drag = DragMode.None;
    OsWindow _dragWindow;
    float _dragDX, _dragDY;
    Rect _dragStart;

    OsWindow _mouseWindow;

    /// <summary>The window whose code is running right now — its Tick, its
    /// painting, its menu. A dialog opened from in there belongs to it, which
    /// is how a message box ends up modal to the program that asked for it
    /// rather than to whatever the user happened to bring forward meanwhile.
    /// Null when the shell itself is doing the asking.</summary>
    public OsWindow Running { get; private set; }


    // Outline drag, used when window contents are not shown while dragging.
    Rect _outline;
    bool _hasOutline;

    /// <summary>Where a window would land if it were let go now: half the
    /// screen at an edge, or the whole of it at the top. Empty when the pointer
    /// is nowhere near an edge, which is most of the time.</summary>
    Rect _snapPreview;
    bool _hasSnap;

    /// <summary>Area windows may occupy — the screen minus the taskbar.</summary>
    /// <summary>The primary monitor's free space — what a new window is placed
    /// against and what the desktop uses. A window carried onto the second
    /// monitor asks <see cref="WorkAreaFor"/> instead.</summary>
    public Rect WorkArea;

    /// <summary>The free space of whichever monitor the window is mostly on.
    /// Maximising, snapping and the bounds clamp all go through this, so a
    /// window maximised on the second screen fills the second screen.</summary>
    public Rect WorkAreaFor(UiContext c, Rect r)
        => Shell?.Taskbar?.WorkAreaFor(c, r) ?? WorkArea;

    const float ResizeGrip = 5;

    // ---- lifetime --------------------------------------------------------

    public void Open(OsWindow w, UiContext c)
    {
        w.Wm = this;
        w.Shell = Shell;

        // A dialog belongs to whatever raised it, which is whatever was in
        // front when it appeared. That is what makes it modal to one program
        // instead of to the whole machine: see ModalOver.
        if (w.Modal && w.Owner == null && !w.SystemModal) w.Owner = Running ?? Focused;

        if (w.Bounds.W <= 0) w.Bounds.W = 640;
        if (w.Bounds.H <= 0) w.Bounds.H = 440;

        // Cascade anything that has not asked for a specific spot.
        if (w.Bounds.X == 0 && w.Bounds.Y == 0 && !w.Modal)
        {
            int n = _windows.Count(x => !x.Modal);
            w.Bounds.X = 40 + (n % 8) * 24;
            w.Bounds.Y = 30 + (n % 8) * 24;
        }

        _windows.Add(w);
        w.RestoreBounds = w.Bounds;
        w.OnOpened(c);

        // An immersive program does not get placed: it gets the screen.
        if (w.Immersive)
        {
            w.RestoreBounds = w.Bounds;
            w.Bounds = FullScreen(c);
        }

        // Grown from a small rectangle at its own centre, which is what a
        // window opening has looked like since windows started animating.
        Animate(w, Shrink(w.Bounds, 0.72f), c.Time, 0.17);

        Focus(w);
        c.Sound(Sfx.WindowOpen, 0.55f);
    }

    /// <summary>Starts a window moving from one rectangle to its current one.
    /// Only the drawing moves: as far as input and layout are concerned the
    /// window is already where it is going.</summary>
    void Animate(OsWindow w, Rect from, double now, double length)
    {
        // «Показывать анимацию окон», from the accessibility page: off, and the
        // window is simply where it is going.
        if (Shell != null && !Shell.Settings.Animations) { w.AnimStart = -1; return; }

        w.AnimFrom = from;
        w.AnimStart = now;
        w.AnimLength = length;
    }

    static Rect Shrink(Rect r, float f)
        => new(r.X + r.W * (1 - f) * 0.5f, r.Y + r.H * (1 - f) * 0.5f, r.W * f, r.H * f);

    /// <summary>Where an immersive program sits: the whole screen, taskbar
    /// included, because it is meant to be the only thing on it.</summary>
    static Rect FullScreen(UiContext c) => new(0, 0, c.ScreenW, c.ScreenH);

    /// <summary>Puts a window in and out of full screen, with the growing and
    /// shrinking that makes the change readable.</summary>
    public void ToggleImmersive(OsWindow w, UiContext c)
    {
        if (w == null) return;

        if (w.Immersive)
        {
            if (Snapped == w) Snapped = null;
            w.Immersive = false;
            var from = w.Bounds;
            w.Bounds = w.RestoreBounds.W > 40 ? w.RestoreBounds : Shrink(WorkArea, 0.6f);
            w.State = WindowState.Normal;
            Animate(w, from, c.Time, 0.2);
            c.Sound(Sfx.Restore, 0.5f);
        }
        else
        {
            w.RestoreBounds = w.Bounds;
            w.Immersive = true;
            var from = w.Bounds;
            w.Bounds = FullScreen(c);
            Animate(w, from, c.Time, 0.2);
            c.Sound(Sfx.Restore, 0.55f, 1.15f);
        }
        Focus(w);
    }

    /// <summary>The topmost program running full screen, or null. The shell
    /// asks so it knows to leave the taskbar off this frame.</summary>
    public OsWindow TopImmersive
    {
        get
        {
            for (int i = _windows.Count - 1; i >= 0; i--)
                if (_windows[i].Immersive && _windows[i].State != WindowState.Minimized)
                    return _windows[i];
            return null;
        }
    }

    // ---- two programs at once, the version 8 way -----------------------------
    //
    // A full-screen program is the screen. Version 8's answer to wanting two of
    // them was to put one in a column down one side and give the rest to the
    // other, with a bar between them that could be dragged: not two windows —
    // two full-screen programs, sharing the screen edge to edge.
    //
    // That is what this is. The snapped one is remembered here, the one filling
    // the rest is whatever else is on top, and the divider between them is the
    // only piece of chrome either of them gets.

    /// <summary>The program in the narrow column, or null when one program has
    /// the screen to itself.</summary>
    public OsWindow Snapped { get; private set; }

    /// <summary>Which side the column is on.</summary>
    public bool SnappedLeft { get; private set; } = true;

    /// <summary>How wide the column is, as a fraction of the screen. Version 8
    /// gave exactly 320 pixels; 8.1 let it be dragged, and this is 8.1.</summary>
    public float SnapFraction = 0.32f;

    const float DividerW = 6;

    /// <summary>The program filling whatever the column leaves — the topmost
    /// full-screen one that is not the snapped one.</summary>
    public OsWindow Filling
    {
        get
        {
            if (Snapped == null) return null;
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var w = _windows[i];
                if (w != Snapped && w.Immersive && w.State != WindowState.Minimized) return w;
            }
            return null;
        }
    }

    public bool SideBySide => Snapped != null && Filling != null;

    /// <summary>Puts a program into the column, or takes it out again. Asking
    /// for the one already there swaps the sides, then lets it go — which is
    /// what pressing the key twice more does in the original.</summary>
    public void SnapImmersive(OsWindow w, UiContext c, bool left)
    {
        if (w == null || !w.Immersive) return;

        if (Snapped == w && SnappedLeft == left) { Snapped = null; }
        else { Snapped = w; SnappedLeft = left; }

        LayOutImmersive(c);
        c.Sound(Sfx.Snap, 0.7f);
    }

    public void UnsnapImmersive(UiContext c)
    {
        if (Snapped == null) return;
        Snapped = null;
        LayOutImmersive(c);
    }

    /// <summary>Gives the two of them their halves of the screen, or gives the
    /// whole thing back when there is only one.</summary>
    public void LayOutImmersive(UiContext c)
    {
        // A snapped program that has gone away takes the arrangement with it.
        if (Snapped != null && (Snapped.Closed || !Snapped.Immersive)) Snapped = null;

        var full = FullScreen(c);

        if (!SideBySide)
        {
            foreach (var w in _windows)
                if (w.Immersive && w.State != WindowState.Minimized) w.Bounds = full;
            return;
        }

        SnapFraction = Math.Clamp(SnapFraction, 0.22f, 0.6f);
        float column = MathF.Round(full.W * SnapFraction);

        var narrow = SnappedLeft
            ? new Rect(full.X, full.Y, column, full.H)
            : new Rect(full.Right - column, full.Y, column, full.H);

        var wide = SnappedLeft
            ? new Rect(full.X + column + DividerW, full.Y, full.W - column - DividerW, full.H)
            : new Rect(full.X, full.Y, full.W - column - DividerW, full.H);

        Snapped.Bounds = narrow;
        Filling.Bounds = wide;

        // Anything else running full screen is behind both of them and keeps
        // the whole screen, so it is there when the arrangement ends.
        foreach (var w in _windows)
            if (w.Immersive && w != Snapped && w != Filling && w.State != WindowState.Minimized)
                w.Bounds = full;
    }

    /// <summary>The bar between two snapped programs, and the drag that moves
    /// it. It is drawn over both of them, after both of them.</summary>
    bool _draggingDivider;

    public void DrawSnapDivider(UiContext c)
    {
        if (!SideBySide) return;

        var full = FullScreen(c);
        float column = MathF.Round(full.W * SnapFraction);
        float x = SnappedLeft ? full.X + column : full.Right - column - DividerW;
        var bar = new Rect(x, full.Y, DividerW, full.H);

        bool hot = c.Hovering(new Rect(bar.X - 3, bar.Y, bar.W + 6, bar.H));
        c.R.FillRect(bar, hot ? Color.Rgb(0x505050) : Color.Rgb(0x2A2A2A));

        // Three dots down the middle of it, which is all the grip it ever had.
        for (int i = -1; i <= 1; i++)
            c.R.FillRect(new Rect(bar.CenterX - 1, bar.CenterY + i * 8 - 1, 2, 2),
                         Color.Rgba(0xFFFFFF, 200));

        if (hot) c.Cursor = CursorShape.SizeWE;

        if (hot && c.In.Pressed(MouseButton.Left)) { _draggingDivider = true; c.MouseHandled = true; }
        if (!_draggingDivider) return;

        if (!c.In.IsDown(MouseButton.Left))
        {
            _draggingDivider = false;
            c.Sound(Sfx.Snap, 0.5f);
            return;
        }

        c.Cursor = CursorShape.SizeWE;
        c.MouseHandled = true;

        float wanted = SnappedLeft ? (c.MouseX - full.X) / full.W
                                   : (full.Right - c.MouseX) / full.W;

        // Dragged all the way past the middle, the two of them change places —
        // which is the gesture that made the divider worth having.
        if (wanted > 0.62f)
        {
            SnappedLeft = !SnappedLeft;
            SnapFraction = 0.32f;
        }
        else SnapFraction = Math.Clamp(wanted, 0.22f, 0.6f);

        LayOutImmersive(c);
    }

    /// <summary>Raised as a window is retired, so the registry can count down
    /// the assembly the window came from.</summary>
    public Action<OsWindow> WindowClosed;

    public void Focus(OsWindow w)
    {
        if (w == null || !_windows.Contains(w)) return;
        _windows.Remove(w);
        _windows.Add(w);
        if (Focused != w)
        {
            Focused = w;
            w.OnActivated();
        }
        if (w.State == WindowState.Minimized) w.State = WindowState.Normal;
    }

    public void RequestClose(OsWindow w, UiContext c)
    {
        if (w.OnClosing(c)) w.Close();
    }

    public T Find<T>() where T : OsWindow => _windows.OfType<T>().FirstOrDefault();

    /// <summary>The modal dialog standing in front of <paramref name="w"/>, or
    /// null when nothing is.
    ///
    /// A message box belongs to the program that raised it and stops that
    /// program only: the rest of the machine carries on, so a question left
    /// open in the antivirus does not freeze the folder window behind it. The
    /// two exceptions are a dialog that never learnt who owns it — nobody was
    /// in front when it appeared — and one that says it speaks for the whole
    /// machine, like the shutdown box. Both of those still block everything.</summary>
    public OsWindow ModalOver(OsWindow w)
    {
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var m = _windows[i];
            if (m == w || !m.Modal || m.State == WindowState.Minimized) continue;
            if (m.SystemModal || m.Owner == null || m.Owner == w) return m;
        }
        return null;
    }

    public void Minimize(OsWindow w, UiContext c)
    {
        if (!w.Minimizable) return;

        // Down to the bar it came from, as a shrinking outline.
        Ghost(w.Bounds, new Rect(w.Bounds.CenterX - 40, c.ScreenH - 6, 80, 6), c.Time, w.Icon);

        w.State = WindowState.Minimized;
        if (Focused == w)
        {
            Focused = _windows.LastOrDefault(x => x != w && x.State != WindowState.Minimized);
            Focused?.OnActivated();
        }
        c.Sound(Sfx.Minimize, 0.5f);
    }

    public void ToggleMaximize(OsWindow w, UiContext c)
    {
        if (!w.Maximizable) return;
        var was = w.Bounds;
        if (w.State == WindowState.Maximized)
        {
            w.Bounds = w.RestoreBounds;
            w.State = WindowState.Normal;
            c.Sound(Sfx.Restore, 0.45f);
        }
        else
        {
            w.RestoreBounds = w.Bounds;
            w.Bounds = WorkAreaFor(c, w.Bounds);
            w.State = WindowState.Maximized;
            c.Sound(Sfx.Restore, 0.5f, 1.15f);
        }
        Animate(w, was, c.Time, 0.15);
    }

    public void RestoreOrFocus(OsWindow w, UiContext c)
    {
        if (w.State == WindowState.Minimized)
        {
            w.State = WindowState.Normal;
            // Back up out of the taskbar, the way it went down into it.
            Animate(w, new Rect(w.Bounds.CenterX - 40, c.ScreenH - 6, 80, 6), c.Time, 0.16);
            c.Sound(Sfx.Restore, 0.5f);
            Focus(w);
        }
        else if (Focused == w) Minimize(w, c);
        else Focus(w);
    }

    public void MinimizeAll(UiContext c)
    {
        foreach (var w in _windows.Where(x => x.Minimizable && x.State != WindowState.Minimized))
            w.State = WindowState.Minimized;
        Focused = null;
        c.Sound(Sfx.Minimize, 0.6f);
    }

    public void CloseAll(UiContext c)
    {
        foreach (var w in _windows.ToList()) w.Close();
    }

    // ---- geometry --------------------------------------------------------

    public Rect CaptionRect(OsWindow w, Theme t) => new(w.Bounds.X, w.Bounds.Y, w.Bounds.W, t.CaptionHeight);

    public Rect ClientRect(OsWindow w, Theme t)
    {
        float f = t.FrameThickness;
        return new Rect(w.Bounds.X + f,
                        w.Bounds.Y + t.CaptionHeight,
                        w.Bounds.W - f * 2,
                        w.Bounds.H - t.CaptionHeight - f);
    }

    DragMode HitEdge(OsWindow w, float mx, float my)
    {
        if (!w.Resizable || w.State == WindowState.Maximized) return DragMode.None;
        var b = w.Bounds;
        bool l = mx >= b.X - 2 && mx < b.X + ResizeGrip;
        bool r = mx <= b.Right + 2 && mx > b.Right - ResizeGrip;
        bool tp = my >= b.Y - 2 && my < b.Y + 3;
        bool bt = my <= b.Bottom + 2 && my > b.Bottom - ResizeGrip;

        if (l && tp) return DragMode.ResizeTL;
        if (r && tp) return DragMode.ResizeTR;
        if (l && bt) return DragMode.ResizeBL;
        if (r && bt) return DragMode.ResizeBR;
        if (l) return DragMode.ResizeL;
        if (r) return DragMode.ResizeR;
        if (tp) return DragMode.ResizeT;
        if (bt) return DragMode.ResizeB;
        return DragMode.None;
    }

    static CursorShape CursorFor(DragMode m) => m switch
    {
        DragMode.ResizeL or DragMode.ResizeR => CursorShape.SizeWE,
        DragMode.ResizeT or DragMode.ResizeB => CursorShape.SizeNS,
        DragMode.ResizeTL or DragMode.ResizeBR => CursorShape.SizeNWSE,
        DragMode.ResizeTR or DragMode.ResizeBL => CursorShape.SizeNESW,
        _ => CursorShape.Arrow,
    };

    // ---- frame -----------------------------------------------------------

    public void Update(UiContext c, bool blockWindows = false)
    {
        // An auto-hiding taskbar gives its strip back to the windows.
        // Whatever the taskbar is not standing on. It knows which edge it is
        // against, so a bar up the side of the screen takes width rather than
        // height and maximised windows follow it there.
        WorkArea = Shell?.Taskbar.WorkArea(c)
                   ?? new Rect(0, 0, c.ScreenW, c.ScreenH - c.Theme.TaskbarHeight);

        // Retire closed windows first so nothing draws a dead window.
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            if (_windows[i].Closed)
            {
                var dead = _windows[i];
                _windows.RemoveAt(i);
                // Widget state is keyed by window id and would otherwise keep a
                // reference into the program's assembly alive.
                c.ForgetState(dead.Id);
                dead.OnClosed();
                Ghost(dead.Bounds, Shrink(dead.Bounds, 0.55f), c.Time, dead.Icon);
                WindowClosed?.Invoke(dead);
                c.Sound(Sfx.WindowClose, 0.5f);
                if (Focused == dead) Focused = null;
                if (_dragWindow == dead) { _dragWindow = null; _drag = DragMode.None; }
            }
        }
        if (Focused == null)
            Focused = _windows.LastOrDefault(x => x.State != WindowState.Minimized);

        // Maximised windows follow the work area as it changes.
        // A maximised window follows the work area of its own monitor, so
        // one on the second screen stays on the second screen.
        foreach (var w in _windows)
            if (w.State == WindowState.Maximized) w.Bounds = WorkAreaFor(c, w.Bounds);

        // The two full-screen programs sharing the screen keep their halves as
        // the screen changes, and give them back when one of them goes.
        LayOutImmersive(c);

        foreach (var w in _windows.ToList())
        {
            Running = w;
            w.Tick(c, c.Dt);
        }
        Running = null;

        HitTest(c, blockWindows);
        UpdateDrag(c);
    }

    void HitTest(UiContext c, bool blockWindows)
    {
        if (_drag != DragMode.None && _dragWindow != null)
        {
            _mouseWindow = _dragWindow;
            return;
        }

        _mouseWindow = null;
        if (c.MouseHandled || blockWindows) return;

        // The window under the pointer, and the dialog standing in front of it
        // if one is — a blocked window is not clicked, it is knocked on.
        OsWindow blocked = null, blocker = null;
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (w.State == WindowState.Minimized) continue;
            if (!w.Bounds.Inflate(ResizeGrip).Contains(c.MouseX, c.MouseY)) continue;

            var modal = ModalOver(w);
            if (modal != null) { blocked = w; blocker = modal; break; }

            _mouseWindow = w;
            break;
        }

        // Clicking a window (anywhere) brings it to the front.
        if (_mouseWindow != null && c.In.Pressed(MouseButton.Left) && Focused != _mouseWindow)
            Focus(_mouseWindow);

        // A modal dialog swallows the click aimed at its owner and answers for
        // it: the dialog comes to the front, and the machine says no.
        if (blocked != null && c.In.Pressed(MouseButton.Left))
        {
            c.MouseHandled = true;
            Focus(blocker);
            c.Sound(Sfx.Error, 0.35f);
        }
    }

    /// <summary>Aero Snap: a window carried against the left or right edge of
    /// the screen takes half of it, and one carried against the top takes all
    /// of it. The window has to be draggable to a size for this to mean
    /// anything, so a fixed-size dialog is left alone.</summary>
    bool SnapTarget(UiContext c, OsWindow w, out Rect target)
    {
        target = default;
        if (!w.Resizable || !w.Maximizable || w.Immersive) return false;

        const float Reach = 6;
        // Snapping happens on whichever monitor the pointer is over, and to
        // that monitor's own edges: carrying a window to the seam between two
        // screens snaps it against the seam, not across it.
        var area = Shell != null
            ? Shell.Taskbar.WorkAreaOf(c, Shell.Displays.At(c.ScreenW, c.ScreenH, c.MouseX, c.MouseY))
            : WorkArea;

        if (c.MouseY <= area.Y + Reach)
        {
            target = area;
            return true;
        }
        if (c.MouseX <= area.X + Reach)
        {
            target = new Rect(area.X, area.Y, MathF.Round(area.W * 0.5f), area.H);
            return true;
        }
        if (c.MouseX >= area.Right - Reach - 1)
        {
            float half = MathF.Round(area.W * 0.5f);
            target = new Rect(area.Right - half, area.Y, half, area.H);
            return true;
        }
        return false;
    }

    /// <summary>The keyboard half of Snap: Win+Left, Win+Right, Win+Up and
    /// Win+Down move the focused window between the halves, the whole screen
    /// and the size it had before.</summary>
    public void SnapFocused(UiContext c, int dx, int dy)
    {
        var w = Focused;
        if (w == null || !w.Resizable || !w.Maximizable || w.Immersive) return;

        var area = WorkAreaFor(c, w.Bounds);
        float half = MathF.Round(area.W * 0.5f);
        var from = w.Bounds;

        if (dy < 0)
        {
            if (w.State != WindowState.Maximized) w.RestoreBounds = w.Bounds;
            w.Bounds = area;
            w.State = WindowState.Maximized;
        }
        else if (dy > 0)
        {
            if (w.State == WindowState.Maximized && w.RestoreBounds.W > 40) w.Bounds = w.RestoreBounds;
            else { Minimize(w, c); return; }
            w.State = WindowState.Normal;
        }
        else
        {
            if (w.State != WindowState.Maximized) w.RestoreBounds = w.Bounds;
            w.Bounds = dx < 0
                ? new Rect(area.X, area.Y, half, area.H)
                : new Rect(area.Right - half, area.Y, half, area.H);
            w.State = WindowState.Normal;
        }

        Animate(w, from, c.Time, 0.14);
        c.Sound(Sfx.Snap, 0.7f);
    }

    void UpdateDrag(UiContext c)
    {
        if (_drag == DragMode.None || _dragWindow == null) return;

        if (!c.In.IsDown(MouseButton.Left))
        {
            // A window let go against an edge takes the shape that was being
            // shown to it, which is the whole of Snap.
            if (_hasSnap && _dragWindow != null)
            {
                var target = _snapPreview;
                var from = _dragWindow.Bounds;

                if (_dragWindow.State != WindowState.Maximized)
                    _dragWindow.RestoreBounds = _dragStart;

                _dragWindow.State = Same(target, WorkAreaFor(c, target))
                    ? WindowState.Maximized : WindowState.Normal;
                _dragWindow.Bounds = target;
                Animate(_dragWindow, from, c.Time, 0.14);
                c.Sound(Sfx.Snap, 0.7f);
            }
            // Commit an outline drag when the button comes up.
            else if (_hasOutline && _dragWindow != null)
            {
                _dragWindow.Bounds.X = _outline.X;
                _dragWindow.Bounds.Y = _outline.Y;
            }

            _hasOutline = false;
            _hasSnap = false;
            _drag = DragMode.None;
            _dragWindow = null;
            c.ActiveDrag = null;
            return;
        }

        c.MouseHandled = true;
        c.ActiveDrag = "wm.drag";
        var w = _dragWindow;

        if (_drag == DragMode.Move)
        {
            _hasSnap = SnapTarget(c, w, out _snapPreview);

            // With "show window contents while dragging" off, only an outline
            // follows the pointer and the window jumps at the end.
            if (Shell != null && !Shell.Settings.ShowWindowContentsWhileDragging)
            {
                _outline = new Rect(MathF.Round(c.MouseX - _dragDX), MathF.Round(c.MouseY - _dragDY),
                                    w.Bounds.W, w.Bounds.H);
                _hasOutline = true;
                c.Cursor = CursorShape.Move;
                return;
            }

            w.Bounds.X = MathF.Round(c.MouseX - _dragDX);
            w.Bounds.Y = MathF.Round(c.MouseY - _dragDY);
            // Keep at least a strip of caption reachable.
            w.Bounds.Y = Math.Clamp(w.Bounds.Y, WorkArea.Y - 2, WorkArea.Bottom - 24);
            w.Bounds.X = Math.Clamp(w.Bounds.X, -w.Bounds.W + 80, c.ScreenW - 80);
            c.Cursor = CursorShape.Move;
            return;
        }

        c.Cursor = CursorFor(_drag);
        var b = _dragStart;
        float minW = w.MinWidth, minH = w.MinHeight;

        if (_drag is DragMode.ResizeL or DragMode.ResizeTL or DragMode.ResizeBL)
        {
            float right = b.Right;
            float x = MathF.Min(c.MouseX - _dragDX, right - minW);
            w.Bounds.X = MathF.Round(x);
            w.Bounds.W = MathF.Round(right - x);
        }
        if (_drag is DragMode.ResizeR or DragMode.ResizeTR or DragMode.ResizeBR)
            w.Bounds.W = MathF.Round(MathF.Max(minW, c.MouseX - _dragDX - b.X));

        if (_drag is DragMode.ResizeT or DragMode.ResizeTL or DragMode.ResizeTR)
        {
            float bottom = b.Bottom;
            float y = MathF.Min(c.MouseY - _dragDY, bottom - minH);
            w.Bounds.Y = MathF.Round(MathF.Max(-2, y));
            w.Bounds.H = MathF.Round(bottom - w.Bounds.Y);
        }
        if (_drag is DragMode.ResizeB or DragMode.ResizeBL or DragMode.ResizeBR)
            w.Bounds.H = MathF.Round(MathF.Max(minH, c.MouseY - _dragDY - b.Y));
    }

    // Reused so the per-frame snapshot below costs no allocation.
    readonly List<OsWindow> _drawOrder = new();

    /// <summary>Paints every visible window from back to front.
    ///
    /// Iterates a snapshot: a window's own drawing may open a dialog, close
    /// itself or take focus, and all three mutate the window list. Anything
    /// added mid-frame simply appears on the next one.</summary>
    /// <summary>A window that has gone: the rectangle it was, the rectangle it
    /// is heading for, and when it left. Drawn as an outline for a fifth of a
    /// second, which is the whole of the closing animation — there is nothing
    /// left to draw properly by then.</summary>
    readonly List<(Rect from, Rect to, double at, IconId icon)> _ghosts = new();

    void Ghost(Rect from, Rect to, double now, IconId icon)
    {
        if (Shell != null && !Shell.Settings.Animations) return;
        if (_ghosts.Count > 8) _ghosts.RemoveAt(0);
        _ghosts.Add((from, to, now, icon));
    }

    const double GhostLength = 0.18;

    /// <summary>Ease-out: fast at the start, settling at the end. Every
    /// animation in the shell uses this one curve.</summary>
    static float Ease(float t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - (1 - t) * (1 - t) * (1 - t);
    }

    static Rect Lerp(Rect a, Rect b, float t) => new(
        a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t,
        a.W + (b.W - a.W) * t, a.H + (b.H - a.H) * t);

    void DrawGhosts(UiContext c)
    {
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var (from, to, at, icon) = _ghosts[i];
            float t = (float)((c.Time - at) / GhostLength);
            if (t >= 1) { _ghosts.RemoveAt(i); continue; }

            var r = Lerp(from, to, Ease(t));
            byte alpha = (byte)(150 * (1 - t));
            c.R.FillRect(r, Color.Rgba(0xFFFFFF, (byte)(alpha / 4)));
            c.R.DrawRect(r, Color.Rgba(0x000000, alpha));
            if (r.W > 40 && r.H > 40)
                Icons.Draw(c.R, icon, new Rect(r.CenterX - 12, r.CenterY - 12, 24, 24));
        }
    }

    public void Draw(UiContext c)
    {
        bool savedMouse = c.MouseHandled;

        _drawOrder.Clear();
        _drawOrder.AddRange(_windows);

        foreach (var w in _drawOrder)
        {
            if (w.Closed) continue;
            if (w.State == WindowState.Minimized) continue;

            bool mine = w == _mouseWindow;
            c.MouseHandled = savedMouse || !mine;
            bool savedKb = c.KeyboardHandled;
            c.KeyboardHandled = savedKb || w != Focused;

            DrawWindow(c, w);

            c.KeyboardHandled = savedKb;
        }

        DrawGhosts(c);

        // Where the window would land, shown while it is still being carried.
        if (_hasSnap)
        {
            c.R.FillRect(_snapPreview, Color.Rgba(0x2D89EF, 60));
            c.R.DrawRect(_snapPreview, Color.Rgba(0xFFFFFF, 200), 2);
        }

        // The drag outline is drawn over the windows it will land among.
        if (_hasOutline)
        {
            for (int i = 0; i < 3; i++)
                c.R.DrawRect(_outline.Inflate(i), Color.Rgba(0xFFFFFF, 120));
            c.R.DrawRect(_outline, Color.Rgba(0x000000, 160));
        }

        c.MouseHandled = savedMouse || _mouseWindow != null;
    }

    void DrawWindow(UiContext c, OsWindow w)
    {
        var t = c.Theme;
        var g = c.R;

        // While a window is still growing it is drawn at an interpolated
        // rectangle. Its real bounds are already final — input and layout use
        // those — so the whole animation lives inside this one swap.
        Rect real = w.Bounds;
        Rect drawn = real;
        bool animating = w.AnimStart >= 0 && c.Time - w.AnimStart < w.AnimLength;
        if (animating)
        {
            float t01 = (float)((c.Time - w.AnimStart) / w.AnimLength);
            drawn = Lerp(w.AnimFrom, real, Ease(t01));
            w.Bounds = drawn;
        }
        else w.AnimStart = -1;

        if (w.Immersive) DrawImmersive(c, w, animating);
        else DrawFramed(c, w, animating);

        // Only put the real rectangle back if the window is still standing
        // where it was put: a program that resized itself while drawing — the
        // task manager opening up, say — means it, and its own bounds win.
        if (animating && Same(w.Bounds, drawn)) w.Bounds = real;
    }

    static bool Same(Rect a, Rect b)
        => MathF.Abs(a.X - b.X) < 0.01f && MathF.Abs(a.Y - b.Y) < 0.01f
        && MathF.Abs(a.W - b.W) < 0.01f && MathF.Abs(a.H - b.H) < 0.01f;

    /// <summary>A full-screen program: no frame, no caption, and a slim bar
    /// that drops out of the top edge when the pointer goes looking for it —
    /// which is where version 8 hid the only way out of one.</summary>
    void DrawImmersive(UiContext c, OsWindow w, bool animating)
    {
        var b = w.Bounds;
        c.R.FillRect(b, c.Theme.Face);

        var content = b;
        if (w.Menu != null)
        {
            float mh = w.Menu.Height(c);
            var mbar = new Rect(b.X, b.Y, b.W, mh);
            w.Menu.Draw(c, mbar, Shell.Menus, w);
            content = new Rect(b.X, mbar.Bottom, b.W, b.H - mh);
        }

        c.R.PushClip(content);
        Running = w;
        w.DrawClient(c, content);
        Running = null;
        c.R.PopClip();

        if (animating) return;

        // The bar itself, shown while the pointer is near the top.
        bool near = c.MouseY <= 34 && !c.MouseHandled;
        if (!near) return;

        var bar = new Rect(b.X, b.Y, b.W, 30);
        c.R.FillRect(bar, Color.Rgba(0x1C1C1C, 235));
        Icons.Draw(c.R, w.Icon, new Rect(bar.X + 8, bar.CenterY - 9, 18, 18));
        c.F.Ui.Draw(c.R, w.Title, bar.X + 34, bar.CenterY - c.F.Ui.Height * 0.5f, Color.White);

        var close = new Rect(bar.Right - 40, bar.Y, 40, bar.H);
        if (c.Hovering(close)) c.R.FillRect(close, Color.Rgb(0xE81123));
        c.R.Line(close.CenterX - 5, close.CenterY - 5, close.CenterX + 5, close.CenterY + 5,
                 Color.White, 1.8f);
        c.R.Line(close.CenterX + 5, close.CenterY - 5, close.CenterX - 5, close.CenterY + 5,
                 Color.White, 1.8f);

        var restore = new Rect(close.X - 40, bar.Y, 40, bar.H);
        if (c.Hovering(restore)) c.R.FillRect(restore, Color.Rgba(0xFFFFFF, 40));
        c.R.DrawRect(new Rect(restore.CenterX - 6, restore.CenterY - 5, 12, 10), Color.White);

        c.Tooltip(restore, Sys.L.T("win.leave_full_screen"));

        if (c.Clicked(close)) RequestClose(w, c);
        else if (c.Clicked(restore)) ToggleImmersive(w, c);
    }

    void DrawFramed(UiContext c, OsWindow w, bool animating)
    {
        var t = c.Theme;
        var g = c.R;
        var b = w.Bounds;
        bool active = w == Focused;

        // Drop shadow.
        if (t.Id == ThemeId.Metro)
        {
            for (int i = 5; i >= 1; i--)
                g.FillRect(b.Inflate(i), Color.Rgba(0x000000, (byte)(6 + (active ? 4 : 0))));
        }
        else if (t.Id == ThemeId.Seven)
        {
            for (int i = 6; i >= 1; i--)
                g.RoundedRect(b.Inflate(i), t.CornerRadius + i, Color.Rgba(0x000000, (byte)(7 + (active ? 4 : 0))));
        }
        else
        {
            g.FillRect(new Rect(b.X + 4, b.Bottom, b.W, 4), Color.Rgba(0x000000, 40));
            g.FillRect(new Rect(b.Right, b.Y + 4, 4, b.H), Color.Rgba(0x000000, 40));
        }

        // Frame: rounded on top, square at the bottom, like Luna.
        Color frameCol = active ? t.FrameOuter : t.FrameOuter.WithAlpha((byte)150);
        if (t.CornerRadius > 0)
        {
            g.RoundedRect(new Rect(b.X, b.Y, b.W, b.H), t.CornerRadius, frameCol);
            g.FillRect(new Rect(b.X, b.Y + t.CornerRadius, b.W, b.H - t.CornerRadius), frameCol);
        }
        else g.FillRect(b, frameCol);

        // Caption.
        var cap = CaptionRect(w, t);
        DrawCaption(c, w, cap, active);

        // Client.
        var client = ClientRect(w, t);
        g.FillRect(client, t.Face);

        // Resize edge feedback for the window under the pointer.
        if (w == _mouseWindow && _drag == DragMode.None && !c.MouseHandled)
        {
            var edge = HitEdge(w, c.MouseX, c.MouseY);
            if (edge != DragMode.None)
            {
                c.Cursor = CursorFor(edge);
                if (c.In.Pressed(MouseButton.Left))
                {
                    _drag = edge;
                    _dragWindow = w;
                    _dragStart = w.Bounds;
                    _dragDX = edge is DragMode.ResizeL or DragMode.ResizeTL or DragMode.ResizeBL
                        ? c.MouseX - b.X : c.MouseX - b.Right;
                    _dragDY = edge is DragMode.ResizeT or DragMode.ResizeTL or DragMode.ResizeTR
                        ? c.MouseY - b.Y : c.MouseY - b.Bottom;
                    c.MouseHandled = true;
                    Focus(w);
                }
            }
        }

        // Menu bar, then whatever the window itself draws.
        var contentArea = client;
        if (w.Menu != null)
        {
            float mh = w.Menu.Height(c);
            var mbar = new Rect(client.X, client.Y, client.W, mh);
            w.Menu.Draw(c, mbar, Shell.Menus, w);
            contentArea = new Rect(client.X, mbar.Bottom, client.W, client.H - mh);
        }

        g.PushClip(contentArea);
        Running = w;
        w.DrawClient(c, contentArea);
        Running = null;
        g.PopClip();
    }

    void DrawCaption(UiContext c, OsWindow w, Rect cap, bool active)
    {
        var t = c.Theme;
        var g = c.R;

        Color top = active ? t.CaptionActiveTop : t.CaptionInactiveTop;
        Color mid = active ? t.CaptionActiveMid : t.CaptionInactiveMid;
        Color bot = active ? t.CaptionActiveBottom : t.CaptionInactiveBottom;

        // A window that names its own colour gets it, flat, while it has the
        // focus — which is what the Office of 2013 did to its window chrome.
        bool tinted = active && w.CaptionTint is { } tint;
        if (tinted)
        {
            top = mid = bot = w.CaptionTint.Value;
        }

        // Rounded top corners; the bottom of the caption stays square.
        float rad = t.CornerRadius;
        if (rad > 0)
        {
            g.RoundedRectV(new Rect(cap.X, cap.Y, cap.W, rad * 2), rad, top, mid);
            g.FillRectV(new Rect(cap.X, cap.Y + rad, cap.W, cap.H - rad), mid, bot);
            g.FillRectV(new Rect(cap.X, cap.Y + rad * 0.6f, cap.W, rad * 0.4f), top, mid);
        }
        else g.FillRectV(cap, top, bot);

        if (tinted)
        {
            // Nothing over it: the whole point of that chrome was that it was
            // one flat colour.
        }
        else if (t.GlassCaption)
        {
            // A soft highlight across the top half sells the glass look.
            g.FillRectV(new Rect(cap.X + 1, cap.Y + 1, cap.W - 2, cap.H * 0.45f),
                        Color.Rgba(0xFFFFFF, 130), Color.Rgba(0xFFFFFF, 20));
        }
        else if (t.Id != ThemeId.Metro)
        {
            g.FillRect(new Rect(cap.X + rad, cap.Y + 1, cap.W - rad * 2, 1), Color.Rgba(0xFFFFFF, 90));
        }

        float pad = 6;
        var iconRect = new Rect(cap.X + pad, cap.Y + (cap.H - 16) * 0.5f, 16, 16);

        // A window with tabs in its title bar has no room for an icon, and no
        // use for one: the tab in front says what the window is.
        if (!w.CaptionTabs) Icons.Draw(g, w.Icon, iconRect);

        // Buttons, right to left: close, maximise, minimise. «Миминус 8» runs
        // them the full height of the caption and hard into its corner, which
        // is what makes the close button a target you cannot miss.
        bool metro = t.Id == ThemeId.Metro || tinted;
        float bs = metro ? cap.H : t.CaptionHeight - 9;
        float bw = metro ? 30 : bs;
        float gap = metro ? 0 : 2;
        float by = metro ? cap.Y : cap.Y + (cap.H - bs) * 0.5f;
        float bx = cap.Right - (metro ? 0 : pad) - bw;

        var closeRect = new Rect(bx, by, bw, bs);
        if (CaptionButton(c, w.Id + ".close", closeRect, CaptionGlyph.Close, active, true, tinted))
            RequestClose(w, c);

        if (w.Maximizable)
        {
            bx -= bw + gap;
            var maxRect = new Rect(bx, by, bw, bs);
            if (CaptionButton(c, w.Id + ".max", maxRect,
                              w.State == WindowState.Maximized ? CaptionGlyph.Restore : CaptionGlyph.Maximize,
                              active, false, tinted))
                ToggleMaximize(w, c);
        }
        if (w.Minimizable)
        {
            bx -= bw + gap;
            var minRect = new Rect(bx, by, bw, bs);
            if (CaptionButton(c, w.Id + ".min", minRect, CaptionGlyph.Minimize, active, false, tinted))
                Minimize(w, c);
        }

        // Either the window's own strip across the caption, or the title.
        if (w.CaptionTabs)
        {
            var strip = new Rect(cap.X + 2, cap.Y, MathF.Max(0, bx - cap.X - 4), cap.H);
            if (strip.W > 20)
            {
                g.PushClip(strip);
                w.DrawCaptionTabs(c, strip);
                g.PopClip();
            }
        }
        else
        {
            // Title text, clipped to whatever room is left.
            var textArea = new Rect(iconRect.Right + 5, cap.Y, bx - iconRect.Right - 10, cap.H);
            if (textArea.W > 10)
            {
                g.PushClip(textArea);
                string title = c.F.Caption.Ellipsize(w.Title, textArea.W);
                float ty = textArea.Y + (textArea.H - c.F.Caption.Height) * 0.5f;
                if (t.CaptionTextShadow.A > 0 && !tinted)
                    c.F.Caption.Draw(g, title, textArea.X + 1, ty + 1, t.CaptionTextShadow);
                c.F.Caption.Draw(g, title, textArea.X, ty,
                                 tinted ? Color.White
                                 : active ? t.CaptionTextActive : t.CaptionTextInactive);
                g.PopClip();
            }
        }

        // Dragging by the caption, and double-click to maximise.
        var dragZone = new Rect(cap.X, cap.Y, MathF.Max(0, bx - cap.X), cap.H);
        if (c.DoubleClicked(dragZone)) ToggleMaximize(w, c);
        else if (c.Clicked(dragZone) && w.State != WindowState.Maximized)
        {
            _drag = DragMode.Move;
            _dragWindow = w;
            _dragStart = w.Bounds;
            _dragDX = c.MouseX - w.Bounds.X;
            _dragDY = c.MouseY - w.Bounds.Y;
            Focus(w);
        }
    }

    enum CaptionGlyph { Minimize, Maximize, Restore, Close }

    bool CaptionButton(UiContext c, string id, Rect r, CaptionGlyph glyph, bool activeWindow,
                       bool isClose, bool tinted = false)
    {
        var t = c.Theme;
        var g = c.R;
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);
        bool clicked = c.Clicked(r);

        Color face, edge;
        if (t.Id == ThemeId.Metro)
        {
            // Nothing at rest, a wash on hover, and red on the close button:
            // the whole of the Windows 8 caption button.
            face = isClose
                ? (held ? Color.Rgb(0xA01018) : hover ? Color.Rgb(0xE81123) : Color.Transparent)
                : (held ? Color.Rgba(0x000000, 46) : hover ? Color.Rgba(0x000000, 26) : Color.Transparent);
            if (face.A > 0) g.FillRect(r, face);

            Color mink = isClose && (hover || held) ? Color.White
                       : tinted ? Color.White
                       : activeWindow ? t.CaptionTextActive : t.CaptionTextInactive;
            DrawCaptionGlyph(g, r, glyph, mink);
            return clicked;
        }

        if (t.Id == ThemeId.Seven)
        {
            face = isClose
                ? (held ? Color.Rgb(0xB0231C) : hover ? Color.Rgb(0xE81123) : Color.Rgba(0xFFFFFF, 60))
                : (held ? Color.Rgba(0x8FB8DC, 220) : hover ? Color.Rgba(0xC8E2F5, 220) : Color.Rgba(0xFFFFFF, 60));
            edge = Color.Rgba(0xFFFFFF, 120);
            g.RoundedRect(r, 2, face, edge, 1);
        }
        else
        {
            Color baseCol = isClose ? Color.Rgb(0xD86040) : Color.Rgb(0x4C8BE8);
            if (!activeWindow) baseCol = baseCol.Shade(0.85f).WithAlpha((byte)200);
            face = held ? baseCol.Shade(0.72f) : hover ? baseCol.Shade(1.22f) : baseCol;
            edge = Color.Rgba(0xFFFFFF, 190);
            g.RoundedRectV(r, 3, face.Shade(1.25f), face.Shade(0.88f), edge, 1);
        }

        Color ink = !tinted && t.Id == ThemeId.Seven && !(isClose && hover)
            ? Color.Rgb(0x203040) : Color.White;
        DrawCaptionGlyph(g, r, glyph, ink);

        return clicked;
    }

    /// <summary>The minus, box and cross themselves, drawn in the middle of a
    /// button whatever shape the theme gave it.</summary>
    static void DrawCaptionGlyph(Graphics.Renderer2D g, Rect r, CaptionGlyph glyph, Color ink)
    {
        float cx = MathF.Round(r.CenterX), cy = MathF.Round(r.CenterY);
        float s = MathF.Round(MathF.Min(r.W, r.H) * 0.28f);

        switch (glyph)
        {
            case CaptionGlyph.Minimize:
                g.FillRect(new Rect(cx - s, cy + s - 2, s * 2, 2), ink);
                break;
            case CaptionGlyph.Maximize:
                g.DrawRect(new Rect(cx - s, cy - s, s * 2, s * 2), ink);
                g.FillRect(new Rect(cx - s, cy - s, s * 2, 2), ink);
                break;
            case CaptionGlyph.Restore:
                g.DrawRect(new Rect(cx - s, cy - s + 2, s * 2 - 2, s * 2 - 2), ink);
                g.FillRect(new Rect(cx - s, cy - s + 2, s * 2 - 2, 2), ink);
                g.DrawRect(new Rect(cx - s + 3, cy - s - 1, s * 2 - 2, s * 2 - 2), ink);
                break;
            case CaptionGlyph.Close:
                g.Line(cx - s, cy - s, cx + s, cy + s, ink, 1.8f);
                g.Line(cx + s, cy - s, cx - s, cy + s, ink, 1.8f);
                break;
        }
    }
}
