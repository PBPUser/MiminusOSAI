using System.Reflection;
using System.Runtime.Loader;
using Miminus.Graphics;
using Miminus.Sys;

namespace Miminus.Shell;

/// <summary>One program the OS can run.
///
/// Each program lives in its own assembly under <c>apps/</c> and is found by
/// reflection at startup, so adding a program means dropping in a DLL rather
/// than editing the shell. The shell never names a window class.</summary>
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

/// <summary>Everything discovered under <c>apps/</c>.
///
/// Assemblies are loaded into the default context so that the single copy of
/// Miminus.Core next to the executable satisfies them all; a program DLL that
/// carried its own copy would produce two incompatible sets of types.</summary>
public sealed class ProgramRegistry
{
    readonly Dictionary<string, IProgram> _byId = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _failures = new();

    public IReadOnlyCollection<IProgram> All => _byId.Values;
    public IReadOnlyList<string> Failures => _failures;
    public int Count => _byId.Count;

    public void Register(IProgram program)
    {
        if (program == null || string.IsNullOrWhiteSpace(program.Id)) return;
        _byId[program.Id] = program;
    }

    public IProgram Find(string id)
        => id != null && _byId.TryGetValue(id, out var p) ? p : null;

    /// <summary>Loads every Miminus.App.*.dll in a directory and registers the
    /// programs it declares. Returns how many programs were added.</summary>
    public int LoadFrom(string directory)
    {
        if (!Directory.Exists(directory)) return 0;
        int added = 0;

        foreach (string path in Directory.EnumerateFiles(directory, "Miminus.App.*.dll").OrderBy(p => p))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(path));
                added += RegisterFrom(asm);
            }
            catch (Exception ex)
            {
                _failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        return added;
    }

    /// <summary>Registers every concrete IProgram in an assembly.</summary>
    public int RegisterFrom(Assembly asm)
    {
        int added = 0;
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface) continue;
            if (!typeof(IProgram).IsAssignableFrom(type)) continue;
            if (type.GetConstructor(Type.EmptyTypes) == null) continue;

            try
            {
                Register((IProgram)Activator.CreateInstance(type));
                added++;
            }
            catch (Exception ex)
            {
                _failures.Add($"{type.Name}: {ex.Message}");
            }
        }
        return added;
    }
}
