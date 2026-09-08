using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Центр специальных возможностей» — the Control Panel page, in the
/// shape Windows 7 gave it and version 8 kept: a heading, a row of the four
/// tools that can be turned on right now, and a list of switches under it.
///
/// Every one of them does something. The magnifier reads the screen back and
/// draws it again; the keyboard types into the same queue the real one fills;
/// the narrator speaks through the engine the machine has; high contrast is a
/// theme, so the whole shell repaints in two colours; and the pointer is drawn
/// from primitives, so making it bigger is a multiplication.</summary>
public sealed class EaseOfAccessWindow : OsWindow
{
    public override string Title => L.T("access.title");
    public override float MinWidth => 520;
    public override float MinHeight => 400;

    public EaseOfAccessWindow()
    {
        Icon = IconId.Access;
        Bounds = new Rect(0, 0, 640, 520);
    }

    /// <summary>The theme to go back to when high contrast is turned off. It is
    /// remembered rather than assumed, so turning it on and off again returns
    /// the desktop to whatever it actually was.</summary>
    ThemeId _before = ThemeId.Metro;

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        if (Shell.ThemeId != ThemeId.HighContrast) _before = Shell.ThemeId;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, Color.White);

        var area = client.Deflate(20, 16, 20, 14);

        c.F.Caption.Draw(c.R, L.T("access.heading"), area.X, area.Y, Color.Rgb(0x1E4E79));
        area.CutTop(c.F.Caption.Height + 4);
        Note(c, ref area, "access.heading_note");
        area.CutTop(14);

        // ---- the four big buttons ------------------------------------------
        var s = Shell.Settings;
        var row = area.CutTop(96);
        float w = (row.W - 24) / 4;

        if (BigTool(c, new Rect(row.X, row.Y, w, 88), IconId.Search, "access.magnifier", s.Magnifier))
            Toggle(c, ref s.Magnifier, "access.magnifier");

        if (BigTool(c, new Rect(row.X + (w + 8), row.Y, w, 88), IconId.Volume,
                    "access.narrator", s.Narrator))
        {
            Toggle(c, ref s.Narrator, "access.narrator");
            if (s.Narrator) Shell.Access.Announce(L.T("access.narrator_on"));
        }

        if (BigTool(c, new Rect(row.X + (w + 8) * 2, row.Y, w, 88), IconId.TextFile,
                    "access.on_screen_keyboard", s.OnScreenKeyboard))
            Toggle(c, ref s.OnScreenKeyboard, "access.on_screen_keyboard");

        bool contrast = Shell.ThemeId == ThemeId.HighContrast;
        if (BigTool(c, new Rect(row.X + (w + 8) * 3, row.Y, w, 88), IconId.Display,
                    "access.high_contrast", contrast))
            SetContrast(c, !contrast);

        area.CutTop(16);

        // ---- the switches ---------------------------------------------------
        c.F.UiBold.Draw(c.R, L.T("access.also"), area.X, area.Y, Color.Rgb(0x1E4E79));
        c.R.FillRect(new Rect(area.X, area.Y + c.F.UiBold.Height + 3, area.W, 1), Color.Rgb(0xE0E0E0));
        area.CutTop(c.F.UiBold.Height + 12);

        var line = area.CutTop(26);
        bool animations = s.Animations;
        if (W.CheckBox(c, Id + ".anim", new Rect(line.X, line.Y, 420, 20),
                       L.T("access.animations"), ref animations))
            s.Animations = animations;
        Note(c, ref area, "access.animations_note");
        area.CutTop(10);

        // ---- the pointer ------------------------------------------------------
        c.F.Ui.Draw(c.R, L.T("access.pointer_size"), area.X, area.Y, t.Text);
        area.CutTop(c.F.Ui.Height + 6);

        var sizes = area.CutTop(40);
        for (int i = 0; i < 3; i++)
        {
            float k = 1f + i * 0.75f;
            var r = new Rect(sizes.X + i * 88, sizes.Y, 80, 36);
            bool picked = MathF.Abs(s.CursorScale - k) < 0.05f;

            if (picked) c.R.FillRect(r, Color.Rgb(0xCFE4F7));
            else if (c.Hovering(r)) c.R.FillRect(r, Color.Rgb(0xE8F1FB));
            c.R.DrawRect(r, picked ? c.Theme.Accent : Color.Rgb(0xC8C8C8));

            // The pointer itself, at the size the button sets.
            DrawPointer(c, r.CenterX - 5 * k, r.CenterY - 9 * k, k);

            if (c.Clicked(r)) { s.CursorScale = k; c.SoundAt(Sfx.Click, r, 0.4f); }
        }

        area.CutTop(14);

        // ---- the magnifier's own setting ---------------------------------------
        c.F.Ui.Draw(c.R, L.F("access.zoom_level", s.MagnifierZoom.ToString("0.#")),
                    area.X, area.Y, t.Text);
        area.CutTop(c.F.Ui.Height + 6);

        float zoom = s.MagnifierZoom;
        if (W.Slider(c, Id + ".zoom", new Rect(area.X, area.Y, 260, 22), ref zoom, 1.5f, 6f))
            s.MagnifierZoom = MathF.Round(zoom * 2) / 2;

        area.CutTop(34);
        Note(c, ref area, "access.note");
    }

    void Toggle(UiContext c, ref bool flag, string key)
    {
        flag = !flag;
        c.Sound(Sfx.Click, 0.5f);
    }

    void SetContrast(UiContext c, bool on)
    {
        if (on)
        {
            _before = Shell.ThemeId == ThemeId.HighContrast ? _before : Shell.ThemeId;
            Shell.SetTheme(ThemeId.HighContrast, c.F);
        }
        else Shell.SetTheme(_before, c.F);

        c.Sound(Sfx.Navigate, 0.5f);
    }

    /// <summary>One of the four: a picture, a name, and a lit face when it is
    /// on. They are buttons rather than checkboxes because that is what turning
    /// a tool on ought to look like.</summary>
    bool BigTool(UiContext c, Rect r, IconId icon, string key, bool on)
    {
        bool hot = c.Hovering(r);

        c.R.FillRect(r, on ? Color.Rgb(0xCFE4F7) : hot ? Color.Rgb(0xE8F1FB) : Color.Rgb(0xF7F7F7));
        c.R.DrawRect(r, on ? c.Theme.Accent : Color.Rgb(0xD0D0D0), on ? 2 : 1);

        Icons.Draw(c.R, icon, new Rect(r.CenterX - 18, r.Y + 10, 36, 36));

        c.R.PushClip(r);
        float y = r.Y + 52;
        foreach (string word in c.F.Small.Wrap(L.T(key), r.W - 8).Take(2))
        {
            float w = c.F.Small.Measure(word);
            c.F.Small.Draw(c.R, word, r.CenterX - w * 0.5f, y, c.Theme.Text);
            y += c.F.Small.Height + 1;
        }
        c.R.PopClip();

        c.F.Small.DrawCentered(c.R, L.T(on ? "access.on" : "access.off"),
                               new Rect(r.X, r.Bottom - 15, r.W, 13),
                               on ? c.Theme.Accent : c.Theme.TextDisabled);

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);
        return clicked;
    }

    /// <summary>A sample pointer at a given scale, for the size buttons.</summary>
    static void DrawPointer(UiContext c, float x, float y, float k)
    {
        Color fill = Color.White, edge = Color.Black;
        c.R.FillTriangle(x, y, x, y + 17 * k, x + 4.5f * k, y + 12.5f * k, edge);
        c.R.FillTriangle(x, y, x + 4.5f * k, y + 12.5f * k, x + 12 * k, y + 12.5f * k, edge);
        c.R.FillTriangle(x + 1.3f * k, y + 2.4f * k, x + 1.3f * k, y + 14f * k,
                         x + 4.7f * k, y + 10.8f * k, fill);
        c.R.FillTriangle(x + 1.3f * k, y + 2.4f * k, x + 4.7f * k, y + 10.8f * k,
                         x + 9.4f * k, y + 10.8f * k, fill);
    }

    static void Note(UiContext c, ref Rect area, string key)
    {
        foreach (string line in c.F.Small.Wrap(L.T(key), MathF.Min(area.W, 520)))
        {
            if (area.H < c.F.Small.Height) return;
            var row = area.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X, row.Y, Color.Rgb(0x707070));
        }
    }
}
