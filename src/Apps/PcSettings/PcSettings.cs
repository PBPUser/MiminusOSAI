using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Параметры компьютера» — the second settings program version 8
/// shipped with, alongside the one it already had.
///
/// This is the joke the original made without meaning to: the Control Panel is
/// still there, still works, and this exists next to it with a bigger typeface
/// and about a third of the switches. Every switch in here is real and shares
/// its state with the old sheets — change the theme in one and the other has
/// already changed.</summary>
public sealed class PcSettingsWindow : OsWindow
{
    public override string Title => L.T("pcs.title");
    public override float MinWidth => 560;
    public override float MinHeight => 380;

    public PcSettingsWindow()
    {
        Icon = IconId.PcSettings;
        Bounds = new Rect(0, 0, 720, 500);
        // A program of version 8's own: it opens filling the screen.
        Immersive = true;
    }

    enum Page { Personalise, Users, Screen, Sound, Start, Access, Power, Language, Update, General }

    static readonly (Page page, string key, IconId icon)[] Pages =
    {
        (Page.Personalise, "pcs.personalise", IconId.Display),
        (Page.Users, "pcs.users", IconId.People),
        (Page.Screen, "pcs.screen", IconId.Devices),
        (Page.Sound, "pcs.sound", IconId.Volume),
        (Page.Start, "pcs.start", IconId.Tiles),
        (Page.Access, "access.title_short", IconId.Access),
        (Page.Power, "power.title", IconId.Power),
        (Page.Language, "pcs.language", IconId.Flag),
        (Page.Update, "pcs.update", IconId.Shield),
        (Page.General, "pcs.general", IconId.Settings),
    };

    Page _page = Page.Personalise;
    string _name;
    bool _updateChecked;

    /// <summary>When the page last changed, so the new one can come in from the
    /// side rather than replacing the old one between frames. Version 8 moved
    /// everything it showed; a settings page that simply swaps looks broken
    /// next to a Start screen that deals itself out.</summary>
    double _pageAt = -10;

    void GoTo(Page page, UiContext c)
    {
        if (page == _page) return;
        _page = page;
        _pageAt = c.Time;
        c.Sound(Sfx.Click, 0.4f);
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        _name = Shell.UserName;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        // ---- the navigation column ----------------------------------------
        var nav = client.CutLeft(190);
        c.R.FillRect(nav, Theme.MetroAccent);

        var head = nav.Deflate(16, 14, 10, 0).CutTop(34);
        c.F.Caption.Draw(c.R, L.T("pcs.title"), head.X, head.Y, Color.White);

        float y = head.Bottom + 10;
        foreach (var (page, key, icon) in Pages)
        {
            var row = new Rect(nav.X + 6, y, nav.W - 12, 34);
            bool hot = c.Hovering(row);
            bool sel = page == _page;

            if (sel) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 60));
            else if (hot) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 30));

            Icons.Draw(c.R, icon, new Rect(row.X + 6, row.CenterY - 9, 18, 18));
            c.F.Ui.Draw(c.R, L.T(key), row.X + 32, row.CenterY - c.F.Ui.Height * 0.5f, Color.White);

            if (c.Clicked(row)) GoTo(page, c);
            y += 36;
        }

        // ---- the page ------------------------------------------------------
        //
        // A page arrives from the right and settles, which is the movement the
        // whole of that shell was built out of.
        float in01 = Shell.Settings.Animations
            ? (float)Math.Clamp((c.Time - _pageAt) / 0.22, 0, 1) : 1;
        float ease = 1 - MathF.Pow(1 - in01, 3);

        var body = client.Deflate(24, 18, 20, 16).Offset(46 * (1 - ease), 0);
        var title = body.CutTop(40);
        c.F.Big.Draw(c.R, L.T(Pages.First(p => p.page == _page).key), title.X, title.Y - 6, t.Text);

        switch (_page)
        {
            case Page.Personalise: DrawPersonalise(c, body); break;
            case Page.Users: DrawUsers(c, body); break;
            case Page.Screen: DrawScreen(c, body); break;
            case Page.Sound: DrawSound(c, body); break;
            case Page.Start: DrawStart(c, body); break;
            case Page.Access: DrawAccess(c, body); break;
            case Page.Power: DrawPower(c, body); break;
            case Page.Language: DrawLanguage(c, body); break;
            case Page.Update: DrawUpdate(c, body); break;
            default: DrawGeneral(c, body); break;
        }
    }

    // ---- pages -------------------------------------------------------------

    void DrawPersonalise(UiContext c, Rect area)
    {
        Caption(c, ref area, "pcs.theme");

        // The themes as swatches: each one painted in its own caption colour,
        // which is as much of a preview as a rectangle can be.
        var themes = new[]
        {
            ThemeId.Metro, ThemeId.LunaBlue, ThemeId.LunaOlive,
            ThemeId.LunaSilver, ThemeId.Seven, ThemeId.Classic,
        };

        var row = area.CutTop(58);
        float sw = 74;
        for (int i = 0; i < themes.Length; i++)
        {
            var preview = Theme.Create(themes[i]);
            var r = new Rect(row.X + i * (sw + 8), row.Y, sw, 46);

            c.R.FillRectV(r, preview.CaptionActiveTop, preview.CaptionActiveBottom);
            c.R.FillRect(new Rect(r.X, r.Y + 14, r.W, r.H - 14), preview.Face);
            c.R.FillRect(new Rect(r.X, r.Bottom - 8, r.W, 8), preview.TaskbarMid);
            c.R.DrawRect(r, themes[i] == Shell.ThemeId ? Theme.MetroAccent : c.Theme.ControlBorder,
                         themes[i] == Shell.ThemeId ? 2 : 1);

            c.Tooltip(r, preview.Name);
            if (c.Clicked(r)) { Shell.SetTheme(themes[i], c.F); c.SoundAt(Sfx.Navigate, r, 0.5f); }
        }

        area.CutTop(12);
        Caption(c, ref area, "pcs.accent");

        // The strip from the out-of-box screens, which is where this colour was
        // first chosen. Picking one here repaints the system the same way.
        var accents = area.CutTop(38);
        float aw = 28;
        for (int i = 0; i < Theme.AccentPalette.Length; i++)
        {
            var r = new Rect(accents.X + i * (aw + 6), accents.Y, aw, aw);
            if (r.Right > accents.Right) break;

            bool picked = Theme.AccentPalette[i].Packed == Theme.MetroAccent.Packed;
            c.R.FillRect(r, Theme.AccentPalette[i]);
            c.R.DrawRect(r, picked ? c.Theme.Text : Color.Rgb(0xC0C0C0), picked ? 2 : 1);

            if (c.Clicked(r))
            {
                Shell.SetAccent(Theme.AccentPalette[i], c.F);
                c.SoundAt(Sfx.Click, r, 0.5f);
            }
        }

        area.CutTop(10);
        Caption(c, ref area, "pcs.background");

        var papers = Shell.Wallpapers.All.ToList();
        var grid = area.CutTop(96);
        float tw = 86, th = 44;
        int perRow = Math.Max(1, (int)(grid.W / (tw + 8)));

        for (int i = 0; i < papers.Count; i++)
        {
            var r = new Rect(grid.X + (i % perRow) * (tw + 8), grid.Y + (i / perRow) * (th + 8), tw, th);
            if (r.Bottom > grid.Bottom) break;

            var paper = Shell.Wallpapers.Get(papers[i]);
            if (paper.Texture != null) c.R.DrawTexture(paper.Texture, r);
            else c.R.FillRect(r, paper.Fallback);

            c.R.DrawRect(r, papers[i] == Shell.Desktop.Current ? Theme.MetroAccent : c.Theme.ControlBorder,
                         papers[i] == Shell.Desktop.Current ? 2 : 1);
            c.Tooltip(r, paper.Name);
            if (c.Clicked(r)) { Shell.SetWallpaper(papers[i]); c.SoundAt(Sfx.Click, r, 0.4f); }
        }
    }

    void DrawUsers(UiContext c, Rect area)
    {
        Caption(c, ref area, "pcs.account_name");

        var field = area.CutTop(30);
        var box = new Rect(field.X, field.Y, 240, 24);
        if (W.TextField(c, Id + ".name", box, ref _name) && _name.Trim().Length > 0)
            Shell.UserName = _name.Trim();

        area.CutTop(10);
        Note(c, ref area, "pcs.account_note");

        area.CutTop(14);
        var pic = area.CutTop(80);
        var avatar = new Rect(pic.X, pic.Y, 64, 64);
        c.R.FillRect(avatar, Theme.MetroAccent);
        c.R.FillCircle(avatar.CenterX, avatar.Y + 22, 13, Color.White);
        c.R.PushClip(avatar);
        c.R.FillCircle(avatar.CenterX, avatar.Bottom + 6, 23, Color.White);
        c.R.PopClip();

        c.F.Caption.Draw(c.R, Shell.UserName, avatar.Right + 14, avatar.Y + 8, c.Theme.Text);
        c.F.Small.Draw(c.R, L.T("pcs.local_account"), avatar.Right + 15,
                       avatar.Y + 10 + c.F.Caption.Height, c.Theme.TextDisabled);

        var btn = new Rect(avatar.Right + 14, avatar.Bottom - 26, 150, 24);
        if (W.Button(c, Id + ".lock", btn, L.T("pcs.lock_now"), true, IconId.Lock))
        {
            Shell.LockScreenNow(c);
        }
    }

    void DrawScreen(UiContext c, Rect area)
    {
        var s = Shell.Settings;

        Caption(c, ref area, "charm.brightness");
        var bright = area.CutTop(30);
        float level = s.Brightness;
        if (W.Slider(c, Id + ".bright", new Rect(bright.X, bright.Y, 260, 22), ref level, 0.35f, 1f))
            s.Brightness = level;
        c.F.Ui.Draw(c.R, (int)MathF.Round(s.Brightness * 100) + "%", bright.X + 274, bright.Y + 3,
                    c.Theme.Text);

        area.CutTop(12);
        Caption(c, ref area, "pcs.scale");
        var dpi = area.CutTop(30);
        int[] scales = { 96, 120, 144 };
        for (int i = 0; i < scales.Length; i++)
        {
            var r = new Rect(dpi.X + i * 120, dpi.Y, 112, 22);
            if (W.Radio(c, Id + ".dpi" + i, r, scales[i] + " DPI", s.Dpi == scales[i]))
                s.Dpi = scales[i];
        }

        area.CutTop(12);
        Caption(c, ref area, "pcs.refresh");
        var hz = area.CutTop(30);
        int[] rates = { 60, 75, 0 };
        for (int i = 0; i < rates.Length; i++)
        {
            var r = new Rect(hz.X + i * 120, hz.Y, 112, 22);
            string label = rates[i] == 0 ? L.T("pcs.uncapped") : rates[i] + " Гц";
            if (W.Radio(c, Id + ".hz" + i, r, label, s.RefreshHz == rates[i]))
                s.RefreshHz = rates[i];
        }

        area.CutTop(12);
        Caption(c, ref area, "pcs.colour_quality");
        var depth = area.CutTop(30);
        int[] depths = { 32, 24, 16 };
        for (int i = 0; i < depths.Length; i++)
        {
            var r = new Rect(depth.X + i * 120, depth.Y, 112, 22);
            if (W.Radio(c, Id + ".depth" + i, r, depths[i] + L.T("pcs.bit"), s.ColorDepth == depths[i]))
                s.ColorDepth = depths[i];
        }
    }

    void DrawSound(UiContext c, Rect area)
    {
        Caption(c, ref area, "charm.volume");

        var row = area.CutTop(30);
        float vol = Shell.Audio.MasterVolume;
        if (W.Slider(c, Id + ".vol", new Rect(row.X, row.Y, 260, 22), ref vol, 0, 1))
        {
            Shell.Audio.MasterVolume = vol;
            c.Sound(Sfx.Tick, 0.6f);
        }
        c.F.Ui.Draw(c.R, (int)MathF.Round(vol * 100) + "%", row.X + 274, row.Y + 3, c.Theme.Text);

        area.CutTop(10);
        var mute = area.CutTop(26);
        bool muted = Shell.Audio.Muted;
        if (W.CheckBox(c, Id + ".mute", new Rect(mute.X, mute.Y, 200, 20), L.T("tray.mute"), ref muted))
            Shell.ToggleMute(c);

        area.CutTop(14);
        var test = area.CutTop(30);
        if (W.Button(c, Id + ".test", new Rect(test.X, test.Y, 160, 24), L.T("pcs.test_sound"),
                     true, IconId.Volume))
            c.Sound(Sfx.Startup, 0.9f);

        area.CutTop(14);
        Note(c, ref area, "pcs.sound_note");
    }

    void DrawStart(UiContext c, Rect area)
    {
        var s = Shell.Settings;

        var one = area.CutTop(28);
        bool tiles = s.UseStartScreen;
        if (W.CheckBox(c, Id + ".tiles", new Rect(one.X, one.Y, 420, 22),
                       L.T("pcs.use_start_screen"), ref tiles))
            s.UseStartScreen = tiles;
        Note(c, ref area, "pcs.use_start_screen_note");

        area.CutTop(10);
        var two = area.CutTop(28);
        bool corners = s.HotCorners;
        if (W.CheckBox(c, Id + ".corners", new Rect(two.X, two.Y, 420, 22),
                       L.T("pcs.hot_corners"), ref corners))
            s.HotCorners = corners;
        Note(c, ref area, "pcs.hot_corners_note");

        area.CutTop(10);
        var three = area.CutTop(28);
        bool lockScreen = s.ShowLockScreen;
        if (W.CheckBox(c, Id + ".lock", new Rect(three.X, three.Y, 420, 22),
                       L.T("pcs.show_lock_screen"), ref lockScreen))
            s.ShowLockScreen = lockScreen;
        Note(c, ref area, "pcs.show_lock_screen_note");

        area.CutTop(12);
        Caption(c, ref area, "pcs.tile_size");

        var sizes = area.CutTop(32);
        string[] tileKeys = { "pcs.tiles_small", "pcs.tiles_medium", "pcs.tiles_large" };
        for (int i = 0; i < tileKeys.Length; i++)
        {
            var r = new Rect(sizes.X + i * 106, sizes.Y, 98, 26);
            if (Chip(c, r, L.T(tileKeys[i]), s.TileSizeStep == i)) s.TileSizeStep = i;
        }
        Note(c, ref area, "pcs.tile_size_note");

        area.CutTop(10);
        Caption(c, ref area, "pcs.desktop_icons");

        var icons = area.CutTop(32);
        string[] iconKeys = { "desktop.icons_small", "desktop.icons_medium", "desktop.icons_large" };
        for (int i = 0; i < iconKeys.Length; i++)
        {
            var r = new Rect(icons.X + i * 136, icons.Y, 128, 26);
            if (Chip(c, r, L.T(iconKeys[i]), s.DesktopIcons == i))
            {
                s.DesktopIcons = i;
                Shell.Desktop.Relayout(c.ScreenW, c.ScreenH);
            }
        }

        area.CutTop(14);
        var open = area.CutTop(30);
        if (W.Button(c, Id + ".open", new Rect(open.X, open.Y, 190, 24), L.T("pcs.open_start_screen"),
                     true, IconId.Tiles))
        {
            Shell.Start.Open(c);
        }
    }

    void DrawLanguage(UiContext c, Rect area)
    {
        Caption(c, ref area, "pcs.interface_language");

        var ru = area.CutTop(28);
        if (W.Radio(c, Id + ".ru", new Rect(ru.X, ru.Y, 260, 22), "Русский", L.IsRu))
            L.Current = Lang.Ru;

        var en = area.CutTop(28);
        if (W.Radio(c, Id + ".en", new Rect(en.X, en.Y, 260, 22), "English", !L.IsRu))
            L.Current = Lang.En;

        area.CutTop(14);
        Note(c, ref area, "pcs.language_note");
    }

    void DrawGeneral(UiContext c, Rect area)
    {
        Caption(c, ref area, "pcs.about_this_pc");

        (string key, string value)[] facts =
        {
            ("pcs.edition", L.T("pcs.edition_value")),
            ("pcs.version", UpdateService.InstalledVersion),
            ("pcs.processor", "Миминус Core 2 Duo 2400 MHz"),
            ("pcs.memory", "2 ГБ"),
            ("pcs.programs", Shell.Programs.Count.ToString()),
        };

        foreach (var (key, value) in facts)
        {
            var row = area.CutTop(24);
            c.F.Ui.Draw(c.R, L.T(key), row.X, row.Y, c.Theme.TextDisabled);
            c.F.Ui.Draw(c.R, value, row.X + 170, row.Y, c.Theme.Text);
        }

        area.CutTop(16);
        var buttons = area.CutTop(30);
        if (W.Button(c, Id + ".update", new Rect(buttons.X, buttons.Y, 190, 24),
                     L.T("pcs.check_for_updates"), true, IconId.Shield))
        {
            GoTo(Page.Update, c);
        }

        if (W.Button(c, Id + ".reset", new Rect(buttons.X + 200, buttons.Y, 190, 24),
                     L.T("pcs.reset_this_pc")))
            Shell.MessageBox(c, L.T("pcs.reset_this_pc"), L.T("pcs.reset_note"),
                             MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }


    /// <summary>«Специальные возможности», version 8's short version of the
    /// Control Panel page: the same four switches with more room around them.</summary>
    void DrawAccess(UiContext c, Rect area)
    {
        var s = Shell.Settings;

        Switch(c, ref area, ".mag", "access.magnifier", "access.magnifier_note", ref s.Magnifier);
        Switch(c, ref area, ".osk", "access.on_screen_keyboard", "access.osk_note",
               ref s.OnScreenKeyboard);
        Switch(c, ref area, ".narr", "access.narrator", "access.narrator_note", ref s.Narrator);

        // High contrast is a theme, so it is not a plain field to flip.
        var row = area.CutTop(26);
        bool contrast = Shell.ThemeId == ThemeId.HighContrast;
        bool wanted = contrast;
        if (W.CheckBox(c, Id + ".hc", new Rect(row.X, row.Y, 420, 22),
                       L.T("access.high_contrast"), ref wanted) && wanted != contrast)
            Shell.ToggleHighContrast(c);
        Note(c, ref area, "access.high_contrast_note");
        area.CutTop(8);

        Switch(c, ref area, ".anim", "access.animations", "access.animations_note", ref s.Animations);

        area.CutTop(6);
        Caption(c, ref area, "access.pointer_size");
        var sizes = area.CutTop(32);
        string[] pointerKeys = { "access.pointer_0", "access.pointer_1", "access.pointer_2" };
        for (int i = 0; i < pointerKeys.Length; i++)
        {
            float k = 1f + i * 0.75f;
            var r = new Rect(sizes.X + i * 96, sizes.Y, 88, 26);
            if (W.Radio(c, Id + ".ptr" + i, r, L.T(pointerKeys[i]),
                        MathF.Abs(s.CursorScale - k) < 0.05f))
                s.CursorScale = k;
        }

        area.CutTop(10);
        Caption(c, ref area, "access.zoom");
        var zoomRow = area.CutTop(30);
        float zoom = s.MagnifierZoom;
        if (W.Slider(c, Id + ".zoom", new Rect(zoomRow.X, zoomRow.Y, 260, 22), ref zoom, 1.5f, 6f))
            s.MagnifierZoom = MathF.Round(zoom * 2) / 2;
        c.F.Ui.Draw(c.R, "×" + s.MagnifierZoom.ToString("0.#"), zoomRow.X + 274, zoomRow.Y + 3,
                    c.Theme.Text);
    }

    /// <summary>«Питание»: the plan, the two timers and what the button does —
    /// the Control Panel page with the radio buttons made larger.</summary>
    void DrawPower(UiContext c, Rect area)
    {
        var s = Shell.Settings;

        Caption(c, ref area, "power.heading");
        Note(c, ref area, "power.heading_note");
        area.CutTop(10);

        (string key, string noteKey)[] plans =
        {
            ("power.balanced", "power.balanced_note"),
            ("power.performance", "power.performance_note"),
            ("power.saver", "power.saver_note"),
        };

        for (int i = 0; i < plans.Length; i++)
        {
            var row = area.CutTop(24);
            if (W.Radio(c, Id + ".plan" + i, new Rect(row.X, row.Y, 360, 22),
                        L.T(plans[i].key), s.PowerPlan == i))
                s.PowerPlan = i;
            Note(c, ref area, plans[i].noteKey);
            area.CutTop(6);
        }

        area.CutTop(8);
        Caption(c, ref area, "power.timers");

        int[] minutes = { 1, 5, 10, 20, 30, 0 };
        var display = area.CutTop(28);
        c.F.Ui.Draw(c.R, L.T("power.display_off"), display.X, display.Y + 4, c.Theme.TextDisabled);
        for (int i = 0; i < minutes.Length; i++)
        {
            var r = new Rect(display.X + 250 + i * 62, display.Y, 58, 24);
            if (Chip(c, r, minutes[i] == 0 ? L.T("power.never") : minutes[i].ToString(),
                     s.DisplayOffMinutes == minutes[i]))
                s.DisplayOffMinutes = minutes[i];
        }

        var sleep = area.CutTop(32);
        c.F.Ui.Draw(c.R, L.T("power.sleep_after"), sleep.X, sleep.Y + 4, c.Theme.TextDisabled);
        for (int i = 0; i < minutes.Length; i++)
        {
            var r = new Rect(sleep.X + 250 + i * 62, sleep.Y, 58, 24);
            if (Chip(c, r, minutes[i] == 0 ? L.T("power.never") : minutes[i].ToString(),
                     s.SleepMinutes == minutes[i]))
                s.SleepMinutes = minutes[i];
        }

        Note(c, ref area, "power.timers_note");
        area.CutTop(12);

        Caption(c, ref area, "power.button_heading");
        var action = area.CutTop(28);
        string[] actions = { "charm.shutdown", "charm.sleep", "start.log_off" };
        for (int i = 0; i < actions.Length; i++)
        {
            var r = new Rect(action.X + i * 150, action.Y, 142, 24);
            if (Chip(c, r, L.T(actions[i]), s.PowerButtonAction == i)) s.PowerButtonAction = i;
        }

        area.CutTop(18);
        var now = area.CutTop(30);
        if (W.Button(c, Id + ".sleepnow", new Rect(now.X, now.Y, 170, 26),
                     L.T("power.sleep_now"), true, IconId.Lock))
            Shell.LockScreenNow(c);
    }

    /// <summary>«Центр обновления», version 8's half of it: the state in one
    /// line of large type, one wide button that does whatever the state calls
    /// for, and the notes underneath.
    ///
    /// It is the same service the Control Panel page reads — there is one check
    /// running, not two, so starting one here and then opening the old page
    /// finds the same progress bar halfway along.</summary>
    void DrawUpdate(UiContext c, Rect area)
    {
        var updates = Shell.Updates;

        // Opening the page is a request to look, once per window: version 8
        // checked the moment you got here rather than waiting to be asked.
        if (!_updateChecked && updates.State == UpdateState.Idle)
        {
            _updateChecked = true;
            updates.BeginCheck();
        }

        Caption(c, ref area, "pcs.update_heading");
        Note(c, ref area, "pcs.update_note");
        area.CutTop(16);

        // ---- the state, in the size version 8 said things in ------------------
        (string head, string detail) = updates.State switch
        {
            UpdateState.Checking => (L.T("update.checking"), updates.Source),
            UpdateState.Downloading => (L.T("update.downloading"),
                                        updates.Total > 0
                                            ? L.F("update.bytes_of", L.FileSize(updates.Fetched),
                                                  L.FileSize(updates.Total))
                                            : L.T("update.do_not_turn_off")),
            UpdateState.Verifying => (L.T("update.verifying"), L.T("update.do_not_turn_off")),
            UpdateState.Extracting => (L.T("update.extracting"), L.T("update.do_not_turn_off")),
            UpdateState.Available => (L.T("update.available"),
                                      L.F("update.available_detail",
                                          updates.Latest?.Name ?? "",
                                          updates.Latest?.Size ?? "?")),
            UpdateState.ReadyToRestart => (L.T("update.ready"),
                                           L.F("update.ready_detail", updates.Latest?.Version ?? "")),
            UpdateState.Failed => (L.T("update.failed"),
                                   string.IsNullOrEmpty(updates.Error)
                                       ? L.T("update.failed_detail") : updates.Error),
            UpdateState.UpToDate => (L.T("update.up_to_date"),
                                     L.F("update.up_to_date_detail", UpdateService.InstalledVersion)),
            _ => (L.T("update.idle"), L.T("update.idle_hint")),
        };

        var badge = area.CutTop(44);
        Icons.Draw(c.R, updates.State == UpdateState.Failed ? IconId.DlgWarning : IconId.Shield,
                   new Rect(badge.X, badge.Y, 36, 36));
        c.R.PushClip(new Rect(badge.X + 46, badge.Y, badge.W - 46, badge.H));
        c.F.Caption.Draw(c.R, head, badge.X + 46, badge.Y + 6, c.Theme.Text);
        c.R.PopClip();

        foreach (string line in c.F.Small.Wrap(detail, MathF.Min(area.W, 480)).Take(2))
        {
            var row = area.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X + 46, row.Y, c.Theme.TextDisabled);
        }

        if (updates.Busy)
        {
            area.CutTop(8);
            var bar = area.CutTop(18);
            W.ProgressBar(c, new Rect(bar.X + 46, bar.Y, MathF.Min(bar.W - 46, 420), 14),
                          updates.Progress);
        }

        area.CutTop(18);

        // ---- one wide button, which is the whole of what this page does -------
        var action = area.CutTop(40);
        var big = new Rect(action.X, action.Y, 260, 32);

        switch (updates.State)
        {
            case UpdateState.ReadyToRestart:
                if (Wide(c, big, L.T("update.restart_now"), !updates.Busy))
                    Shell.RestartForUpdate(c);
                break;

            case UpdateState.Available:
                if (Wide(c, big, L.T("update.install"), true))
                    Shell.MessageBox(c, L.T("update.title"),
                        L.F("update.confirm_install", updates.Latest?.Name ?? "",
                            updates.Latest?.Size ?? "?"),
                        MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion,
                        r => { if (r == MsgResult.Yes) updates.BeginDownload(); },
                        Sfx.Question);
                break;

            default:
                if (Wide(c, big, L.T("pcs.check_for_updates"), !updates.Busy))
                    updates.BeginCheck();
                break;
        }

        area.CutTop(10);

        // ---- the three facts worth stating ------------------------------------
        (string key, string value)[] facts =
        {
            ("update.fact_installed", UpdateService.InstalledVersion),
            ("update.fact_last_checked", updates.LastChecked is { } t
                                            ? L.Time(t) : L.T("update.never_checked")),
            ("update.fact_channel", UpdateService.Branch),
        };
        foreach (var (key, value) in facts)
        {
            if (area.H < 24) break;
            var row = area.CutTop(24);
            c.F.Ui.Draw(c.R, L.T(key), row.X, row.Y, c.Theme.TextDisabled);
            c.F.Ui.Draw(c.R, value, row.X + 200, row.Y, c.Theme.Text);
        }

        // The old page is still there, and this says where.
        if (area.H >= 26)
        {
            var link = area.CutBottom(22);
            var hit = new Rect(link.X, link.Y, MathF.Min(link.W, 360), link.H);
            bool hot = c.Hovering(hit);
            c.F.Small.Draw(c.R, L.T("pcs.update_open_cpl"), hit.X, hit.Y,
                           hot ? Theme.MetroAccent : c.Theme.TextDisabled);
            if (hot) c.Cursor = CursorShape.Hand;
            if (c.Clicked(hit)) Shell.Launch(c, "update", null);
        }

        // ---- what is new ------------------------------------------------------
        string notes = updates.Latest?.Notes;
        if (!string.IsNullOrEmpty(notes) && area.H > 46)
        {
            area.CutTop(10);
            Caption(c, ref area, "pcs.update_history");
            foreach (string line in notes.Split('\n'))
            {
                if (area.H < c.F.Small.Height + 2) break;
                var row = area.CutTop(c.F.Small.Height + 3);
                c.R.FillRect(new Rect(row.X + 1, row.CenterY - 2, 4, 4), Theme.MetroAccent);
                c.R.PushClip(row);
                c.F.Small.Draw(c.R, line, row.X + 12, row.Y, c.Theme.Text);
                c.R.PopClip();
            }
        }
    }

    /// <summary>The wide flat button version 8 put at the foot of a settings
    /// page: the accent colour, no bevel, and the label in white.</summary>
    bool Wide(UiContext c, Rect r, string label, bool enabled)
    {
        bool hot = enabled && c.Hovering(r);
        bool held = hot && c.In.IsDown(MouseButton.Left);

        var face = !enabled ? c.Theme.FaceDark
                 : held ? Theme.MetroAccent.Shade(0.82f)
                 : hot ? Theme.MetroAccent.Shade(1.12f) : Theme.MetroAccent;

        c.R.FillRect(r, face);
        c.F.Ui.DrawCentered(c.R, label, r, enabled ? Color.White : c.Theme.TextDisabled);

        bool clicked = enabled && c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.5f);
        return clicked;
    }

    /// <summary>A flat pick-one button, which is what version 8 used instead of
    /// a drop-down when there were only a few answers.</summary>
    bool Chip(UiContext c, Rect r, string label, bool picked)
    {
        bool hot = c.Hovering(r);
        c.R.FillRect(r, picked ? Theme.MetroAccent
                       : hot ? Color.Rgba(0x2D89EF, 40) : c.Theme.FaceDark);
        c.R.DrawRect(r, picked ? Theme.MetroAccent : c.Theme.ControlBorder);
        c.F.Small.DrawCentered(c.R, label, r, picked ? Color.White : c.Theme.Text);

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.4f);
        return clicked;
    }

    /// <summary>A checkbox with its explanation under it, which is the shape
    /// every switch on these pages takes.</summary>
    void Switch(UiContext c, ref Rect area, string id, string key, string noteKey, ref bool value)
    {
        var row = area.CutTop(26);
        bool v = value;
        if (W.CheckBox(c, Id + id, new Rect(row.X, row.Y, 420, 22), L.T(key), ref v)) value = v;
        Note(c, ref area, noteKey);
        area.CutTop(8);
    }

    // ---- small helpers -----------------------------------------------------

    static void Caption(UiContext c, ref Rect area, string key)
    {
        var row = area.CutTop(24);
        c.F.UiBold.Draw(c.R, L.T(key), row.X, row.Y, c.Theme.Text);
    }

    static void Note(UiContext c, ref Rect area, string key)
    {
        foreach (string line in c.F.Small.Wrap(L.T(key), MathF.Min(area.W, 420)))
        {
            if (area.H < c.F.Small.Height) return;
            var row = area.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X, row.Y, c.Theme.TextDisabled);
        }
    }
}
