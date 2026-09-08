using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Специальные возможности — the three that are more than a switch.
///
/// The magnifier really magnifies: it reads the finished frame back out of the
/// framebuffer around the pointer and draws those pixels again, enlarged, in a
/// strip along the top of the screen. It is a frame behind what is on screen,
/// which is what a docked magnifier looks like anyway.
///
/// The on-screen keyboard really types: what it presses goes into the same list
/// of characters the real keyboard fills, so every text box in the system takes
/// it without being told that anything unusual is happening.
///
/// The narrator really speaks, through the engine the machine already has —
/// the same one «Всё в одном» uses to decline «Михаил Гревцов».</summary>
public sealed class Accessibility : IDisposable
{
    readonly ShellHost _shell;

    public Accessibility(ShellHost shell) => _shell = shell;

    // ---- экранная лупа ------------------------------------------------------

    Texture _lens;
    uint[] _pixels;
    int _lensW, _lensH;

    /// <summary>Height of the strip the magnified picture is drawn into.</summary>
    public float MagnifierHeight(UiContext c)
        => _shell.Settings.Magnifier ? MathF.Round(c.ScreenH * 0.22f) : 0;

    /// <summary>Grabs the region around the pointer and draws it enlarged.
    ///
    /// It runs after everything else in the frame, so what it reads back is the
    /// previous frame — one frame of lag, which is invisible, and much cheaper
    /// than drawing the whole shell a second time at another scale.</summary>
    public unsafe void DrawMagnifier(UiContext c, float deviceScale)
    {
        var s = _shell.Settings;
        if (!s.Magnifier) return;

        float stripH = MagnifierHeight(c);
        var strip = new Rect(0, 0, c.ScreenW, stripH);

        // The source region, in device pixels, centred on the pointer.
        float zoom = Math.Clamp(s.MagnifierZoom, 1.5f, 6f);
        int w = Math.Max(16, (int)(c.ScreenW * deviceScale / zoom));
        int h = Math.Max(16, (int)(stripH * deviceScale / zoom));

        int px = (int)(c.MouseX * deviceScale) - w / 2;
        int py = (int)(c.MouseY * deviceScale) - h / 2;

        int deviceW = (int)MathF.Round(c.ScreenW * deviceScale);
        int deviceH = (int)MathF.Round(c.ScreenH * deviceScale);
        px = Math.Clamp(px, 0, Math.Max(0, deviceW - w));
        py = Math.Clamp(py, 0, Math.Max(0, deviceH - h));

        if (_lens == null || _lensW != w || _lensH != h)
        {
            _lens?.Dispose();
            _pixels = new uint[w * h];
            _lens = new Texture(w, h, _pixels, linear: false);
            _lensW = w;
            _lensH = h;
        }

        // glReadPixels counts rows from the bottom, so the row that is on top
        // of the region on screen is the last one read.
        int readY = Math.Max(0, deviceH - py - h);
        fixed (uint* p = _pixels)
        {
            GL.PixelStore(GL.PACK_ALIGNMENT, 4);
            GL.ReadPixels(px, readY, w, h, GL.RGBA, GL.UNSIGNED_BYTE, (byte*)p);
        }
        _lens.Update(0, 0, w, h, _pixels);

        // Drawn with the texture flipped vertically, which puts it back the
        // right way up.
        c.R.FillRect(strip, Color.Black);
        c.R.DrawTexture(_lens, strip, 0, 1, 1, 0, Color.White);

        c.R.FillRect(new Rect(strip.X, strip.Bottom - 2, strip.W, 2), Theme.MetroAccent);

        // The label, so the strip is never mistaken for the desktop.
        c.F.Small.Draw(c.R, L.F("access.magnifier_at", zoom.ToString("0.#")),
                       strip.X + 8, strip.Bottom - c.F.Small.Height - 6,
                       Color.Rgba(0xFFFFFF, 170));
    }

    // ---- экранная клавиатура ------------------------------------------------

    /// <summary>Where the keyboard sits, or an empty rect when it is off. The
    /// shell reserves this before windows take input, so a key press on it is
    /// never taken by the window underneath.</summary>
    public Rect KeyboardBounds(UiContext c)
    {
        if (!_shell.Settings.OnScreenKeyboard) return default;

        float h = MathF.Round(MathF.Min(260, c.ScreenH * 0.34f));
        float w = MathF.Min(c.ScreenW - 40, 760);
        var work = _shell.Taskbar.WorkArea(c);
        return new Rect(work.CenterX - w * 0.5f, work.Bottom - h - 12, w, h);
    }

    /// <summary>Two layouts, and the language indicator decides which. The
    /// Russian one is ЙЦУКЕН, because that is what the machine speaks.</summary>
    static readonly string[] RowsRu = { "йцукенгшщзхъ", "фывапролджэ", "ячсмитьбю." };
    static readonly string[] RowsEn = { "qwertyuiop", "asdfghjkl", "zxcvbnm,." };
    const string Digits = "1234567890";

    bool _shift;

    public void DrawKeyboard(UiContext c)
    {
        var s = _shell.Settings;
        if (!s.OnScreenKeyboard) return;

        var t = _shell.Theme;
        var panel = KeyboardBounds(c);

        c.R.FillRect(panel.Offset(3, 3), Color.Rgba(0x000000, 70));
        c.R.FillRect(panel, t.Id == ThemeId.HighContrast ? Color.Black : Color.Rgb(0x2A2A2A));
        c.R.DrawRect(panel, Color.Rgba(0xFFFFFF, 90));

        var area = panel.Deflate(8);

        // The title strip, with the close box the real one had.
        var head = area.CutTop(20);
        c.F.Small.Draw(c.R, L.T("access.on_screen_keyboard"), head.X + 2, head.Y,
                       Color.Rgba(0xFFFFFF, 190));

        var close = new Rect(head.Right - 18, head.Y, 16, 16);
        if (c.Hovering(close)) c.R.FillRect(close, Color.Rgb(0xE81123));
        c.R.Line(close.X + 4, close.Y + 4, close.Right - 4, close.Bottom - 4, Color.White, 1.6f);
        c.R.Line(close.Right - 4, close.Y + 4, close.X + 4, close.Bottom - 4, Color.White, 1.6f);
        if (c.Clicked(close)) { s.OnScreenKeyboard = false; return; }

        area.CutTop(4);

        var rows = L.IsRu ? RowsRu : RowsEn;
        float rowH = area.H / 5;
        float gap = 4;

        // ---- digits -----------------------------------------------------------
        DrawRow(c, area.CutTop(rowH), Digits, gap, 0);

        // ---- letters ----------------------------------------------------------
        for (int i = 0; i < rows.Length; i++)
            DrawRow(c, area.CutTop(rowH), rows[i], gap, i == 0 ? 0 : (i == 1 ? 14 : 28));

        // ---- the bottom row ---------------------------------------------------
        var bottom = area.CutTop(rowH);
        float x = bottom.X;

        if (WideKey(c, ref x, bottom, 80, L.T(_shift ? "access.key_shift_on" : "access.key_shift")))
            _shift = !_shift;

        if (WideKey(c, ref x, bottom, 90, L.TrayTag)) L.Toggle();

        float spaceW = bottom.Right - x - 90 - 84 - gap * 3;
        if (WideKey(c, ref x, bottom, MathF.Max(80, spaceW), L.T("access.key_space"))) Type(c, ' ');
        if (WideKey(c, ref x, bottom, 90, L.T("access.key_back"))) Press(c, Keys.Back);
        if (WideKey(c, ref x, bottom, 84, L.T("access.key_enter"))) Press(c, Keys.Enter, '\r');

        // The keyboard is a layer of its own: nothing under it hears the click
        // that pressed a key.
        c.MouseHandled = true;
    }

    void DrawRow(UiContext c, Rect row, string keys, float gap, float indent)
    {
        float w = (row.W - indent - gap * (keys.Length - 1)) / keys.Length;
        float x = row.X + indent;

        foreach (char ch in keys)
        {
            char shown = _shift ? char.ToUpper(ch) : ch;
            var r = new Rect(x, row.Y + 2, w, row.H - 6);
            if (Key(c, r, shown.ToString())) Type(c, shown);
            x += w + gap;
        }
    }

    bool WideKey(UiContext c, ref float x, Rect row, float w, string label)
    {
        var r = new Rect(x, row.Y + 2, w, row.H - 6);
        x += w + 4;
        return Key(c, r, label);
    }

    bool Key(UiContext c, Rect r, string label)
    {
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);
        bool contrast = _shell.Theme.Id == ThemeId.HighContrast;

        Color face = held ? Theme.MetroAccent
                   : hover ? Color.Rgb(0x4A4A4A)
                   : contrast ? Color.Black : Color.Rgb(0x3A3A3A);

        c.R.FillRect(r, face);
        if (contrast || hover) c.R.DrawRect(r, Color.White);

        var font = label.Length > 2 ? c.F.Small : c.F.Caption;
        font.DrawCentered(c.R, label, r, Color.White);

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Key, r, 0.8f);
        return clicked;
    }

    /// <summary>Puts a character into the queue the window will read this
    /// frame. Nothing else is needed: the shell's text boxes take their input
    /// from that list and cannot tell where it came from.</summary>
    void Type(UiContext c, char ch)
    {
        c.In.TypedChars.Add(ch);
        if (_shift) _shift = false;
    }

    void Press(UiContext c, int vk, char? ch = null)
    {
        c.In.SetKey(vk, true);
        if (ch.HasValue) c.In.TypedChars.Add(ch.Value);
    }

    // ---- экранный диктор ----------------------------------------------------

    string _lastSpoken;

    /// <summary>Reads out what has just come to the front. It speaks a window
    /// when it takes the focus and a dialog when it appears, and says nothing
    /// at all the rest of the time — which is as much narration as a system
    /// this size has to narrate.</summary>
    public void Narrate(UiContext c)
    {
        if (!_shell.Settings.Narrator) { _lastSpoken = null; return; }

        string subject = _shell.Wm.Focused?.Title;
        if (string.IsNullOrEmpty(subject) || subject == _lastSpoken) return;

        _lastSpoken = subject;
        try { Speech.Say(subject); }
        catch
        {
            // No voice on this machine: the narrator is silent rather than a
            // fault, which is what the real one does too.
        }
    }

    /// <summary>Says one line on purpose — used when a switch is thrown, so
    /// turning the narrator on announces itself.</summary>
    public void Announce(string text)
    {
        if (!_shell.Settings.Narrator) return;
        _lastSpoken = text;
        try { Speech.Say(text); } catch { }
    }

    public void Dispose() => _lens?.Dispose();
}
