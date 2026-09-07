using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Командная строка — a small shell over the virtual filesystem, with
/// enough commands to poke around and a few that exist purely for the joke.</summary>
public sealed class TerminalWindow : OsWindow
{
    readonly List<string> _lines = new();
    readonly List<string> _history = new();
    int _historyIndex = -1;
    string _input = "";
    VNode _cwd;
    float _scroll;
    double _caretBase;

    public override string Title => L.T("sys.command_prompt");
    public override float MinWidth => 420;
    public override float MinHeight => 240;

    public TerminalWindow()
    {
        Icon = IconId.Terminal;
        Bounds = new Rect(0, 0, 660, 400);
    }

    public override void OnOpened(UiContext c)
    {
        _cwd = Shell.Fs.Desktop;
        _lines.Add(L.T("sys.miminus_os_version_7_0_2010"));
        _lines.Add(L.T("sys.c_grevtsov_and_popov_all_rights_written_from"));
        _lines.Add("");
        _lines.Add(L.T("sys.type_help_for_a_list_of_commands"));
        _lines.Add("");
        c.Focus = Id + ".input";
    }

    string Prompt => _cwd.Path + ">";

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Color.Rgb(0x0C0C0C));

        var font = c.F.Mono;
        float lineH = font.Height + 1;
        var view = client.Deflate(6);

        // The prompt line lives at the end of the log.
        int totalLines = _lines.Count + 1;
        float contentH = totalLines * lineH;
        bool needScroll = contentH > view.H;
        if (needScroll) view.W -= W.ScrollBarSize;

        if (c.Hovering(client) && c.In.WheelDelta != 0)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * lineH * 3, 0, MathF.Max(0, contentH - view.H));
            c.MouseHandled = true;
        }
        else if (!needScroll) _scroll = 0;
        else _scroll = MathF.Max(0, contentH - view.H);   // stick to the bottom

        c.R.PushClip(view);
        float y = view.Y - _scroll;
        foreach (string line in _lines)
        {
            if (y + lineH >= view.Y && y <= view.Bottom)
                font.Draw(c.R, line, view.X, y, Color.Rgb(0xCCCCCC));
            y += lineH;
        }

        string prompt = Prompt + _input;
        font.Draw(c.R, prompt, view.X, y, Color.Rgb(0xCCCCCC));
        if (c.Focus == Id + ".input" && (c.Time - _caretBase) % 1.06 < 0.53)
            c.R.FillRect(new Rect(view.X + font.Measure(prompt), y, font.Measure("M"), lineH - 1),
                         Color.Rgba(0xCCCCCC, 190));
        c.R.PopClip();

        if (needScroll)
            W.ScrollBarV(c, Id + ".sb", new Rect(view.Right, client.Y + 2, W.ScrollBarSize, client.H - 4),
                         _scroll, contentH, view.H);

        if (c.Clicked(client)) c.Focus = Id + ".input";
        HandleInput(c);
    }

    void HandleInput(UiContext c)
    {
        if (c.KeyboardHandled || c.Focus != Id + ".input") return;

        foreach (char ch in c.In.TypedChars)
        {
            if (ch == '\r') { Execute(c); _caretBase = c.Time; }
            else if (ch >= ' ') { _input += ch; _caretBase = c.Time; }
            c.KeyboardHandled = true;
        }

        if (c.In.KeyPressed(Keys.Back) && _input.Length > 0)
        {
            _input = _input[..^1];
            c.KeyboardHandled = true;
        }
        else if (c.In.KeyPressed(Keys.Up) && _history.Count > 0)
        {
            _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Math.Max(0, _historyIndex - 1);
            _input = _history[_historyIndex];
            c.KeyboardHandled = true;
        }
        else if (c.In.KeyPressed(Keys.Down) && _history.Count > 0)
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1) _input = _history[++_historyIndex];
            else { _historyIndex = -1; _input = ""; }
            c.KeyboardHandled = true;
        }
        else if (c.In.KeyPressed(Keys.Escape)) { _input = ""; c.KeyboardHandled = true; }
    }

    void Echo(string s) => _lines.Add(s);

    void Execute(UiContext c)
    {
        Echo(Prompt + _input);
        string raw = _input.Trim();
        _input = "";
        _historyIndex = -1;
        if (raw.Length == 0) return;

        _history.Add(raw);

        string[] parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : "";

        switch (cmd)
        {
            case "help" or "?":
                Echo("");
                Echo(L.T("sys.dir_list_files"));
                Echo(L.T("sys.cd_name_change_folder_cd_up"));
                Echo(L.T("sys.type_f_print_a_text_file"));
                Echo(L.T("sys.start_f_open_a_file_or_program"));
                Echo(L.T("sys.echo_t_print_text"));
                Echo(L.T("sys.ver_system_version"));
                Echo(L.T("sys.time_current_time"));
                Echo(L.T("sys.color_change_the_visual_theme"));
                Echo(L.T("sys.scan_run_the_antivirus"));
                Echo(L.T("sys.bolgenos_check_for_bolgenos"));
                Echo(L.T("sys.cls_clear_the_screen"));
                Echo(L.T("sys.exit_close_this_window"));
                Echo("");
                break;

            case "dir":
            {
                Echo("");
                Echo(L.F("sys.directory_of_0", _cwd.Path));
                Echo("");
                foreach (var n in _cwd.Children)
                {
                    string kind = n.IsContainer ? "<DIR>        " : n.Size.ToString().PadLeft(12) + " ";
                    Echo($"{L.ShortDate(n.Modified)}  {n.Modified:HH:mm}    {kind} {n.Name}");
                }
                int files = _cwd.Children.Count(n => !n.IsContainer);
                int dirs = _cwd.Children.Count(n => n.IsContainer);
                Echo("");
                Echo(L.F("sys.0_file_s_1_dir_s", files, dirs));
                Echo("");
                break;
            }

            case "cd":
                if (arg.Length == 0) { Echo(_cwd.Path); break; }
                if (arg == "..")
                {
                    if (_cwd.Parent != null) _cwd = _cwd.Parent;
                    break;
                }
                var target = _cwd.Find(arg) ?? Shell.Fs.Resolve(arg);
                if (target is { IsContainer: true }) _cwd = target;
                else Echo(L.T("sys.the_system_cannot_find_the_path_specified"));
                break;

            case "type":
            {
                var node = _cwd.Find(arg);
                if (node is { Kind: NodeKind.TextFile })
                    foreach (string l in (node.Text ?? "").Replace("\r\n", "\n").Split('\n')) Echo(l);
                else
                    Echo(L.T("sys.the_system_cannot_find_the_file_specified"));
                break;
            }

            case "start":
            {
                var node = _cwd.Find(arg);
                if (node != null) Shell.Launch(c, node.IsContainer ? "explorer" : node.Launch, node);
                else Shell.Launch(c, arg.ToLowerInvariant(), null);
                break;
            }

            case "echo": Echo(arg); break;

            case "ver":
                Echo("");
                Echo(L.T("sys.miminus_os_version_7_0_2010"));
                Echo(L.T("sys.kernel_of_our_own_design_honest"));
                Echo("");
                break;

            case "time": Echo(Shell.Now.ToString("HH:mm:ss")); break;
            case "date": Echo(L.LongDate(Shell.Now)); break;

            case "color":
            {
                var next = Shell.ThemeId switch
                {
                    ThemeId.LunaBlue => ThemeId.LunaOlive,
                    ThemeId.LunaOlive => ThemeId.LunaSilver,
                    ThemeId.LunaSilver => ThemeId.Seven,
                    ThemeId.Seven => ThemeId.Classic,
                    _ => ThemeId.LunaBlue,
                };
                Shell.SetTheme(next, c.F);
                Echo(L.F("sys.theme_0", Shell.Theme.Name));
                break;
            }

            case "scan":
                Echo(L.T("sys.starting_grevtsov_antivirus"));
                Shell.Launch(c, "notepad", Shell.Fs.AntivirusFile);
                break;

            case "bolgenos":
                Echo("");
                Echo(L.T("sys.searching_for_bolgenos"));
                Echo(L.T("sys.bolgenos_not_found_bolgenos_has_been_defeate"));
                Echo("");
                c.Sound(Sfx.MineWin, 0.6f);
                break;

            case "cls": _lines.Clear(); break;
            case "exit": Close(); break;

            default:
                Echo(L.F("sys.0_is_not_recognized_as_an_internal_or_extern", parts[0]));
                Echo(L.T("sys.operable_program_or_batch_file"));
                c.Sound(Sfx.Error, 0.4f);
                break;
        }
    }
}

/// <summary>Диспетчер задач — lists the open windows as "processes", with a
/// live CPU graph fed by the real frame timings.</summary>
public sealed class TaskManagerWindow : OsWindow
{
    int _tab;
    int _selected = -1;
    readonly float[] _cpuHistory = new float[120];
    int _cpuIndex;
    double _sampleNext;
    float _cpu;

    public override string Title => L.T("sys.task_manager");
    public override float MinWidth => 420;
    public override float MinHeight => 320;

    public TaskManagerWindow()
    {
        Icon = IconId.Settings;
        Bounds = new Rect(0, 0, 520, 420);
    }

    public override void Tick(UiContext c, float dt)
    {
        if (c.Time < _sampleNext) return;
        _sampleNext = c.Time + 0.25;

        // "CPU load" is the honest frame cost, scaled so a 60 Hz frame reads low.
        float load = Math.Clamp(dt / 0.016f * 18f, 2, 100);
        _cpu = _cpu * 0.6f + load * 0.4f;
        _cpuHistory[_cpuIndex] = _cpu;
        _cpuIndex = (_cpuIndex + 1) % _cpuHistory.Length;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(8);

        string[] tabs =
        {
            L.T("sys.applications"),
            L.T("sys.processes"),
            L.T("sys.performance"),
        };
        _tab = W.Tabs(c, Id + ".tabs", area, tabs, _tab, out var body);
        body = body.Deflate(8);

        switch (_tab)
        {
            case 0: DrawApplications(c, body); break;
            case 1: DrawProcesses(c, body); break;
            default: DrawPerformance(c, body); break;
        }
    }

    void DrawApplications(UiContext c, Rect body)
    {
        var buttons = body.CutBottom(34);
        var list = body;

        W.SunkenField(c, list);
        var wins = Shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();

        var head = new Rect(list.X + 1, list.Y + 1, list.W - 2, 18);
        c.R.FillRectV(head, Color.White, c.Theme.Face);
        c.F.Ui.Draw(c.R, L.T("sys.task"), head.X + 24, head.Y + 2, c.Theme.Text);
        c.F.Ui.Draw(c.R, L.T("sys.status"), head.X + head.W * 0.7f, head.Y + 2, c.Theme.Text);
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), c.Theme.ControlBorder);

        c.R.PushClip(new Rect(list.X + 1, head.Bottom, list.W - 2, list.Bottom - head.Bottom - 1));
        for (int i = 0; i < wins.Count; i++)
        {
            var row = new Rect(list.X + 1, head.Bottom + i * 20, list.W - 2, 20);
            bool sel = i == _selected;
            if (sel) c.R.FillRect(row, c.Theme.Selection);
            else if (c.Hovering(row)) c.R.FillRect(row, c.Theme.Hot.WithAlpha((byte)70));

            Icons.Draw(c.R, wins[i].Icon, new Rect(row.X + 4, row.Y + 2, 16, 16));
            Color fg = sel ? c.Theme.SelectionText : c.Theme.Text;
            c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(wins[i].TaskbarTitle, row.W * 0.68f - 26), row.X + 24,
                        row.Y + (row.H - c.F.Ui.Height) * 0.5f, fg);
            c.F.Ui.Draw(c.R, L.T("sys.running"), row.X + row.W * 0.7f,
                        row.Y + (row.H - c.F.Ui.Height) * 0.5f, fg);

            if (c.Clicked(row)) _selected = i;
        }
        c.R.PopClip();

        float bw = 108, gap = 8;
        float x = buttons.Right - bw * 3 - gap * 2;
        bool has = _selected >= 0 && _selected < wins.Count;

        if (W.Button(c, Id + ".end", new Rect(x, buttons.Y + 6, bw, 24), L.T("sys.end_task"), has))
        {
            Shell.Wm.RequestClose(wins[_selected], c);
            _selected = -1;
        }
        if (W.Button(c, Id + ".switch", new Rect(x + bw + gap, buttons.Y + 6, bw, 24),
                     L.T("sys.switch_to"), has))
            Shell.Wm.RestoreOrFocus(wins[_selected], c);
        if (W.Button(c, Id + ".new", new Rect(x + (bw + gap) * 2, buttons.Y + 6, bw, 24),
                     L.T("sys.new_task")))
            Shell.Launch(c, "run", null);

        c.F.Ui.Draw(c.R, L.F("sys.processes_0_cpu_usage_1_f0", wins.Count + 7, _cpu),
                    body.X, buttons.Y + 12, c.Theme.TextDisabled);
    }

    void DrawProcesses(UiContext c, Rect body)
    {
        W.SunkenField(c, body);
        var head = new Rect(body.X + 1, body.Y + 1, body.W - 2, 18);
        c.R.FillRectV(head, Color.White, c.Theme.Face);

        string[] cols = { L.T("sys.image_name"), L.T("sys.user_name"), "ЦП", L.T("sys.mem_usage") };
        float[] widths = { 0.4f, 0.24f, 0.12f, 0.24f };
        float hx = head.X;
        for (int i = 0; i < cols.Length; i++)
        {
            c.F.Ui.Draw(c.R, cols[i], hx + 5, head.Y + 2, c.Theme.Text);
            hx += head.W * widths[i];
        }
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), c.Theme.ControlBorder);

        var procs = new List<(string name, string user, int cpu, int mem)>
        {
            ("miminus.exe", "Admin", (int)_cpu, 48_320),
            ("explorer.exe", "Admin", 1, 21_004),
            ("csrss.exe", "SYSTEM", 0, 4_112),
            ("winlogon.exe", "SYSTEM", 0, 3_880),
            ("services.exe", "SYSTEM", 0, 5_240),
            ("svchost.exe", "SYSTEM", 0, 12_760),
            ("bolgenos.exe", "—", 0, 0),
            ("System Idle Process", "SYSTEM", Math.Max(0, 100 - (int)_cpu), 28),
        };
        foreach (var w in Shell.Wm.Windows.Where(x => x.ShowInTaskbar))
            procs.Insert(1, (AppExe(w), "Admin", 0, 8_000 + w.Id.GetHashCode() % 9000));

        c.R.PushClip(new Rect(body.X + 1, head.Bottom, body.W - 2, body.Bottom - head.Bottom - 1));
        for (int i = 0; i < procs.Count; i++)
        {
            var row = new Rect(body.X + 1, head.Bottom + i * 18, body.W - 2, 18);
            if (row.Y > body.Bottom) break;
            if (c.Hovering(row)) c.R.FillRect(row, c.Theme.Hot.WithAlpha((byte)70));

            float x = row.X;
            var p = procs[i];
            string[] cells = { p.name, p.user, p.cpu.ToString("D2"), p.mem.ToString("N0") + " КБ" };
            for (int k = 0; k < cells.Length; k++)
            {
                c.R.PushClip(new Rect(x, row.Y, row.W * widths[k], row.H));
                c.F.Ui.Draw(c.R, cells[k], x + 5, row.Y + (row.H - c.F.Ui.Height) * 0.5f, c.Theme.Text);
                c.R.PopClip();
                x += row.W * widths[k];
            }
        }
        c.R.PopClip();
    }

    static string AppExe(OsWindow w) => w.ProcessName;

    void DrawPerformance(UiContext c, Rect body)
    {
        var top = body.CutTop(body.H * 0.5f);
        var gauge = top.CutLeft(140);

        W.GroupBox(c, gauge, L.T("sys.cpu_usage"));
        var bar = gauge.Deflate(28, 26, 28, 14);
        c.R.FillRect(bar, Color.Black);
        c.R.DrawRect(bar, c.Theme.ControlBorder);
        int blocks = (int)(bar.H / 6);
        int lit = (int)(blocks * _cpu / 100f);
        for (int i = 0; i < blocks; i++)
        {
            var b = new Rect(bar.X + 2, bar.Bottom - 2 - (i + 1) * 6, bar.W - 4, 4);
            c.R.FillRect(b, i < lit ? Color.Rgb(0x00E000) : Color.Rgb(0x004000));
        }
        c.F.UiBold.DrawCentered(c.R, $"{_cpu:F0} %", new Rect(gauge.X, gauge.Bottom - 4, gauge.W, 14), c.Theme.Text);

        W.GroupBox(c, top, L.T("sys.cpu_usage_history"));
        DrawGraph(c, top.Deflate(10, 22, 10, 10));

        var mem = body;
        W.GroupBox(c, mem, L.T("sys.memory"));
        var box = mem.Deflate(12, 24, 12, 12);
        c.F.Ui.Draw(c.R, L.T("sys.total_physical_memory_2_097_152_kb"), box.X, box.Y, c.Theme.Text);
        c.F.Ui.Draw(c.R, L.T("sys.available_1_402_880_kb"), box.X, box.Y + 20, c.Theme.Text);
        c.F.Ui.Draw(c.R, L.F("sys.windows_open_0", Shell.Wm.Windows.Count), box.X, box.Y + 40, c.Theme.Text);
        W.ProgressBar(c, new Rect(box.X, box.Y + 64, box.W, 16), 0.33f);
    }

    void DrawGraph(UiContext c, Rect r)
    {
        c.R.FillRect(r, Color.Black);
        c.R.DrawRect(r, c.Theme.ControlBorder);

        for (int i = 1; i < 10; i++)
        {
            float y = r.Y + r.H * i / 10f;
            c.R.FillRect(new Rect(r.X + 1, y, r.W - 2, 1), Color.Rgb(0x003000));
            float x = r.X + r.W * i / 10f;
            c.R.FillRect(new Rect(x, r.Y + 1, 1, r.H - 2), Color.Rgb(0x003000));
        }

        int n = _cpuHistory.Length;
        float step = (r.W - 2) / (n - 1);
        for (int i = 1; i < n; i++)
        {
            int a = (_cpuIndex + i - 1) % n;
            int b = (_cpuIndex + i) % n;
            float y0 = r.Bottom - 1 - (r.H - 2) * _cpuHistory[a] / 100f;
            float y1 = r.Bottom - 1 - (r.H - 2) * _cpuHistory[b] / 100f;
            c.R.Line(r.X + 1 + (i - 1) * step, y0, r.X + 1 + i * step, y1, Color.Rgb(0x00E000), 1.4f);
        }
    }
}

/// <summary>Панель управления — an icon board that opens the real applets.</summary>
public sealed class ControlPanelWindow : OsWindow
{
    public override string Title => L.T("sys.control_panel");
    public override float MinWidth => 480;
    public override float MinHeight => 340;

    public ControlPanelWindow()
    {
        Icon = IconId.ControlPanel;
        Bounds = new Rect(0, 0, 620, 440);
    }

    sealed record Applet(string Key, IconId Icon, string DescKey, Action<UiContext, ControlPanelWindow> Open);

    static readonly Applet[] Applets =
    {
        new("cpl.display", IconId.Display,
            "cpl.change_the_background_theme_and_resolution",
            (c, w) => w.Shell.Launch(c, "display", null)),
        new("cpl.sounds_and_audio_devices", IconId.Volume,
            "cpl.volume_and_sound_scheme",
            (c, w) => w.Wm.Open(new SoundPropertiesWindow(), c)),
        new("cpl.regional_and_language_options", IconId.Flag,
            "cpl.system_interface_language",
            (c, w) => w.Wm.Open(new LanguageOptionsWindow(), c)),
        new("cpl.system", IconId.MyComputer,
            "cpl.information_about_miminus",
            (c, w) => w.Shell.Launch(c, "about", null)),
        new("cpl.add_or_remove_programs", IconId.Program,
            "cpl.everything_is_already_installed",
            (c, w) => w.Shell.MessageBox(c, L.T("sys.add_or_remove_programs"),
                L.T("sys.nothing_to_remove_it_is_all_written_from_scr"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info)),
        new("cpl.network_connections", IconId.Network,
            "cpl.verify_your_connection_settings",
            (c, w) => w.Shell.Launch(c, "network", null)),
        new("cpl.printers_and_faxes", IconId.Printer,
            "cpl.no_printers_installed",
            (c, w) => w.Shell.Launch(c, "printers", null)),
        new("cpl.miminus_update", IconId.Shield,
            "cpl.check_the_repository_for_a_newer_version",
            (c, w) => w.Shell.Launch(c, "update", null)),
        new("cpl.security_center", IconId.Antivirus,
            "cpl.grevtsov_antivirus_2009",
            (c, w) => w.Shell.Launch(c, "notepad", w.Shell.Fs.AntivirusFile)),
        new("cpl.date_and_time", IconId.Clock,
            "cpl.the_system_clock",
            (c, w) => w.Shell.Launch(c, "clock", null)),
    };

    int _hover = -1;

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Color.White);

        var side = client.CutLeft(180);
        c.R.FillRectV(side, Color.Rgb(0x7CA7DE), Color.Rgb(0x6389C7));

        var head = new Rect(side.X + 6, side.Y + 8, side.W - 12, 22);
        c.R.RoundedRectV(head, 3, Color.Rgb(0xF0F4FB), Color.Rgb(0xC6D8F0));
        c.F.UiBold.Draw(c.R, L.T("sys.see_also"), head.X + 8,
                        head.CenterY - c.F.UiBold.Height * 0.5f, Color.Rgb(0x0A3070));

        float sy = head.Bottom + 8;
        foreach (var (key, icon, app) in new (string, IconId, string)[]
                 {
                     ("cpl.help_and_support", IconId.Help, "help"),
                     ("cpl.my_computer", IconId.MyComputer, "mycomputer"),
                     ("cpl.task_manager", IconId.Settings, "taskmgr"),
                 })
        {
            var row = new Rect(side.X + 12, sy, side.W - 20, 20);
            bool hov = c.Hovering(row);
            Icons.Draw(c.R, icon, new Rect(row.X, row.Y + 2, 14, 14));
            c.F.Small.Draw(c.R, L.T(key), row.X + 19, row.Y + 3, hov ? Color.Rgb(0xFFE8A8) : Color.White);
            if (c.Clicked(row)) Shell.Launch(c, app, null);
            sy += 22;
        }

        // Applet grid.
        var grid = client.Deflate(12);
        c.F.Big.Draw(c.R, L.T("sys.control_panel"), grid.X, grid.Y, Color.Rgb(0x1E4E8C));
        grid.CutTop(c.F.Big.Height + 10);

        float cellW = 150, cellH = 74;
        int cols = Math.Max(1, (int)(grid.W / cellW));
        _hover = -1;

        for (int i = 0; i < Applets.Length; i++)
        {
            var a = Applets[i];
            var cell = new Rect(grid.X + (i % cols) * cellW, grid.Y + (i / cols) * cellH, cellW - 6, cellH - 6);
            bool hov = c.Hovering(cell);
            if (hov) { _hover = i; c.R.RoundedRect(cell, 3, c.Theme.Hot.WithAlpha((byte)90)); }

            Icons.Draw(c.R, a.Icon, new Rect(cell.X + 6, cell.Y + 6, 32, 32));
            var lines = c.F.Ui.Wrap(L.T(a.Key), cell.W - 46);
            float ty = cell.Y + 6;
            foreach (string line in lines.Take(3))
            {
                c.F.Ui.Draw(c.R, line, cell.X + 44, ty, hov ? Color.Rgb(0x0A50C0) : c.Theme.Text);
                ty += c.F.Ui.Height + 1;
            }

            if (c.Clicked(cell)) { c.SoundAt(Sfx.Click, cell, 0.5f); a.Open(c, this); }
        }

        // Status strip echoing the hovered applet's description.
        var status = new Rect(client.X, client.Bottom - 20, client.W, 20);
        W.StatusBar(c, status, _hover >= 0
            ? L.T(Applets[_hover].DescKey)
            : L.F("sys.0_items", Applets.Length));
    }
}

/// <summary>Свойства: Звуки и аудиоустройства — master volume, mute, and a
/// button per system sound so the synthesised set can be auditioned.</summary>
public sealed class SoundPropertiesWindow : OsWindow
{
    int _tab;
    int _selected;

    static readonly (string key, Sfx sfx)[] Events =
    {
        ("sound.start_miminus_os", Sfx.Startup),
        ("sound.shut_down", Sfx.Shutdown),
        ("sound.logon", Sfx.Logon),
        ("sound.open_window", Sfx.WindowOpen),
        ("sound.close_window", Sfx.WindowClose),
        ("sound.minimize", Sfx.Minimize),
        ("sound.restore", Sfx.Restore),
        ("sound.menu_open", Sfx.MenuOpen),
        ("sound.critical_stop", Sfx.Error),
        ("sound.exclamation", Sfx.Warning),
        ("sound.asterisk", Sfx.Info),
        ("sound.question", Sfx.Question),
        ("sound.balloon", Sfx.Balloon),
        ("sound.empty_recycle_bin", Sfx.Trash),
        ("sound.explosion", Sfx.MineBoom),
        ("sound.fanfare", Sfx.MineWin),
    };

    public override string Title => L.T("sys.sounds_and_audio_devices_properties");
    public override float MinWidth => 400;
    public override float MinHeight => 380;

    public SoundPropertiesWindow()
    {
        Icon = IconId.Volume;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 420, 430);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(8);
        var buttons = area.CutBottom(34);

        string[] tabs = { L.T("sys.volume"), L.T("sys.sounds") };
        _tab = W.Tabs(c, Id + ".tabs", area, tabs, _tab, out var body);
        body = body.Deflate(14);

        if (_tab == 0)
        {
            W.GroupBox(c, new Rect(body.X, body.Y, body.W, 110), L.T("sys.device_volume"));
            var inner = new Rect(body.X + 16, body.Y + 30, body.W - 32, 70);

            float vol = Shell.Audio.MasterVolume;
            c.F.Ui.Draw(c.R, L.T("sys.low"), inner.X, inner.Y + 24, c.Theme.Text);
            c.F.Ui.DrawRight(c.R, L.T("sys.high"), new Rect(inner.X, inner.Y + 24, inner.W, 14), c.Theme.Text);
            if (W.Slider(c, Id + ".vol", new Rect(inner.X + 44, inner.Y, inner.W - 88, 24), ref vol, 0, 1))
                Shell.Audio.MasterVolume = vol;

            bool muted = Shell.Audio.Muted;
            if (W.CheckBox(c, Id + ".mute", new Rect(inner.X, inner.Y + 44, 200, 20),
                           L.T("sys.mute"), ref muted))
            {
                Shell.Audio.Muted = muted;
                Shell.Audio.MasterVolume = Shell.Audio.MasterVolume;
            }

            var info = new Rect(body.X, body.Y + 126, body.W, body.H - 126);
            W.GroupBox(c, info, L.T("sys.device"));
            c.F.Ui.Draw(c.R, Shell.Audio.Status, info.X + 14, info.Y + 30, c.Theme.Text);
            c.F.Small.Draw(c.R, L.T("sys.every_sound_is_synthesised_at_startup_not_on"),
                           info.X + 14, info.Y + 52, c.Theme.TextDisabled);
        }
        else
        {
            c.F.Ui.Draw(c.R, L.T("sys.program_events"), body.X, body.Y, c.Theme.Text);
            var list = new Rect(body.X, body.Y + 18, body.W, body.H - 70);
            var names = Events.Select(e => L.T(e.key)).ToList();
            if (W.ListBox(c, Id + ".events", list, names, ref _selected, out bool activated) && activated)
                Shell.Audio.Play(Events[_selected].sfx, 0.9f);

            if (W.Button(c, Id + ".play", new Rect(body.X, list.Bottom + 10, 110, 24),
                         L.T("sys.play"), true, IconId.MediaPlayer))
                Shell.Audio.Play(Events[Math.Clamp(_selected, 0, Events.Length - 1)].sfx, 0.9f);
        }

        float bw = 80, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("sys.ok"), true, IconId.None, true)) Close();
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("sys.cancel"))) Close();
    }
}

/// <summary>Язык и региональные стандарты — the UI language switch.</summary>
public sealed class LanguageOptionsWindow : OsWindow
{
    int _lang;

    public override string Title => L.T("sys.regional_and_language_options");
    public override float MinWidth => 380;
    public override float MinHeight => 260;

    public LanguageOptionsWindow()
    {
        Icon = IconId.Flag;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 400, 280);
        _lang = L.IsRu ? 0 : 1;
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(14);
        var buttons = area.CutBottom(34);

        W.GroupBox(c, new Rect(area.X, area.Y, area.W, 96), L.T("sys.interface_language"));
        var inner = new Rect(area.X + 16, area.Y + 28, area.W - 32, 60);

        if (W.Radio(c, Id + ".ru", new Rect(inner.X, inner.Y, inner.W, 22), "Русский", _lang == 0)) _lang = 0;
        if (W.Radio(c, Id + ".en", new Rect(inner.X, inner.Y + 26, inner.W, 22), "English", _lang == 1)) _lang = 1;

        var note = new Rect(area.X, area.Y + 112, area.W, area.H - 112);
        c.F.Ui.Draw(c.R, L.T("sys.tip"), note.X, note.Y, c.Theme.Text);
        foreach (string line in c.F.Ui.Wrap(
            L.T("sys.you_can_also_switch_the_language_by_clicking"),
            note.W))
        {
            note.CutTop(c.F.Ui.Height + 2);
            c.F.Ui.Draw(c.R, line, note.X, note.Y - c.F.Ui.Height - 2 + 18, c.Theme.TextDisabled);
        }

        float bw = 80, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 4, bw, 24), L.T("sys.ok"), true, IconId.None, true))
        {
            L.Current = _lang == 0 ? Lang.Ru : Lang.En;
            c.Sound(Sfx.Click, 0.6f);
            Close();
        }
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 4, bw, 24), L.T("sys.cancel")))
            Close();
    }
}
