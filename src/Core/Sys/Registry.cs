using System.Globalization;

namespace Miminus.Sys;

/// <summary>Реестр — the system registry.
///
/// A path-addressed store of typed values, kept in <c>registry.txt</c> beside
/// the executable in the same readable <c>key = value</c> shape as everything
/// else this system writes, one line per value with its full path in front of
/// it. Nothing here is a database: it is a dictionary that knows how to spell
/// itself, which is all the real one is underneath as well.
///
/// It holds what a registry ought to hold — what the machine is called, who it
/// is registered to, which build it is, and the colour the interface is painted
/// in. The accent in particular is read out of here rather than kept in a field
/// on the theme, so changing it in Regedit repaints the system exactly as
/// choosing it in Personalisation does: there is one copy of that value and it
/// lives here.
///
/// <c>settings.txt</c> is still where the switches live. The division is the
/// one the original drew and never explained: the registry is what the system
/// is, and the settings file is how it has been left.</summary>
public static class Registry
{
    const string FileName = "registry.txt";

    /// <summary>The two hives. A path is a hive and then key names separated by
    /// backslashes, which is what makes the paths in the file readable.</summary>
    public const string LocalMachine = @"HKEY_LOCAL_MACHINE";
    public const string CurrentUser = @"HKEY_CURRENT_USER";

    /// <summary>Where the shell's own values live.</summary>
    public const string Appearance = @"HKEY_CURRENT_USER\Software\Miminus\Appearance";
    public const string Machine = @"HKEY_LOCAL_MACHINE\Software\Miminus\Setup";
    public const string Owner = @"HKEY_CURRENT_USER\Software\Miminus\Owner";

    /// <summary>What a value is. The real thing has a dozen types and uses
    /// three; this has the three.</summary>
    public enum Kind { String, Dword, Colour }

    public readonly record struct Value(Kind Kind, string Text)
    {
        public int AsDword => int.TryParse(Text, NumberStyles.Integer,
                                           CultureInfo.InvariantCulture, out int v) ? v : 0;

        public string TypeName => Kind switch
        {
            Kind.Dword => "REG_DWORD",
            Kind.Colour => "REG_COLOUR",
            _ => "REG_SZ",
        };

        public override string ToString() => Kind == Kind.Dword
            ? "0x" + AsDword.ToString("X8", CultureInfo.InvariantCulture) + " (" + AsDword + ")"
            : Text;
    }

    /// <summary>path → (name → value). Ordinal comparison on both, so the file
    /// round-trips exactly as it was written.</summary>
    static readonly SortedDictionary<string, SortedDictionary<string, Value>> Keys =
        new(StringComparer.OrdinalIgnoreCase);

    static bool _dirty;
    static double _lastWrite = -10;

    /// <summary>Raised when anything changes, so the shell can rebuild whatever
    /// was made out of a value — the theme, when the accent moves.</summary>
    public static event Action Changed;

    static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    // ---- the values the system reads ----------------------------------------

    /// <summary>The accent colour, cached so the hundreds of reads a frame cost
    /// a field access rather than two dictionary lookups.</summary>
    static Graphics.Color _accent = Graphics.Color.Rgb(0x2D89EF);

    public static Graphics.Color Accent
    {
        get => _accent;
        set
        {
            if (_accent.Packed == value.Packed) return;
            _accent = value;
            SetColour(Appearance, "Accent", value);
        }
    }

    public static string ComputerName
    {
        get => GetString(Machine, "ComputerName", "МИМИНУС-ПК");
        set => SetString(Machine, "ComputerName", value);
    }

    public static string RegisteredOwner
    {
        get => GetString(Owner, "RegisteredOwner", "Admin");
        set => SetString(Owner, "RegisteredOwner", value);
    }

    // ---- reading and writing --------------------------------------------------

    public static IEnumerable<string> Paths => Keys.Keys;

    /// <summary>The immediate children of a path — the names of the keys one
    /// level below it, which is what a tree needs to draw a branch.</summary>
    public static IEnumerable<string> ChildKeys(string path)
    {
        string prefix = path.Length == 0 ? "" : path + @"\";
        var seen = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string full in Keys.Keys)
        {
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string rest = full[prefix.Length..];
            if (rest.Length == 0) continue;

            int slash = rest.IndexOf('\\');
            seen.Add(slash < 0 ? rest : rest[..slash]);
        }
        return seen;
    }

    /// <summary>The values in one key, in the order the file keeps them.</summary>
    public static IReadOnlyDictionary<string, Value> Values(string path)
        => Keys.TryGetValue(path, out var values)
            ? values
            : new SortedDictionary<string, Value>(StringComparer.OrdinalIgnoreCase);

    public static string GetString(string path, string name, string fallback = "")
        => Keys.TryGetValue(path, out var values) && values.TryGetValue(name, out var v)
            ? v.Text : fallback;

    public static int GetDword(string path, string name, int fallback = 0)
        => Keys.TryGetValue(path, out var values) && values.TryGetValue(name, out var v)
            ? v.AsDword : fallback;

    public static void SetString(string path, string name, string text)
        => Set(path, name, new Value(Kind.String, text ?? ""));

    public static void SetDword(string path, string name, int value)
        => Set(path, name, new Value(Kind.Dword, value.ToString(CultureInfo.InvariantCulture)));

    public static void SetColour(string path, string name, Graphics.Color colour)
        => Set(path, name, new Value(Kind.Colour,
            colour.R.ToString("X2") + colour.G.ToString("X2") + colour.B.ToString("X2")));

    /// <summary>Writes one value, making the key if it is not there. Setting a
    /// value to what it already is changes nothing and raises nothing.</summary>
    public static void Set(string path, string name, Value value)
    {
        if (!Keys.TryGetValue(path, out var values))
            Keys[path] = values = new SortedDictionary<string, Value>(StringComparer.OrdinalIgnoreCase);

        if (values.TryGetValue(name, out var existing) &&
            existing.Kind == value.Kind && existing.Text == value.Text) return;

        values[name] = value;
        _dirty = true;

        // The one value the rest of the system is built out of.
        if (path == Appearance && name == "Accent") _accent = ParseColour(value.Text, _accent);

        Changed?.Invoke();
    }

    public static bool Delete(string path, string name)
    {
        if (!Keys.TryGetValue(path, out var values) || !values.Remove(name)) return false;
        _dirty = true;
        Changed?.Invoke();
        return true;
    }

    static Graphics.Color ParseColour(string hex, Graphics.Color fallback)
        => hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber,
                                           CultureInfo.InvariantCulture, out int rgb)
            ? Graphics.Color.Rgb(rgb) : fallback;

    // ---- the file ---------------------------------------------------------------

    /// <summary>Reads the file, then fills in whatever it did not contain. A
    /// machine with no registry gets the one it should have had, which is what
    /// the installer would have written.</summary>
    public static void Load()
    {
        Keys.Clear();

        try
        {
            if (File.Exists(Path))
                foreach (string raw in File.ReadAllLines(Path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string left = line[..eq].Trim();
                    string text = line[(eq + 1)..].Trim();

                    // «path\name : TYPE» on the left of the equals.
                    var kind = Kind.String;
                    int colon = left.LastIndexOf(':');
                    if (colon > 0)
                    {
                        string type = left[(colon + 1)..].Trim();
                        kind = type switch
                        {
                            "REG_DWORD" => Kind.Dword,
                            "REG_COLOUR" => Kind.Colour,
                            _ => Kind.String,
                        };
                        left = left[..colon].Trim();
                    }

                    int slash = left.LastIndexOf('\\');
                    if (slash <= 0) continue;

                    string path = left[..slash];
                    string name = left[(slash + 1)..];

                    if (!Keys.TryGetValue(path, out var values))
                        Keys[path] = values = new SortedDictionary<string, Value>(
                            StringComparer.OrdinalIgnoreCase);

                    values[name] = new Value(kind, text);
                }
        }
        catch
        {
            // An unreadable registry is the same as none: the defaults below
            // rebuild what the system needs, and the file is written again.
            Keys.Clear();
        }

        Seed();
        _accent = ParseColour(GetString(Appearance, "Accent", "2D89EF"), _accent);
    }

    /// <summary>The keys the system expects to find. Only what is missing is
    /// written, so nothing a person edited is overwritten on the way in.</summary>
    static void Seed()
    {
        Default(Machine, "ProductName", new Value(Kind.String, "МИМИНУС ОС"));
        Default(Machine, "CurrentVersion", new Value(Kind.String, UpdateService.InstalledVersion));
        Default(Machine, "BuildLab", new Value(Kind.String, "8.0.2010.miminus"));
        Default(Machine, "ComputerName", new Value(Kind.String, "МИМИНУС-ПК"));
        Default(Machine, "InstallDate", new Value(Kind.String, "2010-06-06"));
        Default(Machine, "WrittenFromScratch", new Value(Kind.Dword, "1"));
        Default(Machine, "BolgenosDetected", new Value(Kind.Dword, "0"));

        Default(Owner, "RegisteredOwner", new Value(Kind.String, "Admin"));
        Default(Owner, "RegisteredOrganization", new Value(Kind.String, "Гревцов и Попов"));

        Default(Appearance, "Accent", new Value(Kind.Colour, "2D89EF"));
        Default(Appearance, "PopovsFound", new Value(Kind.Dword, "0"));
    }

    static void Default(string path, string name, Value value)
    {
        if (Keys.TryGetValue(path, out var values) && values.ContainsKey(name)) return;

        if (!Keys.TryGetValue(path, out values))
            Keys[path] = values = new SortedDictionary<string, Value>(StringComparer.OrdinalIgnoreCase);

        values[name] = value;
        _dirty = true;
    }

    /// <summary>Called once a frame. Writes only when something changed, and
    /// never more than once a second — the same discipline the settings file
    /// keeps.</summary>
    public static void Poll(double time)
    {
        if (!_dirty || time - _lastWrite < 1) return;
        _lastWrite = time;
        Flush();
    }

    public static void Flush()
    {
        if (!_dirty && File.Exists(Path)) return;
        _dirty = false;

        try
        {
            var lines = new List<string>
            {
                "# МИМИНУС ОС — реестр. Можно править вручную: строка на значение,",
                "# путь и имя слева, тип после двоеточия, содержимое справа.",
                "",
            };

            foreach (var (path, values) in Keys)
            {
                foreach (var (name, value) in values)
                    lines.Add($"{path}\\{name} : {value.TypeName} = {value.Text}");
                lines.Add("");
            }

            File.WriteAllLines(Path, lines);
        }
        catch
        {
            // Read-only install: the system runs, it just forgets — which is
            // what it did before it had a registry at all.
        }
    }
}
