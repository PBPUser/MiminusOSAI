using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Что нового в МИМИНУС ОС» — the presentation the system shows itself
/// the first time it starts after an update.
///
/// Systems of this era greeted a new build with a tour, and this one keeps the
/// habit: a title card, one card per change, and a closing card. Nothing is
/// loaded to draw it — each illustration is built from the same icons and
/// rectangles the rest of the OS is made of, which is the point the tour is
/// making as much as anything it says.</summary>
public sealed class WhatsNewWindow : OsWindow
{
    readonly ShellHost _shell;
    int _slide;
    double _entered;

    /// <summary>The cards. <c>Art</c> names which illustration to draw; the
    /// text is looked up so the tour follows the interface language.</summary>
    readonly record struct Card(string TitleKey, string BodyKey, Art Art);

    enum Art
    {
        Cover, Setup, Settings, Dpi, DragDrop, Volume, Custom, Stop, WindowsFolder, Done,
        // Version 8.
        Look, Tiles, BothStarts, Charms, Lock, FullScreen, NewApps,
    }

    static readonly Card[] Cards =
    {
        new("whatsnew.cover_title",    "whatsnew.cover_body",    Art.Cover),
        new("whatsnew.look_title",     "whatsnew.look_body",     Art.Look),
        new("whatsnew.tiles_title",    "whatsnew.tiles_body",    Art.Tiles),
        new("whatsnew.both_title",     "whatsnew.both_body",     Art.BothStarts),
        new("whatsnew.charms_title",   "whatsnew.charms_body",   Art.Charms),
        new("whatsnew.lock_title",     "whatsnew.lock_body",     Art.Lock),
        new("whatsnew.full_title",     "whatsnew.full_body",     Art.FullScreen),
        new("whatsnew.apps_title",     "whatsnew.apps_body",     Art.NewApps),
        new("whatsnew.done_title",     "whatsnew.done_body",     Art.Done),
    };

    public override string Title => L.T("whatsnew.title");
    public override float MinWidth => 520;
    public override float MinHeight => 400;

    public WhatsNewWindow(ShellHost shell)
    {
        _shell = shell;
        Icon = IconId.Star;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 620, 470);
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        _entered = c.Time;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        var card = Cards[_slide];

        c.R.FillRect(client, t.Face);

        // ---- the coloured band, as every wizard of the period had ---------
        var banner = client.CutTop(58);
        c.R.FillRectV(banner, t.CaptionActiveTop, t.CaptionActiveBottom);
        c.R.FillRect(new Rect(banner.X, banner.Bottom - 1, banner.W, 1), t.Shadow);

        c.F.UiBold.Draw(c.R, L.T(card.TitleKey), banner.X + 16, banner.Y + 12, t.CaptionTextActive);
        c.F.Small.Draw(c.R, L.F("whatsnew.step_of", _slide + 1, Cards.Length),
                       banner.X + 16, banner.Y + 16 + c.F.UiBold.Height, t.CaptionTextActive);

        var footer = client.CutBottom(44);
        var area = client.Deflate(20, 18, 20, 8);

        // ---- illustration, then the words ---------------------------------
        var stage = area.CutTop(170);
        DrawArt(c, stage, card.Art);

        area.CutTop(14);
        foreach (string line in c.F.Ui.Wrap(L.T(card.BodyKey), area.W))
        {
            if (area.H < c.F.Ui.Height) break;
            var row = area.CutTop(c.F.Ui.Height + 4);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, t.Text);
        }

        DrawFooter(c, footer);
        HandleKeys(c);
    }

    void DrawFooter(UiContext c, Rect footer)
    {
        var t = c.Theme;
        c.R.FillRect(new Rect(footer.X, footer.Y, footer.W, 1), t.Shadow);

        // Progress dots: which card of how many, at a glance.
        float dotY = footer.CenterY - 3;
        for (int i = 0; i < Cards.Length; i++)
        {
            var dot = new Rect(footer.X + 16 + i * 13, dotY, 7, 7);
            c.R.FillRect(dot, i == _slide ? t.Accent : t.ControlBorder);
        }

        bool last = _slide == Cards.Length - 1;

        var close = new Rect(footer.Right - 100, footer.CenterY - 12, 88, 24);
        if (W.Button(c, Id + ".finish", close, L.T(last ? "whatsnew.finish" : "whatsnew.skip"),
                     defaultButton: last))
            Close();

        var next = new Rect(close.X - 88 - 8, footer.CenterY - 12, 88, 24);
        if (W.Button(c, Id + ".next", next, L.T("whatsnew.next"), enabled: !last,
                     defaultButton: !last))
            Go(c, 1);

        var back = new Rect(next.X - 88 - 4, footer.CenterY - 12, 88, 24);
        if (W.Button(c, Id + ".back", back, L.T("whatsnew.back"), enabled: _slide > 0))
            Go(c, -1);
    }

    void HandleKeys(UiContext c)
    {
        if (c.KeyboardHandled) return;

        if (c.In.KeyPressed(Keys.Right) || c.In.KeyPressed(Keys.PageDown)) Go(c, 1);
        else if (c.In.KeyPressed(Keys.Left) || c.In.KeyPressed(Keys.PageUp)) Go(c, -1);
        else if (c.In.KeyPressed(Keys.Escape)) Close();
        else return;

        c.KeyboardHandled = true;
    }

    void Go(UiContext c, int delta)
    {
        int next = Math.Clamp(_slide + delta, 0, Cards.Length - 1);
        if (next == _slide) return;

        _slide = next;
        _entered = c.Time;
        c.Sound(Sfx.Navigate, 0.45f);
    }

    // ---- illustrations ----------------------------------------------------

    /// <summary>Each card is drawn from primitives and the system's own icons.
    /// They fade in as a card arrives, which is the only animation here.</summary>
    void DrawArt(UiContext c, Rect stage, Art art)
    {
        var t = c.Theme;

        c.R.FillRect(stage, t.FieldBack);
        c.R.DrawRect(stage, t.FieldBorder);

        float age = (float)Math.Clamp((c.Time - _entered) / 0.35, 0, 1);
        var box = stage.Deflate(16);
        float slide = (1 - age) * 8;
        box = new Rect(box.X, box.Y + slide, box.W, box.H);

        switch (art)
        {
            case Art.Cover: DrawCover(c, box); break;
            case Art.Setup: DrawSetupArt(c, box); break;
            case Art.Settings: DrawSettingsArt(c, box); break;
            case Art.Dpi: DrawDpiArt(c, box); break;
            case Art.DragDrop: DrawDragArt(c, box); break;
            case Art.Volume: DrawVolumeArt(c, box); break;
            case Art.Custom: DrawCustomArt(c, box); break;
            case Art.Stop: DrawStopArt(c, box); break;
            case Art.WindowsFolder: DrawFolderArt(c, box); break;
            case Art.Look: DrawLookArt(c, box); break;
            case Art.Tiles: DrawTilesArt(c, box); break;
            case Art.BothStarts: DrawBothStartsArt(c, box); break;
            case Art.Charms: DrawCharmsArt(c, box); break;
            case Art.Lock: DrawLockArt(c, box); break;
            case Art.FullScreen: DrawFullScreenArt(c, box); break;
            case Art.NewApps: DrawNewAppsArt(c, box); break;
            default: DrawDoneArt(c, box); break;
        }
    }

    // ---- version 8 --------------------------------------------------------

    /// <summary>The flat style, next to the one it replaced: two miniature
    /// windows, one bevelled and one not.</summary>
    void DrawLookArt(UiContext c, Rect box)
    {
        var metro = Theme.Metro();
        var luna = Theme.LunaBlue();

        float w = MathF.Min(190, box.W * 0.44f), h = box.H - 30;
        DrawMiniWindow(c, new Rect(box.X + 8, box.Y + 20, w, h), luna, false);
        DrawMiniWindow(c, new Rect(box.Right - w - 8, box.Y + 20, w, h), metro, true);

        c.F.Small.Draw(c.R, luna.Name, box.X + 10, box.Y + 2, c.Theme.TextDisabled);
        c.F.Small.Draw(c.R, metro.Name, box.Right - w - 6, box.Y + 2, c.Theme.Text);
    }

    static void DrawMiniWindow(UiContext c, Rect r, Theme theme, bool flat)
    {
        float rad = theme.CornerRadius;
        c.R.RoundedRect(r, rad, theme.FrameOuter);

        var cap = new Rect(r.X, r.Y, r.W, 18);
        if (flat) c.R.FillRect(cap, theme.CaptionActiveTop);
        else c.R.RoundedRectV(cap, rad, theme.CaptionActiveTop, theme.CaptionActiveBottom);

        c.F.Small.Draw(c.R, theme.Name, cap.X + 6, cap.CenterY - c.F.Small.Height * 0.5f,
                       theme.CaptionTextActive);

        var body = new Rect(r.X + 3, cap.Bottom, r.W - 6, r.H - cap.H - 3);
        c.R.FillRect(body, theme.Face);

        // One button, drawn the way each style draws one.
        var btn = new Rect(body.X + 10, body.Y + 12, 74, 22);
        if (flat)
        {
            c.R.FillRect(btn, theme.FaceDark);
            c.R.DrawRect(btn, theme.ControlBorder);
        }
        else c.R.RoundedRectV(btn, 3, theme.FaceLight, theme.FaceDark, theme.ControlBorder, 1);
        c.F.Small.DrawCentered(c.R, L.T("dlg.ok"), btn, theme.Text);

        // And a strip of accent, which is the whole difference.
        c.R.FillRect(new Rect(body.X + 10, btn.Bottom + 12, body.W - 20, 10), theme.Accent);
        c.R.FillRect(new Rect(r.X, r.Bottom - 12, r.W, 12), theme.TaskbarMid);
    }

    /// <summary>The board, in miniature: coloured rectangles in three groups.</summary>
    void DrawTilesArt(UiContext c, Rect box)
    {
        c.R.FillRect(box, c.Theme.Accent.Shade(0.55f));

        float s = 26, gap = 3;
        float x = box.X + 14, y = box.Y + 18;
        int i = 0;

        foreach (int group in new[] { 4, 3, 3 })
        {
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < group; col++)
                {
                    bool wide = row == 0 && col == 0;
                    var tile = new Rect(x + col * (s + gap), y + row * (s + gap),
                                        wide ? s * 2 + gap : s, s);
                    if (wide) col++;
                    if (tile.Right > box.Right - 8) continue;

                    c.R.FillRect(tile, Theme.TileColors[i++ % Theme.TileColors.Length]);
                }
            x += group * (s + gap) + 18;
        }

        c.F.Small.Draw(c.R, L.T("start8.start"), box.X + 14, box.Y + 2, Color.White);
    }

    /// <summary>Both Starts, side by side, because neither was taken away.</summary>
    void DrawBothStartsArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        float w = MathF.Min(180, box.W * 0.42f);

        // The menu.
        var menu = new Rect(box.X + 10, box.Y + 24, w, box.H - 34);
        c.R.FillRect(menu, t.StartMenuLeft);
        c.R.DrawRect(menu, t.StartMenuBorder);
        c.R.FillRectV(new Rect(menu.X, menu.Y, menu.W, 18), t.StartMenuHeaderTop, t.StartMenuHeaderBottom);
        for (int i = 0; i < 5; i++)
        {
            var row = new Rect(menu.X + 6, menu.Y + 26 + i * 16, menu.W - 12, 12);
            Icons.Draw(c.R, i switch { 0 => IconId.Notepad, 1 => IconId.Paint, 2 => IconId.Minesweeper,
                                       3 => IconId.Calculator, _ => IconId.Terminal },
                       new Rect(row.X, row.Y, 11, 11));
            c.R.FillRect(new Rect(row.X + 15, row.Y + 4, row.W - 20, 3), t.TextDisabled);
        }
        c.F.Small.Draw(c.R, L.T("start8.classic_menu"), menu.X, box.Y + 6, t.Text);

        // The board.
        var board = new Rect(box.Right - w - 10, box.Y + 24, w, box.H - 34);
        c.R.FillRect(board, c.Theme.Accent.Shade(0.55f));
        float s = 24;
        for (int i = 0; i < 8; i++)
        {
            var tile = new Rect(board.X + 8 + (i % 4) * (s + 4), board.Y + 10 + (i / 4) * (s + 4), s, s);
            if (tile.Right > board.Right - 4 || tile.Bottom > board.Bottom - 4) continue;
            c.R.FillRect(tile, Theme.TileColors[i % Theme.TileColors.Length]);
        }
        c.F.Small.Draw(c.R, L.T("start8.start_screen"), board.X, box.Y + 6, t.Text);

        // The arrow between them, pointing both ways.
        float mid = (menu.Right + board.X) * 0.5f;
        c.R.Line(menu.Right + 6, box.CenterY, board.X - 6, box.CenterY, t.Accent, 2);
        c.R.FillTriangle(menu.Right + 4, box.CenterY, mid - 6, box.CenterY - 6,
                         mid - 6, box.CenterY + 6, t.Accent);
        c.R.FillTriangle(board.X - 4, box.CenterY, mid + 6, box.CenterY - 6,
                         mid + 6, box.CenterY + 6, t.Accent);
    }

    /// <summary>The right edge, out, with the five buttons on it.</summary>
    void DrawCharmsArt(UiContext c, Rect box)
    {
        c.R.FillRect(box, Color.Rgb(0x1F4E86));

        var bar = new Rect(box.Right - 62, box.Y, 62, box.H);
        c.R.FillRect(bar, Color.Rgb(0x1C1C1C));

        (IconId icon, string key)[] charms =
        {
            (IconId.Search, "charm.search"),
            (IconId.Share, "charm.share"),
            (IconId.Tiles, "charm.start"),
            (IconId.Devices, "charm.devices"),
            (IconId.Settings, "charm.settings"),
        };

        float h = bar.H / charms.Length;
        for (int i = 0; i < charms.Length; i++)
        {
            var cell = new Rect(bar.X, bar.Y + i * h, bar.W, h);
            if (i == 2) c.R.FillRect(new Rect(cell.CenterX - 13, cell.CenterY - 13, 26, 26),
                                     c.Theme.Accent);
            Icons.Draw(c.R, charms[i].icon, new Rect(cell.CenterX - 9, cell.CenterY - 9, 18, 18));
        }

        // The clock block in the opposite corner, as the real one shows.
        var clock = new Rect(box.X + 12, box.Bottom - 54, 110, 42);
        c.R.FillRect(clock, Color.Rgba(0x1C1C1C, 210));
        c.F.Big.Draw(c.R, "13:28", clock.X + 8, clock.Y - 6, Color.White);
    }

    /// <summary>The lock screen: a clock over a skyline, and the hint.</summary>
    void DrawLockArt(UiContext c, Rect box)
    {
        c.R.FillRectV(box, Color.Rgb(0x0E2C4C), Color.Rgb(0x061625));

        float baseY = box.Bottom - box.H * 0.28f;
        int seed = 11;
        for (float x = box.X; x < box.Right; )
        {
            seed = seed * 1103515245 + 12345;
            float w = 16 + ((seed >> 16) & 0x1F);
            seed = seed * 1103515245 + 12345;
            float bh = 12 + ((seed >> 16) & 0x3F);
            c.R.FillRect(new Rect(x, baseY - bh, w - 3, bh + box.H), Color.Rgb(0x0A1F35));
            x += w;
        }

        c.F.Big.Draw(c.R, "13:28", box.X + 18, box.Y + 16, Color.White);
        c.F.Small.Draw(c.R, L.LongDate(new DateTime(2010, 6, 6)), box.X + 20,
                       box.Y + 20 + c.F.Big.Height, Color.Rgba(0xFFFFFF, 200));
        c.F.Small.DrawCentered(c.R, L.T("lock.hint"),
                               new Rect(box.X, box.Bottom - 22, box.W, 16), Color.Rgba(0xFFFFFF, 180));
    }

    /// <summary>A window growing into the whole screen, drawn as three frames
    /// of the same rectangle.</summary>
    void DrawFullScreenArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        c.R.FillRect(box, Color.Rgb(0x1F4E86));

        var target = box.Deflate(10);
        for (int i = 0; i < 3; i++)
        {
            float f = 0.44f + i * 0.28f;
            var r = new Rect(target.CenterX - target.W * f * 0.5f,
                             target.CenterY - target.H * f * 0.5f,
                             target.W * f, target.H * f);

            byte alpha = (byte)(70 + i * 60);
            c.R.FillRect(r, Color.Rgba(0xFFFFFF, (byte)(alpha / 3)));
            c.R.DrawRect(r, Color.Rgba(0xFFFFFF, alpha));

            if (i == 2)
            {
                c.R.FillRect(new Rect(r.X, r.Y, r.W, 16), Color.Rgba(0x1C1C1C, 235));
                Icons.Draw(c.R, IconId.Store, new Rect(r.X + 4, r.Y + 2, 12, 12));
                c.F.Small.Draw(c.R, L.T("store.title"), r.X + 20, r.Y + 2, Color.White);
                c.F.Small.DrawCentered(c.R, "F11",
                                       new Rect(r.X, r.CenterY, r.W, 14), Color.Rgba(0xFFFFFF, 220));
            }
        }
    }

    /// <summary>The two programs version 8 brought with it.</summary>
    void DrawNewAppsArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        c.R.FillRect(box, t.Face);

        (IconId icon, string key, int colour)[] apps =
        {
            (IconId.PcSettings, "pcs.title", 0),
            (IconId.Store, "store.title", 6),
        };

        float w = (box.W - 30) * 0.5f;
        for (int i = 0; i < apps.Length; i++)
        {
            var card = new Rect(box.X + 10 + i * (w + 10), box.Y + 12, w, box.H - 24);
            c.R.FillRect(card, Theme.TileColors[apps[i].colour]);
            Icons.Draw(c.R, apps[i].icon, new Rect(card.CenterX - 22, card.Y + 18, 44, 44));
            c.F.Small.DrawCentered(c.R, L.T(apps[i].key),
                                   new Rect(card.X, card.Bottom - 26, card.W, 16), Color.White);
        }
    }

    void DrawCover(UiContext c, Rect box)
    {
        // The desktop itself: the yellow, the black letters, the version.
        c.R.FillRectV(box, Color.Rgb(0xFFD200), Color.Rgb(0xE8B800));

        // Three lines placed in sequence from a centred top, so no two of
        // them can ever land on each other.
        float block = c.F.Huge.Height + 4 + c.F.Big.Height + 2 + c.F.Ui.Height;
        float y = box.Y + MathF.Max(4, (box.H - block) * 0.5f);

        c.F.Huge.DrawCentered(c.R, L.T("shell.miminus_os"),
                              new Rect(box.X, y, box.W, c.F.Huge.Height), Color.Black);
        y += c.F.Huge.Height + 4;

        c.F.Big.DrawCentered(c.R, UpdateService.InstalledVersion,
                             new Rect(box.X, y, box.W, c.F.Big.Height), Color.Rgb(0x604800));
        y += c.F.Big.Height + 2;

        c.F.Ui.DrawCentered(c.R, L.T("dlg.copyright_popov"),
                            new Rect(box.X, y, box.W, c.F.Ui.Height), Color.Rgb(0x806000));
    }

    void DrawSetupArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // The setup screen in miniature, in its own blue.
        var screen = box.Deflate(6);
        c.R.FillRectV(new Rect(screen.X, screen.Y, screen.W, screen.H * 0.5f),
                      Color.Rgb(0x2E6FC4), Color.Rgb(0x14477E));
        c.R.FillRectV(new Rect(screen.X, screen.CenterY, screen.W, screen.H * 0.5f),
                      Color.Rgb(0x14477E), Color.Rgb(0x07203F));
        c.R.DrawRect(screen, t.FieldBorder);

        // The step list down the left, with the second one lit.
        string[] steps =
        {
            "setup.step_welcome", "setup.step_language",
            "setup.step_name", "setup.step_look", "setup.step_ready",
        };

        float y = screen.Y + 16;
        for (int i = 0; i < steps.Length; i++)
        {
            bool current = i == 1;
            c.R.FillRect(new Rect(screen.X + 14, y + 3, 6, 6),
                         current ? Color.White : Color.Rgba(0xFFFFFF, 70));
            c.F.Small.Draw(c.R, L.T(steps[i]), screen.X + 26, y,
                           current ? Color.White : Color.Rgba(0xFFFFFF, 110));
            y += c.F.Small.Height + 6;
        }

        float x = screen.X + 130;
        c.F.UiBold.Draw(c.R, L.T("setup.language_heading"), x, screen.Y + 16, Color.White);

        // The two answers it is waiting for.
        float ry = screen.Y + 20 + c.F.UiBold.Height + 8;
        foreach (var (label, picked) in new[] { ("Русский", true), ("English", false) })
        {
            var row = new Rect(x, ry, screen.Right - x - 16, 22);
            if (picked) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 45));
            c.R.FillCircle(row.X + 9, row.CenterY, 5, Color.White);
            if (picked) c.R.FillCircle(row.X + 9, row.CenterY, 2.5f, Color.Rgb(0x1B5FAF));
            c.F.Small.Draw(c.R, label, row.X + 20, row.CenterY - c.F.Small.Height * 0.5f, Color.White);
            ry += 26;
        }
    }

    void DrawSettingsArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // The file itself, which is what the setting turns into.
        var page = new Rect(box.X + 8, box.Y + 6, box.W - 16, box.H - 12);
        c.R.FillRect(page, t.FieldBack);
        c.R.DrawRect(page, t.FieldBorder);

        c.F.MonoSmall.Draw(c.R, "settings.txt", page.X + 10, page.Y + 8, t.TextDisabled);

        (string key, string value)[] rows =
        {
            ("theme", "LunaBlue"),
            ("wallpaper", "MiminusYellow"),
            ("dpi", "120"),
            ("volume", "0.7"),
            ("taskbar_autohide", "yes"),
        };

        float y = page.Y + 10 + c.F.MonoSmall.Height + 6;
        foreach (var (key, value) in rows)
        {
            c.F.MonoSmall.Draw(c.R, key, page.X + 10, y, t.Text);
            c.F.MonoSmall.Draw(c.R, "= " + value, page.X + 160, y, t.Accent);
            y += c.F.MonoSmall.Height + 4;
        }
    }

    void DrawDpiArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // The same letter at both scales, with the point written under it.
        c.F.Big.Draw(c.R, "Аа", box.X + 16, box.CenterY - c.F.Big.Height, t.Text);
        c.F.Small.DrawCentered(c.R, "96 DPI",
            new Rect(box.X + 8, box.CenterY + 8, 70, 16), t.TextDisabled);

        c.F.Huge.Draw(c.R, "Аа", box.X + 110, box.CenterY - c.F.Huge.Height, t.Text);
        c.F.Small.DrawCentered(c.R, "144 DPI",
            new Rect(box.X + 100, box.CenterY + 8, 90, 16), t.TextDisabled);

        var note = new Rect(box.X + 220, box.Y + 8, box.W - 228, box.H - 16);
        foreach (string line in c.F.Small.Wrap(L.T("whatsnew.dpi_note"), note.W))
        {
            c.F.Small.Draw(c.R, line, note.X, note.Y, t.TextDisabled);
            note.CutTop(c.F.Small.Height + 3);
        }
    }

    void DrawDragArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        var from = new Rect(box.X + 10, box.CenterY - 24, 48, 48);
        var to = new Rect(box.Right - 58, box.CenterY - 24, 48, 48);
        Icons.Draw(c.R, IconId.Folder, from);
        Icons.Draw(c.R, IconId.FolderOpen, to);

        // The file in mid-air, on a dotted path between the two.
        float y = box.CenterY;
        for (float x = from.Right + 10; x < to.X - 12; x += 9)
            c.R.FillRect(new Rect(x, y - 1, 4, 2), t.ControlBorder);

        var carried = new Rect(box.CenterX - 8, y - 26, 16, 16);
        Icons.Draw(c.R, IconId.TextFile, carried);

        var ghost = new Rect(carried.X + 14, carried.Y + 12, 128, 20);
        c.R.FillRect(ghost, t.TooltipBack);
        c.R.DrawRect(ghost, t.TooltipBorder);
        c.F.Small.Draw(c.R, L.T("whatsnew.dnd_ghost"), ghost.X + 5,
                       ghost.CenterY - c.F.Small.Height * 0.5f, t.TooltipText);

        c.R.DrawRect(to.Inflate(3), t.Selection);
    }

    void DrawVolumeArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // The tray, the speaker, and the panel it drops.
        var bar = new Rect(box.X, box.Bottom - 22, box.W, 22);
        c.R.FillRectV(bar, t.TaskbarTop, t.TaskbarBottom);

        var speaker = new Rect(box.CenterX - 8, bar.CenterY - 8, 16, 16);
        Icons.Draw(c.R, IconId.Volume, speaker);

        var panel = new Rect(box.CenterX - 37, box.Y + 6, 74, box.H - 34);
        c.R.FillRect(panel.Offset(2, 2), Color.Rgba(0x000000, 40));
        c.R.FillRect(panel, t.Face);
        c.R.DrawRect(panel, t.MenuBorder);

        c.F.Small.DrawCentered(c.R, L.T("tray.volume_label"),
                               new Rect(panel.X, panel.Y + 5, panel.W, 16), t.Text);

        var groove = new Rect(panel.CenterX - 2, panel.Y + 26, 4, panel.H - 60);
        c.R.FillRect(groove, t.FaceDark);
        c.R.DrawRect(groove, t.ControlBorder);

        var thumb = new Rect(panel.CenterX - 6, groove.Y + groove.H * 0.3f, 12, 16);
        c.R.RoundedRectV(thumb, 2, t.FaceLight, t.FaceDark, t.ControlBorder, 1);

        c.F.Small.DrawCentered(c.R, "70%",
                               new Rect(panel.X, groove.Bottom + 4, panel.W, 16), t.TextDisabled);
    }

    void DrawCustomArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // A folder of programs, one of which came from outside.
        var folder = new Rect(box.X + 12, box.CenterY - 20, 40, 40);
        Icons.Draw(c.R, IconId.Folder, folder);
        c.F.Small.DrawCentered(c.R, "apps",
                               new Rect(folder.X - 8, folder.Bottom + 2, 56, 16), t.TextDisabled);

        string[] names = { "Miminus.App.Paint.dll", "Miminus.App.Sheet.dll", "HelloProgram.dll" };
        float y = box.Y + 14;
        for (int i = 0; i < names.Length; i++)
        {
            bool mine = i == names.Length - 1;
            var row = new Rect(folder.Right + 18, y, box.Right - folder.Right - 26, 26);
            c.R.RoundedRect(row, 3, mine ? t.Accent.WithAlpha(50) : t.Face,
                            mine ? t.Accent : t.ControlBorder, 1);
            Icons.Draw(c.R, mine ? IconId.Star : IconId.Program,
                       new Rect(row.X + 5, row.CenterY - 8, 16, 16));
            c.F.MonoSmall.Draw(c.R, names[i], row.X + 26,
                               row.CenterY - c.F.MonoSmall.Height * 0.5f, t.Text);
            y += 32;
        }
    }

    void DrawStopArt(UiContext c, Rect box)
    {
        // The screen itself, in miniature, in its own colours.
        var screen = box.Deflate(box.W * 0.12f, 6, box.W * 0.12f, 6);
        c.R.FillRect(screen, Color.Rgb(0x0000AA));

        var font = c.F.MonoSmall;
        float y = screen.Y + 10;
        string[] lines =
        {
            L.T("bsod.title"),
            "",
            BlueScreen.StopCode,
            "",
            "*** STOP: " + BlueScreen.StopCode,
            "    System.InvalidOperationException",
            "",
            L.F("bsod.dumping_memory", 60),
        };

        foreach (string line in lines)
        {
            if (line.Length > 0)
                font.Draw(c.R, line, screen.X + 12, y, Color.White);
            y += font.Height + 1;
        }
    }

    void DrawFolderArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        var folder = new Rect(box.CenterX - 24, box.Y + 10, 48, 48);
        Icons.Draw(c.R, IconId.Folder, folder);
        c.F.UiBold.DrawCentered(c.R, "Windows",
                                new Rect(box.X, folder.Bottom + 4, box.W, 20), t.Text);

        // The key that has to be held, and the outcome.
        var cap = new Rect(box.CenterX - 92, folder.Bottom + 30, 54, 24);
        c.R.RoundedRectV(cap, 4, t.FaceLight, t.FaceDark, t.ControlBorder, 1);
        c.F.Small.DrawCentered(c.R, "Ctrl", cap, t.Text);

        c.F.Ui.Draw(c.R, "+  Delete", cap.Right + 10, cap.CenterY - c.F.Ui.Height * 0.5f, t.Text);

        c.F.Small.DrawCentered(c.R, L.T("fs.windows_deleted_short"),
                               new Rect(box.X, cap.Bottom + 12, box.W, 18), t.TextDisabled);
    }

    void DrawDoneArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        Icons.Draw(c.R, IconId.Star, new Rect(box.CenterX - 24, box.Y + 16, 48, 48));
        c.F.Big.DrawCentered(c.R, L.T("whatsnew.done_headline"),
                             new Rect(box.X, box.CenterY + 8, box.W, 30), t.Text);
        c.F.Ui.DrawCentered(c.R, L.T("shell.network_checked_verify_your_connection_setti"),
                            new Rect(box.X, box.Bottom - 26, box.W, 20), t.TextDisabled);
    }
}
