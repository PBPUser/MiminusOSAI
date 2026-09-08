using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;
using System.Reflection;

namespace Miminus.Apps;

/// <summary>Диспетчер задач, as version 8 rebuilt it.
///
/// It opens the way the original did: a bare white list of what is running and
/// one button, with everything else behind «Подробнее». Open that and the rest
/// arrives — a process table with the numbers washed in colour so the heavy row
/// is the one you see first, a performance page of graphs, and a startup list
/// that measures how much each entry costs at boot.
///
/// The numbers are not invented every frame: each process holds a steady figure
/// derived from its own name, drifting slightly, so the table reads like a
/// machine at rest rather than a slot machine. The one honest number is the CPU
/// line, which is the real cost of drawing this window.</summary>
public sealed class TaskManagerWindow : OsWindow
{
    /// <summary>False until «Подробнее» is used: version 8 opened on a list of
    /// programs and one button, and hid the rest of the program behind a link.</summary>
    bool _details;

    int _tab;
    int _selected = -1;

    readonly float[] _cpuHistory = new float[120];
    readonly float[] _memHistory = new float[120];
    readonly float[] _diskHistory = new float[120];
    readonly float[] _netHistory = new float[120];
    int _historyIndex;

    double _sampleNext;
    float _cpu, _disk, _net;

    /// <summary>Which card on the performance page is being shown large.</summary>
    int _resource;

    public override string Title => L.T("sys.task_manager");
    public override float MinWidth => 460;
    public override float MinHeight => 340;

    public TaskManagerWindow()
    {
        Icon = IconId.Settings;
        Bounds = new Rect(0, 0, 480, 360);
    }

    public override void Tick(UiContext c, float dt)
    {
        if (c.Time < _sampleNext) return;
        _sampleNext = c.Time + 0.25;

        // "CPU load" is the honest frame cost, scaled so a 60 Hz frame reads low.
        float load = Math.Clamp(dt / 0.016f * 18f, 2, 100);
        _cpu = _cpu * 0.6f + load * 0.4f;

        // The other three are steady with a little movement, which is what an
        // idle machine actually looks like on this page.
        float wobble = MathF.Sin((float)c.Time * 0.7f) * 3;
        _disk = Math.Clamp(4 + wobble + Shell.Wm.Windows.Count * 0.6f, 0, 100);
        _net = Math.Clamp(1 + MathF.Abs(wobble) * 0.4f, 0, 100);

        _cpuHistory[_historyIndex] = _cpu;
        _memHistory[_historyIndex] = MemoryPercent;
        _diskHistory[_historyIndex] = _disk;
        _netHistory[_historyIndex] = _net;
        _historyIndex = (_historyIndex + 1) % _cpuHistory.Length;
    }

    float MemoryPercent => 30 + Shell.Wm.Windows.Count * 2.5f;

    // ---- the process table --------------------------------------------------

    /// <summary>One row. <c>Window</c> is set for the rows that are really a
    /// program of this system, which are the only ones that can be ended.</summary>
    readonly record struct Proc(string Name, IconId Icon, bool App,
                                float Cpu, float Mem, float Disk, float Net, OsWindow Window);

    /// <summary>A steady number for a name: the same process reports the same
    /// figure every frame instead of flickering.</summary>
    static float Steady(string seed, float min, float max)
    {
        int h = 17;
        foreach (char ch in seed) h = h * 31 + ch;
        return min + (Math.Abs(h) % 1000) / 1000f * (max - min);
    }

    List<Proc> Processes()
    {
        var list = new List<Proc>();

        foreach (var w in Shell.Wm.Windows.Where(x => x.ShowInTaskbar))
            list.Add(new Proc(w.TaskbarTitle, w.Icon, true,
                              Steady(w.ProcessName, 0.1f, 3.4f),
                              Steady(w.ProcessName + "m", 6, 74),
                              Steady(w.ProcessName + "d", 0, 1.2f),
                              Steady(w.ProcessName + "n", 0, 0.4f), w));

        (string name, IconId icon)[] background =
        {
            ("miminus.exe", IconId.MyComputer),
            ("explorer.exe", IconId.Folder),
            ("csrss.exe", IconId.Program),
            ("winlogon.exe", IconId.Program),
            ("services.exe", IconId.Settings),
            ("svchost.exe", IconId.Settings),
            ("antivirus.txt", IconId.Antivirus),
            ("bolgenos.exe", IconId.DlgError),
        };

        foreach (var (name, icon) in background)
        {
            // The one process that is not running, and never was.
            bool absent = name == "bolgenos.exe";
            list.Add(new Proc(name, icon, false,
                              absent ? 0 : Steady(name, 0, 1.8f),
                              absent ? 0 : Steady(name + "m", 2, 46),
                              absent ? 0 : Steady(name + "d", 0, 0.8f),
                              absent ? 0 : Steady(name + "n", 0, 0.2f), null));
        }

        return list;
    }

    // ---- frame --------------------------------------------------------------

    public override void DrawClient(UiContext c, Rect client)
    {
        // Version 8's task manager is white, not the dialog grey of the shell.
        c.R.FillRect(client, Color.White);

        if (!_details) { DrawSummary(c, client); return; }

        var area = client;
        DrawTabStrip(c, area.CutTop(30));
        var footer = area.CutBottom(38);

        switch (_tab)
        {
            case 0: DrawProcesses(c, area.Deflate(10, 6, 10, 0)); break;
            case 1: DrawPerformance(c, area.Deflate(10, 8, 10, 0)); break;
            default: DrawStartup(c, area.Deflate(10, 6, 10, 0)); break;
        }

        DrawFooter(c, footer);
    }

    /// <summary>The window as it first opens: the programs, and one button.</summary>
    void DrawSummary(UiContext c, Rect client)
    {
        var area = client.Deflate(14, 12, 14, 10);
        var footer = area.CutBottom(40);

        var apps = Shell.Wm.Windows.Where(w => w.ShowInTaskbar).ToList();

        if (apps.Count == 0)
            c.F.Ui.Draw(c.R, L.T("taskmgr8.nothing_running"), area.X + 2, area.Y + 6,
                        c.Theme.TextDisabled);

        for (int i = 0; i < apps.Count; i++)
        {
            var row = new Rect(area.X, area.Y + i * 26, area.W, 26);
            if (row.Bottom > area.Bottom) break;

            bool sel = i == _selected;
            if (sel) c.R.FillRect(row, c.Theme.Accent);
            else if (c.Hovering(row)) c.R.FillRect(row, Color.Rgb(0xE8F1FB));

            Icons.Draw(c.R, apps[i].Icon, new Rect(row.X + 4, row.CenterY - 8, 16, 16));
            c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(apps[i].TaskbarTitle, row.W - 34), row.X + 26,
                        row.CenterY - c.F.Ui.Height * 0.5f,
                        sel ? Color.White : c.Theme.Text);

            if (c.Clicked(row)) _selected = i;
            else if (c.DoubleClicked(row)) Shell.Wm.RestoreOrFocus(apps[i], c);
        }

        // «Подробнее» bottom left, «Снять задачу» bottom right — the two things
        // this window offered before it was opened up.
        var more = new Rect(footer.X, footer.CenterY - 10, 130, 20);
        bool hot = c.Hovering(more);
        W.Arrow(c, new Rect(more.X, more.Y, 12, more.H), 2, c.Theme.Accent);
        c.F.Ui.Draw(c.R, L.T("taskmgr8.more_details"), more.X + 14,
                    more.CenterY - c.F.Ui.Height * 0.5f,
                    hot ? c.Theme.Accent.Shade(0.8f) : c.Theme.Accent);
        if (c.Clicked(more))
        {
            _details = true;
            Bounds = new Rect(Bounds.X, Bounds.Y, MathF.Max(Bounds.W, 720), MathF.Max(Bounds.H, 500));
            c.Sound(Sfx.Navigate, 0.5f);
        }

        bool has = _selected >= 0 && _selected < apps.Count;
        if (W.Button(c, Id + ".end", new Rect(footer.Right - 130, footer.CenterY - 13, 130, 26),
                     L.T("taskmgr8.end_task"), has))
        {
            Shell.Wm.RequestClose(apps[_selected], c);
            _selected = -1;
        }
    }

    void DrawTabStrip(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.White);
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Color.Rgb(0xD8D8D8));

        string[] tabs = { L.T("sys.processes"), L.T("sys.performance"), L.T("taskmgr8.startup") };

        float x = bar.X + 10;
        for (int i = 0; i < tabs.Length; i++)
        {
            float w = c.F.Ui.Measure(tabs[i]) + 26;
            var tab = new Rect(x, bar.Y, w, bar.H);
            bool sel = i == _tab;

            if (sel)
            {
                c.R.FillRect(tab, Color.White);
                c.R.FillRect(new Rect(tab.X, tab.Bottom - 2, tab.W, 2), c.Theme.Accent);
            }
            else if (c.Hovering(tab)) c.R.FillRect(tab, Color.Rgb(0xF0F0F0));

            c.F.Ui.DrawCentered(c.R, tabs[i], tab, sel ? c.Theme.Accent : c.Theme.Text);
            if (c.Clicked(tab)) { _tab = i; c.SoundAt(Sfx.Click, tab, 0.4f); }
            x += w;
        }
    }

    void DrawFooter(UiContext c, Rect footer)
    {
        c.R.FillRect(new Rect(footer.X, footer.Y, footer.W, 1), Color.Rgb(0xD8D8D8));

        var less = new Rect(footer.X + 12, footer.CenterY - 10, 140, 20);
        W.Arrow(c, new Rect(less.X, less.Y, 12, less.H), 0, c.Theme.Accent);
        c.F.Ui.Draw(c.R, L.T("taskmgr8.fewer_details"), less.X + 14,
                    less.CenterY - c.F.Ui.Height * 0.5f, c.Theme.Accent);
        if (c.Clicked(less)) { _details = false; _selected = -1; c.Sound(Sfx.Navigate, 0.5f); }

        if (_tab != 0) return;

        var procs = Processes();
        int index = _selected;
        bool has = index >= 0 && index < procs.Count && procs[index].Window != null;

        if (W.Button(c, Id + ".endtask", new Rect(footer.Right - 130, footer.CenterY - 13, 130, 26),
                     L.T("taskmgr8.end_task"), has))
        {
            Shell.Wm.RequestClose(procs[index].Window, c);
            _selected = -1;
        }
    }

    // ---- процессы ------------------------------------------------------------

    /// <summary>The wash behind a number: nothing at rest, yellow as it climbs,
    /// orange when it matters. Version 8's one good idea about this table.</summary>
    static Color Heat(float value, float ceiling)
    {
        float f = Math.Clamp(value / ceiling, 0, 1);
        if (f < 0.02f) return Color.Transparent;

        // Pale yellow through amber to a dull red.
        return f < 0.5f
            ? Color.Lerp(Color.Rgb(0xFFF6D8), Color.Rgb(0xFFD98A), f * 2)
            : Color.Lerp(Color.Rgb(0xFFD98A), Color.Rgb(0xE8875A), (f - 0.5f) * 2);
    }

    void DrawProcesses(UiContext c, Rect body)
    {
        var procs = Processes();

        float totalCpu = procs.Sum(p => p.Cpu) + _cpu * 0.4f;
        float totalMem = MemoryPercent;
        float totalDisk = procs.Sum(p => p.Disk);
        float totalNet = procs.Sum(p => p.Net);

        // ---- header, with the totals version 8 put in the column titles ----
        var head = body.CutTop(40);
        float nameW = body.W - 4 * 86;

        (string key, float total, string unit)[] cols =
        {
            ("taskmgr8.cpu", totalCpu, "%"),
            ("taskmgr8.memory", totalMem, "%"),
            ("taskmgr8.disk", totalDisk, "%"),
            ("taskmgr8.network", totalNet, "%"),
        };

        c.F.Ui.Draw(c.R, L.T("taskmgr8.name"), head.X + 4, head.Bottom - c.F.Ui.Height - 4,
                    c.Theme.TextDisabled);

        for (int i = 0; i < cols.Length; i++)
        {
            var cell = new Rect(head.X + nameW + i * 86, head.Y, 86, head.H);
            string total = cols[i].total.ToString("0") + cols[i].unit;

            c.F.Small.DrawCentered(c.R, total, new Rect(cell.X, cell.Y + 2, cell.W, 14),
                                   c.Theme.Accent);
            c.F.Ui.DrawCentered(c.R, L.T(cols[i].key),
                                new Rect(cell.X, cell.Bottom - c.F.Ui.Height - 4, cell.W, c.F.Ui.Height),
                                c.Theme.TextDisabled);
        }
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), Color.Rgb(0xD8D8D8));

        // ---- the rows, in the two groups the original sorted them into -----
        c.R.PushClip(body);
        float y = body.Y + 2;
        int index = 0;

        foreach (bool apps in new[] { true, false })
        {
            var group = procs.Where(p => p.App == apps).ToList();
            if (group.Count == 0) { index += group.Count; continue; }

            var title = new Rect(body.X, y, body.W, 22);
            c.F.UiBold.Draw(c.R, L.F(apps ? "taskmgr8.group_apps" : "taskmgr8.group_background",
                                     group.Count),
                            title.X + 4, title.CenterY - c.F.UiBold.Height * 0.5f, c.Theme.Text);
            y = title.Bottom;

            foreach (var p in group)
            {
                int rowIndex = procs.IndexOf(p);
                var row = new Rect(body.X, y, body.W, 24);
                if (row.Bottom > body.Bottom) break;

                bool sel = rowIndex == _selected;
                if (sel) c.R.FillRect(row, c.Theme.Accent);
                else if (c.Hovering(row)) c.R.FillRect(row, Color.Rgb(0xE8F1FB));

                Icons.Draw(c.R, p.Icon, new Rect(row.X + 18, row.CenterY - 8, 16, 16));
                c.R.PushClip(new Rect(row.X, row.Y, nameW, row.H));
                c.F.Ui.Draw(c.R, p.Name, row.X + 40, row.CenterY - c.F.Ui.Height * 0.5f,
                            sel ? Color.White : c.Theme.Text);
                c.R.PopClip();

                float[] values = { p.Cpu, p.Mem, p.Disk, p.Net };
                float[] ceilings = { 4f, 80f, 1.5f, 0.5f };
                string[] units = { "%", " МБ", " МБ/с", " Мбит/с" };

                for (int k = 0; k < 4; k++)
                {
                    var cell = new Rect(row.X + nameW + k * 86, row.Y, 86, row.H);
                    if (!sel)
                    {
                        var wash = Heat(values[k], ceilings[k]);
                        if (wash.A > 0) c.R.FillRect(cell.Deflate(1), wash);
                    }

                    string text = values[k] <= 0.005f ? "0" + units[k]
                                : values[k].ToString(k == 1 ? "0" : "0.0") + units[k];
                    c.F.Small.DrawRight(c.R, text, new Rect(cell.X, cell.Y, cell.W - 8, cell.H),
                                        sel ? Color.White : c.Theme.Text);
                }

                if (c.Clicked(row)) _selected = rowIndex;
                else if (c.DoubleClicked(row) && p.Window != null)
                    Shell.Wm.RestoreOrFocus(p.Window, c);
                else if (c.RightClicked(row)) ShowProcessMenu(c, p, rowIndex);

                y = row.Bottom;
                index++;
            }

            y += 6;
        }
        c.R.PopClip();
    }

    void ShowProcessMenu(UiContext c, Proc p, int index)
    {
        _selected = index;
        Shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("taskmgr8.switch_to"),
                        () => Shell.Wm.RestoreOrFocus(p.Window, c), p.Icon,
                        enabled: p.Window != null),
            MenuItem.Sep(),
            MenuItem.Of(L.T("taskmgr8.end_task"),
                        () => Shell.Wm.RequestClose(p.Window, c), enabled: p.Window != null),
            MenuItem.Of(L.T("taskmgr8.open_file_location"),
                        () => Shell.Launch(c, "mycomputer", null), IconId.Folder),
        }, c.MouseX, c.MouseY, this, c);
    }

    // ---- производительность --------------------------------------------------

    void DrawPerformance(UiContext c, Rect body)
    {
        (string key, float value, float[] history, Color colour, string unit)[] cards =
        {
            ("taskmgr8.cpu", _cpu, _cpuHistory, Color.Rgb(0x2D89EF), "%"),
            ("taskmgr8.memory", MemoryPercent, _memHistory, Color.Rgb(0x7E3878), "%"),
            ("taskmgr8.disk", _disk, _diskHistory, Color.Rgb(0x00A300), "%"),
            ("taskmgr8.network", _net, _netHistory, Color.Rgb(0xDA532C), "%"),
        };

        var side = body.CutLeft(150);
        for (int i = 0; i < cards.Length; i++)
        {
            var card = new Rect(side.X, side.Y + i * 74, side.W - 8, 66);
            bool sel = i == _resource;

            if (sel) c.R.FillRect(card, Color.Rgb(0xE8F1FB));
            else if (c.Hovering(card)) c.R.FillRect(card, Color.Rgb(0xF3F3F3));
            if (sel) c.R.FillRect(new Rect(card.X, card.Y, 3, card.H), cards[i].colour);

            var mini = new Rect(card.X + 10, card.Y + 8, 42, 42);
            DrawGraph(c, mini, cards[i].history, cards[i].colour, grid: false);

            c.F.Ui.Draw(c.R, L.T(cards[i].key), mini.Right + 10, card.Y + 10, c.Theme.Text);
            c.F.Small.Draw(c.R, cards[i].value.ToString("0") + cards[i].unit,
                           mini.Right + 10, card.Y + 12 + c.F.Ui.Height, c.Theme.TextDisabled);

            if (c.Clicked(card)) { _resource = i; c.SoundAt(Sfx.Click, card, 0.4f); }
        }

        // ---- the big one ---------------------------------------------------
        var (key, value, history, colour, unit) = cards[_resource];

        var title = body.CutTop(40);
        c.F.Big.Draw(c.R, L.T(key), title.X, title.Y - 6, c.Theme.Text);
        c.F.Small.DrawRight(c.R, L.T("taskmgr8.sixty_seconds"),
                            new Rect(title.X, title.Bottom - 16, title.W, 14), c.Theme.TextDisabled);

        var stats = body.CutBottom(72);
        var graph = body.Deflate(0, 0, 0, 8);
        DrawGraph(c, graph, history, colour, grid: true);

        c.F.Small.Draw(c.R, "100%", graph.X + 4, graph.Y + 2, c.Theme.TextDisabled);
        c.F.Small.Draw(c.R, "0%", graph.X + 4, graph.Bottom - c.F.Small.Height - 2,
                       c.Theme.TextDisabled);

        (string key, string value)[] readouts = _resource switch
        {
            0 => new[]
            {
                ("taskmgr8.utilisation", _cpu.ToString("0") + "%"),
                ("taskmgr8.processes", Processes().Count.ToString()),
                ("taskmgr8.uptime", L.T("taskmgr8.uptime_value")),
                ("taskmgr8.speed", "2,40 ГГц"),
            },
            1 => new[]
            {
                ("taskmgr8.in_use", (MemoryPercent * 20.48f).ToString("0") + " МБ"),
                ("taskmgr8.available", (2048 - MemoryPercent * 20.48f).ToString("0") + " МБ"),
                ("taskmgr8.total", "2,0 ГБ"),
                ("taskmgr8.speed", "667 МГц"),
            },
            2 => new[]
            {
                ("taskmgr8.active_time", _disk.ToString("0") + "%"),
                ("taskmgr8.capacity", "80 ГБ"),
                ("taskmgr8.model", "МИМИНУС HDD"),
                ("taskmgr8.speed", "7200 об/мин"),
            },
            _ => new[]
            {
                ("taskmgr8.send", _net.ToString("0.0") + " Мбит/с"),
                ("taskmgr8.receive", "0,0 Мбит/с"),
                ("taskmgr8.adapter", L.T("taskmgr8.adapter_value")),
                ("taskmgr8.state", L.T("charm.network_state")),
            },
        };

        float sx = stats.X;
        foreach (var (rk, rv) in readouts)
        {
            c.F.Small.Draw(c.R, L.T(rk), sx, stats.Y + 8, c.Theme.TextDisabled);
            c.F.Caption.Draw(c.R, rv, sx, stats.Y + 10 + c.F.Small.Height, c.Theme.Text);
            sx += MathF.Max(120, stats.W / readouts.Length);
        }
    }

    void DrawGraph(UiContext c, Rect r, float[] history, Color colour, bool grid)
    {
        c.R.FillRect(r, Color.Rgb(0xFBFBFB));
        c.R.DrawRect(r, Color.Rgb(0xD8D8D8));

        if (grid)
            for (int i = 1; i < 10; i++)
            {
                c.R.FillRect(new Rect(r.X + 1, r.Y + r.H * i / 10f, r.W - 2, 1), Color.Rgb(0xEDEDED));
                c.R.FillRect(new Rect(r.X + r.W * i / 10f, r.Y + 1, 1, r.H - 2), Color.Rgb(0xEDEDED));
            }

        int n = history.Length;
        float step = (r.W - 2) / (n - 1);

        // Filled underneath and drawn on top, the way version 8's graphs were.
        for (int i = 1; i < n; i++)
        {
            int a = (_historyIndex + i - 1) % n;
            int b = (_historyIndex + i) % n;

            float x0 = r.X + 1 + (i - 1) * step, x1 = r.X + 1 + i * step;
            float y0 = r.Bottom - 1 - (r.H - 2) * Math.Clamp(history[a], 0, 100) / 100f;
            float y1 = r.Bottom - 1 - (r.H - 2) * Math.Clamp(history[b], 0, 100) / 100f;

            c.R.FillTriangle(x0, y0, x1, y1, x1, r.Bottom - 1, colour.WithAlpha((byte)60));
            c.R.FillTriangle(x0, y0, x1, r.Bottom - 1, x0, r.Bottom - 1, colour.WithAlpha((byte)60));
            c.R.Line(x0, y0, x1, y1, colour, 1.4f);
        }
    }

    // ---- автозагрузка ---------------------------------------------------------

    static readonly (string name, IconId icon, string impactKey, bool enabled)[] Startup =
    {
        ("miminus.exe", IconId.MyComputer, "taskmgr8.impact_high", true),
        ("explorer.exe", IconId.Folder, "taskmgr8.impact_medium", true),
        ("antivirus.txt", IconId.Antivirus, "taskmgr8.impact_none", true),
        ("orega.exe", IconId.Opera, "taskmgr8.impact_medium", false),
        ("bolgenos.exe", IconId.DlgError, "taskmgr8.impact_none", false),
    };

    void DrawStartup(UiContext c, Rect body)
    {
        c.F.Small.Draw(c.R, L.T("taskmgr8.startup_note"), body.X + 4, body.Y + 2,
                       c.Theme.TextDisabled);
        body.CutTop(c.F.Small.Height + 10);

        var head = body.CutTop(24);
        float nameW = body.W - 260;
        c.F.Ui.Draw(c.R, L.T("taskmgr8.name"), head.X + 4, head.Y + 4, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("taskmgr8.publisher"), head.X + nameW, head.Y + 4, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("taskmgr8.status"), head.X + nameW + 130, head.Y + 4, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("taskmgr8.impact"), head.X + nameW + 190, head.Y + 4, c.Theme.TextDisabled);
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), Color.Rgb(0xD8D8D8));

        for (int i = 0; i < Startup.Length; i++)
        {
            var row = new Rect(body.X, body.Y + i * 26, body.W, 26);
            if (row.Bottom > body.Bottom) break;

            bool sel = i == _selected;
            if (sel) c.R.FillRect(row, c.Theme.Accent);
            else if (c.Hovering(row)) c.R.FillRect(row, Color.Rgb(0xE8F1FB));

            Color ink = sel ? Color.White : c.Theme.Text;
            Icons.Draw(c.R, Startup[i].icon, new Rect(row.X + 6, row.CenterY - 8, 16, 16));
            c.F.Ui.Draw(c.R, Startup[i].name, row.X + 28, row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.F.Ui.Draw(c.R, L.T("taskmgr8.publisher_value"), row.X + nameW,
                        row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.F.Ui.Draw(c.R, L.T(Startup[i].enabled ? "taskmgr8.enabled" : "taskmgr8.disabled"),
                        row.X + nameW + 130, row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.F.Ui.Draw(c.R, L.T(Startup[i].impactKey), row.X + nameW + 190,
                        row.CenterY - c.F.Ui.Height * 0.5f, ink);

            if (c.Clicked(row)) _selected = i;
        }
    }
}
