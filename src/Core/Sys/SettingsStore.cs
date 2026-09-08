using Miminus.Graphics;
using Miminus.Shell;
using Miminus.UI;

namespace Miminus.Sys;

/// <summary>Keeps what the user chose across restarts.
///
/// Everything adjustable in the system ends up in one <c>settings.txt</c> beside
/// the executable, in the same <c>key = value</c> shape as the update manifest,
/// so it can be read and edited by hand. It is written whenever something
/// actually changes rather than on a save button, because nothing in this system
/// has a save button: the sheets apply as they are ticked.
///
/// The file is not part of an update package, so settings survive an update the
/// same way the version record does.</summary>
public static class SettingsStore
{
    const string FileName = "settings.txt";

    static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>What the settings looked like when they were last written, so a
    /// frame that changed nothing costs one string comparison.</summary>
    static string _saved;
    static double _lastWrite = -10;

    /// <summary>Set while loading, so applying a stored value does not count as
    /// a change worth writing straight back.</summary>
    static bool _loading;

    /// <summary>Reads the file and applies it. Called once, before the first
    /// frame, and quietly does nothing when there is no file yet.</summary>
    public static void Load(ShellHost shell, Fonts fonts)
    {
        var values = Read();
        if (values.Count == 0) { _saved = Signature(shell); return; }

        _loading = true;
        try
        {
            var s = shell.Settings;

            if (Enum.TryParse(Get(values, "theme"), out ThemeId theme)) shell.SetTheme(theme, fonts);
            if (Enum.TryParse(Get(values, "wallpaper"), out WallpaperId paper)) shell.SetWallpaper(paper);
            if (Enum.TryParse(Get(values, "language"), out Lang lang)) L.Current = lang;
            if (Enum.TryParse(Get(values, "smoothing"), out FontSmoothing smoothing)) s.Smoothing = smoothing;

            shell.Audio.MasterVolume = Number(values, "volume", shell.Audio.MasterVolume);
            shell.Audio.Muted = Flag(values, "muted", shell.Audio.Muted);
            shell.Audio.MasterVolume = shell.Audio.MasterVolume;   // re-apply the listener gain

            s.Dpi = (int)Number(values, "dpi", s.Dpi);
            s.RefreshHz = (int)Number(values, "refresh", s.RefreshHz);
            s.ColorDepth = (int)Number(values, "depth", s.ColorDepth);

            s.MenuTransition = Flag(values, "menu_transition", s.MenuTransition);
            s.MenuFade = Flag(values, "menu_fade", s.MenuFade);
            s.MenuShadows = Flag(values, "menu_shadows", s.MenuShadows);
            s.LargeIcons = Flag(values, "large_icons", s.LargeIcons);
            s.ShowWindowContentsWhileDragging =
                Flag(values, "drag_contents", s.ShowWindowContentsWhileDragging);
            s.HideAccessKeys = Flag(values, "hide_access_keys", s.HideAccessKeys);

            s.LockTaskbar = Flag(values, "taskbar_locked", s.LockTaskbar);
            s.AutoHideTaskbar = Flag(values, "taskbar_autohide", s.AutoHideTaskbar);
            s.TaskbarOnTop = Flag(values, "taskbar_on_top", s.TaskbarOnTop);
            s.GroupSimilar = Flag(values, "taskbar_group", s.GroupSimilar);
            s.ShowQuickLaunch = Flag(values, "quick_launch", s.ShowQuickLaunch);
            s.ShowClock = Flag(values, "show_clock", s.ShowClock);
            s.HideInactiveIcons = Flag(values, "hide_tray_icons", s.HideInactiveIcons);
        }
        catch
        {
            // A settings file from another build, or a damaged one: whatever
            // was applied stands, and the rest keeps its default.
        }
        finally
        {
            _loading = false;
            _saved = Signature(shell);
        }
    }

    /// <summary>Called once a frame. Writes only when something has actually
    /// changed, and never more than once a second.</summary>
    public static void Poll(ShellHost shell, double time)
    {
        if (_loading) return;

        string now = Signature(shell);
        if (now == _saved) return;
        if (time - _lastWrite < 1) return;

        _saved = now;
        _lastWrite = time;
        Write(now);
    }

    /// <summary>Writes immediately, whatever the timer says. Used on the way
    /// out, so the last change before a shutdown is not lost.</summary>
    public static void Flush(ShellHost shell)
    {
        string now = Signature(shell);
        if (now == _saved && System.IO.File.Exists(Path)) return;

        _saved = now;
        Write(now);
    }

    /// <summary>The file's whole contents, which doubles as the change check:
    /// if the text is the same, nothing needs writing.</summary>
    static string Signature(ShellHost shell)
    {
        var s = shell.Settings;
        var lines = new List<string>
        {
            "# МИМИНУС ОС — сохранённые настройки. Можно править вручную.",
            "",
            "theme      = " + shell.ThemeId,
            "wallpaper  = " + shell.Desktop.Current,
            "language   = " + L.Current,
            "smoothing  = " + s.Smoothing,
            "",
            "volume     = " + shell.Audio.MasterVolume.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture),
            "muted      = " + Yes(shell.Audio.Muted),
            "",
            "dpi        = " + s.Dpi,
            "refresh    = " + s.RefreshHz,
            "depth      = " + s.ColorDepth,
            "",
            "menu_transition  = " + Yes(s.MenuTransition),
            "menu_fade        = " + Yes(s.MenuFade),
            "menu_shadows     = " + Yes(s.MenuShadows),
            "large_icons      = " + Yes(s.LargeIcons),
            "drag_contents    = " + Yes(s.ShowWindowContentsWhileDragging),
            "hide_access_keys = " + Yes(s.HideAccessKeys),
            "",
            "taskbar_locked   = " + Yes(s.LockTaskbar),
            "taskbar_autohide = " + Yes(s.AutoHideTaskbar),
            "taskbar_on_top   = " + Yes(s.TaskbarOnTop),
            "taskbar_group    = " + Yes(s.GroupSimilar),
            "quick_launch     = " + Yes(s.ShowQuickLaunch),
            "show_clock       = " + Yes(s.ShowClock),
            "hide_tray_icons  = " + Yes(s.HideInactiveIcons),
        };
        return string.Join("\n", lines) + "\n";
    }

    static string Yes(bool value) => value ? "yes" : "no";

    static void Write(string text)
    {
        try { System.IO.File.WriteAllText(Path, text); }
        catch
        {
            // Read-only install: the system runs, it just forgets.
        }
    }

    static Dictionary<string, string> Read()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!System.IO.File.Exists(Path)) return values;

            foreach (string raw in System.IO.File.ReadAllLines(Path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                values[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
        catch { values.Clear(); }
        return values;
    }

    static string Get(Dictionary<string, string> values, string key)
        => values.TryGetValue(key, out string v) ? v : null;

    static bool Flag(Dictionary<string, string> values, string key, bool fallback)
        => Get(values, key) is { } v
            ? v.Equals("yes", StringComparison.OrdinalIgnoreCase) || v == "1"
            : fallback;

    static float Number(Dictionary<string, string> values, string key, float fallback)
        => float.TryParse(Get(values, key), System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out float v)
            ? v : fallback;
}
