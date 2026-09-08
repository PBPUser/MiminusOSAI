using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

public enum ShellPhase { Setup, Post, Booting, Welcome, Running, LoggingOff, ShuttingDown, PoweredOff, Stopped }

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
    public StartMenu StartMenu { get; }

    public Theme Theme { get; private set; } = Theme.LunaBlue();
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

        L.Changed += () => { /* windows re-read their titles each frame */ };

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

    // ---- frame -----------------------------------------------------------

    public void Frame(UiContext c)
    {
        c.Theme = Theme;
        c.Menus = Menus;
        Menus.Shadows = Settings.MenuShadows;
        Menus.Fade = Settings.MenuTransition && Settings.MenuFade;
        _uptime += c.Dt;
        Audio.SetScreenSize(c.ScreenW, c.ScreenH);

        switch (Phase)
        {
            case ShellPhase.Setup: DrawSetup(c); break;
            case ShellPhase.Post: DrawPost(c); break;
            case ShellPhase.Booting: DrawBootSplash(c); break;
            case ShellPhase.Welcome: DrawWelcome(c); break;
            case ShellPhase.LoggingOff: DrawLogOff(c); break;
            case ShellPhase.ShuttingDown: DrawShuttingDown(c); break;
            case ShellPhase.PoweredOff: DrawPoweredOff(c); break;
            case ShellPhase.Stopped: DrawBlueScreen(c); break;
            default: DrawDesktopSession(c); break;
        }

        DrawTooltip(c);
        // A stopped system has no pointer: nothing on that screen can be
        // clicked, and the original never showed one either.
        if (!Stopped) DrawCursor(c);
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

        bool overChrome =
            Menus.HitTest(c.MouseX, c.MouseY) ||
            (!Taskbar.Retracted && Taskbar.Bounds(c).Contains(c.MouseX, c.MouseY)) ||
            Taskbar.VolumeBounds(c).Contains(c.MouseX, c.MouseY) ||
            (Taskbar.StartOpen && StartMenu.Bounds(c).Contains(c.MouseX, c.MouseY));

        Wm.Update(c, blockWindows: overChrome);

        Desktop.Draw(c);

        // "Keep the taskbar on top" is literally the draw order: off, and
        // windows paint over it.
        if (Settings.TaskbarOnTop)
        {
            Wm.Draw(c);
            Taskbar.Draw(c);
        }
        else
        {
            Taskbar.Draw(c);
            Wm.Draw(c);
        }

        if (Taskbar.StartOpen) StartMenu.Draw(c);
        Desktop.Update(c);
        Drag.Resolve(c);
        Menus.Draw(c);
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
        else if (c.In.Win) HandleWindowsKey(c);
    }

    /// <summary>The Windows key and its combinations.
    ///
    /// The key is taken from the host shell by a low-level hook, so it belongs
    /// to МИМИНУС while the window has the focus. On its own it opens the Start
    /// menu, on the release rather than the press — otherwise every combination
    /// below would flash the menu open on its way through.</summary>
    void HandleWindowsKey(UiContext c)
    {
        if (c.In.KeyPressed(Keys.E)) { Launch(c, "mycomputer", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.R)) { Launch(c, "run", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.F)) { Launch(c, "search", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.L)) { BeginLogOff(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.U)) { Launch(c, "update", null); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.D)) { Wm.MinimizeAll(c); _winCombo = true; }
        else if (c.In.KeyPressed(Keys.Pause)) { Launch(c, "about", null); _winCombo = true; }
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
        Taskbar.ToggleStart(c);
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

    /// <summary>Name the account goes by, chosen during setup.</summary>
    public string UserName = "Admin";

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

    void DrawBootSplash(UiContext c)
    {
        c.R.Clear(Color.Black);
        double t = Elapsed(c);

        float cx = c.ScreenW * 0.5f, cy = c.ScreenH * 0.42f;

        string title = L.T("shell.miminus");
        string sub = L.T("shell.operating_system");

        float tw = c.F.Huge.Measure(title);
        // Glow behind the wordmark.
        c.R.FillCircle(cx, cy + c.F.Huge.Height * 0.5f, 220, Color.Rgba(0x1E5FD8, 26));
        c.F.Huge.Draw(c.R, title, cx - tw * 0.5f, cy, Color.White);

        float sw = c.F.Big.Measure(sub);
        c.F.Big.Draw(c.R, sub, cx - sw * 0.5f, cy + c.F.Huge.Height + 6, Color.Rgb(0xE08A2E));

        // XP-style marching progress blocks.
        var trough = new Rect(cx - 82, cy + c.F.Huge.Height + c.F.Big.Height + 46, 164, 16);
        c.R.RoundedRect(trough, 3, Color.Rgb(0x101418), Color.Rgb(0x50596A), 1);
        for (int i = 0; i < 3; i++)
        {
            double phase = (t * 1.4 + i * 0.14) % 1.6;
            if (phase > 1) continue;
            float x = trough.X + 4 + (float)phase * (trough.W - 30);
            c.R.RoundedRectV(new Rect(x, trough.Y + 3, 8, trough.H - 6), 2,
                             Color.Rgb(0x7FC8FF), Color.Rgb(0x1E5FD8));
        }

        string copy = L.T("shell.2010_miminus_written_from_scratch");
        float cw = c.F.Ui.Measure(copy);
        c.F.Ui.Draw(c.R, copy, cx - cw * 0.5f, c.ScreenH - 46, Color.Rgb(0x9098A8));

        if (t > 3.4 || c.In.Pressed(MouseButton.Left) || c.In.KeyPressed(Keys.Escape))
        {
            SetPhase(ShellPhase.Welcome, c);
            Audio.Play(Sfx.Logon, 0.7f);
        }
    }

    void DrawWelcome(UiContext c)
    {
        // The XP welcome screen: blue field, a divider, one user tile.
        var full = new Rect(0, 0, c.ScreenW, c.ScreenH);
        c.R.FillRectV(new Rect(0, 0, c.ScreenW, c.ScreenH * 0.5f),
                      Color.Rgb(0x2E6FC4), Color.Rgb(0x14477E));
        c.R.FillRectV(new Rect(0, c.ScreenH * 0.5f, c.ScreenW, c.ScreenH * 0.5f),
                      Color.Rgb(0x14477E), Color.Rgb(0x0A2E5A));

        float barY = c.ScreenH * 0.30f;
        float barY2 = c.ScreenH * 0.74f;
        c.R.FillRect(new Rect(0, barY, c.ScreenW, 2), Color.Rgba(0xFFFFFF, 60));
        c.R.FillRect(new Rect(0, barY2, c.ScreenW, 2), Color.Rgba(0xFFFFFF, 60));
        c.R.FillRectV(new Rect(0, barY + 2, c.ScreenW, 26), Color.Rgba(0xFFFFFF, 26), Color.Transparent);

        // Brand on the left half.
        string brand = L.T("shell.miminus_os");
        c.F.Big.Draw(c.R, brand, 60, barY - c.F.Big.Height - 18, Color.White);
        string tag = L.T("shell.our_answer_to_bolgenos");
        c.F.Ui.Draw(c.R, tag, 62, barY - 16, Color.Rgba(0xFFFFFF, 190));

        c.R.FillRect(new Rect(c.ScreenW * 0.5f, barY + 30, 1, barY2 - barY - 60), Color.Rgba(0xFFFFFF, 60));

        // User tile on the right half.
        var tile = new Rect(c.ScreenW * 0.55f, c.ScreenH * 0.42f, 240, 60);
        bool hot = c.Hovering(tile);
        if (hot) c.R.RoundedRect(tile.Inflate(4), 4, Color.Rgba(0xFFFFFF, 40));

        var pic = new Rect(tile.X, tile.Y, 56, 56);
        c.R.RoundedRect(pic, 4, Color.Rgb(0xE8F0FA), Color.White, 2);
        c.R.FillCircle(pic.CenterX, pic.Y + 20, 11, Color.Rgb(0x3C82C8));
        c.R.PushClip(pic);
        c.R.FillCircle(pic.CenterX, pic.Bottom + 4, 20, Color.Rgb(0x3C82C8));
        c.R.PopClip();

        c.F.Big.Draw(c.R, UserName, pic.Right + 14, tile.Y + 6, Color.White);
        c.F.Ui.Draw(c.R, L.T("shell.click_to_log_on"),
                    pic.Right + 16, tile.Y + 6 + c.F.Big.Height + 2, Color.Rgba(0xFFFFFF, 200));

        string hint = L.T("shell.after_logging_on_click_ru_en_in_the_tray_to");
        float hw = c.F.Ui.Measure(hint);
        c.F.Ui.Draw(c.R, hint, (c.ScreenW - hw) * 0.5f, barY2 + 18, Color.Rgba(0xFFFFFF, 150));

        if (c.Clicked(tile) || c.In.KeyPressed(Keys.Enter))
        {
            SetPhase(ShellPhase.Running, c);
            Desktop.Relayout(c.ScreenW, c.ScreenH);
        }
    }

    void DrawLogOff(UiContext c)
    {
        DimOverlay(c, L.T("shell.logging_off"));
        if (Elapsed(c) > 1.8)
        {
            Wm.CloseAll(c);
            _startupSoundPlayed = false;
            _networkBalloonShown = false;
            SetPhase(ShellPhase.Welcome, c);
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

    void DrawCursor(UiContext c)
    {
        float x = c.MouseX, y = c.MouseY;
        Color fill = Color.White, edge = Color.Black;

        switch (c.Cursor)
        {
            case CursorShape.Text:
                c.R.FillRect(new Rect(x - 0.5f, y - 8, 1.6f, 16), edge);
                c.R.FillRect(new Rect(x - 3, y - 8, 6, 1.4f), edge);
                c.R.FillRect(new Rect(x - 3, y + 6.6f, 6, 1.4f), edge);
                break;

            case CursorShape.SizeWE: DoubleArrow(c, x, y, true); break;
            case CursorShape.SizeNS: DoubleArrow(c, x, y, false); break;

            case CursorShape.SizeNWSE:
            case CursorShape.SizeNESW:
            {
                float s = c.Cursor == CursorShape.SizeNWSE ? 1 : -1;
                c.R.Line(x - 6 * s, y - 6, x + 6 * s, y + 6, edge, 3.4f);
                c.R.Line(x - 6 * s, y - 6, x + 6 * s, y + 6, fill, 1.6f);
                c.R.FillTriangle(x - 9 * s, y - 9, x - 9 * s + 8 * s, y - 9, x - 9 * s, y - 1, fill);
                c.R.FillTriangle(x + 9 * s, y + 9, x + 9 * s - 8 * s, y + 9, x + 9 * s, y + 1, fill);
                break;
            }

            case CursorShape.Move:
                c.R.Line(x - 8, y, x + 8, y, edge, 3.4f);
                c.R.Line(x, y - 8, x, y + 8, edge, 3.4f);
                c.R.Line(x - 8, y, x + 8, y, fill, 1.6f);
                c.R.Line(x, y - 8, x, y + 8, fill, 1.6f);
                break;

            case CursorShape.Hand:
                c.R.RoundedRect(new Rect(x - 1, y + 1, 10, 12), 3, fill, edge, 1);
                c.R.RoundedRect(new Rect(x + 1, y - 7, 4, 10), 2, fill, edge, 1);
                break;

            case CursorShape.Cross:
                c.R.Line(x - 9, y, x + 9, y, edge, 1.4f);
                c.R.Line(x, y - 9, x, y + 9, edge, 1.4f);
                break;

            case CursorShape.Wait:
            {
                float a = (float)(c.Time * 5);
                c.R.FillCircle(x + 8, y + 8, 9, Color.Rgba(0xFFFFFF, 220));
                c.R.DrawCircle(x + 8, y + 8, 9, edge, 1.4f);
                for (int i = 0; i < 8; i++)
                {
                    float ang = a + i * MathF.PI / 4;
                    byte al = (byte)(40 + i * 25);
                    c.R.FillCircle(x + 8 + MathF.Cos(ang) * 6, y + 8 + MathF.Sin(ang) * 6, 1.6f,
                                   Color.Rgba(0x1E5FD8, al));
                }
                break;
            }

            default:
                c.R.FillTriangle(x, y, x, y + 17, x + 4.5f, y + 12.5f, edge);
                c.R.FillTriangle(x, y, x + 4.5f, y + 12.5f, x + 12, y + 12.5f, edge);
                c.R.FillTriangle(x + 1.3f, y + 2.4f, x + 1.3f, y + 14f, x + 4.7f, y + 10.8f, fill);
                c.R.FillTriangle(x + 1.3f, y + 2.4f, x + 4.7f, y + 10.8f, x + 9.4f, y + 10.8f, fill);
                c.R.Line(x + 6.6f, y + 11.6f, x + 9.6f, y + 17.6f, edge, 4.2f);
                c.R.Line(x + 6.6f, y + 11.6f, x + 9.6f, y + 17.6f, fill, 2.2f);
                break;
        }
    }

    static void DoubleArrow(UiContext c, float x, float y, bool horizontal)
    {
        Color fill = Color.White, edge = Color.Black;
        if (horizontal)
        {
            c.R.Line(x - 7, y, x + 7, y, edge, 3.4f);
            c.R.Line(x - 7, y, x + 7, y, fill, 1.6f);
            c.R.FillTriangle(x - 11, y, x - 5, y - 5, x - 5, y + 5, fill);
            c.R.FillTriangle(x + 11, y, x + 5, y - 5, x + 5, y + 5, fill);
        }
        else
        {
            c.R.Line(x, y - 7, x, y + 7, edge, 3.4f);
            c.R.Line(x, y - 7, x, y + 7, fill, 1.6f);
            c.R.FillTriangle(x, y - 11, x - 5, y - 5, x + 5, y - 5, fill);
            c.R.FillTriangle(x, y + 11, x - 5, y + 5, x + 5, y + 5, fill);
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

        var area = new Rect(0, 0, c.ScreenW, c.ScreenH - Theme.TaskbarHeight);
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
    public void LoadPrograms(string appsDirectory)
    {
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
    }
}
