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

    // Outline drag, used when window contents are not shown while dragging.
    Rect _outline;
    bool _hasOutline;

    /// <summary>Area windows may occupy — the screen minus the taskbar.</summary>
    public Rect WorkArea;

    const float ResizeGrip = 5;

    // ---- lifetime --------------------------------------------------------

    public void Open(OsWindow w, UiContext c)
    {
        w.Wm = this;
        w.Shell = Shell;

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
        Focus(w);
        c.Sound(Sfx.WindowOpen, 0.55f);
    }

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

    public OsWindow TopModal()
    {
        for (int i = _windows.Count - 1; i >= 0; i--)
            if (_windows[i].Modal) return _windows[i];
        return null;
    }

    public void Minimize(OsWindow w, UiContext c)
    {
        if (!w.Minimizable) return;
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
        if (w.State == WindowState.Maximized)
        {
            w.Bounds = w.RestoreBounds;
            w.State = WindowState.Normal;
            c.Sound(Sfx.Restore, 0.45f);
        }
        else
        {
            w.RestoreBounds = w.Bounds;
            w.Bounds = WorkArea;
            w.State = WindowState.Maximized;
            c.Sound(Sfx.Restore, 0.5f, 1.15f);
        }
    }

    public void RestoreOrFocus(OsWindow w, UiContext c)
    {
        if (w.State == WindowState.Minimized)
        {
            w.State = WindowState.Normal;
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
        WorkArea = new Rect(0, 0, c.ScreenW, c.ScreenH - c.Theme.TaskbarHeight);

        // Retire closed windows first so nothing draws a dead window.
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            if (_windows[i].Closed)
            {
                var dead = _windows[i];
                _windows.RemoveAt(i);
                c.ForgetState(dead.Id);
                dead.OnClosed();
                c.Sound(Sfx.WindowClose, 0.5f);
                if (Focused == dead) Focused = null;
                if (_dragWindow == dead) { _dragWindow = null; _drag = DragMode.None; }
            }
        }
        if (Focused == null)
            Focused = _windows.LastOrDefault(x => x.State != WindowState.Minimized);

        // Maximised windows follow the work area as it changes.
        foreach (var w in _windows)
            if (w.State == WindowState.Maximized) w.Bounds = WorkArea;

        foreach (var w in _windows.ToList()) w.Tick(c, c.Dt);

        HitTest(c, blockWindows);
        UpdateDrag(c);
    }

    void HitTest(UiContext c, bool blockWindows)
    {
        var modal = TopModal();

        if (_drag != DragMode.None && _dragWindow != null)
        {
            _mouseWindow = _dragWindow;
            return;
        }

        _mouseWindow = null;
        if (c.MouseHandled || blockWindows) return;

        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var w = _windows[i];
            if (w.State == WindowState.Minimized) continue;
            if (modal != null && w != modal) continue;
            if (w.Bounds.Inflate(ResizeGrip).Contains(c.MouseX, c.MouseY)) { _mouseWindow = w; break; }
        }

        // Clicking a window (anywhere) brings it to the front.
        if (_mouseWindow != null && c.In.Pressed(MouseButton.Left) && Focused != _mouseWindow)
            Focus(_mouseWindow);

        // A modal dialog swallows clicks aimed at its owner.
        if (modal != null && _mouseWindow == null && c.In.Pressed(MouseButton.Left) &&
            _windows.Any(w => w != modal && w.State != WindowState.Minimized &&
                              w.Bounds.Contains(c.MouseX, c.MouseY)))
        {
            c.MouseHandled = true;
            c.Sound(Sfx.Error, 0.35f);
        }
    }

    void UpdateDrag(UiContext c)
    {
        if (_drag == DragMode.None || _dragWindow == null) return;

        if (!c.In.IsDown(MouseButton.Left))
        {
            // Commit an outline drag when the button comes up.
            if (_hasOutline && _dragWindow != null)
            {
                _dragWindow.Bounds.X = _outline.X;
                _dragWindow.Bounds.Y = _outline.Y;
            }
            _hasOutline = false;
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
            w.Bounds.Y = Math.Clamp(w.Bounds.Y, -2, c.ScreenH - c.Theme.TaskbarHeight - 24);
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
        var b = w.Bounds;
        bool active = w == Focused;

        // Drop shadow.
        if (t.Id == ThemeId.Seven)
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
        w.DrawClient(c, contentArea);
        g.PopClip();
    }

    void DrawCaption(UiContext c, OsWindow w, Rect cap, bool active)
    {
        var t = c.Theme;
        var g = c.R;

        Color top = active ? t.CaptionActiveTop : t.CaptionInactiveTop;
        Color mid = active ? t.CaptionActiveMid : t.CaptionInactiveMid;
        Color bot = active ? t.CaptionActiveBottom : t.CaptionInactiveBottom;

        // Rounded top corners; the bottom of the caption stays square.
        float rad = t.CornerRadius;
        if (rad > 0)
        {
            g.RoundedRectV(new Rect(cap.X, cap.Y, cap.W, rad * 2), rad, top, mid);
            g.FillRectV(new Rect(cap.X, cap.Y + rad, cap.W, cap.H - rad), mid, bot);
            g.FillRectV(new Rect(cap.X, cap.Y + rad * 0.6f, cap.W, rad * 0.4f), top, mid);
        }
        else g.FillRectV(cap, top, bot);

        if (t.GlassCaption)
        {
            // A soft highlight across the top half sells the glass look.
            g.FillRectV(new Rect(cap.X + 1, cap.Y + 1, cap.W - 2, cap.H * 0.45f),
                        Color.Rgba(0xFFFFFF, 130), Color.Rgba(0xFFFFFF, 20));
        }
        else
        {
            g.FillRect(new Rect(cap.X + rad, cap.Y + 1, cap.W - rad * 2, 1), Color.Rgba(0xFFFFFF, 90));
        }

        float pad = 6;
        var iconRect = new Rect(cap.X + pad, cap.Y + (cap.H - 16) * 0.5f, 16, 16);
        Icons.Draw(g, w.Icon, iconRect);

        // Buttons, right to left: close, maximise, minimise.
        float bs = t.CaptionHeight - 9;
        float bx = cap.Right - pad - bs;
        var closeRect = new Rect(bx, cap.Y + (cap.H - bs) * 0.5f, bs, bs);
        if (CaptionButton(c, w.Id + ".close", closeRect, CaptionGlyph.Close, active, true))
            RequestClose(w, c);

        if (w.Maximizable)
        {
            bx -= bs + 2;
            var maxRect = new Rect(bx, closeRect.Y, bs, bs);
            if (CaptionButton(c, w.Id + ".max", maxRect,
                              w.State == WindowState.Maximized ? CaptionGlyph.Restore : CaptionGlyph.Maximize, active, false))
                ToggleMaximize(w, c);
        }
        if (w.Minimizable)
        {
            bx -= bs + 2;
            var minRect = new Rect(bx, closeRect.Y, bs, bs);
            if (CaptionButton(c, w.Id + ".min", minRect, CaptionGlyph.Minimize, active, false))
                Minimize(w, c);
        }

        // Title text, clipped to whatever room is left.
        var textArea = new Rect(iconRect.Right + 5, cap.Y, bx - iconRect.Right - 10, cap.H);
        if (textArea.W > 10)
        {
            g.PushClip(textArea);
            string title = c.F.Caption.Ellipsize(w.Title, textArea.W);
            float ty = textArea.Y + (textArea.H - c.F.Caption.Height) * 0.5f;
            if (t.CaptionTextShadow.A > 0)
                c.F.Caption.Draw(g, title, textArea.X + 1, ty + 1, t.CaptionTextShadow);
            c.F.Caption.Draw(g, title, textArea.X, ty, active ? t.CaptionTextActive : t.CaptionTextInactive);
            g.PopClip();
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

    bool CaptionButton(UiContext c, string id, Rect r, CaptionGlyph glyph, bool activeWindow, bool isClose)
    {
        var t = c.Theme;
        var g = c.R;
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);
        bool clicked = c.Clicked(r);

        Color face, edge;
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

        Color ink = t.Id == ThemeId.Seven && !(isClose && hover) ? Color.Rgb(0x203040) : Color.White;
        float cx = MathF.Round(r.CenterX), cy = MathF.Round(r.CenterY);
        float s = MathF.Round(r.W * 0.28f);

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

        return clicked;
    }
}
