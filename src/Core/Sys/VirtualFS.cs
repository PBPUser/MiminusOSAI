using Miminus.Graphics;

namespace Miminus.Sys;

public enum NodeKind
{
    Folder,
    TextFile,
    ImageFile,
    Shortcut,
    Program,
    Drive,
    DvdDrive,
    Removable,
    Device,
    Archive,
    Audio,
    Video,
    Spreadsheet,
    Document,
    Unknown,
}

/// <summary>One entry in the fake filesystem. Files are tiny — text nodes hold
/// their whole body as a string, image nodes name a procedurally drawn picture —
/// so the whole tree lives happily in memory.</summary>
public sealed class VNode
{
    string _name = "";
    string _text;

    /// <summary>Catalogue key for the display name. When set, the name follows
    /// the current language instead of being frozen at construction.</summary>
    public string NameKey;

    /// <summary>Catalogue key for the body of a text file. Editing the file sets
    /// <see cref="Text"/> directly and pins it, so user edits are never
    /// overwritten by a language change.</summary>
    public string TextKey;

    public string Name
    {
        get => NameKey != null ? L.T(NameKey) : _name;
        set => _name = value;
    }

    public NodeKind Kind;
    public IconId Icon;
    public VNode Parent;
    public readonly List<VNode> Children = new();

    /// <summary>Body of a text file. Notepad edits this in place.</summary>
    public string Text
    {
        get
        {
            if (_text != null) return _text;
            if (HostPath != null && Kind == NodeKind.TextFile) return HostMount.ReadText(this);
            return TextKey != null ? L.T(TextKey) : null;
        }
        set => _text = value;
    }

    /// <summary>Which procedural picture an image node shows.</summary>
    public PictureId Picture;

    /// <summary>For shortcuts and program nodes: the app id to launch.</summary>
    public string Launch;

    /// <summary>Explicit size for nodes whose bytes we do not actually store.</summary>
    public long ExplicitSize = -1;

    public DateTime Modified = new(2010, 6, 6, 13, 31, 0);

    /// <summary>Catalogue key for the hover text, e.g. the
    /// «Сыграйте в игру "Сапер"!» tip from part 2.</summary>
    public string TooltipKey;

    /// <summary>Free space fraction, drives only, for the Explorer details pane.</summary>
    public long Capacity, Free;

    /// <summary>Real path on the host machine when this node lives under a mount.</summary>
    public string HostPath;

    /// <summary>The mount this node belongs to, or null for the built-in tree.</summary>
    public HostMount Mount;

    /// <summary>Set once a mounted folder's children have been read from disk.</summary>
    public bool HostLoaded;

    /// <summary>Refuses deletion. Part 3 selects the МИ folder, presses Ctrl and
    /// notes that "the most important folder in my system" will not go.</summary>
    public bool Protected;

    public bool IsHosted => HostPath != null;

    public bool IsContainer => Kind is NodeKind.Folder or NodeKind.Drive or NodeKind.DvdDrive
        or NodeKind.Removable or NodeKind.Device;

    public long Size
    {
        get
        {
            if (ExplicitSize >= 0) return ExplicitSize;
            if (Kind == NodeKind.TextFile) return Text == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(Text);
            return 0;
        }
    }

    public string Path
    {
        get
        {
            if (Parent == null) return Name;
            string p = Parent.Path;
            return p.EndsWith("\\") ? p + Name : p + "\\" + Name;
        }
    }

    public VNode Add(VNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return child;
    }

    public VNode Find(string name)
    {
        HostMount.Populate(this);
        return Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Children, with a mounted folder read from disk on first use.</summary>
    public IReadOnlyList<VNode> Entries
    {
        get
        {
            HostMount.Populate(this);
            return Children;
        }
    }

    /// <summary>Human-readable type column, matching XP's wording.</summary>
    public string TypeName => Kind switch
    {
        NodeKind.Folder => L.T("fs.file_folder"),
        NodeKind.TextFile => L.T("fs.text_document"),
        NodeKind.ImageFile => L.T("fs.jpeg_image"),
        NodeKind.Shortcut => L.T("fs.shortcut"),
        NodeKind.Program => L.T("fs.application"),
        NodeKind.Drive => L.T("fs.local_disk"),
        NodeKind.DvdDrive => L.T("fs.dvd_drive"),
        NodeKind.Removable => L.T("fs.removable_disk"),
        NodeKind.Device => L.T("fs.device"),
        NodeKind.Archive => L.T("fs.winrar_archive"),
        NodeKind.Audio => L.T("fs.mp3_audio_file"),
        NodeKind.Video => L.T("fs.video_file"),
        NodeKind.Spreadsheet => L.T("fs.microsoft_excel_worksheet"),
        NodeKind.Document => L.T("fs.microsoft_word_document"),
        _ => L.T("fs.file"),
    };

    public string Tooltip => TooltipKey == null ? null : L.T(TooltipKey);
}

/// <summary>The whole pretend disk. Laid out to match what the reference videos
/// show on screen, right down to the folder names and the joke files.</summary>
public sealed class VirtualFS
{
    public readonly VNode MyComputer;
    public readonly VNode DriveC;

    /// <summary>C:\WINDOWS, which is also shown on the desktop so it can be
    /// found without going looking for it.</summary>
    public VNode WindowsFolder;
    public readonly VNode Desktop;
    public readonly VNode MyDocuments;
    public readonly VNode MyPictures;
    public readonly VNode MyMusic;
    public readonly VNode Revolutionary;    // «Революционные дистрибутивы»
    public readonly VNode UsefulTricks;     // «ПОЛЕЗНЫЕ ФИШКИ МИМИНУСА»
    public readonly VNode AntivirusFile;
    public readonly VNode RecycleBin;

    /// <summary>«100 БЭКАПОВ» — the folder of copies from part 3.</summary>
    public readonly VNode Backups;


    public VirtualFS()
    {
        MyComputer = new VNode { NameKey = "fs.my_computer", Kind = NodeKind.Device, Icon = IconId.MyComputer };

        DriveC = MyComputer.Add(new VNode
        {
            Name = "C:", Kind = NodeKind.Drive, Icon = IconId.DriveHdd,
            Capacity = 80L * 1024 * 1024 * 1024, Free = 12L * 1024 * 1024 * 1024
        });
        var driveD = MyComputer.Add(new VNode
        {
            Name = "D:", Kind = NodeKind.Drive, Icon = IconId.DriveHdd,
            Capacity = 160L * 1024 * 1024 * 1024, Free = 43L * 1024 * 1024 * 1024
        });
        var driveE = MyComputer.Add(new VNode
        {
            Name = "E:", Kind = NodeKind.Drive, Icon = IconId.DriveHdd,
            Capacity = 250L * 1024 * 1024 * 1024, Free = 200L * 1024 * 1024 * 1024
        });
        MyComputer.Add(new VNode { Name = "F:", Kind = NodeKind.DvdDrive, Icon = IconId.DriveDvd });
        MyComputer.Add(new VNode { Name = "G:", Kind = NodeKind.DvdDrive, Icon = IconId.DriveDvd });
        MyComputer.Add(new VNode { Name = "H:", Kind = NodeKind.DvdDrive, Icon = IconId.DriveDvd });
        MyComputer.Add(new VNode { Name = "Mobile Device", Kind = NodeKind.Device, Icon = IconId.Phone });
        MyComputer.Add(new VNode { Name = "Nokia Phone Browser", Kind = NodeKind.Device, Icon = IconId.Phone });
        MyComputer.Add(new VNode
        {
            NameKey = "fs.usb_video_device",
            Kind = NodeKind.Device, Icon = IconId.Camera
        });

        // C:\Documents and Settings\Admin\Рабочий стол — the path shown in the videos.
        var docsAndSettings = DriveC.Add(Folder("Documents and Settings"));
        var admin = docsAndSettings.Add(Folder("Admin"));
        Desktop = admin.Add(FolderKey("fs.desktop"));
        MyDocuments = admin.Add(new VNode
        {
            NameKey = "fs.my_documents", Kind = NodeKind.Folder, Icon = IconId.MyDocuments
        });
        MyPictures = MyDocuments.Add(new VNode
        {
            NameKey = "fs.my_pictures", Kind = NodeKind.Folder, Icon = IconId.MyPictures
        });
        MyMusic = MyDocuments.Add(new VNode
        {
            NameKey = "fs.my_music", Kind = NodeKind.Folder, Icon = IconId.MyMusic
        });

        DriveC.Add(Folder("Program Files"));

        // The folder part 3 deletes to prove the system is not Windows
        // underneath. It refuses an ordinary delete and goes with Ctrl held.
        WindowsFolder = DriveC.Add(Folder("WINDOWS"));

        RecycleBin = new VNode
        {
            NameKey = "fs.recycle_bin", Kind = NodeKind.Folder, Icon = IconId.RecycleBin
        };

        // ---- «Революционные дистрибутивы» (video 2) ----------------------
        Revolutionary = Desktop.Add(FolderKey("fs.revolutionary_distributions"));

        AntivirusFile = Revolutionary.Add(new VNode
        {
            NameKey = "fs.grevtsov_antivirus_txt",
            Kind = NodeKind.TextFile,
            Icon = IconId.TextFile,
            TextKey = "fs.antivirus_greeting",
            Launch = "notepad",
        });

        Revolutionary.Add(new VNode
        {
            NameKey = "fs.efrate_jpeg",
            Kind = NodeKind.ImageFile,
            Icon = IconId.ImageFile,
            Picture = PictureId.Photo,
            ExplicitSize = 23 * 1024,
            Launch = "paint",
        });

        Revolutionary.Add(new VNode
        {
            Name = "File.АЗЦqЧИГ",
            Kind = NodeKind.Unknown,
            Icon = IconId.UnknownFile,
            ExplicitSize = 0,
        });

        Revolutionary.Add(new VNode
        {
            NameKey = "fs.minesweeper",
            Kind = NodeKind.Shortcut,
            Icon = IconId.Minesweeper,
            Launch = "minesweeper",
            ExplicitSize = 1505,
            TooltipKey = "fs.play_minesweeper",
        });

        // ---- «ПОЛЕЗНЫЕ ФИШКИ МИМИНУСА» (video 3) -------------------------
        UsefulTricks = Desktop.Add(FolderKey("fs.useful_miminus_tricks"));

        UsefulTricks.Add(new VNode
        {
            NameKey = "fs.calculator_plus",
            Kind = NodeKind.Shortcut, Icon = IconId.Calculator, Launch = "calculator",
        });
        UsefulTricks.Add(new VNode
        {
            NameKey = "fs.all_in_one_txt",
            Kind = NodeKind.TextFile, Icon = IconId.TextFile, Launch = "notepad",
            TextKey = "fs.miminus_os_all_in_one_our_own_web_browser_ou",
        });
        UsefulTricks.Add(new VNode
        {
            Name = "BolgenOS on TV.avi",
            Kind = NodeKind.Video, Icon = IconId.VideoFile, Launch = "player",
            ExplicitSize = 18L * 1024 * 1024,
        });

        // ---- «МИ», the most important folder in the system (video 3) -----
        var mi = Desktop.Add(new VNode
        {
            NameKey = "fs.mi_folder", Kind = NodeKind.Folder, Icon = IconId.Folder,
            Protected = true,
        });
        mi.Add(new VNode
        {
            NameKey = "fs.all_in_one_txt_2", Kind = NodeKind.TextFile, Icon = IconId.TextFile,
            Launch = "notepad", TextKey = "fs.mi_folder_note",
        });

        // ---- 100 backups, made by copying (video 3, 02:22) ---------------
        Backups = Desktop.Add(FolderKey("fs.backups_folder"));
        for (int i = 1; i <= 100; i++)
        {
            Backups.Add(new VNode
            {
                Name = L.F("fs.backup_n", i),
                Kind = NodeKind.Archive,
                Icon = IconId.Archive,
                Launch = "backup",
                ExplicitSize = 1024L * (40 + i),
            });
        }

        // ---- other desktop content --------------------------------------
        Desktop.Add(new VNode
        {
            NameKey = "fs.coursework_xls",
            Kind = NodeKind.Spreadsheet, Icon = IconId.Spreadsheet, Launch = "spreadsheet",
            ExplicitSize = 27 * 1024,
        });
        Desktop.Add(new VNode
        {
            NameKey = "fs.read_me_txt",
            Kind = NodeKind.TextFile, Icon = IconId.TextFile, Launch = "notepad",
            TextKey = "fs.our_answer_to_bolgenos_we_took_everything_go",
        });

        var music = MyMusic;
        music.Add(new VNode { Name = "Songa.mp3", Kind = NodeKind.Audio, Icon = IconId.AudioFile, ExplicitSize = 4200000, Launch = "player" });
        music.Add(new VNode { Name = "Chudo.mp3", Kind = NodeKind.Audio, Icon = IconId.AudioFile, ExplicitSize = 3800000, Launch = "player" });
        music.Add(new VNode { Name = "Metro2033.mp3", Kind = NodeKind.Audio, Icon = IconId.AudioFile, ExplicitSize = 5100000, Launch = "player" });

        MyPictures.Add(new VNode
        {
            NameKey = "fs.efrate_jpeg", Kind = NodeKind.ImageFile,
            Icon = IconId.ImageFile, Picture = PictureId.Photo, ExplicitSize = 23 * 1024, Launch = "paint"
        });
        MyPictures.Add(new VNode
        {
            NameKey = "fs.miminus_wallpaper_bmp", Kind = NodeKind.ImageFile,
            Icon = IconId.ImageFile, Picture = PictureId.Wallpaper, ExplicitSize = 900 * 1024, Launch = "paint"
        });

        MyDocuments.Add(new VNode
        {
            NameKey = "fs.earnings_doc", Kind = NodeKind.Document,
            Icon = IconId.WordDoc, ExplicitSize = 34 * 1024,
        });
    }

    /// <summary>Every host folder currently mounted as a drive.</summary>
    public readonly List<HostMount> Mounts = new();

    /// <summary>Mounts a real directory as a drive under My Computer.
    ///
    /// The mount is read-only unless <paramref name="writable"/> is set, so
    /// browsing somebody's disk from the parody desktop cannot damage it.</summary>
    public HostMount MountHostFolder(string hostPath, string driveLetter = null, bool writable = false)
    {
        string full;
        try { full = Path.GetFullPath(hostPath); }
        catch { return null; }
        if (!Directory.Exists(full)) return null;

        // Pick the first free letter from Z downwards when none was asked for.
        if (string.IsNullOrEmpty(driveLetter))
        {
            for (char ch = 'Z'; ch >= 'M'; ch--)
            {
                string candidate = ch + ":";
                if (MyComputer.Children.All(n => !string.Equals(n.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    driveLetter = candidate;
                    break;
                }
            }
            driveLetter ??= "Z:";
        }
        if (!driveLetter.EndsWith(':')) driveLetter += ":";

        string label = new DirectoryInfo(full).Name;
        if (string.IsNullOrEmpty(label)) label = full;

        var node = new VNode
        {
            Name = $"{label} ({driveLetter})",
            Kind = NodeKind.Removable,
            Icon = IconId.DriveUsb,
            HostPath = full,
            Modified = DateTime.Now,
        };

        var mount = new HostMount(node, full, writable);
        node.Mount = mount;

        var (free, total) = mount.Space();
        node.Free = free;
        node.Capacity = total;

        MyComputer.Add(node);
        Mounts.Add(mount);
        return mount;
    }

    static VNode Folder(string name) => new() { Name = name, Kind = NodeKind.Folder, Icon = IconId.Folder };

    /// <summary>A folder whose name comes from the translation catalogue.</summary>
    static VNode FolderKey(string key) => new() { NameKey = key, Kind = NodeKind.Folder, Icon = IconId.Folder };

    /// <summary>Finds a child by its catalogue key rather than its rendered name,
    /// so lookups keep working across a language switch.</summary>
    public static VNode ByKey(VNode parent, string key)
        => parent?.Children.FirstOrDefault(n => n.NameKey == key);

    /// <summary>Resolves a backslash path from the My Computer root. Returns null
    /// when any segment is missing.</summary>
    public VNode Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        VNode cur = MyComputer.Find(parts[0]);
        if (cur == null)
        {
            // Allow paths that start at My Computer itself.
            if (string.Equals(parts[0], MyComputer.Name, StringComparison.OrdinalIgnoreCase))
            {
                cur = MyComputer;
                parts = parts[1..];
                if (parts.Length == 0) return cur;
                cur = cur.Find(parts[0]);
            }
            if (cur == null) return null;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            cur = cur.Find(parts[i]);
            if (cur == null) return null;
        }
        return cur;
    }

    /// <summary>Creates a uniquely named child, the way the New submenu does.</summary>
    public VNode CreateChild(VNode parent, string baseName, NodeKind kind, IconId icon)
    {
        string name = baseName;
        int n = 2;
        while (parent.Find(name) != null)
            name = $"{baseName} ({n++})";

        var node = new VNode
        {
            Name = name,
            Kind = kind,
            Icon = icon,
            Modified = DateTime.Now,
            Text = kind == NodeKind.TextFile ? "" : null,
            Launch = kind == NodeKind.TextFile ? "notepad" : null,
        };
        parent.Add(node);
        return node;
    }

    /// <summary>True when the node refuses to be deleted.</summary>
    public static bool IsProtected(VNode node) => node is { Protected: true };

    /// <summary>True for a folder named Windows, which part 3 creates purely to
    /// delete again and prove the system survives.</summary>
    public static bool IsWindowsFolder(VNode node)
        => node is { Kind: NodeKind.Folder } &&
           node.Name.Equals("Windows", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when a node's name may be edited. The protected nodes —
    /// the МИ folder, the drives, the special shell places — keep their names,
    /// and a node under a read-only mount is backed by a real file we have no
    /// business touching.</summary>
    public static bool CanRename(VNode node)
        => node?.Parent != null
           && !node.Protected
           && node.Kind is not (NodeKind.Drive or NodeKind.DvdDrive or NodeKind.Removable
                                or NodeKind.Device)
           && (node.Mount == null || node.Mount.Writable);

    /// <summary>The characters Windows refuses in a name, which this refuses too
    /// so a mounted rename cannot fail halfway.</summary>
    public const string InvalidNameChars = "\\/:*?\"<>|";

    /// <summary>Renames a node, and the real file or folder behind it when it is
    /// mounted. Returns false with a reason the caller can show.</summary>
    public bool Rename(VNode node, string newName, out string error)
    {
        error = null;
        newName = newName?.Trim();

        if (!CanRename(node)) { error = L.T("fs.rename_refused"); return false; }
        if (string.IsNullOrEmpty(newName)) { error = L.T("fs.rename_empty"); return false; }

        if (newName.IndexOfAny(InvalidNameChars.ToCharArray()) >= 0)
        {
            error = L.F("fs.rename_invalid_chars", InvalidNameChars);
            return false;
        }

        if (newName == node.Name) return true;

        if (node.Parent.Children.Any(sibling => sibling != node &&
                sibling.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            error = L.F("fs.rename_exists", newName);
            return false;
        }

        if (node.IsHosted && !HostMount.Rename(node, newName, out error)) return false;

        // A translated name cannot survive being edited: once the user has typed
        // one, the node stops following the interface language.
        node.NameKey = null;
        node.Name = newName;
        node.Modified = DateTime.Now;
        return true;
    }

    /// <summary>True for a node the shell will not delete on an ordinary
    /// Delete: a folder called Windows belongs to the system, and taking it out
    /// has to be deliberate. Holding Ctrl is what makes it deliberate.</summary>
    public static bool NeedsForce(VNode node) => IsWindowsFolder(node);

    /// <summary>Moves a node to the Recycle Bin, or removes it outright when
    /// <paramref name="permanent"/> — which is what Ctrl+Delete does, and the
    /// only way a Windows folder goes anywhere.</summary>
    /// <summary>True when <paramref name="folder"/> is inside
    /// <paramref name="node"/>, which is the one move that cannot be made.</summary>
    static bool Contains(VNode node, VNode folder)
    {
        for (var walk = folder; walk != null; walk = walk.Parent)
            if (walk == node) return true;
        return false;
    }

    /// <summary>Moves a node into a folder. Returns false with a reason the
    /// caller can put in front of the user; a move that would change nothing
    /// succeeds silently.</summary>
    public bool Move(VNode node, VNode folder, out string error)
    {
        error = null;

        if (node == null || folder == null || !folder.IsContainer)
        {
            error = L.T("fs.move_refused");
            return false;
        }

        if (node.Parent == folder) return true;

        if (node.Parent == null || node.Protected)
        {
            error = L.T("fs.protected_folder");
            return false;
        }

        if (Contains(node, folder))
        {
            error = L.T("fs.move_into_itself");
            return false;
        }

        // Reading the destination first also loads a mounted folder that has
        // not been opened yet, so the name check sees what is really in it.
        if (folder.Entries.Any(sibling =>
                sibling.Name.Equals(node.Name, StringComparison.OrdinalIgnoreCase)))
        {
            error = L.F("fs.rename_exists", node.Name);
            return false;
        }

        // A node backed by a real file can only move within its own mount, and
        // only when that mount was opened for writing.
        if (node.IsHosted || folder.IsHosted)
        {
            if (!node.IsHosted || !folder.IsHosted)
            {
                error = L.T("mount.move_refused");
                return false;
            }
            if (!HostMount.MoveInto(node, folder, out error)) return false;
        }

        node.Parent.Children.Remove(node);
        folder.Add(node);
        node.Modified = DateTime.Now;
        return true;
    }

    public void Delete(VNode node, bool permanent = false)
    {
        if (node?.Parent == null || node.Protected) return;
        if (NeedsForce(node) && !permanent) return;

        node.Parent.Children.Remove(node);

        if (permanent)
        {
            node.Parent = null;
            return;
        }

        node.Parent = RecycleBin;
        RecycleBin.Children.Add(node);
    }
}
