using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Панель управления, as every Windows from 7 onward drew it.
///
/// The XP grid of icons became a page: a breadcrumb along the top with a search
/// box on the right, a column of links down the left, and the applets gathered
/// into eight categories, each one a heading in blue with its own tasks listed
/// underneath in grey. The «Просмотр» control in the corner switches between
/// that and the flat wall of icons, which is the other half of what the real one
/// does — and the flat wall is the older window, kept.
///
/// It is also a window that goes places rather than one that opens others.
/// Version 8 made the Control Panel a single frame you navigate — the address
/// bar carries the trail, the arrows go back and forward through it, and
/// «Персонализация» and «Разрешение экрана» are pages inside it rather than
/// windows of their own. Both of those live here now, which is where the two
/// halves of the old Свойства: Экран ended up.
///
/// Opening one of them from the desktop opens the frame at that page and titles
/// the window after it, which is exactly what the original did: it is the same
/// window, standing somewhere else.</summary>
public sealed partial class ControlPanelWindow : OsWindow
{
    /// <summary>Where the frame is standing. Home is the wall of categories;
    /// the rest are the pages that used to be windows of their own.</summary>
    public enum Page { Home, Personalise, Screen, Update }

    /// <summary>Every page carries its own icon, so a frame standing on one of
    /// them is that applet in the taskbar rather than the Control Panel.</summary>
    static IconId IconFor(Page page) => page switch
    {
        Page.Screen => IconId.Devices,
        Page.Personalise => IconId.Display,
        Page.Update => IconId.Shield,
        _ => IconId.ControlPanel,
    };

    /// <summary>The trail, and where along it we are — which is all the two
    /// arrows in the address bar need.</summary>
    readonly List<Page> _history = new() { Page.Home };
    int _step;

    Page Current => _history[_step];

    public ControlPanelWindow() : this(Page.Home) { }

    public ControlPanelWindow(Page page)
    {
        Icon = IconFor(page);
        Bounds = new Rect(0, 0, 760, 560);

        if (page != Page.Home) { _history.Add(page); _step = 1; }
    }

    /// <summary>Goes to a page, remembering the way back. Going somewhere from
    /// halfway down the trail throws away what was in front, as a trail does.</summary>
    public void GoTo(Page page, UiContext c)
    {
        if (page == Current) return;

        if (_step < _history.Count - 1) _history.RemoveRange(_step + 1, _history.Count - _step - 1);
        _history.Add(page);
        _step = _history.Count - 1;

        Icon = IconFor(page);
        c.Sound(Sfx.Navigate, 0.5f);
    }

    void Step(UiContext c, int delta)
    {
        int next = Math.Clamp(_step + delta, 0, _history.Count - 1);
        if (next == _step) return;

        _step = next;
        Icon = IconFor(Current);
        c.Sound(Sfx.Navigate, 0.5f);
    }

    /// <summary>The window is named after the page it is showing, which is how
    /// one frame manages to be three windows in the taskbar.</summary>
    public override string Title => Current switch
    {
        Page.Personalise => L.T("person.title"),
        Page.Screen => L.T("screen.title"),
        Page.Update => L.T("update.title"),
        _ => L.T("sys.control_panel"),
    };
    public override float MinWidth => 560;
    public override float MinHeight => 380;

    /// <summary>One thing that can be opened. <c>DescKey</c> is what the status
    /// strip says about it, and what the category page prints underneath the
    /// heading when it has room.</summary>
    sealed record Applet(string Key, IconId Icon, string DescKey, Action<UiContext, ControlPanelWindow> Open);

    /// <summary>One heading on the category page, and the applets under it.</summary>
    sealed record Category(string Key, string DescKey, IconId Icon, Applet[] Items);

    // These two are pages of this window rather than windows of their own, so
    // they are the two applets that move the frame instead of opening anything.
    static readonly Applet Display = new("person.title", IconId.Display,
        "cpl.change_the_background_theme_and_resolution",
        (c, w) => w.GoTo(Page.Personalise, c));

    static readonly Applet Screen = new("screen.title", IconId.Devices,
        "screen.applet_desc",
        (c, w) => w.GoTo(Page.Screen, c));

    static readonly Applet Sound = new("cpl.sounds_and_audio_devices", IconId.Volume,
        "cpl.volume_and_sound_scheme",
        (c, w) => w.Shell.Launch(c, "sound", null));

    static readonly Applet Language = new("cpl.regional_and_language_options", IconId.Flag,
        "cpl.system_interface_language",
        (c, w) => w.Shell.Launch(c, "language", null));

    static readonly Applet System = new("cpl.system", IconId.MyComputer,
        "cpl.information_about_miminus",
        (c, w) => w.Shell.Launch(c, "about", null));

    static readonly Applet Programs = new("cpl.add_or_remove_programs", IconId.Program,
        "cpl.everything_is_already_installed",
        (c, w) => w.Shell.MessageBox(c, L.T("sys.add_or_remove_programs"),
            L.T("sys.nothing_to_remove_it_is_all_written_from_scr"),
            MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info));

    static readonly Applet Network = new("cpl.network_connections", IconId.Network,
        "cpl.verify_your_connection_settings",
        (c, w) => w.Shell.Launch(c, "network", null));

    static readonly Applet Printers = new("cpl.printers_and_faxes", IconId.Printer,
        "cpl.no_printers_installed",
        (c, w) => w.Shell.Launch(c, "printers", null));

    static readonly Applet Taskbar = new("tbprops.title", IconId.Settings,
        "cpl.taskbar_and_start_menu",
        (c, w) => w.Shell.Launch(c, "taskbarprops", null));

    static readonly Applet Update = new("cpl.miminus_update", IconId.Shield,
        "cpl.check_the_repository_for_a_newer_version",
        (c, w) => w.GoTo(Page.Update, c));

    static readonly Applet Security = new("cpl.security_center", IconId.Antivirus,
        "cpl.grevtsov_antivirus_2009",
        (c, w) => w.Shell.Launch(c, "notepad", w.Shell.Fs.AntivirusFile));

    static readonly Applet Clock = new("cpl.date_and_time", IconId.Clock,
        "cpl.the_system_clock",
        (c, w) => w.Shell.Launch(c, "clock", null));

    static readonly Applet PcSettings = new("pcs.title", IconId.PcSettings,
        "cpl.pc_settings_desc",
        (c, w) => w.Shell.Launch(c, "pcsettings", null));

    static readonly Applet Access = new("access.title", IconId.Access,
        "access.applet_desc",
        (c, w) => w.Shell.Launch(c, "access", null));

    static readonly Applet Power = new("power.title", IconId.Power,
        "power.applet_desc",
        (c, w) => w.Shell.Launch(c, "power", null));

    static readonly Applet DeviceManager = new("devmgr.title", IconId.MyComputer,
        "devmgr.applet_desc",
        (c, w) => w.Shell.Launch(c, "devmgr", null));

    static readonly Applet TaskManager = new("taskbar.task_manager", IconId.Settings,
        "cpl.task_manager_desc",
        (c, w) => w.Shell.Launch(c, "taskmgr", null));

    static readonly Applet Regedit = new("regedit.title", IconId.Registry,
        "regedit.applet_desc",
        (c, w) => w.Shell.Launch(c, "regedit", null));

    static readonly Applet Store = new("store.title", IconId.Store,
        "cpl.store_desc",
        (c, w) => w.Shell.Launch(c, "store", null));

    static readonly Category[] Categories =
    {
        new("cpl.cat_appearance", "cpl.cat_appearance_desc", IconId.Display,
            new[] { Display, Screen, Taskbar }),
        new("cpl.cat_hardware", "cpl.cat_hardware_desc", IconId.Printer,
            new[] { Sound, Printers, Power, DeviceManager }),
        new("cpl.cat_network", "cpl.cat_network_desc", IconId.Network,
            new[] { Network }),
        new("cpl.cat_programs", "cpl.cat_programs_desc", IconId.Program,
            new[] { Programs, Store }),
        new("cpl.cat_system", "cpl.cat_system_desc", IconId.MyComputer,
            new[] { System, TaskManager, Regedit, PcSettings }),
        new("cpl.cat_security", "cpl.cat_security_desc", IconId.Shield,
            new[] { Security, Update }),
        new("cpl.cat_clock", "cpl.cat_clock_desc", IconId.Clock,
            new[] { Clock, Language }),
        new("cpl.cat_access", "cpl.cat_access_desc", IconId.Access,
            new[] { Access }),
    };

    /// <summary>Everything, flat, for the icon view.</summary>
    static readonly Applet[] Applets =
    {
        Display, Screen, Sound, Language, System, Programs, Network, Printers,
        Taskbar, Update, Security, Clock, PcSettings, TaskManager, Store,
        Access, Power, DeviceManager, Regedit,
    };

    /// <summary>0 = categories, 1 = large icons. The real one remembers this
    /// per user; this one remembers it per window, which is the same thing
    /// until the window is closed.</summary>
    int _view;

    int _hover = -1;

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Color.White);

        var area = client;
        DrawAddressBar(c, area.CutTop(30));
        var status = area.CutBottom(22);
        var side = area.CutLeft(210);

        DrawSidePane(c, side);

        _hover = -1;

        switch (Current)
        {
            case Page.Personalise:
                DrawPersonalisePage(c, area);
                break;

            case Page.Screen:
                DrawScreenPage(c, area.Deflate(18, 14, 14, 8));
                break;

            case Page.Update:
                DrawUpdatePage(c, area.Deflate(18, 14, 14, 8));
                break;

            default:
                var page = area.Deflate(18, 14, 14, 8);
                DrawHeader(c, ref page);
                if (_view == 0) DrawCategories(c, page);
                else DrawIcons(c, page);
                break;
        }

        // The strip at the foot says what the pointer is over, as it always did.
        c.R.FillRect(status, Color.Rgb(0xF5F5F5));
        c.R.FillRect(new Rect(status.X, status.Y, status.W, 1), Color.Rgb(0xE0E0E0));
        c.F.Small.Draw(c.R, _hover >= 0 ? L.T(Applets[_hover].DescKey)
                                        : Current == Page.Home
                                            ? L.F("sys.0_items", Applets.Length)
                                            : L.T(PageDesc(Current)),
                       status.X + 8, status.CenterY - c.F.Small.Height * 0.5f, c.Theme.TextDisabled);
    }

    /// <summary>What the strip at the foot says when the pointer is over
    /// nothing in particular — which on a page is what the page is for.</summary>
    static string PageDesc(Page page) => page switch
    {
        Page.Screen => "screen.applet_desc",
        Page.Personalise => "cpl.change_the_background_theme_and_resolution",
        Page.Update => "cpl.check_the_repository_for_a_newer_version",
        _ => "cpl.all_categories",
    };

    // ---- chrome ---------------------------------------------------------------

    void DrawAddressBar(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.Rgb(0xF5F5F5));
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Color.Rgb(0xE0E0E0));

        // Back and forward, and they work: the frame has a trail now.
        float x = bar.X + 6;
        bool canBack = _step > 0;
        bool canForward = _step < _history.Count - 1;

        for (int i = 0; i < 2; i++)
        {
            bool enabled = i == 0 ? canBack : canForward;
            var r = new Rect(x, bar.CenterY - 11, 22, 22);

            if (enabled && c.Hovering(r)) c.R.FillCircle(r.CenterX, r.CenterY, 11, Color.Rgb(0xE8F1FB));
            W.Arrow(c, r, i == 0 ? 3 : 1,
                    enabled ? c.Theme.Accent : c.Theme.TextDisabled, 4.5f);

            if (enabled && c.Clicked(r)) Step(c, i == 0 ? -1 : 1);
            x += 24;
        }

        var field = new Rect(x + 6, bar.Y + 4, bar.W - (x - bar.X) - 190, bar.H - 9);
        c.R.FillRect(field, Color.White);
        c.R.DrawRect(field, Color.Rgb(0xC8C8C8));

        Icons.Draw(c.R, IconId.ControlPanel, new Rect(field.X + 3, field.CenterY - 8, 16, 16));

        // The trail: the root, then the category the page sits under, then the
        // page. Every part of it but the last is a place to go back to.
        var crumbs = new List<(string label, Page? page)>
        {
            (L.T("sys.control_panel"), Page.Home),
        };

        if (Current != Page.Home)
        {
            crumbs.Add((L.T(Current == Page.Update ? "cpl.cat_security" : "cpl.cat_appearance"),
                        Page.Home));
            crumbs.Add((L.T(Current switch
            {
                Page.Screen => "screen.title",
                Page.Update => "update.title",
                _ => "person.title",
            }), null));
        }
        else crumbs.Add((L.T(_view == 0 ? "cpl.all_categories" : "cpl.all_items"), null));

        c.R.PushClip(field);
        float cx = field.X + 23;
        for (int i = 0; i < crumbs.Count; i++)
        {
            var (label, page) = crumbs[i];
            var crumb = new Rect(cx, field.Y + 1, c.F.Ui.Measure(label) + 10, field.H - 2);

            bool live = page.HasValue && i < crumbs.Count - 1;
            if (live && c.Hovering(crumb)) c.R.FillRect(crumb, Color.Rgb(0xE8F1FB));
            c.F.Ui.DrawCentered(c.R, label, crumb, c.Theme.Text);
            if (live && c.Clicked(crumb)) GoTo(page.Value, c);

            cx = crumb.Right;
            if (i == crumbs.Count - 1) break;

            var sep = new Rect(cx, field.Y, 14, field.H);
            W.Arrow(c, sep, 1, c.Theme.TextDisabled, 3f);
            cx = sep.Right;
        }
        c.R.PopClip();

        var search = new Rect(field.Right + 6, field.Y, bar.Right - field.Right - 12, field.H);
        c.R.FillRect(search, Color.White);
        c.R.DrawRect(search, Color.Rgb(0xC8C8C8));
        c.F.Ui.Draw(c.R, L.T("cpl.search_control_panel"), search.X + 6,
                    search.CenterY - c.F.Ui.Height * 0.5f, c.Theme.TextDisabled);
        Icons.Draw(c.R, IconId.Search, new Rect(search.Right - 20, search.CenterY - 8, 16, 16));
        if (c.Clicked(search)) Shell.Launch(c, "search", null);
    }

    void DrawSidePane(UiContext c, Rect side)
    {
        c.R.FillRect(side, Color.Rgb(0xF7F7F7));
        c.R.FillRect(new Rect(side.Right - 1, side.Y, 1, side.H), Color.Rgb(0xE0E0E0));

        var area = side.Deflate(14, 14, 10, 10);

        // The pane's own title is long enough to need two lines at this width.
        foreach (string line in c.F.UiBold.Wrap(L.T("cpl.home"), area.W - 4))
        {
            var row = area.CutTop(c.F.UiBold.Height + 1);
            c.F.UiBold.Draw(c.R, line, row.X, row.Y, Color.Rgb(0x1E4E79));
        }
        area.CutTop(8);

        // The home link, then the two pages this frame can show, then the
        // sheets that are still windows of their own.
        if (Current != Page.Home) PageLink(c, ref area, "cpl.home_link", Page.Home);
        PageLink(c, ref area, "person.title", Page.Personalise);
        PageLink(c, ref area, "screen.title", Page.Screen);
        PageLink(c, ref area, "cpl.miminus_update", Page.Update);

        (string key, string app)[] links =
        {
            ("pcs.title", "pcsettings"),
            ("tbprops.title", "taskbarprops"),
        };
        foreach (var (key, app) in links) SideLink(c, ref area, key, app);

        area.CutTop(14);
        c.F.UiBold.Draw(c.R, L.T("sys.see_also"), area.X, area.Y, Color.Rgb(0x1E4E79));
        c.R.FillRect(new Rect(area.X, area.Y + c.F.UiBold.Height + 3, area.W - 6, 1),
                     Color.Rgb(0xE0E0E0));
        area.CutTop(c.F.UiBold.Height + 10);

        (string key, string app)[] more =
        {
            ("cpl.help_and_support", "help"),
            ("cpl.my_computer", "mycomputer"),
            ("cpl.task_manager", "taskmgr"),
        };
        foreach (var (key, app) in more) SideLink(c, ref area, key, app);
    }

    /// <summary>A link to another page of this frame, marked when it is the
    /// one already showing.</summary>
    void PageLink(UiContext c, ref Rect area, string key, Page page)
    {
        var row = area.CutTop(22);
        bool here = Current == page;
        bool hot = c.Hovering(row);

        if (here) c.R.FillRect(row.Inflate(2), Color.Rgb(0xDCE9F7));
        else if (hot) c.R.FillRect(row.Inflate(2), Color.Rgb(0xE8F1FB));

        c.R.PushClip(row);
        c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(L.T(key), row.W), row.X,
                    row.CenterY - c.F.Ui.Height * 0.5f,
                    here ? c.Theme.Accent : hot ? c.Theme.Accent : c.Theme.Text);
        c.R.PopClip();

        if (c.Clicked(row)) GoTo(page, c);
    }

    void SideLink(UiContext c, ref Rect area, string key, string app)
    {
        var row = area.CutTop(22);
        bool hot = c.Hovering(row);
        if (hot) c.R.FillRect(row.Inflate(2), Color.Rgb(0xE8F1FB));

        c.R.PushClip(row);
        c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(L.T(key), row.W), row.X,
                    row.CenterY - c.F.Ui.Height * 0.5f,
                    hot ? c.Theme.Accent : c.Theme.Text);
        c.R.PopClip();

        if (c.Clicked(row)) Shell.Launch(c, app, null);
    }

    void DrawHeader(UiContext c, ref Rect page)
    {
        var head = page.CutTop(40);

        // The heading is set in the caption face, not the poster one: it has to
        // share the line with the «Просмотр» control on the right.
        c.R.PushClip(new Rect(head.X, head.Y, head.W - 150, head.H));
        c.F.Caption.Draw(c.R, L.T("cpl.adjust_settings"), head.X, head.Y + 2, Color.Rgb(0x1E4E79));
        c.R.PopClip();

        // «Просмотр:» in the corner, which is the control that swaps the page.
        string label = L.T("cpl.view_by");
        float lw = c.F.Small.Measure(label);
        var value = new Rect(head.Right - 130, head.Y + 2, 130, 18);

        c.F.Small.Draw(c.R, label, value.X - lw - 6, value.CenterY - c.F.Small.Height * 0.5f,
                       c.Theme.TextDisabled);

        bool hot = c.Hovering(value);
        c.F.Small.Draw(c.R, L.T(_view == 0 ? "cpl.view_categories" : "cpl.view_icons"),
                       value.X, value.CenterY - c.F.Small.Height * 0.5f,
                       hot ? c.Theme.Accent : Color.Rgb(0x1E4E79));
        W.Arrow(c, new Rect(value.Right - 16, value.Y, 12, value.H), 2, c.Theme.TextDisabled);

        if (c.Clicked(value))
        {
            Shell.Menus.Open(new List<MenuItem>
            {
                new() { Text = L.T("cpl.view_categories"), IsRadio = true, Checked = _view == 0,
                        Click = () => _view = 0 },
                new() { Text = L.T("cpl.view_icons"), IsRadio = true, Checked = _view == 1,
                        Click = () => _view = 1 },
            }, value.X, value.Bottom, this, c);
        }
    }

    // ---- the category page -----------------------------------------------------

    void DrawCategories(UiContext c, Rect page)
    {
        float colW = page.W * 0.5f;
        float rowH = 74;

        for (int i = 0; i < Categories.Length; i++)
        {
            var cat = Categories[i];
            var cell = new Rect(page.X + (i % 2) * colW, page.Y + (i / 2) * rowH, colW - 16, rowH - 8);
            if (cell.Bottom > page.Bottom) break;

            var icon = new Rect(cell.X, cell.Y + 2, 32, 32);
            Icons.Draw(c.R, cat.Icon, icon);

            // The heading is a link: it opens the first applet under it, which
            // is what the real one does when a category has an obvious front.
            string title = L.T(cat.Key);
            var head = new Rect(icon.Right + 10, cell.Y, c.F.UiBold.Measure(title) + 4,
                                c.F.UiBold.Height + 2);
            bool headHot = c.Hovering(head);
            c.F.UiBold.Draw(c.R, title, head.X, head.Y,
                            headHot ? c.Theme.Accent : Color.Rgb(0x1E4E79));
            if (headHot)
                c.R.FillRect(new Rect(head.X, head.Y + c.F.UiBold.Ascent + 3, head.W - 4, 1),
                             c.Theme.Accent);
            if (c.Clicked(head)) Activate(c, cat.Items[0]);

            // The tasks under it, in grey, each one its own link.
            float ty = head.Bottom + 2;
            foreach (var item in cat.Items)
            {
                string text = L.T(item.Key);
                var link = new Rect(head.X, ty, MathF.Min(cell.Right - head.X, c.F.Small.Measure(text) + 4),
                                    c.F.Small.Height + 2);
                if (link.Bottom > cell.Bottom) break;

                bool hot = c.Hovering(link);
                if (hot) _hover = Array.IndexOf(Applets, item);

                c.R.PushClip(new Rect(link.X, link.Y, cell.Right - link.X, link.H));
                c.F.Small.Draw(c.R, text, link.X, link.Y, hot ? c.Theme.Accent : Color.Rgb(0x606060));
                c.R.PopClip();

                if (c.Clicked(link)) Activate(c, item);
                ty = link.Bottom + 1;
            }
        }
    }

    // ---- the icon page ---------------------------------------------------------

    void DrawIcons(UiContext c, Rect page)
    {
        const float cellW = 172, cellH = 44;
        int cols = Math.Max(1, (int)(page.W / cellW));

        for (int i = 0; i < Applets.Length; i++)
        {
            var a = Applets[i];
            var cell = new Rect(page.X + (i % cols) * cellW, page.Y + (i / cols) * cellH,
                                cellW - 8, cellH - 6);
            if (cell.Bottom > page.Bottom) break;

            bool hot = c.Hovering(cell);
            if (hot) { _hover = i; c.R.FillRect(cell, Color.Rgb(0xE8F1FB)); }

            Icons.Draw(c.R, a.Icon, new Rect(cell.X + 4, cell.CenterY - 16, 32, 32));

            c.R.PushClip(cell);
            var lines = c.F.Ui.Wrap(L.T(a.Key), cell.W - 44);
            float ty = cell.CenterY - lines.Count * (c.F.Ui.Height + 1) * 0.5f;
            foreach (string line in lines.Take(2))
            {
                c.F.Ui.Draw(c.R, line, cell.X + 40, ty, hot ? c.Theme.Accent : c.Theme.Text);
                ty += c.F.Ui.Height + 1;
            }
            c.R.PopClip();

            if (c.Clicked(cell)) Activate(c, a);
        }
    }

    void Activate(UiContext c, Applet a)
    {
        c.Sound(Sfx.Click, 0.5f);
        a.Open(c, this);
    }
}
