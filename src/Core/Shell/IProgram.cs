using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Miminus.Graphics;
using Miminus.Sys;

namespace Miminus.Shell;

/// <summary>One program the OS can run.
///
/// Each program lives in its own assembly under <c>apps/</c> and is found by
/// reflection, so adding a program means dropping in a DLL rather than editing
/// the shell. The shell never names a window class.</summary>
public interface IProgram
{
    /// <summary>Stable identifier used by shortcuts, the Start menu and --open.</summary>
    string Id { get; }

    /// <summary>Catalogue key for the display name.</summary>
    string NameKey { get; }

    IconId Icon { get; }

    /// <summary>When true, launching again focuses the running instance.</summary>
    bool Singleton => false;

    /// <summary>Creates the window. <paramref name="document"/> is the file the
    /// program was asked to open, or null.</summary>
    OsWindow Create(ShellHost shell, VNode document);
}

/// <summary>What the registry knows about a program before its DLL is read:
/// enough to put it in a menu, and enough to load it when it is asked for.</summary>
public sealed class ProgramEntry
{
    public string Id;
    public string NameKey;
    public IconId Icon;
    public bool Singleton;

    /// <summary>Assembly file this program lives in, and the type inside it.</summary>
    public string AssemblyPath;
    public string TypeName;

    public string AssemblyName => Path.GetFileNameWithoutExtension(AssemblyPath);
}

/// <summary>One program assembly and the context it is loaded into.
///
/// Each assembly gets its own collectible context so it can be dropped again
/// once nothing is using it. <c>Miminus.Core</c> is deliberately not resolved
/// here: it falls through to the default context, so the shell and the program
/// share one set of types rather than two incompatible ones.</summary>
sealed class AppAssembly
{
    sealed class Context : AssemblyLoadContext
    {
        readonly string _directory;

        public Context(string name, string directory) : base(name, isCollectible: true)
            => _directory = directory;

        protected override Assembly Load(AssemblyName name)
        {
            // Anything the host already has — Core included — comes from the
            // default context. Only a program's own private dependencies, if it
            // ever grows one, are loaded here.
            string candidate = System.IO.Path.Combine(_directory, name.Name + ".dll");
            return File.Exists(candidate) && name.Name.StartsWith("Miminus.App.", StringComparison.Ordinal)
                ? LoadFromAssemblyPath(candidate)
                : null;
        }
    }

    public readonly string Path;
    public int Windows;              // how many open windows came from it

    Context _context;
    WeakReference _unloading;

    /// <summary>Frame time the last window closed, so an assembly is not
    /// dropped the instant a program is closed and reopened.</summary>
    public double IdleSince = -1;

    public bool Loaded => _context != null;

    public AppAssembly(string path) => Path = path;

    public Assembly Load()
    {
        if (_context == null)
        {
            _context = new Context(System.IO.Path.GetFileNameWithoutExtension(Path),
                                   System.IO.Path.GetDirectoryName(Path));
            _unloading = null;
        }
        return _context.LoadFromAssemblyPath(System.IO.Path.GetFullPath(Path));
    }

    /// <summary>Drops the assembly. The context stops being reachable here, but
    /// .NET only finishes the unload once nothing holds a type from it, which is
    /// why <see cref="Collected"/> is a question rather than a statement.</summary>
    public void Unload()
    {
        if (_context == null) return;
        _unloading = new WeakReference(_context);
        _context.Unload();
        _context = null;
    }

    /// <summary>True once the runtime has actually finished the unload.</summary>
    public bool Collected => _unloading != null && !_unloading.IsAlive;
}

/// <summary>Everything under <c>apps/</c>, loaded only when it is used.
///
/// Reading twelve assemblies at startup to ask each one its name is work the
/// OS does not need to do, so the answers are cached in <c>apps/programs.index</c>
/// and the DLL itself is opened the first time one of its programs is actually
/// launched. When the last window from an assembly closes, the assembly is
/// unloaded again — which is only possible because each one lives in its own
/// collectible load context.</summary>
public sealed class ProgramRegistry
{
    /// <summary>How long an assembly with no windows is kept before it is
    /// dropped. Closing a program and opening it again is common enough that
    /// unloading immediately would only churn.</summary>
    const double IdleGrace = 20;

    const string IndexFile = "programs.index";
    const int IndexVersion = 1;

    readonly Dictionary<string, ProgramEntry> _byId = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, AppAssembly> _assemblies = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _failures = new();

    public IReadOnlyCollection<ProgramEntry> All => _byId.Values;
    public IReadOnlyList<string> Failures => _failures;
    public int Count => _byId.Count;

    /// <summary>How many program assemblies are in memory right now — what the
    /// terminal's <c>apps</c> command reports.</summary>
    public int LoadedAssemblies => _assemblies.Values.Count(a => a.Loaded);

    public ProgramEntry Find(string id)
        => id != null && _byId.TryGetValue(id, out var e) ? e : null;

    /// <summary>Assemblies and their state, for diagnostics.</summary>
    public IEnumerable<(string name, bool loaded, int windows)> Assemblies()
        => _assemblies.Values.Select(a =>
            (Path.GetFileNameWithoutExtension(a.Path), a.Loaded, a.Windows));

    // ---- discovery --------------------------------------------------------

    /// <summary>Catalogues every Miminus.App.*.dll in a directory without
    /// loading any of them, when the cached index is still good for it.</summary>
    public int LoadFrom(string directory)
    {
        if (!Directory.Exists(directory)) return 0;

        var files = Directory.EnumerateFiles(directory, "Miminus.App.*.dll").OrderBy(p => p).ToList();
        var cached = ReadIndex(Path.Combine(directory, IndexFile));
        bool indexStale = false;

        foreach (string path in files)
        {
            var assembly = new AppAssembly(path);
            _assemblies[path] = assembly;

            string stamp = Stamp(path);
            if (cached.TryGetValue(stamp, out var entries))
            {
                foreach (var entry in entries) _byId[entry.Id] = entry;
                continue;
            }

            // Nothing cached for this build of the DLL: read it once to find
            // out what it offers, then let it go again.
            indexStale = true;
            foreach (var entry in Harvest(assembly)) _byId[entry.Id] = entry;
            assembly.Unload();
        }

        if (indexStale || cached.Count != files.Count)
            WriteIndex(Path.Combine(directory, IndexFile));

        return _byId.Count;
    }

    /// <summary>Opens an assembly to read the programs it declares.</summary>
    List<ProgramEntry> Harvest(AppAssembly assembly)
    {
        var found = new List<ProgramEntry>();
        try
        {
            var asm = assembly.Load();
            foreach (var type in Types(asm))
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IProgram).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;

                var program = (IProgram)Activator.CreateInstance(type);
                found.Add(new ProgramEntry
                {
                    Id = program.Id,
                    NameKey = program.NameKey,
                    Icon = program.Icon,
                    Singleton = program.Singleton,
                    AssemblyPath = assembly.Path,
                    TypeName = type.FullName,
                });
            }
        }
        catch (Exception ex)
        {
            _failures.Add($"{Path.GetFileName(assembly.Path)}: {ex.Message}");
        }
        return found;
    }

    static IEnumerable<Type> Types(Assembly asm)
    {
        try { return asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
    }

    // ---- launching --------------------------------------------------------

    /// <summary>Loads the program's assembly if it is not already in memory and
    /// creates its window. The window keeps the assembly alive; nothing else
    /// does, which is what makes unloading possible later.</summary>
    public OsWindow Create(ProgramEntry entry, ShellHost shell, VNode document)
    {
        if (entry == null) return null;
        if (!_assemblies.TryGetValue(entry.AssemblyPath, out var assembly)) return null;

        try
        {
            var asm = assembly.Load();
            var type = asm.GetType(entry.TypeName);
            if (type == null) throw new TypeLoadException(entry.TypeName);

            var program = (IProgram)Activator.CreateInstance(type);
            var window = program.Create(shell, document);
            if (window != null)
            {
                assembly.Windows++;
                assembly.IdleSince = -1;
            }
            return window;
        }
        catch (Exception ex)
        {
            _failures.Add($"{entry.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Called when a window closes, so its assembly can be counted
    /// down and eventually dropped.</summary>
    public void WindowClosed(string programId, double time)
    {
        var entry = Find(programId);
        if (entry == null) return;
        if (!_assemblies.TryGetValue(entry.AssemblyPath, out var assembly)) return;

        assembly.Windows = Math.Max(0, assembly.Windows - 1);
        if (assembly.Windows == 0) assembly.IdleSince = time;
    }

    /// <summary>Unloads assemblies whose last window closed a while ago. Runs
    /// once a frame; the work is a handful of integer comparisons unless
    /// something is actually due to go.</summary>
    public void CollectUnused(double time)
    {
        foreach (var assembly in _assemblies.Values)
        {
            if (!assembly.Loaded || assembly.Windows > 0) continue;
            if (assembly.IdleSince < 0 || time - assembly.IdleSince < IdleGrace) continue;

            assembly.Unload();
            assembly.IdleSince = -1;
        }
    }

    /// <summary>Forces every idle assembly out now, and reports how many went.
    /// Used by the terminal, and by the tests that prove unloading works.</summary>
    public int UnloadIdle()
    {
        int dropped = 0;
        foreach (var assembly in _assemblies.Values)
        {
            if (!assembly.Loaded || assembly.Windows > 0) continue;
            assembly.Unload();
            assembly.IdleSince = -1;
            dropped++;
        }

        if (dropped > 0) FinishUnloading();
        return dropped;
    }

    /// <summary>Gives the runtime the two collections an unload needs to
    /// complete. Without this the assembly is unreachable but still resident.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void FinishUnloading()
    {
        for (int i = 0; i < 2; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>True once every dropped assembly has actually gone away.</summary>
    public bool AllDroppedCollected => _assemblies.Values.All(a => a.Loaded || a.Collected || a.IdleSince < 0 && !a.Loaded);

    // ---- the index --------------------------------------------------------

    /// <summary>Identifies a particular build of a DLL, so the cache is dropped
    /// when the file changes.</summary>
    static string Stamp(string path)
    {
        var info = new FileInfo(path);
        return $"{Path.GetFileName(path)}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    Dictionary<string, List<ProgramEntry>> ReadIndex(string path)
    {
        var result = new Dictionary<string, List<ProgramEntry>>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(path)) return result;

            string directory = Path.GetDirectoryName(path);
            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0 || lines[0] != "miminus-programs " + IndexVersion) return result;

            string stamp = null;
            foreach (string line in lines.Skip(1))
            {
                if (line.Length == 0) continue;

                if (line[0] != '\t')
                {
                    stamp = line;
                    // A DLL named in the index that is no longer on disk simply
                    // contributes nothing.
                    if (!File.Exists(Path.Combine(directory, stamp.Split(':')[0]))) stamp = null;
                    else result[stamp] = new List<ProgramEntry>();
                    continue;
                }

                if (stamp == null) continue;
                string[] f = line[1..].Split('|');
                if (f.Length != 5) continue;

                result[stamp].Add(new ProgramEntry
                {
                    Id = f[0],
                    NameKey = f[1],
                    Icon = Enum.TryParse<IconId>(f[2], out var icon) ? icon : IconId.Program,
                    Singleton = f[3] == "1",
                    TypeName = f[4],
                    AssemblyPath = Path.Combine(directory, stamp.Split(':')[0]),
                });
            }
        }
        catch
        {
            // A damaged index is not worth reporting: it is rebuilt from the
            // assemblies themselves.
            result.Clear();
        }
        return result;
    }

    void WriteIndex(string path)
    {
        try
        {
            var lines = new List<string> { "miminus-programs " + IndexVersion };

            foreach (var group in _byId.Values.GroupBy(e => e.AssemblyPath).OrderBy(g => g.Key))
            {
                lines.Add(Stamp(group.Key));
                foreach (var e in group.OrderBy(e => e.Id))
                    lines.Add($"\t{e.Id}|{e.NameKey}|{e.Icon}|{(e.Singleton ? 1 : 0)}|{e.TypeName}");
            }

            File.WriteAllLines(path, lines);
        }
        catch
        {
            // Read-only install: the index is an optimisation, not a
            // requirement, so carry on without it.
        }
    }
}
