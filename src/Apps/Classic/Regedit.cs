using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Редактор реестра» — the two-pane window, with the tree of keys on
/// the left and the values of the selected one on the right.
///
/// It edits the real store. Changing
/// <c>HKEY_CURRENT_USER\Software\Miminus\Appearance\Accent</c> here repaints the
/// system before the dialog has finished closing, because that value is where
/// the interface colour actually lives — the theme is built out of it, and the
/// registry tells the shell when it moves. It is the one program in the system
/// that can change the look without having a picture of a colour in it.</summary>
public sealed class RegeditWindow : OsWindow
{
    public override string Title => L.T("regedit.title");
    public override float MinWidth => 520;
    public override float MinHeight => 340;

    public RegeditWindow()
    {
        Icon = IconId.Registry;
        Bounds = new Rect(0, 0, 720, 460);
        BuildMenu();
    }

    /// <summary>Which keys are open. Everything above the selected key is
    /// opened when the window appears, so it lands somewhere useful.</summary>
    readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);

    string _selected = Registry.Appearance;
    string _selectedValue;
    float _treeScroll, _listScroll;
    float _split = 250;

    /// <summary>The value being edited, and the text being typed into it.</summary>
    string _editing;
    string _editText = "";

    UiContext _ctx;

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("regedit.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("regedit.export"), () => Export(_ctx), IconId.TextFile),
            MenuItem.Sep(),
            MenuItem.Of(L.T("regedit.exit"), Close),
        });

        Menu.Add(L.T("regedit.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("regedit.modify"), () => BeginEdit(_selectedValue),
                        enabled: _selectedValue != null),
            MenuItem.Sep(),
            MenuItem.Of(L.T("regedit.delete"), () => DeleteValue(_ctx),
                        enabled: _selectedValue != null),
        });

        Menu.Add(L.T("regedit.view"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("regedit.expand_all"), OpenEverything),
            MenuItem.Of(L.T("regedit.collapse_all"), () => _open.Clear()),
        });

        Menu.Add(L.T("regedit.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("regedit.about"), () =>
                Shell.MessageBox(_ctx, L.T("regedit.title"), L.T("regedit.about_body"),
                    MsgButtons.Ok, IconId.Settings, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        OpenEverything();
    }

    void OpenEverything()
    {
        foreach (string path in Registry.Paths)
        {
            string walk = "";
            foreach (string part in path.Split('\\'))
            {
                walk = walk.Length == 0 ? part : walk + "\\" + part;
                _open.Add(walk);
            }
        }
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client;
        var status = area.CutBottom(20);

        var tree = area.CutLeft(_split);
        var splitter = area.CutLeft(4);
        DrawSplitter(c, splitter);

        DrawTree(c, tree.Deflate(4, 4, 0, 4));
        DrawValues(c, area.Deflate(4, 4, 4, 4));

        W.StatusBar(c, status, _selected);

        DrawEditor(c, client);
    }

    void DrawSplitter(UiContext c, Rect r)
    {
        if (c.Hovering(r)) c.Cursor = CursorShape.SizeWE;
        if (c.Clicked(r)) c.ActiveDrag = Id + ".split";
        if (c.ActiveDrag == Id + ".split" && c.In.IsDown(MouseButton.Left))
            _split = Math.Clamp(c.MouseX - Bounds.X - 6, 140, Bounds.W - 220);
    }

    // ---- the tree -----------------------------------------------------------

    /// <summary>Rows, flattened: the path each one stands for and how deep it
    /// is. The tree is derived from the paths in the store rather than stored
    /// as a tree, which is the only shape a flat file can round-trip.</summary>
    List<(string path, string name, int depth)> TreeRows()
    {
        var rows = new List<(string, string, int)>();

        void Walk(string parent, int depth)
        {
            foreach (string child in Registry.ChildKeys(parent))
            {
                string path = parent.Length == 0 ? child : parent + "\\" + child;
                rows.Add((path, child, depth));
                if (_open.Contains(path)) Walk(path, depth + 1);
            }
        }

        Walk("", 0);
        return rows;
    }

    void DrawTree(UiContext c, Rect area)
    {
        var t = c.Theme;
        W.SunkenField(c, area);
        var view = area.Deflate(1);
        c.R.FillRect(view, t.FieldBack);

        var rows = TreeRows();
        const float rowH = 20;
        float contentH = rows.Count * rowH;

        if (contentH > view.H)
        {
            var bar = new Rect(view.Right - W.ScrollBarSize, view.Y, W.ScrollBarSize, view.H);
            _treeScroll = W.ScrollBarV(c, Id + ".tree", bar, _treeScroll, contentH, view.H);
            view.W -= W.ScrollBarSize;
        }
        else _treeScroll = 0;

        if (c.Hovering(view) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _treeScroll = Math.Clamp(_treeScroll - c.In.WheelDelta * 40, 0,
                                     MathF.Max(0, contentH - view.H));
            c.In.WheelDelta = 0;
        }

        c.R.PushClip(view);
        for (int i = 0; i < rows.Count; i++)
        {
            var (path, name, depth) = rows[i];
            var row = new Rect(view.X, view.Y - _treeScroll + i * rowH, view.W, rowH);
            if (row.Bottom < view.Y || row.Y > view.Bottom) continue;

            bool sel = string.Equals(path, _selected, StringComparison.OrdinalIgnoreCase);
            if (sel) c.R.FillRect(row, t.Selection);
            else if (c.Hovering(row)) c.R.FillRect(row, t.Hot.WithAlpha((byte)70));

            Color ink = sel ? t.SelectionText : t.Text;
            float x = row.X + 4 + depth * 14;

            // The hinge, when there is anything below.
            bool branches = Registry.ChildKeys(path).Any();
            if (branches)
            {
                var hinge = new Rect(x, row.CenterY - 5, 10, 10);
                c.R.FillRect(hinge, t.FieldBack);
                c.R.DrawRect(hinge, t.ControlBorder);
                c.R.FillRect(new Rect(hinge.X + 2, hinge.CenterY - 0.5f, 6, 1), t.Text);
                if (!_open.Contains(path))
                    c.R.FillRect(new Rect(hinge.CenterX - 0.5f, hinge.Y + 2, 1, 6), t.Text);
            }

            Icons.Draw(c.R, depth == 0 ? IconId.MyComputer : IconId.Folder,
                       new Rect(x + 14, row.CenterY - 8, 16, 16));

            c.R.PushClip(row);
            c.F.Ui.Draw(c.R, name, x + 34, row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.R.PopClip();

            if (c.Clicked(row))
            {
                _selected = path;
                _selectedValue = null;
                _listScroll = 0;
                if (branches)
                {
                    if (!_open.Add(path)) _open.Remove(path);
                }
                c.SoundAt(Sfx.Click, row, 0.3f);
            }
        }
        c.R.PopClip();
    }

    // ---- the values ----------------------------------------------------------

    void DrawValues(UiContext c, Rect area)
    {
        var t = c.Theme;
        W.SunkenField(c, area);
        var view = area.Deflate(1);
        c.R.FillRect(view, t.FieldBack);

        // The three columns the real one has.
        var head = new Rect(view.X, view.Y, view.W, 20);
        c.R.FillRectV(head, Color.White, t.Face);
        c.R.FillRect(new Rect(head.X, head.Bottom - 1, head.W, 1), t.ControlBorder);

        float nameW = MathF.Min(180, view.W * 0.34f);
        float typeW = MathF.Min(110, view.W * 0.22f);

        c.F.Ui.Draw(c.R, L.T("regedit.col_name"), head.X + 6, head.Y + 2, t.Text);
        c.F.Ui.Draw(c.R, L.T("regedit.col_type"), head.X + nameW + 6, head.Y + 2, t.Text);
        c.F.Ui.Draw(c.R, L.T("regedit.col_value"), head.X + nameW + typeW + 6, head.Y + 2, t.Text);

        var list = new Rect(view.X, head.Bottom, view.W, view.Bottom - head.Bottom);
        var values = Registry.Values(_selected).ToList();
        const float rowH = 20;

        if (values.Count * rowH > list.H)
        {
            var bar = new Rect(list.Right - W.ScrollBarSize, list.Y, W.ScrollBarSize, list.H);
            _listScroll = W.ScrollBarV(c, Id + ".list", bar, _listScroll, values.Count * rowH, list.H);
            list.W -= W.ScrollBarSize;
        }
        else _listScroll = 0;

        c.R.PushClip(list);
        for (int i = 0; i < values.Count; i++)
        {
            var (name, value) = values[i];
            var row = new Rect(list.X, list.Y - _listScroll + i * rowH, list.W, rowH);
            if (row.Bottom < list.Y || row.Y > list.Bottom) continue;

            bool sel = name == _selectedValue;
            if (sel) c.R.FillRect(row, t.Selection);
            else if (c.Hovering(row)) c.R.FillRect(row, t.Hot.WithAlpha((byte)70));

            Color ink = sel ? t.SelectionText : t.Text;

            Icons.Draw(c.R, value.Kind == Registry.Kind.Dword ? IconId.Calculator : IconId.TextFile,
                       new Rect(row.X + 4, row.CenterY - 8, 16, 16));

            Cell(c, row, row.X + 24, nameW - 24, name, ink);
            Cell(c, row, row.X + nameW + 6, typeW - 6, value.TypeName, ink);

            // A colour value shows the colour, because a registry that holds
            // one and will not show it is being obtuse.
            float valueX = row.X + nameW + typeW + 6;
            if (value.Kind == Registry.Kind.Colour && value.Text.Length == 6)
            {
                var swatch = new Rect(valueX, row.Y + 4, 22, rowH - 8);
                if (int.TryParse(value.Text, System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture, out int rgb))
                {
                    c.R.FillRect(swatch, Color.Rgb(rgb));
                    c.R.DrawRect(swatch, t.ControlBorder);
                }
                valueX = swatch.Right + 6;
            }
            Cell(c, row, valueX, row.Right - valueX - 4, value.ToString(), ink);

            if (c.Clicked(row)) { _selectedValue = name; c.SoundAt(Sfx.Click, row, 0.3f); }
            else if (c.DoubleClicked(row)) { _selectedValue = name; BeginEdit(name); }
            else if (c.RightClicked(row))
            {
                _selectedValue = name;
                Shell.Menus.Open(new List<MenuItem>
                {
                    MenuItem.Of(L.T("regedit.modify"), () => BeginEdit(name), IconId.TextFile),
                    MenuItem.Sep(),
                    MenuItem.Of(L.T("regedit.delete"), () => DeleteValue(c), IconId.RecycleBin),
                }, c.MouseX, c.MouseY, this, c);
            }
        }
        c.R.PopClip();

        if (values.Count == 0)
            c.F.Ui.Draw(c.R, L.T("regedit.no_values"), list.X + 8, list.Y + 6, t.TextDisabled);
    }

    static void Cell(UiContext c, Rect row, float x, float w, string text, Color ink)
    {
        c.R.PushClip(new Rect(x, row.Y, MathF.Max(0, w), row.H));
        c.F.Ui.Draw(c.R, text, x, row.CenterY - c.F.Ui.Height * 0.5f, ink);
        c.R.PopClip();
    }

    // ---- editing ---------------------------------------------------------------

    void BeginEdit(string name)
    {
        if (name == null) return;
        _editing = name;
        _editText = Registry.Values(_selected).TryGetValue(name, out var v) ? v.Text : "";
    }

    /// <summary>«Изменение параметра» — the small dialog the real one opens,
    /// drawn inside this window rather than as a second one so the value and
    /// the box that edits it stay together.</summary>
    void DrawEditor(UiContext c, Rect client)
    {
        if (_editing == null) return;

        var t = c.Theme;
        var panel = new Rect(client.CenterX - 190, client.CenterY - 70, 380, 140);

        c.R.FillRect(client, Color.Rgba(0x000000, 60));
        c.R.FillRect(panel.Offset(3, 3), Color.Rgba(0x000000, 70));
        c.R.FillRect(panel, t.Face);
        c.R.DrawRect(panel, t.ControlBorder);

        var area = panel.Deflate(14);
        c.F.UiBold.Draw(c.R, L.T("regedit.modify_title"), area.X, area.Y, t.Text);
        area.CutTop(c.F.UiBold.Height + 10);

        c.F.Ui.Draw(c.R, L.T("regedit.value_name"), area.X, area.Y, t.TextDisabled);
        c.F.Ui.Draw(c.R, _editing, area.X + 110, area.Y, t.Text);
        area.CutTop(c.F.Ui.Height + 10);

        c.F.Ui.Draw(c.R, L.T("regedit.value_data"), area.X, area.Y + 4, t.TextDisabled);
        var field = new Rect(area.X + 110, area.Y, area.W - 110, 24);
        W.TextField(c, Id + ".edit", field, ref _editText);

        var buttons = panel.Deflate(14).CutBottom(26);
        if (W.Button(c, Id + ".ok", new Rect(buttons.Right - 176, buttons.Y, 84, 24),
                     L.T("win.ok"), true, IconId.None, true))
            Commit(c);

        if (W.Button(c, Id + ".cancel", new Rect(buttons.Right - 86, buttons.Y, 84, 24),
                     L.T("win.cancel")))
            _editing = null;

        if (!c.KeyboardHandled)
        {
            if (c.In.KeyPressed(Keys.Enter)) Commit(c);
            else if (c.In.KeyPressed(Keys.Escape)) _editing = null;
        }

        // The dialog is modal to this window while it is up.
        c.MouseHandled = true;
        c.KeyboardHandled = true;
    }

    void Commit(UiContext c)
    {
        if (_editing == null) return;

        var kind = Registry.Values(_selected).TryGetValue(_editing, out var old)
            ? old.Kind : Registry.Kind.String;

        // A colour is checked before it is written: six hex digits or nothing,
        // because everything downstream trusts it.
        if (kind == Registry.Kind.Colour &&
            (_editText.Trim().Length != 6 ||
             !int.TryParse(_editText.Trim(), System.Globalization.NumberStyles.HexNumber,
                           System.Globalization.CultureInfo.InvariantCulture, out _)))
        {
            Shell.MessageBox(c, L.T("regedit.title"), L.T("regedit.bad_colour"),
                             MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
            return;
        }

        Registry.Set(_selected, _editing, new Registry.Value(kind, _editText.Trim()));
        _editing = null;
        c.Sound(Sfx.Click, 0.5f);
    }

    void DeleteValue(UiContext c)
    {
        if (_selectedValue == null) return;

        string name = _selectedValue;
        Shell.MessageBox(c, L.T("regedit.delete"), L.F("regedit.delete_body", name),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion, result =>
            {
                if (result != MsgResult.Yes) return;
                Registry.Delete(_selected, name);
                _selectedValue = null;
            }, Sfx.Question);
    }

    /// <summary>«Экспорт» writes the registry out as a text file in the virtual
    /// filesystem, where Notepad can open it — which is the only export this
    /// system has any use for.</summary>
    void Export(UiContext c)
    {
        Registry.Flush();

        var lines = new List<string> { "МИМИНУС ОС — реестр", "" };
        foreach (string path in Registry.Paths)
        {
            lines.Add("[" + path + "]");
            foreach (var (name, value) in Registry.Values(path))
                lines.Add("    " + name + " : " + value.TypeName + " = " + value.Text);
            lines.Add("");
        }

        var file = Shell.Fs.CreateChild(Shell.Fs.MyDocuments, "registry.txt",
                                        NodeKind.TextFile, IconId.TextFile);
        file.Text = string.Join("\n", lines);

        Shell.Launch(c, "notepad", file);
    }
}
