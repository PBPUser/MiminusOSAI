using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>The Properties sheet: General / Security tabs over a filesystem node.</summary>
public sealed class PropertiesWindow : OsWindow
{
    readonly string _label;
    readonly VNode _node;
    readonly IconId _icon;
    int _tab;
    bool _readOnly, _hidden, _archive = true;

    public override string Title => L.F("dlg.0_properties", _label);
    public override float MinWidth => 340;
    public override float MinHeight => 360;

    public PropertiesWindow(string label, VNode node, IconId icon)
    {
        _label = label;
        _node = node;
        _icon = icon;
        Icon = icon;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 360, 430);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(8);
        var buttons = area.CutBottom(34);

        string[] tabs = { L.T("dlg.general"), L.T("dlg.security"), L.T("dlg.summary") };
        _tab = W.Tabs(c, Id + ".tabs", area, tabs, _tab, out var body);
        body = body.Deflate(12);

        switch (_tab)
        {
            case 0: DrawGeneral(c, body); break;
            case 1: DrawSecurity(c, body); break;
            default: DrawSummary(c, body); break;
        }

        float bw = 80, gap = 8;
        float x = buttons.Right - bw * 3 - gap * 2;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("dlg.ok"), true, IconId.None, true)) Close();
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("dlg.cancel"))) Close();
        W.Button(c, Id + ".apply", new Rect(x + (bw + gap) * 2, buttons.Y + 4, bw, 24), L.T("dlg.apply"), false);
    }

    void DrawGeneral(UiContext c, Rect body)
    {
        var head = body.CutTop(44);
        Icons.Draw(c.R, _icon, new Rect(head.X, head.Y, 32, 32));
        c.F.UiBold.Draw(c.R, c.F.UiBold.Ellipsize(_label, head.W - 44), head.X + 42, head.Y + 8, c.Theme.Text);
        c.R.FillRect(new Rect(body.X, head.Bottom, body.W, 1), Color.Rgb(0xC0C0C0));
        body.CutTop(8);

        void Row(string key, string value)
        {
            var r = body.CutTop(20);
            c.F.Ui.Draw(c.R, L.T(key), r.X, r.Y + 2, c.Theme.Text);
            c.R.PushClip(new Rect(r.X + 110, r.Y, r.W - 110, r.H));
            c.F.Ui.Draw(c.R, value, r.X + 110, r.Y + 2, c.Theme.Text);
            c.R.PopClip();
        }

        Row("props.type_of_file", _node?.TypeName ?? L.T("dlg.shortcut"));
        Row("props.opens_with", AppName(_node?.Launch));
        body.CutTop(6);
        c.R.FillRect(new Rect(body.X, body.Y, body.W, 1), Color.Rgb(0xC0C0C0));
        body.CutTop(8);

        Row("props.location", _node?.Parent?.Path ?? @"C:\Documents and Settings\Admin\Рабочий стол");
        Row("props.size", _node == null ? "—" : L.FileSize(_node.Size));
        Row("props.size_on_disk", _node == null ? "—" : L.FileSize(((_node.Size + 4095) / 4096) * 4096));
        body.CutTop(6);
        c.R.FillRect(new Rect(body.X, body.Y, body.W, 1), Color.Rgb(0xC0C0C0));
        body.CutTop(8);

        Row("props.created", L.LongDate(_node?.Modified ?? DateTime.Now));
        Row("props.modified", $"{L.ShortDate(_node?.Modified ?? DateTime.Now)} {(_node?.Modified ?? DateTime.Now):HH:mm:ss}");
        body.CutTop(10);
        c.R.FillRect(new Rect(body.X, body.Y, body.W, 1), Color.Rgb(0xC0C0C0));
        body.CutTop(8);

        var attrRow = body.CutTop(22);
        c.F.Ui.Draw(c.R, L.T("dlg.attributes"), attrRow.X, attrRow.Y + 3, c.Theme.Text);
        W.CheckBox(c, Id + ".ro", new Rect(attrRow.X + 110, attrRow.Y, 130, 20),
                   L.T("dlg.read_only"), ref _readOnly);
        var attrRow2 = body.CutTop(22);
        W.CheckBox(c, Id + ".hidden", new Rect(attrRow2.X + 110, attrRow2.Y, 130, 20),
                   L.T("dlg.hidden"), ref _hidden);
        var attrRow3 = body.CutTop(22);
        W.CheckBox(c, Id + ".arch", new Rect(attrRow3.X + 110, attrRow3.Y, 130, 20),
                   L.T("dlg.archive"), ref _archive);
    }

    static string AppName(string launch) => launch switch
    {
        "notepad" => L.T("dlg.notepad"),
        "paint" => "Paint",
        "player" => L.T("dlg.miminus_media_player"),
        "spreadsheet" => L.T("dlg.miminus_sheet"),
        "minesweeper" => L.T("dlg.minesweeper"),
        "browser" => "Firefox",
        _ => L.T("dlg.unknown"),
    };

    void DrawSecurity(UiContext c, Rect body)
    {
        c.F.Ui.Draw(c.R, L.T("dlg.group_or_user_names"), body.X, body.Y, c.Theme.Text);
        var list = new Rect(body.X, body.Y + 18, body.W, 90);
        int sel = 0;
        W.ListBox(c, Id + ".users", list, new[] { "Admin (МИМИНУС\\Admin)", "SYSTEM", L.T("dlg.everyone") },
                  ref sel, out _);

        c.F.Ui.Draw(c.R, L.T("dlg.permissions"), body.X, list.Bottom + 10, c.Theme.Text);
        var perms = new Rect(body.X, list.Bottom + 28, body.W, 120);
        c.R.FillRect(perms, c.Theme.FieldBack);
        c.R.DrawRect(perms, c.Theme.FieldBorder);

        string[] rights =
        {
            L.T("dlg.full_control"),
            L.T("dlg.modify"),
            L.T("dlg.read_and_execute"),
            L.T("dlg.read"),
            L.T("dlg.write"),
        };
        float y = perms.Y + 4;
        foreach (string right in rights)
        {
            c.F.Ui.Draw(c.R, right, perms.X + 6, y, c.Theme.Text);
            var box = new Rect(perms.Right - 40, y, 12, 12);
            c.R.FillRect(box, c.Theme.FieldBack);
            c.R.DrawRect(box, c.Theme.FieldBorder);
            c.R.Line(box.X + 2.5f, box.CenterY, box.X + 5, box.Bottom - 3, c.Theme.Text, 1.8f);
            c.R.Line(box.X + 5, box.Bottom - 3, box.Right - 2, box.Y + 2.5f, c.Theme.Text, 1.8f);
            y += 21;
        }

        c.F.Small.Draw(c.R, L.T("dlg.everything_is_allowed_this_is_miminus_os"),
                       body.X, perms.Bottom + 8, c.Theme.TextDisabled);
    }

    void DrawSummary(UiContext c, Rect body)
    {
        string[] labels =
        {
            L.T("dlg.title"), L.T("dlg.subject"),
            L.T("dlg.author"), L.T("dlg.category"),
            L.T("dlg.keywords"), L.T("dlg.comments"),
        };
        string[] values =
        {
            _label, L.T("dlg.revolutionary_distributions"),
            "Гревцов", L.T("dlg.in_house_development"),
            L.T("dlg.miminus_os_from_scratch"),
            L.T("dlg.written_from_scratch"),
        };
        for (int i = 0; i < labels.Length; i++)
        {
            var row = body.CutTop(26);
            c.F.Ui.Draw(c.R, labels[i], row.X, row.Y + 4, c.Theme.Text);
            var field = new Rect(row.X + 110, row.Y, row.W - 110, 20);
            W.SunkenField(c, field);
            c.R.PushClip(field.Deflate(2));
            c.F.Ui.Draw(c.R, values[i], field.X + 4, field.Y + 3, c.Theme.Text);
            c.R.PopClip();
        }
    }
}

/// <summary>«Выбор программы» — the Open With chooser from part 3.</summary>
public sealed class OpenWithWindow : OsWindow
{
    readonly VNode _node;
    int _selected;
    bool _always;

    static readonly (string key, IconId icon, string app)[] Programs =
    {
        ("openwith.notepad", IconId.Notepad, "notepad"),
        ("openwith.paint", IconId.Paint, "paint"),
        ("openwith.miminus_media_player", IconId.MediaPlayer, "player"),
        ("openwith.miminus_sheet", IconId.Spreadsheet, "spreadsheet"),
        ("openwith.firefox_web_browser", IconId.Firefox, "browser"),
        ("openwith.opera_internet_browser", IconId.Opera, null),
        ("openwith.nero_photosnap_viewer", IconId.ImageFile, null),
        ("openwith.microsoft_office_picture_manager", IconId.ImageFile, null),
        ("openwith.adobe_reader_9", IconId.WordDoc, null),
        ("openwith.windows_media_player", IconId.MediaPlayer, null),
    };

    public override string Title => L.T("dlg.open_with");
    public override float MinWidth => 400;
    public override float MinHeight => 380;

    public OpenWithWindow(VNode node)
    {
        _node = node;
        Icon = IconId.Program;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Modal = true;
        Bounds = new Rect(0, 0, 420, 420);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(12);

        var head = area.CutTop(50);
        Icons.Draw(c.R, _node?.Icon ?? IconId.UnknownFile, new Rect(head.X, head.Y, 32, 32));
        var lines = c.F.Ui.Wrap(
            L.F("dlg.choose_the_program_you_want_to_use_to_open_0", _node?.Name ?? "?"),
            head.W - 42);
        float ty = head.Y;
        foreach (string line in lines) { c.F.Ui.Draw(c.R, line, head.X + 42, ty, c.Theme.Text); ty += c.F.Ui.Height + 2; }

        area.CutTop(4);
        c.F.Ui.Draw(c.R, L.T("dlg.programs"), area.X, area.Y, c.Theme.Text);
        area.CutTop(18);

        var buttons = area.CutBottom(34);
        var check = area.CutBottom(28);

        // Icon list, drawn by hand so each row can carry its program icon.
        var list = area;
        W.SunkenField(c, list);
        c.R.PushClip(list.Deflate(1));
        float rowH = 20;
        for (int i = 0; i < Programs.Length; i++)
        {
            var row = new Rect(list.X + 1, list.Y + 1 + i * rowH, list.W - 2, rowH);
            bool sel = i == _selected;
            if (sel) c.R.FillRect(row, c.Theme.Selection);
            else if (c.Hovering(row)) c.R.FillRect(row, c.Theme.Hot.WithAlpha((byte)70));

            Icons.Draw(c.R, Programs[i].icon, new Rect(row.X + 3, row.Y + 2, 16, 16));
            c.F.Ui.Draw(c.R, L.T(Programs[i].key), row.X + 24,
                        row.Y + (rowH - c.F.Ui.Height) * 0.5f,
                        sel ? c.Theme.SelectionText : c.Theme.Text);

            if (c.DoubleClicked(row)) { _selected = i; Accept(c); }
            else if (c.Clicked(row)) { _selected = i; c.SoundAt(Sfx.Tick, row, 0.3f); }
        }
        c.R.PopClip();

        W.CheckBox(c, Id + ".always", new Rect(check.X, check.Y + 4, check.W, 20),
                   L.T("dlg.always_use_the_selected_program_to_open_this"), ref _always);

        float bw = 86, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("dlg.ok"), true, IconId.None, true))
            Accept(c);
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("dlg.cancel")))
            Close();
    }

    void Accept(UiContext c)
    {
        var prog = Programs[_selected];
        string progName = L.T(prog.key);
        Close();
        if (prog.app != null) Shell.Launch(c, prog.app, _node);
        else
            Shell.MessageBox(c, L.T("dlg.error"),
                L.F("dlg.could_not_start_0_miminus_os_does_not_need_i", progName),
                MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
    }
}

/// <summary>A minimal Open dialog: browse the virtual tree, pick a file.</summary>
public sealed class FilePickerWindow : OsWindow
{
    readonly VirtualFS _fs;
    readonly string _title;
    readonly Action<VNode> _onPick;
    VNode _folder;
    int _selected = -1;

    public override string Title => _title;
    public override float MinWidth => 420;
    public override float MinHeight => 320;

    public FilePickerWindow(VirtualFS fs, string title, Action<VNode> onPick)
    {
        _fs = fs;
        _title = title;
        _onPick = onPick;
        _folder = fs.Desktop;
        Icon = IconId.Folder;
        Modal = true;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 460, 340);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(10);

        var top = area.CutTop(24);
        c.F.Ui.Draw(c.R, L.T("dlg.look_in"), top.X, top.Y + 4, c.Theme.Text);
        var pathField = new Rect(top.X + 54, top.Y, top.W - 54 - 60, 20);
        W.SunkenField(c, pathField);
        c.R.PushClip(pathField.Deflate(2));
        c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(_folder.Path, pathField.W - 6), pathField.X + 4, pathField.Y + 3, c.Theme.Text);
        c.R.PopClip();
        if (W.Button(c, Id + ".up", new Rect(top.Right - 54, top.Y, 54, 20), L.T("dlg.up"), _folder.Parent != null))
        {
            _folder = _folder.Parent;
            _selected = -1;
        }

        area.CutTop(8);
        var buttons = area.CutBottom(32);

        var names = _folder.Children.Select(n => (n.IsContainer ? "[" + n.Name + "]" : n.Name)).ToList();
        if (W.ListBox(c, Id + ".files", area, names, ref _selected, out bool activated) && activated)
            Activate(c);

        float bw = 86, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("dlg.open"),
                     _selected >= 0, IconId.None, true))
            Activate(c);
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("dlg.cancel")))
            Close();
    }

    void Activate(UiContext c)
    {
        if (_selected < 0 || _selected >= _folder.Children.Count) return;
        var node = _folder.Children[_selected];
        if (node.IsContainer)
        {
            _folder = node;
            _selected = -1;
            c.Sound(Sfx.Navigate, 0.4f);
            return;
        }
        Close();
        _onPick?.Invoke(node);
    }
}

/// <summary>«Выполнить» — the Run box, wired to the same launcher the Start menu uses.</summary>
public sealed class RunWindow : OsWindow
{
    string _command = "";

    public override string Title => L.T("dlg.run");
    public override float MinWidth => 380;
    public override float MinHeight => 160;

    public RunWindow()
    {
        Icon = IconId.Run;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 400, 180);
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        c.Focus = Id + ".cmd";
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(12);

        var head = area.CutTop(50);
        Icons.Draw(c.R, IconId.Run, new Rect(head.X, head.Y, 32, 32));
        var lines = c.F.Ui.Wrap(
            L.T("dlg.type_the_name_of_a_program_folder_or_documen"),
            head.W - 44);
        float ty = head.Y;
        foreach (string line in lines) { c.F.Ui.Draw(c.R, line, head.X + 44, ty, c.Theme.Text); ty += c.F.Ui.Height + 2; }

        var row = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("dlg.open_2"), row.X, row.Y + 5, c.Theme.Text);
        W.TextField(c, Id + ".cmd", new Rect(row.X + 62, row.Y, row.W - 62, 22), ref _command);

        var buttons = area.CutBottom(32);
        float bw = 82, gap = 8;
        float x = buttons.Right - bw * 3 - gap * 2;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("dlg.ok"), true, IconId.None, true))
            RunCommand(c);
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("dlg.cancel")))
            Close();
        if (W.Button(c, Id + ".browse", new Rect(x + (bw + gap) * 2, buttons.Y + 4, bw, 24), L.T("dlg.browse")))
            Wm.Open(new FilePickerWindow(Shell.Fs, L.T("dlg.browse_2"), n => _command = n.Name), c);

        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Enter)) RunCommand(c);
        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape)) Close();
    }

    void RunCommand(UiContext c)
    {
        string cmd = _command.Trim().ToLowerInvariant();
        string app = cmd switch
        {
            "notepad" or "блокнот" or "notepad.exe" => "notepad",
            "calc" or "calc.exe" or "калькулятор" => "calculator",
            "mspaint" or "paint" => "paint",
            "winmine" or "сапер" or "minesweeper" => "minesweeper",
            "explorer" or "проводник" => "mycomputer",
            "cmd" or "cmd.exe" or "command" => "terminal",
            "taskmgr" => "taskmgr",
            "firefox" or "iexplore" or "opera" => "browser",
            "excel" or "таблица" => "spreadsheet",
            "control" => "controlpanel",
            "regedit" or "regedit.exe" or "реестр" => "regedit",
            "devmgmt.msc" or "devmgmt" => "devmgr",
            "powercfg.cpl" or "powercfg" => "power",
            "utilman" or "access.cpl" => "access",
            "dfrg.msc" or "defrag" => "defrag",
            "charmap" => "charmap",
            "osk" => "access",
            "desk.cpl" or "экран" => "display",
            "bolgenos" => null,
            _ => null,
        };

        Close();

        if (app != null) { Shell.Launch(c, app, null); return; }

        if (cmd == "bolgenos")
        {
            Shell.MessageBox(c, L.T("dlg.error"),
                L.T("dlg.bolgenos_has_been_defeated_it_no_longer_runs"),
                MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
            return;
        }

        Shell.MessageBox(c, L.T("dlg.run"),
            L.F("dlg.cannot_find_0_make_sure_you_typed_the_name_c", _command),
            MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
    }
}

/// <summary>«Выключить компьютер» — the three-button XP shutdown dialog.</summary>
public sealed class ShutdownWindow : OsWindow
{
    public override string Title => L.T("dlg.turn_off_computer");
    public override float MinWidth => 420;
    public override float MinHeight => 200;

    public ShutdownWindow()
    {
        Icon = IconId.Shutdown;
        Modal = true;
        // Turning the machine off is the machine's business, not one program's.
        SystemModal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 440, 210);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRectV(client, Color.Rgb(0x4A8BE0), Color.Rgb(0x1E5FD8));

        string head = L.T("dlg.turn_off_computer");
        c.F.Big.Draw(c.R, head, client.X + 24, client.Y + 16, Color.Rgba(0x0A2E5A, 160));
        c.F.Big.Draw(c.R, head, client.X + 23, client.Y + 15, Color.White);

        var row = new Rect(client.X, client.Y + 74, client.W, 84);
        float bw = 110;
        float gap = (row.W - bw * 3) / 4;
        float x = row.X + gap;

        if (BigButton(c, ".standby", new Rect(x, row.Y, bw, row.H), IconId.Clock,
                      L.T("dlg.stand_by")))
        {
            Close();
            Shell.MessageBox(c, L.T("dlg.stand_by"),
                L.T("dlg.miminus_os_does_not_sleep_it_works"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
        }
        x += bw + gap;

        if (BigButton(c, ".off", new Rect(x, row.Y, bw, row.H), IconId.Shutdown,
                      L.T("dlg.turn_off")))
        {
            Close();
            Shell.BeginShutdown(c);
        }
        x += bw + gap;

        if (BigButton(c, ".restart", new Rect(x, row.Y, bw, row.H), IconId.Settings,
                      L.T("dlg.restart")))
        {
            Close();
            Shell.BeginShutdown(c);
        }

        var cancel = new Rect(client.Right - 90, client.Bottom - 30, 80, 22);
        if (W.Button(c, Id + ".cancel", cancel, L.T("dlg.cancel"))) Close();

        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape)) Close();
    }

    bool BigButton(UiContext c, string id, Rect r, IconId icon, string label)
    {
        bool hover = c.Hovering(r);
        var circle = new Rect(r.X + (r.W - 48) * 0.5f, r.Y, 48, 48);

        if (hover) c.R.FillCircle(circle.CenterX, circle.CenterY, 28, Color.Rgba(0xFFFFFF, 45));
        c.R.FillCircle(circle.CenterX, circle.CenterY, 24, Color.Rgba(0xFFFFFF, 25));
        Icons.Draw(c.R, icon, circle);

        float lw = c.F.Ui.Measure(label);
        c.F.Ui.Draw(c.R, label, r.CenterX - lw * 0.5f, circle.Bottom + 8, Color.White);

        return c.Clicked(r);
    }
}

/// <summary>The About box: the parody spec sheet for МИМИНУС ОС.</summary>
public sealed class AboutWindow : OsWindow
{
    public override string Title => L.T("dlg.about_miminus");
    public override float MinWidth => 420;
    public override float MinHeight => 330;

    public AboutWindow()
    {
        Icon = IconId.DlgInfo;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 560, 580);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);

        var banner = client.CutTop(78);
        c.R.FillRectV(banner, Color.Rgb(0xFFD200), Color.Rgb(0xE8B800));
        c.F.Big.Draw(c.R, L.T("dlg.miminus_os"), banner.X + 16, banner.Y + 12, Color.Black);
        c.F.Ui.Draw(c.R, L.T("dlg.copyright_popov"), banner.X + 18,
                    banner.Y + 16 + c.F.Big.Height, Color.Rgb(0x604800));
        c.R.FillRect(new Rect(banner.X, banner.Bottom - 1, banner.W, 1), Color.Rgb(0xA08000));

        var body = client.Deflate(16);

        string[] rows =
        {
            "about.version",
            "about.kernel",
            "about.graphics",
            "about.audio",
            "about.language",
            "about.author",
            "about.distributed_as",
            "about.cursor",
            "about.acronym",
            "about.price",
            "about.deployment",
            "about.motto",
        };
        string[] values =
        {
            L.T("dlg.7_0_build_our_answer_to_bolgenos"),
            L.T("dlg.of_our_own_design"),
            $"OpenGL {SystemInfo.GlVersion}",
            Shell.Audio.Status,
            L.T("dlg.russian_english"),
            L.T("about.author_value"),
            L.T("about.windows_hp"),
            L.T("about.cursor_value"),
            L.T("about.acronym_value"),
            L.T("about.price_value"),
            L.T("about.deployment_value"),
            L.T("about.motto_value"),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            // The distribution line carries a newline; the rest are single-line.
            string[] lines = values[i].Split('\n');
            var row = body.CutTop(22 + (lines.Length - 1) * 14);
            c.F.UiBold.Draw(c.R, L.T(rows[i]), row.X, row.Y + 3, c.Theme.Text);
            c.R.PushClip(new Rect(row.X + 110, row.Y, row.W - 110, row.H));
            float vy = row.Y + 3;
            foreach (string line in lines)
            {
                c.F.Ui.Draw(c.R, line, row.X + 110, vy, c.Theme.Text);
                vy += 14;
            }
            c.R.PopClip();
        }

        body.CutTop(8);
        c.R.FillRect(new Rect(body.X, body.Y, body.W, 1), Color.Rgb(0xC0C0C0));
        body.CutTop(10);

        string blurb = L.T("dlg.everything_here_is_written_from_scratch_wind")
                     + " " + L.T("about.tested_by")
                     + " " + L.T("about.licensed_software");
        foreach (string line in c.F.Ui.Wrap(blurb, body.W))
        {
            c.F.Ui.Draw(c.R, line, body.X, body.Y, c.Theme.Text);
            body.CutTop(c.F.Ui.Height + 2);
        }

        var ok = new Rect(client.Right - 96, client.Bottom - 34, 84, 24);
        if (W.Button(c, Id + ".ok", ok, L.T("dlg.ok"), true, IconId.None, true)) Close();
    }
}

/// <summary>A command-link task dialog — the shape Paint.NET uses for its
/// "unsaved changes" prompt in part 2, with a thumbnail of the image and one
/// stacked choice per action rather than a row of buttons.</summary>
public sealed class TaskDialogWindow : OsWindow
{
    public sealed record Choice(string TitleKey, string DetailKey, IconId Icon, Action Run);

    readonly string _titleKey;
    readonly string _messageKey;
    readonly object _messageArg;
    readonly IconId _badge;
    readonly Choice[] _choices;
    readonly Bitmap _thumbnail;
    readonly Audio.Sfx _sound;
    bool _played;

    public override string Title => L.T(_titleKey);
    public override float MinWidth => 320;
    public override float MinHeight => 160;

    public TaskDialogWindow(string titleKey, string messageKey, object messageArg,
                            IconId badge, Bitmap thumbnail, Audio.Sfx sound, params Choice[] choices)
    {
        _titleKey = titleKey;
        _messageKey = messageKey;
        _messageArg = messageArg;
        _badge = badge;
        _thumbnail = thumbnail;
        _choices = choices;
        _sound = sound;

        Icon = badge;
        Modal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 400, 240);
    }

    public override void OnOpened(UiContext c)
    {
        float textW = 330 - (_thumbnail != null ? 74 : 0);
        int lines = c.F.Ui.Wrap(Message(), textW).Count;
        Bounds.W = 400;
        Bounds.H = c.Theme.CaptionHeight + 30 + lines * (c.F.Ui.Height + 3)
                 + _choices.Length * 44 + 26;
        if (_thumbnail != null) Bounds.H = MathF.Max(Bounds.H, c.Theme.CaptionHeight + 210);
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
    }

    string Message() => _messageArg == null ? L.T(_messageKey) : L.F(_messageKey, _messageArg);

    public override void DrawClient(UiContext c, Rect client)
    {
        if (!_played) { _played = true; c.Sound(_sound, 0.85f); }

        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(12, 10, 12, 10);

        // Header: optional thumbnail of the document, then the question.
        var head = area.CutTop(_thumbnail != null ? 70 : 44);
        float tx = head.X;
        if (_thumbnail != null)
        {
            var thumb = new Rect(head.X, head.Y, 64, 52);
            c.R.FillRect(thumb.Inflate(1), Color.Rgb(0x808080));
            c.R.DrawTexture(_thumbnail.Texture, thumb);
            tx = thumb.Right + 10;
        }

        float ty = head.Y;
        foreach (string line in c.F.Ui.Wrap(Message(), head.Right - tx))
        {
            c.F.Ui.Draw(c.R, line, tx, ty, c.Theme.Text);
            ty += c.F.Ui.Height + 3;
        }

        // Command links.
        foreach (var choice in _choices)
        {
            var row = area.CutTop(44);
            bool hover = c.Hovering(row);
            if (hover)
            {
                c.R.FillRect(row, Color.Rgba(0xD8E8F8, 190));
                c.R.DrawRect(row, Color.Rgb(0x9CC0E8));
            }

            Icons.Draw(c.R, choice.Icon, new Rect(row.X + 6, row.Y + 6, 18, 18));
            c.F.Ui.Draw(c.R, L.T(choice.TitleKey), row.X + 32, row.Y + 6,
                        hover ? Color.Rgb(0x11407A) : c.Theme.Text);
            c.F.Small.Draw(c.R, L.T(choice.DetailKey), row.X + 32, row.Y + 8 + c.F.Ui.Height,
                           c.Theme.TextDisabled);

            if (c.Clicked(row))
            {
                Close();
                c.Sound(Audio.Sfx.Click, 0.5f);
                choice.Run?.Invoke();
                return;
            }
        }

        if (!c.KeyboardHandled && c.In.KeyPressed(Platform.Keys.Escape)) Close();
    }
}
