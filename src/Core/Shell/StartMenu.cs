using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>The XP two-column Start menu: pinned programs and recents on the
/// left, shell places on the right, log-off and shut-down in the footer.</summary>
public sealed class StartMenu
{
    readonly ShellHost _shell;

    const float MenuW = 396;
    const float HeaderH = 56;
    const float FooterH = 40;
    const float LeftW = 200;

    public StartMenu(ShellHost shell) => _shell = shell;

    sealed record Entry(string Key, IconId Icon, string App, string SubKey = null,
                        bool Separator = false, VNode Node = null);

    Entry[] Pinned => new[]
    {
        new Entry("start.internet", IconId.Firefox, "browser",
                  "start.firefox_web_browser"),
        new Entry("start.e_mail", IconId.Mail, "mail",
                  "start.outlook_express"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.notepad", IconId.Notepad, "notepad"),
        new Entry("start.media_player", IconId.MediaPlayer, "player"),
        new Entry("start.paint", IconId.Paint, "paint"),
        new Entry("start.calculator_plus", IconId.Calculator, "calculator"),
        new Entry("start.minesweeper", IconId.Minesweeper, "minesweeper"),
        new Entry("start.miminus_sheet", IconId.Spreadsheet, "spreadsheet"),
        new Entry("start.command_prompt", IconId.Terminal, "terminal"),
        new Entry("start.all_in_one", IconId.Settings, "allinone"),
    };

    Entry[] Places => new[]
    {
        new Entry("start.my_documents", IconId.MyDocuments, "explorer", Node: _shell.Fs.MyDocuments),
        new Entry("start.my_pictures", IconId.MyPictures, "explorer", Node: _shell.Fs.MyPictures),
        new Entry("start.my_music", IconId.MyMusic, "explorer", Node: _shell.Fs.MyMusic),
        new Entry("start.my_computer", IconId.MyComputer, "mycomputer"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.control_panel", IconId.ControlPanel, "controlpanel"),
        new Entry("start.windows_update", IconId.Shield, "update"),
        new Entry("start.connect_to", IconId.Network, "network"),
        new Entry("start.printers_and_faxes", IconId.Printer, "printers"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.help_and_support", IconId.Help, "help"),
        new Entry("start.search", IconId.Search, "search"),
        new Entry("start.run", IconId.Run, "run"),
    };

    public Rect Bounds(UiContext c)
    {
        float h = HeaderH + FooterH + 300;
        // Grow to fit whichever column is taller.
        float leftH = Pinned.Sum(e => e.Separator ? 8f : 34f) + 30;
        float rightH = Places.Sum(e => e.Separator ? 8f : 27f) + 8;
        h = HeaderH + FooterH + MathF.Max(leftH, rightH) + 8;

        float bottom = c.ScreenH - c.Theme.TaskbarHeight;
        return new Rect(2, MathF.Max(2, bottom - h), MenuW, MathF.Min(h, bottom - 4));
    }

    public void Draw(UiContext c)
    {
        var t = c.Theme;
        var r = Bounds(c);

        // Shadow + panel.
        c.R.FillRect(r.Offset(4, 4), Color.Rgba(0x000000, 55));
        c.R.RoundedRect(r, 8, t.StartMenuRight, t.StartMenuBorder, 1);

        var header = new Rect(r.X, r.Y, r.W, HeaderH);
        var footer = new Rect(r.X, r.Bottom - FooterH, r.W, FooterH);
        var body = new Rect(r.X, header.Bottom, r.W, footer.Y - header.Bottom);

        // ---- header ------------------------------------------------------
        c.R.RoundedRectV(new Rect(header.X, header.Y, header.W, 16), 8,
                         t.StartMenuHeaderTop, t.StartMenuHeaderTop);
        c.R.FillRectV(new Rect(header.X, header.Y + 8, header.W, header.H - 8),
                      t.StartMenuHeaderTop, t.StartMenuHeaderBottom);
        c.R.FillRect(new Rect(header.X, header.Bottom - 2, header.W, 2), Color.Rgba(0xFFFFFF, 60));

        var avatar = new Rect(header.X + 8, header.Y + 7, 42, 42);
        c.R.RoundedRect(avatar, 4, Color.Rgba(0xFFFFFF, 200), Color.Rgba(0xFFFFFF, 230), 2);
        // A little user glyph: head and shoulders.
        c.R.FillCircle(avatar.CenterX, avatar.Y + 15, 8, Color.Rgb(0x3C82C8));
        c.R.FillCircle(avatar.CenterX, avatar.Bottom + 2, 15, Color.Rgb(0x3C82C8));
        c.R.PushClip(avatar);
        c.R.FillCircle(avatar.CenterX, avatar.Bottom + 2, 15, Color.Rgb(0x3C82C8));
        c.R.PopClip();

        c.F.Caption.Draw(c.R, "Admin", avatar.Right + 10, header.CenterY - c.F.Caption.Height * 0.5f + 1,
                         Color.Rgba(0x000000, 110));
        c.F.Caption.Draw(c.R, "Admin", avatar.Right + 9, header.CenterY - c.F.Caption.Height * 0.5f,
                         Color.White);

        // ---- columns -----------------------------------------------------
        var left = new Rect(body.X + 2, body.Y, LeftW, body.H);
        var right = new Rect(left.Right, body.Y, body.Right - left.Right - 2, body.H);

        c.R.FillRect(left, t.StartMenuLeft);
        c.R.FillRect(right, t.StartMenuRight);
        c.R.FillRect(new Rect(right.X, right.Y, 1, right.H), Color.Rgba(0x000000, 25));

        DrawLeftColumn(c, left);
        DrawRightColumn(c, right);

        // ---- footer ------------------------------------------------------
        c.R.FillRectV(footer, t.StartMenuFooterTop, t.StartMenuFooterBottom);
        c.R.RoundedRectV(new Rect(footer.X, footer.Bottom - 16, footer.W, 16), 8,
                         t.StartMenuFooterBottom, t.StartMenuFooterBottom);
        c.R.FillRect(new Rect(footer.X, footer.Y, footer.W, 1), Color.Rgba(0xFFFFFF, 70));

        float bw = 132;
        var logoff = new Rect(footer.Right - bw * 2 - 16, footer.Y + 6, bw, footer.H - 12);
        var shutdown = new Rect(footer.Right - bw - 8, footer.Y + 6, bw, footer.H - 12);

        if (FooterButton(c, logoff, IconId.Logoff, L.T("start.log_off")))
        {
            _shell.Taskbar.CloseStart(c);
            _shell.BeginLogOff(c);
        }
        if (FooterButton(c, shutdown, IconId.Shutdown, L.T("start.turn_off_computer")))
        {
            _shell.Taskbar.CloseStart(c);
            _shell.ShowShutdownDialog(c);
        }

        // Clicking anywhere outside dismisses the menu.
        if ((c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) &&
            !r.Contains(c.MouseX, c.MouseY))
        {
            var startBtn = new Rect(0, c.ScreenH - c.Theme.TaskbarHeight, 100, c.Theme.TaskbarHeight);
            if (!startBtn.Contains(c.MouseX, c.MouseY)) _shell.Taskbar.CloseStart(c);
        }

        if (c.In.KeyPressed(Keys.Escape)) { _shell.Taskbar.CloseStart(c); c.KeyboardHandled = true; }

        c.MouseHandled = true;
    }

    void DrawLeftColumn(UiContext c, Rect col)
    {
        float y = col.Y + 4;
        int idx = 0;
        foreach (var e in Pinned)
        {
            if (e.Separator)
            {
                c.R.FillRect(new Rect(col.X + 8, y + 3, col.W - 16, 1), Color.Rgb(0xC8D4E8));
                y += 8;
                continue;
            }

            var row = new Rect(col.X + 2, y, col.W - 4, 34);
            DrawStartRow(c, row, e, 26, hasSubtitle: e.SubKey != null);
            if (c.Clicked(row)) Activate(c, e);
            y += 34;
            idx++;
        }

        // "Все программы" strip pinned to the bottom of the left column.
        var all = new Rect(col.X + 2, col.Bottom - 26, col.W - 4, 24);
        bool hot = c.Hovering(all);
        if (hot) c.R.FillRect(all, c.Theme.Selection);
        c.R.FillRect(new Rect(col.X + 8, all.Y - 3, col.W - 16, 1), Color.Rgb(0xC8D4E8));

        string label = L.T("start.all_programs");
        c.F.UiBold.Draw(c.R, label, all.X + 8, all.CenterY - c.F.UiBold.Height * 0.5f,
                        hot ? Color.White : c.Theme.Text);

        float ax = all.X + 12 + c.F.UiBold.Measure(label);
        var arrowBox = new Rect(ax, all.Y, 16, all.H);
        c.R.FillCircle(arrowBox.CenterX, arrowBox.CenterY, 7, Color.Rgb(0x3C9B27));
        W.Arrow(c, arrowBox, 1, Color.White, 3.2f);

        if (c.Clicked(all)) ShowAllPrograms(c, all);
    }

    void DrawRightColumn(UiContext c, Rect col)
    {
        float y = col.Y + 4;
        foreach (var e in Places)
        {
            if (e.Separator)
            {
                c.R.FillRect(new Rect(col.X + 8, y + 3, col.W - 16, 1), Color.Rgb(0xAEC4E0));
                y += 8;
                continue;
            }
            var row = new Rect(col.X + 2, y, col.W - 4, 27);
            DrawStartRow(c, row, e, 20, hasSubtitle: false, bold: true);
            if (c.Clicked(row)) Activate(c, e);
            y += 27;
        }
    }

    void DrawStartRow(UiContext c, Rect row, Entry e, float iconSize, bool hasSubtitle, bool bold = false)
    {
        bool hot = c.Hovering(row);
        if (hot) c.R.RoundedRect(row, 3, c.Theme.Selection);

        var ic = new Rect(row.X + 5, row.CenterY - iconSize * 0.5f, iconSize, iconSize);
        Icons.Draw(c.R, e.Icon, ic);

        Color fg = hot ? Color.White : c.Theme.Text;
        string name = L.T(e.Key);
        var font = bold ? c.F.UiBold : c.F.Ui;

        if (hasSubtitle)
        {
            font.Draw(c.R, name, ic.Right + 8, row.Y + 4, fg);
            c.F.Small.Draw(c.R, L.T(e.SubKey), ic.Right + 8, row.Y + 4 + font.Height,
                           hot ? Color.Rgba(0xFFFFFF, 200) : Color.Rgb(0x606060));
        }
        else
        {
            font.Draw(c.R, name, ic.Right + 8, row.CenterY - font.Height * 0.5f, fg);
        }
    }

    bool FooterButton(UiContext c, Rect r, IconId icon, string label)
    {
        bool hot = c.Hovering(r);
        if (hot) c.R.RoundedRect(r, 3, Color.Rgba(0xFFFFFF, 55));

        var ic = new Rect(r.X + 4, r.CenterY - 11, 22, 22);
        Icons.Draw(c.R, icon, ic);

        c.R.PushClip(r);
        c.F.Ui.Draw(c.R, label, ic.Right + 6, r.CenterY - c.F.Ui.Height * 0.5f, Color.White);
        c.R.PopClip();

        return c.Clicked(r);
    }

    void Activate(UiContext c, Entry e)
    {
        _shell.Taskbar.CloseStart(c);
        _shell.Launch(c, e.App, e.Node);
    }

    void ShowAllPrograms(UiContext c, Rect anchor)
    {
        var accessories = new List<MenuItem>
        {
            MenuItem.Of(L.T("start.notepad"), () => Go(c, "notepad", null), IconId.Notepad),
            MenuItem.Of("Paint", () => Go(c, "paint", null), IconId.Paint),
            MenuItem.Of(L.T("start.calculator_plus"), () => Go(c, "calculator", null), IconId.Calculator),
            MenuItem.Of(L.T("start.command_prompt"), () => Go(c, "terminal", null), IconId.Terminal),
            MenuItem.Of(L.T("start.all_in_one"), () => Go(c, "allinone", null), IconId.Settings),
            MenuItem.Of(L.T("start.voice_recognition"), () => Go(c, "voice", null), IconId.Volume),
            MenuItem.Of(L.T("start.windows_explorer"), () => Go(c, "mycomputer", null), IconId.Folder),
        };

        var games = new List<MenuItem>
        {
            MenuItem.Of(L.T("start.minesweeper"), () => Go(c, "minesweeper", null), IconId.Minesweeper),
        };

        var tools = new List<MenuItem>
        {
            MenuItem.Of(L.T("start.grevtsov_antivirus"),
                        () => Go(c, "notepad", _shell.Fs.AntivirusFile), IconId.Antivirus),
            MenuItem.Of(L.T("start.task_manager"), () => Go(c, "taskmgr", null), IconId.Settings),
            MenuItem.Of(L.T("start.display_properties"), () => Go(c, "display", null), IconId.Display),
            MenuItem.Of(L.T("start.windows_update"), () => Go(c, "update", null), IconId.Shield),
            MenuItem.Of(L.T("whatsnew.title"), () => Go(c, "whatsnew", null), IconId.Star),
        };

        // Anything registered that the groups above do not already name — a
        // program dropped into apps/ by somebody else, most of all — is listed
        // here, so a custom program is reachable without anyone editing a menu.
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "notepad", "paint", "calculator", "terminal", "allinone", "voice", "mycomputer",
            "minesweeper", "taskmgr", "display", "update", "whatsnew", "browser", "orega",
            "player", "spreadsheet", "explorer", "controlpanel", "sound", "language",
            "effects", "appearance", "advanced", "taskbarprops",
        };

        var others = _shell.Programs.All
            .Where(p => !listed.Contains(p.Id))
            .OrderBy(p => L.T(p.NameKey), StringComparer.CurrentCulture)
            .Select(p => MenuItem.Of(L.T(p.NameKey), () => Go(c, p.Id, null), p.Icon))
            .ToList();

        var items = new List<MenuItem>
        {
            MenuItem.Sub(L.T("start.accessories"), accessories, IconId.Folder),
            MenuItem.Sub(L.T("start.games"), games, IconId.Folder),
            MenuItem.Sub(L.T("start.system_tools"), tools, IconId.Folder),
        };

        if (others.Count > 0)
            items.Add(MenuItem.Sub(L.T("start.other_programs"), others, IconId.Program));

        items.AddRange(new[]
        {
            MenuItem.Sep(),
            MenuItem.Of(L.T("start.firefox_web_browser"), () => Go(c, "browser", null), IconId.Firefox),
            MenuItem.Of(L.T("start.orega"), () => Go(c, "orega", null), IconId.Opera),
            MenuItem.Of(L.T("start.miminus_media_player"), () => Go(c, "player", null), IconId.MediaPlayer),
            MenuItem.Of(L.T("start.miminus_sheet"), () => Go(c, "spreadsheet", null), IconId.Spreadsheet),
            MenuItem.Sep(),
            MenuItem.Of(L.T("start.about_miminus"), () => Go(c, "about", null), IconId.DlgInfo),
        });

        _shell.Menus.Open(items, anchor.Right - 4, anchor.Bottom - 8, this, c);
    }

    void Go(UiContext c, string app, VNode node)
    {
        _shell.Taskbar.CloseStart(c);
        _shell.Launch(c, app, node);
    }
}
