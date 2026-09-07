using System.Net.Http;

namespace Miminus.Sys;

/// <summary>Where a check has got to.</summary>
public enum UpdateState
{
    Idle, Checking, UpToDate, Available,
    Downloading, Verifying, Extracting, ReadyToRestart,
    Failed,
}

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

    /// <summary>Archive the update centre can actually install, as opposed to
    /// <see cref="Download"/>, which is the page a person would open.</summary>
    public string Package = "";

    /// <summary>Hex SHA-256 of the package, checked before anything is
    /// extracted. Without it the package is refused.</summary>
    public string Sha256 = "";

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
    public const string InstalledVersion = "7.1";

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

    /// <summary>Which step the background task has reached, so the window can
    /// name what is happening without the worker touching UI state.</summary>
    volatile UpdateState _stage;
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

    public bool Busy => State is UpdateState.Checking or UpdateState.Downloading
        or UpdateState.Verifying or UpdateState.Extracting;

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

    // ---- installing -------------------------------------------------------

    /// <summary>Where the package is downloaded and unpacked. Beside the
    /// executable rather than in TEMP, so the swap that follows is a move
    /// within one volume and a failed run leaves evidence behind.</summary>
    public static string StagingRoot => Path.Combine(AppContext.BaseDirectory, "update");

    /// <summary>Folder holding the unpacked build, once one is ready.</summary>
    public string Staging { get; private set; }

    /// <summary>Bytes fetched so far, and the total when the server declares
    /// one, for the progress readout.</summary>
    public long Fetched { get; private set; }
    public long Total { get; private set; }

    /// <summary>Downloads the package named by the manifest, checks it against
    /// the published hash, and unpacks it ready to be installed. Does nothing
    /// unless an update is actually on offer.</summary>
    public void BeginDownload()
    {
        if (State != UpdateState.Available || Latest == null) return;
        if (string.IsNullOrEmpty(Latest.Package))
        {
            State = UpdateState.Failed;
            Error = L.T("update.no_package");
            return;
        }

        var info = Latest;
        State = _stage = UpdateState.Downloading;
        Progress = 0;
        Staging = null;
        Fetched = Total = 0;
        Error = null;
        _incoming = null;
        _running = Task.Run(() => Fetch(info));
    }

    async Task Fetch(UpdateInfo info)
    {
        string archive = null;
        try
        {
            Directory.CreateDirectory(StagingRoot);
            archive = Path.Combine(StagingRoot, "package.zip");

            string hash = await DownloadAndHash(info.Package, archive).ConfigureAwait(false);

            // A package with no published hash, or the wrong one, is not
            // unpacked: this is code that is about to replace the running
            // program.
            _stage = UpdateState.Verifying;
            if (string.IsNullOrWhiteSpace(info.Sha256))
                throw new InvalidOperationException(L.T("update.no_checksum"));

            if (!hash.Equals(info.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(L.F("update.checksum_mismatch", hash));

            _stage = UpdateState.Extracting;
            string unpacked = Path.Combine(StagingRoot, "staging");
            if (Directory.Exists(unpacked)) Directory.Delete(unpacked, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(archive, unpacked);
            File.Delete(archive);

            string root = FindBuild(unpacked);
            if (root == null) throw new InvalidOperationException(L.T("update.package_has_no_build"));

            Staging = root;
            _incoming = new Result(UpdateState.ReadyToRestart, info, null);
        }
        catch (Exception ex)
        {
            try { if (archive != null && File.Exists(archive)) File.Delete(archive); } catch { }
            _incoming = new Result(UpdateState.Failed, info, ex.Message);
        }
    }

    /// <summary>Streams the package to disk, hashing as it goes so the file is
    /// read once rather than twice.</summary>
    async Task<string> DownloadAndHash(string url, string destination)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
                                       .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Total = response.Content.Headers.ContentLength ?? 0;

        using var sha = System.Security.Cryptography.SHA256.Create();
        using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var file = File.Create(destination);

        var buffer = new byte[64 * 1024];
        long fetched = 0;
        int read;
        while ((read = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            await file.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
            fetched += read;
            Fetched = fetched;
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash);
    }

    /// <summary>Finds the folder inside the unpacked package that holds the
    /// executable — archives usually wrap everything in one directory.</summary>
    static string FindBuild(string root)
    {
        if (File.Exists(Path.Combine(root, "MiminusOS.exe"))) return root;

        foreach (string dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            if (File.Exists(Path.Combine(dir, "MiminusOS.exe")))
                return dir;

        return null;
    }

    /// <summary>Puts the unpacked build in place and starts it.
    ///
    /// The running program cannot overwrite its own files, so a one-shot script
    /// does it: wait for this process to end, copy the staged build over the
    /// installation, start it again, and delete itself. The caller is expected
    /// to shut the OS down immediately afterwards.</summary>
    public bool Install(out string error)
    {
        error = null;
        if (State != UpdateState.ReadyToRestart || Staging == null || !Directory.Exists(Staging))
        {
            error = L.T("update.nothing_staged");
            return false;
        }

        try
        {
            string install = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            string exe = Path.Combine(install, "MiminusOS.exe");
            string script = Path.Combine(StagingRoot, "install.cmd");
            int pid = Environment.ProcessId;

            File.WriteAllText(script, $"""
                @echo off
                rem Written by the МИМИНУС update centre. Safe to delete.
                :wait
                tasklist /FI "PID eq {pid}" | find "{pid}" >nul
                if not errorlevel 1 (
                    ping -n 2 127.0.0.1 >nul
                    goto wait
                )
                xcopy /E /I /Y "{Staging}" "{install}" >nul
                rd /s /q "{Path.Combine(StagingRoot, "staging")}" 2>nul
                start "" "{exe}"
                del "%~f0"
                """, System.Text.Encoding.Default);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"" + script + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = install,
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
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
        else if (State is UpdateState.Downloading or UpdateState.Verifying or UpdateState.Extracting)
        {
            State = _stage;
            // Real progress while bytes are arriving and the server declared a
            // length; the unpacking that follows has none to report.
            Progress = State == UpdateState.Downloading && Total > 0
                ? Math.Clamp(Fetched / (float)Total, 0, 1)
                : MathF.Min(0.97f, Progress + dt * 0.4f);
        }

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
            Package = Get("package"),
            Sha256 = Get("sha256"),
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
