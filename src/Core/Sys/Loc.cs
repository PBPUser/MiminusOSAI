using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Miminus.Sys;

public enum Lang { Ru, En }

/// <summary>Runtime language switch backed by JSON catalogues.
///
/// Every user-visible string lives in <c>lang/ru.json</c> and <c>lang/en.json</c>
/// as <c>"key": "text"</c>. The files are embedded in the assembly so the program
/// runs standalone, and are also copied next to the executable so they can be
/// edited or a new language dropped in without rebuilding — a file on disk always
/// wins over the embedded copy.
///
/// Call sites use <see cref="T"/> for plain strings and <see cref="F"/> for ones
/// with <c>{0}</c> placeholders.</summary>
public static class L
{
    static Lang _current = Lang.Ru;

    static readonly Dictionary<string, string> Ru = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> En = new(StringComparer.Ordinal);

    /// <summary>Keys asked for but not present in any catalogue. Surfaced by
    /// <see cref="MissingKeys"/> so gaps are findable rather than silent.</summary>
    static readonly HashSet<string> Missing = new(StringComparer.Ordinal);

    static L() => Load();

    public static Lang Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when the language flips, so open windows can re-lay-out.</summary>
    public static event Action Changed;

    public static void Toggle() => Current = _current == Lang.Ru ? Lang.En : Lang.Ru;

    public static bool IsRu => _current == Lang.Ru;

    /// <summary>Short language tag shown in the taskbar tray, as XP did.</summary>
    public static string TrayTag => _current == Lang.Ru ? "RU" : "EN";

    public static IReadOnlyCollection<string> MissingKeys => Missing;
    public static int StringCount => Ru.Count;

    // ---- catalogue -------------------------------------------------------

    static void Load()
    {
        LoadInto("ru.json", Ru);
        LoadInto("en.json", En);
    }

    static void LoadInto(string fileName, Dictionary<string, string> table)
    {
        table.Clear();

        // Embedded copy first, so a partial file on disk only overrides what it defines.
        var asm = Assembly.GetExecutingAssembly();
        string resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resource != null)
        {
            using var stream = asm.GetManifestResourceStream(resource);
            if (stream != null) Merge(table, stream);
        }

        string path = Path.Combine(AppContext.BaseDirectory, "lang", fileName);
        if (File.Exists(path))
        {
            try
            {
                using var fs = File.OpenRead(path);
                Merge(table, fs);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"lang: could not read {path}: {ex.Message}");
            }
        }
    }

    static void Merge(Dictionary<string, string> table, Stream stream)
    {
        try
        {
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.String)
                    table[prop.Name] = prop.Value.GetString() ?? "";
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine("lang: malformed catalogue — " + ex.Message);
        }
    }

    /// <summary>Re-reads the catalogues from disk. Handy while translating.</summary>
    public static void Reload()
    {
        Load();
        Changed?.Invoke();
    }

    // ---- lookup ----------------------------------------------------------

    /// <summary>Translated string for <paramref name="key"/>.
    ///
    /// Falls back to the other language, then to the key itself, so a missing
    /// entry degrades to something legible instead of an empty label.</summary>
    public static string T(string key)
    {
        var primary = _current == Lang.Ru ? Ru : En;
        if (primary.TryGetValue(key, out string s)) return s;

        var secondary = _current == Lang.Ru ? En : Ru;
        if (secondary.TryGetValue(key, out s)) return s;

        Missing.Add(key);
        return key;
    }

    /// <summary>Translated format string with arguments substituted into its
    /// <c>{0}</c>, <c>{1}</c> … placeholders.</summary>
    public static string F(string key, params object[] args)
    {
        string fmt = T(key);
        try { return string.Format(CultureInfo.InvariantCulture, fmt, args); }
        catch (FormatException) { return fmt; }
    }

    // ---- date and number formatting --------------------------------------

    static string[] Months => _current == Lang.Ru
        ? new[] { "января", "февраля", "марта", "апреля", "мая", "июня",
                  "июля", "августа", "сентября", "октября", "ноября", "декабря" }
        : new[] { "January", "February", "March", "April", "May", "June",
                  "July", "August", "September", "October", "November", "December" };

    static string[] Days => _current == Lang.Ru
        ? new[] { "воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота" }
        : new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    public static string LongDate(DateTime d) => _current == Lang.Ru
        ? $"{Days[(int)d.DayOfWeek]}, {d.Day} {Months[d.Month - 1]} {d.Year} г."
        : $"{Days[(int)d.DayOfWeek]}, {Months[d.Month - 1]} {d.Day}, {d.Year}";

    public static string ShortDate(DateTime d) => _current == Lang.Ru
        ? d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
        : d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    public static string Time(DateTime d) => d.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>File sizes the way Explorer showed them: whole kilobytes, rounded up.</summary>
    public static string FileSize(long bytes)
    {
        if (bytes < 1024) return F("unit.bytes", bytes);
        long kb = (bytes + 1023) / 1024;
        if (kb < 1024) return F("unit.kilobytes", kb);
        return F("unit.megabytes", (kb / 1024.0).ToString("0.0", CultureInfo.InvariantCulture));
    }
}
