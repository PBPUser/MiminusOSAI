using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>The two pages the Control Panel shows instead of opening a window:
/// «Персонализация» and «Разрешение экрана».
///
/// They were one property sheet with five tabs, then two windows of their own,
/// and version 8 made them two pages of the Control Panel — which is where they
/// are now. Nothing about them changed in the move: the theme gallery still
/// applies a theme the moment it is clicked, and the monitor page still changes
/// the four things this system can actually change about its screen.</summary>
public sealed partial class ControlPanelWindow
{



    static readonly ThemeId[] PageThemes =
    {
        ThemeId.Metro, ThemeId.LunaBlue, ThemeId.LunaOlive,
        ThemeId.LunaSilver, ThemeId.Seven, ThemeId.Classic, ThemeId.HighContrast,
    };

    float _paperScroll;


    void DrawPersonalisePage(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, Color.White);

        var area = client;
        DrawPersonaliseHeader(c, area.CutTop(56));
        var links = area.CutBottom(76);

        var page = area.Deflate(20, 10, 20, 6);
        DrawThemeGallery(c, ref page);
        DrawAccentNote(c, ref page);
        DrawWallpapers(c, page);

        DrawPersonaliseLinks(c, links);
    }

    void DrawPersonaliseHeader(UiContext c, Rect head)
    {
        c.R.FillRect(head, Color.Rgb(0xF7F7F7));
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), Color.Rgb(0xE0E0E0));

        c.F.Caption.Draw(c.R, L.T("person.heading"), head.X + 20, head.Y + 10, Color.Rgb(0x1E4E79));
        c.F.Small.Draw(c.R, L.T("person.heading_note"), head.X + 21,
                       head.Y + 12 + c.F.Caption.Height, Color.Rgb(0x707070));
    }

    void DrawThemeGallery(UiContext c, ref Rect page)
    {
        PageCaption(c, ref page, "person.themes");

        var row = page.CutTop(104);
        float w = MathF.Min(140, (row.W - 8 * (PageThemes.Length - 1)) / PageThemes.Length);

        for (int i = 0; i < PageThemes.Length; i++)
        {
            var r = new Rect(row.X + i * (w + 8), row.Y, w, 84);
            if (r.Right > row.Right) break;

            var preview = Theme.Create(PageThemes[i]);
            bool picked = PageThemes[i] == Shell.ThemeId;

            // A miniature of a window in that theme, which is the only honest
            // way to show one.
            c.R.FillRect(r, preview.Face);
            c.R.FillRectV(new Rect(r.X, r.Y, r.W, 18), preview.CaptionActiveTop,
                          preview.CaptionActiveBottom);
            c.R.FillRect(new Rect(r.X + 8, r.Y + 28, r.W - 16, 10), preview.Accent);
            c.R.FillRect(new Rect(r.X + 8, r.Y + 44, r.W * 0.5f, 8), preview.ControlBorder);
            c.R.FillRect(new Rect(r.X, r.Bottom - 12, r.W, 12), preview.TaskbarMid);
            c.R.DrawRect(r, picked ? c.Theme.Accent : Color.Rgb(0xC8C8C8), picked ? 3 : 1);

            string name = c.F.Small.Ellipsize(preview.Name, r.W - 4);
            float nw = c.F.Small.Measure(name);
            c.F.Small.Draw(c.R, name, r.CenterX - nw * 0.5f, r.Bottom + 4,
                           picked ? c.Theme.Accent : Color.Rgb(0x404040));

            if (c.Clicked(r))
            {
                Shell.SetTheme(PageThemes[i], c.F);
                c.SoundAt(Sfx.Navigate, r, 0.5f);
            }
        }

        page.CutTop(10);
    }

    /// <summary>Where the accent colour used to be offered here.
    ///
    /// It is not offered here any more: the colour the flat theme is built out
    /// of belongs to «Параметры компьютера», which is the full-screen program
    /// that owns everything about the version 8 look. This window says so and
    /// opens it, which is what the real one did when it wanted you somewhere
    /// else — the two settings programs of that era spent a good deal of their
    /// time handing you to each other.</summary>
    void DrawAccentNote(UiContext c, ref Rect page)
    {
        PageCaption(c, ref page, "pcs.accent");

        var row = page.CutTop(30);
        var swatch = new Rect(row.X, row.Y + 2, 26, 22);
        c.R.FillRect(swatch, Theme.MetroAccent);
        c.R.DrawRect(swatch, Color.Rgb(0xC0C0C0));

        var link = new Rect(swatch.Right + 10, row.Y, row.W - 40, 26);
        bool hot = c.Hovering(link);

        c.F.Ui.Draw(c.R, L.T("person.accent_lives_in_pc_settings"), link.X,
                    link.CenterY - c.F.Ui.Height * 0.5f,
                    hot ? c.Theme.Accent : Color.Rgb(0x1E4E79));
        if (hot)
            c.R.FillRect(new Rect(link.X, link.CenterY + c.F.Ui.Height * 0.5f,
                                  c.F.Ui.Measure(L.T("person.accent_lives_in_pc_settings")), 1),
                         c.Theme.Accent);

        if (c.Clicked(link)) Shell.Launch(c, "pcsettings", null);
        page.CutTop(8);
    }

    void DrawWallpapers(UiContext c, Rect page)
    {
        PageCaption(c, ref page, "person.background");

        var papers = Shell.Wallpapers.All.ToList();
        const float tw = 104, th = 62, gap = 8;
        int cols = Math.Max(1, (int)((page.W + gap) / (tw + gap)));
        int rows = (papers.Count + cols - 1) / cols;
        float contentH = rows * (th + gap + 14);

        if (c.Hovering(page) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _paperScroll = Math.Clamp(_paperScroll - c.In.WheelDelta * 50, 0, MathF.Max(0, contentH - page.H));
            c.In.WheelDelta = 0;
        }
        if (contentH <= page.H) _paperScroll = 0;

        c.R.PushClip(page);
        for (int i = 0; i < papers.Count; i++)
        {
            var r = new Rect(page.X + (i % cols) * (tw + gap),
                             page.Y - _paperScroll + (i / cols) * (th + gap + 14), tw, th);
            if (r.Bottom < page.Y || r.Y > page.Bottom) continue;

            var paper = Shell.Wallpapers.Get(papers[i]);
            if (paper.Texture != null) c.R.DrawTexture(paper.Texture, r);
            else c.R.FillRect(r, paper.Fallback);

            bool picked = papers[i] == Shell.Desktop.Current;
            c.R.DrawRect(r, picked ? c.Theme.Accent : Color.Rgb(0xC0C0C0), picked ? 3 : 1);

            c.R.PushClip(new Rect(r.X, r.Bottom, r.W, 14));
            c.F.Small.Draw(c.R, c.F.Small.Ellipsize(paper.Name, r.W), r.X, r.Bottom + 1,
                           picked ? c.Theme.Accent : Color.Rgb(0x606060));
            c.R.PopClip();

            if (c.Clicked(r))
            {
                Shell.SetWallpaper(papers[i]);
                c.SoundAt(Sfx.Click, r, 0.45f);
            }
        }
        c.R.PopClip();
    }

    /// <summary>The four along the foot, which is the row seven put there and
    /// eight kept — with the old property sheet added to the end of it.</summary>
    void DrawPersonaliseLinks(UiContext c, Rect strip)
    {
        c.R.FillRect(strip, Color.Rgb(0xF7F7F7));
        c.R.FillRect(new Rect(strip.X, strip.Y, strip.W, 1), Color.Rgb(0xE0E0E0));

        (IconId icon, string key, string app)[] links =
        {
            (IconId.Display, "person.link_background", null),
            (IconId.Paint, "person.link_colour", null),
            (IconId.Volume, "person.link_sounds", "sound"),
            (IconId.Lock, "person.link_screensaver", "screensaver"),
            (IconId.Settings, "person.link_advanced", "displayprops"),
        };

        float w = strip.W / links.Length;
        for (int i = 0; i < links.Length; i++)
        {
            var (icon, key, app) = links[i];
            var r = new Rect(strip.X + i * w, strip.Y + 6, w, strip.H - 12);
            bool hot = app != null && c.Hovering(r);

            if (hot) c.R.FillRect(r.Deflate(6, 0, 6, 0), Color.Rgb(0xE8F1FB));
            Icons.Draw(c.R, icon, new Rect(r.CenterX - 14, r.Y + 4, 28, 28));

            string label = c.F.Small.Ellipsize(L.T(key), r.W - 8);
            float lw = c.F.Small.Measure(label);
            c.F.Small.Draw(c.R, label, r.CenterX - lw * 0.5f, r.Y + 36,
                           app == null ? Color.Rgb(0x909090)
                           : hot ? c.Theme.Accent : Color.Rgb(0x1E4E79));

            if (app != null && c.Clicked(r)) Shell.Launch(c, app, null);
        }
    }

    static void PageCaption(UiContext c, ref Rect area, string key)
    {
        var row = area.CutTop(22);
        c.F.UiBold.Draw(c.R, L.T(key), row.X, row.Y, Color.Rgb(0x1E4E79));
    }





    void DrawScreenPage(UiContext c, Rect area)
    {
        var t = c.Theme;
        var s = Shell.Settings;

        c.F.Caption.Draw(c.R, L.T("screen.heading"), area.X, area.Y, Color.Rgb(0x1E4E79));
        area.CutTop(c.F.Caption.Height + 10);

        // ---- the monitors ---------------------------------------------------
        var stage = area.CutTop(184);
        DrawArrangement(c, stage);
        area.CutTop(2);

        // ---- what the second one is doing -------------------------------------
        var multi = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("screen.multiple_displays"), multi.X, multi.Y + 4, t.Text);

        var modes = new List<string>
        {
            L.T("screen.mode_single"), L.T("screen.mode_extend"), L.T("screen.mode_duplicate"),
        };
        int mode = (int)Shell.Displays.Mode;
        if (W.ComboBox(c, Id + ".multi", new Rect(multi.X + 190, multi.Y, 200, 24), modes, ref mode))
        {
            Shell.Displays.Mode = (MultiMode)mode;
            if (!Shell.Displays.Extended) Shell.Displays.PrimaryIndex = 0;
            Shell.Desktop.Relayout(c.ScreenW, c.ScreenH);
            c.Sound(Sfx.Navigate, 0.5f);
        }

        // ---- the four settings ------------------------------------------------
        var scale = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("screen.scale"), scale.X, scale.Y + 4, t.Text);
        int[] dpis = { 96, 120, 144 };
        for (int i = 0; i < dpis.Length; i++)
        {
            var r = new Rect(scale.X + 190 + i * 106, scale.Y, 100, 24);
            if (ScreenChip(c, r, L.F("screen.dpi", dpis[i]), s.Dpi == dpis[i])) s.Dpi = dpis[i];
        }

        var refresh = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("screen.refresh"), refresh.X, refresh.Y + 4, t.Text);
        int[] rates = { 60, 75, 0 };
        for (int i = 0; i < rates.Length; i++)
        {
            var r = new Rect(refresh.X + 190 + i * 106, refresh.Y, 100, 24);
            string label = rates[i] == 0 ? L.T("pcs.uncapped") : rates[i] + " Гц";
            if (ScreenChip(c, r, label, s.RefreshHz == rates[i])) s.RefreshHz = rates[i];
        }

        var depth = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("screen.colours"), depth.X, depth.Y + 4, t.Text);
        int[] depths = { 32, 24, 16 };
        for (int i = 0; i < depths.Length; i++)
        {
            var r = new Rect(depth.X + 190 + i * 106, depth.Y, 100, 24);
            if (ScreenChip(c, r, depths[i] + L.T("pcs.bit"), s.ColorDepth == depths[i]))
                s.ColorDepth = depths[i];
        }

        var bright = area.CutTop(34);
        c.F.Ui.Draw(c.R, L.T("charm.brightness"), bright.X, bright.Y + 4, t.Text);
        float level = s.Brightness;
        if (W.Slider(c, Id + ".bright", new Rect(bright.X + 190, bright.Y, 210, 22),
                     ref level, 0.35f, 1f))
            s.Brightness = level;
        c.F.Ui.Draw(c.R, (int)MathF.Round(s.Brightness * 100) + "%", bright.X + 410, bright.Y + 4,
                    t.TextDisabled);

        area.CutTop(10);
        foreach (string line in c.F.Small.Wrap(L.T("screen.note"), MathF.Min(area.W, 500)))
        {
            var row = area.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X, row.Y, Color.Rgb(0x707070));
        }

        // ---- the links -------------------------------------------------------
        area.CutTop(10);
        var links = area.CutTop(26);
        if (ScreenLink(c, new Rect(links.X, links.Y, 260, 20), "screen.advanced"))
            Shell.Launch(c, "advanced", null);

        links = area.CutTop(26);
        if (ScreenLink(c, new Rect(links.X, links.Y, 260, 20), "person.title"))
            Shell.Launch(c, "personalise", null);
    }

    /// <summary>The monitors as the page arranges them: one when there is one,
    /// two side by side when the desktop is extended.
    ///
    /// The two can be swapped by clicking the one on the right, and either can
    /// be made primary — both of which really move things, because the taskbar
    /// and the icons live on the primary and a maximised window fills the
    /// screen it is on. «Определить» prints a large number on each, which is
    /// the only reliable way anybody has ever told two monitors apart.</summary>
    void DrawArrangement(UiContext c, Rect box)
    {
        var displays = Shell.Displays;

        if (!displays.Extended)
        {
            DrawMonitor(c, box);
            DrawIdentifyButton(c, box);
            return;
        }

        var all = displays.All(c.ScreenW, c.ScreenH);

        // Drawn in the order they are arranged on the desk, so the picture and
        // the desktop agree about which one is on the left.
        var ordered = displays.SecondOnLeft ? new[] { all[1], all[0] } : new[] { all[0], all[1] };

        float w = MathF.Min(150, (box.W - 30) * 0.5f);
        float h = w * 0.74f;
        float left = box.CenterX - w - 8;

        for (int i = 0; i < 2; i++)
        {
            var m = ordered[i];
            var r = new Rect(left + i * (w + 16), box.Y + 6, w, h);
            bool hot = c.Hovering(r);

            c.R.RoundedRectV(r, 5, Color.Rgb(0xF0F2F6), Color.Rgb(0xB8C0CC),
                             m.Primary ? c.Theme.Accent : Color.Rgb(0x6E7A8C), m.Primary ? 2 : 1);

            var screen = new Rect(r.X + 9, r.Y + 8, r.W - 18, r.H - 26);
            var wp = Shell.Wallpapers.Get(Shell.Desktop.Current);
            if (wp.Texture != null) c.R.DrawTexture(wp.Texture, screen);
            else c.R.FillRect(screen, wp.Fallback);

            // Only the primary carries the bar, and the picture says so.
            if (m.Primary)
                c.R.FillRect(new Rect(screen.X, screen.Bottom - 6, screen.W, 6), c.Theme.TaskbarMid);
            c.R.DrawRect(screen, Color.Rgb(0x40484F));

            c.F.Big.DrawCentered(c.R, m.Label, screen, Color.Rgba(0xFFFFFF, 210));

            if (hot) c.R.DrawRect(r.Inflate(2), c.Theme.Accent, 1);
            c.Tooltip(r, L.F(m.Primary ? "screen.tip_primary" : "screen.tip_make_primary", m.Label));

            if (c.Clicked(r))
            {
                displays.PrimaryIndex = m.Index;
                Shell.Desktop.Relayout(c.ScreenW, c.ScreenH);
                c.SoundAt(Sfx.Click, r, 0.5f);
            }

            c.F.Small.DrawCentered(c.R, L.F("screen.display_n", m.Label),
                                   new Rect(r.X, r.Bottom + 4, r.W, 14), Color.Rgb(0x606060));
        }

        // Swapping which side the second one is on, which is the other half of
        // arranging two monitors.
        var swap = new Rect(box.CenterX - 60, box.Y + h + 26, 120, 24);
        if (W.Button(c, Id + ".swap", swap, L.T("screen.swap_sides")))
        {
            displays.SecondOnLeft = !displays.SecondOnLeft;
            Shell.Desktop.Relayout(c.ScreenW, c.ScreenH);
            c.Sound(Sfx.Navigate, 0.5f);
        }

        DrawIdentifyButton(c, box);

        c.F.Small.DrawCentered(c.R, L.T("screen.arrangement_note"),
                               new Rect(box.X, box.Bottom - 16, box.W, 14), Color.Rgb(0x808080));
    }

    void DrawIdentifyButton(UiContext c, Rect box)
    {
        var identify = new Rect(box.Right - 116, box.Y + 2, 112, 24);
        if (W.Button(c, Id + ".identify", identify, L.T("screen.identify"), true, IconId.Devices))
        {
            Shell.Displays.IdentifiedAt = c.Time;
            c.Sound(Sfx.Click, 0.5f);
        }
    }

    /// <summary>The monitor, with the desktop it is showing inside it and the
    /// figures under it — which is the picture «Разрешение экрана» opened on.</summary>
    void DrawMonitor(UiContext c, Rect box)
    {
        var shell = new Rect(box.CenterX - 100, box.Y, 200, 148);
        c.R.RoundedRectV(shell, 6, Color.Rgb(0xF0F2F6), Color.Rgb(0xB8C0CC), Color.Rgb(0x6E7A8C), 1);

        var screen = new Rect(shell.X + 12, shell.Y + 11, shell.W - 24, shell.H - 36);
        var wp = Shell.Wallpapers.Get(Shell.Desktop.Current);
        if (wp.Texture != null) c.R.DrawTexture(wp.Texture, screen);
        else c.R.FillRect(screen, wp.Fallback);

        // The taskbar in the miniature, so it reads as this desktop.
        c.R.FillRect(new Rect(screen.X, screen.Bottom - 8, screen.W, 8), c.Theme.TaskbarMid);
        c.R.DrawRect(screen, Color.Rgb(0x40484F));

        // The stand.
        c.R.FillRect(new Rect(shell.CenterX - 18, shell.Bottom - 2, 36, 8), Color.Rgb(0xB8C0CC));
        c.R.FillRect(new Rect(shell.CenterX - 34, shell.Bottom + 6, 68, 5), Color.Rgb(0x9AA4B0));

        c.F.Small.DrawCentered(c.R, L.F("screen.monitor_line", c.ScreenW, c.ScreenH),
                               new Rect(box.X, shell.Bottom + 18, box.W, 14), Color.Rgb(0x606060));
    }

    bool ScreenChip(UiContext c, Rect r, string label, bool picked)
    {
        bool hot = c.Hovering(r);
        c.R.FillRect(r, picked ? c.Theme.Accent : hot ? Color.Rgb(0xE8F1FB) : Color.Rgb(0xF2F2F2));
        c.R.DrawRect(r, picked ? c.Theme.Accent : Color.Rgb(0xC8C8C8));
        c.F.Small.DrawCentered(c.R, label, r, picked ? Color.White : c.Theme.Text);

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.4f);
        return clicked;
    }

    bool ScreenLink(UiContext c, Rect r, string key)
    {
        bool hot = c.Hovering(r);
        c.F.Ui.Draw(c.R, L.T(key), r.X, r.CenterY - c.F.Ui.Height * 0.5f,
                    hot ? c.Theme.Accent : Color.Rgb(0x1E4E79));
        if (hot)
            c.R.FillRect(new Rect(r.X, r.CenterY + c.F.Ui.Height * 0.5f, c.F.Ui.Measure(L.T(key)), 1),
                         c.Theme.Accent);
        return c.Clicked(r);
    }
}
