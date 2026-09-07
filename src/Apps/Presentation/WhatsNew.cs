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

    enum Art { Cover, Update, Rename, Taskbar, WinKey, WindowsFolder, Speech, Lazy, Done }

    static readonly Card[] Cards =
    {
        new("whatsnew.cover_title",   "whatsnew.cover_body",   Art.Cover),
        new("whatsnew.update_title",  "whatsnew.update_body",  Art.Update),
        new("whatsnew.rename_title",  "whatsnew.rename_body",  Art.Rename),
        new("whatsnew.taskbar_title", "whatsnew.taskbar_body", Art.Taskbar),
        new("whatsnew.winkey_title",  "whatsnew.winkey_body",  Art.WinKey),
        new("whatsnew.folder_title",  "whatsnew.folder_body",  Art.WindowsFolder),
        new("whatsnew.speech_title",  "whatsnew.speech_body",  Art.Speech),
        new("whatsnew.lazy_title",    "whatsnew.lazy_body",    Art.Lazy),
        new("whatsnew.done_title",    "whatsnew.done_body",    Art.Done),
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
            case Art.Update: DrawUpdateArt(c, box); break;
            case Art.Rename: DrawRenameArt(c, box); break;
            case Art.Taskbar: DrawTaskbarArt(c, box); break;
            case Art.WinKey: DrawWinKeyArt(c, box); break;
            case Art.WindowsFolder: DrawFolderArt(c, box); break;
            case Art.Speech: DrawSpeechArt(c, box); break;
            case Art.Lazy: DrawLazyArt(c, box); break;
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

    void DrawUpdateArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        Icons.Draw(c.R, IconId.Shield, new Rect(box.X + 8, box.CenterY - 24, 48, 48));

        // The four steps of the cycle, with the third one running.
        string[] steps =
        {
            "whatsnew.step_download", "whatsnew.step_verify",
            "whatsnew.step_unpack", "whatsnew.step_restart",
        };

        float x = box.X + 72;
        float w = (box.W - 80) / steps.Length;
        for (int i = 0; i < steps.Length; i++)
        {
            var cell = new Rect(x + i * w, box.CenterY - 26, w - 8, 52);
            bool done = i < 3;
            c.R.RoundedRect(cell, 3, done ? t.Accent.WithAlpha(40) : t.Face, t.ControlBorder, 1);
            c.F.Small.DrawCentered(c.R, L.T(steps[i]),
                                   new Rect(cell.X, cell.Y + 8, cell.W, 16), t.Text);
            W.ProgressBar(c, new Rect(cell.X + 8, cell.Y + 28, cell.W - 16, 10), done ? 1 : 0.35f);
        }
    }

    void DrawRenameArt(UiContext c, Rect box)
    {
        var t = c.Theme;
        var icon = new Rect(box.CenterX - 24, box.Y + 12, 48, 48);
        Icons.Draw(c.R, IconId.Folder, icon);

        // The edit box, with the name selected the way it opens.
        var field = new Rect(box.CenterX - 96, icon.Bottom + 12, 192, 24);
        c.R.FillRect(field, t.FieldBack);
        c.R.DrawRect(field, t.ControlBorderHot);

        string name = L.T("whatsnew.rename_example");
        float w = c.F.Ui.Measure(name);
        c.R.FillRect(new Rect(field.X + 5, field.Y + 3, w, field.H - 6), t.Selection);
        c.F.Ui.Draw(c.R, name, field.X + 5, field.CenterY - c.F.Ui.Height * 0.5f, t.SelectionText);

        c.F.Small.DrawCentered(c.R, L.T("whatsnew.rename_hint"),
                               new Rect(box.X, field.Bottom + 10, box.W, 18), t.TextDisabled);
    }

    void DrawTaskbarArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // A taskbar, and above it the space an auto-hidden one gives back.
        var free = new Rect(box.X, box.Y, box.W, box.H - 34);
        c.R.FillRect(free, t.Face);
        c.F.Small.DrawCentered(c.R, L.T("whatsnew.taskbar_space"),
                               new Rect(free.X, free.CenterY - 8, free.W, 18), t.TextDisabled);
        c.R.DrawRect(free, t.ControlBorder);

        var bar = new Rect(box.X, box.Bottom - 26, box.W, 26);
        c.R.FillRectV(bar, t.TaskbarTop, t.TaskbarBottom);

        var start = new Rect(bar.X + 3, bar.Y + 3, 56, bar.H - 6);
        c.R.RoundedRectV(start, 3, t.StartTop, t.StartBottom);
        c.F.Small.DrawCentered(c.R, L.T("tbprops.start"), start, Color.White);

        var grouped = new Rect(start.Right + 8, bar.Y + 3, 118, bar.H - 6);
        c.R.RoundedRectV(grouped, 3, t.TaskButtonFace, t.TaskButtonFace.Shade(0.85f));
        Icons.Draw(c.R, IconId.Notepad, new Rect(grouped.X + 4, grouped.CenterY - 7, 14, 14));
        c.F.Small.Draw(c.R, "3  " + L.T("tbprops.preview_app"), grouped.X + 22,
                       grouped.CenterY - c.F.Small.Height * 0.5f, t.TaskbarText);

        var tray = new Rect(bar.Right - 62, bar.Y + 2, 60, bar.H - 4);
        c.R.FillRectV(tray, t.TrayBack, t.TrayBack.Shade(0.85f));
        c.F.Small.DrawRight(c.R, L.Time(_shell.Now),
                            new Rect(tray.X, tray.Y, tray.W - 5, tray.H), t.TaskbarText);
    }

    void DrawWinKeyArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // The key itself, and what it now opens.
        var cap = new Rect(box.X + 10, box.CenterY - 26, 76, 52);
        c.R.RoundedRectV(cap, 5, t.FaceLight, t.FaceDark, t.ControlBorder, 1);
        c.R.RoundedRect(cap.Deflate(4), 3, t.Face, t.ControlBorder, 1);

        // A four-pane window mark, drawn rather than written.
        float s = 7, gx = cap.CenterX - s - 1, gy = cap.CenterY - s - 1;
        for (int i = 0; i < 4; i++)
            c.R.FillRect(new Rect(gx + (i % 2) * (s + 2), gy + (i / 2) * (s + 2), s, s), t.Accent);

        var combos = new[]
        {
            ("Win", "whatsnew.combo_start"), ("Win+E", "whatsnew.combo_computer"),
            ("Win+R", "whatsnew.combo_run"), ("Win+D", "whatsnew.combo_desktop"),
            ("Win+U", "whatsnew.combo_update"),
        };

        float y = box.Y + 6;
        foreach (var (keys, key) in combos)
        {
            c.F.MonoSmall.Draw(c.R, keys, cap.Right + 20, y, t.Accent);
            c.F.Small.Draw(c.R, L.T(key), cap.Right + 90, y, t.Text);
            y += c.F.Small.Height + 8;
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

    void DrawSpeechArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        Icons.Draw(c.R, IconId.Volume, new Rect(box.X + 12, box.CenterY - 20, 40, 40));

        // Sound leaving the speaker: three arcs of growing width.
        for (int i = 1; i <= 3; i++)
            c.R.FillRect(new Rect(box.X + 56 + i * 7, box.CenterY - 3 * i, 3, 6 * i),
                         t.Accent.WithAlpha((byte)(200 - i * 40)));

        float x = box.X + 96;
        c.F.Big.Draw(c.R, "МихаИл ГревцОв", x, box.Y + 22, t.Text);
        c.F.Ui.Draw(c.R, "[михаИл грефцОф]", x, box.Y + 26 + c.F.Big.Height, t.TextDisabled);
        c.F.Small.Draw(c.R, L.T("whatsnew.speech_cases"), x,
                       box.Y + 34 + c.F.Big.Height + c.F.Ui.Height, t.TextDisabled);
    }

    void DrawLazyArt(UiContext c, Rect box)
    {
        var t = c.Theme;

        // Twelve libraries; the two in use are filled in.
        const int Total = 12, Loaded = 2;
        float w = (box.W - 16) / 6, h = 34;

        for (int i = 0; i < Total; i++)
        {
            var cell = new Rect(box.X + (i % 6) * w, box.Y + 8 + (i / 6) * (h + 10), w - 10, h);
            bool loaded = i < Loaded;
            c.R.RoundedRect(cell, 3, loaded ? t.Accent.WithAlpha(60) : t.Face,
                            loaded ? t.Accent : t.ControlBorder, 1);
            Icons.Draw(c.R, IconId.Program, new Rect(cell.X + 6, cell.CenterY - 9, 18, 18));
            c.F.Small.Draw(c.R, loaded ? L.T("sys.apps_loaded") : L.T("sys.apps_unloaded"),
                           cell.X + 28, cell.CenterY - c.F.Small.Height * 0.5f,
                           loaded ? t.Text : t.TextDisabled);
        }

        c.F.Small.DrawCentered(c.R, L.F("sys.apps_summary", Loaded, _shell.Programs.Count),
                               new Rect(box.X, box.Bottom - 20, box.W, 18), t.TextDisabled);
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
