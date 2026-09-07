using Miminus.Graphics;

namespace Miminus.Sys;

/// <summary>Mounts a real directory from the host machine as a drive inside
/// МИМИНУС ОС.
///
/// Nodes under a mount are filled in lazily the first time a folder is opened,
/// so mounting a large tree costs nothing until it is browsed. Mounts are
/// **read-only by default**: Explorer will not delete host files and Notepad
/// will not write them back unless the mount was created writable, because a
/// parody desktop has no business quietly modifying somebody's disk.</summary>
public sealed class HostMount
{
    public readonly string HostRoot;
    public readonly bool Writable;
    public readonly VNode Node;

    /// <summary>Entries above this are not listed, to keep a stray mount of a
    /// huge directory from stalling the shell.</summary>
    public const int MaxEntriesPerFolder = 2000;

    /// <summary>Text files larger than this open as a notice rather than content.</summary>
    public const long MaxTextBytes = 1 << 20;

    public HostMount(VNode node, string hostRoot, bool writable)
    {
        Node = node;
        HostRoot = hostRoot;
        Writable = writable;
    }

    /// <summary>Fills a mounted folder's children from disk, once.</summary>
    public static void Populate(VNode folder)
    {
        if (folder?.HostPath == null || folder.HostLoaded) return;
        folder.HostLoaded = true;
        folder.Children.Clear();

        var mount = folder.Mount;
        try
        {
            var dir = new DirectoryInfo(folder.HostPath);
            if (!dir.Exists) return;

            int count = 0;

            foreach (var sub in dir.EnumerateDirectories().OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (count++ >= MaxEntriesPerFolder) break;
                if ((sub.Attributes & FileAttributes.Hidden) != 0) continue;

                folder.Add(new VNode
                {
                    Name = sub.Name,
                    Kind = NodeKind.Folder,
                    Icon = IconId.Folder,
                    Modified = SafeTime(() => sub.LastWriteTime),
                    HostPath = sub.FullName,
                    Mount = mount,
                });
            }

            foreach (var file in dir.EnumerateFiles().OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (count++ >= MaxEntriesPerFolder) break;
                if ((file.Attributes & FileAttributes.Hidden) != 0) continue;

                var (kind, icon, launch) = Classify(file.Name);
                folder.Add(new VNode
                {
                    Name = file.Name,
                    Kind = kind,
                    Icon = icon,
                    Launch = launch,
                    ExplicitSize = SafeSize(file),
                    Modified = SafeTime(() => file.LastWriteTime),
                    HostPath = file.FullName,
                    Mount = mount,
                });
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable directory simply shows up empty.
        }
    }

    static DateTime SafeTime(Func<DateTime> get)
    {
        try { return get(); } catch { return DateTime.Now; }
    }

    static long SafeSize(FileInfo f)
    {
        try { return f.Length; } catch { return 0; }
    }

    /// <summary>Maps a file extension onto the shell's node kind, icon and
    /// default program.</summary>
    public static (NodeKind kind, IconId icon, string launch) Classify(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".txt" or ".log" or ".ini" or ".cfg" or ".md" or ".json" or ".xml" or ".csv"
                or ".cs" or ".c" or ".h" or ".cpp" or ".py" or ".js" or ".html" or ".css"
                => (NodeKind.TextFile, IconId.TextFile, "notepad"),

            ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".tif" or ".tiff"
                => (NodeKind.ImageFile, IconId.ImageFile, "paint"),

            ".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a"
                => (NodeKind.Audio, IconId.AudioFile, "player"),

            ".avi" or ".mp4" or ".mkv" or ".mov" or ".wmv"
                => (NodeKind.Video, IconId.VideoFile, "player"),

            ".zip" or ".rar" or ".7z" or ".tar" or ".gz"
                => (NodeKind.Archive, IconId.Archive, null),

            ".xls" or ".xlsx" => (NodeKind.Spreadsheet, IconId.Spreadsheet, "spreadsheet"),
            ".doc" or ".docx" or ".rtf" or ".pdf" => (NodeKind.Document, IconId.WordDoc, null),
            ".exe" or ".com" or ".bat" or ".cmd" => (NodeKind.Program, IconId.Program, null),
            ".lnk" => (NodeKind.Shortcut, IconId.Program, null),

            _ => (NodeKind.Unknown, IconId.UnknownFile, null),
        };
    }

    /// <summary>Reads a mounted text file, with the size guard applied.</summary>
    public static string ReadText(VNode node)
    {
        try
        {
            var info = new FileInfo(node.HostPath);
            if (!info.Exists) return L.T("mount.file_no_longer_exists");
            if (info.Length > MaxTextBytes) return L.F("mount.file_too_large", L.FileSize(info.Length));
            return File.ReadAllText(node.HostPath);
        }
        catch (Exception ex)
        {
            return L.F("mount.could_not_read", ex.Message);
        }
    }

    /// <summary>Writes a mounted text file back to disk. Refuses on a read-only
    /// mount and reports why.</summary>
    public static bool WriteText(VNode node, string text, out string error)
    {
        error = null;
        if (node.Mount is not { Writable: true })
        {
            error = L.T("mount.read_only");
            return false;
        }
        try
        {
            File.WriteAllText(node.HostPath, text);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Renames a mounted file or folder on disk and repoints the node
    /// and, for a folder, everything already loaded beneath it. Refuses on a
    /// read-only mount and reports why.</summary>
    public static bool Rename(VNode node, string newName, out string error)
    {
        error = null;
        if (node.Mount is not { Writable: true })
        {
            error = L.T("mount.read_only");
            return false;
        }

        string directory = Path.GetDirectoryName(node.HostPath);
        if (directory == null) { error = L.T("mount.read_only"); return false; }

        string target = Path.Combine(directory, newName);
        try
        {
            if (node.IsContainer) Directory.Move(node.HostPath, target);
            else File.Move(node.HostPath, target);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        Repoint(node, target);
        return true;
    }

    /// <summary>Rewrites the host paths of a moved node and its loaded children,
    /// which would otherwise still point at the old name.</summary>
    static void Repoint(VNode node, string hostPath)
    {
        node.HostPath = hostPath;
        foreach (var child in node.Children)
            if (child.HostPath != null)
                Repoint(child, Path.Combine(hostPath, Path.GetFileName(child.HostPath)));
    }

    /// <summary>Free and total bytes for the volume backing this mount.</summary>
    public (long free, long total) Space()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(HostRoot) ?? HostRoot);
            return (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch
        {
            return (0, 0);
        }
    }
}
