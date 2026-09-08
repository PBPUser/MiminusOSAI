using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Свойства панели задач и меню "Пуск"» — the taskbar property sheet.
///
/// Two tabs, as in XP, with a picture of the taskbar above the checkboxes.
/// Everything here changes the shell for real: hiding the quick launch really
/// removes it, auto-hide really slides the bar away and gives the space back to
/// maximised windows, and "keep on top" is the order the layers are painted in.
/// Version 8 gave the second tab something real to do: the system now has two
/// Start interfaces, the menu and the tile board, and the radio pair there
/// decides which of them the Start button opens. Neither is ever taken away —
/// the menu lists the board, and the board's app bar lists the menu.</summary>
public sealed class TaskbarPropertiesWindow : OsWindow
{
    readonly ShellHost _shell;
    int _tab;

    // The sheet is modal in spirit: OK applies, Cancel puts everything back.
    readonly Snapshot _entry;

    readonly struct Snapshot
    {
        public readonly bool Lock, AutoHide, OnTop, Group, Quick, Clock, HideIcons;
        public readonly bool StartScreen, Corners, LockScreen;
        public readonly TaskbarEdge Edge;
        public readonly int Size;

        public Snapshot(ShellSettings s)
        {
            Edge = s.TaskbarEdge;
            Size = s.TaskbarSize;
            Lock = s.LockTaskbar;
            AutoHide = s.AutoHideTaskbar;
            OnTop = s.TaskbarOnTop;
            Group = s.GroupSimilar;
            Quick = s.ShowQuickLaunch;
            Clock = s.ShowClock;
            HideIcons = s.HideInactiveIcons;
            StartScreen = s.UseStartScreen;
            Corners = s.HotCorners;
            LockScreen = s.ShowLockScreen;
        }

        public void RestoreTo(ShellSettings s)
        {
            s.TaskbarEdge = Edge;
            s.TaskbarSize = Size;
            s.LockTaskbar = Lock;
            s.AutoHideTaskbar = AutoHide;
            s.TaskbarOnTop = OnTop;
            s.GroupSimilar = Group;
            s.ShowQuickLaunch = Quick;
            s.ShowClock = Clock;
            s.HideInactiveIcons = HideIcons;
            s.UseStartScreen = StartScreen;
            s.HotCorners = Corners;
            s.ShowLockScreen = LockScreen;
        }
    }

    public override string Title => L.T("tbprops.title");
    public override float MinWidth => 400;
    public override float MinHeight => 500;

    public TaskbarPropertiesWindow(ShellHost shell)
    {
        _shell = shell;
        _entry = new Snapshot(shell.Settings);
        Icon = IconId.Settings;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 440, 520);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client.Deflate(10);
        var footer = area.CutBottom(30);

        _tab = W.Tabs(c, Id + ".tabs", area,
                      new[] { L.T("tbprops.taskbar"), L.T("tbprops.start_menu") }, _tab, out var page);
        page = page.Deflate(10);

        if (_tab == 0) DrawTaskbarPage(c, page);
        else DrawStartPage(c, page);

        // OK / Cancel / Apply, and Apply is redundant because every checkbox
        // takes effect the moment it is ticked.
        var apply = new Rect(footer.Right - 84, footer.Y, 84, 24);
        W.Button(c, Id + ".apply", apply, L.T("dlg.apply"), enabled: false);

        var cancel = new Rect(apply.X - 84 - 6, footer.Y, 84, 24);
        if (W.Button(c, Id + ".cancel", cancel, L.T("dlg.cancel")))
        {
            _entry.RestoreTo(_shell.Settings);
            Close();
        }

        var ok = new Rect(cancel.X - 84 - 6, footer.Y, 84, 24);
        if (W.Button(c, Id + ".ok", ok, L.T("dlg.ok"), defaultButton: true)) Close();
    }

    // ---- «Панель задач» ---------------------------------------------------

    void DrawTaskbarPage(UiContext c, Rect page)
    {
        var s = _shell.Settings;

        DrawPreview(c, page.CutTop(64));
        page.CutTop(10);

        var box = page.CutTop(182);
        W.GroupBox(c, box, L.T("tbprops.taskbar_appearance"));
        var inner = box.Deflate(12);
        inner.CutTop(12);

        // Where the bar lives. It has always been draggable to any of the four
        // sides, and this is the same choice written down — the drag and the
        // combo set the same thing.
        var where = inner.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("tbprops.position"), where.X, where.Y + 4, c.Theme.Text);

        var edges = new List<string>
        {
            L.T("tbprops.edge_bottom"), L.T("tbprops.edge_top"),
            L.T("tbprops.edge_left"), L.T("tbprops.edge_right"),
        };
        int edge = (int)s.TaskbarEdge;
        if (W.ComboBox(c, Id + ".edge", new Rect(where.X + 190, where.Y, 150, 22), edges, ref edge))
            s.TaskbarEdge = (TaskbarEdge)edge;

        // How thick it is, next to where it is.
        var size = inner.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("tbprops.size"), size.X, size.Y + 4, c.Theme.Text);

        var sizes = new List<string>
        {
            L.T("tbprops.size_small"), L.T("tbprops.size_normal"), L.T("tbprops.size_large"),
        };
        int step = Math.Clamp(s.TaskbarSize, 0, 2);
        if (W.ComboBox(c, Id + ".size", new Rect(size.X + 190, size.Y, 150, 22), sizes, ref step))
            s.TaskbarSize = step;

        inner.CutTop(4);

        Check(c, ref inner, ".lock", L.T("tbprops.lock_the_taskbar"), ref s.LockTaskbar);
        Check(c, ref inner, ".autohide", L.T("tbprops.auto_hide"), ref s.AutoHideTaskbar);
        Check(c, ref inner, ".ontop", L.T("tbprops.keep_on_top"), ref s.TaskbarOnTop);
        Check(c, ref inner, ".group", L.T("tbprops.group_similar"), ref s.GroupSimilar);
        Check(c, ref inner, ".quick", L.T("tbprops.show_quick_launch"), ref s.ShowQuickLaunch);

        page.CutTop(10);

        var tray = page.CutTop(96);
        W.GroupBox(c, tray, L.T("tbprops.notification_area"));
        inner = tray.Deflate(12);
        inner.CutTop(12);

        Check(c, ref inner, ".clock", L.T("tbprops.show_the_clock"), ref s.ShowClock);
        // Hiding them all at once is a switch; moving them one at a time is a
        // drag, and happens in the bar itself.
        var hideRow = inner.CutTop(22);
        bool hideAll = s.HideInactiveIcons;
        if (W.CheckBox(c, Id + ".hideicons", new Rect(hideRow.X, hideRow.Y, hideRow.W, 20),
                       L.T("tbprops.hide_inactive_icons"), ref hideAll))
            s.HideInactiveIcons = hideAll;
        c.F.Small.Draw(c.R, L.T("tbprops.notification_note"), inner.X, inner.Y + 4,
                       c.Theme.TextDisabled);
    }

    /// <summary>A small painting of the taskbar, which XP puts at the top of
    /// this sheet and which here follows the settings as they are changed.</summary>
    void DrawPreview(UiContext c, Rect frame)
    {
        var t = c.Theme;
        var s = _shell.Settings;

        c.R.FillRect(frame, Color.Rgb(0x2A6099));
        c.R.DrawRect(frame, t.FieldBorder);

        // A stripe on whichever side the bar is on, so the sheet says where it
        // is going before the bar gets there.
        var hint = s.TaskbarEdge switch
        {
            TaskbarEdge.Top => new Rect(frame.X + 1, frame.Y + 1, frame.W - 2, 5),
            TaskbarEdge.Left => new Rect(frame.X + 1, frame.Y + 1, 7, frame.H - 2),
            TaskbarEdge.Right => new Rect(frame.Right - 8, frame.Y + 1, 7, frame.H - 2),
            _ => new Rect(frame.X + 1, frame.Bottom - 6, frame.W - 2, 5),
        };
        c.R.FillRect(hint, t.TaskbarMid);

        var bar = new Rect(frame.X + 6, frame.Bottom - 24, frame.W - 12, 18);
        c.R.FillRectV(bar, t.TaskbarTop, t.TaskbarBottom);

        float x = bar.X + 2;
        var start = new Rect(x, bar.Y + 2, 40, bar.H - 4);
        c.R.RoundedRectV(start, 2, t.StartTop, t.StartBottom);
        c.F.Small.DrawCentered(c.R, L.T("tbprops.start"), start, Color.White);
        x = start.Right + 4;

        if (s.ShowQuickLaunch)
        {
            for (int i = 0; i < 3; i++)
            {
                Icons.Draw(c.R, i switch { 0 => IconId.Firefox, 1 => IconId.Notepad, _ => IconId.MediaPlayer },
                           new Rect(x, bar.CenterY - 6, 12, 12));
                x += 14;
            }
            x += 4;
        }

        // Two windows of one program, so grouping is visible in the picture.
        float bw = s.GroupSimilar ? 54 : 40;
        for (int i = 0; i < (s.GroupSimilar ? 1 : 2); i++)
        {
            var b = new Rect(x, bar.Y + 2, bw, bar.H - 4);
            c.R.RoundedRectV(b, 2, t.TaskButtonFace, t.TaskButtonFace.Shade(0.85f));
            c.F.Small.Draw(c.R, s.GroupSimilar ? "2  " + L.T("tbprops.preview_app") : L.T("tbprops.preview_app"),
                           b.X + 3, b.CenterY - c.F.Small.Height * 0.5f, t.TaskbarText);
            x += bw + 3;
        }

        float trayW = s.ShowClock ? 56 : 34;
        var trayRect = new Rect(bar.Right - trayW, bar.Y + 1, trayW - 2, bar.H - 2);
        c.R.FillRectV(trayRect, t.TrayBack, t.TrayBack.Shade(0.85f));
        if (s.ShowClock)
            c.F.Small.DrawRight(c.R, L.Time(_shell.Now),
                                new Rect(trayRect.X, trayRect.Y, trayRect.W - 4, trayRect.H), t.TaskbarText);
    }

    // ---- «Меню Пуск» ------------------------------------------------------

    void DrawStartPage(UiContext c, Rect page)
    {
        var t = c.Theme;
        var s = _shell.Settings;

        // The real choice version 8 argued about: a menu, or a screenful of
        // tiles. Both are kept whichever way this is set — the loser is still
        // one click away inside the winner.
        var box = page.CutTop(150);
        W.GroupBox(c, box, L.T("tbprops.start_menu_style"));
        var inner = box.Deflate(12);
        inner.CutTop(12);

        var row = inner.CutTop(20);
        if (W.Radio(c, Id + ".menu", new Rect(row.X, row.Y, row.W, 18),
                    L.T("tbprops.miminus_start_menu"), !s.UseStartScreen))
            s.UseStartScreen = false;
        Note(c, ref inner, 20, "tbprops.miminus_start_menu_note");

        row = inner.CutTop(20);
        if (W.Radio(c, Id + ".screen", new Rect(row.X, row.Y, row.W, 18),
                    L.T("tbprops.start_screen"), s.UseStartScreen))
            s.UseStartScreen = true;
        Note(c, ref inner, 20, "tbprops.start_screen_note");

        inner.CutTop(2);
        Note(c, ref inner, 0, "tbprops.both_note");

        page.CutTop(10);

        var eight = page.CutTop(112);
        W.GroupBox(c, eight, L.T("tbprops.edges"));
        inner = eight.Deflate(12);
        inner.CutTop(12);

        Check(c, ref inner, ".corners", L.T("pcs.hot_corners"), ref s.HotCorners);
        Check(c, ref inner, ".lockscreen", L.T("pcs.show_lock_screen"), ref s.ShowLockScreen);
        inner.CutTop(4);
        Note(c, ref inner, 0, "tbprops.edges_note");

        page.CutTop(10);

        var privacy = page.CutTop(86);
        W.GroupBox(c, privacy, L.T("tbprops.privacy"));
        inner = privacy.Deflate(12);
        inner.CutTop(12);
        c.F.Ui.Draw(c.R, L.T("tbprops.privacy_note"), inner.X, inner.Y, t.Text);
        c.F.Small.Draw(c.R, L.T("tbprops.privacy_detail"), inner.X, inner.Y + c.F.Ui.Height + 6,
                       t.TextDisabled);
    }

    /// <summary>A grey explanatory line under a control, wrapped to whatever
    /// room the group box has left rather than running out of the side of it.</summary>
    static void Note(UiContext c, ref Rect area, float indent, string key)
    {
        foreach (string line in c.F.Small.Wrap(L.T(key), area.W - indent))
        {
            var row = area.CutTop(c.F.Small.Height + 1);
            c.F.Small.Draw(c.R, line, row.X + indent, row.Y, c.Theme.TextDisabled);
        }
    }

    void Check(UiContext c, ref Rect area, string id, string label, ref bool value)
    {
        var row = area.CutTop(22);
        if (W.CheckBox(c, Id + id, new Rect(row.X, row.Y, row.W, 18), label, ref value))
            c.Sound(Sfx.Click, 0.5f);
    }
}
