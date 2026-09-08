using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Свойства: Экран» — the dialog part 3 uses to swap between the
/// «Миминус 7» wallpapers. All five tabs are here: themes, the desktop with its
/// live monitor preview and wallpaper list, the screen saver, the appearance
/// scheme, and the settings tab.</summary>
public sealed class DisplayPropertiesWindow : OsWindow
{
    int _tab = 1;                 // opens on Рабочий стол, as in the video
    int _wallpaperIndex;
    int _positionIndex = 2;       // растянуть
    int _colorIndex;
    int _themeIndex;
    int _saverIndex;
    int _resolutionIndex = 3;
    int _colorDepthIndex = 2;
    float _saverWait = 10;

    WallpaperId _pendingWallpaper;
    ThemeId _pendingTheme;
    bool _dirty;

    /// <summary>Taken from the wallpaper library rather than hardcoded, so a new
    /// background appears here the moment it is added.</summary>
    WallpaperId[] Wallpapers => Shell.Wallpapers.All.ToArray();

    static readonly ThemeId[] Themes =
    {
        ThemeId.LunaBlue, ThemeId.LunaOlive, ThemeId.LunaSilver, ThemeId.Seven, ThemeId.Classic,
    };

    public override string Title => L.T("display.display_properties");
    public override float MinWidth => 420;
    public override float MinHeight => 460;

    public DisplayPropertiesWindow()
    {
        Icon = IconId.Display;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 440, 500);
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        _pendingWallpaper = Shell.Desktop.Current;
        _pendingTheme = Shell.ThemeId;
        _wallpaperIndex = Math.Max(0, Array.IndexOf(Wallpapers, _pendingWallpaper));
        _themeIndex = Math.Max(0, Array.IndexOf(Themes, _pendingTheme));
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(8);
        var buttons = area.CutBottom(36);

        string[] tabs =
        {
            L.T("display.themes"),
            L.T("display.desktop"),
            L.T("display.screen_saver"),
            L.T("display.appearance"),
            L.T("display.settings"),
        };
        _tab = W.Tabs(c, Id + ".tabs", area, tabs, _tab, out var body);
        body = body.Deflate(12);

        switch (_tab)
        {
            case 0: DrawThemes(c, body); break;
            case 1: DrawDesktop(c, body); break;
            case 2: DrawScreenSaver(c, body); break;
            case 3: DrawAppearance(c, body); break;
            default: DrawSettings(c, body); break;
        }

        float bw = 84, gap = 8;
        float x = buttons.Right - bw * 3 - gap * 2;
        if (W.Button(c, Id + ".ok", new Rect(x, buttons.Y + 6, bw, 24), L.T("display.ok"), true, IconId.None, true))
        {
            Apply(c);
            Close();
        }
        if (W.Button(c, Id + ".cancel", new Rect(x + bw + gap, buttons.Y + 6, bw, 24), L.T("display.cancel")))
            Close();
        if (W.Button(c, Id + ".apply", new Rect(x + (bw + gap) * 2, buttons.Y + 6, bw, 24),
                     L.T("display.apply"), _dirty))
            Apply(c);
    }

    void Apply(UiContext c)
    {
        Shell.SetWallpaper(_pendingWallpaper);
        if (_pendingTheme != Shell.ThemeId) Shell.SetTheme(_pendingTheme, c.F);
        _dirty = false;
        c.Sound(Sfx.Navigate, 0.6f);
    }

    /// <summary>The little monitor that previews the selection, drawn on every tab
    /// that has one.</summary>
    void DrawMonitorPreview(UiContext c, Rect box)
    {
        // Monitor shell.
        var shell = new Rect(box.CenterX - 78, box.Y, 156, 118);
        c.R.RoundedRectV(shell, 6, Color.Rgb(0xF0F2F6), Color.Rgb(0xB8C0CC), Color.Rgb(0x6E7A8C), 1);
        var screen = new Rect(shell.X + 10, shell.Y + 9, shell.W - 20, shell.H - 28);

        var wp = Shell.Wallpapers.Get(_pendingWallpaper);
        if (wp.Texture != null) c.R.DrawTexture(wp.Texture, screen);
        else c.R.FillRect(screen, wp.Fallback);

        // A miniature window and taskbar so the theme is visible too.
        var theme = Theme.Create(_pendingTheme);
        var mini = new Rect(screen.X + 14, screen.Y + 12, screen.W * 0.55f, screen.H * 0.5f);
        c.R.FillRect(mini, theme.FrameOuter);
        c.R.FillRectV(new Rect(mini.X, mini.Y, mini.W, 7), theme.CaptionActiveTop, theme.CaptionActiveBottom);
        c.R.FillRect(new Rect(mini.X + 1, mini.Y + 7, mini.W - 2, mini.H - 8), theme.Face);

        var bar = new Rect(screen.X, screen.Bottom - 9, screen.W, 9);
        c.R.FillRectV(bar, theme.TaskbarTop, theme.TaskbarBottom);
        c.R.RoundedRect(new Rect(bar.X + 2, bar.Y + 2, 22, 5), 2, theme.StartMid);

        // Stand.
        c.R.FillRect(new Rect(shell.CenterX - 12, shell.Bottom - 4, 24, 8), Color.Rgb(0xC8CED8));
        c.R.RoundedRect(new Rect(shell.CenterX - 26, shell.Bottom + 3, 52, 5), 2, Color.Rgb(0x9AA4B2));
    }

    void DrawThemes(UiContext c, Rect body)
    {
        c.F.Ui.Draw(c.R, L.T("display.theme"), body.X, body.Y + 4, c.Theme.Text);
        var names = Themes.Select(t => Theme.Create(t).Name).ToList();
        int before = _themeIndex;
        W.ComboBox(c, Id + ".theme", new Rect(body.X + 54, body.Y, body.W - 54, 22), names, ref _themeIndex);
        if (_themeIndex != before) { _pendingTheme = Themes[_themeIndex]; _dirty = true; }

        c.F.Small.Draw(c.R,
            L.T("display.a_theme_is_a_set_of_desktop_colours_fonts_an"),
            body.X, body.Y + 30, c.Theme.TextDisabled);

        DrawMonitorPreview(c, new Rect(body.X, body.Y + 56, body.W, 130));

        var box = new Rect(body.X, body.Y + 200, body.W, body.H - 200);
        W.GroupBox(c, box, L.T("display.sample"));
        var inner = box.Deflate(12, 24, 12, 12);

        var theme = Theme.Create(_pendingTheme);
        var win = new Rect(inner.X, inner.Y, inner.W, 74);
        c.R.FillRect(win, theme.FrameOuter);
        var cap = new Rect(win.X, win.Y, win.W, 20);
        c.R.FillRectV(cap, theme.CaptionActiveTop, theme.CaptionActiveBottom);
        c.F.Caption.Draw(c.R, L.T("display.active_window"), cap.X + 6,
                         cap.CenterY - c.F.Caption.Height * 0.5f, theme.CaptionTextActive);
        var face = new Rect(win.X + 3, cap.Bottom, win.W - 6, win.Bottom - cap.Bottom - 3);
        c.R.FillRect(face, theme.Face);
        c.F.Ui.Draw(c.R, L.T("display.window_text"), face.X + 8, face.Y + 8, theme.Text);
        c.R.FillRect(new Rect(face.X + 8, face.Y + 26, 90, 16), theme.Selection);
        c.F.Ui.Draw(c.R, L.T("display.selected"), face.X + 12, face.Y + 27, theme.SelectionText);
    }

    void DrawDesktop(UiContext c, Rect body)
    {
        DrawMonitorPreview(c, new Rect(body.X, body.Y, body.W, 130));

        float y = body.Y + 140;
        c.F.Ui.Draw(c.R, L.T("display.background"), body.X, y, c.Theme.Text);
        y += c.F.Ui.Height + 4;

        var list = new Rect(body.X, y, body.W, 118);
        var names = Wallpapers.Select(w => Shell.Wallpapers.Get(w).Name).ToList();
        int before = _wallpaperIndex;
        W.ListBox(c, Id + ".wp", list, names, ref _wallpaperIndex, out _);
        if (_wallpaperIndex != before)
        {
            _pendingWallpaper = Wallpapers[_wallpaperIndex];
            _dirty = true;
            c.Sound(Sfx.Tick, 0.4f);
        }

        var browse = new Rect(body.Right - 90, list.Bottom + 8, 90, 24);
        if (W.Button(c, Id + ".browse", browse, L.T("display.browse")))
            Wm.Open(new FilePickerWindow(Shell.Fs, L.T("display.browse_2"), _ => { }), c);

        float ry = list.Bottom + 8;
        c.F.Ui.Draw(c.R, L.T("display.position"), body.X, ry + 4, c.Theme.Text);
        var pos = new List<string>
        {
            L.T("display.center"), L.T("display.tile"),
            L.T("display.stretch"), L.T("display.fit"),
        };
        W.ComboBox(c, Id + ".pos", new Rect(body.X + 96, ry, 130, 22), pos, ref _positionIndex);
        ry += 30;

        c.F.Ui.Draw(c.R, L.T("display.color"), body.X, ry + 4, c.Theme.Text);
        var colors = new List<string>
        {
            L.T("display.blue"), L.T("display.black"),
            L.T("display.yellow"), L.T("display.green"),
        };
        W.ComboBox(c, Id + ".col", new Rect(body.X + 96, ry, 130, 22), colors, ref _colorIndex);

        var custom = new Rect(body.X, ry + 34, 230, 24);
        if (W.Button(c, Id + ".custom", custom, L.T("display.customize_desktop")))
            Shell.MessageBox(c, L.T("display.desktop_items"),
                L.T("display.desktop_icons_are_arranged_by_dragging_f5_re"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }

    void DrawScreenSaver(UiContext c, Rect body)
    {
        DrawMonitorPreview(c, new Rect(body.X, body.Y, body.W, 130));

        float y = body.Y + 148;
        W.GroupBox(c, new Rect(body.X, y, body.W, 92), L.T("display.screen_saver_2"));
        var inner = new Rect(body.X + 12, y + 26, body.W - 24, 60);

        var savers = new List<string>
        {
            L.T("display.none"), L.T("display.miminus_logo"),
            L.T("display.marquee"), L.T("display.3d_text"),
        };
        W.ComboBox(c, Id + ".saver", new Rect(inner.X, inner.Y, 180, 22), savers, ref _saverIndex);

        if (W.Button(c, Id + ".preview", new Rect(inner.X + 190, inner.Y, 90, 22), L.T("display.preview")))
            Shell.MessageBox(c, L.T("display.screen_saver_2"),
                L.T("display.no_screen_saver_needed_miminus_os_never_gets"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

        c.F.Ui.Draw(c.R, L.T("display.wait"), inner.X, inner.Y + 32, c.Theme.Text);
        W.Slider(c, Id + ".wait", new Rect(inner.X + 70, inner.Y + 28, 150, 20), ref _saverWait, 1, 60);
        c.F.Ui.Draw(c.R, L.F("display.0_f0_min", _saverWait), inner.X + 228, inner.Y + 32, c.Theme.Text);

        var power = new Rect(body.X, y + 104, body.W, body.H - (y + 104 - body.Y) - 4);
        W.GroupBox(c, power, L.T("display.monitor_power"));
        c.F.Small.Draw(c.R,
            L.T("display.to_save_power_the_monitor_can_be_switched_of"),
            power.X + 14, power.Y + 28, c.Theme.TextDisabled);
        if (W.Button(c, Id + ".power", new Rect(power.X + 14, power.Y + 62, 90, 24), L.T("display.power")))
            Shell.Launch(c, "clock", null);
    }

    void DrawAppearance(UiContext c, Rect body)
    {
        var preview = new Rect(body.X, body.Y, body.W, 150);
        c.R.FillRect(preview, Color.Rgb(0x3A6EA5));
        c.R.DrawRect(preview, c.Theme.ControlBorder);

        var theme = Theme.Create(_pendingTheme);

        // Inactive window behind, active window in front.
        var inactive = new Rect(preview.X + 16, preview.Y + 14, preview.W - 90, 60);
        c.R.FillRect(inactive, theme.FrameOuter.WithAlpha((byte)170));
        c.R.FillRectV(new Rect(inactive.X, inactive.Y, inactive.W, 18),
                      theme.CaptionInactiveTop, theme.CaptionInactiveBottom);
        c.F.Caption.Draw(c.R, L.T("display.inactive_window"), inactive.X + 6,
                         inactive.Y + 3, theme.CaptionTextInactive);
        c.R.FillRect(new Rect(inactive.X + 3, inactive.Y + 18, inactive.W - 6, inactive.H - 21), theme.Face);

        var active = new Rect(preview.X + 46, preview.Y + 52, preview.W - 90, 84);
        c.R.FillRect(active.Offset(3, 3), Color.Rgba(0x000000, 60));
        c.R.FillRect(active, theme.FrameOuter);
        c.R.FillRectV(new Rect(active.X, active.Y, active.W, 20),
                      theme.CaptionActiveTop, theme.CaptionActiveBottom);
        c.F.Caption.Draw(c.R, L.T("display.active_window"), active.X + 6,
                         active.Y + 3, theme.CaptionTextActive);
        var af = new Rect(active.X + 3, active.Y + 20, active.W - 6, active.H - 23);
        c.R.FillRect(af, theme.Face);
        c.F.Ui.Draw(c.R, L.T("display.window_text"), af.X + 8, af.Y + 6, theme.Text);
        c.R.FillRect(new Rect(af.X + 8, af.Y + 24, 100, 16), theme.Selection);
        c.F.Ui.Draw(c.R, L.T("display.selected_text"), af.X + 12, af.Y + 25, theme.SelectionText);
        W.DrawButtonFace(c, new Rect(af.Right - 80, af.Y + 24, 70, 22), true, false, false);
        c.F.Ui.DrawCentered(c.R, L.T("display.ok"), new Rect(af.Right - 80, af.Y + 24, 70, 22), theme.Text);

        float y = preview.Bottom + 14;
        c.F.Ui.Draw(c.R, L.T("display.windows_and_buttons"), body.X, y + 4, c.Theme.Text);
        var styles = new List<string> { L.T("display.miminus_style"), L.T("display.classic_style") };
        int style = _pendingTheme == ThemeId.Classic ? 1 : 0;
        int styleBefore = style;
        W.ComboBox(c, Id + ".style", new Rect(body.X + 130, y, body.W - 130, 22), styles, ref style);
        if (style != styleBefore)
        {
            _pendingTheme = style == 1 ? ThemeId.Classic : ThemeId.LunaBlue;
            _themeIndex = Math.Max(0, Array.IndexOf(Themes, _pendingTheme));
            _dirty = true;
        }
        y += 30;

        c.F.Ui.Draw(c.R, L.T("display.color_scheme"), body.X, y + 4, c.Theme.Text);
        var schemeNames = Themes.Select(t => Theme.Create(t).Name).ToList();
        int before = _themeIndex;
        W.ComboBox(c, Id + ".scheme", new Rect(body.X + 130, y, body.W - 130, 22), schemeNames, ref _themeIndex);
        if (_themeIndex != before) { _pendingTheme = Themes[_themeIndex]; _dirty = true; }

        // The two dialogs XP hung off this tab.
        var effects = new Rect(body.Right - 220, y + 36, 104, 26);
        var advanced = new Rect(body.Right - 108, y + 36, 108, 26);
        if (W.Button(c, Id + ".effects", effects, L.T("display.effects_button")))
            Wm.Open(new EffectsWindow(Shell.Settings), c);
        if (W.Button(c, Id + ".advappearance", advanced, L.T("display.advanced_button")))
            Wm.Open(new AdvancedAppearanceWindow(), c);
    }

    void DrawSettings(UiContext c, Rect body)
    {
        DrawMonitorPreview(c, new Rect(body.X, body.Y, body.W, 130));

        float y = body.Y + 150;
        c.F.Small.Draw(c.R, L.T("display.display"), body.X, y, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("display.miminus_monitor_on_miminus_gl_adapter"),
                    body.X, y + c.F.Small.Height + 2, c.Theme.Text);
        y += 44;

        var half = body.W * 0.5f - 8;

        W.GroupBox(c, new Rect(body.X, y, half, 86), L.T("display.screen_resolution"));
        var resList = new List<string> { "800 x 600", "1024 x 768", "1152 x 864", "1280 x 800", "1440 x 900", "1920 x 1080" };
        float rv = _resolutionIndex;
        W.Slider(c, Id + ".res", new Rect(body.X + 14, y + 30, half - 28, 22), ref rv, 0, resList.Count - 1);
        _resolutionIndex = (int)MathF.Round(rv);
        c.F.Ui.DrawCentered(c.R, resList[Math.Clamp(_resolutionIndex, 0, resList.Count - 1)],
                            new Rect(body.X, y + 56, half, 18), c.Theme.Text);

        W.GroupBox(c, new Rect(body.X + half + 16, y, half, 86), L.T("display.color_quality"));
        var depths = new List<string>
        {
            L.T("display.medium_16_bit"),
            L.T("display.high_24_bit"),
            L.T("display.highest_32_bit"),
        };
        // The picture really is reduced: 16-bit bands every gradient in the
        // system, because the shader snaps each channel to 32 levels.
        int depth = Shell.Settings.ColorDepth switch { 16 => 0, 24 => 1, _ => 2 };
        if (W.ComboBox(c, Id + ".depth", new Rect(body.X + half + 30, y + 32, half - 28, 22),
                       depths, ref depth))
            Shell.Settings.ColorDepth = depth switch { 0 => 16, 1 => 24, _ => 32 };

        var note = new Rect(body.X, y + 100, body.W, body.H - (y + 100 - body.Y));
        c.F.Small.Draw(c.R,
            L.T("display.the_miminus_os_resolution_follows_the_size_o"),
            note.X, note.Y, c.Theme.TextDisabled);

        if (W.Button(c, Id + ".advanced", new Rect(note.Right - 130, note.Y + 24, 130, 24),
                     L.T("display.advanced")))
            Wm.Open(new AdvancedSettingsWindow(Shell.Settings), c);
    }
}
