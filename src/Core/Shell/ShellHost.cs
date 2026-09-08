using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

public enum ShellPhase { Setup, Post, Booting, Locked, Welcome, Running, LoggingOff, ShuttingDown, PoweredOff, Stopped }

/// <summary>The operating system itself: boot sequence, desktop, taskbar, the
/// window manager and the program launcher.
///
/// A frame runs top-down through the layers — menus, then the Start menu, then
/// windows, then the taskbar, then the desktop — each claiming the pointer if it
/// wants it, so the layer beneath only ever sees input nobody above used.</summary>
public sealed class ShellHost : IDisposable
{
    public readonly VirtualFS Fs = new();
    public readonly WallpaperLibrary Wallpapers = new();
    public readonly WindowManager Wm = new();
    public readonly MenuHost Menus = new();

    /// <summary>Programs discovered in the apps/ folder.</summary>
    public readonly ProgramRegistry Programs = new();

    /// <summary>A file, folder or shortcut being carried between windows.</summary>
    public DragDropHost Drag;

    /// <summary>«Центр обновления» — checks the project repository for a newer
    /// build. Owned by the shell so the tray can announce a finding even when
    /// the update window is closed.</summary>
    public readonly UpdateService Updates = new();
    public readonly AudioEngine Audio;

    /// <summary>Visual-effect switches from Display Properties → Эффекты.</summary>
    public readonly ShellSettings Settings = new();

    public Desktop Desktop { get; }
    public Taskbar Taskbar { get; }

    /// <summary>The monitors this machine believes it has. There is one real
    /// window, so a second monitor is half of it — but every place the shell
    /// asks "how big is the screen" asks this instead, which is what makes the
    /// second one behave like a screen rather than a picture of one.</summary>
    public DisplayLayout Displays { get; } = new();
    public StartMenu StartMenu { get; }

    /// <summary>Экран «Пуск» — the tile board version 8 opens instead of a
    /// menu. It covers everything while it is up, so the shell asks it before
    /// it asks anything else.</summary>
    public StartScreen Start { get; }

    /// <summary>The right edge, and the two corners that summon it.</summary>
    public Charms Charms { get; }

    /// <summary>The left edge: the Start corner, the Win+X list, and the strip
    /// of running programs.</summary>
    public SwitcherBar Switcher { get; }

    readonly LockScreen _lock = new();

    /// <summary>Специальные возможности: the magnifier, the on-screen keyboard
    /// and the narrator, which are shell-wide rather than any program's.</summary>
    public Accessibility Access { get; }

    public Theme Theme { get; private set; } = Theme.Metro();
    public ThemeId ThemeId => Theme.Id;

    // A machine that has run this before starts at POST; one that has not is
    // set up first.
    public ShellPhase Phase { get; private set; } =
        FirstRun.NeverRun ? ShellPhase.Setup : ShellPhase.Post;

    /// <summary>The out-of-box questions, live only on a first run.</summary>
    public readonly Setup Setup;

    /// <summary>Clock shown in the tray. Runs on real time but starts at the
    /// timestamp stamped on the files in the reference videos.</summary>
    public DateTime Now => new DateTime(2010, 6, 6, 13, 28, 0).AddSeconds(_uptime * 12);

    double _uptime;
    double _phaseStart;

    /// <summary>When the pointer last moved or a key was last pressed, and
    /// whether the screen has been turned off since.</summary>
    double _lastActivity;
    float _lastMouseX = -1, _lastMouseY = -1;
    bool _displayOff;
    int _appliedPlan = -1;
    bool _startupSoundPlayed;
    bool _networkBalloonShown;
    bool _updateCheckStarted;

    /// <summary>Set once the staged build is in place and the shutdown that
    /// follows is a restart rather than a power-off. The host watches it.</summary>
    public bool ExitRequested { get; private set; }
    bool _restartPending;

    readonly List<string> _postLines = new();
    int _postShown;
    double _postNext;

    public ShellHost(AudioEngine audio)
    {
        Audio = audio;
        Wm.Shell = this;
        Desktop = new Desktop(this);
        Drag = new DragDropHost(this);
        Setup = new Setup(this);
        Taskbar = new Taskbar(this);
        StartMenu = new StartMenu(this);
        Access = new Accessibility(this);
        Start = new StartScreen(this);
        Charms = new Charms(this);
        Switcher = new SwitcherBar(this);

        L.Changed += () => { /* windows re-read their titles each frame */ };

        // Anything that edits the registry — Regedit included — repaints the
        // system, because the theme is built out of one of its values.
        Registry.Changed += () => _themeStale = true;

        BuildPostText();
    }

    void BuildPostText()
    {
        _postLines.AddRange(new[]
        {
            "МИМИНУС BIOS v1.03  (C) 2010 Гревцов и Попов",
            "",
            "Main Processor : Миминус Core 2 Duo  2400 MHz",
            "Memory Testing : 2097152K OK",
            "",
            "Detecting IDE drives ...",
            "  Primary Master   : МИМИНУС HDD 80GB",
            "  Primary Slave    : МИМИНУС HDD 160GB",
            "  Secondary Master : МИМИНУС DVD-RW",
            "",
            "Initializing USB Controllers .. Done.",
            "Auto-Detecting BOLGENOS ....... Not found.",
            "",
            "Booting from Hard Disk ...",
        });
    }

    // ---- theme and wallpaper --------------------------------------------

    public void SetTheme(ThemeId id, Fonts fonts)
    {
        Theme = Theme.Create(id);
        fonts.Use(id);
    }

    public void SetWallpaper(WallpaperId id) => Desktop.Current = id;

    /// <summary>Picks the colour «Миминус 8» is painted in. The theme holds a
    /// copy of it in a dozen fields, so it is built again — which is cheap, and
    /// the only way the change reaches everything at once.</summary>
    public void SetAccent(Color accent, Fonts fonts)
    {
        Theme.MetroAccent = accent;
        SetTheme(ThemeId, fonts);
    }

    /// <summary>Set when a registry value the look depends on has moved. The
    /// theme is rebuilt on the next frame rather than inside whatever was
    /// editing the value.</summary>
    bool _themeStale;

    // ---- frame -----------------------------------------------------------

    public void Frame(UiContext c)
    {
        if (_themeStale)
        {
            _themeStale = false;
            SetTheme(ThemeId, c.F);
        }

        c.Theme = Theme;
        c.Menus = Menus;
        Menus.Shadows = Settings.MenuShadows;
        Menus.Fade = Settings.MenuTransition && Settings.MenuFade;
        _uptime += c.Dt;
        Audio.SetScreenSize(c.ScreenW, c.ScreenH);
        PollPower(c);

        switch (Phase)
        {
            case ShellPhase.Setup: DrawSetup(c); break;
            case ShellPhase.Post: DrawPost(c); break;
            case ShellPhase.Booting: DrawBootSplash(c); break;
            case ShellPhase.Locked: DrawLockScreen(c); break;
            case ShellPhase.Welcome: DrawWelcome(c); break;
            case ShellPhase.LoggingOff: DrawLogOff(c); break;
            case ShellPhase.ShuttingDown: DrawShuttingDown(c); break;
            case ShellPhase.PoweredOff: DrawPoweredOff(c); break;
            case ShellPhase.Stopped: DrawBlueScreen(c); break;
            default: DrawDesktopSession(c); break;
        }

        DrawTooltip(c);

        // Яркость, from the settings charm. A veil over the finished frame is
        // the whole of it, and it goes on before the pointer so the pointer
        // stays readable however dark the picture is.
        if (Settings.Brightness < 0.999f)
            c.R.FillRect(new Rect(0, 0, c.ScreenW, c.ScreenH),
                         Color.Rgba(0x000000, (byte)(255 * (1 - Settings.Brightness))));

        // The screen the power plan turned off. Anything at all brings it back,
        // and nothing under it is drawn while it is dark.
        if (_displayOff)
        {
            c.R.FillRect(new Rect(0, 0, c.ScreenW, c.ScreenH), Color.Black);
            return;
        }

        // A stopped system has no pointer: nothing on that screen can be
        // clicked, and the original never showed one either.
        if (!Stopped) DrawCursor(c);
    }

    /// <summary>Электропитание, once a frame: applies the plan when it
    /// changes, notices whether anybody is there, and turns the screen off and
    /// then locks the machine when nobody is.
    ///
    /// "Power" in a machine like this one is the frame rate and the backlight,
    /// so that is what a plan sets. It is not a metaphor: the saver really does
    /// hold the loop to thirty frames and dim the picture, and the machine
    /// really does draw less.</summary>
    void PollPower(UiContext c)
    {
        var s = Settings;

        if (s.PowerPlan != _appliedPlan)
        {
            _appliedPlan = s.PowerPlan;
            switch (s.PowerPlan)
            {
                case 1:  s.RefreshHz = 0;  s.Brightness = 1f;    break;   // performance
                case 2:  s.RefreshHz = 30; s.Brightness = 0.7f;  break;   // saver
                default: s.RefreshHz = 60; s.Brightness = 1f;    break;   // balanced
            }
        }

        // Anything at all counts as somebody being there.
        bool moved = MathF.Abs(c.MouseX - _lastMouseX) > 1 || MathF.Abs(c.MouseY - _lastMouseY) > 1;
        bool typed = c.In.TypedChars.Count > 0 || c.In.Pressed(MouseButton.Left) ||
                     c.In.Pressed(MouseButton.Right) || MathF.Abs(c.In.WheelDelta) > 0.01f;

        _lastMouseX = c.MouseX;
        _lastMouseY = c.MouseY;

        if (moved || typed || _lastActivity <= 0)
        {
            _lastActivity = c.Time;
            if (_displayOff) { _displayOff = false; Audio.Play(Sfx.Click, 0.4f); }
            return;
        }

        // Timers only run on a desktop that is actually sitting there.
        if (Phase != ShellPhase.Running) { _lastActivity = c.Time; return; }

        double idle = (c.Time - _lastActivity) / 60.0;

        if (s.SleepMinutes > 0 && idle >= s.SleepMinutes)
        {
            _displayOff = false;
            _lastActivity = c.Time;
            LockScreenNow(c);
        }
        else if (s.DisplayOffMinutes > 0 && idle >= s.DisplayOffMinutes) _displayOff = true;
    }

    /// <summary>Turns the high-contrast scheme on and off from anywhere,
    /// remembering what the desktop was wearing before.</summary>
    ThemeId _beforeContrast = ThemeId.Metro;

    public void ToggleHighContrast(UiContext c)
    {
        if (ThemeId == ThemeId.HighContrast) SetTheme(_beforeContrast, c.F);
        else
        {
            _beforeContrast = ThemeId;
            SetTheme(ThemeId.HighContrast, c.F);
        }
        c.Sound(Sfx.Navigate, 0.5f);
    }

    /// <summary>What the power buttons do first, from the setting: shut down,
    /// sleep, or sign out.</summary>
    public void PowerButtonPressed(UiContext c)
    {
        switch (Settings.PowerButtonAction)
        {
            case 1: LockScreenNow(c); break;
            case 2: BeginLogOff(c); break;
            default: BeginShutdown(c); break;
        }
    }

    void SetPhase(ShellPhase p, UiContext c)
    {
        bool wasWelcome = Phase == ShellPhase.Welcome;

        Phase = p;
        _phaseStart = c.Time;

        if (p == ShellPhase.Welcome) StartLogonMusic();
        else if (wasWelcome) Audio.StopMusic();
    }

    /// <summary>The logon screen has a song, which is where a system of this
    /// era put one. It is synthesised the first time it is needed and kept, so
    /// logging off and back on does not build it again.</summary>
    void StartLogonMusic()
    {
        try
        {
            _logonSong ??= Chiptune.Welcome();
            Audio.PlayMusic(_logonSong.Value.pcm, _logonSong.Value.rate);
            Audio.SetMusicVolume(0.7f);
        }
        catch
        {
            // Without audio the welcome screen is simply quiet.
        }
    }

    (short[] pcm, int rate)? _logonSong;

    double Elapsed(UiContext c) => c.Time - _phaseStart;

    // ---- running session -------------------------------------------------

    void DrawDesktopSession(UiContext c)
    {
        if (!_startupSoundPlayed)
        {
            _startupSoundPlayed = true;
            Audio.Play(Sfx.Startup, 0.9f);
        }

        // The service-message balloon from part 1, a few seconds after logon.
        if (!_networkBalloonShown && Elapsed(c) > 6)
        {
            _networkBalloonShown = true;
            ShowNetworkBalloon(c);
        }

        // A build that has just replaced an older one introduces itself, once,
        // after the desktop has settled.
        if (FirstRun.JustUpdated && !FirstRun.Announced && Elapsed(c) > 2.5)
        {
            FirstRun.Announced = true;
            Launch(c, "whatsnew", null);
        }

        // The updater checks once shortly after logon, the way an XP-era system
        // did, and only speaks up when it has something to offer.
        if (!_updateCheckStarted && Elapsed(c) > 10)
        {
            _updateCheckStarted = true;
            Updates.BeginCheck();
        }

        Updates.Poll(c.Dt);

        // Program assemblies whose windows have all closed are let go.
        Programs.CollectUnused(_uptime);
        if (Updates.State == UpdateState.Available && !Updates.Announced)
        {
            Updates.Announced = true;
            ShowUpdateBalloon(c);
        }

        HandleGlobalKeys(c);
        HandleWindowsKeyRelease(c);

        // Layers are painted back-to-front but must claim input front-to-back.
        // Menus resolve their input first (they sit above everything), then the
        // shell chrome reserves its strip so no window can grab a click meant for
        // the taskbar or the Start menu. Windows run input while they paint, and
        // the desktop — the bottom layer — gets whatever is left, after them.
        Menus.Update(c);

        // The edges are watched before anything is drawn, because whether the
        // charms are coming out decides who gets this frame's clicks.
        if (Settings.HotCorners)
        {
            Charms.Poll(c);
            Switcher.Poll(c);
        }

        bool overChrome =
            Start.Visible ||
            Charms.Bounds(c).Contains(c.MouseX, c.MouseY) ||
            Access.KeyboardBounds(c).Contains(c.MouseX, c.MouseY) ||
            Switcher.Bounds(c).Contains(c.MouseX, c.MouseY) ||
            Menus.HitTest(c.MouseX, c.MouseY) ||
            (!Taskbar.Retracted && Taskbar.Bounds(c).Contains(c.MouseX, c.MouseY)) ||
            Taskbar.VolumeBounds(c).Contains(c.MouseX, c.MouseY) ||
            Taskbar.ClockBounds(c).Contains(c.MouseX, c.MouseY) ||
            Taskbar.LanguageBounds(c).Contains(c.MouseX, c.MouseY) ||
            Taskbar.TrayPopupBounds(c).Contains(c.MouseX, c.MouseY) ||
            (StartMenu.Interactive && StartMenu.Bounds(c).Contains(c.MouseX, c.MouseY));

        // Windows keep ticking under the Start screen — they are still running,
        // they are simply not on the screen — but they take no input from it.
        Wm.Update(c, blockWindows: overChrome);

        // A board that has finished opening covers the desktop completely, so
        // the desktop is not drawn at all: this is a separate screen, not an
        // overlay, which is exactly the complaint people had about it.
        if (!Start.Opaque)
        {
            Desktop.Draw(c);

            // A program running full screen is the screen: the taskbar stays
            // off, and the edges are the way back out of it. Two of them
            // sharing the screen is still that.
            bool immersive = Wm.TopImmersive != null && !Taskbar.StartOpen;

            // "Keep the taskbar on top" is literally the draw order: off, and
            // windows paint over it.
            if (immersive) Wm.Draw(c);
            else if (Settings.TaskbarOnTop)
            {
                Wm.Draw(c);
                Taskbar.Draw(c);
            }
            else
            {
                Taskbar.Draw(c);
                Wm.Draw(c);
            }

            // The bar between two full-screen programs sharing the screen is
            // above both of them and belongs to neither.
            Wm.DrawSnapDivider(c);

            StartMenu.Draw(c);

            // The edges paint above windows and the taskbar, and their strips
            // were reserved above so the click is theirs.
            Charms.Draw(c);
            Switcher.Draw(c);

            Desktop.Update(c);
            Drag.Resolve(c);
        }

        Start.Draw(c);
        Menus.Draw(c);

        DrawDisplayIdentity(c);

        // The on-screen keyboard is above every window and below the menus it
        // may be typing into; the narrator reads whatever came to the front.
        Access.DrawKeyboard(c);
        Access.Narrate(c);
    }

    /// <summary>«Определить»: a large number in the middle of each monitor for
    /// a few seconds, so it is possible to tell which one the settings page is
    /// talking about. It is the only way anybody has ever found out.</summary>
    void DrawDisplayIdentity(UiContext c)
    {
        double age = c.Time - Displays.IdentifiedAt;
        if (age < 0 || age > 3.2) return;

        // It holds, then goes.
        byte alpha = (byte)(255 * Math.Clamp((3.2 - age) / 0.7, 0, 1));

        foreach (var m in Displays.All(c.ScreenW, c.ScreenH))
        {
            if (!m.Enabled) continue;

            float w = MathF.Min(m.Bounds.W * 0.4f, 190);
            var box = new Rect(m.Bounds.CenterX - w * 0.5f, m.Bounds.CenterY - w * 0.5f, w, w);

            c.R.FillRect(box, Color.Rgba(0x101010, (byte)(alpha * 0.78f)));
            c.R.DrawRect(box, Color.Rgba(0xFFFFFF, (byte)(alpha * 0.5f)), 2);
            c.F.Huge.DrawCentered(c.R, m.Label, box, Color.Rgba(0xFFFFFF, alpha));

            if (m.Primary)
                c.F.Small.DrawCentered(c.R, L.T("screen.this_is_primary"),
                    new Rect(box.X, box.Bottom - 26, box.W, 18), Color.Rgba(0xFFFFFF, alpha));
        }
    }

    void HandleGlobalKeys(UiContext c)
    {
        if (c.KeyboardHandled) return;

        // Alt+F4 closes the focused window.
        if (c.In.Alt && c.In.KeyPressed(Keys.F4))
        {
            if (Wm.Focused != null) Wm.RequestClose(Wm.Focused, c);
            else ShowShutdownDialog(c);
            c.KeyboardHandled = true;
        }
        // Ctrl+Shift+L flips language, matching the tray indicator.
        else if (c.In.Ctrl && c.In.Shift && c.In.KeyPressed(Keys.L))
        {
            L.Toggle();
            c.Sound(Sfx.Click, 0.6f);
            c.KeyboardHandled = true;
        }
        else if (c.In.KeyPressed(Keys.F12))
        {
            Launch(c, "about", null);
            c.KeyboardHandled = true;
        }
        // F11 puts the focused program full screen, and takes it back out.
        else if (c.In.KeyPressed(Keys.F11))
        {
            if (Wm.Focused != null) Wm.ToggleImmersive(Wm.Focused, c);
            c.KeyboardHandled = true;
        }
        else if (c.In.Win) HandleWindowsKey(c);
    }

    /// <summary>The Windows key and its combinations.
    ///
    /// The key is taken from the host shell by a low-level hook, so it belongs
    /// to МИМИНУС while the window has the focus. On its own it opens the Start
    /// screen, on the release rather than the press — otherwise every
    /// combination below would flash the board open on its way through.
    ///
    /// Version 8 added the five that reach the edges: C for the charms, I for
    /// the settings pane, Q to search the board, X for the list in the corner,
    /// and Tab for the strip of running programs.</summary>
    void HandleWindowsKey(UiContext c)
    {
        if (c.In.KeyPressed(Keys.E)) { Launch(c, "mycomputer", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.R)) { Launch(c, "run", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.F)) { Launch(c, "search", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.L)) { LockScreenNow(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.U)) { Launch(c, "access", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.D)) { Wm.MinimizeAll(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Pause)) { Launch(c, "about", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.C)) { Charms.Show(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.I)) { Charms.Show(c, Charms.Pane.Settings); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Q)) { Start.Open(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.X))
        {
            var bar = Taskbar.Bounds(c);
            Switcher.ShowPowerUserMenu(c, new Rect(bar.X + 6, bar.Y - 6, 8, 8));
            _winCombo = true;
        }
        else if (c.In.KeyPressed(Keys.Tab)) { Switcher.Show(); _winCombo = true; }
        // Snap, from the keyboard: the halves, the whole screen, and back.
        // Win+. is version 8's own snap: the full-screen program goes into
        // the column on the right, then the left, then back to the whole
        // screen — which is the cycle the original ran through.
        else if (c.In.KeyPressed(Keys.Period))
        {
            var w = Wm.Focused is { Immersive: true } f ? f : Wm.TopImmersive;
            if (w != null)
            {
                bool left = c.In.Shift || (Wm.Snapped == w && !Wm.SnappedLeft);
                Wm.SnapImmersive(w, c, left);
            }
            _winCombo = true;
        }
        else if (c.In.KeyPressed(Keys.Left)) { Wm.SnapFocused(c, -1, 0); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Right)) { Wm.SnapFocused(c, 1, 0); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Up)) { Wm.SnapFocused(c, 0, -1); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Down)) { Wm.SnapFocused(c, 0, 1); _winCombo = true; }
        else return;

        Taskbar.CloseStart(c);
        c.KeyboardHandled = true;
    }

    /// <summary>Set while a Win+key combination is running, so letting the
    /// Windows key go afterwards does not also open the Start menu.</summary>
    bool _winCombo;

    void HandleWindowsKeyRelease(UiContext c)
    {
        if (!c.In.KeyReleased(Keys.LWin) && !c.In.KeyReleased(Keys.RWin)) return;

        if (_winCombo) { _winCombo = false; return; }
        ToggleStartUi(c);
    }

    /// <summary>What the Start button and the Windows key both do. Version 8
    /// opens the tile board; a system told to keep the old menu opens that
    /// instead, and everything else in the shell is unaware of the difference.</summary>
    public void ToggleStartUi(UiContext c)
    {
        if (Settings.UseStartScreen)
        {
            Taskbar.CloseStart(c);
            Charms.Close(c);
            Start.Toggle(c);
        }
        else
        {
            Start.Close(c);
            Taskbar.ToggleStart(c);
        }
    }

    // ---- boot / shutdown screens ----------------------------------------

    /// <summary>Runs the first-run questions. When they are answered the
    /// machine starts for real, from POST, like any other start.</summary>
    void DrawSetup(UiContext c)
    {
        if (!Setup.Draw(c)) return;

        UserName = Setup.UserName;
        SetPhase(ShellPhase.Post, c);
        Audio.Play(Sfx.Logon, 0.8f);
    }

    /// <summary>Name the account goes by, chosen during setup and kept in the
    /// registry under <c>Software\Miminus\Owner</c> — which is where a
    /// registered owner has always been.</summary>
    public string UserName
    {
        get => Registry.RegisteredOwner;
        set => Registry.RegisteredOwner = value;
    }

    void DrawPost(UiContext c)
    {
        c.R.Clear(Color.Black);

        if (c.Time >= _postNext && _postShown < _postLines.Count)
        {
            _postShown++;
            _postNext = c.Time + (_postLines[_postShown - 1].Length == 0 ? 0.04 : 0.13);
            if (_postShown % 3 == 0) Audio.Play(Sfx.Tick, 0.25f, 0.8f);
        }

        float y = 24;
        for (int i = 0; i < _postShown; i++)
        {
            c.F.Mono.Draw(c.R, _postLines[i], 32, y, Color.Rgb(0xD8D8D8));
            y += c.F.Mono.Height + 2;
        }

        // Blinking block cursor at the end.
        if (_postShown >= _postLines.Count && (int)(c.Time * 2) % 2 == 0)
            c.R.FillRect(new Rect(32, y, 9, c.F.Mono.Height), Color.Rgb(0xD8D8D8));

        c.F.Mono.Draw(c.R, "Press DEL to enter SETUP", 32, c.ScreenH - 40, Color.Rgb(0x808080));

        bool skip = c.In.Pressed(MouseButton.Left) || c.In.TypedChars.Count > 0 ||
                    c.In.KeyPressed(Keys.Escape) || c.In.KeyPressed(Keys.Space);
        if (skip) _postShown = _postLines.Count;

        if (_postShown >= _postLines.Count && (c.Time - _postNext > 0.7 || skip))
            SetPhase(ShellPhase.Booting, c);
    }

    /// <summary>The loading screen.
    ///
    /// Version 8 threw away the marching blocks: what boots now is a wordmark
    /// standing still in the middle of a black screen with a ring of dots going
    /// round underneath it, which is the whole of the animation and, famously,
    /// the whole of the information. The dots are eased rather than evenly
    /// spaced, so they bunch at the top of the circle and stretch at the bottom
    /// — the detail that makes the original read as a wheel rather than a
    /// carousel.</summary>
    void DrawBootSplash(UiContext c)
    {
        c.R.Clear(Color.Black);
        double t = Elapsed(c);

        float cx = c.ScreenW * 0.5f, cy = c.ScreenH * 0.40f;

        // The four flat panes, the same mark the wallpaper and the tiles wear.
        float pane = 34, gap = 6;
        Color[] cols =
        {
            Color.Rgb(0x2D89EF), Color.Rgb(0x00ABA9),
            Color.Rgb(0x00A300), Color.Rgb(0xE3A21A),
        };
        // They arrive one at a time over the first second, which is the only
        // thing on this screen that reports progress at all.
        for (int i = 0; i < 4; i++)
        {
            float appear = (float)Math.Clamp((t - i * 0.13) / 0.3, 0, 1);
            if (appear <= 0) continue;

            float ox = (i % 2 == 0 ? -1 : 1) * (pane + gap) * 0.5f;
            float oy = (i < 2 ? -1 : 1) * (pane + gap) * 0.5f;
            float grow = 0.6f + 0.4f * appear;

            c.R.FillRect(new Rect(cx + ox - pane * grow * 0.5f, cy + oy - pane * grow * 0.5f,
                                  pane * grow, pane * grow),
                         cols[i].WithAlpha((byte)(255 * appear)));
        }

        // Wordmark under the mark.
        string title = L.T("shell.miminus_os");
        float tw = c.F.Big.Measure(title);
        c.F.Big.Draw(c.R, title, cx - tw * 0.5f, cy + pane + 26, Color.White);

        string version = L.T("boot.version");
        float vw = c.F.Ui.Measure(version);
        c.F.Ui.Draw(c.R, version, cx - vw * 0.5f, cy + pane + 30 + c.F.Big.Height,
                    Color.Rgb(0x7A8695));

        // The ring: five dots round a circle, eased so they gather and spread.
        float ringY = cy + pane + 118;
        float radius = 26;
        for (int i = 0; i < 5; i++)
        {
            double phase = (t * 0.62 - i * 0.055) % 1.0;
            if (phase < 0) phase += 1;

            float eased = Smoothstep((float)phase);
            float ang = -MathF.PI * 0.5f + eased * MathF.PI * 2;

            // Faded in and out at the ends of the lap, so a dot appears rather
            // than jumps back to the start.
            float fade = MathF.Min(1, MathF.Min((float)phase, 1 - (float)phase) * 8);
            c.R.FillCircle(cx + MathF.Cos(ang) * radius, ringY + MathF.Sin(ang) * radius,
                           2.6f, Color.Rgba(0xFFFFFF, (byte)(235 * fade)));
        }

        string copy = L.T("shell.2010_miminus_written_from_scratch");
        float cw = c.F.Ui.Measure(copy);
        c.F.Ui.Draw(c.R, copy, cx - cw * 0.5f, c.ScreenH - 46, Color.Rgb(0x5A6572));

        if (t > 3.4 || c.In.Pressed(MouseButton.Left) || c.In.KeyPressed(Keys.Escape))
        {
            _lock.Reset();
            SetPhase(Settings.ShowLockScreen ? ShellPhase.Locked : ShellPhase.Welcome, c);
            Audio.Play(Sfx.Logon, 0.7f);
        }
    }

    static float Smoothstep(float t) => t * t * (3 - 2 * t);

    /// <summary>Runs the lock screen. When the curtain is finally out of the
    /// way the logon screen is underneath it, which is the order version 8
    /// put them in.</summary>
    void DrawLockScreen(UiContext c)
    {
        int unread = Updates.State == UpdateState.Available ? 1 : 0;
        if (!_lock.Draw(c, Now, unread)) return;

        Audio.Play(Sfx.Unlock, 0.6f);
        SetPhase(ShellPhase.Welcome, c);
    }

    /// <summary>Locks the machine: Win+L, the account menu on the board and
    /// «Спящий режим» in the settings charm all arrive here.</summary>
    public void LockScreenNow(UiContext c)
    {
        Start.Close(c);
        Charms.Close(c);
        Taskbar.CloseStart(c);
        Menus.Close();
        _lock.Reset();
        Audio.Play(Sfx.Lock, 0.7f);
        SetPhase(ShellPhase.Locked, c);
    }

    /// <summary>The logon screen the lock screen lifts off, in version 8's
    /// arrangement: the accent colour edge to edge, the account in the middle
    /// of it — a round picture, the name under it, and a box that asks for a
    /// password with an arrow at the end — and the two corner buttons nobody
    /// ever pressed on purpose.
    ///
    /// There is no password. Typing something and pressing the arrow logs on,
    /// and so does pressing the arrow with nothing typed, because the account
    /// has never had one and the box is there for the shape of it.</summary>
    void DrawWelcome(UiContext c)
    {
        var screen = new Rect(0, 0, c.ScreenW, c.ScreenH);
        var accent = Theme.MetroAccent;

        c.R.FillRectV(screen, accent.Shade(0.9f), accent.Shade(0.62f));
        c.R.FillRectH(new Rect(screen.X, screen.Y, screen.W * 0.55f, screen.H),
                      Color.Rgba(0xFFFFFF, 20), Color.Transparent);

        // ---- the account, in the middle -------------------------------------
        float cx = screen.CenterX;
        float cy = screen.CenterY - 40;

        // The round picture version 8 used, drawn from two circles behind a
        // circular clip — the same head and shoulders the Start screen wears.
        float rad = 54;
        c.R.FillCircle(cx, cy, rad + 3, Color.Rgba(0xFFFFFF, 90));
        c.R.FillCircle(cx, cy, rad, Color.Rgb(0xE8EEF4));
        c.R.PushClip(new Rect(cx - rad, cy - rad, rad * 2, rad * 2));
        c.R.FillCircle(cx, cy - rad * 0.22f, rad * 0.34f, accent.Shade(0.8f));
        c.R.FillCircle(cx, cy + rad * 0.72f, rad * 0.62f, accent.Shade(0.8f));
        c.R.PopClip();

        float nameW = c.F.Big.Measure(UserName);
        c.F.Big.Draw(c.R, UserName, cx - nameW * 0.5f, cy + rad + 16, Color.White);

        // ---- the password box, which is a formality --------------------------
        float boxY = cy + rad + 16 + c.F.Big.Height + 18;
        var field = new Rect(cx - 130, boxY, 220, 32);
        var go = new Rect(field.Right, boxY, 32, 32);

        c.R.FillRect(field, Color.White);
        c.R.PushClip(field.Deflate(8, 0, 8, 0));
        c.F.Ui.Draw(c.R, new string('•', Math.Min(_password.Length, 18)), field.X + 10,
                    field.CenterY - c.F.Ui.Height * 0.5f, Color.Rgb(0x202020));
        if (_password.Length == 0)
            c.F.Ui.Draw(c.R, L.T("logon.password"), field.X + 10,
                        field.CenterY - c.F.Ui.Height * 0.5f, Color.Rgb(0x9A9A9A));
        c.R.PopClip();

        bool goHot = c.Hovering(go);
        c.R.FillRect(go, goHot ? Color.Rgb(0xF0F0F0) : Color.White);
        c.R.FillRect(new Rect(go.X, go.Y + 6, 1, go.H - 12), Color.Rgb(0xD0D0D0));
        W.Arrow(c, go, 1, accent.Shade(0.7f), 5f);

        c.F.Small.DrawCentered(c.R, L.T("logon.no_password"),
                               new Rect(field.X, go.Bottom + 10, field.W + go.W, 16),
                               Color.Rgba(0xFFFFFF, 170));

        // ---- the two corners --------------------------------------------------
        var ease = new Rect(screen.X + 28, screen.Bottom - 56, 30, 30);
        c.R.DrawCircle(ease.CenterX, ease.CenterY, 14,
                       Color.Rgba(0xFFFFFF, c.Hovering(ease) ? (byte)240 : (byte)150), 1.8f);
        c.R.FillCircle(ease.CenterX, ease.CenterY, 5, Color.Rgba(0xFFFFFF, 200));
        c.Tooltip(ease, L.T("logon.ease_of_access"));
        if (c.Clicked(ease))
            Menus.Open(new List<MenuItem>
            {
                MenuItem.Check(L.T("access.magnifier"), Settings.Magnifier,
                               () => Settings.Magnifier = !Settings.Magnifier),
                MenuItem.Check(L.T("access.on_screen_keyboard"), Settings.OnScreenKeyboard,
                               () => Settings.OnScreenKeyboard = !Settings.OnScreenKeyboard),
                MenuItem.Check(L.T("access.narrator"), Settings.Narrator,
                               () => Settings.Narrator = !Settings.Narrator),
                MenuItem.Check(L.T("access.high_contrast"), ThemeId == ThemeId.HighContrast,
                               () => ToggleHighContrast(c)),
            }, ease.X, ease.Y - 100, this, c);

        var power = new Rect(screen.Right - 58, screen.Bottom - 56, 30, 30);
        bool powerHot = c.Hovering(power);
        c.R.DrawCircle(power.CenterX, power.CenterY, 14,
                       Color.Rgba(0xFFFFFF, powerHot ? (byte)240 : (byte)150), 1.8f);
        Icons.Draw(c.R, IconId.Power, power.Deflate(7));
        c.Tooltip(power, L.T("charm.power"));
        if (c.Clicked(power))
        {
            var off = MenuItem.Of(L.T("charm.shutdown"), () => BeginShutdown(c), IconId.Shutdown);
            var restart = MenuItem.Of(L.T("charm.restart"), () => Restart(c), IconId.Power);
            var items = Settings.PowerButtonAction == 0
                ? new List<MenuItem> { off, restart }
                : new List<MenuItem> { restart, off };
            items[0].Bold = true;
            Menus.Open(items, power.X - 140, power.Y - 60, this, c);
        }

        // ---- getting in --------------------------------------------------------
        foreach (char ch in c.In.TypedChars)
        {
            if (ch == '\b') { if (_password.Length > 0) _password = _password[..^1]; continue; }
            if (ch is '\r' or '\n') { LogOn(c); return; }
            if (ch >= ' ' && _password.Length < 32) _password += ch;
        }
        if (c.In.KeyPressed(Keys.Back) && _password.Length > 0) _password = _password[..^1];

        if (c.Clicked(go) || c.In.KeyPressed(Keys.Enter)) LogOn(c);
    }

    /// <summary>What is typed into the logon box. It is never checked against
    /// anything: the account has no password and the box is there for the
    /// shape of the screen.</summary>
    string _password = "";

    void LogOn(UiContext c)
    {
        _password = "";
        SetPhase(ShellPhase.Running, c);
        Desktop.Relayout(c.ScreenW, c.ScreenH);
    }

    void DrawLogOff(UiContext c)
    {
        DimOverlay(c, L.T("shell.logging_off"));
        if (Elapsed(c) > 1.8)
        {
            Wm.CloseAll(c);
            _startupSoundPlayed = false;
            _networkBalloonShown = false;
            _lock.Reset();
            SetPhase(Settings.ShowLockScreen ? ShellPhase.Locked : ShellPhase.Welcome, c);
        }
    }

    void DrawShuttingDown(UiContext c)
    {
        DimOverlay(c, L.T("shell.shutting_down"));
        if (Elapsed(c) <= 2.6) return;

        // An update restart ends the process instead of parking on the
        // "safe to turn off" screen: the installer is waiting for it to exit.
        if (_restartPending) ExitRequested = true;
        else SetPhase(ShellPhase.PoweredOff, c);
    }

    void DimOverlay(UiContext c, string message)
    {
        // Keep the last frame of the desktop underneath, dimmed.
        Desktop.Draw(c);
        c.R.FillRect(new Rect(0, 0, c.ScreenW, c.ScreenH), Color.Rgba(0x0A2E5A, 200));

        var panel = new Rect(c.ScreenW * 0.5f - 190, c.ScreenH * 0.5f - 60, 380, 120);
        c.R.RoundedRect(panel, 8, Color.Rgba(0x14477E, 235), Color.Rgba(0xFFFFFF, 60), 1);

        float mw = c.F.Big.Measure(message);
        c.F.Big.Draw(c.R, message, panel.CenterX - mw * 0.5f, panel.Y + 26, Color.White);

        // Indeterminate sweep.
        var bar = new Rect(panel.X + 40, panel.Bottom - 34, panel.W - 80, 10);
        c.R.RoundedRect(bar, 3, Color.Rgba(0x000000, 90));
        float p = (float)((c.Time * 0.8) % 1.0);
        c.R.RoundedRect(new Rect(bar.X + p * (bar.W - 50), bar.Y + 1, 48, bar.H - 2), 3,
                        Color.Rgb(0x7FC8FF));
    }

    void DrawPoweredOff(UiContext c)
    {
        c.R.Clear(Color.Black);
        string a = L.T("shell.it_is_now_safe_to_turn_off_your_computer");
        string b = L.T("shell.press_any_key_to_power_on_again");

        float aw = c.F.Big.Measure(a);
        c.F.Big.Draw(c.R, a, (c.ScreenW - aw) * 0.5f, c.ScreenH * 0.44f, Color.Rgb(0xE0A020));
        float bw = c.F.Ui.Measure(b);
        c.F.Ui.Draw(c.R, b, (c.ScreenW - bw) * 0.5f, c.ScreenH * 0.44f + c.F.Big.Height + 14,
                    Color.Rgb(0x808080));

        if (c.In.TypedChars.Count > 0 || c.In.Pressed(MouseButton.Left) ||
            c.In.KeyPressed(Keys.Enter) || c.In.KeyPressed(Keys.Space))
        {
            _postShown = 0;
            _postNext = 0;
            _startupSoundPlayed = false;
            _networkBalloonShown = false;
            SetPhase(ShellPhase.Post, c);
        }
    }

    /// <summary>Jumps past POST, the splash and the welcome screen. Used by
    /// --skip-boot and by the screenshot mode.</summary>
    public void SkipToDesktop()
    {
        _postShown = _postLines.Count;
        Phase = ShellPhase.Running;
    }

    /// <summary>Starts on the lock screen, for --open=lock and the screenshots
    /// that want to show it.</summary>
    public void SkipToLockScreen()
    {
        _postShown = _postLines.Count;
        _lock.Reset();
        Phase = ShellPhase.Locked;
    }

    public void BeginLogOff(UiContext c)
    {
        Audio.Play(Sfx.Logoff, 0.8f);
        SetPhase(ShellPhase.LoggingOff, c);
    }

    BlueScreen _stop;

    /// <summary>True once the system has stopped on an error and is showing
    /// the reason. The host stops asking it to do anything else.</summary>
    public bool Stopped => Phase == ShellPhase.Stopped;

    /// <summary>Takes the system down on an unhandled error. Everything that
    /// could throw again — audio, the window list, the drag in progress — is
    /// let go of first, so the screen that explains the fault cannot cause a
    /// second one.</summary>
    public void Crash(UiContext c, Exception ex)
    {
        if (Stopped) return;

        _stop = new BlueScreen(ex, c.Time);
        SetPhase(ShellPhase.Stopped, c);

        try
        {
            Audio.StopMusic();
            Audio.Muted = true;
            Menus.Close();
            Drag.Cancel();
        }
        catch
        {
            // Whatever state the system was left in, the report still shows.
        }

        Console.Error.WriteLine(ex);
    }

    void DrawBlueScreen(UiContext c)
    {
        _stop?.Draw(c, c.Time);

        // The only way out is a restart, which is where the videos leave it too.
        if (_stop != null && _stop.DumpComplete(c.Time) && BlueScreen.RestartRequested(c))
            Restart(c);
    }

    /// <summary>Back to POST with everything closed, as after a real stop.</summary>
    public void Restart(UiContext c)
    {
        _stop = null;
        Wm.CloseAll(c);
        Audio.Muted = false;
        _startupSoundPlayed = false;
        _networkBalloonShown = false;
        _updateCheckStarted = false;
        _postShown = 0;
        _postNext = 0;
        SetPhase(ShellPhase.Post, c);
    }

    public void BeginShutdown(UiContext c)
    {
        Audio.StopMusic();
        Audio.Play(Sfx.Shutdown, 0.95f);
        SetPhase(ShellPhase.ShuttingDown, c);
    }

    /// <summary>Puts the downloaded build in place and restarts into it. The
    /// shutdown sequence plays first, which is also the delay the installer
    /// needs: it waits for this process to go away before copying over it.</summary>
    public void RestartForUpdate(UiContext c)
    {
        if (!Updates.Install(out string error))
        {
            MessageBox(c, L.T("update.title"), error, MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
            return;
        }

        _restartPending = true;
        BeginShutdown(c);
    }

    public void ShowShutdownDialog(UiContext c)
    {
        var w = Find<ShutdownWindow>();
        if (w != null) { Wm.Focus(w); return; }
        Wm.Open(new ShutdownWindow(), c);
    }

    // ---- tooltip and cursor ---------------------------------------------

    string _lastTooltip;
    double _tooltipSince;

    void DrawTooltip(UiContext c)
    {
        if (string.IsNullOrEmpty(c.TooltipText)) { _lastTooltip = null; return; }

        if (c.TooltipText != _lastTooltip)
        {
            _lastTooltip = c.TooltipText;
            _tooltipSince = c.Time;
        }
        if (c.Time - _tooltipSince < 0.45) return;   // hover delay

        var f = c.F.Ui;
        var lines = f.Wrap(c.TooltipText, 320);
        float w = lines.Max(l => f.Measure(l)) + 12;
        float h = lines.Count * (f.Height + 2) + 6;

        float x = MathF.Min(c.TooltipX, c.ScreenW - w - 4);
        float y = MathF.Min(c.TooltipY, c.ScreenH - h - 4);

        var box = new Rect(x, y, w, h);
        c.R.FillRect(box.Offset(2, 2), Color.Rgba(0x000000, 40));
        c.R.FillRect(box, Theme.TooltipBack);
        c.R.DrawRect(box, Theme.TooltipBorder);

        float ty = box.Y + 3;
        foreach (string line in lines)
        {
            f.Draw(c.R, line, box.X + 6, ty, Theme.TooltipText);
            ty += f.Height + 2;
        }
    }

    /// <summary>The pointer, drawn from primitives at whatever size the
    /// accessibility setting asks for.
    ///
    /// Every shape here is written in units that are multiplied by <c>k</c> on
    /// the way out, so «крупный указатель» is one number rather than a second
    /// set of pictures — which is the advantage of never having had a cursor
    /// file in the first place.</summary>
    void DrawCursor(UiContext c)
    {
        float x = c.MouseX, y = c.MouseY;
        float k = Math.Clamp(Settings.CursorScale, 1f, 3f);
        Color fill = Color.White, edge = Color.Black;

        switch (c.Cursor)
        {
            case CursorShape.Text:
                c.R.FillRect(new Rect(x - 0.5f * k, y - 8 * k, 1.6f * k, 16 * k), edge);
                c.R.FillRect(new Rect(x - 3 * k, y - 8 * k, 6 * k, 1.4f * k), edge);
                c.R.FillRect(new Rect(x - 3 * k, y + 6.6f * k, 6 * k, 1.4f * k), edge);
                break;

            case CursorShape.SizeWE: DoubleArrow(c, x, y, true, k); break;
            case CursorShape.SizeNS: DoubleArrow(c, x, y, false, k); break;

            case CursorShape.SizeNWSE:
            case CursorShape.SizeNESW:
            {
                float d = c.Cursor == CursorShape.SizeNWSE ? 1 : -1;
                c.R.Line(x - 6 * k * d, y - 6 * k, x + 6 * k * d, y + 6 * k, edge, 3.4f * k);
                c.R.Line(x - 6 * k * d, y - 6 * k, x + 6 * k * d, y + 6 * k, fill, 1.6f * k);
                c.R.FillTriangle(x - 9 * k * d, y - 9 * k, x - 9 * k * d + 8 * k * d, y - 9 * k,
                                 x - 9 * k * d, y - 1 * k, fill);
                c.R.FillTriangle(x + 9 * k * d, y + 9 * k, x + 9 * k * d - 8 * k * d, y + 9 * k,
                                 x + 9 * k * d, y + 1 * k, fill);
                break;
            }

            case CursorShape.Move:
                c.R.Line(x - 8 * k, y, x + 8 * k, y, edge, 3.4f * k);
                c.R.Line(x, y - 8 * k, x, y + 8 * k, edge, 3.4f * k);
                c.R.Line(x - 8 * k, y, x + 8 * k, y, fill, 1.6f * k);
                c.R.Line(x, y - 8 * k, x, y + 8 * k, fill, 1.6f * k);
                break;

            case CursorShape.Hand:
                c.R.RoundedRect(new Rect(x - 1 * k, y + 1 * k, 10 * k, 12 * k), 3 * k, fill, edge, k);
                c.R.RoundedRect(new Rect(x + 1 * k, y - 7 * k, 4 * k, 10 * k), 2 * k, fill, edge, k);
                break;

            case CursorShape.Cross:
                c.R.Line(x - 9 * k, y, x + 9 * k, y, edge, 1.4f * k);
                c.R.Line(x, y - 9 * k, x, y + 9 * k, edge, 1.4f * k);
                break;

            case CursorShape.Wait:
            {
                float a = (float)(c.Time * 5);
                c.R.FillCircle(x + 8 * k, y + 8 * k, 9 * k, Color.Rgba(0xFFFFFF, 220));
                c.R.DrawCircle(x + 8 * k, y + 8 * k, 9 * k, edge, 1.4f * k);
                for (int i = 0; i < 8; i++)
                {
                    float ang = a + i * MathF.PI / 4;
                    byte al = (byte)(40 + i * 25);
                    c.R.FillCircle(x + 8 * k + MathF.Cos(ang) * 6 * k,
                                   y + 8 * k + MathF.Sin(ang) * 6 * k, 1.6f * k,
                                   Color.Rgba(0x1E5FD8, al));
                }
                break;
            }

            default:
                c.R.FillTriangle(x, y, x, y + 17 * k, x + 4.5f * k, y + 12.5f * k, edge);
                c.R.FillTriangle(x, y, x + 4.5f * k, y + 12.5f * k, x + 12 * k, y + 12.5f * k, edge);
                c.R.FillTriangle(x + 1.3f * k, y + 2.4f * k, x + 1.3f * k, y + 14f * k,
                                 x + 4.7f * k, y + 10.8f * k, fill);
                c.R.FillTriangle(x + 1.3f * k, y + 2.4f * k, x + 4.7f * k, y + 10.8f * k,
                                 x + 9.4f * k, y + 10.8f * k, fill);
                c.R.Line(x + 6.6f * k, y + 11.6f * k, x + 9.6f * k, y + 17.6f * k, edge, 4.2f * k);
                c.R.Line(x + 6.6f * k, y + 11.6f * k, x + 9.6f * k, y + 17.6f * k, fill, 2.2f * k);
                break;
        }
    }

    static void DoubleArrow(UiContext c, float x, float y, bool horizontal, float k)
    {
        Color fill = Color.White, edge = Color.Black;
        if (horizontal)
        {
            c.R.Line(x - 7 * k, y, x + 7 * k, y, edge, 3.4f * k);
            c.R.Line(x - 7 * k, y, x + 7 * k, y, fill, 1.6f * k);
            c.R.FillTriangle(x - 11 * k, y, x - 5 * k, y - 5 * k, x - 5 * k, y + 5 * k, fill);
            c.R.FillTriangle(x + 11 * k, y, x + 5 * k, y - 5 * k, x + 5 * k, y + 5 * k, fill);
        }
        else
        {
            c.R.Line(x, y - 7 * k, x, y + 7 * k, edge, 3.4f * k);
            c.R.Line(x, y - 7 * k, x, y + 7 * k, fill, 1.6f * k);
            c.R.FillTriangle(x, y - 11 * k, x - 5 * k, y - 5 * k, x + 5 * k, y - 5 * k, fill);
            c.R.FillTriangle(x, y + 11 * k, x - 5 * k, y + 5 * k, x + 5 * k, y + 5 * k, fill);
        }
    }

    // ---- window helpers --------------------------------------------------

    public T Find<T>() where T : OsWindow => Wm.Find<T>();

    public void MessageBox(UiContext c, string title, string message, MsgButtons buttons,
                           IconId icon, Action<MsgResult> onResult, Sfx sound = Sfx.Info)
        => Wm.Open(new MessageBoxWindow(title, message, buttons, icon, onResult, sound), c);

    public void CascadeWindows(UiContext c)
    {
        int n = 0;
        foreach (var w in Wm.Windows.Where(x => x.State != WindowState.Minimized && x.ShowInTaskbar))
        {
            w.State = WindowState.Normal;
            w.Bounds = new Rect(24 + n * 26, 20 + n * 26,
                                MathF.Min(720, c.ScreenW - 80), MathF.Min(520, c.ScreenH - 140));
            n++;
        }
        c.Sound(Sfx.Navigate, 0.5f);
    }

    public void TileWindows(UiContext c, bool vertical)
    {
        var wins = Wm.Windows.Where(x => x.State != WindowState.Minimized && x.ShowInTaskbar).ToList();
        if (wins.Count == 0) return;

        var area = Taskbar.WorkArea(c);
        int cols = vertical ? wins.Count : 1;
        int rows = vertical ? 1 : wins.Count;
        if (wins.Count > 3)
        {
            cols = (int)MathF.Ceiling(MathF.Sqrt(wins.Count));
            rows = (int)MathF.Ceiling(wins.Count / (float)cols);
        }

        float cw = area.W / cols, ch = area.H / rows;
        for (int i = 0; i < wins.Count; i++)
        {
            wins[i].State = WindowState.Normal;
            wins[i].Bounds = new Rect(MathF.Round((i % cols) * cw), MathF.Round((i / cols) * ch),
                                      MathF.Floor(cw), MathF.Floor(ch));
        }
        c.Sound(Sfx.Navigate, 0.5f);
    }

    public void ToggleMute(UiContext c)
    {
        Audio.Muted = !Audio.Muted;
        Audio.MasterVolume = Audio.MasterVolume;   // re-apply listener gain
        if (!Audio.Muted) c.Sound(Sfx.Click, 0.6f);
    }

    public void ShowNetworkBalloon(UiContext c)
    {
        Taskbar.Notify(new Balloon
        {
            Title = L.T("shell.service_message"),
            Text = L.T("shell.network_checked_verify_your_connection_setti"),
            Icon = IconId.DlgWarning,
            OnClick = () => Launch(c, "network", null),
        }, c);
    }

    /// <summary>What the «Уведомления» square in the settings charm has to
    /// say: the update, if there is one, and otherwise that there is nothing.</summary>
    public void ShowUpdateNotice(UiContext c)
    {
        if (Updates.State == UpdateState.Available) ShowUpdateBalloon(c);
        else
            Taskbar.Notify(new Balloon
            {
                Title = L.T("charm.notifications"),
                Text = L.T("charm.no_notifications"),
                Icon = IconId.DlgInfo,
            }, c);
    }

    /// <summary>«Доступны обновления для МИМИНУС ОС» — the tray balloon that
    /// opens the update centre when clicked.</summary>
    public void ShowUpdateBalloon(UiContext c)
    {
        Taskbar.Notify(new Balloon
        {
            Title = L.T("update.balloon_title"),
            Text = L.F("update.balloon_text", Updates.Latest?.Name ?? ""),
            Icon = IconId.Shield,
            OnClick = () => Launch(c, "update", null),
        }, c);
    }

    public void ShowProperties(UiContext c, string label, VNode node, IconId icon)
        => Wm.Open(new PropertiesWindow(label, node, icon), c);

    /// <summary>The "Открыть с помощью" submenu, listing the same programs the
    /// reference video shows.</summary>
    public List<MenuItem> BuildOpenWithMenu(UiContext c, VNode node)
    {
        var items = new List<MenuItem>
        {
            MenuItem.Of("Nero PhotoSnap Viewer", () => NotInstalled(c, "Nero PhotoSnap Viewer"), IconId.ImageFile),
            MenuItem.Of("Paint.NET", () => Launch(c, "paint", node), IconId.Paint),
            MenuItem.Of("Opera Internet Browser", () => Launch(c, "browser", node), IconId.Opera),
            MenuItem.Of("Microsoft Office Picture Manager", () => NotInstalled(c, "Microsoft Office Picture Manager"), IconId.ImageFile),
            MenuItem.Of(L.T("shell.notepad"), () => Launch(c, "notepad", node), IconId.Notepad),
            MenuItem.Sep(),
            MenuItem.Of(L.T("shell.choose_program"),
                        () => Wm.Open(new OpenWithWindow(node), c)),
        };
        return items;
    }

    void NotInstalled(UiContext c, string program)
    {
        MessageBox(c, L.T("shell.error"),
            L.F("shell.could_not_start_0_miminus_os_does_not_need_i", program),
            MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
    }

    // ---- launcher --------------------------------------------------------

    /// <summary>Starts a program by id. Windows that should be unique are
    /// focused instead of duplicated.</summary>
    public void Launch(UiContext c, string app, VNode node)
    {
        if (string.IsNullOrEmpty(app))
        {
            if (node != null && node.IsContainer) app = "explorer";
            else if (node?.Launch != null) app = node.Launch;
            else if (node is { Kind: NodeKind.Unknown }) app = "unknownfile";
            else return;
        }

        // A registered program wins; the cases below are shell notices rather
        // than programs, so they stay here.
        var program = Programs.Find(app);
        if (program != null)
        {
            if (program.Singleton)
            {
                var running = Wm.Windows.FirstOrDefault(w => w.ProgramId == program.Id);
                if (running != null) { Wm.Focus(running); return; }
            }

            // The DLL is read here, not at startup: this is the first moment
            // the OS actually needs it.
            var window = Programs.Create(program, this, node);
            if (window != null)
            {
                window.ProgramId = program.Id;
                Wm.Open(window, c);
            }
            else foreach (string failure in Programs.Failures.TakeLast(1))
                MessageBox(c, L.T("shell.miminus_os"), L.F("shell.program_failed_to_start", failure),
                           MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
            return;
        }

        switch (app)
        {
            case "run": FocusOrOpen<RunWindow>(c, () => new RunWindow()); break;
            case "about": FocusOrOpen<AboutWindow>(c, () => new AboutWindow()); break;
            case "openwith": Wm.Open(new OpenWithWindow(node), c); break;

            // "Для чего нужен бэкап? Мы открываем бэкап и закрываем бэкап."
            case "backup":
                MessageBox(c, node?.Name ?? L.T("fs.backup"), L.T("fs.backup_opened"),
                    MsgButtons.Ok, IconId.Archive, null, Sfx.Info);
                break;

            // Part 2: Windows Media Player cannot open the invented file type.
            case "unknownfile":
                MessageBox(c, "Windows Media Player", L.T("fs.wmp_cannot_open"),
                    MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
                break;

            case "search":
                MessageBox(c, L.T("shell.search"), L.T("shell.no_search_needed_in_miminus_os_everything_is"),
                    MsgButtons.Ok, IconId.Search, null, Sfx.Info);
                break;

            case "help":
                MessageBox(c, L.T("shell.help_and_support"), L.T("shell.help_if_something_does_not_work_it_is_by_des"),
                    MsgButtons.Ok, IconId.Help, null, Sfx.Info);
                break;

            case "network":
                MessageBox(c, L.T("shell.local_area_connection"), L.T("shell.network_checked_verify_your_connection_setti_2"),
                    MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
                break;

            case "printers":
                MessageBox(c, L.T("shell.printers_and_faxes"), L.T("shell.no_printers_are_installed"),
                    MsgButtons.Ok, IconId.Printer, null, Sfx.Info);
                break;

            case "mail":
                MessageBox(c, "Outlook Express", L.T("shell.no_mail_account_is_configured"),
                    MsgButtons.Ok, IconId.Mail, null, Sfx.Warning);
                break;

            case "clock":
                MessageBox(c, L.T("shell.date_and_time"),
                    L.LongDate(Now) + "\n" + Now.ToString("HH:mm:ss"),
                    MsgButtons.Ok, IconId.Clock, null, Sfx.Info);
                break;

            case "shutdown": ShowShutdownDialog(c); break;

            default:
                if (node != null && node.IsContainer) Launch(c, "explorer", node);
                break;
        }
    }

    /// <summary>Launches by a friendly name, including a few shorthands that
    /// carry a document with them. Used by the --open command line switch.</summary>
    public void LaunchByName(UiContext c, string name)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "antivirus": Launch(c, "notepad", Fs.AntivirusFile); break;

            // The first mounted host folder, and the first real text file in it.
            case "mount":
                if (Fs.Mounts.Count > 0) Launch(c, "explorer", Fs.Mounts[0].Node);
                break;

            case "mounttext":
                if (Fs.Mounts.Count > 0)
                {
                    var text = Fs.Mounts[0].Node.Entries
                        .FirstOrDefault(n => n.Kind == NodeKind.TextFile);
                    if (text != null) Launch(c, "notepad", text);
                }
                break;

            case "mountimage":
                if (Fs.Mounts.Count > 0)
                {
                    var img = FindHosted(Fs.Mounts[0].Node, NodeKind.ImageFile);
                    if (img != null) Launch(c, "paint", img);
                }
                break;


            case "scan":
                Launch(c, "notepad", Fs.AntivirusFile);
                (Wm.Windows.LastOrDefault(w => w.ProgramId == "notepad") as IScannable)?.BeginScan();
                break;

            case "revolutionary": Launch(c, "explorer", Fs.Revolutionary); break;
            case "drivec": Launch(c, "explorer", Fs.DriveC); break;
            case "tricks": Launch(c, "explorer", Fs.UsefulTricks); break;
            case "pictures": Launch(c, "explorer", Fs.MyPictures); break;
            case "photo": Launch(c, "paint", Fs.Revolutionary.Children
                .FirstOrDefault(n => n.Kind == NodeKind.ImageFile)); break;
            case "shutdown": ShowShutdownDialog(c); break;
            case "properties": ShowProperties(c, Fs.AntivirusFile.Name, Fs.AntivirusFile, IconId.TextFile); break;
            case "openwith": Wm.Open(new OpenWithWindow(Fs.AntivirusFile), c); break;
            case "startmenu": Taskbar.StartOpen = true; break;

            // The version 8 screens, for --open and for the screenshots.
            case "start":
            case "startscreen": Start.Open(c); break;
            case "charms": Charms.Show(c); break;
            case "settingscharm": Charms.Show(c, Charms.Pane.Settings); break;
            case "switcher": Switcher.Show(); break;
            case "lock": LockScreenNow(c); break;
            case "balloon": ShowNetworkBalloon(c); break;
            default: Launch(c, name.Trim().ToLowerInvariant(), null); break;
        }
    }

    /// <summary>Depth-first search through a mount for the first node of a kind.</summary>
    static VNode FindHosted(VNode root, NodeKind kind)
    {
        foreach (var child in root.Entries)
        {
            if (child.Kind == kind) return child;
            if (child.IsContainer)
            {
                var hit = FindHosted(child, kind);
                if (hit != null) return hit;
            }
        }
        return null;
    }

    void FocusOrOpen<T>(UiContext c, Func<T> make) where T : OsWindow
    {
        var existing = Wm.Find<T>();
        if (existing != null) { Wm.Focus(existing); return; }
        Wm.Open(make(), c);
    }

    /// <summary>Mounts a host directory as a drive and drops a shortcut to it on
    /// the desktop. Returns null when the path does not exist.</summary>
    /// <summary>Loads the program DLLs. Called once, before the first frame.</summary>
    /// <summary>Where installed programs live, and where the packages that are
    /// not installed wait. Both are ordinary folders of ordinary DLLs, which is
    /// what makes installing one a copy and uninstalling it a move.</summary>
    public string AppsDirectory { get; private set; }

    public string StoreDirectory =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "store");

    /// <summary>Installs a package: the assembly is copied into apps/ and the
    /// registry rescans, which is all it takes. Every menu in the system is
    /// built from that registry each time it opens, so the program is in the
    /// Start menu and launchable before the message box has finished closing —
    /// no restart, because nothing was compiled in.</summary>
    public bool InstallPackage(string packagePath, out string error)
    {
        error = null;
        try
        {
            if (AppsDirectory == null || !System.IO.File.Exists(packagePath))
            {
                error = L.T("store.install_missing");
                return false;
            }

            System.IO.Directory.CreateDirectory(AppsDirectory);
            string target = System.IO.Path.Combine(AppsDirectory,
                                                   System.IO.Path.GetFileName(packagePath));
            System.IO.File.Copy(packagePath, target, overwrite: true);

            Programs.LoadFrom(AppsDirectory);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Uninstalls one: its windows are closed, the registry forgets it,
    /// and the assembly goes back to store/ so it can be put on again.</summary>
    public bool UninstallPackage(string assemblyPath, UiContext c, out string error)
    {
        error = null;
        try
        {
            // Nothing from it may still be open, or the file cannot be moved
            // and the types cannot be dropped.
            foreach (var w in Wm.Windows.ToList())
            {
                var entry = Programs.Find(w.ProgramId);
                if (entry != null && string.Equals(entry.AssemblyPath, assemblyPath,
                                                   StringComparison.OrdinalIgnoreCase))
                    w.Close();
            }

            Programs.Forget(assemblyPath);

            System.IO.Directory.CreateDirectory(StoreDirectory);
            string back = System.IO.Path.Combine(StoreDirectory,
                                                 System.IO.Path.GetFileName(assemblyPath));

            // The context may still be finishing its unload, so a delete can
            // fail where a move succeeds; either way the file leaves apps/.
            if (System.IO.File.Exists(back)) System.IO.File.Delete(back);
            System.IO.File.Move(assemblyPath, back);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void LoadPrograms(string appsDirectory)
    {
        AppsDirectory = appsDirectory;
        Programs.LoadFrom(appsDirectory);
        Wm.WindowClosed = w => Programs.WindowClosed(w.ProgramId, _uptime);
        foreach (string failure in Programs.Failures)
            Console.Error.WriteLine("program load failed: " + failure);
    }

    public HostMount MountFolder(string hostPath, string driveLetter, bool writable)
    {
        var mount = Fs.MountHostFolder(hostPath, driveLetter, writable);
        if (mount == null) return null;
        Desktop.AddMountShortcut(mount.Node);
        return mount;
    }

    public void Dispose()
    {
        Wallpapers.Dispose();
        Access.Dispose();
    }
}
