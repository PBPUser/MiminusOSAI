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

    enum Art { Cover, Setup, Settings, Dpi, DragDrop, Volume, Custom, Stop, WindowsFolder, Done }

    static readonly Card[] Cards =
    {
        new("whatsnew.cover_title",    "whatsnew.cover_body",    Art.Cover),
        new("whatsnew.setup_title",    "whatsnew.setup_body",    Art.Setup),
        new("whatsnew.settings_title", "whatsnew.settings_body", Art.Settings),
        new("whatsnew.dpi_title",      "whatsnew.dpi_body",      Art.Dpi),
        new("whatsnew.dnd_title",      "whatsnew.dnd_body",      Art.DragDrop),
        new("whatsnew.volume_title",   "whatsnew.volume_body",   Art.Volume),
        new("whatsnew.custom_title",   "whatsnew.custom_body",   Art.Custom),
        new("whatsnew.stop_title",     "whatsnew.stop_body",     Art.Stop),
        new("whatsnew.folder_title",   "whatsnew.folder_body",   Art.WindowsFolder),
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
            default: DrawDoneArt(c, box); break;
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
