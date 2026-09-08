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

            // Which program assemblies are resident. The point of the command
            // is that the answer changes as programs are opened and closed.
            case "apps":
            {
                Echo("");
                foreach (var (name, loaded, windows) in Shell.Programs.Assemblies().OrderBy(a => a.name))
                    Echo($"  {name,-28} {(loaded ? L.T("sys.apps_loaded") : L.T("sys.apps_unloaded")),-12} {windows}");
                Echo("");
                Echo(L.F("sys.apps_summary", Shell.Programs.LoadedAssemblies, Shell.Programs.Count));

                if (arg.Trim().Equals("free", StringComparison.OrdinalIgnoreCase))
                {
                    Echo(L.F("sys.apps_unloaded_count", Shell.Programs.UnloadIdle()));
                    // Unloading is a request; this says whether the runtime
                    // actually finished it.
                    Echo(L.T(Shell.Programs.AllDroppedCollected
                        ? "sys.apps_collected" : "sys.apps_still_resident"));
                }

                Echo("");
                break;
            }

            // Throws on purpose, so the stop screen can be seen without
            // waiting for a real fault.
            case "crash":
                Echo("");
                Echo(L.T("sys.crash_warning"));
                throw new InvalidOperationException(L.T("sys.crash_message"));

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
                    ThemeId.Seven => ThemeId.Metro,
                    ThemeId.Metro => ThemeId.Classic,
                    ThemeId.Classic => ThemeId.HighContrast,
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
