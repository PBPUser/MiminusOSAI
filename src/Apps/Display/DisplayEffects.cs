using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Эффекты» — the dialog behind Display Properties → Оформление.
///
/// XP put the font smoothing method here, alongside the menu animation, large
/// icons, menu shadows and the drag behaviour, and so does this. Every switch is
/// wired to something the shell really does.</summary>
public sealed class EffectsWindow : OsWindow
{
    readonly ShellSettings _s;

    int _transition;
    int _smoothing;

    public override string Title => L.T("effects.title");
    public override float MinWidth => 420;
    public override float MinHeight => 320;

    public EffectsWindow(ShellSettings settings)
    {
        _s = settings;
        Icon = IconId.Display;
        Modal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 460, 340);

        _transition = _s.MenuFade ? 0 : 1;
        _smoothing = _s.Smoothing switch
        {
            FontSmoothing.None => 2,
            FontSmoothing.ClearType => 1,
            _ => 0,
        };
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(14);
        var buttons = area.CutBottom(36);

        // ---- menu transition --------------------------------------------
        var row = area.CutTop(24);
        bool transition = _s.MenuTransition;
        W.CheckBox(c, Id + ".trans", new Rect(row.X, row.Y, row.W, 20),
                   L.T("effects.menu_transition"), ref transition);
        _s.MenuTransition = transition;

        row = area.CutTop(28);
        int before = _transition;
        W.ComboBox(c, Id + ".transkind", new Rect(row.X + 22, row.Y, 190, 22),
                   new List<string> { L.T("effects.fade"), L.T("effects.scroll") }, ref _transition);
        if (_transition != before) _s.MenuFade = _transition == 0;
        area.CutTop(6);

        // ---- font smoothing ---------------------------------------------
        row = area.CutTop(24);
        bool smoothOn = _s.Smoothing != FontSmoothing.None;
        if (W.CheckBox(c, Id + ".smooth", new Rect(row.X, row.Y, row.W, 20),
                       L.T("effects.font_smoothing"), ref smoothOn))
        {
            _s.Smoothing = smoothOn ? FontSmoothing.Standard : FontSmoothing.None;
            _smoothing = smoothOn ? 0 : 2;
        }

        row = area.CutTop(28);
        int sBefore = _smoothing;
        W.ComboBox(c, Id + ".smoothkind", new Rect(row.X + 22, row.Y, 190, 22),
                   new List<string>
                   {
                       L.T("effects.smoothing_standard"),
                       L.T("effects.smoothing_cleartype"),
                       L.T("effects.smoothing_none"),
                   }, ref _smoothing);
        if (_smoothing != sBefore)
        {
            _s.Smoothing = _smoothing switch
            {
                1 => FontSmoothing.ClearType,
                2 => FontSmoothing.None,
                _ => FontSmoothing.Standard,
            };
            c.Sound(Sfx.Click, 0.5f);
        }

        // A sample line so the effect of the setting is visible immediately.
        var sample = area.CutTop(30);
        W.SunkenField(c, new Rect(sample.X + 22, sample.Y, sample.W - 22, 26));
        c.F.Ui.Draw(c.R, L.T("effects.sample_text"), sample.X + 28, sample.Y + 6, c.Theme.Text);
        area.CutTop(4);

        // ---- the remaining switches -------------------------------------
        row = area.CutTop(24);
        bool large = _s.LargeIcons;
        if (W.CheckBox(c, Id + ".large", new Rect(row.X, row.Y, row.W, 20),
                       L.T("effects.large_icons"), ref large))
        {
            _s.LargeIcons = large;
            Shell.Desktop.Relayout(c.ScreenW, c.ScreenH);
        }

        row = area.CutTop(24);
        bool shadows = _s.MenuShadows;
        W.CheckBox(c, Id + ".shadow", new Rect(row.X, row.Y, row.W, 20),
                   L.T("effects.menu_shadows"), ref shadows);
        _s.MenuShadows = shadows;

        row = area.CutTop(24);
        bool dragContents = _s.ShowWindowContentsWhileDragging;
        W.CheckBox(c, Id + ".dragcontents", new Rect(row.X, row.Y, row.W, 20),
                   L.T("effects.show_window_contents"), ref dragContents);
        _s.ShowWindowContentsWhileDragging = dragContents;

        row = area.CutTop(24);
        bool hideKeys = _s.HideAccessKeys;
        W.CheckBox(c, Id + ".accesskeys", new Rect(row.X, row.Y, row.W, 20),
                   L.T("effects.hide_access_keys"), ref hideKeys);
        _s.HideAccessKeys = hideKeys;

        float bw = 84, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 6, bw, 24), L.T("dlg.ok"), true, IconId.None, true))
            Close();
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 6, bw, 24), L.T("dlg.cancel")))
            Close();
    }
}

/// <summary>«Дополнительное оформление» — XP's per-element appearance editor:
/// pick an element, set its size and colour, and watch the preview change.</summary>
public sealed class AdvancedAppearanceWindow : OsWindow
{
    int _element;
    int _size = 18;
    int _colorIndex;
    int _fontSize = 11;

    static readonly string[] ElementKeys =
    {
        "appearance.element_desktop", "appearance.element_active_caption",
        "appearance.element_inactive_caption", "appearance.element_menu",
        "appearance.element_window", "appearance.element_selected_items",
        "appearance.element_tooltip", "appearance.element_scrollbar",
    };

    static readonly Color[] Swatches =
    {
        Color.Rgb(0x0A50C8), Color.Rgb(0x8FA84E), Color.Rgb(0x9295A8),
        Color.Rgb(0x3A6EA5), Color.Rgb(0xECE9D8), Color.Rgb(0xFFFFFF),
        Color.Rgb(0x316AC5), Color.Rgb(0x202020),
    };

    public override string Title => L.T("appearance.title");
    public override float MinWidth => 420;
    public override float MinHeight => 380;

    public AdvancedAppearanceWindow()
    {
        Icon = IconId.Display;
        Modal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 460, 420);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);
        var area = client.Deflate(14);
        var buttons = area.CutBottom(36);

        // ---- preview -----------------------------------------------------
        var preview = area.CutTop(150);
        c.R.FillRect(preview, Color.Rgb(0x3A6EA5));
        c.R.DrawRect(preview, t.ControlBorder);

        var inactive = new Rect(preview.X + 14, preview.Y + 12, preview.W - 80, 46);
        c.R.FillRect(inactive, t.FrameOuter.WithAlpha((byte)170));
        c.R.FillRectV(new Rect(inactive.X, inactive.Y, inactive.W, 18),
                      t.CaptionInactiveTop, t.CaptionInactiveBottom);
        c.F.Caption.Draw(c.R, L.T("appearance.inactive_window"), inactive.X + 6, inactive.Y + 2,
                         t.CaptionTextInactive);
        c.R.FillRect(new Rect(inactive.X + 3, inactive.Y + 18, inactive.W - 6, inactive.H - 21), t.Face);

        var active = new Rect(preview.X + 40, preview.Y + 46, preview.W - 80, 88);
        c.R.FillRect(active.Offset(3, 3), Color.Rgba(0x000000, 60));
        c.R.FillRect(active, t.FrameOuter);
        c.R.FillRectV(new Rect(active.X, active.Y, active.W, _size),
                      t.CaptionActiveTop, t.CaptionActiveBottom);
        c.F.Caption.Draw(c.R, L.T("appearance.active_window"), active.X + 6, active.Y + 3,
                         t.CaptionTextActive);
        var face = new Rect(active.X + 3, active.Y + _size, active.W - 6, active.Bottom - active.Y - _size - 3);
        c.R.FillRect(face, t.Face);
        c.F.Ui.Draw(c.R, L.T("appearance.window_text"), face.X + 8, face.Y + 6, t.Text);
        c.R.FillRect(new Rect(face.X + 8, face.Y + 24, 110, 16), Swatches[_colorIndex]);
        c.F.Ui.Draw(c.R, L.T("appearance.selected"), face.X + 12, face.Y + 25, Color.White);

        area.CutTop(10);

        // ---- element / size / colour -------------------------------------
        float labelW = 96;
        var row = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("appearance.item"), row.X, row.Y + 4, t.Text);
        W.ComboBox(c, Id + ".element", new Rect(row.X + labelW, row.Y, 200, 22),
                   ElementKeys.Select(L.T).ToList(), ref _element);

        var sizeBox = new Rect(row.X + labelW + 214, row.Y, 60, 22);
        c.F.Ui.Draw(c.R, L.T("appearance.size"), sizeBox.X - 42, row.Y + 4, t.Text);
        W.SunkenField(c, sizeBox);
        c.F.Ui.DrawCentered(c.R, _size.ToString(), sizeBox, t.Text);
        var up = new Rect(sizeBox.Right - 16, sizeBox.Y + 1, 15, 10);
        var down = new Rect(sizeBox.Right - 16, sizeBox.CenterY, 15, 10);
        if (W.FlatButton(c, Id + ".sizeup", up, null)) _size = Math.Min(40, _size + 1);
        if (W.FlatButton(c, Id + ".sizedown", down, null)) _size = Math.Max(12, _size - 1);
        W.Arrow(c, up, 0);
        W.Arrow(c, down, 2);

        area.CutTop(6);
        row = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("appearance.color"), row.X, row.Y + 6, t.Text);
        for (int i = 0; i < Swatches.Length; i++)
        {
            var sw = new Rect(row.X + labelW + i * 26, row.Y, 22, 22);
            c.R.FillRect(sw, Swatches[i]);
            c.R.DrawRect(sw, i == _colorIndex ? Color.Black : t.ControlBorder,
                         i == _colorIndex ? 2 : 1);
            if (c.Clicked(sw)) { _colorIndex = i; c.SoundAt(Sfx.Tick, sw, 0.3f); }
        }

        area.CutTop(6);
        row = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("appearance.font_size"), row.X, row.Y + 4, t.Text);
        float fs = _fontSize;
        if (W.Slider(c, Id + ".fontsize", new Rect(row.X + labelW, row.Y + 2, 200, 20), ref fs, 8, 18))
            _fontSize = (int)MathF.Round(fs);
        c.F.Ui.Draw(c.R, _fontSize + " pt", row.X + labelW + 210, row.Y + 4, t.Text);

        float bw = 84, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 6, bw, 24), L.T("dlg.ok"), true, IconId.None, true))
            Close();
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 6, bw, 24), L.T("dlg.cancel")))
            Close();
    }
}

/// <summary>The Advanced sheet behind the Settings tab: the four tabs XP showed
/// for the adapter and monitor, filled in with МИМИНУС hardware.</summary>
public sealed class AdvancedSettingsWindow : OsWindow
{
    readonly ShellSettings _settings;

    /// <summary>The rates offered, and the cap each one applies.</summary>
    static readonly int[] Rates = { 30, 60, 75, 144, 240, 0 };

    /// <summary>The scales offered, in dots per inch.</summary>
    static readonly int[] Dpis = { 96, 120, 144 };

    int _tab;
    bool _hwAccelFull = true;

    public AdvancedSettingsWindow(ShellSettings settings)
    {
        _settings = settings;
        Icon = IconId.Display;
        Modal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 450, 420);
    }

    public override string Title => L.T("advanced.title");
    public override float MinWidth => 420;
    public override float MinHeight => 380;

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(10);
        var buttons = area.CutBottom(36);

        string[] tabs =
        {
            L.T("advanced.tab_general"), L.T("advanced.tab_adapter"),
            L.T("advanced.tab_monitor"), L.T("advanced.tab_troubleshoot"),
        };
        _tab = W.Tabs(c, Id + ".tabs", area, tabs, _tab, out var body);
        body = body.Deflate(14);

        switch (_tab)
        {
            case 0: DrawGeneral(c, body); break;
            case 1: DrawAdapter(c, body); break;
            case 2: DrawMonitor(c, body); break;
            default: DrawTroubleshoot(c, body); break;
        }

        float bw = 84, gap = 8;
        float x = buttons.Right - bw * 2 - gap;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 6, bw, 24), L.T("dlg.ok"), true, IconId.None, true))
            Close();
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 6, bw, 24), L.T("dlg.cancel")))
            Close();
    }

    void DrawGeneral(UiContext c, Rect body)
    {
        W.GroupBox(c, new Rect(body.X, body.Y, body.W, 96), L.T("advanced.display_scale"));
        var inner = new Rect(body.X + 14, body.Y + 28, body.W - 28, 60);
        c.F.Ui.Draw(c.R, L.T("advanced.dpi_setting"), inner.X, inner.Y + 4, c.Theme.Text);

        int dpi = Math.Max(0, Array.IndexOf(Dpis, _settings.Dpi));
        var names = Dpis.Select(d => L.F(d == 96 ? "advanced.dpi_normal" : "advanced.dpi_scaled", d)).ToList();

        // Changing it here changes the size of everything on screen from the
        // next frame, this window included.
        if (W.ComboBox(c, Id + ".dpi", new Rect(inner.X, inner.Y + 24, 240, 22), names, ref dpi))
            _settings.Dpi = Dpis[Math.Clamp(dpi, 0, Dpis.Length - 1)];

        var note = new Rect(body.X, body.Y + 110, body.W, body.H - 110);
        foreach (string line in c.F.Ui.Wrap(L.T("advanced.compatibility_note"), note.W))
        {
            c.F.Ui.Draw(c.R, line, note.X, note.Y, c.Theme.TextDisabled);
            note.CutTop(c.F.Ui.Height + 3);
        }
    }

    void DrawAdapter(UiContext c, Rect body)
    {
        W.GroupBox(c, new Rect(body.X, body.Y, body.W, 130), L.T("advanced.adapter_type"));
        var inner = new Rect(body.X + 14, body.Y + 28, body.W - 28, 100);
        c.F.UiBold.Draw(c.R, "МИМИНУС GL Accelerator", inner.X, inner.Y, c.Theme.Text);

        (string key, string value)[] rows =
        {
            ("advanced.chip_type", "OpenGL 3.3 Core"),
            ("advanced.dac_type", SystemInfo.Renderer),
            ("advanced.memory", "∞"),
            ("advanced.driver", SystemInfo.GlVersion),
        };
        float y = inner.Y + 22;
        foreach (var (key, value) in rows)
        {
            c.F.Ui.Draw(c.R, L.T(key), inner.X, y, c.Theme.Text);
            c.R.PushClip(new Rect(inner.X + 120, y, inner.W - 120, 16));
            c.F.Ui.Draw(c.R, value, inner.X + 120, y, c.Theme.Text);
            c.R.PopClip();
            y += 18;
        }

        var accel = new Rect(body.X, body.Y + 146, body.W, 70);
        W.GroupBox(c, accel, L.T("advanced.hardware_acceleration"));
        var ai = new Rect(accel.X + 14, accel.Y + 28, accel.W - 28, 30);
        c.F.Ui.Draw(c.R, L.T("advanced.none"), ai.X, ai.Y + 18, c.Theme.TextDisabled);
        c.F.Ui.DrawRight(c.R, L.T("advanced.full"), new Rect(ai.X, ai.Y + 18, ai.W, 14), c.Theme.TextDisabled);
        float v = _hwAccelFull ? 1 : 0;
        if (W.Slider(c, Id + ".accel", new Rect(ai.X + 40, ai.Y, ai.W - 80, 20), ref v, 0, 1))
            _hwAccelFull = v > 0.5f;
    }

    void DrawMonitor(UiContext c, Rect body)
    {
        W.GroupBox(c, new Rect(body.X, body.Y, body.W, 80), L.T("advanced.monitor_type"));
        c.F.UiBold.Draw(c.R, L.T("advanced.miminus_monitor"), body.X + 14, body.Y + 30, c.Theme.Text);
        c.F.Small.Draw(c.R, L.T("advanced.plug_and_play"), body.X + 14, body.Y + 48, c.Theme.TextDisabled);

        var settings = new Rect(body.X, body.Y + 96, body.W, 90);
        W.GroupBox(c, settings, L.T("advanced.monitor_settings"));
        var inner = new Rect(settings.X + 14, settings.Y + 28, settings.W - 28, 60);
        c.F.Ui.Draw(c.R, L.T("advanced.refresh_rate"), inner.X, inner.Y + 4, c.Theme.Text);

        int rate = Math.Max(0, Array.IndexOf(Rates, _settings.RefreshHz));
        var names = Rates.Select(r => r == 0
            ? L.T("advanced.unlimited")
            : r + " " + L.T("advanced.hertz")).ToList();

        if (W.ComboBox(c, Id + ".refresh", new Rect(inner.X, inner.Y + 24, 200, 22), names, ref rate))
            _settings.RefreshHz = Rates[Math.Clamp(rate, 0, Rates.Length - 1)];

        c.F.Small.Draw(c.R, L.T("advanced.refresh_note"), inner.X, inner.Y + 52, c.Theme.TextDisabled);
    }

    void DrawTroubleshoot(UiContext c, Rect body)
    {
        foreach (string line in c.F.Ui.Wrap(L.T("advanced.troubleshoot_note"), body.W))
        {
            c.F.Ui.Draw(c.R, line, body.X, body.Y, c.Theme.Text);
            body.CutTop(c.F.Ui.Height + 3);
        }

        body.CutTop(12);
        if (W.Button(c, Id + ".diag", new Rect(body.X, body.Y, 180, 26),
                     L.T("advanced.run_diagnostics"), true, IconId.Settings))
            Shell.MessageBox(c, L.T("advanced.title"), L.T("advanced.diagnostics_result"),
                             MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }
}
