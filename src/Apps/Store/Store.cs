using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Магазин Миминус» — the store version 8 came with, and it installs
/// things.
///
/// A program in this system is a DLL in <c>apps/</c>, found by reflection and
/// remembered in an index. So installing one is a file copy and a rescan, and
/// that is exactly what «Установить» does: the package is copied out of
/// <c>store/</c> into <c>apps/</c>, the registry reads it, and the program is in
/// the Start menu and launchable immediately — nothing is compiled in anywhere,
/// so nothing has to be restarted. «Удалить» closes its windows, drops the
/// assembly out of memory and moves the file back, and the program stops
/// existing.
///
/// The catalogue is therefore not a list of pictures. It is the two folders:
/// what is in <c>apps/</c> is installed, what is in <c>store/</c> is available,
/// and dropping any assembly with an <c>IProgram</c> in it into <c>store/</c>
/// puts it on the shelf. The curated entries below only add a better name, a
/// description and a colour for the ones that shipped with the system.
///
/// Two entries can never be installed: one is refused on principle, and the
/// other has been «скоро» since 2010.</summary>
public sealed class StoreWindow : OsWindow
{
    public override string Title => L.T("store.title");
    public override float MinWidth => 560;
    public override float MinHeight => 380;

    public StoreWindow()
    {
        Icon = IconId.Store;
        Bounds = new Rect(0, 0, 760, 520);
        Immersive = true;
    }

    /// <summary>One catalogue entry. <c>App</c> is null for the two that cannot
    /// be installed, which is what makes them interesting.</summary>
    sealed record Listing(string NameKey, string DescKey, IconId Icon, string App,
                          int Colour, int Stars, string CategoryKey);

    static readonly Listing[] Catalogue =
    {
        new("start.notepad", "store.desc_notepad", IconId.Notepad, "notepad", 6, 5, "store.cat_tools"),
        new("start.grevtsov_antivirus", "store.desc_antivirus", IconId.Antivirus, "$antivirus", 1, 5,
            "store.cat_security"),
        new("start.paint", "store.desc_paint", IconId.Paint, "paint", 5, 4, "store.cat_tools"),
        new("start.minesweeper", "store.desc_minesweeper", IconId.Minesweeper, "minesweeper", 2, 5,
            "store.cat_games"),
        new("start.calculator_plus", "store.desc_calculator", IconId.Calculator, "calculator", 3, 4,
            "store.cat_tools"),
        new("start.miminus_sheet", "store.desc_sheet", IconId.Spreadsheet, "spreadsheet", 9, 4,
            "store.cat_office"),
        new("start.media_player", "store.desc_player", IconId.MediaPlayer, "player", 8, 4,
            "store.cat_media"),
        new("start.internet", "store.desc_browser", IconId.Firefox, "browser", 4, 3, "store.cat_internet"),
        new("start.orega", "store.desc_orega", IconId.Opera, "orega", 0, 3, "store.cat_internet"),
        new("start.all_in_one", "store.desc_allinone", IconId.Settings, "allinone", 7, 5,
            "store.cat_tools"),
        new("store.bolgenos", "store.desc_bolgenos", IconId.DlgError, null, 1, 1, "store.cat_system"),
        new("store.voice2", "store.desc_voice2", IconId.Volume, null, 3, 2, "store.cat_tools"),
    };

    /// <summary>One thing the store can show: either a curated entry for a
    /// program that shipped with the system, or a package sitting in one of the
    /// two folders. <c>Package</c> is the assembly this came from, when there
    /// is a file behind it — which is what makes it installable.</summary>
    sealed record Shelf(string Name, string Desc, IconId Icon, string App, int Colour,
                        int Stars, string CategoryKey, string Package, bool Installed,
                        bool Removable, string Refusal);

    int _selected = -1;
    float _scroll;

    /// <summary>Rebuilt whenever the shelves might have changed — which is when
    /// something is installed or removed, and once when the window opens.</summary>
    List<Shelf> _shelf;
    string _notice;
    double _noticeAt = -10;

    List<Shelf> Shelves(UiContext c)
    {
        if (_shelf != null) return _shelf;

        var list = new List<Shelf>();
        var byAssembly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ---- what shipped with the system --------------------------------
        foreach (var item in Catalogue)
        {
            // The two that can never be installed are added below with their
            // refusals attached, so they are not taken from here as well.
            if (item.App == null) continue;

            var entry = item.App[0] != '$' ? Shell.Programs.Find(item.App) : null;
            bool installed = item.App == "$antivirus" || entry != null;

            // A curated program that is no longer registered has been removed:
            // the store offers to put it back if the package is still around.
            string package = entry?.AssemblyPath;
            if (package != null) byAssembly[package] = item.NameKey;

            list.Add(new Shelf(L.T(item.NameKey), L.T(item.DescKey), item.Icon, item.App,
                               item.Colour, item.Stars, item.CategoryKey,
                               package, installed,
                               // The system's own programs are not removable: the
                               // store is not a way to take the OS apart.
                               false, null));
        }

        list.Add(new Shelf(L.T("store.bolgenos"), L.T("store.desc_bolgenos"), IconId.DlgError,
                           null, 1, 1, "store.cat_system", null, false, false, "store.refused"));
        list.Add(new Shelf(L.T("store.voice2"), L.T("store.desc_voice2"), IconId.Volume,
                           null, 3, 2, "store.cat_tools", null, false, false, "store.coming_soon"));

        // ---- what is installed but not in the catalogue -------------------
        foreach (var entry in Shell.Programs.All
                     .Where(e => e.AssemblyPath != null && !byAssembly.ContainsKey(e.AssemblyPath))
                     .GroupBy(e => e.AssemblyPath)
                     .Select(g => g.First()))
        {
            list.Add(new Shelf(L.T(entry.NameKey), Describe(entry.Id), entry.Icon, entry.Id,
                               Math.Abs(entry.Id.GetHashCode()) % 10, 5, "store.cat_tools",
                               entry.AssemblyPath, true, true, null));
        }

        // ---- what is on the shelf and not on the machine ------------------
        foreach (string file in Packages())
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(file);
            string key = name.StartsWith("Miminus.App.", StringComparison.Ordinal)
                ? name["Miminus.App.".Length..] : name;

            list.Add(new Shelf(PackageName(key), Describe(key), PackageIcon(key), null,
                               Math.Abs(key.GetHashCode()) % 10, 5, "store.cat_tools",
                               file, false, true, null));
        }

        _shelfAt = c.Time;
        return _shelf = list;
    }

    /// <summary>The assemblies waiting in store/ that are not already on the
    /// machine. A file in both folders is installed, and only shown once.</summary>
    IEnumerable<string> Packages()
    {
        string dir = Shell.StoreDirectory;
        if (!System.IO.Directory.Exists(dir)) yield break;

        foreach (string file in System.IO.Directory.EnumerateFiles(dir, "*.dll").OrderBy(f => f))
        {
            string installed = System.IO.Path.Combine(Shell.AppsDirectory ?? "",
                                                      System.IO.Path.GetFileName(file));
            if (!System.IO.File.Exists(installed)) yield return file;
        }
    }

    /// <summary>A name for a package the catalogue says nothing about. The one
    /// that ships on the shelf is known by name; anything else somebody dropped
    /// in there is called after its file, which is honest.</summary>
    static string PackageName(string key) => key switch
    {
        "Clock" => L.T("clockapp.title"),
        _ => key,
    };

    static IconId PackageIcon(string key) => key switch
    {
        "Clock" => IconId.Clock,
        _ => IconId.Program,
    };

    static string Describe(string key) => key switch
    {
        "Clock" or "clocktimer" => L.T("clockapp.store_desc"),
        _ => L.T("store.desc_custom"),
    };

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);

        var head = client.CutTop(64);
        c.R.FillRect(head, Theme.MetroAccent);
        c.F.Big.Draw(c.R, L.T("store.title"), head.X + 20, head.CenterY - c.F.Big.Height * 0.5f,
                     Color.White);
        c.F.Small.Draw(c.R, L.T("store.tagline"),
                       head.X + 22 + c.F.Big.Measure(L.T("store.title")) + 14,
                       head.CenterY - 2, Color.Rgba(0xFFFFFF, 190));

        var shelves = Shelves(c);
        if (_selected >= shelves.Count) _selected = -1;

        if (_selected >= 0) DrawDetail(c, client, shelves[_selected]);
        else DrawGrid(c, client, shelves);

        // What just happened, said once and then gone.
        if (_notice != null && c.Time - _noticeAt < 4.5)
        {
            var strip = new Rect(client.X, client.Bottom - 34, client.W, 34);
            c.R.FillRect(strip, Color.Rgb(0x1E7145));
            c.F.Ui.DrawCentered(c.R, _notice, strip, Color.White);
        }
    }

    void DrawGrid(UiContext c, Rect area, List<Shelf> shelves)
    {
        var view = area.Deflate(16, 14, 16, 12);

        const float cardW = 168, cardH = 132, gap = 12;
        int perRow = Math.Max(1, (int)((view.W + gap) / (cardW + gap)));
        int rows = (shelves.Count + perRow - 1) / perRow;
        float contentH = rows * (cardH + gap);

        if (contentH > view.H)
        {
            var bar = new Rect(view.Right - W.ScrollBarSize, view.Y, W.ScrollBarSize, view.H);
            _scroll = W.ScrollBarV(c, Id + ".scroll", bar, _scroll, contentH, view.H);
            view.W -= W.ScrollBarSize + 4;
            perRow = Math.Max(1, (int)((view.W + gap) / (cardW + gap)));
        }
        else _scroll = 0;

        if (c.Hovering(view) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 60, 0, MathF.Max(0, contentH - view.H));
            c.In.WheelDelta = 0;
        }

        c.R.PushClip(view);
        for (int i = 0; i < shelves.Count; i++)
        {
            var card = new Rect(view.X + (i % perRow) * (cardW + gap),
                                view.Y - _scroll + (i / perRow) * (cardH + gap), cardW, cardH);
            if (card.Bottom < view.Y || card.Y > view.Bottom) continue;

            // The same cascade the Start screen deals its tiles with, because
            // it is the same kind of board and it arrived the same way.
            float t01 = Arrival(c, i);
            if (t01 <= 0.001f) continue;

            float ease = 1 - MathF.Pow(1 - t01, 3);
            DrawCard(c, card.Offset(0, 26 * (1 - ease)), shelves[i], i, t01);
        }
        c.R.PopClip();
    }

    /// <summary>How far into its arrival a card is. The shelves are rebuilt
    /// whenever something is installed, and the cascade runs again from there —
    /// which is how the store says that something changed.</summary>
    float Arrival(UiContext c, int index)
    {
        if (!Shell.Settings.Animations) return 1;

        const double Stagger = 0.018, Travel = 0.3;
        double t = c.Time - _shelfAt - index * Stagger;
        return t <= 0 ? 0 : (float)Math.Clamp(t / Travel, 0, 1);
    }

    double _shelfAt = -10;

    void DrawCard(UiContext c, Rect r, Shelf item, int index, float fade = 1)
    {
        var colour = Theme.TileColors[item.Colour % Theme.TileColors.Length];
        bool hot = c.Hovering(r);

        var art = new Rect(r.X, r.Y, r.W, 74);
        c.R.FillRect(art, hot ? colour.Shade(1.12f) : colour);
        Icons.Draw(c.R, item.Icon, new Rect(art.CenterX - 20, art.CenterY - 20, 40, 40));

        var foot = new Rect(r.X, art.Bottom, r.W, r.H - art.H);
        c.R.FillRect(foot, c.Theme.FaceLight);
        c.R.DrawRect(r, hot ? Theme.MetroAccent : c.Theme.ControlBorder);

        c.R.PushClip(foot);
        c.F.UiBold.Draw(c.R, c.F.UiBold.Ellipsize(item.Name, foot.W - 12),
                        foot.X + 6, foot.Y + 5, c.Theme.Text);
        DrawStars(c, new Rect(foot.X + 6, foot.Y + 6 + c.F.UiBold.Height + 2, 70, 12), item.Stars);

        // «Установлено», or «Можно установить» for something with a real
        // package behind it, or nothing at all for the two that never will be.
        string state = item.Installed ? "store.installed"
                     : item.Package != null ? "store.available" : "store.not_installed";
        c.F.Small.Draw(c.R, L.T(state), foot.X + 6, foot.Bottom - c.F.Small.Height - 5,
                       item.Installed ? Color.Rgb(0x1E7145)
                       : item.Package != null ? Theme.MetroAccent : c.Theme.TextDisabled);
        c.R.PopClip();

        if (c.Clicked(r)) { _selected = index; c.SoundAt(Sfx.Navigate, r, 0.5f); }
    }

    static void DrawStars(UiContext c, Rect r, int stars)
    {
        for (int i = 0; i < 5; i++)
        {
            var box = new Rect(r.X + i * 13, r.Y, 12, 12);
            Icons.Draw(c.R, IconId.Star, box);
            if (i >= stars) c.R.FillRect(box, c.Theme.FaceLight.WithAlpha((byte)190));
        }
    }

    void DrawDetail(UiContext c, Rect area, Shelf item)
    {
        var colour = Theme.TileColors[item.Colour % Theme.TileColors.Length];
        var body = area.Deflate(20, 16, 20, 16);

        var back = body.CutTop(28);
        var backBtn = new Rect(back.X, back.Y, 90, 22);
        if (W.FlatButton(c, Id + ".back", backBtn, L.T("store.back")))
        {
            _selected = -1;
            return;
        }

        body.CutTop(8);
        var header = body.CutTop(110);

        var art = new Rect(header.X, header.Y, 100, 100);
        c.R.FillRect(art, colour);
        Icons.Draw(c.R, item.Icon, art.Deflate(26));

        c.F.Big.Draw(c.R, item.Name, art.Right + 18, art.Y - 4, c.Theme.Text);
        c.F.Small.Draw(c.R, L.T(item.CategoryKey), art.Right + 20, art.Y + c.F.Big.Height,
                       c.Theme.TextDisabled);
        DrawStars(c, new Rect(art.Right + 20, art.Y + c.F.Big.Height + c.F.Small.Height + 6, 70, 12),
                  item.Stars);
        c.F.Small.Draw(c.R, L.T("store.price_free"), art.Right + 100,
                       art.Y + c.F.Big.Height + c.F.Small.Height + 6, Color.Rgb(0x1E7145));

        var action = new Rect(art.Right + 18, art.Bottom - 28, 180, 26);
        string label = item.Installed ? "store.open"
                     : item.Package != null ? "store.install" : "store.install";
        if (W.Button(c, Id + ".action", action, L.T(label), true, item.Icon, true))
            Activate(c, item);

        // Only something that arrived from the shelf can go back to it.
        if (item.Installed && item.Removable &&
            W.Button(c, Id + ".remove", new Rect(action.Right + 10, action.Y, 140, 26),
                     L.T("store.uninstall")))
            Uninstall(c, item);

        // Where the file actually is, because that is the whole trick.
        if (item.Package != null)
            c.F.Small.Draw(c.R, L.F("store.package_at", System.IO.Path.GetFileName(item.Package),
                                    item.Installed ? "apps" : "store"),
                           art.Right + 18, action.Bottom + 6, c.Theme.TextDisabled);

        body.CutTop(10);
        foreach (string line in c.F.Ui.Wrap(item.Desc, body.W))
        {
            if (body.H < c.F.Ui.Height) break;
            var row = body.CutTop(c.F.Ui.Height + 4);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, c.Theme.Text);
        }
    }

    void Activate(UiContext c, Shelf item)
    {
        if (item.App == "$antivirus") { Shell.LaunchByName(c, "antivirus"); return; }
        if (item.Installed && item.App != null) { Shell.Launch(c, item.App, null); return; }

        // Something with a package behind it is installed for real.
        if (item.Package != null) { Install(c, item); return; }

        // The two that never will be, and the reasons they never will be.
        Shell.MessageBox(c, L.T("store.title"), L.T(item.Refusal ?? "store.coming_soon"),
                         MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
    }

    void Install(UiContext c, Shelf item)
    {
        if (!Shell.InstallPackage(item.Package, out string error))
        {
            Shell.MessageBox(c, L.T("store.title"), L.F("store.install_failed", error),
                             MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
            return;
        }

        _shelf = null;
        _selected = -1;
        _notice = L.F("store.installed_notice", item.Name);
        _noticeAt = c.Time;
        c.Sound(Sfx.Snap, 0.9f);
    }

    void Uninstall(UiContext c, Shelf item)
    {
        Shell.MessageBox(c, L.T("store.title"), L.F("store.confirm_uninstall", item.Name),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion,
            r =>
            {
                if (r != MsgResult.Yes) return;

                if (!Shell.UninstallPackage(item.Package, c, out string error))
                {
                    Shell.MessageBox(c, L.T("store.title"), L.F("store.uninstall_failed", error),
                                     MsgButtons.Ok, IconId.DlgError, null, Sfx.Error);
                    return;
                }

                _shelf = null;
                _selected = -1;
                _notice = L.F("store.uninstalled_notice", item.Name);
                _noticeAt = c.Time;
            },
            Sfx.Question);
    }
}
