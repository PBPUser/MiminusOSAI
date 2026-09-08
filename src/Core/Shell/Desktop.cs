using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>One icon on the desktop. Either points at a filesystem node or is a
/// shell place (My Computer, the Recycle Bin) or a decorative stand-in for one
/// of the many programs littering the desktop in the reference videos.</summary>
public sealed class DesktopIcon
{
    public string LabelKey;

    /// <summary>Set instead of <see cref="LabelKey"/> for names that come from the
    /// host filesystem and so are not translatable.</summary>
    public string LiteralLabel;
    public IconId Icon;
    public VNode Node;
    public string Launch;       // app id, when this icon starts something
    public bool Shortcut;
    public bool Decorative;     // "part of Миминус ОС", opens an info box
    public int Col, Row;
    public Rect Bounds;
    public bool Selected;

    public string Label => LiteralLabel ?? L.T(LabelKey);
}

/// <summary>The desktop layer: wallpaper, icon grid, rubber-band selection and
/// the right-click menu. Drawn beneath every window and updated last, so it only
/// sees input nothing above it wanted.</summary>
public sealed class Desktop
{
    public readonly List<DesktopIcon> Icons = new();
    public WallpaperId Current = WallpaperId.MiminusYellow;

    readonly ShellHost _shell;

    // Rubber band
    bool _banding;
    float _bandX, _bandY;

    // Icon dragging
    DesktopIcon _dragIcon;
    float _dragDX, _dragDY;
    bool _dragMoved;

    const float TopPad = 8, LeftPad = 8;

    // Cell and glyph size follow the "use large icons" effect.
    float CellW => _shell.Settings.DesktopCellSize;
    float CellH => _shell.Settings.DesktopCellSize;
    float IconSize => _shell.Settings.DesktopIconSize;

    public Desktop(ShellHost shell)
    {
        _shell = shell;
        Populate();
    }

    void Populate()
    {
        var fs = _shell.Fs;

        // The working shell places, in the order XP put them.
        Add("icon.my_computer", IconId.MyComputer, launch: "mycomputer");
        Add("icon.my_documents", IconId.MyDocuments, node: fs.MyDocuments, launch: "explorer");
        Add("icon.recycle_bin", IconId.RecycleBin, node: fs.RecycleBin, launch: "explorer");
        Add("icon.my_network_places", IconId.Network, launch: "network");
        Add("icon.internet", IconId.Firefox, launch: "browser", shortcut: true);

        // Content straight out of the videos.
        Add("icon.revolutionary_distributions", IconId.Folder,
            node: fs.Revolutionary, launch: "explorer");
        Add("icon.useful_miminus_tricks", IconId.Folder,
            node: fs.UsefulTricks, launch: "explorer");
        Add("icon.grevtsov_antivirus", IconId.Antivirus,
            node: fs.AntivirusFile, launch: "notepad", shortcut: true);
        Add("icon.minesweeper", IconId.Minesweeper, launch: "minesweeper", shortcut: true);
        Add("icon.coursework_xls", IconId.Spreadsheet,
            node: VirtualFS.ByKey(fs.Desktop, "fs.coursework_xls"), launch: "spreadsheet");
        Add("icon.read_me_txt", IconId.TextFile,
            node: VirtualFS.ByKey(fs.Desktop, "fs.read_me_txt"), launch: "notepad");
        Add("icon.paint", IconId.Paint, launch: "paint", shortcut: true);
        Add("icon.calculator_plus", IconId.Calculator, launch: "calculator", shortcut: true);
        Add("icon.media_player", IconId.MediaPlayer, launch: "player", shortcut: true);
        Add("icon.display_properties", IconId.Display, launch: "display", shortcut: true);
        Add("icon.control_panel", IconId.ControlPanel, launch: "controlpanel");
        Add("icon.all_in_one", IconId.Settings, launch: "allinone", shortcut: true);
        Add("icon.voice_recognition", IconId.Volume, launch: "voice", shortcut: true);
        Add("icon.orega", IconId.Opera, launch: "orega", shortcut: true);
        Add("icon.mi_folder", IconId.Folder,
            node: VirtualFS.ByKey(fs.Desktop, "fs.mi_folder"), launch: "explorer");
        Add("icon.backups", IconId.Folder, node: fs.Backups, launch: "explorer");
        Add("icon.windows_folder", IconId.Folder, node: fs.WindowsFolder, launch: "explorer");
        Add("icon.command_prompt", IconId.Terminal, launch: "terminal", shortcut: true);

        // The scenery: programs that exist only as icons, exactly as on the
        // author's own overloaded desktop.
        (string key, IconId icon)[] clutter =
        {
            ("icon.torrents", IconId.Folder),
            ("icon.network_connections", IconId.Network),
            ("icon.translator_2009", IconId.Program),
            ("icon.disciples_iii", IconId.Game),
            ("icon.books", IconId.Folder),
            ("icon.metro_2033", IconId.Game),
            ("icon.reading", IconId.Folder),
            ("icon.kaylee", IconId.Folder),
            ("icon.finecount", IconId.Program),
            ("icon.acer_crystal_eye_webcam", IconId.Camera),
            ("icon.aimp2_audio_converter", IconId.AudioFile),
            ("icon.adobe_reader_9", IconId.WordDoc),
            ("icon.alo_audio_center", IconId.MediaPlayer),
            ("icon.skype", IconId.Skype),
            ("icon.counter_strike_1_6", IconId.Game),
            ("icon.google_earth", IconId.Globe),
            ("icon.windows_mobile", IconId.Phone),
            ("icon.photoshop", IconId.Paint),
            ("icon.opera", IconId.Opera),
            ("icon.cureit_exe", IconId.Shield),
            ("icon.daemon_tools_lite", IconId.DriveDvd),
            ("icon.chamax_rus", IconId.Program),
            ("icon.qip_2005", IconId.Mail),
            ("icon.earned", IconId.WordDoc),
            ("icon.delphi_7", IconId.Program),
            ("icon.fifa_09", IconId.Game),
            ("icon.fifa_10", IconId.Game),
            ("icon.isubo", IconId.Archive),
            ("icon.driverscan", IconId.Settings),
            ("icon.nokia_pc_suite", IconId.Phone),
            ("icon.torrent", IconId.Torrent),
            ("icon.video_ts", IconId.Folder),
            ("icon.lac", IconId.Folder),
            ("icon.audiovip", IconId.AudioFile),
            ("icon.mk_new", IconId.WordDoc),
            ("icon.discr_xls", IconId.Spreadsheet),
            ("icon.pc2setup_exe", IconId.Program),
            ("icon.vancouver_2010", IconId.Game),
        };
        foreach (var (key, icon) in clutter)
            Add(key, icon, decorative: true);

        Relayout(1280, 800);
    }

    /// <summary>Adds a desktop shortcut for a mounted host drive. Its label is the
    /// drive's own name rather than a catalogue key, since it comes from the host.</summary>
    public void AddMountShortcut(VNode driveNode)
    {
        Icons.Insert(0, new DesktopIcon
        {
            LiteralLabel = driveNode.Name,
            Icon = IconId.DriveUsb,
            Node = driveNode,
            Launch = "explorer",
            Shortcut = true,
        });
    }

    void Add(string labelKey, IconId icon, VNode node = null, string launch = null,
             bool shortcut = false, bool decorative = false)
    {
        Icons.Add(new DesktopIcon
        {
            LabelKey = labelKey, Icon = icon,
            Node = node, Launch = launch, Shortcut = shortcut, Decorative = decorative,
        });
    }

    /// <summary>Fills columns top-to-bottom then wraps, the way Explorer does.</summary>
    public void Relayout(int screenW, int screenH)
    {
        int rows = Math.Max(1, (int)((screenH - _shell.Theme.TaskbarHeight - TopPad) / CellH));
        for (int i = 0; i < Icons.Count; i++)
        {
            Icons[i].Col = i / rows;
            Icons[i].Row = i % rows;
            Icons[i].Bounds = CellRect(Icons[i].Col, Icons[i].Row);
        }
    }

    Rect CellRect(int col, int row) => new(LeftPad + col * CellW, TopPad + row * CellH, CellW, CellH);

    public void ClearSelection()
    {
        foreach (var i in Icons) i.Selected = false;
    }

    // ---- painting --------------------------------------------------------

    /// <summary>Paints the wallpaper and the icon grid. Runs first in the frame,
    /// beneath every window; interaction happens later in <see cref="Update"/>.</summary>
    public void Draw(UiContext c)
    {
        var screen = new Rect(0, 0, c.ScreenW, c.ScreenH - c.Theme.TaskbarHeight);
        var wp = _shell.Wallpapers.Get(Current);

        if (wp.Texture != null) c.R.DrawTexture(wp.Texture, new Rect(0, 0, c.ScreenW, c.ScreenH));
        else c.R.FillRect(new Rect(0, 0, c.ScreenW, c.ScreenH), wp.Fallback);

        wp.Overlay?.Invoke(c, screen);

        foreach (var icon in Icons)
            DrawIcon(c, icon);

        // The rubber band uses the rectangle computed by the previous Update, so
        // it paints under the windows rather than over them.
        if (!_bandRect.IsEmpty)
        {
            c.R.FillRect(_bandRect, Color.Rgba(0x316AC5, 60));
            c.R.DrawRect(_bandRect, Color.Rgba(0xFFFFFF, 200));
        }
    }

    /// <summary>The icon being renamed in place, and the text being typed.</summary>
    DesktopIcon _renaming;
    string _renameText = "";

    void BeginRename(DesktopIcon icon)
    {
        if (icon == null) return;
        // A shortcut or a decorative icon renames its own label; one backed by a
        // real node renames the node, and so refuses when the node does.
        if (icon.Node != null && !VirtualFS.CanRename(icon.Node)) return;

        ClearSelection();
        icon.Selected = true;
        _renaming = icon;
        _renameText = icon.Label;
    }

    void CommitRename(UiContext c)
    {
        var icon = _renaming;
        _renaming = null;
        if (icon == null) return;

        string name = _renameText?.Trim();
        if (string.IsNullOrEmpty(name) || name == icon.Label) return;

        if (icon.Node != null)
        {
            if (!_shell.Fs.Rename(icon.Node, name, out string error))
            {
                _shell.MessageBox(c, L.T("desktop.rename_failed_title"), error,
                                  MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
                return;
            }
            // The label follows the node, which no longer follows the language.
            icon.LabelKey = null;
            icon.LiteralLabel = icon.Node.Name;
        }
        else
        {
            icon.LabelKey = null;
            icon.LiteralLabel = name;
        }

        c.Sound(Sfx.Tick, 0.5f);
    }

    void DrawIcon(UiContext c, DesktopIcon icon)
    {
        var cell = icon.Bounds;
        if (icon == _dragIcon && _dragMoved)
            cell = new Rect(c.MouseX - _dragDX, c.MouseY - _dragDY, CellW, CellH);

        var iconRect = new Rect(cell.X + (CellW - IconSize) * 0.5f, cell.Y + 4, IconSize, IconSize);
        var labelArea = new Rect(cell.X + 2, iconRect.Bottom + 3, CellW - 4, CellH - IconSize - 8);

        bool hover = cell.Contains(c.MouseX, c.MouseY) && !c.MouseHandled;

        if (icon == _renaming)
        {
            Graphics.Icons.Draw(c.R, icon.Icon, iconRect);
            if (icon.Shortcut || icon.Node?.Kind == NodeKind.Shortcut)
                Graphics.Icons.DrawShortcutOverlay(c.R, iconRect);

            var box = new Rect(labelArea.X, labelArea.Y - 1, labelArea.W, c.F.Ui.Height + 6);
            switch (W.RenameBox(c, "desktop.rename.box", box, ref _renameText))
            {
                case W.RenameResult.Commit: CommitRename(c); break;
                case W.RenameResult.Cancel: _renaming = null; break;
            }
            return;
        }

        // Label wraps to at most two lines and is ellipsised after that.
        var lines = c.F.Small.Wrap(icon.Label, labelArea.W - 4);
        if (lines.Count > 2)
        {
            lines = lines.Take(2).ToList();
            lines[1] = c.F.Small.Ellipsize(lines[1] + "…", labelArea.W - 4);
        }
        float lineH = c.F.Small.Height + 1;

        if (icon.Selected)
        {
            c.R.FillRect(iconRect.Inflate(2), Color.Rgba(0x316AC5, 110));
            for (int i = 0; i < lines.Count; i++)
            {
                float lw = c.F.Small.Measure(lines[i]);
                c.R.FillRect(new Rect(labelArea.CenterX - lw * 0.5f - 2, labelArea.Y + i * lineH, lw + 4, lineH),
                             Color.Rgb(0x316AC5));
            }
        }
        else if (hover)
        {
            c.R.FillRect(iconRect.Inflate(2), Color.Rgba(0xFFFFFF, 45));
        }

        Graphics.Icons.Draw(c.R, icon.Icon, iconRect);
        if (icon.Shortcut || icon.Node?.Kind == NodeKind.Shortcut)
            Graphics.Icons.DrawShortcutOverlay(c.R, iconRect);

        // White label text with a soft shadow, so it reads on any wallpaper.
        for (int i = 0; i < lines.Count; i++)
        {
            float lw = c.F.Small.Measure(lines[i]);
            float lx = labelArea.CenterX - lw * 0.5f;
            float ly = labelArea.Y + i * lineH;
            if (!icon.Selected)
                c.F.Small.Draw(c.R, lines[i], lx + 1, ly + 1, Color.Rgba(0x000000, 190));
            c.F.Small.Draw(c.R, lines[i], lx, ly, Color.White);
        }
    }

    // ---- interaction -----------------------------------------------------

    Rect _bandRect;

    /// <summary>Handles desktop input. Runs after every window has had its turn,
    /// so a click only reaches the desktop when nothing above wanted it.</summary>
    public void Update(UiContext c)
    {
        // Snapshot: activating an icon can add or remove icons (New…, Delete).
        foreach (var icon in Icons.ToArray())
        {
            var cell = icon.Bounds;
            if (icon == _dragIcon && _dragMoved)
                cell = new Rect(c.MouseX - _dragDX, c.MouseY - _dragDY, CellW, CellH);

            if (!string.IsNullOrEmpty(icon.Node?.Tooltip))
                c.Tooltip(cell, icon.Node.Tooltip);

            if (icon != _renaming) HandleIcon(c, icon, cell);
        }

        HandleBackground(c, new Rect(0, 0, c.ScreenW, c.ScreenH - c.Theme.TaskbarHeight));
    }

    void HandleIcon(UiContext c, DesktopIcon icon, Rect cell)
    {
        if (c.DoubleClicked(cell))
        {
            ClearSelection();
            icon.Selected = true;
            Activate(c, icon);
            _dragIcon = null;
            return;
        }

        if (c.Clicked(cell))
        {
            if (!c.In.Ctrl) { if (!icon.Selected) ClearSelection(); }
            icon.Selected = c.In.Ctrl ? !icon.Selected : true;
            _dragIcon = icon;
            _dragDX = c.MouseX - icon.Bounds.X;
            _dragDY = c.MouseY - icon.Bounds.Y;
            _dragMoved = false;
            c.SoundAt(Sfx.Tick, cell, 0.25f);
        }
        else if (c.RightClicked(cell))
        {
            if (!icon.Selected) { ClearSelection(); icon.Selected = true; }
            ShowIconMenu(c, icon);
        }
    }

    void HandleBackground(UiContext c, Rect screen)
    {
        // Finish an icon drag.
        if (_dragIcon != null)
        {
            if (!c.In.IsDown(MouseButton.Left))
            {
                // A folder window has taken the file: leave the icon where it
                // was, because it is about to go away with the node.
                bool takenElsewhere = _shell.Drag.Dragging && !_shell.Drag.IsTarget(this)
                                      && _shell.Drag.Source == (object)this;

                if (_dragMoved && !takenElsewhere) SnapToGrid(c, _dragIcon);
                _dragIcon = null;
                _dragMoved = false;
            }
            else
            {
                if (MathF.Abs(c.MouseX - _dragDX - _dragIcon.Bounds.X) > 3 ||
                    MathF.Abs(c.MouseY - _dragDY - _dragIcon.Bounds.Y) > 3)
                    _dragMoved = true;

                // Dragging on the desktop rearranges icons. The same drag over
                // a folder window means something else, so the file is offered
                // to the rest of the shell at the same time and whichever
                // reading the pointer ends on is the one that happens.
                if (_dragMoved && _dragIcon.Node != null && !_shell.Drag.Dragging)
                    _shell.Drag.Begin(_dragIcon.Node, _dragIcon.Icon, _dragIcon.Label, this);

                c.MouseHandled = true;
                if (_dragMoved) c.Cursor = CursorShape.Move;
                return;
            }
        }

        // Anything let go over the desktop lands in the desktop folder, unless
        // an icon of a folder took it first.
        if (_shell.Drag.Dragging)
        {
            var over = Icons.FirstOrDefault(i => i.Node is { } n && n.IsContainer
                                                 && i.Bounds.Contains(c.MouseX, c.MouseY));
            if (over == null || !_shell.Drag.Offer(c, over.Bounds, over.Node, over))
                _shell.Drag.Offer(c, screen, _shell.Fs.Desktop, this);
        }

        if (c.Clicked(screen))
        {
            ClearSelection();
            _banding = true;
            _bandX = c.MouseX;
            _bandY = c.MouseY;
        }
        else if (c.RightClicked(screen))
        {
            ClearSelection();
            ShowDesktopMenu(c);
        }

        if (_banding)
        {
            if (!c.In.IsDown(MouseButton.Left)) { _banding = false; _bandRect = default; }
            else
            {
                _bandRect = new Rect(MathF.Min(_bandX, c.MouseX), MathF.Min(_bandY, c.MouseY),
                                     MathF.Abs(c.MouseX - _bandX), MathF.Abs(c.MouseY - _bandY));
                foreach (var i in Icons) i.Selected = i.Bounds.Intersects(_bandRect);
                c.MouseHandled = true;
            }
        }
        else _bandRect = default;

        // F5 refreshes, F2 renames, Delete removes the selection. The rename
        // box claims the keyboard while it is open, so none of this fires.
        if (!c.KeyboardHandled)
        {
            // Ctrl on its own, with a folder called Windows selected, is all
            // it takes — as part 3 demonstrates.
            var windows = Icons.FirstOrDefault(i => i.Selected && VirtualFS.IsWindowsFolder(i.Node));
            if (c.In.KeyPressed(Keys.Control) && windows != null)
            {
                _shell.Fs.Delete(windows.Node, permanent: true);
                Icons.Remove(windows);
                Relayout(c.ScreenW, c.ScreenH);
                _shell.Audio.Play(Sfx.Trash, 0.8f);
                _shell.MessageBox(c, L.T("shell.miminus_os"), L.T("fs.windows_deleted"),
                                  MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
            }
            else if (c.In.KeyPressed(Keys.F2))
                BeginRename(Icons.FirstOrDefault(i => i.Selected));
            else if (c.In.KeyPressed(Keys.F5))
            {
                Relayout(c.ScreenW, c.ScreenH);
                c.Sound(Sfx.Navigate, 0.4f);
            }
            else if (c.In.KeyPressed(Keys.Delete))
            {
                var sel = Icons.Where(i => i.Selected && i.Node != null).ToList();
                if (sel.Count > 0) DeleteSelected(c, sel);
            }
        }
    }

    void SnapToGrid(UiContext c, DesktopIcon icon)
    {
        float x = c.MouseX - _dragDX;
        float y = c.MouseY - _dragDY;
        int col = Math.Max(0, (int)MathF.Round((x - LeftPad) / CellW));
        int row = Math.Max(0, (int)MathF.Round((y - TopPad) / CellH));

        // Nudge along until a free cell is found so icons never stack.
        while (Icons.Any(i => i != icon && i.Col == col && i.Row == row))
        {
            row++;
            if (TopPad + row * CellH + CellH > c.ScreenH - c.Theme.TaskbarHeight) { row = 0; col++; }
        }
        icon.Col = col;
        icon.Row = row;
        icon.Bounds = CellRect(col, row);
        c.Sound(Sfx.Tick, 0.3f);
    }

    void DeleteSelected(UiContext c, List<DesktopIcon> sel)
    {
        // Ctrl deletes outright instead of to the Recycle Bin — and a folder
        // called Windows moves no other way. The key is read now, because the
        // confirmation is answered after it has been let go.
        bool force = c.In.Ctrl;

        var stubborn = sel.FirstOrDefault(i => VirtualFS.NeedsForce(i.Node));
        if (stubborn != null && !force)
        {
            _shell.MessageBox(c, stubborn.Label, L.T("fs.hold_ctrl_to_delete"),
                MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
            return;
        }

        string msg = sel.Count == 1
            ? L.F(force ? "desktop.are_you_sure_you_want_to_delete_0_permanentl"
                        : "desktop.are_you_sure_you_want_to_move_0_to_the_recyc", sel[0].Label)
            : L.F(force ? "desktop.are_you_sure_you_want_to_delete_these_0_item"
                        : "desktop.are_you_sure_you_want_to_move_these_0_items", sel.Count);

        _shell.MessageBox(c,
            force ? L.T("desktop.confirm_folder_delete") : L.T("desktop.confirm_file_delete"), msg,
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion, r =>
            {
                if (r != MsgResult.Yes) return;

                bool windowsWentAway = false;
                foreach (var i in sel)
                {
                    if (VirtualFS.IsWindowsFolder(i.Node)) windowsWentAway = true;
                    if (i.Node != null) _shell.Fs.Delete(i.Node, force);
                    Icons.Remove(i);
                }
                Relayout(c.ScreenW, c.ScreenH);
                _shell.Audio.Play(Sfx.Trash, 0.8f);

                // Part 3 deletes a Windows folder to prove the system is not
                // Windows underneath. It carries on.
                if (windowsWentAway)
                    _shell.MessageBox(c, L.T("shell.miminus_os"), L.T("fs.windows_deleted"),
                        MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
            }, Sfx.Question);
    }

    /// <summary>Called after a node has moved anywhere in the filesystem: the
    /// desktop gains an icon if it landed here and loses one if it left.</summary>
    public void NodeMoved(UiContext c, VNode node)
    {
        if (node == null) return;

        bool onDesktop = node.Parent == _shell.Fs.Desktop;
        var existing = Icons.FirstOrDefault(i => i.Node == node);

        if (onDesktop && existing == null)
        {
            var icon = new DesktopIcon
            {
                LiteralLabel = node.Name,
                Icon = node.Icon,
                Node = node,
                Launch = node.Launch ?? (node.IsContainer ? "explorer" : null),
                Shortcut = node.Kind == NodeKind.Shortcut,
            };
            Icons.Add(icon);

            // It lands where it was let go, in the nearest free cell, rather
            // than at the end of the grid where nobody would look for it.
            _dragDX = CellW * 0.5f;
            _dragDY = CellH * 0.5f;
            SnapToGrid(c, icon);
            return;
        }

        if (!onDesktop && existing != null) Icons.Remove(existing);
        else return;

        Relayout(c.ScreenW, c.ScreenH);
    }

    public void Activate(UiContext c, DesktopIcon icon)
    {
        if (icon.Decorative)
        {
            _shell.MessageBox(c,
                icon.Label,
                L.F("desktop.0_is_part_of_miminus_os_no_installation_need", icon.Label),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
            return;
        }
        _shell.Launch(c, icon.Launch, icon.Node);
    }

    // ---- context menus ---------------------------------------------------

    void ShowIconMenu(UiContext c, DesktopIcon icon)
    {
        var items = new List<MenuItem>
        {
            new() { Text = L.T("desktop.open"), Bold = true, Click = () => Activate(c, icon) },
        };

        if (icon.Node is { Kind: NodeKind.ImageFile })
            items.Add(MenuItem.Of(L.T("desktop.edit"), () => _shell.Launch(c, "paint", icon.Node)));

        if (icon.Node is { Kind: NodeKind.TextFile or NodeKind.ImageFile })
        {
            items.Add(MenuItem.Sub(L.T("desktop.open_with"), _shell.BuildOpenWithMenu(c, icon.Node)));
        }

        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("desktop.cut"), () => c.Sound(Sfx.Click)));
        items.Add(MenuItem.Of(L.T("desktop.copy"), () => c.Sound(Sfx.Click)));
        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("desktop.create_shortcut"), () => c.Sound(Sfx.Click)));
        items.Add(MenuItem.Of(L.T("desktop.delete"), () =>
        {
            var sel = Icons.Where(i => i.Selected).ToList();
            if (sel.Count > 0) DeleteSelected(c, sel);
        }));
        items.Add(MenuItem.Of(L.T("desktop.rename"), () => BeginRename(icon),
                              enabled: icon.Node == null || VirtualFS.CanRename(icon.Node)));
        items.Add(MenuItem.Sep());
        items.Add(MenuItem.Of(L.T("desktop.properties"),
            () => _shell.ShowProperties(c, icon.Label, icon.Node, icon.Icon)));

        _shell.Menus.Open(items, c.MouseX, c.MouseY, this, c);
    }

    void ShowDesktopMenu(UiContext c)
    {
        var arrange = new List<MenuItem>
        {
            MenuItem.Of(L.T("desktop.name"), () => { SortBy(i => i.Label); Relayout(c.ScreenW, c.ScreenH); }),
            MenuItem.Of(L.T("desktop.size"), () => { SortBy(i => (i.Node?.Size ?? 0).ToString("D12")); Relayout(c.ScreenW, c.ScreenH); }),
            MenuItem.Of(L.T("desktop.type"), () => { SortBy(i => i.Icon.ToString()); Relayout(c.ScreenW, c.ScreenH); }),
            MenuItem.Sep(),
            MenuItem.Of(L.T("desktop.align_to_grid"), () => Relayout(c.ScreenW, c.ScreenH)),
        };

        var create = new List<MenuItem>
        {
            MenuItem.Of(L.T("desktop.folder"), () => CreateOnDesktop(c, NodeKind.Folder)),
            MenuItem.Of(L.T("desktop.shortcut"), () => CreateOnDesktop(c, NodeKind.Shortcut)),
            MenuItem.Sep(),
            MenuItem.Of(L.T("desktop.text_document"), () => CreateOnDesktop(c, NodeKind.TextFile)),
            MenuItem.Of(L.T("desktop.bitmap_image"), () => CreateOnDesktop(c, NodeKind.ImageFile)),
            MenuItem.Of(L.T("desktop.microsoft_excel_worksheet"), () => CreateOnDesktop(c, NodeKind.Spreadsheet)),
            MenuItem.Of(L.T("desktop.microsoft_word_document"), () => CreateOnDesktop(c, NodeKind.Document)),
            MenuItem.Of(L.T("desktop.winrar_archive"), () => CreateOnDesktop(c, NodeKind.Archive)),
        };

        var items = new List<MenuItem>
        {
            MenuItem.Sub(L.T("desktop.arrange_icons_by"), arrange),
            MenuItem.Of(L.T("desktop.refresh"), () => { Relayout(c.ScreenW, c.ScreenH); c.Sound(Sfx.Navigate, 0.4f); }),
            MenuItem.Sep(),
            MenuItem.Of(L.T("desktop.paste"), null, enabled: false),
            MenuItem.Of(L.T("desktop.paste_shortcut"), null, enabled: false),
            MenuItem.Sep(),
            MenuItem.Sub(L.T("desktop.new"), create),
            MenuItem.Sep(),
            MenuItem.Of(L.T("desktop.properties"), () => _shell.Launch(c, "display", null), IconId.Display),
        };

        _shell.Menus.Open(items, c.MouseX, c.MouseY, this, c);
    }

    void SortBy(Func<DesktopIcon, string> key)
    {
        var sorted = Icons.OrderBy(key, StringComparer.CurrentCulture).ToList();
        Icons.Clear();
        Icons.AddRange(sorted);
    }

    void CreateOnDesktop(UiContext c, NodeKind kind)
    {
        (string key, IconId icon, string launch) spec = kind switch
        {
            NodeKind.Folder => ("newitem.new_folder", IconId.Folder, "explorer"),
            NodeKind.Shortcut => ("newitem.new_shortcut", IconId.Program, null),
            NodeKind.TextFile => ("newitem.new_text_document_txt", IconId.TextFile, "notepad"),
            NodeKind.ImageFile => ("newitem.new_bitmap_image_bmp", IconId.ImageFile, "paint"),
            NodeKind.Spreadsheet => ("newitem.new_microsoft_excel_worksheet_xls", IconId.Spreadsheet, "spreadsheet"),
            NodeKind.Document => ("newitem.new_microsoft_word_document_doc", IconId.WordDoc, null),
            _ => ("newitem.new_winrar_archive_rar", IconId.Archive, null),
        };

        string name = L.T(spec.key);
        var node = _shell.Fs.CreateChild(_shell.Fs.Desktop, name, kind, spec.icon);
        if (spec.launch == "notepad") node.Launch = "notepad";

        Icons.Add(new DesktopIcon
        {
            LabelKey = spec.key, Icon = spec.icon,
            Node = node, Launch = spec.launch,
        });
        Relayout(c.ScreenW, c.ScreenH);
        c.Sound(Sfx.Navigate, 0.5f);
    }
}
