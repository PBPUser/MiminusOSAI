using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Экран «Пуск» — the full-screen tile board version 8 opens instead of
/// a Start menu.
///
/// Everything the Windows 8 Start screen did that mattered is here: solid
/// colour tiles in groups, tiles that are alive and turn over to show something
/// new, an *Все приложения* view behind a swipe up, a search that starts the
/// moment a letter is typed, and an app bar under the right button. Nothing on
/// it is an image — a tile is a rectangle of one colour with an icon and a word
/// on it, which is exactly what the originals were.</summary>
public sealed class StartScreen
{
    readonly ShellHost _shell;

    public StartScreen(ShellHost shell) => _shell = shell;

    // ---- what is on it ---------------------------------------------------

    /// <summary>What a tile does with the space under its name: nothing, or one
    /// of the four things this system has to say.</summary>
    enum Live { None, Clock, Antivirus, Weather, People, Store }

    sealed record Tile(string Key, IconId Icon, string App, bool Wide = false,
                       int Colour = 0, Live Live = Live.None, string Arg = null);

    sealed record Group(string TitleKey, Tile[] Tiles);

    static readonly Group[] Groups =
    {
        new("start8.group_miminus", new[]
        {
            new Tile("start8.desktop", IconId.Display, "$desktop", Wide: true, Colour: 4),
            new Tile("start8.clock", IconId.Clock, "clock", Wide: true, Colour: 0, Live: Live.Clock),
            new Tile("start.internet", IconId.Firefox, "browser", Colour: 2),
            new Tile("start.notepad", IconId.Notepad, "notepad", Colour: 6),
            new Tile("start.paint", IconId.Paint, "paint", Colour: 5),
            new Tile("start.media_player", IconId.MediaPlayer, "player", Colour: 8),
            new Tile("start.minesweeper", IconId.Minesweeper, "minesweeper", Colour: 1),
            new Tile("start.calculator_plus", IconId.Calculator, "calculator", Colour: 3),
            new Tile("start.miminus_sheet", IconId.Spreadsheet, "spreadsheet", Colour: 9),
            new Tile("start.command_prompt", IconId.Terminal, "terminal", Colour: 4),
        }),

        new("start8.group_system", new[]
        {
            new Tile("start8.antivirus", IconId.Antivirus, "$antivirus", Wide: true, Colour: 1,
                     Live: Live.Antivirus),
            new Tile("pcs.title", IconId.PcSettings, "pcsettings", Colour: 0),
            new Tile("start.control_panel", IconId.ControlPanel, "controlpanel", Colour: 4),
            new Tile("start.display_properties", IconId.Display, "display", Colour: 6),
            new Tile("taskbar.task_manager", IconId.Settings, "taskmgr", Colour: 3),
            new Tile("start.windows_update", IconId.Shield, "update", Colour: 1),
            new Tile("start.my_computer", IconId.MyComputer, "mycomputer", Colour: 8),
        }),

        new("start8.group_fun", new[]
        {
            new Tile("store.title", IconId.Store, "store", Wide: true, Colour: 6, Live: Live.Store),
            new Tile("start8.weather", IconId.Weather, "allinone", Wide: true, Colour: 2,
                     Live: Live.Weather),
            new Tile("start8.people", IconId.People, "voice", Colour: 7, Live: Live.People),
            new Tile("start.all_in_one", IconId.Settings, "allinone", Colour: 5),
            new Tile("start.orega", IconId.Opera, "orega", Colour: 9),
            new Tile("whatsnew.title", IconId.Star, "whatsnew", Colour: 3),
        }),
    };

    // ---- state -----------------------------------------------------------

    bool _open;

    /// <summary>0 closed, 1 fully open. The board fades and slides in over it,
    /// which is as much of the zoom as a flat renderer needs.</summary>
    float _phase;

    bool _allApps;
    string _query;
    float _scroll;
    double _openedAt;

    /// <summary>True while the board is on its way in. The tiles cascade then
    /// and only then.</summary>
    bool _opening;

    /// <summary>True while any part of the board is on screen.</summary>
    public bool Visible => _phase > 0.002f;

    /// <summary>True once it covers the screen: nothing underneath is worth
    /// drawing, and nothing underneath can be clicked.</summary>
    public bool Opaque => _phase > 0.995f;

    public bool IsOpen => _open;

    /// <summary>How big a tile is. It is a setting rather than a constant now,
    /// so the board can be made to hold more of them or fewer.</summary>
    float TileSize => _shell.Settings.StartTileSize;

    const float TileGap = 8;
    const int Rows = 3;
    const float HeaderH = 96;
    const float AppBarH = 68;

    public void Open(UiContext c)
    {
        if (_open) return;
        _open = true;
        _allApps = false;
        _query = null;
        _scroll = 0;
        _openedAt = c.Time;
        _opening = true;
        c.Sound(Sfx.MenuOpen, 0.6f);
    }

    public void Close(UiContext c)
    {
        if (!_open) return;
        _open = false;
        _opening = false;
        _query = null;
        c.Sound(Sfx.MenuClose, 0.6f);
    }

    public void Toggle(UiContext c)
    {
        if (_open) Close(c); else Open(c);
    }

    // ---- frame -----------------------------------------------------------

    /// <summary>Draws the board and runs its input. Called last in the frame,
    /// and claims the pointer and the keyboard outright: while the Start screen
    /// is up it is the only thing there is.</summary>
    public void Draw(UiContext c)
    {
        float target = _open ? 1 : 0;
        if (!_shell.Settings.Animations) _phase = target;
        _phase += Math.Clamp(target - _phase, -1f, 1f) * MathF.Min(1, c.Dt * 11);
        if (MathF.Abs(target - _phase) < 0.004f) _phase = target;
        if (!Visible) return;

        var t = c.Theme;
        var screen = new Rect(0, 0, c.ScreenW, c.ScreenH);
        byte alpha = (byte)(255 * _phase);

        DrawBackdrop(c, screen, alpha);

        // The board slides in from the right as it fades, which is the whole of
        // the animation the original had.
        float slide = (1 - _phase) * 120;

        var header = new Rect(screen.X, screen.Y, screen.W, HeaderH);
        DrawHeader(c, header, alpha, slide);

        var body = new Rect(screen.X, header.Bottom, screen.W, screen.H - header.H);

        if (_query != null) DrawSearch(c, body, alpha, slide);
        else if (_allApps) DrawAllApps(c, body, alpha, slide);
        else DrawTiles(c, body, alpha, slide);

        DrawAppBar(c, screen, alpha);
        HandleInput(c, screen);

        // Nothing beneath the board hears anything while it is up.
        c.MouseHandled = true;
        c.KeyboardHandled = true;
    }

    /// <summary>Flat accent colour with the faint diagonal weave the original
    /// backgrounds had. No texture is loaded: it is drawn every frame, because
    /// two hundred thin lines cost less than the branch that would avoid them.</summary>
    void DrawBackdrop(UiContext c, Rect screen, byte alpha)
    {
        var accent = Theme.MetroAccent;
        c.R.FillRect(screen, accent.Shade(0.62f).WithAlpha(alpha));

        // A wash that lifts the left edge, so the header does not float.
        c.R.FillRectH(screen, accent.Shade(0.78f).WithAlpha((byte)(alpha * 0.9f)),
                      accent.Shade(0.5f).WithAlpha((byte)(alpha * 0.9f)));

        var line = Color.Rgba(0xFFFFFF, (byte)(10 * _phase));
        for (float x = -screen.H; x < screen.W; x += 26)
            c.R.Line(x, screen.Bottom, x + screen.H, screen.Y, line, 6);
    }

    void DrawHeader(UiContext c, Rect r, byte alpha, float slide)
    {
        string title = _query != null ? L.T("start8.search")
                     : _allApps ? L.T("start8.apps")
                     : L.T("start8.start");

        c.F.Big.Draw(c.R, title, 58 - slide, r.CenterY - c.F.Big.Height * 0.5f,
                     Color.Rgba(0xFFFFFF, alpha));

        // The account, top right: the picture, the name, and the power button
        // beside it — which is where version 8 hid "turn the computer off".
        var avatar = new Rect(r.Right - 60, r.CenterY - 20, 40, 40);
        string name = _shell.UserName;
        float nameW = c.F.Caption.Measure(name);
        var nameRect = new Rect(avatar.X - nameW - 12, r.CenterY - c.F.Caption.Height * 0.5f,
                                nameW + 4, c.F.Caption.Height);
        var account = new Rect(nameRect.X - 6, r.CenterY - 24, avatar.Right - nameRect.X + 12, 48);

        if (c.Hovering(account)) c.R.FillRect(account, Color.Rgba(0xFFFFFF, (byte)(30 * _phase)));

        c.F.Caption.Draw(c.R, name, nameRect.X, nameRect.Y, Color.Rgba(0xFFFFFF, alpha));
        c.R.FillRect(avatar, Color.Rgba(0xFFFFFF, (byte)(200 * _phase)));
        c.R.FillCircle(avatar.CenterX, avatar.Y + 14, 8, Theme.MetroAccent.WithAlpha(alpha));
        c.R.PushClip(avatar);
        c.R.FillCircle(avatar.CenterX, avatar.Bottom + 3, 14, Theme.MetroAccent.WithAlpha(alpha));
        c.R.PopClip();

        if (c.Clicked(account)) ShowAccountMenu(c, account);

        // Back arrow, when the board is showing something other than tiles.
        if (_allApps || _query != null)
        {
            var back = new Rect(20 - slide, r.CenterY - 16, 32, 32);
            bool hot = c.Hovering(back);
            c.R.DrawCircle(back.CenterX, back.CenterY, 15,
                           Color.Rgba(0xFFFFFF, (byte)((hot ? 230 : 150) * _phase)), 2);
            W.Arrow(c, back, 3, Color.Rgba(0xFFFFFF, alpha), 4.5f);
            if (c.Clicked(back)) { _allApps = false; _query = null; c.Sound(Sfx.Navigate, 0.5f); }
        }
    }

    void ShowAccountMenu(UiContext c, Rect anchor)
    {
        _shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("start8.lock"), () => { _open = false; _shell.LockScreenNow(c); }, IconId.Lock),
            MenuItem.Of(L.T("start.log_off"), () => { _open = false; _shell.BeginLogOff(c); }, IconId.Logoff),
            MenuItem.Sep(),
            MenuItem.Of(L.T("pcs.title"), () => Launch(c, "pcsettings"), IconId.PcSettings),
        }, anchor.X, anchor.Bottom, this, c);
    }

    // ---- the tile board --------------------------------------------------

    /// <summary>Where one tile ended up, in board coordinates.</summary>
    readonly List<(Rect r, Tile tile)> _placed = new();

    void DrawTiles(UiContext c, Rect body, byte alpha, float slide)
    {
        _placed.Clear();

        float boardH = Rows * TileSize + (Rows - 1) * TileGap;
        float y0 = body.Y + MathF.Max(20, (body.H - AppBarH - boardH - 40) * 0.5f);
        float x = 58 - slide - _scroll;
        float total = 0;

        foreach (var group in Groups)
        {
            float width = LayoutGroup(group, x, y0);

            // Group heading, in the small caps the originals used.
            c.F.Ui.Draw(c.R, L.T(group.TitleKey), x + 2, y0 - c.F.Ui.Height - 10,
                        Color.Rgba(0xFFFFFF, (byte)(190 * _phase)));

            x += width + 56;
            total += width + 56;
        }

        // The cascade. Version 8's board did not appear — it was dealt, tile
        // by tile, from the left, each one arriving a fraction after the one
        // before it and sliding the last of its distance home as it faded up.
        // It is the most recognisable thing that board ever did, and it costs
        // one stagger term per tile.
        for (int i = 0; i < _placed.Count; i++)
        {
            var (r, tile) = _placed[i];
            float t01 = TileArrival(c, i);
            if (t01 <= 0.001f) continue;

            float ease = 1 - MathF.Pow(1 - t01, 3);
            var from = r.Offset(64 * (1 - ease), 22 * (1 - ease));

            DrawTile(c, from, tile, (byte)(alpha * t01));
        }

        // Horizontal scrolling: the board is wider than the screen soon enough.
        float overflow = MathF.Max(0, total + 58 - body.W);
        if (overflow > 0)
        {
            if (MathF.Abs(c.In.WheelDelta) > 0.01f)
            {
                _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 90, 0, overflow);
                c.In.WheelDelta = 0;
            }
            var track = new Rect(58, body.Bottom - AppBarH - 14, body.W - 116, 4);
            c.R.FillRect(track, Color.Rgba(0xFFFFFF, (byte)(40 * _phase)));
            float frac = track.W * MathF.Min(1, body.W / (total + 58));
            c.R.FillRect(new Rect(track.X + (track.W - frac) * (_scroll / overflow), track.Y, frac, track.H),
                         Color.Rgba(0xFFFFFF, (byte)(160 * _phase)));
        }
        else _scroll = 0;
    }

    /// <summary>Packs one group into columns three rows deep, the way the tile
    /// board did: each tile takes the first hole it fits in, reading down a
    /// column before moving to the next one. Returns the width it used.</summary>
    float LayoutGroup(Group group, float x, float y)
    {
        const int MaxCols = 8;
        var taken = new bool[MaxCols, Rows];
        int used = 0;

        foreach (var tile in group.Tiles)
        {
            int span = tile.Wide ? 2 : 1;
            bool placed = false;

            for (int col = 0; col + span <= MaxCols && !placed; col++)
            {
                for (int row = 0; row < Rows && !placed; row++)
                {
                    bool free = true;
                    for (int k = 0; k < span; k++) free &= !taken[col + k, row];
                    if (!free) continue;

                    for (int k = 0; k < span; k++) taken[col + k, row] = true;
                    used = Math.Max(used, col + span);
                    placed = true;

                    _placed.Add((new Rect(x + col * (TileSize + TileGap),
                                          y + row * (TileSize + TileGap),
                                          span * TileSize + (span - 1) * TileGap, TileSize), tile));
                }
            }
        }

        return used * TileSize + Math.Max(0, used - 1) * TileGap;
    }

    /// <summary>How far into its own arrival a tile is: nought before its turn
    /// comes, one once it has landed. The stagger is small — a fortieth of a
    /// second between neighbours — because the whole board has to be there
    /// before anybody has finished looking at it. On the way out there is no
    /// cascade at all: the board goes at once, which is what it did, and what
    /// keeps closing from feeling slow.</summary>
    float TileArrival(UiContext c, int index)
    {
        if (!_shell.Settings.Animations || !_opening) return _phase;

        const double Stagger = 0.025, Travel = 0.34;
        double t = c.Time - _openedAt - index * Stagger;
        if (t <= 0) return 0;

        return _phase * (float)Math.Clamp(t / Travel, 0, 1);
    }

    void DrawTile(UiContext c, Rect r, Tile tile, byte alpha)
    {
        var colour = Theme.TileColors[tile.Colour % Theme.TileColors.Length];
        bool hot = c.Hovering(r);
        bool held = hot && c.In.IsDown(MouseButton.Left);

        // A pressed tile tips a little, which the original did by tilting the
        // whole rectangle; shrinking it reads the same at this size.
        var face = held ? r.Deflate(3) : r;
        c.R.FillRect(face, (held ? colour.Shade(0.86f) : hot ? colour.Shade(1.12f) : colour)
                     .WithAlpha(alpha));
        if (hot) c.R.DrawRect(face, Color.Rgba(0xFFFFFF, (byte)(180 * _phase)), 2);

        if (tile.Live != Live.None) DrawLiveFace(c, face, tile, alpha);
        else
        {
            float size = tile.Wide ? 40 : 36;
            Icons.Draw(c.R, tile.Icon,
                       new Rect(face.CenterX - size * 0.5f, face.Y + (face.H - size) * 0.5f - 8, size, size));
        }

        // The name always sits in the bottom-left corner, whatever the tile is,
        // and is cut with an ellipsis rather than by the edge of the tile.
        c.R.PushClip(face);
        c.F.Small.Draw(c.R, c.F.Small.Ellipsize(L.T(tile.Key), face.W - 14),
                       face.X + 8, face.Bottom - c.F.Small.Height - 7,
                       Color.Rgba(0xFFFFFF, alpha));
        c.R.PopClip();

        if (c.Clicked(r)) { c.SoundAt(Sfx.Tile, r, 0.7f); Launch(c, tile.App); }
        else if (c.RightClicked(r)) ShowTileMenu(c, tile);
    }

    /// <summary>The half of a live tile that changes. Each one turns over every
    /// few seconds, which is the entire behaviour the word "live" described.</summary>
    void DrawLiveFace(UiContext c, Rect face, Tile tile, byte alpha)
    {
        var white = Color.Rgba(0xFFFFFF, alpha);
        var faint = Color.Rgba(0xFFFFFF, (byte)(alpha * 0.75f));
        var inner = face.Deflate(10, 8, 10, 24);

        // Two faces, swapped every four seconds with a slide between them.
        double cycle = (c.Time - _openedAt) * 0.25;
        bool second = ((int)cycle) % 2 == 1;
        float turn = (float)(cycle - Math.Floor(cycle));
        float lift = turn > 0.92f ? (turn - 0.92f) / 0.08f * inner.H : 0;
        inner = inner.Offset(0, -lift);

        c.R.PushClip(face);
        switch (tile.Live)
        {
            case Live.Clock:
                if (!second)
                {
                    c.F.Big.Draw(c.R, L.Time(_shell.Now), inner.X, inner.Y, white);
                    c.F.Small.Draw(c.R, L.LongDate(_shell.Now), inner.X + 2,
                                   inner.Y + c.F.Big.Height, faint);
                }
                else
                {
                    c.F.Caption.Draw(c.R, L.T("start8.uptime"), inner.X, inner.Y, faint);
                    c.F.Big.Draw(c.R, _shell.Now.ToString("HH:mm:ss"), inner.X,
                                 inner.Y + c.F.Caption.Height + 2, white);
                }
                break;

            case Live.Antivirus:
                if (!second)
                {
                    Icons.Draw(c.R, IconId.Antivirus, new Rect(inner.X, inner.Y, 36, 36));
                    c.F.Caption.Draw(c.R, L.T("start8.antivirus_ready"),
                                     inner.X + 44, inner.Y + 8, white);
                }
                else
                {
                    c.F.Big.Draw(c.R, "0", inner.X, inner.Y - 4, white);
                    c.F.Small.Draw(c.R, L.T("start8.popovs_found"),
                                   inner.X + 34, inner.Y + 14, faint);
                }
                break;

            case Live.Weather:
                Icons.Draw(c.R, IconId.Weather, new Rect(inner.X, inner.Y, 40, 40));
                if (!second)
                {
                    c.F.Big.Draw(c.R, L.T("start8.weather_temp"), inner.X + 48, inner.Y - 4, white);
                    c.F.Small.Draw(c.R, L.T("start8.weather_place"), inner.X + 50,
                                   inner.Y + c.F.Big.Height - 6, faint);
                }
                else
                {
                    c.F.Caption.Draw(c.R, L.T("start8.weather_month"), inner.X + 48, inner.Y + 2, white);
                    c.F.Small.Draw(c.R, L.T("start8.weather_forecast"), inner.X + 50,
                                   inner.Y + c.F.Caption.Height + 4, faint);
                }
                break;

            case Live.People:
                Icons.Draw(c.R, IconId.People, new Rect(inner.X, inner.Y, 34, 34));
                c.F.Small.Draw(c.R, L.T(second ? "start8.people_second" : "start8.people_first"),
                               inner.X, inner.Y + 38, faint);
                break;

            case Live.Store:
                Icons.Draw(c.R, IconId.Store, new Rect(inner.X, inner.Y, 40, 40));
                c.F.Caption.Draw(c.R, L.T(second ? "start8.store_second" : "start8.store_first"),
                                 inner.X + 48, inner.Y + 10, white);
                break;
        }
        c.R.PopClip();
    }

    void ShowTileMenu(UiContext c, Tile tile)
    {
        _shell.Menus.Open(new List<MenuItem>
        {
            MenuItem.Of(L.T("start8.open"), () => Launch(c, tile.App), tile.Icon),
            MenuItem.Sep(),
            // Tiles cannot actually be unpinned: this board is the system.
            MenuItem.Of(L.T("start8.unpin"), () => _shell.MessageBox(c, L.T("start8.start"),
                L.T("start8.cannot_unpin"), MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info)),
            MenuItem.Of(L.T("start8.all_apps"), () => { _allApps = true; c.Sound(Sfx.Navigate, 0.5f); },
                        IconId.Tiles),
            MenuItem.Of(L.T("start8.classic_menu"),
                        () => { Close(c); _shell.Taskbar.StartOpen = true; }, IconId.Flag),
        }, c.MouseX, c.MouseY, this, c);
    }

    // ---- все приложения ---------------------------------------------------

    /// <summary>Everything registered, in one alphabetical wall. A program
    /// dropped into apps/ by somebody else is in here without anything being
    /// edited, exactly as it is in the old Start menu.</summary>
    List<(string name, IconId icon, string app)> AllPrograms()
    {
        var list = _shell.Programs.All
            .Select(p => (name: L.T(p.NameKey), icon: p.Icon, app: p.Id))
            .ToList();

        list.Add((L.T("start.my_computer"), IconId.MyComputer, "mycomputer"));
        list.Add((L.T("start.run"), IconId.Run, "run"));
        list.Add((L.T("start.help_and_support"), IconId.Help, "help"));
        list.Add((L.T("start.about_miminus"), IconId.DlgInfo, "about"));

        return list.OrderBy(x => x.name, StringComparer.CurrentCulture).ToList();
    }

    void DrawAllApps(UiContext c, Rect body, byte alpha, float slide)
    {
        var apps = AllPrograms();
        var area = new Rect(58 - slide, body.Y + 10, body.W - 96, body.H - AppBarH - 20);

        float rowH = 46;
        int rows = Math.Max(1, (int)(area.H / rowH));
        float colW = 260;

        float x = area.X - _scroll, y = area.Y;
        int inColumn = 0;
        float widest = 0;

        foreach (var (name, icon, app) in apps)
        {
            var row = new Rect(x, y, colW - 20, rowH - 6);
            DrawAppRow(c, row, name, icon, app, alpha);

            widest = MathF.Max(widest, row.Right - (area.X - _scroll));
            y += rowH;
            if (++inColumn >= rows) { inColumn = 0; x += colW; y = area.Y; }
        }

        float total = widest + (inColumn > 0 ? colW : 0);
        float overflow = MathF.Max(0, total - area.W);
        if (overflow > 0 && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 110, 0, overflow);
            c.In.WheelDelta = 0;
        }
        if (overflow <= 0) _scroll = 0;
    }

    void DrawAppRow(UiContext c, Rect row, string name, IconId icon, string app, byte alpha)
    {
        bool hot = c.Hovering(row);
        if (hot) c.R.FillRect(row, Color.Rgba(0xFFFFFF, (byte)(40 * _phase)));

        var badge = new Rect(row.X, row.CenterY - 16, 32, 32);
        c.R.FillRect(badge, Theme.TileColors[Math.Abs(app.GetHashCode()) % Theme.TileColors.Length]
                     .WithAlpha(alpha));
        Icons.Draw(c.R, icon, badge.Deflate(5));

        c.R.PushClip(row);
        c.F.Ui.Draw(c.R, name, badge.Right + 10, row.CenterY - c.F.Ui.Height * 0.5f,
                    Color.Rgba(0xFFFFFF, alpha));
        c.R.PopClip();

        if (c.Clicked(row)) Launch(c, app);
    }

    // ---- поиск -------------------------------------------------------------

    /// <summary>Typing anywhere on the board searches it, which is the one part
    /// of version 8 nobody had to be told about.</summary>
    void DrawSearch(UiContext c, Rect body, byte alpha, float slide)
    {
        var apps = AllPrograms()
            .Where(a => a.name.Contains(_query, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        // Results on the left, the box they answer on the right — the way round
        // the original had it.
        var pane = new Rect(body.Right - 320, body.Y, 320, body.H);
        c.R.FillRect(pane, Theme.MetroAccent.Shade(0.42f).WithAlpha(alpha));

        var box = new Rect(pane.X + 24, pane.Y + 30, pane.W - 48, 32);
        c.R.FillRect(box, Color.Rgba(0xFFFFFF, alpha));
        c.R.PushClip(box.Deflate(6, 0, 30, 0));
        c.F.Ui.Draw(c.R, _query.Length > 0 ? _query : L.T("start8.search_hint"),
                    box.X + 8, box.CenterY - c.F.Ui.Height * 0.5f,
                    _query.Length > 0 ? Color.Rgb(0x202020) : Color.Rgb(0x909090));
        c.R.PopClip();

        // Caret, so the box looks like it is being typed into.
        if ((int)(c.Time * 2) % 2 == 0 && _query.Length > 0)
            c.R.FillRect(new Rect(box.X + 9 + c.F.Ui.Measure(_query), box.Y + 7, 1.4f, box.H - 14),
                         Color.Rgb(0x202020));

        Icons.Draw(c.R, IconId.Search, new Rect(box.Right - 26, box.CenterY - 9, 18, 18));

        c.F.Ui.Draw(c.R, L.F("start8.results", apps.Count), box.X, box.Bottom + 16,
                    Color.Rgba(0xFFFFFF, (byte)(200 * _phase)));

        // The results themselves.
        var area = new Rect(58 - slide, body.Y + 10, pane.X - 100, body.H - AppBarH - 20);
        float rowH = 46;
        int rows = Math.Max(1, (int)(area.H / rowH));
        float x = area.X, y = area.Y;
        int inColumn = 0;

        foreach (var (name, icon, app) in apps)
        {
            if (x > area.Right) break;
            DrawAppRow(c, new Rect(x, y, 240, rowH - 6), name, icon, app, alpha);
            y += rowH;
            if (++inColumn >= rows) { inColumn = 0; x += 260; y = area.Y; }
        }

        if (apps.Count == 0)
            c.F.Big.Draw(c.R, L.T("start8.no_results"), area.X, area.Y + 40,
                         Color.Rgba(0xFFFFFF, (byte)(160 * _phase)));
    }

    // ---- панель приложений -------------------------------------------------

    bool _appBar;

    void DrawAppBar(UiContext c, Rect screen, byte alpha)
    {
        if (!_appBar) return;

        var bar = new Rect(0, screen.Bottom - AppBarH, screen.W, AppBarH);
        c.R.FillRect(bar, Theme.MetroAccent.Shade(0.38f).WithAlpha(alpha));
        c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 1), Color.Rgba(0xFFFFFF, (byte)(40 * _phase)));

        float x = bar.Right - 120;
        if (BarButton(c, new Rect(x, bar.Y + 10, 100, AppBarH - 20), IconId.Tiles,
                      L.T(_allApps ? "start8.start" : "start8.all_apps"), alpha))
        {
            _allApps = !_allApps;
            _scroll = 0;
            _appBar = false;
            c.Sound(Sfx.Navigate, 0.5f);
        }

        x -= 110;
        if (BarButton(c, new Rect(x, bar.Y + 10, 100, AppBarH - 20), IconId.Display,
                      L.T("start8.desktop"), alpha))
        {
            _appBar = false;
            Launch(c, "$desktop");
        }

        // The old menu is still there, and this is the way back to it.
        x -= 110;
        if (BarButton(c, new Rect(x, bar.Y + 10, 100, AppBarH - 20), IconId.Flag,
                      L.T("start8.classic_menu"), alpha))
        {
            _appBar = false;
            Close(c);
            _shell.Taskbar.StartOpen = true;
        }
    }

    bool BarButton(UiContext c, Rect r, IconId icon, string label, byte alpha)
    {
        bool hot = c.Hovering(r);
        if (hot) c.R.FillRect(r, Color.Rgba(0xFFFFFF, (byte)(35 * _phase)));

        var ring = new Rect(r.CenterX - 13, r.Y + 2, 26, 26);
        c.R.DrawCircle(ring.CenterX, ring.CenterY, 13, Color.Rgba(0xFFFFFF, alpha), 1.6f);
        Icons.Draw(c.R, icon, ring.Deflate(6));

        float w = c.F.Small.Measure(label);
        c.F.Small.Draw(c.R, label, r.CenterX - w * 0.5f, r.Bottom - c.F.Small.Height - 2,
                       Color.Rgba(0xFFFFFF, alpha));
        return c.Clicked(r);
    }

    // ---- input -------------------------------------------------------------

    void HandleInput(UiContext c, Rect screen)
    {
        // The right button raises the app bar, as it did on the original; a
        // left click on empty board puts it away again.
        if (c.In.Pressed(MouseButton.Right)) _appBar = !_appBar;
        else if (c.In.Pressed(MouseButton.Left) && !c.MouseHandled) _appBar = false;

        if (c.In.KeyPressed(Keys.Escape))
        {
            if (_query != null) _query = null;
            else if (_allApps) _allApps = false;
            else Close(c);
            return;
        }

        if (c.In.KeyPressed(Keys.Back) && _query is { Length: > 0 })
        {
            _query = _query[..^1];
            return;
        }

        // Anything typed starts a search, which is the whole search interface.
        foreach (char ch in c.In.TypedChars)
        {
            if (ch < ' ') continue;
            _query = (_query ?? "") + ch;
            _allApps = false;
        }
    }

    /// <summary>Starts what a tile or a row names and stands aside. The one
    /// pseudo-program is <c>$desktop</c>, which is the board closing.</summary>
    void Launch(UiContext c, string app)
    {
        Close(c);
        _allApps = false;
        _query = null;
        _appBar = false;

        switch (app)
        {
            case "$desktop": break;
            case "$antivirus": _shell.LaunchByName(c, "antivirus"); break;
            default: _shell.Launch(c, app, null); break;
        }
    }
}
