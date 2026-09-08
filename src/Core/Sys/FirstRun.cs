namespace Miminus.Sys;

/// <summary>Remembers which build was last seen, so the OS can tell a fresh
/// install from one that has just been updated.
///
/// The file sits beside the executable and is not part of the package, so the
/// installer copying a new build over the old one leaves it behind — which is
/// exactly what makes the version comparison work: the build says 7.2 while the
/// state still says 7.1.</summary>
public static class FirstRun
{
    const string StateFile = "state.txt";

    /// <summary>Version recorded the last time the OS started, or null when it
    /// has never run here.</summary>
    public static string PreviousVersion { get; private set; }

    /// <summary>True when this build is newer than the one last seen: the OS
    /// has been updated since it last ran, and has something to show for it.</summary>
    public static bool JustUpdated { get; private set; }

    /// <summary>True when the system has never run here at all, which is when
    /// setup asks its questions.</summary>
    public static bool NeverRun { get; private set; }

    /// <summary>Set once the presentation has been shown, so it appears a
    /// single time even if the state file could not be written.</summary>
    public static bool Announced;

    /// <summary>Reads the state and records the current build. Called once at
    /// startup, before the first frame.</summary>
    public static void Check()
    {
        string path = Path.Combine(AppContext.BaseDirectory, StateFile);

        try
        {
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && line[..eq].Trim().Equals("version", StringComparison.OrdinalIgnoreCase))
                        PreviousVersion = line[(eq + 1)..].Trim();
                }
            }
        }
        catch
        {
            // Unreadable state is the same as no state: nothing is announced.
        }

        NeverRun = PreviousVersion == null;
        JustUpdated = PreviousVersion != null &&
                      UpdateService.IsNewer(UpdateService.InstalledVersion, PreviousVersion);

        // A first run records the version silently. There is nothing new to
        // show someone who has never seen the old version.
        try
        {
            File.WriteAllText(path,
                "# МИМИНУС ОС — что система запомнила о себе.\n" +
                "version = " + UpdateService.InstalledVersion + "\n");
        }
        catch
        {
            // Read-only install: the presentation will simply not be repeated
            // within this session, and may appear again on the next start.
        }
    }
}
