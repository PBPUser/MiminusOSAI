using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Проводник — the folder window from the videos, task pane and all.
///
/// Covers the two things the reference footage actually does with it: browsing
/// «Революционные дистрибутивы» and «ПОЛЕЗНЫЕ ФИШКИ МИМИНУСА», and acting as My
/// Computer with its list of drives. The left pane switches its task group based
/// on what is selected, exactly as XP did.
///
/// Version 8 put a ribbon on it, and the ribbon stays on whatever the rest of
/// the system is wearing: tabs, groups, a big button and a row of small ones,
/// a chevron that rolls the whole thing up, and a breadcrumb address bar with a
/// search box. The theme still colours it — under Luna it is a Luna ribbon —
/// but the shape of the window is the newer one either way.</summary>
public sealed class ExplorerWindow : OsWindow
{
    VNode _folder;
    VNode _selected;

    /// <summary>The node being renamed in place, and the text being typed.
    /// The counter gives each rename a state of its own, so starting a second
    /// one never inherits the caret or the selection of the first.</summary>
    VNode _renaming;
    string _renameText = "";
    int _renameSeq;

    string RenameId => Id + ".rename#" + _renameSeq;

    /// <summary>Item pressed but not yet dragged, and where the press landed.
    /// A drag starts once the pointer has moved far enough to mean it.</summary>
    VNode _pressed;
    float _pressX, _pressY;


    readonly List<VNode> _history = new();
    int _historyIndex = -1;

    bool _showTaskPane = true;
    ViewMode _view = ViewMode.Icons;
    float _scroll;

    /// <summary>Which ribbon tab is open, and whether the ribbon is rolled up.
    /// Version 8 remembered the second of these between windows; this one does
    /// not, which is one fewer setting to explain.</summary>
    int _ribbonTab;
    bool _ribbonCollapsed;

    enum ViewMode { Icons, List, Details, Tiles }

    UiContext _ctx;

    public override string Title => _folder.Name;
    public override float MinWidth => 420;
    public override float MinHeight => 300;

    public ExplorerWindow(VNode folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
        Icon = _folder == null ? IconId.Folder : _folder.Icon;
        if (Icon == IconId.None) Icon = IconId.Folder;
        Bounds = new Rect(0, 0, 760, 520);
        PushHistory(_folder);
    }

    void PushHistory(VNode n)
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(n);
        _historyIndex = _history.Count - 1;
    }

    void Navigate(VNode target, UiContext c, bool record = true)
    {
        if (target == null || !target.IsContainer) return;
        _folder = target;
        _selected = null;
        _scroll = 0;
        CancelRename();
        Icon = target.Icon == IconId.None ? IconId.Folder : target.Icon;
        if (record) PushHistory(target);
        c.Sound(Sfx.Navigate, 0.5f);
    }

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("explorer.file"), () =>
        {
            var items = new List<MenuItem>();
            if (_selected != null)
            {
                items.Add(new MenuItem { Text = L.T("explorer.open"), Bold = true, Click = () => Open(_selected) });
                if (_selected.Kind is NodeKind.TextFile or NodeKind.ImageFile)
                    items.Add(MenuItem.Sub(L.T("explorer.open_with"),
                                           Shell.BuildOpenWithMenu(_ctx, _selected)));
                items.Add(MenuItem.Sep());
                items.Add(MenuItem.Of(L.T("explorer.delete"), () => DeleteSelected(_ctx)));
                items.Add(MenuItem.Of(L.T("explorer.rename"), () => BeginRename(_selected),
                                      enabled: VirtualFS.CanRename(_selected)));
                items.Add(MenuItem.Sep());
                items.Add(MenuItem.Of(L.T("explorer.properties"),
                    () => Shell.ShowProperties(_ctx, _selected.Name, _selected, _selected.Icon)));
                items.Add(MenuItem.Sep());
            }
            items.Add(MenuItem.Sub(L.T("explorer.new"), NewMenu()));
            items.Add(MenuItem.Sep());
            items.Add(MenuItem.Of(L.T("explorer.close"), () => Wm.RequestClose(this, _ctx)));
            return items;
        });

        Menu.Add(L.T("explorer.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("explorer.undo"), null, enabled: false, shortcut: "Ctrl+Z"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("explorer.cut"), null, enabled: _selected != null, shortcut: "Ctrl+X"),
            MenuItem.Of(L.T("explorer.copy"), null, enabled: _selected != null, shortcut: "Ctrl+C"),
            MenuItem.Of(L.T("explorer.paste"), null, enabled: false, shortcut: "Ctrl+V"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("explorer.select_all"), null, shortcut: "Ctrl+A"),
        });

        Menu.Add(L.T("explorer.view"), () => new List<MenuItem>
        {
            MenuItem.Check(L.T("ribbon.nav_pane"), _showTaskPane, () => _showTaskPane = !_showTaskPane),
            MenuItem.Sep(),
            new() { Text = L.T("explorer.tiles"), Checked = _view == ViewMode.Tiles, IsRadio = true, Click = () => _view = ViewMode.Tiles },
            new() { Text = L.T("explorer.icons"), Checked = _view == ViewMode.Icons, IsRadio = true, Click = () => _view = ViewMode.Icons },
            new() { Text = L.T("explorer.list"), Checked = _view == ViewMode.List, IsRadio = true, Click = () => _view = ViewMode.List },
            new() { Text = L.T("explorer.details"), Checked = _view == ViewMode.Details, IsRadio = true, Click = () => _view = ViewMode.Details },
            MenuItem.Sep(),
            MenuItem.Of(L.T("explorer.refresh"), () => Refresh(_ctx), shortcut: "F5"),
        });

        Menu.Add(L.T("explorer.f_avorites"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("explorer.revolutionary_distributions"),
                        () => Navigate(Shell.Fs.Revolutionary, _ctx), IconId.Folder),
            MenuItem.Of(L.T("explorer.useful_miminus_tricks"),
                        () => Navigate(Shell.Fs.UsefulTricks, _ctx), IconId.Folder),
            MenuItem.Of(L.T("explorer.my_documents"),
                        () => Navigate(Shell.Fs.MyDocuments, _ctx), IconId.MyDocuments),
        });

        Menu.Add(L.T("explorer.tools"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("explorer.map_network_drive"), () =>
                Shell.MessageBox(_ctx, L.T("explorer.network"),
                    L.T("explorer.network_checked_connect_failed"),
                    MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning)),
            MenuItem.Sep(),
            MenuItem.Of(L.T("explorer.folder_options"), () =>
                Shell.MessageBox(_ctx, L.T("explorer.folder_options_2"),
                    L.T("explorer.every_folder_in_miminus_os_is_already_config"),
                    MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info)),
        });

        Menu.Add(L.T("explorer.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("explorer.about"), () => Shell.Launch(_ctx, "about", null), IconId.DlgInfo),
        });
    }

    List<MenuItem> NewMenu() => new()
    {
        MenuItem.Of(L.T("explorer.folder"), () => Create(NodeKind.Folder, IconId.Folder), IconId.Folder),
        MenuItem.Of(L.T("explorer.shortcut"), () => Create(NodeKind.Shortcut, IconId.Program)),
        MenuItem.Sep(),
        MenuItem.Of(L.T("explorer.text_document"), () => Create(NodeKind.TextFile, IconId.TextFile), IconId.TextFile),
        MenuItem.Of(L.T("explorer.bitmap_image"), () => Create(NodeKind.ImageFile, IconId.ImageFile), IconId.ImageFile),
        MenuItem.Of(L.T("explorer.microsoft_excel_worksheet"), () => Create(NodeKind.Spreadsheet, IconId.Spreadsheet), IconId.Spreadsheet),
        MenuItem.Of(L.T("explorer.microsoft_word_document"), () => Create(NodeKind.Document, IconId.WordDoc), IconId.WordDoc),
        MenuItem.Of(L.T("explorer.winrar_archive"), () => Create(NodeKind.Archive, IconId.Archive), IconId.Archive),
    };

    void Create(NodeKind kind, IconId icon)
    {
        string name = kind switch
        {
            NodeKind.Folder => L.T("explorer.new_folder"),
            NodeKind.TextFile => L.T("explorer.new_text_document_txt"),
            NodeKind.ImageFile => L.T("explorer.new_bitmap_image_bmp"),
            NodeKind.Spreadsheet => L.T("explorer.new_microsoft_excel_worksheet_xls"),
            NodeKind.Document => L.T("explorer.new_microsoft_word_document_doc"),
            NodeKind.Archive => L.T("explorer.new_winrar_archive_rar"),
            _ => L.T("explorer.new_shortcut"),
        };
        _selected = Shell.Fs.CreateChild(_folder, name, kind, icon);
        _ctx.Sound(Sfx.Navigate, 0.5f);
    }

    // ---- frame -----------------------------------------------------------

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        var t = c.Theme;

        var area = client;

        // A window with a ribbon has no menu bar: that was the trade, and it
        // is made under every theme.
        Menu = null;
        DrawRibbonTabs(c, area.CutTop(26));
        if (!_ribbonCollapsed) DrawRibbon(c, area.CutTop(96));
        DrawBreadcrumbBar(c, area.CutTop(28));

        // Seven moved what XP had in the side pane down here, and put a tree
        // where the tasks used to be.
        DrawDetailsPane(c, area.CutBottom(54));

        Rect nav = default;
        if (_showTaskPane && area.W > 380)
            nav = area.CutLeft(198);

        DrawFileList(c, area);
        if (!nav.IsEmpty) DrawNavigationPane(c, nav);

        // Backspace goes up a level, like Explorer. The rename box claims the
        // keyboard while it is open, so none of this fires underneath it.
        if (!c.KeyboardHandled)
        {
            // Part 3: the folder called Windows is selected, Ctrl is pressed,
            // and it goes. No Delete, no confirmation — just Ctrl.
            if (c.In.KeyPressed(Keys.Control) && VirtualFS.IsWindowsFolder(_selected))
                DeleteWindowsFolder(c, _selected);
            else if (c.In.KeyPressed(Keys.F2) && _selected != null) BeginRename(_selected);
            else if (c.In.KeyPressed(Keys.Back)) GoUp(c);
            else if (c.In.KeyPressed(Keys.Delete) && _selected != null) DeleteSelected(c);
            else if (c.In.KeyPressed(Keys.Enter) && _selected != null) Open(_selected);
        }
    }


    // ---- лента (version 8) --------------------------------------------------

    static readonly string[] RibbonTabs =
    {
        "ribbon.file", "ribbon.home", "ribbon.share", "ribbon.view",
    };

    void DrawRibbonTabs(UiContext c, Rect bar)
    {
        var t = c.Theme;
        c.R.FillRect(bar, Color.Rgb(0xF5F5F5));
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Color.Rgb(0xD8D8D8));

        float x = bar.X;
        for (int i = 0; i < RibbonTabs.Length; i++)
        {
            string label = L.T(RibbonTabs[i]);
            var tab = new Rect(x, bar.Y, c.F.Ui.Measure(label) + 26, bar.H);
            bool file = i == 0;
            bool sel = !file && i == _ribbonTab && !_ribbonCollapsed;

            if (file) c.R.FillRect(tab, c.Theme.Accent);
            else if (sel) c.R.FillRect(tab, Color.White);
            else if (c.Hovering(tab)) c.R.FillRect(tab, Color.Rgb(0xE8F1FB));

            if (sel) c.R.FillRect(new Rect(tab.X, tab.Y, tab.W, 2), c.Theme.Accent);

            c.F.Ui.DrawCentered(c.R, label, tab, file ? Color.White : t.Text);

            if (c.Clicked(tab))
            {
                if (file) ShowFileMenu(c, tab);
                else if (i == _ribbonTab) _ribbonCollapsed = !_ribbonCollapsed;
                else { _ribbonTab = i; _ribbonCollapsed = false; }
                c.SoundAt(Sfx.Click, tab, 0.4f);
            }
            x = tab.Right;
        }

        // The chevron that rolls the ribbon up, at the far right.
        var chevron = new Rect(bar.Right - 22, bar.Y + 4, 18, bar.H - 8);
        if (c.Hovering(chevron)) c.R.FillRect(chevron, Color.Rgb(0xE8F1FB));
        W.Arrow(c, chevron, _ribbonCollapsed ? 2 : 0, t.Text);
        c.Tooltip(chevron, L.T(_ribbonCollapsed ? "ribbon.expand" : "ribbon.collapse"));
        if (c.Clicked(chevron)) _ribbonCollapsed = !_ribbonCollapsed;
    }

    void ShowFileMenu(UiContext c, Rect anchor)
    {
        Shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("ribbon.open_new_window"),
                        () => Shell.Launch(c, "explorer", _folder), IconId.FolderOpen),
            MenuItem.Of(L.T("ribbon.open_command_prompt"),
                        () => Shell.Launch(c, "terminal", null), IconId.Terminal),
            MenuItem.Sep(),
            MenuItem.Of(L.T("explorer.close"), Close),
        }, anchor.X, anchor.Bottom, this, c);
    }

    void DrawRibbon(UiContext c, Rect ribbon)
    {
        c.R.FillRect(ribbon, Color.White);
        c.R.FillRect(new Rect(ribbon.X, ribbon.Bottom - 1, ribbon.W, 1), Color.Rgb(0xD8D8D8));

        var area = ribbon.Deflate(6, 4, 6, 18);
        switch (_ribbonTab)
        {
            case 2: DrawShareTab(c, ribbon, area); break;
            case 3: DrawViewTab(c, ribbon, area); break;
            default: DrawHomeTab(c, ribbon, area); break;
        }
    }

    /// <summary>«Главная»: the clipboard, then what can be done to a file.</summary>
    void DrawHomeTab(UiContext c, Rect ribbon, Rect area)
    {
        bool has = _selected != null;

        var group = RibbonGroup(c, ribbon, ref area, 156, "ribbon.group_clipboard");
        if (RibbonBig(c, group.CutLeft(78), IconId.Archive, "ribbon.copy", has))
            c.Sound(Sfx.Click, 0.5f);
        if (RibbonBig(c, group.CutLeft(78), IconId.FolderOpen, "ribbon.paste", false))
            c.Sound(Sfx.Click, 0.5f);

        group = RibbonGroup(c, ribbon, ref area, 250, "ribbon.group_organise");
        if (RibbonBig(c, group.CutLeft(78), IconId.RecycleBin, "ribbon.delete", has))
            DeleteSelected(c);
        if (RibbonBig(c, group.CutLeft(90), IconId.TextFile, "ribbon.rename", has))
            BeginRename(_selected);
        if (RibbonBig(c, group.CutLeft(82), IconId.Folder, "ribbon.new_folder", true))
            Create(NodeKind.Folder, IconId.Folder);

        group = RibbonGroup(c, ribbon, ref area, 164, "ribbon.group_open");
        if (RibbonBig(c, group.CutLeft(78), IconId.Program, "ribbon.open", has))
            Open(_selected);
        if (RibbonBig(c, group.CutLeft(86), IconId.Settings, "ribbon.properties", has))
            Shell.ShowProperties(c, _selected.Name, _selected, _selected.Icon);
    }

    /// <summary>«Поделиться»: nothing leaves this machine, and it says so.</summary>
    void DrawShareTab(UiContext c, Rect ribbon, Rect area)
    {
        bool has = _selected != null;

        var group = RibbonGroup(c, ribbon, ref area, 160, "ribbon.group_send");
        if (RibbonBig(c, group.CutLeft(78), IconId.Mail, "ribbon.email", has))
            Shell.Launch(c, "mail", null);
        if (RibbonBig(c, group.CutLeft(78), IconId.Archive, "ribbon.zip", has))
            Shell.MessageBox(c, _folder.Name, L.T("ribbon.zip_note"),
                             MsgButtons.Ok, IconId.Archive, null, Sfx.Info);

        group = RibbonGroup(c, ribbon, ref area, 160, "ribbon.group_share_with");
        if (RibbonBig(c, group.CutLeft(78), IconId.Network, "ribbon.network", has))
            Shell.Launch(c, "network", null);
        if (RibbonBig(c, group.CutLeft(78), IconId.Printer, "ribbon.print", has))
            Shell.Launch(c, "printers", null);
    }

    /// <summary>«Вид»: the four view modes and the pane switch, which is what
    /// the toolbar used to hide behind one button.</summary>
    void DrawViewTab(UiContext c, Rect ribbon, Rect area)
    {
        var group = RibbonGroup(c, ribbon, ref area, 96, "ribbon.group_panes");
        if (RibbonBig(c, group.CutLeft(94), IconId.Folder, "ribbon.nav_pane", true, _showTaskPane))
            _showTaskPane = !_showTaskPane;

        group = RibbonGroup(c, ribbon, ref area, 300, "ribbon.group_layout");
        (ViewMode mode, string key, IconId icon)[] modes =
        {
            (ViewMode.Tiles, "explorer.tiles", IconId.ImageFile),
            (ViewMode.Icons, "explorer.icons", IconId.Folder),
            (ViewMode.List, "explorer.list", IconId.TextFile),
            (ViewMode.Details, "explorer.details", IconId.Spreadsheet),
        };
        foreach (var (mode, key, icon) in modes)
            if (RibbonBig(c, group.CutLeft(74), icon, key, true, _view == mode))
                _view = mode;

        group = RibbonGroup(c, ribbon, ref area, 88, "ribbon.group_refresh");
        if (RibbonBig(c, group.CutLeft(86), IconId.Globe, "explorer.refresh", true))
            Refresh(c);
    }

    /// <summary>Cuts one titled group out of the ribbon and draws the divider
    /// after it. Returns the room the buttons have.</summary>
    Rect RibbonGroup(UiContext c, Rect ribbon, ref Rect area, float width, string titleKey)
    {
        var group = area.CutLeft(MathF.Min(width, MathF.Max(0, area.W)));

        // The group name sits under the buttons, in grey, as it did.
        c.F.Small.DrawCentered(c.R, L.T(titleKey),
                               new Rect(group.X, ribbon.Bottom - 17, group.W, 14),
                               c.Theme.TextDisabled);

        // The hairline that separates one group from the next.
        c.R.FillRect(new Rect(group.Right + 1, ribbon.Y + 6, 1, ribbon.H - 14),
                     Color.Rgb(0xE4E4E4));
        area.CutLeft(4);
        return group;
    }

    /// <summary>A ribbon button: picture on top, caption under it.</summary>
    bool RibbonBig(UiContext c, Rect r, IconId icon, string key, bool enabled, bool active = false)
    {
        bool hover = enabled && c.Hovering(r);
        bool clicked = enabled && c.Clicked(r);

        if (active) c.R.FillRect(r, Color.Rgb(0xDCEBFA));
        if (hover) c.R.FillRect(r, Color.Rgb(0xE8F1FB));
        if (hover || active) c.R.DrawRect(r, c.Theme.Accent.WithAlpha((byte)140));

        var ic = new Rect(r.CenterX - 16, r.Y + 6, 32, 32);
        Icons.Draw(c.R, icon, ic);
        if (!enabled) c.R.FillRect(ic, Color.Rgba(0xFFFFFF, 150));

        c.R.PushClip(r);
        string label = L.T(key);
        foreach (string line in c.F.Small.Wrap(label, r.W - 4).Take(2))
        {
            float w = c.F.Small.Measure(line);
            c.F.Small.Draw(c.R, line, r.CenterX - w * 0.5f, ic.Bottom + 4,
                           enabled ? c.Theme.Text : c.Theme.TextDisabled);
            ic = new Rect(ic.X, ic.Y + c.F.Small.Height, ic.W, ic.H);
        }
        c.R.PopClip();

        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);
        return clicked;
    }


    /// <summary>The address bar version 8 used: the path as a row of buttons
    /// with chevrons between them, each one a place you can go back to, and a
    /// search box on the right that has nothing to look through.</summary>
    void DrawBreadcrumbBar(UiContext c, Rect bar)
    {
        var t = c.Theme;
        c.R.FillRect(bar, Color.Rgb(0xF5F5F5));
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Color.Rgb(0xD8D8D8));

        // Back, forward and up, in the corner they moved to.
        float x = bar.X + 4;
        bool canBack = _historyIndex > 0;
        bool canFwd = _historyIndex < _history.Count - 1;

        if (NavButton(c, ref x, bar, 3, canBack))
        {
            _historyIndex--;
            Navigate(_history[_historyIndex], c, record: false);
        }
        if (NavButton(c, ref x, bar, 1, canFwd))
        {
            _historyIndex++;
            Navigate(_history[_historyIndex], c, record: false);
        }
        if (NavButton(c, ref x, bar, 0, _folder.Parent != null)) GoUp(c);

        // The path, as a chain of buttons from the root down.
        var chain = new List<VNode>();
        for (var n = _folder; n != null; n = n.Parent) chain.Insert(0, n);

        var field = new Rect(x + 4, bar.Y + 3, bar.W - (x - bar.X) - 190, bar.H - 7);
        c.R.FillRect(field, Color.White);
        c.R.DrawRect(field, Color.Rgb(0xC8C8C8));

        c.R.PushClip(field);
        Icons.Draw(c.R, Icon, new Rect(field.X + 3, field.CenterY - 8, 16, 16));

        float cx = field.X + 23;
        foreach (var node in chain)
        {
            string name = node.Name;
            float w = c.F.Ui.Measure(name) + 10;
            var crumb = new Rect(cx, field.Y + 1, w, field.H - 2);
            if (crumb.Right > field.Right - 6) break;

            bool hot = c.Hovering(crumb);
            if (hot) c.R.FillRect(crumb, Color.Rgb(0xE8F1FB));
            c.F.Ui.DrawCentered(c.R, name, crumb, t.Text);
            if (c.Clicked(crumb) && node != _folder) Navigate(node, c);

            cx = crumb.Right;
            var sep = new Rect(cx, field.Y, 14, field.H);
            W.Arrow(c, sep, 1, t.TextDisabled, 3f);
            if (c.Clicked(sep)) ShowPlacesMenu(c, sep);
            cx = sep.Right;
        }
        c.R.PopClip();

        // The search box, which is honest about what it can do.
        var search = new Rect(field.Right + 6, field.Y, bar.Right - field.Right - 12, field.H);
        c.R.FillRect(search, Color.White);
        c.R.DrawRect(search, Color.Rgb(0xC8C8C8));
        c.R.PushClip(search);
        c.F.Ui.Draw(c.R, L.F("explorer.search_in", _folder.Name), search.X + 6,
                    search.CenterY - c.F.Ui.Height * 0.5f, t.TextDisabled);
        c.R.PopClip();
        Icons.Draw(c.R, IconId.Search, new Rect(search.Right - 20, search.CenterY - 8, 16, 16));
        if (c.Clicked(search)) Shell.Launch(c, "search", null);
    }

    /// <summary>One of the three round navigation buttons.</summary>
    bool NavButton(UiContext c, ref float x, Rect bar, int direction, bool enabled)
    {
        var r = new Rect(x, bar.CenterY - 11, 22, 22);
        bool hover = enabled && c.Hovering(r);
        bool clicked = enabled && c.Clicked(r);

        if (hover) c.R.FillCircle(r.CenterX, r.CenterY, 11, Color.Rgb(0xE8F1FB));
        W.Arrow(c, r, direction, enabled ? c.Theme.Accent : c.Theme.TextDisabled, 4.5f);

        x += 24;
        return clicked;
    }

    void ShowPlacesMenu(UiContext c, Rect anchor)
    {
        var fs = Shell.Fs;
        var items = new List<MenuItem>
        {
            MenuItem.Of(fs.MyComputer.Name, () => Navigate(fs.MyComputer, c), IconId.MyComputer),
            MenuItem.Of(fs.Desktop.Name, () => Navigate(fs.Desktop, c), IconId.Folder),
            MenuItem.Of(fs.MyDocuments.Name, () => Navigate(fs.MyDocuments, c), IconId.MyDocuments),
            MenuItem.Of(fs.MyPictures.Name, () => Navigate(fs.MyPictures, c), IconId.MyPictures),
            MenuItem.Of(fs.MyMusic.Name, () => Navigate(fs.MyMusic, c), IconId.MyMusic),
            MenuItem.Sep(),
            MenuItem.Of(fs.Revolutionary.Name, () => Navigate(fs.Revolutionary, c), IconId.Folder),
            MenuItem.Of(fs.UsefulTricks.Name, () => Navigate(fs.UsefulTricks, c), IconId.Folder),
        };
        Shell.Menus.Open(items, anchor.X - 180, anchor.Bottom, this, c);
    }

    /// <summary>Re-reads the current folder. On a mount this rescans the host
    /// directory, so files added outside the OS show up.</summary>
    void Refresh(UiContext c)
    {
        if (_folder.IsHosted)
        {
            _folder.HostLoaded = false;
            _selected = null;
        }
        c.Sound(Sfx.Navigate, 0.4f);
    }

    void GoUp(UiContext c)
    {
        if (_folder.Parent != null) Navigate(_folder.Parent, c);
    }

    // ---- task pane -------------------------------------------------------


    // ---- область навигации (the seven tree) --------------------------------

    /// <summary>Which of the four groups are open. Seven opened all of them and
    /// so does this, but the triangles work.</summary>
    readonly bool[] _expanded = { true, true, true, true };

    /// <summary>How far the tree has been scrolled, when it is taller than the
    /// window it is in.</summary>
    float _navScroll;

    /// <summary>The navigation pane seven replaced the task list with: four
    /// groups — favourites, libraries, the computer and the network — each a
    /// heading with a triangle and a short list under it.
    ///
    /// The entries are the real nodes of the filesystem, so a mounted host
    /// folder appears under «Компьютер» beside the invented drives and can be
    /// reached from here like anything else.</summary>
    void DrawNavigationPane(UiContext c, Rect pane)
    {
        c.R.FillRect(pane, Color.Rgb(0xF7F7F7));
        c.R.FillRect(new Rect(pane.Right - 1, pane.Y, 1, pane.H), Color.Rgb(0xE0E0E0));

        var fs = Shell.Fs;

        // The four groups, and what is in each of them.
        (string key, IconId icon, (string label, IconId icon, VNode node, string app)[] items)[] groups =
        {
            ("nav.favourites", IconId.Star, new[]
            {
                (fs.Desktop.Name, IconId.Folder, fs.Desktop, (string)null),
                (fs.Revolutionary.Name, IconId.Folder, fs.Revolutionary, null),
                (fs.UsefulTricks.Name, IconId.Folder, fs.UsefulTricks, null),
            }),
            ("nav.libraries", IconId.MyDocuments, new[]
            {
                (fs.MyDocuments.Name, IconId.MyDocuments, fs.MyDocuments, (string)null),
                (fs.MyPictures.Name, IconId.MyPictures, fs.MyPictures, null),
                (fs.MyMusic.Name, IconId.MyMusic, fs.MyMusic, null),
            }),
            ("nav.computer", IconId.MyComputer, Drives(fs)),
            ("nav.network", IconId.Network, new[]
            {
                (L.T("explorer.my_network_places"), IconId.Network, (VNode)null, "network"),
            }),
        };

        var area = pane.Deflate(6, 8, 4, 6);

        // The whole tree is drawn into a clip and scrolled with the wheel: at
        // this width it is taller than the pane on any small window.
        float contentH = 0;
        foreach (var (_, _, items) in groups) contentH += 26 + (items.Length * 22) + 6;

        if (c.Hovering(area) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _navScroll = Math.Clamp(_navScroll - c.In.WheelDelta * 40, 0,
                                    MathF.Max(0, contentH - area.H));
            c.In.WheelDelta = 0;
        }
        if (contentH <= area.H) _navScroll = 0;

        c.R.PushClip(area);
        float y = area.Y - _navScroll;

        for (int g = 0; g < groups.Length; g++)
        {
            var (key, icon, items) = groups[g];

            var head = new Rect(area.X, y, area.W, 24);
            bool headHot = c.Hovering(head);
            if (headHot) c.R.FillRect(head, Color.Rgb(0xE8F1FB));

            // The triangle: pointing down when the group is open.
            var tri = new Rect(head.X + 2, head.Y, 14, head.H);
            W.Arrow(c, tri, _expanded[g] ? 2 : 1,
                    headHot ? Color.Rgb(0x1E4E79) : Color.Rgb(0x8A8A8A), 3.6f);

            Icons.Draw(c.R, icon, new Rect(head.X + 18, head.CenterY - 8, 16, 16));
            c.F.UiBold.Draw(c.R, L.T(key), head.X + 38, head.CenterY - c.F.UiBold.Height * 0.5f,
                            Color.Rgb(0x1E4E79));

            if (c.Clicked(head)) { _expanded[g] = !_expanded[g]; c.SoundAt(Sfx.Click, head, 0.35f); }
            y = head.Bottom + 2;

            if (!_expanded[g]) { y += 4; continue; }

            foreach (var (label, itemIcon, node, app) in items)
            {
                var row = new Rect(area.X + 18, y, area.W - 18, 22);
                bool current = node != null && node == _folder;
                bool hot = c.Hovering(row);

                if (current) c.R.FillRect(row, Color.Rgb(0xD8E8F8));
                else if (hot) c.R.FillRect(row, Color.Rgb(0xE8F1FB));
                if (current) c.R.DrawRect(row, Color.Rgb(0xB8D0E8));

                Icons.Draw(c.R, itemIcon, new Rect(row.X + 6, row.CenterY - 8, 16, 16));

                c.R.PushClip(row);
                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(label, row.W - 32), row.X + 26,
                            row.CenterY - c.F.Ui.Height * 0.5f, Color.Rgb(0x1A1A1A));
                c.R.PopClip();

                if (c.Clicked(row))
                {
                    if (node != null) Navigate(node, c);
                    else if (app != null) Shell.Launch(c, app, null);
                }
                y = row.Bottom;
            }
            y += 6;
        }
        c.R.PopClip();
    }

    /// <summary>What «Компьютер» holds: the drives, and any host folder that
    /// was mounted onto the machine at startup.</summary>
    static (string, IconId, VNode, string)[] Drives(VirtualFS fs)
        => fs.MyComputer.Entries
             .Where(n => n.IsContainer)
             .Take(6)
             .Select(n => (n.Name, n.Icon == IconId.None ? IconId.DriveHdd : n.Icon, n, (string)null))
             .ToArray();

    /// <summary>The strip along the foot: seven's details pane. The icon of
    /// whatever is selected, its name in bold, and two lines of facts about it —
    /// or the folder itself when nothing is selected.</summary>
    void DrawDetailsPane(UiContext c, Rect r)
    {
        c.R.FillRect(r, Color.Rgb(0xF0F0F0));
        c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Color.Rgb(0xE0E0E0));

        var node = _selected ?? _folder;
        var icon = new Rect(r.X + 10, r.CenterY - 16, 32, 32);
        DrawNodeIcon(c, node, icon);

        c.R.PushClip(r);
        c.F.UiBold.Draw(c.R, c.F.UiBold.Ellipsize(node.Name, r.W * 0.4f), icon.Right + 10,
                        r.Y + 8, Color.Rgb(0x1A1A1A));
        c.F.Small.Draw(c.R, node.TypeName, icon.Right + 11, r.Y + 10 + c.F.UiBold.Height,
                       Color.Rgb(0x707070));

        // A second column of facts, as seven laid them out.
        float x = icon.Right + 10 + r.W * 0.42f;
        if (_selected != null)
        {
            Fact(c, x, r.Y + 8, "explorer.date_modified_label", L.ShortDate(_selected.Modified));
            if (!_selected.IsContainer)
                Fact(c, x, r.Y + 10 + c.F.UiBold.Height, "explorer.size_label",
                     L.FileSize(_selected.Size));
        }
        else
        {
            Fact(c, x, r.Y + 8, "explorer.items_label", _folder.Entries.Count.ToString());
            Fact(c, x, r.Y + 10 + c.F.UiBold.Height, "explorer.location_label",
                 _folder.Parent?.Name ?? L.T("explorer.my_computer"));
        }
        c.R.PopClip();
    }

    static void Fact(UiContext c, float x, float y, string key, string value)
    {
        string label = L.T(key);
        c.F.Small.Draw(c.R, label, x, y + 1, Color.Rgb(0x707070));
        c.F.Small.Draw(c.R, value, x + c.F.Small.Measure(label) + 6, y + 1, Color.Rgb(0x1A1A1A));
    }

    // ---- file list -------------------------------------------------------

    void DrawFileList(UiContext c, Rect area)
    {
        var t = c.Theme;
        c.R.FillRect(area, t.FieldBack);
        c.R.DrawRect(area, t.ControlBorder);

        var view = area.Deflate(1);
        var items = _folder.Entries;

        float cellW = _view switch
        {
            ViewMode.Tiles => 190,
            ViewMode.List => 170,
            ViewMode.Details => view.W,
            _ => 92,
        };
        float cellH = _view switch
        {
            ViewMode.Tiles => 52,
            ViewMode.List => 18,
            ViewMode.Details => 18,
            _ => 68,
        };

        int perRow = Math.Max(1, (int)(view.W / cellW));
        if (_view is ViewMode.List or ViewMode.Details) perRow = 1;

        int rows = (int)MathF.Ceiling(items.Count / (float)perRow);
        float contentH = rows * cellH + 8;
        bool needScroll = contentH > view.H;
        if (needScroll)
        {
            view.W -= W.ScrollBarSize;
            perRow = Math.Max(1, (int)(view.W / cellW));
            if (_view is ViewMode.List or ViewMode.Details) perRow = 1;
            rows = (int)MathF.Ceiling(items.Count / (float)perRow);
            contentH = rows * cellH + 8;
        }

        if (c.Hovering(view) && c.In.WheelDelta != 0)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * cellH, 0, MathF.Max(0, contentH - view.H));
            c.MouseHandled = true;
        }

        c.R.PushClip(view);

        // Details view gets a column header.
        float top = view.Y + 4;
        if (_view == ViewMode.Details)
        {
            var head = new Rect(view.X, view.Y, view.W, 18);
            c.R.FillRectV(head, Color.White, t.Face);
            c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), t.ControlBorder);
            float[] cols = { 0.45f, 0.18f, 0.22f, 0.15f };
            string[] names =
            {
                L.T("explorer.name"), L.T("explorer.size"),
                L.T("explorer.type"), L.T("explorer.date_modified"),
            };
            float hx = head.X;
            for (int i = 0; i < names.Length; i++)
            {
                float w = head.W * cols[i];
                c.F.Ui.Draw(c.R, names[i], hx + 5, head.CenterY - c.F.Ui.Height * 0.5f, t.Text);
                c.R.FillRect(new Rect(hx + w - 1, head.Y + 2, 1, head.H - 4), t.ControlBorder);
                hx += w;
            }
            top = head.Bottom + 1;
        }

        for (int i = 0; i < items.Count; i++)
        {
            var node = items[i];
            int col = i % perRow, row = i / perRow;
            var cell = new Rect(view.X + col * cellW, top + row * cellH - _scroll, cellW, cellH);
            if (cell.Bottom < view.Y || cell.Y > view.Bottom) continue;
            DrawItem(c, node, cell);
        }

        c.R.PopClip();

        if (needScroll)
            _scroll = W.ScrollBarV(c, Id + ".sb", new Rect(view.Right, view.Y, W.ScrollBarSize, view.H),
                                   _scroll, contentH, view.H);
        else _scroll = 0;

        // The view is the fallback drop target: anything let go over it that a
        // folder item did not claim lands in the folder being shown.
        if (Shell.Drag.Offer(c, view, _folder, this))
            c.R.DrawRect(view.Deflate(1), c.Theme.Selection);

        BeginDragIfMoved(c);

        // Empty space: clear selection, or open the folder context menu.
        if (c.Clicked(area)) _selected = null;
        else if (c.RightClicked(area))
        {
            _selected = null;
            Shell.Menus.Open(new List<MenuItem>
            {
                MenuItem.Sub(L.T("explorer.view_2"), new List<MenuItem>
                {
                    new() { Text = L.T("explorer.tiles"), IsRadio = true, Checked = _view == ViewMode.Tiles, Click = () => _view = ViewMode.Tiles },
                    new() { Text = L.T("explorer.icons"), IsRadio = true, Checked = _view == ViewMode.Icons, Click = () => _view = ViewMode.Icons },
                    new() { Text = L.T("explorer.list"), IsRadio = true, Checked = _view == ViewMode.List, Click = () => _view = ViewMode.List },
                    new() { Text = L.T("explorer.details"), IsRadio = true, Checked = _view == ViewMode.Details, Click = () => _view = ViewMode.Details },
                }),
                MenuItem.Of(L.T("explorer.refresh"), () => Refresh(c)),
                MenuItem.Sep(),
                MenuItem.Sub(L.T("explorer.new"), NewMenu()),
                MenuItem.Sep(),
                MenuItem.Of(L.T("explorer.properties"),
                    () => Shell.ShowProperties(c, _folder.Name, _folder, _folder.Icon)),
            }, c.MouseX, c.MouseY, this, c);
        }
    }

    void DrawItem(UiContext c, VNode node, Rect cell)
    {
        var t = c.Theme;
        bool selected = node == _selected;
        bool hover = c.Hovering(cell);

        if (node == _renaming) { DrawRenameBox(c, node, cell); return; }

        switch (_view)
        {
            case ViewMode.Icons:
            {
                var iconRect = new Rect(cell.X + (cell.W - 32) * 0.5f, cell.Y + 4, 32, 32);
                var labelArea = new Rect(cell.X + 2, iconRect.Bottom + 3, cell.W - 4, cell.H - 40);

                var lines = c.F.Small.Wrap(node.Name, labelArea.W - 4);
                if (lines.Count > 2) { lines = lines.Take(2).ToList(); lines[1] = c.F.Small.Ellipsize(lines[1] + "…", labelArea.W - 4); }
                float lineH = c.F.Small.Height + 1;

                if (selected || hover)
                {
                    Color wash = selected ? t.Selection : t.Hot;
                    c.R.FillRect(iconRect.Inflate(2), wash.WithAlpha((byte)(selected ? 110 : 70)));
                    for (int i = 0; i < lines.Count; i++)
                    {
                        float lw = c.F.Small.Measure(lines[i]);
                        c.R.FillRect(new Rect(labelArea.CenterX - lw * 0.5f - 2, labelArea.Y + i * lineH, lw + 4, lineH),
                                     selected ? t.Selection : t.Hot.WithAlpha((byte)90));
                    }
                }

                DrawNodeIcon(c, node, iconRect);
                for (int i = 0; i < lines.Count; i++)
                {
                    float lw = c.F.Small.Measure(lines[i]);
                    c.F.Small.Draw(c.R, lines[i], labelArea.CenterX - lw * 0.5f, labelArea.Y + i * lineH,
                                   selected ? t.SelectionText : t.Text);
                }
                break;
            }

            case ViewMode.Tiles:
            {
                if (selected) c.R.FillRect(cell.Deflate(2), t.Selection);
                else if (hover) c.R.FillRect(cell.Deflate(2), t.Hot.WithAlpha((byte)80));

                var iconRect = new Rect(cell.X + 6, cell.CenterY - 16, 32, 32);
                DrawNodeIcon(c, node, iconRect);
                Color fg = selected ? t.SelectionText : t.Text;
                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(node.Name, cell.W - 48), iconRect.Right + 6, cell.Y + 8, fg);
                c.F.Small.Draw(c.R, node.TypeName, iconRect.Right + 6, cell.Y + 8 + c.F.Ui.Height + 1,
                               selected ? t.SelectionText : t.TextDisabled);
                if (!node.IsContainer)
                    c.F.Small.Draw(c.R, L.FileSize(node.Size), iconRect.Right + 6,
                                   cell.Y + 8 + c.F.Ui.Height + c.F.Small.Height + 2,
                                   selected ? t.SelectionText : t.TextDisabled);
                break;
            }

            case ViewMode.List:
            {
                if (selected) c.R.FillRect(cell, t.Selection);
                else if (hover) c.R.FillRect(cell, t.Hot.WithAlpha((byte)80));
                DrawNodeIcon(c, node, new Rect(cell.X + 3, cell.Y + 1, 16, 16));
                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(node.Name, cell.W - 26), cell.X + 22,
                            cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, selected ? t.SelectionText : t.Text);
                break;
            }

            default: // Details
            {
                if (selected) c.R.FillRect(cell, t.Selection);
                else if (hover) c.R.FillRect(cell, t.Hot.WithAlpha((byte)80));

                Color fg = selected ? t.SelectionText : t.Text;
                float[] cols = { 0.45f, 0.18f, 0.22f, 0.15f };
                float x = cell.X;

                DrawNodeIcon(c, node, new Rect(x + 3, cell.Y + 1, 16, 16));
                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(node.Name, cell.W * cols[0] - 26), x + 22,
                            cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, fg);
                x += cell.W * cols[0];

                c.F.Ui.Draw(c.R, node.IsContainer ? "" : L.FileSize(node.Size), x + 5,
                            cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, fg);
                x += cell.W * cols[1];

                c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(node.TypeName, cell.W * cols[2] - 8), x + 5,
                            cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, fg);
                x += cell.W * cols[2];

                c.F.Ui.Draw(c.R, $"{L.ShortDate(node.Modified)} {node.Modified:HH:mm}", x + 5,
                            cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, fg);
                break;
            }
        }

        if (!string.IsNullOrEmpty(node.Tooltip)) c.Tooltip(cell, node.Tooltip);

        if (c.DoubleClicked(cell)) { _selected = node; Open(node); _pressed = null; }
        else if (c.Clicked(cell))
        {
            _selected = node;
            _pressed = node;
            _pressX = c.MouseX;
            _pressY = c.MouseY;
            c.SoundAt(Sfx.Tick, cell, 0.25f);
        }
        else if (c.RightClicked(cell)) { _selected = node; ShowItemMenu(c, node); }

        // A folder under the pointer takes the drop instead of the view.
        if (node.IsContainer && Shell.Drag.Offer(c, cell, node, node))
            c.R.DrawRect(cell.Deflate(1), t.Selection);
    }

    static void DrawNodeIcon(UiContext c, VNode node, Rect rect)
    {
        Icons.Draw(c.R, node.Icon == IconId.None ? IconId.UnknownFile : node.Icon, rect);
        if (node.Kind == NodeKind.Shortcut) Icons.DrawShortcutOverlay(c.R, rect);
    }

    void ShowItemMenu(UiContext c, VNode node)
    {
        var items = new List<MenuItem>
        {
            new() { Text = L.T("explorer.open"), Bold = true, Click = () => Open(node) },
        };
        if (node.Kind == NodeKind.ImageFile)
            items.Add(MenuItem.Of(L.T("explorer.edit_2"), () => Shell.Launch(c, "paint", node), IconId.Paint));
        if (node.Kind is NodeKind.TextFile or NodeKind.ImageFile or NodeKind.Unknown)
            items.Add(MenuItem.Sub(L.T("explorer.open_with"), Shell.BuildOpenWithMenu(c, node)));

        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("explorer.scan_for_viruses"), () =>
            Shell.Launch(c, "notepad", Shell.Fs.AntivirusFile), IconId.Antivirus));
        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("explorer.cut"), null, enabled: false));
        items.Add(MenuItem.Of(L.T("explorer.copy"), null, enabled: false));
        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("explorer.delete"), () => DeleteSelected(c)));
        items.Add(MenuItem.Of(L.T("explorer.rename"), () => BeginRename(node),
                              enabled: VirtualFS.CanRename(node)));
        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("explorer.properties"), () => Shell.ShowProperties(c, node.Name, node, node.Icon)));

        Shell.Menus.Open(items, c.MouseX, c.MouseY, this, c);
    }

    void Open(VNode node)
    {
        if (node.IsContainer) { Navigate(node, _ctx); return; }
        Shell.Launch(_ctx, node.Launch, node);
    }

    /// <summary>Turns a press that has travelled far enough into a drag. The
    /// threshold keeps an ordinary click from picking the file up.</summary>
    void BeginDragIfMoved(UiContext c)
    {
        if (_pressed == null) return;

        if (!c.In.IsDown(MouseButton.Left)) { _pressed = null; return; }
        if (Shell.Drag.Dragging) return;
        if (MathF.Abs(c.MouseX - _pressX) < 5 && MathF.Abs(c.MouseY - _pressY) < 5) return;

        Shell.Drag.Begin(_pressed, _pressed.Icon, _pressed.Name, this, _pressed.Launch);
        _pressed = null;
    }

    // ---- renaming in place ------------------------------------------------

    void BeginRename(VNode node)
    {
        if (!VirtualFS.CanRename(node)) return;
        _selected = node;
        _renaming = node;
        _renameText = node.Name;
        _renameSeq++;
    }

    /// <summary>How much of a name the box opens with selected: everything for
    /// a folder, and only the part before the extension for a file — which is
    /// what is nearly always being changed.</summary>
    static int SelectionFor(VNode node)
    {
        if (node.Kind == NodeKind.Folder) return -1;

        int dot = node.Name.LastIndexOf('.');
        return dot > 0 ? dot : -1;
    }

    /// <summary>Draws the edit box where the item's name would be, sized to the
    /// view so the name stays where the eye left it.</summary>
    void DrawRenameBox(UiContext c, VNode node, Rect cell)
    {
        Rect box;
        switch (_view)
        {
            case ViewMode.Icons:
            {
                var iconRect = new Rect(cell.X + (cell.W - 32) * 0.5f, cell.Y + 4, 32, 32);
                DrawNodeIcon(c, node, iconRect);
                box = new Rect(cell.X + 2, iconRect.Bottom + 2, cell.W - 4, c.F.Ui.Height + 6);
                break;
            }
            case ViewMode.Tiles:
            {
                var iconRect = new Rect(cell.X + 6, cell.CenterY - 16, 32, 32);
                DrawNodeIcon(c, node, iconRect);
                box = new Rect(iconRect.Right + 6, cell.Y + 6, cell.W - 52, c.F.Ui.Height + 6);
                break;
            }
            case ViewMode.List:
            {
                DrawNodeIcon(c, node, new Rect(cell.X + 3, cell.Y + 1, 16, 16));
                box = new Rect(cell.X + 21, cell.Y, cell.W - 24, cell.H);
                break;
            }
            default:
            {
                DrawNodeIcon(c, node, new Rect(cell.X + 3, cell.Y + 1, 16, 16));
                box = new Rect(cell.X + 21, cell.Y, cell.W * 0.45f - 24, cell.H);
                break;
            }
        }

        switch (W.RenameBox(c, RenameId, box, ref _renameText, SelectionFor(node)))
        {
            case W.RenameResult.Commit: CommitRename(c); break;
            case W.RenameResult.Cancel: _renaming = null; break;
        }
    }

    /// <summary>Walking away abandons the rename, rather than leaving a box
    /// attached to a node that is no longer on screen.</summary>
    void CancelRename() => _renaming = null;

    void CommitRename(UiContext c)
    {
        var node = _renaming;
        _renaming = null;
        if (node == null) return;

        if (Shell.Fs.Rename(node, _renameText, out string error))
        {
            c.Sound(Sfx.Tick, 0.5f);
            return;
        }

        Shell.MessageBox(c, L.T("explorer.rename_failed_title"), error,
                         MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
    }

    /// <summary>Removes the Windows folder outright and says what everybody in
    /// the video is waiting to hear.</summary>
    void DeleteWindowsFolder(UiContext c, VNode folder)
    {
        Shell.Fs.Delete(folder, permanent: true);
        if (_selected == folder) _selected = null;
        Shell.Audio.Play(Sfx.Trash, 0.8f);

        Shell.MessageBox(c, L.T("shell.miminus_os"), L.T("fs.windows_deleted"),
                         MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }

    void DeleteSelected(UiContext c)
    {
        if (_selected == null) return;
        var victim = _selected;

        // Ctrl removes outright rather than to the Recycle Bin, and it is the
        // only thing that will shift a folder called Windows. Read now: the
        // confirmation is answered long after the key has been let go.
        bool force = c.In.Ctrl;

        if (VirtualFS.IsProtected(victim))
        {
            Shell.MessageBox(c, victim.Name, L.T("fs.protected_folder"),
                MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
            return;
        }

        if (victim.IsHosted)
        {
            Shell.MessageBox(c, L.T("mount.title"), L.T("mount.delete_refused"),
                MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
            return;
        }

        if (VirtualFS.NeedsForce(victim) && !force)
        {
            Shell.MessageBox(c, victim.Name, L.T("fs.hold_ctrl_to_delete"),
                MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
            return;
        }

        Shell.MessageBox(c,
            force ? L.T("explorer.confirm_folder_delete") : L.T("explorer.confirm_file_delete"),
            force ? L.F("explorer.are_you_sure_you_want_to_delete_0_permanentl", victim.Name)
                  : L.F("explorer.are_you_sure_you_want_to_move_0_to_the_recyc", victim.Name),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion, r =>
            {
                if (r != MsgResult.Yes) return;
                bool wasWindows = VirtualFS.IsWindowsFolder(victim);
                Shell.Fs.Delete(victim, force);
                if (_selected == victim) _selected = null;
                Shell.Audio.Play(Sfx.Trash, 0.8f);

                // Deleting a folder called Windows is, in part 3, proof of
                // originality: the system carries on regardless.
                if (wasWindows)
                    Shell.MessageBox(c, L.T("shell.miminus_os"), L.T("fs.windows_deleted"),
                        MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
            }, Sfx.Question);
    }
}
