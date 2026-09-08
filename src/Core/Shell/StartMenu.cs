using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>The Start menu, in the shape Windows 7 gave it.
///
/// It is one tall panel rather than two squat columns: the programs down the
/// left with the search box under them, the places down the right in their own
/// tinted strip, the account picture round and half out of the top corner, and
/// the shutdown button in the bottom right with an arrow for the other ways to
/// stop.
///
/// The search box is the part that earned the redesign, and it works: typing
/// searches every registered program — including one somebody dropped into
/// <c>apps/</c> — and the results replace the program list while the query
/// stands. Escape, or emptying the box, puts the list back.
///
/// It still unrolls out of the taskbar rather than appearing, and it is still
/// only half of the Start in this system: «Начальный экран» at the top of the
/// right column is the other one.</summary>
public sealed class StartMenu
{
    readonly ShellHost _shell;

    const float MenuW = 412;
    const float LeftW = 232;
    const float FooterH = 42;
    const float SearchH = 30;
    const float RowH = 32;

    public StartMenu(ShellHost shell) => _shell = shell;

    /// <summary>How far the menu has unrolled, 0 to 1. The panel is drawn whole
    /// and clipped to a window that grows up out of the taskbar, which is what
    /// the original animation was: nothing moves, the mask does.</summary>
    float _phase;

    /// <summary>True while the menu is on screen at all — open, or still on its
    /// way in or out.</summary>
    public bool Visible => _phase > 0.002f;

    /// <summary>True once it is all the way out and can be clicked. A menu that
    /// is still unrolling takes no input: half a menu is not a target.</summary>
    public bool Interactive => _phase > 0.995f;

    /// <summary>What has been typed into the search box. Seven put the caret
    /// there the moment the menu opened, and so does this.</summary>
    string _query = "";

    sealed record Entry(string Key, IconId Icon, string App, string SubKey = null,
                        bool Separator = false, VNode Node = null);

    Entry[] Pinned => new[]
    {
        new Entry("start.internet", IconId.Firefox, "browser", "start.firefox_web_browser"),
        new Entry("start.e_mail", IconId.Mail, "mail", "start.outlook_express"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.notepad", IconId.Notepad, "notepad"),
        new Entry("start.media_player", IconId.MediaPlayer, "player"),
        new Entry("start.paint", IconId.Paint, "paint"),
        new Entry("start.calculator_plus", IconId.Calculator, "calculator"),
        new Entry("start.minesweeper", IconId.Minesweeper, "minesweeper"),
        new Entry("start.miminus_sheet", IconId.Spreadsheet, "spreadsheet"),
        new Entry("start.command_prompt", IconId.Terminal, "terminal"),
    };

    Entry[] Places => new[]
    {
        new Entry("start8.start_screen", IconId.Tiles, "$startscreen"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.my_documents", IconId.MyDocuments, "explorer", Node: _shell.Fs.MyDocuments),
        new Entry("start.my_pictures", IconId.MyPictures, "explorer", Node: _shell.Fs.MyPictures),
        new Entry("start.my_music", IconId.MyMusic, "explorer", Node: _shell.Fs.MyMusic),
        new Entry("start.my_computer", IconId.MyComputer, "mycomputer"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.control_panel", IconId.ControlPanel, "controlpanel"),
        new Entry("devmgr.title", IconId.Devices, "devmgr"),
        new Entry("start.windows_update", IconId.Shield, "update"),
        new Entry(null, IconId.None, null, Separator: true),
        new Entry("start.help_and_support", IconId.Help, "help"),
        new Entry("start.run", IconId.Run, "run"),
    };

    public Rect Bounds(UiContext c)
    {
        float leftH = Pinned.Sum(e => e.Separator ? 8f : RowH) + 34 + SearchH + 12;
        float rightH = Places.Sum(e => e.Separator ? 9f : 26f) + 70;
        float h = MathF.Max(leftH, rightH) + FooterH + 10;

        // The menu comes out of the Start button, so it follows the bar: it
        // hangs down from a bar along the top, and stands beside one up the side.
        var bar = _shell.Taskbar.Bounds(c);
        return _shell.Taskbar.Edge switch
        {
            TaskbarEdge.Top => new Rect(2, bar.Bottom + 2, MenuW,
                                        MathF.Min(h, c.ScreenH - bar.Bottom - 6)),
            TaskbarEdge.Left => new Rect(bar.Right + 2, 2, MenuW, MathF.Min(h, c.ScreenH - 6)),
            TaskbarEdge.Right => new Rect(MathF.Max(2, bar.X - MenuW - 2), 2, MenuW,
                                          MathF.Min(h, c.ScreenH - 6)),
            _ => new Rect(2, MathF.Max(2, bar.Y - h), MenuW, MathF.Min(h, bar.Y - 4)),
        };
    }

    /// <summary>Advances the animation and draws the menu through it.</summary>
    public void Draw(UiContext c)
    {
        float target = _shell.Taskbar.StartOpen ? 1 : 0;
        if (!_shell.Settings.Animations) _phase = target;
        _phase += Math.Clamp(target - _phase, -1f, 1f) * MathF.Min(1, c.Dt * 14);
        if (MathF.Abs(target - _phase) < 0.006f) _phase = target;

        if (target == 0 && _phase == 0) _query = "";
        if (!Visible) return;

        var full = Bounds(c);
        float shown = MathF.Max(2, full.H * _phase);

        bool savedMouse = c.MouseHandled;
        if (!Interactive) c.MouseHandled = true;

        // The account picture stands proud of the top edge, so the clip has to
        // be a little taller than the panel it is revealing.
        c.R.PushClip(new Rect(full.X - 10, full.Bottom - shown - 30, full.W + 20, shown + 38));
        DrawPanel(c);
        c.R.PopClip();

        if (!Interactive) c.MouseHandled = savedMouse;
    }

    void DrawPanel(UiContext c)
    {
        var t = c.Theme;
        var r = Bounds(c);

        c.R.FillRect(r.Offset(4, 4), Color.Rgba(0x000000, 60));

        float rad = t.Id is ThemeId.Metro or ThemeId.HighContrast or ThemeId.Classic ? 0 : 6;
        c.R.RoundedRect(r, rad, t.StartMenuLeft, t.StartMenuBorder, 1);

        var left = new Rect(r.X + 1, r.Y + 1, LeftW, r.H - FooterH - 1);
        var right = new Rect(left.Right, r.Y + 1, r.Right - left.Right - 1, r.H - 2);

        // The right column is the tinted one and runs all the way down, which is
        // what puts the shutdown button inside it.
        c.R.FillRect(right, t.StartMenuRight);
        c.R.FillRect(new Rect(right.X, right.Y, 1, right.H), Color.Rgba(0x000000, 30));

        DrawAccount(c, right);
        DrawLeftColumn(c, left);
        DrawRightColumn(c, new Rect(right.X, right.Y + 66, right.W, right.H - 66 - FooterH));
        DrawFooter(c, new Rect(right.X, r.Bottom - FooterH, right.W, FooterH));

        HandleInput(c, r);
    }

    /// <summary>The round picture and the name at the top of the right column,
    /// with the picture half out of the menu — where seven put it, and the one
    /// detail that dates the design instantly.</summary>
    void DrawAccount(UiContext c, Rect right)
    {
        var t = c.Theme;
        float rad = 26;
        float cx = right.CenterX, cy = right.Y + 18;

        c.R.FillCircle(cx, cy, rad + 2, t.StartMenuBorder);
        c.R.FillCircle(cx, cy, rad, Color.Rgb(0xE8EEF4));

        c.R.PushClip(new Rect(cx - rad, cy - rad, rad * 2, rad * 2));
        c.R.FillCircle(cx, cy - rad * 0.22f, rad * 0.34f, t.Accent);
        c.R.FillCircle(cx, cy + rad * 0.72f, rad * 0.62f, t.Accent);
        c.R.PopClip();

        string name = c.F.Ui.Ellipsize(_shell.UserName, right.W - 12);
        float w = c.F.Ui.Measure(name);
        c.F.Ui.Draw(c.R, name, cx - w * 0.5f, cy + rad + 6, t.Text);
    }

    void DrawLeftColumn(UiContext c, Rect col)
    {
        var t = c.Theme;
        c.R.FillRect(col, t.StartMenuLeft);

        var area = col.Deflate(2, 4, 2, 4);
        var search = area.CutBottom(SearchH);
        var all = area.CutBottom(28);

        if (_query.Length > 0) DrawResults(c, area);
        else
        {
            float y = area.Y;
            foreach (var e in Pinned)
            {
                if (e.Separator)
                {
                    c.R.FillRect(new Rect(area.X + 8, y + 3, area.W - 16, 1),
                                 Color.Rgba(0x000000, 40));
                    y += 8;
                    continue;
                }

                var row = new Rect(area.X, y, area.W, RowH);
                if (row.Bottom > area.Bottom) break;

                DrawRow(c, row, L.T(e.Key), e.Icon, e.SubKey == null ? null : L.T(e.SubKey), 24);
                if (c.Clicked(row)) Activate(c, e);
                y += RowH;
            }
        }

        // ---- «Все программы» --------------------------------------------------
        c.R.FillRect(new Rect(all.X + 8, all.Y, all.W - 16, 1), Color.Rgba(0x000000, 40));

        var allRow = new Rect(all.X, all.Y + 2, all.W, all.H - 2);
        bool hot = c.Hovering(allRow);
        if (hot) c.R.FillRect(allRow, t.Hot);

        var arrow = new Rect(allRow.X + 6, allRow.CenterY - 8, 16, 16);
        c.R.FillCircle(arrow.CenterX, arrow.CenterY, 7, t.Accent);
        W.Arrow(c, arrow, 1, Color.White, 3.2f);

        c.F.UiBold.Draw(c.R, L.T("start.all_programs"), arrow.Right + 8,
                        allRow.CenterY - c.F.UiBold.Height * 0.5f, t.Text);
        if (c.Clicked(allRow)) ShowAllPrograms(c, allRow);

        // ---- the search box ---------------------------------------------------
        var box = new Rect(search.X + 6, search.Y, search.W - 12, search.H - 4);
        c.R.FillRect(box, t.FieldBack);
        c.R.DrawRect(box, t.FieldBorder);

        c.R.PushClip(box.Deflate(4, 0, 22, 0));
        if (_query.Length > 0)
            c.F.Ui.Draw(c.R, _query, box.X + 6, box.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        else
            c.F.Ui.Draw(c.R, L.T("start.search_box"), box.X + 6,
                        box.CenterY - c.F.Ui.Height * 0.5f, t.TextDisabled);
        c.R.PopClip();

        // The caret is in the box from the moment the menu opens, which is what
        // made typing straight into it the way this menu was used.
        if (Interactive && (int)(c.Time * 2) % 2 == 0)
            c.R.FillRect(new Rect(box.X + 7 + c.F.Ui.Measure(_query), box.Y + 5, 1.4f, box.H - 10),
                         t.Text);

        Icons.Draw(c.R, IconId.Search, new Rect(box.Right - 20, box.CenterY - 8, 16, 16));
    }

    /// <summary>What the search box finds: every registered program whose name
    /// contains what was typed, under a heading, the way seven grouped them.</summary>
    void DrawResults(UiContext c, Rect area)
    {
        var t = c.Theme;

        var hits = _shell.Programs.All
            .Select(p => (name: L.T(p.NameKey), icon: p.Icon, app: p.Id))
            .Where(x => x.name.Contains(_query, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(x => x.name, StringComparer.CurrentCulture)
            .ToList();

        var head = area.CutTop(20);
        c.F.UiBold.Draw(c.R, L.F("start.results", hits.Count), head.X + 6, head.Y + 2, t.Accent);
        c.R.FillRect(new Rect(head.X + 6, head.Bottom - 2, head.W - 12, 1), Color.Rgba(0x000000, 40));

        float y = area.Y;
        foreach (var (name, icon, app) in hits)
        {
            var row = new Rect(area.X, y, area.W, 26);
            if (row.Bottom > area.Bottom) break;

            DrawRow(c, row, name, icon, null, 18);
            if (c.Clicked(row)) Go(c, app, null);
            y += 26;
        }

        if (hits.Count == 0)
            c.F.Ui.Draw(c.R, L.T("start.no_results"), area.X + 8, area.Y + 6, t.TextDisabled);
    }

    void DrawRow(UiContext c, Rect row, string name, IconId icon, string subtitle, float iconSize)
    {
        var t = c.Theme;
        bool hot = c.Hovering(row);
        if (hot) c.R.FillRect(row, t.Hot);

        var ic = new Rect(row.X + 6, row.CenterY - iconSize * 0.5f, iconSize, iconSize);
        Icons.Draw(c.R, icon, ic);

        c.R.PushClip(row);
        if (subtitle != null)
        {
            c.F.Ui.Draw(c.R, name, ic.Right + 8, row.Y + 3, t.Text);
            c.F.Small.Draw(c.R, subtitle, ic.Right + 8, row.Y + 3 + c.F.Ui.Height, t.TextDisabled);
        }
        else c.F.Ui.Draw(c.R, name, ic.Right + 8, row.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        c.R.PopClip();
    }

    void DrawRightColumn(UiContext c, Rect col)
    {
        var t = c.Theme;
        float y = col.Y;

        foreach (var e in Places)
        {
            if (e.Separator)
            {
                c.R.FillRect(new Rect(col.X + 10, y + 4, col.W - 20, 1), Color.Rgba(0x000000, 35));
                y += 9;
                continue;
            }

            var row = new Rect(col.X + 2, y, col.W - 4, 26);
            if (row.Bottom > col.Bottom) break;

            if (c.Hovering(row)) c.R.FillRect(row, t.Hot);
            Icons.Draw(c.R, e.Icon, new Rect(row.X + 6, row.CenterY - 8, 16, 16));

            c.R.PushClip(row);
            c.F.Ui.Draw(c.R, L.T(e.Key), row.X + 28, row.CenterY - c.F.Ui.Height * 0.5f, t.Text);
            c.R.PopClip();

            if (c.Clicked(row)) Activate(c, e);
            y += 26;
        }
    }

    /// <summary>Seven's split button: the action the power page chose written
    /// on the face, and an arrow for all the others.</summary>
    void DrawFooter(UiContext c, Rect footer)
    {
        var t = c.Theme;

        string label = _shell.Settings.PowerButtonAction switch
        {
            1 => L.T("charm.sleep"),
            2 => L.T("start.log_off"),
            _ => L.T("charm.shutdown"),
        };

        var arrow = new Rect(footer.Right - 32, footer.Y + 7, 24, footer.H - 14);
        var main = new Rect(footer.X + 8, footer.Y + 7, arrow.X - footer.X - 10, footer.H - 14);

        DrawFooterButton(c, main, label, IconId.Shutdown);
        if (c.Clicked(main))
        {
            _shell.Taskbar.CloseStart(c);
            _shell.PowerButtonPressed(c);
        }

        DrawFooterButton(c, arrow, null, IconId.None);
        W.Arrow(c, arrow, 1, t.Text, 3.4f);

        if (c.Clicked(arrow))
            _shell.Menus.Open(new List<MenuItem>
            {
                MenuItem.Of(L.T("start.log_off"), () =>
                    { _shell.Taskbar.CloseStart(c); _shell.BeginLogOff(c); }, IconId.Logoff),
                MenuItem.Of(L.T("charm.sleep"), () =>
                    { _shell.Taskbar.CloseStart(c); _shell.LockScreenNow(c); }, IconId.Lock),
                MenuItem.Of(L.T("charm.restart"), () =>
                    { _shell.Taskbar.CloseStart(c); _shell.Restart(c); }, IconId.Power),
                MenuItem.Sep(),
                MenuItem.Of(L.T("charm.shutdown"), () =>
                    { _shell.Taskbar.CloseStart(c); _shell.ShowShutdownDialog(c); }, IconId.Shutdown),
            }, arrow.X - 150, arrow.Y - 116, this, c);
    }

    void DrawFooterButton(UiContext c, Rect r, string label, IconId icon)
    {
        var t = c.Theme;
        bool hot = c.Hovering(r);
        bool held = hot && c.In.IsDown(MouseButton.Left);

        if (t.Flat || t.Id == ThemeId.HighContrast)
        {
            c.R.FillRect(r, held ? t.Selection : hot ? t.Hot : t.FaceDark);
            c.R.DrawRect(r, t.ControlBorder);
        }
        else
            c.R.RoundedRectV(r, 3, held ? t.FaceDark : t.FaceLight,
                             held ? t.FaceLight : t.FaceDark, t.ControlBorder, 1);

        if (label == null) return;

        var ic = new Rect(r.X + 6, r.CenterY - 8, 16, 16);
        Icons.Draw(c.R, icon, ic);

        c.R.PushClip(r);
        c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(label, r.W - 30), ic.Right + 6,
                    r.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        c.R.PopClip();
    }

    // ---- input -----------------------------------------------------------------

    void HandleInput(UiContext c, Rect r)
    {
        // Clicking anywhere outside dismisses the menu.
        if ((c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right)) &&
            !r.Contains(c.MouseX, c.MouseY))
        {
            var bar = _shell.Taskbar.Bounds(c);
            var startBtn = _shell.Taskbar.Vertical
                ? new Rect(bar.X, bar.Y, bar.W, bar.W)
                : new Rect(bar.X, bar.Y, 100, bar.H);
            if (!startBtn.Contains(c.MouseX, c.MouseY)) _shell.Taskbar.CloseStart(c);
        }

        if (!Interactive) return;

        // Typing goes into the search box wherever the pointer happens to be,
        // which is the whole point of the search box being there.
        if (c.In.KeyPressed(Keys.Escape))
        {
            if (_query.Length > 0) _query = "";
            else _shell.Taskbar.CloseStart(c);
            c.KeyboardHandled = true;
        }
        else if (c.In.KeyPressed(Keys.Back) && _query.Length > 0)
        {
            _query = _query[..^1];
            c.KeyboardHandled = true;
        }
        else
        {
            foreach (char ch in c.In.TypedChars)
            {
                if (ch < ' ') continue;
                _query += ch;
                c.KeyboardHandled = true;
            }
        }

        c.MouseHandled = true;
    }

    void Activate(UiContext c, Entry e)
    {
        _shell.Taskbar.CloseStart(c);
        _query = "";

        // The one entry that is not a program: it opens the other Start.
        if (e.App == "$startscreen") { _shell.Start.Open(c); return; }

        _shell.Launch(c, e.App, e.Node);
    }

    void ShowAllPrograms(UiContext c, Rect anchor)
    {
        var accessories = new List<MenuItem>
        {
            MenuItem.Of(L.T("start.notepad"), () => Go(c, "notepad", null), IconId.Notepad),
            MenuItem.Of("Paint", () => Go(c, "paint", null), IconId.Paint),
            MenuItem.Of(L.T("start.calculator_plus"), () => Go(c, "calculator", null), IconId.Calculator),
            MenuItem.Of(L.T("calc8.title"), () => Go(c, "calculator8", null), IconId.Calculator),
            MenuItem.Of(L.T("start.command_prompt"), () => Go(c, "terminal", null), IconId.Terminal),
            MenuItem.Of(L.T("charmap.title"), () => Go(c, "charmap", null), IconId.TextFile),
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
            MenuItem.Of(L.T("taskbar.task_manager"), () => Go(c, "taskmgr", null), IconId.Settings),
            MenuItem.Of(L.T("devmgr.title"), () => Go(c, "devmgr", null), IconId.MyComputer),
            MenuItem.Of(L.T("regedit.title"), () => Go(c, "regedit", null), IconId.Registry),
            MenuItem.Of(L.T("defrag.title"), () => Go(c, "defrag", null), IconId.DriveHdd),
            MenuItem.Of(L.T("access.title"), () => Go(c, "access", null), IconId.Access),
            MenuItem.Of(L.T("power.title"), () => Go(c, "power", null), IconId.Power),
            MenuItem.Of(L.T("start.windows_update"), () => Go(c, "update", null), IconId.Shield),
            MenuItem.Of(L.T("whatsnew.title"), () => Go(c, "whatsnew", null), IconId.Star),
        };

        var modern = new List<MenuItem>
        {
            MenuItem.Of(L.T("pcs.title"), () => Go(c, "pcsettings", null), IconId.PcSettings),
            MenuItem.Of(L.T("store.title"), () => Go(c, "store", null), IconId.Store),
            MenuItem.Of(L.T("weather.title"), () => Go(c, "weather", null), IconId.Weather),
            MenuItem.Of(L.T("photos.title"), () => Go(c, "photos", null), IconId.MyPictures),
            MenuItem.Of(L.T("notes.title"), () => Go(c, "notes", null), IconId.TextFile),
        };

        // Anything registered that the groups above do not already name — a
        // program dropped into apps/ by somebody else, most of all — is listed
        // here, so a custom program is reachable without anyone editing a menu.
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "notepad", "paint", "calculator", "calculator8", "terminal", "charmap", "allinone",
            "voice", "mycomputer", "minesweeper", "taskmgr", "devmgr", "defrag", "access",
            "regedit",
            "power", "update", "whatsnew", "browser", "orega", "player", "spreadsheet",
            "explorer", "controlpanel", "sound", "language", "display", "personalise",
            "screenres", "displayprops", "screensaver", "effects", "appearance", "advanced",
            "taskbarprops", "pcsettings", "store", "weather", "photos", "notes",
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
            MenuItem.Sub(L.T("start.modern_programs"), modern, IconId.Tiles),
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

        _shell.Menus.Open(items, anchor.Right - 4, anchor.Y, this, c);
    }

    void Go(UiContext c, string app, VNode node)
    {
        _shell.Taskbar.CloseStart(c);
        _query = "";
        _shell.Launch(c, app, node);
    }
}
