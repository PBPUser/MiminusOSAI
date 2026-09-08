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
/// on what is selected, exactly as XP did.</summary>
public sealed class ExplorerWindow : OsWindow
{
    VNode _folder;
    VNode _selected;

    /// <summary>The node being renamed in place, and the text being typed.</summary>
    VNode _renaming;
    string _renameText = "";

    /// <summary>Item pressed but not yet dragged, and where the press landed.
    /// A drag starts once the pointer has moved far enough to mean it.</summary>
    VNode _pressed;
    float _pressX, _pressY;


    readonly List<VNode> _history = new();
    int _historyIndex = -1;

    bool _showTaskPane = true;
    ViewMode _view = ViewMode.Icons;
    float _scroll;

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
        BuildMenu();
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
            MenuItem.Check(L.T("explorer.task_pane"), _showTaskPane, () => _showTaskPane = !_showTaskPane),
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
        DrawToolbar(c, area.CutTop(30));
        DrawAddressBar(c, area.CutTop(24));
        var status = area.CutBottom(20);

        Rect taskPane = default;
        if (_showTaskPane && area.W > 380)
            taskPane = area.CutLeft(184);

        DrawFileList(c, area);
        if (!taskPane.IsEmpty) DrawTaskPane(c, taskPane);

        string count = L.F("explorer.0_objects", _folder.Entries.Count);
        string detail = _selected != null
            ? (_selected.IsContainer ? _selected.TypeName : L.FileSize(_selected.Size))
            : "";
        W.StatusBar(c, status, _selected != null
            ? L.F("explorer.type_0_modified_1_2_hh_mm_size_3", _selected.TypeName, L.ShortDate(_selected.Modified), _selected.Modified, L.FileSize(_selected.Size))
            : count,
            detail, L.T("explorer.my_computer"));

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

    void DrawToolbar(UiContext c, Rect bar)
    {
        W.ToolbarBackground(c, bar);
        float x = bar.X + 4;
        float bh = bar.H - 6;

        bool canBack = _historyIndex > 0;
        bool canFwd = _historyIndex < _history.Count - 1;

        if (ToolButton(c, ".back", ref x, bar, bh, IconId.None, L.T("explorer.back"), canBack, 3))
        {
            _historyIndex--;
            Navigate(_history[_historyIndex], c, record: false);
        }
        if (ToolButton(c, ".fwd", ref x, bar, bh, IconId.None, null, canFwd, 1))
        {
            _historyIndex++;
            Navigate(_history[_historyIndex], c, record: false);
        }
        if (ToolButton(c, ".up", ref x, bar, bh, IconId.FolderOpen, null, _folder.Parent != null, 0))
            GoUp(c);

        W.Separator(c, x + 2, bar.Y + 4, bar.H - 8);
        x += 8;

        if (ToolButton(c, ".search", ref x, bar, bh, IconId.Search, L.T("explorer.search"), true, -1))
            Shell.Launch(c, "search", null);
        if (ToolButton(c, ".folders", ref x, bar, bh, IconId.Folder, L.T("explorer.folders"), true, -1))
            _showTaskPane = !_showTaskPane;

        W.Separator(c, x + 2, bar.Y + 4, bar.H - 8);
        x += 8;

        var viewBtn = new Rect(x, bar.Y + 3, 34, bh);
        if (W.FlatButton(c, Id + ".view", viewBtn, null, true, IconId.Display))
        {
            Shell.Menus.Open(new List<MenuItem>
            {
                new() { Text = L.T("explorer.tiles"), IsRadio = true, Checked = _view == ViewMode.Tiles, Click = () => _view = ViewMode.Tiles },
                new() { Text = L.T("explorer.icons"), IsRadio = true, Checked = _view == ViewMode.Icons, Click = () => _view = ViewMode.Icons },
                new() { Text = L.T("explorer.list"), IsRadio = true, Checked = _view == ViewMode.List, Click = () => _view = ViewMode.List },
                new() { Text = L.T("explorer.details"), IsRadio = true, Checked = _view == ViewMode.Details, Click = () => _view = ViewMode.Details },
            }, viewBtn.X, viewBtn.Bottom, this, c);
        }
        W.Arrow(c, new Rect(viewBtn.Right - 10, viewBtn.Y, 8, viewBtn.H), 2);
    }

    /// <summary>Toolbar button. <paramref name="arrow"/> &gt;= 0 draws a direction
    /// triangle instead of an icon (Back / Forward / Up).</summary>
    bool ToolButton(UiContext c, string id, ref float x, Rect bar, float bh,
                    IconId icon, string label, bool enabled, int arrow)
    {
        float w = 24 + (label != null ? c.F.Ui.Measure(label) + 6 : 0);
        var r = new Rect(x, bar.Y + 3, w, bh);
        bool hover = enabled && c.Hovering(r);
        bool clicked = enabled && c.Clicked(r);

        if (hover)
        {
            c.R.FillRect(r, c.Theme.Hot.WithAlpha((byte)110));
            c.R.DrawRect(r, c.Theme.ControlBorderHot);
        }

        Color ink = enabled ? Color.Rgb(0x1E5FA8) : c.Theme.TextDisabled;
        if (arrow >= 0)
        {
            var box = new Rect(r.X + 2, r.Y, 20, r.H);
            c.R.FillCircle(box.CenterX, box.CenterY, 8, enabled ? Color.Rgb(0x2E8AF5) : Color.Rgb(0xC8C8C8));
            c.R.FillCircle(box.CenterX, box.CenterY - 2, 6.5f, enabled ? Color.Rgb(0x7FC0FF) : Color.Rgb(0xDCDCDC));
            W.Arrow(c, box, arrow, Color.White, 4f);
        }
        else Icons.Draw(c.R, icon, new Rect(r.X + 3, r.CenterY - 9, 18, 18));

        if (label != null)
            c.F.Ui.Draw(c.R, label, r.X + 24, r.CenterY - c.F.Ui.Height * 0.5f,
                        enabled ? c.Theme.Text : c.Theme.TextDisabled);

        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);
        x += w + 2;
        return clicked;
    }

    void DrawAddressBar(UiContext c, Rect bar)
    {
        var t = c.Theme;
        c.R.FillRect(bar, t.Face);

        float labelW = c.F.Ui.Measure(L.T("explorer.address")) + 8;
        c.F.Ui.Draw(c.R, L.T("explorer.address"), bar.X + 4, bar.CenterY - c.F.Ui.Height * 0.5f, t.Text);

        var go = new Rect(bar.Right - 74, bar.Y + 2, 70, bar.H - 4);
        var field = new Rect(bar.X + labelW, bar.Y + 2, go.X - bar.X - labelW - 4, bar.H - 4);

        W.SunkenField(c, field);
        Icons.Draw(c.R, Icon, new Rect(field.X + 2, field.CenterY - 8, 16, 16));

        c.R.PushClip(field.Deflate(2));
        string path = _folder.Path;
        c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(path, field.W - 44), field.X + 21,
                    field.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        c.R.PopClip();

        var drop = new Rect(field.Right - 17, field.Y + 1, 16, field.H - 2);
        c.R.FillRectV(drop, t.FaceLight, t.FaceDark);
        c.R.DrawRect(drop, t.ControlBorder);
        W.Arrow(c, drop, 2);
        if (c.Clicked(drop)) ShowPlacesMenu(c, drop);

        if (W.FlatButton(c, Id + ".go", go, L.T("explorer.go"), true, IconId.Globe))
            c.Sound(Sfx.Navigate, 0.5f);
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

    void DrawTaskPane(UiContext c, Rect pane)
    {
        var t = c.Theme;
        // The pane's own pale-blue background, as in Luna.
        c.R.FillRectV(pane, Color.Rgb(0x7CA7DE), Color.Rgb(0x6389C7));
        c.R.PushClip(pane);

        float y = pane.Y + 8;

        if (_selected is { Kind: NodeKind.ImageFile })
        {
            y = TaskGroup(c, pane, y, L.T("explorer.picture_tasks"), new (string, IconId, Action)[]
            {
                ("task.get_pictures_from_camera_or_scanner", IconId.Camera, () => NotAvailable(c)),
                ("task.view_as_a_slide_show", IconId.MediaPlayer, () => Shell.Launch(c, "player", _selected)),
                ("task.order_prints_online", IconId.Globe, () => NotAvailable(c)),
                ("task.print_this_picture", IconId.Printer, () => NotAvailable(c)),
                ("task.set_as_desktop_background", IconId.Display,
                    () => { Shell.SetWallpaper(WallpaperId.MiminusYellow); c.Sound(Sfx.Navigate, 0.5f); }),
                ("task.copy_to_cd", IconId.DriveDvd, () => NotAvailable(c)),
            });
        }

        if (_selected != null)
        {
            y = TaskGroup(c, pane, y, L.T("explorer.file_and_folder_tasks"), new (string, IconId, Action)[]
            {
                (_selected.IsContainer ? "task.rename_this_folder" : "task.rename_this_file",
                 _selected.IsContainer ? IconId.Folder : IconId.TextFile,
                 () => BeginRename(_selected)),
                ("task.move_this_file", IconId.Folder, () => NotAvailable(c)),
                ("task.copy_this_file", IconId.Folder, () => NotAvailable(c)),
                ("task.publish_this_file_to_the_web", IconId.Globe, () => NotAvailable(c)),
                ("task.e_mail_this_file", IconId.Mail, () => NotAvailable(c)),
                ("task.delete_this_file", IconId.RecycleBin, () => DeleteSelected(c)),
            });
        }
        else
        {
            y = TaskGroup(c, pane, y, L.T("explorer.file_and_folder_tasks"), new (string, IconId, Action)[]
            {
                ("task.make_a_new_folder", IconId.Folder, () => Create(NodeKind.Folder, IconId.Folder)),
                ("task.publish_this_folder_to_the_web", IconId.Globe, () => NotAvailable(c)),
                ("task.share_this_folder", IconId.Network, () => NotAvailable(c)),
            });
        }

        y = TaskGroup(c, pane, y, L.T("explorer.other_places"), new (string, IconId, Action)[]
        {
            (Shell.Fs.Desktop.Name, IconId.Folder, () => Navigate(Shell.Fs.Desktop, c)),
            (Shell.Fs.MyDocuments.Name, IconId.MyDocuments, () => Navigate(Shell.Fs.MyDocuments, c)),
            (Shell.Fs.MyComputer.Name, IconId.MyComputer, () => Navigate(Shell.Fs.MyComputer, c)),
            (L.T("explorer.my_network_places"), IconId.Network, () => Shell.Launch(c, "network", null)),
        });

        // Details group.
        y = GroupHeader(c, pane, y, L.T("explorer.details_2"));
        var box = new Rect(pane.X + 10, y, pane.W - 20, 90);
        if (_selected != null)
        {
            c.F.UiBold.Draw(c.R, c.F.UiBold.Ellipsize(_selected.Name, box.W), box.X, box.Y, Color.White);
            float dy = box.Y + c.F.UiBold.Height + 2;
            c.F.Small.Draw(c.R, _selected.TypeName, box.X, dy, Color.Rgba(0xFFFFFF, 220));
            dy += c.F.Small.Height + 2;
            c.F.Small.Draw(c.R, L.F("explorer.date_modified_0_1_hh_mm", L.ShortDate(_selected.Modified), _selected.Modified),
                           box.X, dy, Color.Rgba(0xFFFFFF, 220));
            dy += c.F.Small.Height + 2;
            if (!_selected.IsContainer)
                c.F.Small.Draw(c.R, L.F("explorer.size_0", L.FileSize(_selected.Size)),
                               box.X, dy, Color.Rgba(0xFFFFFF, 220));
        }
        else
        {
            c.F.UiBold.Draw(c.R, c.F.UiBold.Ellipsize(_folder.Name, box.W), box.X, box.Y, Color.White);
            c.F.Small.Draw(c.R, _folder.TypeName, box.X, box.Y + c.F.UiBold.Height + 2, Color.Rgba(0xFFFFFF, 220));
        }

        c.R.PopClip();
    }

    float GroupHeader(UiContext c, Rect pane, float y, string title)
    {
        var head = new Rect(pane.X + 6, y, pane.W - 12, 22);
        c.R.RoundedRectV(head, 3, Color.Rgb(0xF0F4FB), Color.Rgb(0xC6D8F0));
        c.F.UiBold.Draw(c.R, title, head.X + 8, head.CenterY - c.F.UiBold.Height * 0.5f, Color.Rgb(0x0A3070));

        // The collapse chevron XP put on the right.
        var chev = new Rect(head.Right - 20, head.Y + 3, 16, 16);
        c.R.FillCircle(chev.CenterX, chev.CenterY, 7, Color.Rgb(0x4A82C8));
        W.Arrow(c, chev, 0, Color.White, 3f);

        return head.Bottom + 4;
    }

    float TaskGroup(UiContext c, Rect pane, float y, string title,
                    (string key, IconId icon, Action click)[] links)
    {
        y = GroupHeader(c, pane, y, title);
        foreach (var (key, icon, click) in links)
        {
            var row = new Rect(pane.X + 12, y, pane.W - 20, 0);
            string text = key.Contains('.') && !key.Contains(' ') ? L.T(key) : key;
            var lines = c.F.Small.Wrap(text, row.W - 22);
            row.H = MathF.Max(16, lines.Count * (c.F.Small.Height + 1)) + 3;

            bool hover = c.Hovering(row);
            Icons.Draw(c.R, icon, new Rect(row.X, row.Y + 1, 14, 14));

            float ly = row.Y;
            foreach (string line in lines)
            {
                c.F.Small.Draw(c.R, line, row.X + 19, ly, hover ? Color.Rgb(0xFFE8A8) : Color.White);
                if (hover)
                    c.R.FillRect(new Rect(row.X + 19, ly + c.F.Small.Ascent + 2, c.F.Small.Measure(line), 1),
                                 Color.Rgb(0xFFE8A8));
                ly += c.F.Small.Height + 1;
            }

            if (c.Clicked(row)) { c.SoundAt(Sfx.Click, row, 0.4f); click(); }
            y = row.Bottom + 2;
        }
        return y + 8;
    }

    void NotAvailable(UiContext c)
        => Shell.MessageBox(c, _folder.Name,
            L.T("explorer.that_task_is_not_available_in_miminus_os"),
            MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

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

        Shell.Drag.Begin(_pressed, _pressed.Icon, _pressed.Name, this);
        _pressed = null;
    }

    // ---- renaming in place ------------------------------------------------

    void BeginRename(VNode node)
    {
        if (!VirtualFS.CanRename(node)) return;
        _selected = node;
        _renaming = node;
        _renameText = node.Name;
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

        switch (W.RenameBox(c, Id + ".rename", box, ref _renameText))
        {
            case W.RenameResult.Commit: CommitRename(c); break;
            case W.RenameResult.Cancel: _renaming = null; break;
        }
    }

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
