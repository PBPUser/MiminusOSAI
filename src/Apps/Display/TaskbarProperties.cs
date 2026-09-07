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
/// The one thing that does not work is the classic Start menu, which МИМИНУС
/// never had — so it says so rather than pretending.</summary>
public sealed class TaskbarPropertiesWindow : OsWindow
{
    readonly ShellHost _shell;
    int _tab;
    int _startStyle;         // 0 = МИМИНУС, 1 = classic

    // The sheet is modal in spirit: OK applies, Cancel puts everything back.
    readonly Snapshot _entry;

    readonly struct Snapshot
    {
        public readonly bool Lock, AutoHide, OnTop, Group, Quick, Clock, HideIcons;

        public Snapshot(ShellSettings s)
        {
            Lock = s.LockTaskbar;
            AutoHide = s.AutoHideTaskbar;
            OnTop = s.TaskbarOnTop;
            Group = s.GroupSimilar;
            Quick = s.ShowQuickLaunch;
            Clock = s.ShowClock;
            HideIcons = s.HideInactiveIcons;
        }

        public void RestoreTo(ShellSettings s)
        {
            s.LockTaskbar = Lock;
            s.AutoHideTaskbar = AutoHide;
            s.TaskbarOnTop = OnTop;
            s.GroupSimilar = Group;
            s.ShowQuickLaunch = Quick;
            s.ShowClock = Clock;
            s.HideInactiveIcons = HideIcons;
        }
    }

    public override string Title => L.T("tbprops.title");
    public override float MinWidth => 400;
    public override float MinHeight => 430;

    public TaskbarPropertiesWindow(ShellHost shell)
    {
        _shell = shell;
        _entry = new Snapshot(shell.Settings);
        Icon = IconId.Settings;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 420, 452);
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

        var box = page.CutTop(150);
        W.GroupBox(c, box, L.T("tbprops.taskbar_appearance"));
        var inner = box.Deflate(12);
        inner.CutTop(12);

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
        Check(c, ref inner, ".hideicons", L.T("tbprops.hide_inactive_icons"), ref s.HideInactiveIcons);
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

        var box = page.CutTop(126);
        W.GroupBox(c, box, L.T("tbprops.start_menu_style"));
        var inner = box.Deflate(12);
        inner.CutTop(12);

        var row = inner.CutTop(20);
        if (W.Radio(c, Id + ".xp", new Rect(row.X, row.Y, row.W, 18),
                    L.T("tbprops.miminus_start_menu"), _startStyle == 0))
            _startStyle = 0;
        c.F.Small.Draw(c.R, L.T("tbprops.miminus_start_menu_note"), row.X + 20, row.Y + 20, t.TextDisabled);
        inner.CutTop(24);

        row = inner.CutTop(20);
        // The classic menu is offered and refused: this OS has only ever had
        // the one, and says so instead of pretending to switch.
        if (W.Radio(c, Id + ".classic", new Rect(row.X, row.Y, row.W, 18),
                    L.T("tbprops.classic_start_menu"), _startStyle == 1))
        {
            _shell.MessageBox(c, L.T("tbprops.title"), L.T("tbprops.classic_not_available"),
                              MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
        }
        c.F.Small.Draw(c.R, L.T("tbprops.classic_start_menu_note"), row.X + 20, row.Y + 20, t.TextDisabled);

        page.CutTop(10);

        var privacy = page.CutTop(96);
        W.GroupBox(c, privacy, L.T("tbprops.privacy"));
        inner = privacy.Deflate(12);
        inner.CutTop(12);
        c.F.Ui.Draw(c.R, L.T("tbprops.privacy_note"), inner.X, inner.Y, t.Text);
        c.F.Small.Draw(c.R, L.T("tbprops.privacy_detail"), inner.X, inner.Y + c.F.Ui.Height + 6,
                       t.TextDisabled);
    }

    void Check(UiContext c, ref Rect area, string id, string label, ref bool value)
    {
        var row = area.CutTop(22);
        if (W.CheckBox(c, Id + id, new Rect(row.X, row.Y, row.W, 18), label, ref value))
            c.Sound(Sfx.Click, 0.5f);
    }
}
