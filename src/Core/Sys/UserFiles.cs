using System.Text;

namespace Miminus.Sys;

/// <summary>Keeps what the user made across restarts.
///
/// The filesystem is built from code every time the machine starts: the same
/// folders, the same «читать.txt», the same Курсач.xls, in the same places. That
/// is the point of it — this is a machine from 2010 that boots into the same
/// desktop every time. But a folder somebody created themselves, or a document
/// they typed and saved, is not part of that picture, and losing it on the way
/// out is not a joke, it is a bug.
///
/// So the tree itself is not saved. What is saved is what the user did to it: a
/// journal of the nodes they created, the ones they deleted, and the ones they
/// renamed. On the next start the tree is seeded as usual and the journal is
/// replayed over the top, and the desktop comes back the way they left it.
///
/// Nothing under a mounted host folder is ever journalled — those are real files
/// on a real disk, and they persist by being real.</summary>
public static class UserFiles
{
    const string FileName = "files.txt";

    static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, FileName);

    /// <summary>What the file said when it was last written, so a frame that
    /// changed nothing costs one string comparison.</summary>
    static string _saved;
    static double _lastWrite = -10;
    static bool _loading;

    // ---- writing -----------------------------------------------------------

    /// <summary>Called once a frame; writes only when something has actually
    /// changed, and never more than once a second.</summary>
    public static void Poll(VirtualFS fs, double time)
    {
        if (_loading || fs == null) return;

        string now = Journal(fs);
        if (now == _saved) return;
        if (time - _lastWrite < 1) return;

        _saved = now;
        _lastWrite = time;
        Write(now);
    }

    /// <summary>Writes immediately, whatever the timer says — used on the way
    /// out, so the last folder created before a shutdown is not lost.</summary>
    public static void Flush(VirtualFS fs)
    {
        if (fs == null) return;

        string now = Journal(fs);
        if (now == _saved && System.IO.File.Exists(Path)) return;

        _saved = now;
        Write(now);
    }

    /// <summary>The journal, as text. Three kinds of line, in the order they
    /// have to be replayed: what was renamed, what was removed, and what was
    /// made — the last of those sorted so a folder always arrives before
    /// anything that lives in it.</summary>
    static string Journal(VirtualFS fs)
    {
        var sb = new StringBuilder();
        sb.Append("# МИМИНУС ОС — что сделал пользователь. Можно править вручную.\n");
        sb.Append("#\n");
        sb.Append("# ~ старый путь | новое имя      переименовано\n");
        sb.Append("# - путь                         удалено\n");
        sb.Append("# + путь | вид | значок | чем открыть | текст\n\n");

        foreach (var (from, to) in fs.RenamedNodes)
            sb.Append("~ ").Append(Escape(from)).Append(" | ").Append(Escape(to)).Append('\n');

        foreach (string path in fs.DeletedNodes)
            sb.Append("- ").Append(Escape(path)).Append('\n');

        var made = new List<VNode>();
        Collect(fs.Root, made);
        foreach (var node in made.OrderBy(n => n.StablePath.Count(ch => ch == '\\'))
                                 .ThenBy(n => n.StablePath))
        {
            sb.Append("+ ").Append(Escape(node.StablePath))
              .Append(" | ").Append(node.Kind)
              .Append(" | ").Append(node.Icon)
              .Append(" | ").Append(Escape(node.Launch ?? ""))
              .Append(" | ").Append(Escape(node.Kind == NodeKind.TextFile ? node.Text ?? "" : ""))
              .Append('\n');
        }

        return sb.ToString();
    }

    static void Collect(VNode folder, List<VNode> into)
    {
        foreach (var child in folder.Children)
        {
            // A mounted subtree is real files on a real disk: it keeps itself.
            if (child.Mount != null) continue;

            if (child.UserCreated) into.Add(child);
            if (child.IsContainer) Collect(child, into);
        }
    }

    // ---- reading -----------------------------------------------------------

    /// <summary>Replays the journal over a freshly seeded tree. Anything that
    /// cannot be applied — a path whose folder is no longer there, a line from
    /// another build — is skipped rather than fatal: a damaged journal costs
    /// one file, not the machine.</summary>
    public static void Load(VirtualFS fs)
    {
        if (!System.IO.File.Exists(Path)) { _saved = Journal(fs); return; }

        _loading = true;
        try
        {
            foreach (string raw in System.IO.File.ReadAllLines(Path))
            {
                string line = raw.Trim();
                if (line.Length < 2 || line[0] == '#') continue;

                char op = line[0];
                string rest = line[1..].Trim();

                try
                {
                    switch (op)
                    {
                        case '~': Replay(fs, rest, rename: true); break;
                        case '-': Remove(fs, Unescape(rest)); break;
                        case '+': Replay(fs, rest, rename: false); break;
                    }
                }
                catch
                {
                    // One bad line, one lost file.
                }
            }
        }
        catch
        {
            // No journal is the same as an empty one.
        }
        finally
        {
            _loading = false;
            _saved = Journal(fs);
        }
    }

    static void Remove(VirtualFS fs, string path)
    {
        var node = fs.FindByPath(path);
        if (node == null || node.Mount != null) return;

        node.Parent?.Children.Remove(node);
        fs.NoteDeleted(path);
    }

    static void Replay(VirtualFS fs, string rest, bool rename)
    {
        string[] parts = rest.Split('|');
        if (parts.Length < 2) return;

        string path = Unescape(parts[0].Trim());

        if (rename)
        {
            var target = fs.FindByPath(path);
            string to = Unescape(parts[1].Trim());
            if (target != null && target.Mount == null && to.Length > 0)
            {
                target.NameKey = null;
                target.Name = to;
                fs.NoteRenamed(path, to);
            }
            return;
        }

        if (parts.Length < 5) return;

        int slash = path.LastIndexOf('\\');
        if (slash <= 0) return;

        var folder = fs.FindByPath(path[..slash]);
        if (folder is not { IsContainer: true }) return;

        string name = path[(slash + 1)..];
        if (folder.Find(name) != null) return;      // already there

        if (!Enum.TryParse(parts[1].Trim(), out NodeKind kind)) return;
        Enum.TryParse(parts[2].Trim(), out Graphics.IconId icon);

        string launch = Unescape(parts[3].Trim());
        string text = Unescape(string.Join("|", parts[4..]).Trim());

        var node = new VNode
        {
            Name = name,
            Kind = kind,
            Icon = icon,
            Launch = launch.Length > 0 ? launch : null,
            Modified = DateTime.Now,
            UserCreated = true,
            Text = kind == NodeKind.TextFile ? text : null,
        };
        folder.Add(node);
    }

    // ---- one line, one node ------------------------------------------------

    /// <summary>Newlines and the separator have to survive a line-based file,
    /// so they travel escaped.</summary>
    static string Escape(string s) => s == null ? ""
        : s.Replace("\\n", "\\\\n").Replace("\n", "\\n").Replace("\r", "").Replace("|", "\\p");

    static string Unescape(string s) => s == null ? ""
        : s.Replace("\\p", "|").Replace("\\n", "\n").Replace("\\\\n", "\\n");

    static void Write(string text)
    {
        try { System.IO.File.WriteAllText(Path, text); }
        catch
        {
            // Read-only install: the system runs, it just forgets.
        }
    }
}
