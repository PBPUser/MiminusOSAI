using System.Net.Http;

namespace Miminus.Sys;

/// <summary>Where a check has got to.</summary>
public enum UpdateState { Idle, Checking, UpToDate, Available, Failed }

/// <summary>What the published manifest says about the newest build.</summary>
public sealed class UpdateInfo
{
    public string Version = "";
    public string Name = "";
    public string Codename = "";
    public string Released = "";
    public string Size = "";
    public string Url = "";
    public string Download = "";
    public string Notes = "";

    /// <summary>True when this came from the copy shipped beside the executable
    /// rather than from the repository.</summary>
    public bool Local;
}

/// <summary>«Центр обновления МИМИНУС» — the update service.
///
/// The newest build is announced by a small text file in the project's GitHub
/// repository, so a new version can be published by editing one file rather
/// than by shipping a new binary. The file is plain <c>key = value</c> lines:
/// a manifest format anyone can read and edit, in keeping with a system whose
/// author insists everything is written from scratch.
///
/// The network call runs on a background task; the UI thread only ever reads
/// <see cref="State"/> and <see cref="Latest"/>, which are swapped in whole by
/// <see cref="Poll"/> so a half-filled result is never drawn.</summary>
public sealed class UpdateService
{
    public const string Repository = "PBPUser/MiminusOSAI";
    public const string Branch = "main";
    public const string ManifestFile = "latest.txt";

    /// <summary>The version this build reports as installed.</summary>
    public const string InstalledVersion = "7.0";

    public static string RepositoryUrl => "https://github.com/" + Repository;

    public static string ManifestUrl =>
        $"https://raw.githubusercontent.com/{Repository}/{Branch}/{ManifestFile}";

    /// <summary>Where the manifest is read from. Defaults to the repository, but
    /// a fork — or a test — can point it at another address or at a file on
    /// disk via <c>--update-url=</c>.</summary>
    public string Source = ManifestUrl;

    /// <summary>Long enough that a dead network does not stall the shell, short
    /// enough that the progress bar does not outlive the user's patience.</summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    sealed record Result(UpdateState State, UpdateInfo Info, string Error);

    static readonly HttpClient Http = CreateClient();

    volatile Result _incoming;
    Task _running;

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public UpdateInfo Latest { get; private set; }
    public string Error { get; private set; }
    public DateTime? LastChecked { get; private set; }

    /// <summary>0..1 while a check is running, for the progress bar.</summary>
    public float Progress { get; private set; }

    /// <summary>Set once the tray has announced an available update, so the
    /// balloon appears a single time per finding.</summary>
    public bool Announced;

    public bool Busy => State == UpdateState.Checking;

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = Timeout };
        // GitHub serves raw files to anything, but a real agent string keeps
        // the request out of the "unidentified client" bucket.
        http.DefaultRequestHeaders.Add("User-Agent", "MiminusOS/" + InstalledVersion);
        return http;
    }

    /// <summary>Starts a check unless one is already running.</summary>
    public void BeginCheck()
    {
        if (Busy) return;

        State = UpdateState.Checking;
        Progress = 0;
        Error = null;
        _incoming = null;
        _running = Task.Run(Check);
    }

    async Task Check()
    {
        try
        {
            string body = File.Exists(Source)
                ? await File.ReadAllTextAsync(Source).ConfigureAwait(false)
                : await Http.GetStringAsync(Source).ConfigureAwait(false);
            var info = Parse(body);
            _incoming = info == null || info.Version.Length == 0
                ? new Result(UpdateState.Failed, LocalManifest(), "update.manifest_unreadable")
                : new Result(IsNewer(info.Version, InstalledVersion)
                                 ? UpdateState.Available : UpdateState.UpToDate,
                             info, null);
        }
        catch (Exception ex)
        {
            // Offline is the expected case for a system that boasts its network
            // "does not require a connection", so fall back to the copy shipped
            // beside the executable and say plainly where the figures came from.
            _incoming = new Result(UpdateState.Failed, LocalManifest(), ex.Message);
        }
    }

    /// <summary>Called once per frame: advances the progress bar and publishes a
    /// finished result to the UI thread.</summary>
    public void Poll(float dt)
    {
        if (State == UpdateState.Checking)
            // Creeps towards, but never reaches, full: the last step belongs to
            // the answer actually arriving.
            Progress = MathF.Min(0.92f, Progress + dt * 0.55f);

        var result = _incoming;
        if (result == null) return;

        _incoming = null;
        _running = null;
        State = result.State;
        Latest = result.Info;
        Error = result.Error;
        LastChecked = DateTime.Now;
        Progress = 1;
        Announced = false;
    }

    /// <summary>Reads the manifest that ships beside the executable, used when
    /// the repository cannot be reached.</summary>
    public static UpdateInfo LocalManifest()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, ManifestFile);
            if (!File.Exists(path)) return null;
            var info = Parse(File.ReadAllText(path));
            if (info != null) info.Local = true;
            return info;
        }
        catch { return null; }
    }

    /// <summary>Parses the <c>key = value</c> manifest. Keys may carry a
    /// language suffix (<c>name.en</c>), which wins when that language is
    /// selected.</summary>
    public static UpdateInfo Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            int eq = line.IndexOf('=');
            if (eq <= 0) continue;

            string key = line[..eq].Trim();
            // The manifest is a single line per field, so an escaped newline is
            // the only way to write a multi-line release note.
            string value = line[(eq + 1)..].Trim().Replace("\\n", "\n");
            if (key.Length != 0) fields[key] = value;
        }

        if (fields.Count == 0) return null;

        string suffix = L.IsRu ? null : ".en";
        string Get(string key)
            => suffix != null && fields.TryGetValue(key + suffix, out string localised) ? localised
             : fields.TryGetValue(key, out string plain) ? plain
             : "";

        return new UpdateInfo
        {
            Version = Get("version"),
            Name = Get("name"),
            Codename = Get("codename"),
            Released = Get("released"),
            Size = Get("size"),
            Url = Get("url").Length != 0 ? Get("url") : RepositoryUrl,
            Download = Get("download"),
            Notes = Get("notes"),
        };
    }

    /// <summary>Compares dotted version numbers, treating a missing component as
    /// zero so "7.1" beats "7" and "7.0.1" beats "7.0".</summary>
    public static bool IsNewer(string candidate, string installed)
    {
        var a = Components(candidate);
        var b = Components(installed);
        for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            int x = i < a.Length ? a[i] : 0;
            int y = i < b.Length ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    static int[] Components(string version)
        => (version ?? "").Split('.')
           .Select(p => int.TryParse(new string(p.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0)
           .ToArray();
}
