using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var opts = Options.Parse(args);
        if (opts.ShowHelp)
        {
            Console.WriteLine(Options.HelpText);
            return 0;
        }

        try
        {
            using var window = new AppWindow("Миминус ОС", opts.Width, opts.Height, opts.Fullscreen);
            using var renderer = new Renderer2D();
            using var fonts = new Fonts();
            using var audio = new AudioEngine();
            using var shell = new ShellHost(audio);

            // Programs live in apps/ next to the executable, one DLL each.
            shell.LoadPrograms(Path.Combine(AppContext.BaseDirectory, "apps"));
            if (opts.UpdateUrl != null) shell.Updates.Source = opts.UpdateUrl;
            Console.WriteLine($"Programs    : {shell.Programs.Count}");

            if (opts.Lang != null) L.Current = opts.Lang.Value;
            if (opts.Muted) audio.Muted = true;
            if (opts.Theme != null) shell.SetTheme(opts.Theme.Value, fonts);
            if (opts.SkipBoot || opts.Open.Count > 0) shell.SkipToDesktop();
            if (opts.Wallpaper != null) shell.SetWallpaper(opts.Wallpaper.Value);

            foreach (var (path, letter) in opts.Mounts)
            {
                var mount = shell.MountFolder(path, letter, opts.MountWritable);
                Console.WriteLine(mount != null
                    ? $"Mounted     : {mount.HostRoot} as {mount.Node.Name}" +
                      (mount.Writable ? " (writable)" : " (read-only)")
                    : $"Mount failed: {path} (not a directory)");
            }

            Console.WriteLine($"GL_RENDERER : {SystemInfo.Renderer}");
            Console.WriteLine($"GL_VERSION  : {SystemInfo.GlVersion}");
            Console.WriteLine($"GLSL        : {SystemInfo.GlslVersion}");
            Console.WriteLine($"Audio       : {audio.Status}");

            var ctx = new UiContext
            {
                R = renderer,
                F = fonts,
                In = window.Input,
                Audio = audio,
                Theme = shell.Theme,
            };

            window.Resized += (w, h) =>
            {
                ctx.ScreenW = w;
                ctx.ScreenH = h;
                shell.Desktop.Relayout(w, h);
            };
            shell.Desktop.Relayout(window.Width, window.Height);

            // --open launches programs before the first frame is presented, which
            // is how the screenshot mode captures individual applications.
            bool pendingOpen = opts.Open.Count > 0;
            var pendingClicks = new List<(float x, float y, double at)>(opts.Clicks);
            var pendingKeys = new List<(int vk, char? ch, double at)>(opts.Keys);
            bool clickHeld = false;
            int heldKey = 0;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            double last = 0;
            int frame = 0;
            int fpsFrames = 0;
            double fpsTimer = 0, fps = 0;

            while (!window.ShouldClose)
            {
                window.PumpMessages();

                double now = clock.Elapsed.TotalSeconds;
                float dt = (float)Math.Min(now - last, 0.1);   // clamp after a stall
                last = now;

                fpsFrames++;
                fpsTimer += dt;
                if (fpsTimer >= 0.5) { fps = fpsFrames / fpsTimer; fpsFrames = 0; fpsTimer = 0; }

                // Scripted clicks: press on the scheduled frame, release next.
                if (clickHeld) { window.InjectRelease(); clickHeld = false; }
                if (heldKey != 0) { window.InjectKeyUp(heldKey); heldKey = 0; }
                for (int i = pendingKeys.Count - 1; i >= 0; i--)
                {
                    if (now < pendingKeys[i].at) continue;
                    window.InjectKey(pendingKeys[i].vk, pendingKeys[i].ch);
                    heldKey = pendingKeys[i].vk;
                    pendingKeys.RemoveAt(i);
                    break;
                }
                for (int i = pendingClicks.Count - 1; i >= 0; i--)
                {
                    if (now < pendingClicks[i].at) continue;
                    window.InjectClick(pendingClicks[i].x, pendingClicks[i].y);
                    pendingClicks.RemoveAt(i);
                    clickHeld = true;
                    break;
                }

                ctx.Time = now;
                ctx.Dt = dt;
                ctx.ScreenW = window.Width;
                ctx.ScreenH = window.Height;
                ctx.MouseHandled = false;
                ctx.KeyboardHandled = false;
                ctx.TooltipText = null;
                ctx.Cursor = CursorShape.Arrow;

                renderer.Begin(window.Width, window.Height);
                renderer.Clear(Color.Black);

                shell.Frame(ctx);

                // The update centre has staged a new build and the installer is
                // waiting for this process to end.
                if (shell.ExitRequested) break;

                if (pendingOpen)
                {
                    pendingOpen = false;
                    foreach (string app in opts.Open)
                        shell.LaunchByName(ctx, app);
                }

                if (opts.ShowStats)
                {
                    string stats = $"{fps:F0} FPS   {renderer.DrawCalls} draw   {renderer.QuadsThisFrame} quads";
                    float w = fonts.Small.Measure(stats);
                    renderer.FillRect(new Rect(window.Width - w - 14, 4, w + 10, fonts.Small.Height + 6),
                                      Color.Rgba(0x000000, 150));
                    fonts.Small.Draw(renderer, stats, window.Width - w - 9, 7, Color.Rgb(0x8CFF8C));
                }

                renderer.End();

                frame++;
                bool timeToShoot = opts.ScreenshotAt >= 0
                    ? now >= opts.ScreenshotAt
                    : frame >= opts.ScreenshotFrame;

                if (opts.ScreenshotPath != null && timeToShoot)
                {
                    Screenshot.Capture(window.Width, window.Height, opts.ScreenshotPath);
                    Console.WriteLine("wrote " + Path.GetFullPath(opts.ScreenshotPath));
                    window.RequestClose();
                }

                window.SwapBuffers();
                window.EndFrame();

                // The driver may ignore the swap interval, so cap the rate here
                // rather than spinning the GPU on a desktop that rarely changes.
                if (opts.FpsCap > 0)
                {
                    double target = 1.0 / opts.FpsCap;
                    double spent = clock.Elapsed.TotalSeconds - now;
                    int sleep = (int)((target - spent) * 1000);
                    if (sleep > 1) Thread.Sleep(sleep);
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    sealed class Options
    {
        public int Width = 1280, Height = 800;
        public bool Fullscreen;
        public bool ShowStats;
        public bool ShowHelp;
        public bool SkipBoot;
        public bool Muted;
        public Lang? Lang;
        public ThemeId? Theme;
        public string ScreenshotPath;
        public int ScreenshotFrame = 4;
        public double ScreenshotAt = -1;
        public int FpsCap;   // 0 = uncapped; --fps=N to limit
        public WallpaperId? Wallpaper;
        public readonly List<string> Open = new();
        public readonly List<(string path, string letter)> Mounts = new();
        public readonly List<(float x, float y, double at)> Clicks = new();
        public readonly List<(int vk, char? ch, double at)> Keys = new();
        public bool MountWritable;
        public string UpdateUrl;

        public const string HelpText = """
            Миминус ОС — a pseudo-operating system in raw OpenGL, GLSL and OpenAL.

              --size=WxH        window size (default 1280x800)
              --fullscreen, -f  borderless full screen
              --lang=ru|en      start in this language
              --theme=NAME      lunablue | lunaolive | lunasilver | seven | classic
              --skip-boot       jump straight to the desktop
              --open=A,B        launch these programs at start (implies --skip-boot)
              --wallpaper=NAME  yellow | wave | seven | dark | green | bliss |
                                azure | sunset | matrix | space | plaid | blueprint
              --mount=PATH      mount a real folder as a drive (repeatable);
                                use --mount=X:PATH to pick the drive letter
              --mount-writable  allow the OS to write back to mounted files
                                (off by default — mounts are read-only)
              --update-url=U    read the version manifest from U (a URL or a
                                file) instead of the project repository
              --mute            start with sound off
              --stats           show an FPS / draw-call overlay
              --screenshot=PATH render PATH as PNG and exit
              --frames=N        capture on frame N (default 4)
              --at=SECONDS      capture after this many seconds instead
              --click=X,Y[,T]   synthesise a click at X,Y after T seconds
                                (repeatable; for scripted screenshots)
              --key=NAME[,T]    synthesise a key press: back, enter, esc, del,
                                or a single character
              --fps=N           limit the frame rate (default: uncapped)
              --help, -h        this text
            """;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            foreach (string a in args)
            {
                if (a is "--help" or "-h") o.ShowHelp = true;
                else if (a is "--fullscreen" or "-f") o.Fullscreen = true;
                else if (a == "--stats") o.ShowStats = true;
                else if (a == "--skip-boot") o.SkipBoot = true;
                else if (a == "--mute") o.Muted = true;
                else if (a.StartsWith("--size="))
                {
                    string[] p = a[7..].Split('x');
                    if (p.Length == 2 && int.TryParse(p[0], out int w) && int.TryParse(p[1], out int h))
                    { o.Width = Math.Max(640, w); o.Height = Math.Max(480, h); }
                }
                else if (a.StartsWith("--lang="))
                    o.Lang = a[7..].StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Sys.Lang.En : Sys.Lang.Ru;
                else if (a.StartsWith("--theme="))
                    o.Theme = a[8..].ToLowerInvariant() switch
                    {
                        "lunaolive" or "olive" => ThemeId.LunaOlive,
                        "lunasilver" or "silver" => ThemeId.LunaSilver,
                        "seven" or "7" => ThemeId.Seven,
                        "classic" => ThemeId.Classic,
                        _ => ThemeId.LunaBlue,
                    };
                else if (a == "--mount-writable") o.MountWritable = true;
                else if (a.StartsWith("--update-url=")) o.UpdateUrl = a[13..];
                else if (a.StartsWith("--mount="))
                {
                    string spec = a[8..];
                    // "X:path" names a drive letter; a bare Windows path does not.
                    string letter = null;
                    if (spec.Length > 2 && spec[1] == ':' && spec[2] != '\\' && spec[2] != '/')
                    {
                        letter = spec[..2];
                        spec = spec[2..];
                    }
                    if (spec.Length > 0) o.Mounts.Add((spec, letter));
                }
                else if (a.StartsWith("--click="))
                {
                    string[] p2 = a[8..].Split(',');
                    if (p2.Length >= 2 &&
                        float.TryParse(p2[0], System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out float cx) &&
                        float.TryParse(p2[1], System.Globalization.NumberStyles.Any,
                                       System.Globalization.CultureInfo.InvariantCulture, out float cy))
                    {
                        double at = 1.0;
                        if (p2.Length >= 3)
                            double.TryParse(p2[2], System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture, out at);
                        o.Clicks.Add((cx, cy, at));
                    }
                }
                else if (a.StartsWith("--key="))
                {
                    string[] kp = a[6..].Split(',');
                    string name = kp[0].ToLowerInvariant();
                    double at = 1.0;
                    if (kp.Length >= 2)
                        double.TryParse(kp[1], System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.InvariantCulture, out at);
                    (int vk, char? ch) = name switch
                    {
                        "back" or "backspace" => (Platform.Keys.Back, (char?)null),
                        "enter" => (Platform.Keys.Enter, '\r'),
                        "esc" or "escape" => (Platform.Keys.Escape, (char?)null),
                        "del" or "delete" => (Platform.Keys.Delete, (char?)null),
                        "tab" => (Platform.Keys.Tab, (char?)null),
                        "f1" => (Platform.Keys.F1, (char?)null),
                        "f2" => (Platform.Keys.F2, (char?)null),
                        "f3" => (Platform.Keys.F3, (char?)null),
                        "f5" => (Platform.Keys.F5, (char?)null),
                        _ => (0, name.Length > 0 ? name[0] : (char?)null),
                    };
                    o.Keys.Add((vk, ch, at));
                }
                else if (a.StartsWith("--open="))
                    o.Open.AddRange(a[7..].Split(',', StringSplitOptions.RemoveEmptyEntries));
                else if (a.StartsWith("--wallpaper="))
                    o.Wallpaper = a[12..].ToLowerInvariant() switch
                    {
                        "wave" => WallpaperId.MiminusWave,
                        "seven" or "blue" => WallpaperId.Miminus7Blue,
                        "dark" => WallpaperId.Miminus7Dark,
                        "green" => WallpaperId.Miminus7Green,
                        "bliss" => WallpaperId.Bliss,
                        "azure" => WallpaperId.Azure,
                        "sunset" => WallpaperId.Sunset,
                        "matrix" => WallpaperId.Matrix,
                        "space" => WallpaperId.Space,
                        "plaid" => WallpaperId.Plaid,
                        "blueprint" => WallpaperId.Blueprint,
                        _ => WallpaperId.MiminusYellow,
                    };
                else if (a.StartsWith("--screenshot=")) o.ScreenshotPath = a[13..];
                else if (a.StartsWith("--frames=") && int.TryParse(a[9..], out int n)) o.ScreenshotFrame = n;
                else if (a.StartsWith("--at=") && double.TryParse(a[5..],
                         System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out double at)) o.ScreenshotAt = at;
                else if (a.StartsWith("--fps=") && int.TryParse(a[6..], out int fc)) o.FpsCap = Math.Max(0, fc);
            }
            return o;
        }
    }
}
