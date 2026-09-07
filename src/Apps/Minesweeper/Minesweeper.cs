using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Сапер — the Minesweeper played in part 2, rebuilt rule for rule:
/// LED counters, the smiley reset button, flags and question marks, chording on
/// a satisfied number, a first click that is always safe, and the classic three
/// difficulty presets.</summary>
public sealed class MinesweeperWindow : OsWindow
{
    enum CellState { Hidden, Revealed, Flagged, Question }
    enum GameState { Ready, Playing, Won, Lost }

    int _cols = 9, _rows = 9, _mineCount = 10;
    int[,] _adjacent;
    bool[,] _mine;
    CellState[,] _state;

    GameState _game = GameState.Ready;
    double _startTime;
    int _elapsed;
    bool _questionMarks = true;

    // Pressed-cell feedback while the button is held.
    int _pressCol = -1, _pressRow = -1;
    bool _chording;
    bool _smileyPressed;

    readonly int[] _best = { 999, 999, 999 };
    int _preset;   // 0 beginner, 1 intermediate, 2 expert

    const float CellSize = 16;
    const float Border = 9;
    const float PanelH = 34;

    public override string Title => L.T("mine.minesweeper");
    public override float MinWidth => 160;
    public override float MinHeight => 160;

    public MinesweeperWindow()
    {
        Icon = IconId.Minesweeper;
        Resizable = false;
        Maximizable = false;
        NewGame(9, 9, 10, 0);
        BuildMenu();
        Bounds = new Rect(0, 0, 200, 260);
    }

    public override void OnOpened(UiContext c) => Resize(c);

    /// <summary>Client area the board needs: a 6px margin around the status
    /// panel and the grid, with 6px between them.</summary>
    float ClientWidth => 12 + _cols * CellSize + 6;
    float ClientHeight => 6 + PanelH + 6 + (_rows * CellSize + 6) + 6;

    /// <summary>Sizes the window so the grid always fits exactly, accounting for
    /// the frame, caption and menu bar of the current theme.</summary>
    void Resize(UiContext c)
    {
        float menuH = Menu?.Height(c) ?? 0;
        Bounds.W = MathF.Round(ClientWidth + c.Theme.FrameThickness * 2);
        Bounds.H = MathF.Round(ClientHeight + menuH + c.Theme.CaptionHeight + c.Theme.FrameThickness);
    }

    void BuildMenu()
    {
        Menu = new MenuBar();
        Menu.Add(L.T("mine.game"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("mine.new"), () => NewGame(_cols, _rows, _mineCount, _preset), shortcut: "F2"),
            MenuItem.Sep(),
            new() { Text = L.T("mine.beginner"), IsRadio = true, Checked = _preset == 0,
                    Click = () => { NewGame(9, 9, 10, 0); Resize(_ctx); } },
            new() { Text = L.T("mine.intermediate"), IsRadio = true, Checked = _preset == 1,
                    Click = () => { NewGame(16, 16, 40, 1); Resize(_ctx); } },
            new() { Text = L.T("mine.expert"), IsRadio = true, Checked = _preset == 2,
                    Click = () => { NewGame(30, 16, 99, 2); Resize(_ctx); } },
            MenuItem.Sep(),
            MenuItem.Check(L.T("mine.marks"), _questionMarks,
                           () => _questionMarks = !_questionMarks),
            MenuItem.Sep(),
            MenuItem.Of(L.T("mine.rules_title"), ShowRules, IconId.Help),
            MenuItem.Of(L.T("mine.best_times"), ShowBestTimes),
            MenuItem.Sep(),
            MenuItem.Of(L.T("mine.exit"), () => Close()),
        });
        Menu.Add(L.T("mine.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("mine.about_minesweeper"), () =>
                Shell.MessageBox(_ctx, L.T("mine.minesweeper"),
                    L.T("mine.miminus_minesweeper_version_5_1_a_game_of_ou"),
                    MsgButtons.Ok, IconId.Minesweeper, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    void ShowBestTimes()
    {
        string Best(int i) => _best[i] == 999 ? "—" : L.F("mine.seconds", _best[i]);
        string text = L.F("mine.best_times_body", Best(0), Best(1), Best(2));
        Shell.MessageBox(_ctx, L.T("mine.best_times_2"), text, MsgButtons.Ok, IconId.Star, null, Sfx.Info);
    }

    /// <summary>The rules as explained in part 2, which are not the rules of
    /// Minesweeper.</summary>
    void ShowRules()
    {
        Shell.MessageBox(_ctx, L.T("mine.rules_title"), L.T("mine.rules_body"),
                         MsgButtons.Ok, IconId.Minesweeper, null, Sfx.Info);
    }

    void NewGame(int cols, int rows, int mines, int preset)
    {
        _cols = cols; _rows = rows; _mineCount = mines; _preset = preset;
        _mine = new bool[cols, rows];
        _adjacent = new int[cols, rows];
        _state = new CellState[cols, rows];
        _game = GameState.Ready;
        _elapsed = 0;
        _pressCol = _pressRow = -1;
    }

    /// <summary>Mines are placed after the first click so that click is never a
    /// loss — the same courtesy the original extends.</summary>
    void PlaceMines(int safeCol, int safeRow)
    {
        var rng = Random.Shared;
        int placed = 0;
        while (placed < _mineCount)
        {
            int x = rng.Next(_cols), y = rng.Next(_rows);
            if (_mine[x, y]) continue;
            if (Math.Abs(x - safeCol) <= 1 && Math.Abs(y - safeRow) <= 1) continue;
            _mine[x, y] = true;
            placed++;
        }

        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
            {
                int n = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < _cols && ny >= 0 && ny < _rows && _mine[nx, ny]) n++;
                    }
                _adjacent[x, y] = n;
            }
    }

    int FlagCount()
    {
        int n = 0;
        foreach (var s in _state) if (s == CellState.Flagged) n++;
        return n;
    }

    int HiddenCount()
    {
        int n = 0;
        foreach (var s in _state) if (s != CellState.Revealed) n++;
        return n;
    }

    UiContext _ctx;

    public override void Tick(UiContext c, float dt)
    {
        if (_game == GameState.Playing)
            _elapsed = Math.Min(999, (int)(c.Time - _startTime));
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        var t = c.Theme;

        // Minesweeper always drew itself in the classic grey, whatever the theme.
        c.R.FillRect(client, Color.Rgb(0xC0C0C0));

        var outer = new Rect(client.X + 4, client.Y + 4,
                             _cols * CellSize + Border * 2 - 6,
                             client.H - 8);
        outer.W = MathF.Min(outer.W, client.W - 8);

        // Status panel.
        var panel = new Rect(client.X + 6, client.Y + 6, _cols * CellSize + 6, PanelH);
        SunkenBevel(c, panel, 2);
        DrawLed(c, new Rect(panel.X + 5, panel.Y + 4, 39, PanelH - 8), _mineCount - FlagCount());
        DrawLed(c, new Rect(panel.Right - 44, panel.Y + 4, 39, PanelH - 8), _elapsed);
        DrawSmiley(c, new Rect(panel.CenterX - 13, panel.Y + 4, 26, 26));

        // Grid.
        var grid = new Rect(client.X + 6, panel.Bottom + 6, _cols * CellSize + 6, _rows * CellSize + 6);
        SunkenBevel(c, grid, 3);
        var cells = new Rect(grid.X + 3, grid.Y + 3, _cols * CellSize, _rows * CellSize);

        HandleGrid(c, cells);

        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
                DrawCell(c, new Rect(cells.X + x * CellSize, cells.Y + y * CellSize, CellSize, CellSize), x, y);

        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.F2))
        {
            NewGame(_cols, _rows, _mineCount, _preset);
            c.Sound(Sfx.Click, 0.6f);
        }
    }

    static void SunkenBevel(UiContext c, Rect r, float w)
    {
        c.R.FillRect(new Rect(r.X, r.Y, r.W, w), Color.Rgb(0x808080));
        c.R.FillRect(new Rect(r.X, r.Y, w, r.H), Color.Rgb(0x808080));
        c.R.FillRect(new Rect(r.X, r.Bottom - w, r.W, w), Color.White);
        c.R.FillRect(new Rect(r.Right - w, r.Y, w, r.H), Color.White);
    }

    static void RaisedBevel(UiContext c, Rect r, float w)
    {
        c.R.FillRect(new Rect(r.X, r.Y, r.W, w), Color.White);
        c.R.FillRect(new Rect(r.X, r.Y, w, r.H), Color.White);
        c.R.FillRect(new Rect(r.X, r.Bottom - w, r.W, w), Color.Rgb(0x808080));
        c.R.FillRect(new Rect(r.Right - w, r.Y, w, r.H), Color.Rgb(0x808080));
    }

    /// <summary>Three-digit seven-segment display, clamped to -99..999.</summary>
    void DrawLed(UiContext c, Rect r, int value)
    {
        c.R.FillRect(r, Color.Black);
        value = Math.Clamp(value, -99, 999);
        string s = value < 0 ? "-" + Math.Abs(value).ToString("D2") : value.ToString("D3");

        float dw = r.W / 3f;
        for (int i = 0; i < 3; i++)
            DrawDigit(c, new Rect(r.X + i * dw + 2, r.Y + 2, dw - 4, r.H - 4), s[i]);
    }

    static readonly Dictionary<char, int> SegmentMap = new()
    {
        ['0'] = 0b1111110, ['1'] = 0b0110000, ['2'] = 0b1101101, ['3'] = 0b1111001,
        ['4'] = 0b0110011, ['5'] = 0b1011011, ['6'] = 0b1011111, ['7'] = 0b1110000,
        ['8'] = 0b1111111, ['9'] = 0b1111011, ['-'] = 0b0000001, [' '] = 0,
    };

    void DrawDigit(UiContext c, Rect r, char ch)
    {
        int segs = SegmentMap.TryGetValue(ch, out int v) ? v : 0;
        Color on = Color.Rgb(0xFF2020), off = Color.Rgb(0x400808);
        float th = MathF.Max(2, r.H * 0.11f);
        float midY = r.CenterY;

        // Segment order: a b c d e f g (bit 6 .. bit 0)
        void H(float y, int bit) =>
            c.R.FillRect(new Rect(r.X + th * 0.6f, y - th * 0.5f, r.W - th * 1.2f, th),
                         (segs & (1 << bit)) != 0 ? on : off);
        void V(float x, float y0, float y1, int bit) =>
            c.R.FillRect(new Rect(x - th * 0.5f, y0, th, y1 - y0),
                         (segs & (1 << bit)) != 0 ? on : off);

        H(r.Y + th * 0.5f, 6);                                   // a
        V(r.Right - th * 0.6f, r.Y + th, midY - th * 0.5f, 5);   // b
        V(r.Right - th * 0.6f, midY + th * 0.5f, r.Bottom - th, 4); // c
        H(r.Bottom - th * 0.5f, 3);                              // d
        V(r.X + th * 0.6f, midY + th * 0.5f, r.Bottom - th, 2);  // e
        V(r.X + th * 0.6f, r.Y + th, midY - th * 0.5f, 1);       // f
        H(midY, 0);                                              // g
    }

    void DrawSmiley(UiContext c, Rect r)
    {
        bool hover = c.Hovering(r);
        bool pressed = _smileyPressed && hover && c.In.IsDown(MouseButton.Left);

        c.R.FillRect(r, Color.Rgb(0xC0C0C0));
        if (pressed) SunkenBevel(c, r, 2); else RaisedBevel(c, r, 2);

        var face = r.Deflate(4);
        if (pressed) face = face.Offset(1, 1);
        c.R.FillCircle(face.CenterX, face.CenterY, face.W * 0.5f, Color.Rgb(0xFFFF00));
        c.R.DrawCircle(face.CenterX, face.CenterY, face.W * 0.5f, Color.Black, 1);

        float ex = face.W * 0.20f, ey = face.H * 0.16f;

        if (_game == GameState.Lost)
        {
            // Dead: X eyes and a frown.
            foreach (float sx in new[] { -ex, ex })
            {
                c.R.Line(face.CenterX + sx - 2.4f, face.CenterY - ey - 2.4f,
                         face.CenterX + sx + 2.4f, face.CenterY - ey + 2.4f, Color.Black, 1.6f);
                c.R.Line(face.CenterX + sx + 2.4f, face.CenterY - ey - 2.4f,
                         face.CenterX + sx - 2.4f, face.CenterY - ey + 2.4f, Color.Black, 1.6f);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = MathF.PI * (0.2f + i * 0.15f);
                c.R.FillRect(new Rect(face.CenterX - MathF.Cos(a) * face.W * 0.26f - 1,
                                      face.CenterY + face.H * 0.30f - MathF.Sin(a) * face.H * 0.10f, 2, 2), Color.Black);
            }
        }
        else if (_game == GameState.Won)
        {
            // Sunglasses and a grin.
            c.R.FillRect(new Rect(face.CenterX - ex - 4, face.CenterY - ey - 2, 8, 5), Color.Black);
            c.R.FillRect(new Rect(face.CenterX + ex - 4, face.CenterY - ey - 2, 8, 5), Color.Black);
            c.R.FillRect(new Rect(face.CenterX - 4, face.CenterY - ey - 1, 8, 1.5f), Color.Black);
            Smile(c, face, true);
        }
        else if (_pressCol >= 0)
        {
            // Tense "oh" face while a cell is held down.
            c.R.FillCircle(face.CenterX - ex, face.CenterY - ey, 1.7f, Color.Black);
            c.R.FillCircle(face.CenterX + ex, face.CenterY - ey, 1.7f, Color.Black);
            c.R.DrawCircle(face.CenterX, face.CenterY + face.H * 0.18f, face.W * 0.13f, Color.Black, 1.4f);
        }
        else
        {
            c.R.FillCircle(face.CenterX - ex, face.CenterY - ey, 1.7f, Color.Black);
            c.R.FillCircle(face.CenterX + ex, face.CenterY - ey, 1.7f, Color.Black);
            Smile(c, face, true);
        }

        if (c.Clicked(r)) _smileyPressed = true;
        if (_smileyPressed && c.In.Released(MouseButton.Left))
        {
            _smileyPressed = false;
            if (hover)
            {
                NewGame(_cols, _rows, _mineCount, _preset);
                c.SoundAt(Sfx.Click, r, 0.6f);
            }
        }
    }

    static void Smile(UiContext c, Rect face, bool up)
    {
        for (int i = 0; i <= 8; i++)
        {
            float t = i / 8f;
            float a = MathF.PI * (0.15f + t * 0.7f);
            float x = face.CenterX - MathF.Cos(a) * face.W * 0.28f;
            float y = face.CenterY + face.H * 0.12f + (up ? MathF.Sin(a) : -MathF.Sin(a)) * face.H * 0.16f;
            c.R.FillRect(new Rect(x - 1, y - 1, 2, 2), Color.Black);
        }
    }

    static readonly Color[] NumberColors =
    {
        Color.Rgb(0x000000), Color.Rgb(0x0000FF), Color.Rgb(0x008000), Color.Rgb(0xFF0000),
        Color.Rgb(0x000080), Color.Rgb(0x800000), Color.Rgb(0x008080), Color.Rgb(0x000000),
        Color.Rgb(0x808080),
    };

    void DrawCell(UiContext c, Rect r, int x, int y)
    {
        var st = _state[x, y];
        bool showPressed = _pressCol >= 0 &&
            (_chording
                ? Math.Abs(x - _pressCol) <= 1 && Math.Abs(y - _pressRow) <= 1
                : x == _pressCol && y == _pressRow)
            && st is CellState.Hidden or CellState.Question;

        if (st == CellState.Revealed || showPressed)
        {
            c.R.FillRect(r, Color.Rgb(0xC0C0C0));
            c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Color.Rgb(0x808080));
            c.R.FillRect(new Rect(r.X, r.Y, 1, r.H), Color.Rgb(0x808080));
        }
        else
        {
            c.R.FillRect(r, Color.Rgb(0xC0C0C0));
            RaisedBevel(c, r, 2);
        }

        if (st == CellState.Revealed)
        {
            if (_mine[x, y])
            {
                if (_game == GameState.Lost) c.R.FillRect(r, Color.Rgb(0xFF0000));
                DrawMine(c, r);
            }
            else if (_adjacent[x, y] > 0)
            {
                string s = _adjacent[x, y].ToString();
                float w = c.F.UiBold.Measure(s);
                c.F.UiBold.Draw(c.R, s, r.CenterX - w * 0.5f, r.CenterY - c.F.UiBold.Height * 0.5f,
                                NumberColors[_adjacent[x, y]]);
            }
        }
        else if (st == CellState.Flagged)
        {
            DrawFlag(c, r);
            // After a loss, a flag on a safe cell is crossed out.
            if (_game == GameState.Lost && !_mine[x, y])
            {
                c.R.Line(r.X + 2, r.Y + 2, r.Right - 2, r.Bottom - 2, Color.Rgb(0xFF0000), 1.6f);
                c.R.Line(r.Right - 2, r.Y + 2, r.X + 2, r.Bottom - 2, Color.Rgb(0xFF0000), 1.6f);
            }
        }
        else if (st == CellState.Question)
        {
            float w = c.F.UiBold.Measure("?");
            c.F.UiBold.Draw(c.R, "?", r.CenterX - w * 0.5f, r.CenterY - c.F.UiBold.Height * 0.5f, Color.Black);
        }
    }

    static void DrawMine(UiContext c, Rect r)
    {
        float cx = r.CenterX, cy = r.CenterY, rad = r.W * 0.26f;
        c.R.FillRect(new Rect(cx - 1, cy - rad * 1.7f, 2, rad * 3.4f), Color.Black);
        c.R.FillRect(new Rect(cx - rad * 1.7f, cy - 1, rad * 3.4f, 2), Color.Black);
        c.R.Line(cx - rad * 1.25f, cy - rad * 1.25f, cx + rad * 1.25f, cy + rad * 1.25f, Color.Black, 2);
        c.R.Line(cx + rad * 1.25f, cy - rad * 1.25f, cx - rad * 1.25f, cy + rad * 1.25f, Color.Black, 2);
        c.R.FillCircle(cx, cy, rad, Color.Black);
        c.R.FillRect(new Rect(cx - rad * 0.55f, cy - rad * 0.55f, rad * 0.4f, rad * 0.4f), Color.White);
    }

    static void DrawFlag(UiContext c, Rect r)
    {
        float x = r.X + r.W * 0.5f, top = r.Y + r.H * 0.22f;
        c.R.FillTriangle(x, top, x, top + r.H * 0.28f, x - r.W * 0.28f, top + r.H * 0.14f, Color.Rgb(0xFF0000));
        c.R.FillRect(new Rect(x - 1, top, 1.6f, r.H * 0.45f), Color.Black);
        c.R.FillRect(new Rect(x - r.W * 0.22f, r.Bottom - r.H * 0.28f, r.W * 0.44f, 2), Color.Black);
        c.R.FillRect(new Rect(x - r.W * 0.14f, r.Bottom - r.H * 0.36f, r.W * 0.28f, 2), Color.Black);
    }

    // ---- interaction -----------------------------------------------------

    void HandleGrid(UiContext c, Rect cells)
    {
        if (_game is GameState.Won or GameState.Lost)
        {
            _pressCol = _pressRow = -1;
            return;
        }

        bool inside = c.Hovering(cells);
        int cx = inside ? (int)((c.MouseX - cells.X) / CellSize) : -1;
        int cy = inside ? (int)((c.MouseY - cells.Y) / CellSize) : -1;
        bool valid = cx >= 0 && cx < _cols && cy >= 0 && cy < _rows;

        bool left = c.In.IsDown(MouseButton.Left);
        bool right = c.In.IsDown(MouseButton.Right);

        // Track the pressed cell for the sunken preview and the tense smiley.
        if (valid && (left || (left && right)))
        {
            _pressCol = cx; _pressRow = cy;
            _chording = left && right;
            c.MouseHandled = true;
        }
        else if (!left)
        {
            _pressCol = _pressRow = -1;
            _chording = false;
        }

        if (!valid) return;

        if (c.RightClicked(cells) && !left)
        {
            CycleFlag(c, cx, cy);
            return;
        }

        // Left release commits: reveal, or chord when both buttons were down.
        if (c.In.Released(MouseButton.Left) && _pressCol == cx && _pressRow == cy)
        {
            if (_chording || right) Chord(c, cx, cy);
            else Reveal(c, cx, cy);
            c.MouseHandled = true;
        }
        else if (c.Clicked(cells))
        {
            _pressCol = cx; _pressRow = cy;
        }
    }

    void CycleFlag(UiContext c, int x, int y)
    {
        var st = _state[x, y];
        if (st == CellState.Revealed) return;

        _state[x, y] = st switch
        {
            CellState.Hidden => CellState.Flagged,
            CellState.Flagged => _questionMarks ? CellState.Question : CellState.Hidden,
            _ => CellState.Hidden,
        };
        c.Sound(Sfx.MineFlag, 0.5f, _state[x, y] == CellState.Flagged ? 1f : 0.85f);
    }

    void Reveal(UiContext c, int x, int y)
    {
        if (_state[x, y] != CellState.Hidden && _state[x, y] != CellState.Question) return;

        if (_game == GameState.Ready)
        {
            PlaceMines(x, y);
            _game = GameState.Playing;
            _startTime = c.Time;
        }

        if (_mine[x, y])
        {
            _state[x, y] = CellState.Revealed;
            LoseGame(c);
            return;
        }

        Flood(x, y);
        c.Sound(Sfx.MineReveal, 0.45f);
        CheckWin(c);
    }

    /// <summary>Iterative flood fill from an empty cell; a stack keeps deep
    /// cascades on a 30×16 board from recursing hundreds deep.</summary>
    void Flood(int sx, int sy)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((sx, sy));

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            if (x < 0 || x >= _cols || y < 0 || y >= _rows) continue;
            if (_state[x, y] == CellState.Revealed || _state[x, y] == CellState.Flagged) continue;
            if (_mine[x, y]) continue;

            _state[x, y] = CellState.Revealed;
            if (_adjacent[x, y] != 0) continue;

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) stack.Push((x + dx, y + dy));
        }
    }

    /// <summary>Clicking a satisfied number reveals its remaining neighbours.</summary>
    void Chord(UiContext c, int x, int y)
    {
        if (_state[x, y] != CellState.Revealed || _adjacent[x, y] == 0) return;

        int flags = 0;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= _cols || ny < 0 || ny >= _rows) continue;
                if (_state[nx, ny] == CellState.Flagged) flags++;
            }
        if (flags != _adjacent[x, y]) return;

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= _cols || ny < 0 || ny >= _rows) continue;
                if (_state[nx, ny] is CellState.Flagged or CellState.Revealed) continue;
                if (_mine[nx, ny]) { _state[nx, ny] = CellState.Revealed; LoseGame(c); return; }
                Flood(nx, ny);
            }
        c.Sound(Sfx.MineReveal, 0.5f);
        CheckWin(c);
    }

    void LoseGame(UiContext c)
    {
        _game = GameState.Lost;
        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
                if (_mine[x, y] && _state[x, y] != CellState.Flagged)
                    _state[x, y] = CellState.Revealed;
        c.Sound(Sfx.MineBoom, 0.95f);
    }

    void CheckWin(UiContext c)
    {
        if (HiddenCount() != _mineCount) return;

        _game = GameState.Won;
        for (int x = 0; x < _cols; x++)
            for (int y = 0; y < _rows; y++)
                if (_mine[x, y]) _state[x, y] = CellState.Flagged;

        c.Sound(Sfx.MineWin, 0.9f);

        if (_elapsed < _best[_preset])
        {
            _best[_preset] = _elapsed;
            Shell.MessageBox(c, L.T("mine.new_best_time"),
                L.F("mine.you_have_the_fastest_time_0_sec", _elapsed),
                MsgButtons.Ok, IconId.Star, null, Sfx.MineWin);
        }
    }
}
