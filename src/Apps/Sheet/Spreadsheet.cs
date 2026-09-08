using System.Globalization;
using System.Text;
using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>EXCE1 — the spreadsheet part 1 spends a minute in, loaded with the
/// same investment exercise: cash flows, NPV, payback period and MIRR.
///
/// Formulas are real. A small recursive-descent parser handles arithmetic, cell
/// and range references, and a set of functions, with cycle detection so a
/// self-referencing cell reports #ЦИКЛ! instead of hanging.
///
/// The face is the one Office wore in 2013, which is the flattest that suite
/// ever looked: the window chrome is a single block of the program's own green,
/// the menu bar is gone and the tabs are set in small capitals with ФАЙЛ as a
/// solid green rectangle at the head of them, the ribbon is white with its group
/// names in grey underneath, and nothing anywhere has a bevel on it. The grid
/// went with it — hairline rules, flat headers, and the selection drawn as a
/// green box with a green square at its corner.</summary>
public sealed class SpreadsheetWindow : OsWindow
{
    const int Cols = 26, Rows = 200;

    sealed class Cell
    {
        public string Raw = "";
        public bool Bold;
        public int Align;          // 0 auto, 1 left, 2 centre, 3 right
    }

    readonly Dictionary<int, Cell> _cells = new();
    readonly Dictionary<int, double> _cache = new();
    readonly HashSet<int> _evaluating = new();

    int _curCol, _curRow;
    int _selCol, _selRow;
    float _scrollX, _scrollY;
    string _editBuffer;
    bool _editing;
    int _sheet;

    readonly float[] _colWidth = new float[Cols];
    const float RowHeight = 18;
    const float HeaderW = 42;
    const float HeaderH = 18;

    VNode _file;

    public override string Title
        => (_file?.Name ?? L.T("sheet.book1")) + " - " + L.T("sheet.miminus_sheet");

    public override float MinWidth => 560;
    public override float MinHeight => 380;

    /// <summary>The green Office painted the spreadsheet's whole window in.</summary>
    static readonly Color Green = Color.Rgb(0x217346);
    static readonly Color GreenDark = Color.Rgb(0x1A5C38);
    static readonly Color GreenPale = Color.Rgb(0xE6F2EC);
    static readonly Color Rule = Color.Rgb(0xD4D4D4);
    static readonly Color HeaderFace = Color.Rgb(0xF3F3F3);
    static readonly Color Ink = Color.Rgb(0x2B2B2B);
    static readonly Color InkDim = Color.Rgb(0x7A7A7A);

    public override Color? CaptionTint => Green;

    public SpreadsheetWindow(VNode file)
    {
        _file = file;
        Icon = IconId.Spreadsheet;
        Bounds = new Rect(0, 0, 940, 620);
        for (int i = 0; i < Cols; i++) _colWidth[i] = 74;
        _colWidth[0] = 150;
        LoadSample();
    }

    static int Key(int col, int row) => row * Cols + col;

    Cell At(int col, int row, bool create = false)
    {
        int k = Key(col, row);
        if (_cells.TryGetValue(k, out var c)) return c;
        if (!create) return null;
        c = new Cell();
        _cells[k] = c;
        return c;
    }

    void Set(int col, int row, string raw, bool bold = false, int align = 0)
    {
        var c = At(col, row, true);
        c.Raw = raw;
        c.Bold = bold;
        c.Align = align;
        _cache.Clear();
    }

    /// <summary>Reproduces the worksheet visible in part 1: «Гревцов Михаил 71 гр»,
    /// «Задача 2», a project horizon and rate, a cash-flow table, then NPV,
    /// payback, profitability and MIRR.</summary>
    void LoadSample()
    {
        Set(0, 2, L.T("sheet.mikhail_grevtsov_group_71"), bold: true);
        Set(0, 4, L.T("sheet.problem_2"), bold: true);

        Set(0, 6, L.T("sheet.project_horizon_t"));
        Set(1, 6, "8");
        Set(0, 7, L.T("sheet.interest_rate"));
        Set(1, 7, "0,2");

        // Period header row.
        Set(0, 9, L.T("sheet.period"));
        for (int i = 0; i <= 8; i++) Set(1 + i, 9, i.ToString(), align: 3);

        // Cash flows: an outlay followed by returns.
        string[] flows = { "-300", "150", "-100", "250", "230", "250", "-170", "250", "230" };
        Set(0, 10, "Ri");
        for (int i = 0; i < flows.Length; i++) Set(1 + i, 10, flows[i], align: 3);

        // Discount factors and discounted flows.
        Set(0, 11, "(1+n)i");
        for (int i = 0; i < flows.Length; i++)
            Set(1 + i, 11, $"=1/(1+$B$8)^{i}", align: 3);

        Set(0, 12, "Ri*(1+n)i");
        for (int i = 0; i < flows.Length; i++)
            Set(1 + i, 12, $"={ColName(1 + i)}11*{ColName(1 + i)}12", align: 3);

        Set(0, 13, L.T("sheet.cumulative"));
        Set(1, 13, "=B13");
        for (int i = 1; i < flows.Length; i++)
            Set(1 + i, 13, $"={ColName(i)}14+{ColName(1 + i)}13", align: 3);

        // Results block.
        Set(1, 16, "NPV =", align: 3);
        Set(2, 16, $"=СУММ(B13:{ColName(flows.Length)}13)", align: 3);

        Set(1, 17, L.T("sheet.payback_period"), align: 3);
        Set(2, 17, "4", align: 3);

        Set(1, 18, L.T("sheet.profitability_index"), align: 3);
        Set(2, 18, "=C17/300+1", align: 3);

        Set(1, 19, "MIRR =", align: 3);
        Set(2, 19, "=КОРЕНЬ(C19)+0,05", align: 3);

        _curCol = 1; _curRow = 16;
        _selCol = 1; _selRow = 16;
    }

    static string ColName(int col)
    {
        string s = "";
        col++;
        while (col > 0)
        {
            int rem = (col - 1) % 26;
            s = (char)('A' + rem) + s;
            col = (col - 1) / 26;
        }
        return s;
    }

    static bool TryParseRef(string s, out int col, out int row)
    {
        col = row = -1;
        s = s.Replace("$", "").Trim().ToUpperInvariant();
        int i = 0;
        int c = 0;
        while (i < s.Length && s[i] >= 'A' && s[i] <= 'Z')
        {
            c = c * 26 + (s[i] - 'A' + 1);
            i++;
        }
        if (i == 0 || i >= s.Length) return false;
        if (!int.TryParse(s[i..], out int r)) return false;
        col = c - 1;
        row = r - 1;
        return col >= 0 && col < Cols && row >= 0 && row < Rows;
    }

    // ---- evaluation ------------------------------------------------------

    /// <summary>Numeric value of a cell, evaluating its formula if needed.</summary>
    double Value(int col, int row)
    {
        int k = Key(col, row);
        if (_cache.TryGetValue(k, out double v)) return v;

        var cell = At(col, row);
        if (cell == null || cell.Raw.Length == 0) return 0;

        if (!cell.Raw.StartsWith('='))
            return double.TryParse(cell.Raw.Replace(',', '.'), NumberStyles.Any,
                                   CultureInfo.InvariantCulture, out double n) ? n : 0;

        if (!_evaluating.Add(k)) return double.NaN;   // cycle
        try
        {
            var p = new Parser(cell.Raw[1..], this);
            double r = p.ParseExpression();
            _cache[k] = r;
            return r;
        }
        catch { return double.NaN; }
        finally { _evaluating.Remove(k); }
    }

    /// <summary>What the cell shows: formulas render their result, text renders itself.</summary>
    string Display(int col, int row)
    {
        var cell = At(col, row);
        if (cell == null || cell.Raw.Length == 0) return "";

        if (cell.Raw.StartsWith('='))
        {
            double v = Value(col, row);
            if (double.IsNaN(v)) return _evaluating.Count > 0 ? "#ЦИКЛ!" : "#ЗНАЧ!";
            if (double.IsInfinity(v)) return "#ДЕЛ/0!";
            return Fmt(v);
        }
        return cell.Raw;
    }

    static string Fmt(double v)
    {
        if (Math.Abs(v) >= 1e11) return v.ToString("G6", CultureInfo.InvariantCulture);
        string s = v.ToString("0.#########", CultureInfo.InvariantCulture);
        return L.IsRu ? s.Replace('.', ',') : s;
    }

    /// <summary>Shortens a formatted number until it fits <paramref name="maxWidth"/>,
    /// giving up with ##### exactly as Excel does.</summary>
    static string FitNumber(string text, Font font, float maxWidth)
    {
        if (maxWidth <= 0 || font.Measure(text) <= maxWidth) return text;

        char sep = L.IsRu ? ',' : '.';
        int dot = text.IndexOf(sep);
        if (dot >= 0)
        {
            int decimals = text.Length - dot - 1;
            for (int keep = decimals - 1; keep >= 0; keep--)
            {
                string candidate = keep == 0 ? text[..dot] : text[..(dot + 1 + keep)];
                if (font.Measure(candidate) <= maxWidth) return candidate;
            }
        }

        // Still too wide even as an integer: try scientific, then give up.
        if (double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double v))
        {
            string sci = v.ToString("0.#E+0", CultureInfo.InvariantCulture);
            if (font.Measure(sci) <= maxWidth) return sci;
        }

        var sb = new StringBuilder();
        while (font.Measure(sb.ToString() + "#") <= maxWidth) sb.Append('#');
        return sb.Length > 0 ? sb.ToString() : "#";
    }

    bool IsNumeric(int col, int row)
    {
        var cell = At(col, row);
        if (cell == null || cell.Raw.Length == 0) return false;
        if (cell.Raw.StartsWith('=')) return true;
        return double.TryParse(cell.Raw.Replace(',', '.'), NumberStyles.Any,
                               CultureInfo.InvariantCulture, out _);
    }

    /// <summary>Recursive-descent formula parser: + - * / ^ %, parentheses, cell
    /// and range references, and the function set below.</summary>
    sealed class Parser
    {
        readonly string _s;
        readonly SpreadsheetWindow _sheet;
        int _i;

        public Parser(string s, SpreadsheetWindow sheet) { _s = s; _sheet = sheet; }

        void Skip() { while (_i < _s.Length && _s[_i] == ' ') _i++; }
        char Peek { get { Skip(); return _i < _s.Length ? _s[_i] : '\0'; } }

        public double ParseExpression()
        {
            double left = ParseTerm();
            while (true)
            {
                char c = Peek;
                if (c == '+') { _i++; left += ParseTerm(); }
                else if (c == '-') { _i++; left -= ParseTerm(); }
                else return left;
            }
        }

        double ParseTerm()
        {
            double left = ParsePower();
            while (true)
            {
                char c = Peek;
                if (c == '*') { _i++; left *= ParsePower(); }
                else if (c == '/') { _i++; double d = ParsePower(); left = d == 0 ? double.PositiveInfinity : left / d; }
                else return left;
            }
        }

        double ParsePower()
        {
            double b = ParseUnary();
            if (Peek == '^') { _i++; return Math.Pow(b, ParsePower()); }
            return b;
        }

        double ParseUnary()
        {
            char c = Peek;
            if (c == '-') { _i++; return -ParseUnary(); }
            if (c == '+') { _i++; return ParseUnary(); }
            return ParseAtom();
        }

        double ParseAtom()
        {
            Skip();
            char c = Peek;

            if (c == '(')
            {
                _i++;
                double v = ParseExpression();
                if (Peek == ')') _i++;
                return v;
            }

            if (char.IsDigit(c) || c == '.' || c == ',')
            {
                int start = _i;
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == ',')) _i++;
                string num = _s[start.._i].Replace(',', '.');
                double.TryParse(num, NumberStyles.Any, CultureInfo.InvariantCulture, out double v);
                if (Peek == '%') { _i++; v /= 100; }
                return v;
            }

            // An identifier: either a function call or a cell/range reference.
            int idStart = _i;
            while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '$' || _s[_i] == '_')) _i++;
            string ident = _s[idStart.._i];
            if (ident.Length == 0) { _i++; return 0; }

            if (Peek == '(') return Function(ident.ToUpperInvariant());

            if (Peek == ':')
            {
                _i++;
                int rs = _i;
                while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '$')) _i++;
                // A bare range outside a function collapses to its sum.
                return SumRange(ident, _s[rs.._i]);
            }

            return TryParseRef(ident, out int col, out int row) ? _sheet.Value(col, row) : 0;
        }

        List<double> Args()
        {
            var args = new List<double>();
            if (Peek == '(') _i++;
            if (Peek == ')') { _i++; return args; }

            while (true)
            {
                Skip();
                // Detect a range argument before falling back to an expression.
                int save = _i;
                int idStart = _i;
                while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '$')) _i++;
                string first = _s[idStart.._i];
                if (first.Length > 0 && Peek == ':')
                {
                    _i++;
                    int rs = _i;
                    while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] == '$')) _i++;
                    args.AddRange(RangeValues(first, _s[rs.._i]));
                }
                else
                {
                    _i = save;
                    args.Add(ParseExpression());
                }

                if (Peek == ';' || Peek == ',') { _i++; continue; }
                if (Peek == ')') { _i++; }
                break;
            }
            return args;
        }

        IEnumerable<double> RangeValues(string a, string b)
        {
            if (!TryParseRef(a, out int c0, out int r0) || !TryParseRef(b, out int c1, out int r1))
                yield break;
            if (c0 > c1) (c0, c1) = (c1, c0);
            if (r0 > r1) (r0, r1) = (r1, r0);
            for (int r = r0; r <= r1; r++)
                for (int cc = c0; cc <= c1; cc++)
                    yield return _sheet.Value(cc, r);
        }

        double SumRange(string a, string b) => RangeValues(a, b).Sum();

        double Function(string name)
        {
            var a = Args();
            switch (name)
            {
                case "СУММ" or "SUM": return a.Sum();
                case "СРЗНАЧ" or "AVERAGE": return a.Count == 0 ? 0 : a.Average();
                case "МИН" or "MIN": return a.Count == 0 ? 0 : a.Min();
                case "МАКС" or "MAX": return a.Count == 0 ? 0 : a.Max();
                case "СЧЁТ" or "СЧЕТ" or "COUNT": return a.Count;
                case "ABS": return a.Count > 0 ? Math.Abs(a[0]) : 0;
                case "КОРЕНЬ" or "SQRT": return a.Count > 0 ? Math.Sqrt(Math.Abs(a[0])) : 0;
                case "СТЕПЕНЬ" or "POWER": return a.Count > 1 ? Math.Pow(a[0], a[1]) : 0;
                case "ОКРУГЛ" or "ROUND":
                    return a.Count > 1 ? Math.Round(a[0], (int)Math.Clamp(a[1], 0, 15)) : Math.Round(a.FirstOrDefault());
                case "LN": return a.Count > 0 && a[0] > 0 ? Math.Log(a[0]) : double.NaN;
                case "LOG10": return a.Count > 0 && a[0] > 0 ? Math.Log10(a[0]) : double.NaN;
                case "EXP": return a.Count > 0 ? Math.Exp(a[0]) : 1;
                case "ПИ" or "PI": return Math.PI;
                case "ЕСЛИ" or "IF": return a.Count > 2 ? (a[0] != 0 ? a[1] : a[2]) : 0;
                case "ЧПС" or "NPV":
                {
                    // rate, then the flows
                    if (a.Count < 2) return 0;
                    double rate = a[0], sum = 0;
                    for (int i = 1; i < a.Count; i++) sum += a[i] / Math.Pow(1 + rate, i);
                    return sum;
                }
                default: return 0;
            }
        }
    }

    // ---- ФАЙЛ ------------------------------------------------------------
    //
    // 2013 replaced the menu bar with one green rectangle at the head of the
    // tabs; pressing it turned the whole window into a page of commands. There
    // is not enough in this spreadsheet to fill a page, so it drops a list
    // instead — the same commands, in the place they moved to.

    List<MenuItem> FileMenu(UiContext c) => new()
    {
        MenuItem.Of(L.T("sheet.new"), () => { _cells.Clear(); _cache.Clear(); },
                    IconId.Spreadsheet),
        MenuItem.Of(L.T("sheet.save"), () => Save(c), IconId.DriveHdd, "Ctrl+S"),
        MenuItem.Sep(),
        MenuItem.Of(L.T("sheet.about"), () =>
            Shell.MessageBox(c, L.T("sheet.miminus_sheet"), L.T("sheet.about_exce1"),
                MsgButtons.Ok, IconId.Spreadsheet, null, Sfx.Info), IconId.DlgInfo),
        MenuItem.Of(L.T("sheet.functions"), () =>
            Shell.MessageBox(c, L.T("sheet.functions"), L.T("sheet.functions_help"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info), IconId.Help),
        MenuItem.Sep(),
        MenuItem.Of(L.T("sheet.exit"), Close),
    };

    void Save(UiContext c)
    {
        if (_file != null) _file.Modified = Shell.Now;
        c.Sound(Sfx.Click, 0.6f);
    }

    void AutoSum()
    {
        int r0 = 0;
        for (int r = _curRow - 1; r >= 0 && IsNumeric(_curCol, r); r--) r0 = r;
        if (r0 < _curRow)
            Set(_curCol, _curRow, $"=СУММ({ColName(_curCol)}{r0 + 1}:{ColName(_curCol)}{_curRow})");
    }

    void SumRangeBelow()
    {
        if (_selRow == _curRow && _selCol == _curCol) return;
        int c0 = Math.Min(_curCol, _selCol), c1 = Math.Max(_curCol, _selCol);
        int r0 = Math.Min(_curRow, _selRow), r1 = Math.Max(_curRow, _selRow);
        Set(c1, r1 + 1, $"=СУММ({ColName(c0)}{r0 + 1}:{ColName(c1)}{r1 + 1})");
    }

    UiContext _ctx;

    // ---- rendering -------------------------------------------------------

    /// <summary>Which ribbon tab is out, and whether the ribbon is rolled up.
    /// ФАЙЛ is not a tab: it is a button that drops a list.</summary>
    int _tab;
    bool _ribbonUp;

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        c.R.FillRect(client, Color.White);

        var area = client;
        DrawTabStrip(c, area.CutTop(28));
        if (!_ribbonUp) DrawRibbon(c, area.CutTop(84));
        DrawFormulaBar(c, area.CutTop(26));

        var status = area.CutBottom(22);
        var tabs = area.CutBottom(24);

        DrawGrid(c, area);
        DrawSheetTabs(c, tabs);
        DrawStatusBar(c, status);
    }

    // ---- the tab strip ----------------------------------------------------

    static readonly string[] TabKeys =
    {
        "sheet.tab_home", "sheet.tab_insert", "sheet.tab_formulas", "sheet.tab_view",
    };

    void DrawTabStrip(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.White);
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Rule);

        // ФАЙЛ: a solid block of the program's colour, which is the one thing
        // everybody remembers about that ribbon.
        string fileText = L.T("sheet.tab_file");
        var file = new Rect(bar.X, bar.Y, c.F.Small.Measure(fileText) + 26, bar.H - 1);
        bool fileHot = c.Hovering(file);
        c.R.FillRect(file, fileHot ? GreenDark : Green);
        c.F.Small.DrawCentered(c.R, fileText, file, Color.White);
        if (c.Clicked(file))
            Shell.Menus.Open(FileMenu(c), file.X, file.Bottom, this, c, 170);

        float x = file.Right + 6;
        for (int i = 0; i < TabKeys.Length; i++)
        {
            string label = L.T(TabKeys[i]);
            var r = new Rect(x, bar.Y, c.F.Small.Measure(label) + 24, bar.H - 1);
            bool sel = i == _tab;
            bool hot = c.Hovering(r);

            if (sel)
            {
                // The tab that is out is white with the page under it, joined
                // by the gap it leaves in the rule.
                c.R.FillRect(r, Color.White);
                c.R.FillRect(new Rect(r.X, r.Y, 1, r.H), Rule);
                c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), Rule);
                c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Rule);
                c.R.FillRect(new Rect(r.X + 1, r.Bottom, r.W - 2, 1), Color.White);
            }
            else if (hot) c.R.FillRect(r, Color.Rgb(0xF2F2F2));

            c.F.Small.DrawCentered(c.R, label, r, sel ? Ink : Green);

            if (c.Clicked(r))
            {
                if (sel) _ribbonUp = !_ribbonUp;
                else { _tab = i; _ribbonUp = false; }
                c.SoundAt(Sfx.Tick, r, 0.3f);
            }
            if (c.DoubleClicked(r)) _ribbonUp = !_ribbonUp;

            x = r.Right + 2;
        }

        // The account name in the corner, which 2013 put there and which here
        // is the name the machine was registered to.
        string who = Shell.UserName;
        var whoR = new Rect(bar.Right - c.F.Small.Measure(who) - 12, bar.Y,
                            c.F.Small.Measure(who) + 8, bar.H - 1);
        if (whoR.X > x + 20)
            c.F.Small.DrawCentered(c.R, who, whoR, InkDim);
    }

    // ---- the ribbon -------------------------------------------------------

    void DrawRibbon(UiContext c, Rect ribbon)
    {
        c.R.FillRect(ribbon, Color.White);
        c.R.FillRect(new Rect(ribbon.X, ribbon.Bottom - 1, ribbon.W, 1), Rule);

        var area = ribbon.Deflate(6, 4, 6, 16);
        switch (_tab)
        {
            case 1: DrawInsertTab(c, ribbon, area); break;
            case 2: DrawFormulasTab(c, ribbon, area); break;
            case 3: DrawViewTab(c, ribbon, area); break;
            default: DrawHomeTab(c, ribbon, area); break;
        }

        // The chevron that rolls the ribbon away, in the corner it lived in.
        var up = new Rect(ribbon.Right - 22, ribbon.Y + 4, 16, 16);
        if (c.Hovering(up)) c.R.FillRect(up, Color.Rgb(0xF0F0F0));
        W.Arrow(c, up, 0, InkDim, 3.4f);
        c.Tooltip(up, L.T("sheet.collapse_ribbon"));
        if (c.Clicked(up)) _ribbonUp = true;
    }

    /// <summary>«ГЛАВНАЯ»: the clipboard, the face of the text and where it
    /// sits in the cell.</summary>
    void DrawHomeTab(UiContext c, Rect ribbon, Rect area)
    {
        var cell = At(_curCol, _curRow, true);

        var group = Group(c, ribbon, ref area, 158, "sheet.group_clipboard");
        if (Big(c, group.CutLeft(72), "sheet.paste", IconId.TextFile))
            Set(_curCol, _curRow, Clipboard.GetText().Split('\n')[0].Trim());
        if (Big(c, group.CutLeft(84), "sheet.copy", IconId.ImageFile))
            Clipboard.SetText(Display(_curCol, _curRow));

        group = Group(c, ribbon, ref area, 116, "sheet.group_font");
        if (Letter(c, group.CutLeft(30), "Ж", cell.Bold, true)) cell.Bold = !cell.Bold;
        Letter(c, group.CutLeft(30), "К", false, false);
        Letter(c, group.CutLeft(30), "Ч", false, false);

        group = Group(c, ribbon, ref area, 116, "sheet.group_align");
        for (int i = 1; i <= 3; i++)
        {
            var r = group.CutLeft(30);
            if (AlignButton(c, r, i, cell.Align == i)) cell.Align = i;
        }

        group = Group(c, ribbon, ref area, 76, "sheet.group_cells");
        if (Big(c, group.CutLeft(70), "sheet.clear", IconId.RecycleBin))
        {
            for (int col = Math.Min(_curCol, _selCol); col <= Math.Max(_curCol, _selCol); col++)
                for (int row = Math.Min(_curRow, _selRow); row <= Math.Max(_curRow, _selRow); row++)
                    _cells.Remove(Key(col, row));
            _cache.Clear();
        }

        group = Group(c, ribbon, ref area, 84, "sheet.group_editing");
        if (Big(c, group.CutLeft(78), "sheet.autosum", IconId.Star)) AutoSum();
    }

    void DrawInsertTab(UiContext c, Rect ribbon, Rect area)
    {
        var group = Group(c, ribbon, ref area, 168, "sheet.group_functions");
        if (Big(c, group.CutLeft(78), "sheet.autosum", IconId.Star)) AutoSum();
        if (Big(c, group.CutLeft(84), "sheet.sum_of_range", IconId.Spreadsheet)) SumRangeBelow();
    }

    void DrawFormulasTab(UiContext c, Rect ribbon, Rect area)
    {
        var group = Group(c, ribbon, ref area, 168, "sheet.group_functions");
        if (Big(c, group.CutLeft(78), "sheet.autosum", IconId.Star)) AutoSum();
        if (Big(c, group.CutLeft(84), "sheet.functions", IconId.Help))
            Shell.MessageBox(c, L.T("sheet.functions"), L.T("sheet.functions_help"),
                             MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

        // What the current cell is really made of, which is the one thing this
        // tab can say that the others cannot.
        group = Group(c, ribbon, ref area, MathF.Max(200, area.W), "sheet.group_current");
        string raw = At(_curCol, _curRow)?.Raw ?? "";
        c.F.Small.Draw(c.R, $"{ColName(_curCol)}{_curRow + 1}", group.X + 2, group.Y + 6, InkDim);
        c.R.PushClip(group);
        c.F.Ui.Draw(c.R, raw.Length > 0 ? raw : "—", group.X + 2, group.Y + 22, Ink);
        c.R.PopClip();
    }

    void DrawViewTab(UiContext c, Rect ribbon, Rect area)
    {
        var group = Group(c, ribbon, ref area, 176, "sheet.group_show");
        if (Big(c, group.CutLeft(84), "sheet.collapse_ribbon", IconId.Display)) _ribbonUp = true;
        if (Big(c, group.CutLeft(84), "sheet.about", IconId.DlgInfo))
            Shell.MessageBox(c, L.T("sheet.miminus_sheet"), L.T("sheet.about_exce1"),
                             MsgButtons.Ok, IconId.Spreadsheet, null, Sfx.Info);
    }

    /// <summary>One ribbon group: the commands, then the group's name in grey
    /// underneath and a hairline between it and the next.</summary>
    Rect Group(UiContext c, Rect ribbon, ref Rect area, float width, string titleKey)
    {
        width = MathF.Min(width, MathF.Max(0, area.W));
        var group = area.CutLeft(width);
        area.CutLeft(6);

        string title = L.T(titleKey);
        c.F.Small.DrawCentered(c.R, title,
            new Rect(group.X, ribbon.Bottom - 16, group.W, 14), InkDim);

        if (area.W > 4)
            c.R.FillRect(new Rect(group.Right + 2, ribbon.Y + 6, 1, ribbon.H - 24), Rule);

        return group;
    }

    /// <summary>A ribbon command: the icon over its name, no border until the
    /// pointer is on it.</summary>
    bool Big(UiContext c, Rect r, string key, IconId icon, bool enabled = true)
    {
        bool hot = enabled && c.Hovering(r);
        bool held = hot && c.In.IsDown(MouseButton.Left);
        if (held) c.R.FillRect(r, Color.Rgb(0xD8E9E0));
        else if (hot) c.R.FillRect(r, GreenPale);

        Icons.Draw(c.R, icon, new Rect(r.CenterX - 12, r.Y + 2, 24, 24));

        c.R.PushClip(r);
        var lines = c.F.Small.Wrap(L.T(key), r.W - 2);
        float y = r.Y + 28;
        foreach (string line in lines.Take(2))
        {
            c.F.Small.DrawCentered(c.R, line, new Rect(r.X, y, r.W, c.F.Small.Height),
                                   enabled ? Ink : InkDim);
            y += c.F.Small.Height + 1;
        }
        c.R.PopClip();

        bool clicked = enabled && c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.4f);
        return clicked;
    }

    /// <summary>Ж, К and Ч — the three letters that stand for the three faces
    /// in a Russian office program.</summary>
    bool Letter(UiContext c, Rect r, string glyph, bool on, bool enabled)
    {
        var box = new Rect(r.X + 2, r.Y + 2, 26, 26);
        bool hot = enabled && c.Hovering(box);

        if (on) { c.R.FillRect(box, GreenPale); c.R.DrawRect(box, Green); }
        else if (hot) c.R.FillRect(box, GreenPale);

        var font = glyph == "Ж" ? c.F.UiBold : c.F.Ui;
        font.DrawCentered(c.R, glyph, box, enabled ? Ink : InkDim);
        if (glyph == "Ч")
            c.R.FillRect(new Rect(box.CenterX - 5, box.CenterY + 7, 10, 1), enabled ? Ink : InkDim);

        bool clicked = enabled && c.Clicked(box);
        if (clicked) c.SoundAt(Sfx.Click, box, 0.4f);
        return clicked;
    }

    bool AlignButton(UiContext c, Rect r, int align, bool on)
    {
        var box = new Rect(r.X + 2, r.Y + 2, 26, 26);
        bool hot = c.Hovering(box);

        if (on) { c.R.FillRect(box, GreenPale); c.R.DrawRect(box, Green); }
        else if (hot) c.R.FillRect(box, GreenPale);

        for (int line = 0; line < 4; line++)
        {
            float lw = line % 2 == 0 ? 14 : 9;
            float lx = align == 1 ? box.X + 6 : align == 3 ? box.Right - 6 - lw
                                                           : box.CenterX - lw * 0.5f;
            c.R.FillRect(new Rect(lx, box.Y + 7 + line * 3.5f, lw, 1.4f), Ink);
        }

        bool clicked = c.Clicked(box);
        if (clicked) c.SoundAt(Sfx.Click, box, 0.4f);
        return clicked;
    }

    // ---- the formula bar --------------------------------------------------

    void DrawFormulaBar(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.White);
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), Rule);

        var nameBox = new Rect(bar.X + 4, bar.Y + 2, 92, bar.H - 6);
        c.R.FillRect(nameBox, Color.White);
        c.R.DrawRect(nameBox, Rule);
        c.F.Ui.DrawCentered(c.R, $"{ColName(_curCol)}{_curRow + 1}", nameBox, Ink);
        W.Arrow(c, new Rect(nameBox.Right - 14, nameBox.Y, 12, nameBox.H), 2, InkDim, 3f);

        var fx = new Rect(nameBox.Right + 6, bar.Y + 2, 24, bar.H - 6);
        c.F.UiBold.DrawCentered(c.R, "fx", fx, Green);
        c.Tooltip(fx, L.T("sheet.functions"));
        if (c.Clicked(fx))
            Shell.MessageBox(c, L.T("sheet.functions"), L.T("sheet.functions_help"),
                             MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

        var field = new Rect(fx.Right + 4, bar.Y + 2, bar.Right - fx.Right - 10, bar.H - 6);
        c.R.FillRect(field, Color.White);
        c.R.DrawRect(field, _editing ? Green : Rule);

        string shown = _editing ? _editBuffer : (At(_curCol, _curRow)?.Raw ?? "");
        c.R.PushClip(field.Deflate(3, 0, 3, 0));
        c.F.Ui.Draw(c.R, shown, field.X + 5, field.CenterY - c.F.Ui.Height * 0.5f, Ink);
        if (_editing && (c.Time % 1.06) < 0.53)
            c.R.FillRect(new Rect(field.X + 5 + c.F.Ui.Measure(shown), field.Y + 4, 1.4f,
                                  field.H - 8), Ink);
        c.R.PopClip();

        if (c.Hovering(field)) c.Cursor = CursorShape.Text;
        if (c.Clicked(field)) BeginEdit(keepExisting: true);
    }

    // ---- the grid ---------------------------------------------------------

    void DrawGrid(UiContext c, Rect area)
    {
        c.R.FillRect(area, Color.White);

        var corner = new Rect(area.X, area.Y, HeaderW, HeaderH);
        var colHead = new Rect(area.X + HeaderW, area.Y, area.W - HeaderW - W.ScrollBarSize, HeaderH);
        var rowHead = new Rect(area.X, area.Y + HeaderH, HeaderW, area.H - HeaderH - W.ScrollBarSize);
        var cellsArea = new Rect(colHead.X, rowHead.Y, colHead.W, rowHead.H);

        float totalW = 0;
        for (int i = 0; i < Cols; i++) totalW += _colWidth[i];
        float totalH = Rows * RowHeight;

        _scrollX = W.ScrollBarH(c, Id + ".hs",
            new Rect(cellsArea.X, area.Bottom - W.ScrollBarSize, cellsArea.W, W.ScrollBarSize),
            _scrollX, totalW, cellsArea.W);
        _scrollY = W.ScrollBarV(c, Id + ".vs",
            new Rect(area.Right - W.ScrollBarSize, cellsArea.Y, W.ScrollBarSize, cellsArea.H),
            _scrollY, totalH, cellsArea.H);

        if (c.Hovering(cellsArea) && c.In.WheelDelta != 0)
        {
            _scrollY = Math.Clamp(_scrollY - c.In.WheelDelta * RowHeight * 3, 0,
                                  MathF.Max(0, totalH - cellsArea.H));
            c.MouseHandled = true;
        }

        int firstRow = (int)(_scrollY / RowHeight);
        int lastRow = Math.Min(Rows - 1, firstRow + (int)(cellsArea.H / RowHeight) + 1);

        int firstCol = 0;
        float acc = 0;
        while (firstCol < Cols - 1 && acc + _colWidth[firstCol] < _scrollX) { acc += _colWidth[firstCol]; firstCol++; }
        float firstColX = acc - _scrollX;

        int selC0 = Math.Min(_curCol, _selCol), selC1 = Math.Max(_curCol, _selCol);
        int selR0 = Math.Min(_curRow, _selRow), selR1 = Math.Max(_curRow, _selRow);

        // ---- cells -------------------------------------------------------
        c.R.PushClip(cellsArea);
        float x0 = cellsArea.X + firstColX;
        for (int col = firstCol; col < Cols && x0 < cellsArea.Right; col++)
        {
            float w = _colWidth[col];
            for (int row = firstRow; row <= lastRow; row++)
            {
                float y = cellsArea.Y + row * RowHeight - _scrollY;
                var r = new Rect(x0, y, w, RowHeight);

                bool inSel = col >= selC0 && col <= selC1 && row >= selR0 && row <= selR1;
                bool isCur = col == _curCol && row == _curRow;

                if (inSel && !isCur) c.R.FillRect(r, Color.Rgba(0x217346, 26));

                // Hairlines, which is all a 2013 grid was.
                c.R.FillRect(new Rect(r.X, r.Bottom - 1, r.W, 1), Rule);
                c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), Rule);

                string text = Display(col, row);
                if (text.Length > 0)
                {
                    var cell = At(col, row);
                    var font = cell is { Bold: true } ? c.F.UiBold : c.F.Ui;
                    bool numeric = IsNumeric(col, row);
                    int align = cell?.Align ?? 0;
                    if (align == 0) align = numeric ? 3 : 1;

                    if (numeric && !text.StartsWith('#'))
                        text = FitNumber(text, font, w - 6);

                    Rect clip = new(r.X + 1, r.Y, r.W - 3, r.H);
                    float tw = font.Measure(text);
                    if (!numeric && tw > r.W - 6)
                    {
                        if (align == 3)
                        {
                            float extra = 0;
                            for (int k = col - 1; k >= 0 && extra < tw - r.W + 6; k--)
                            {
                                if (Display(k, row).Length > 0) break;
                                extra += _colWidth[k];
                            }
                            clip = new Rect(r.X - extra, r.Y, r.W + extra - 3, r.H);
                        }
                        else
                        {
                            float extra = 0;
                            for (int k = col + 1; k < Cols && extra < tw - r.W + 6; k++)
                            {
                                if (Display(k, row).Length > 0) break;
                                extra += _colWidth[k];
                            }
                            clip = new Rect(r.X + 1, r.Y, r.W + extra - 3, r.H);
                        }
                    }

                    float tx = align switch
                    {
                        3 => r.Right - 4 - tw,
                        2 => r.CenterX - tw * 0.5f,
                        _ => r.X + 3,
                    };
                    Color ink = text.StartsWith('#') ? Color.Rgb(0xC00000) : Ink;

                    c.R.PushClip(clip.Intersect(cellsArea));
                    font.Draw(c.R, text, tx, r.Y + (RowHeight - font.Height) * 0.5f, ink);
                    c.R.PopClip();
                }

                if (isCur && !_editing)
                {
                    c.R.DrawRect(r, Green, 2);
                    // The fill handle, which 2013 drew as a small green square.
                    c.R.FillRect(new Rect(r.Right - 3, r.Bottom - 3, 5, 5), Green);
                }

                if (isCur && _editing)
                {
                    c.R.FillRect(r, Color.White);
                    c.R.DrawRect(r, Green, 2);
                    c.R.PushClip(r.Deflate(2));
                    c.F.Ui.Draw(c.R, _editBuffer, r.X + 3, r.Y + (RowHeight - c.F.Ui.Height) * 0.5f, Ink);
                    if ((c.Time % 1.06) < 0.53)
                        c.R.FillRect(new Rect(r.X + 3 + c.F.Ui.Measure(_editBuffer), r.Y + 3,
                                              1.4f, RowHeight - 6), Ink);
                    c.R.PopClip();
                }

                if (c.Clicked(r))
                {
                    if (_editing) CommitEdit();
                    _curCol = col; _curRow = row;
                    if (!c.In.Shift) { _selCol = col; _selRow = row; }
                    c.SoundAt(Sfx.Tick, r, 0.22f);
                }
                else if (c.DoubleClicked(r))
                {
                    _curCol = col; _curRow = row;
                    _selCol = col; _selRow = row;
                    BeginEdit(keepExisting: true);
                }
            }
            x0 += w;
        }
        c.R.PopClip();

        // ---- headers -----------------------------------------------------
        c.R.FillRect(corner, HeaderFace);
        c.R.FillRect(new Rect(corner.Right - 1, corner.Y, 1, corner.H), Rule);
        c.R.FillRect(new Rect(corner.X, corner.Bottom - 1, corner.W, 1), Rule);
        // The little triangle in the corner that selects the whole sheet.
        c.R.FillTriangle(corner.Right - 4, corner.Bottom - 4, corner.Right - 4,
                         corner.Bottom - 11, corner.Right - 11, corner.Bottom - 4, InkDim);

        c.R.PushClip(colHead);
        float hx = colHead.X + firstColX;
        for (int col = firstCol; col < Cols && hx < colHead.Right; col++)
        {
            float w = _colWidth[col];
            var r = new Rect(hx, colHead.Y, w, colHead.H);
            bool sel = col >= selC0 && col <= selC1;

            c.R.FillRect(r, sel ? GreenPale : HeaderFace);
            c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), Rule);
            c.R.FillRect(new Rect(r.X, r.Bottom - 1, r.W, 1), sel ? Green : Rule);
            c.F.Small.DrawCentered(c.R, ColName(col), r, sel ? Green : Ink);

            var grip = new Rect(r.Right - 3, r.Y, 6, r.H);
            if (c.Hovering(grip)) c.Cursor = CursorShape.SizeWE;
            if (c.Clicked(grip)) _resizingCol = col;

            if (c.Clicked(r)) { _curCol = col; _selCol = col; _curRow = 0; _selRow = Rows - 1; }
            hx += w;
        }
        c.R.PopClip();

        if (_resizingCol >= 0)
        {
            if (!c.In.IsDown(MouseButton.Left)) _resizingCol = -1;
            else
            {
                float left = colHead.X + firstColX;
                for (int col = firstCol; col < _resizingCol; col++) left += _colWidth[col];
                _colWidth[_resizingCol] = Math.Clamp(c.MouseX - left, 24, 400);
                c.Cursor = CursorShape.SizeWE;
                c.MouseHandled = true;
            }
        }

        c.R.PushClip(rowHead);
        for (int row = firstRow; row <= lastRow; row++)
        {
            var r = new Rect(rowHead.X, rowHead.Y + row * RowHeight - _scrollY, rowHead.W, RowHeight);
            bool sel = row >= selR0 && row <= selR1;

            c.R.FillRect(r, sel ? GreenPale : HeaderFace);
            c.R.FillRect(new Rect(r.X, r.Bottom - 1, r.W, 1), Rule);
            c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), sel ? Green : Rule);
            c.F.Small.DrawCentered(c.R, (row + 1).ToString(), r, sel ? Green : Ink);

            if (c.Clicked(r)) { _curRow = row; _selRow = row; _curCol = 0; _selCol = Cols - 1; }
        }
        c.R.PopClip();

        HandleKeys(c, cellsArea);
    }

    int _resizingCol = -1;

    // ---- the sheet tabs and the status bar --------------------------------

    void DrawSheetTabs(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.White);
        c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 1), Rule);

        string[] names = { L.T("sheet.sheet1"), L.T("sheet.sheet2"), L.T("sheet.sheet3") };

        // The four arrows that walked the tabs, kept as two.
        float x = bar.X + 4;
        foreach (int dir in new[] { 3, 1 })
        {
            var a = new Rect(x, bar.Y + 2, 18, bar.H - 4);
            if (c.Hovering(a)) c.R.FillRect(a, Color.Rgb(0xF0F0F0));
            W.Arrow(c, a, dir, InkDim, 3.2f);
            x = a.Right;
        }
        x += 4;

        for (int i = 0; i < names.Length; i++)
        {
            float w = c.F.Small.Measure(names[i]) + 24;
            var r = new Rect(x, bar.Y, w, bar.H);
            bool sel = i == _sheet;
            bool hot = c.Hovering(r);

            if (sel)
            {
                c.R.FillRect(r, Color.White);
                c.R.FillRect(new Rect(r.X, r.Y, r.W, 2), Green);
            }
            else if (hot) c.R.FillRect(r, Color.Rgb(0xF2F2F2));

            c.F.Small.DrawCentered(c.R, names[i], r, sel ? Green : Ink);
            if (c.Clicked(r)) { _sheet = i; c.SoundAt(Sfx.Tick, r, 0.3f); }
            x = r.Right + 1;
        }

        // The plus that made a new sheet, which is the one control 2013 added.
        var plus = new Rect(x + 4, bar.Y + 4, 16, bar.H - 8);
        if (c.Hovering(plus)) c.R.FillRect(plus, Color.Rgb(0xF0F0F0));
        c.R.FillRect(new Rect(plus.CenterX - 5, plus.CenterY - 0.75f, 10, 1.5f), InkDim);
        c.R.FillRect(new Rect(plus.CenterX - 0.75f, plus.CenterY - 5, 1.5f, 10), InkDim);
        c.Tooltip(plus, L.T("sheet.new_sheet"));
        if (c.Clicked(plus))
            Shell.MessageBox(c, L.T("sheet.miminus_sheet"), L.T("sheet.three_sheets_note"),
                             MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }

    void DrawStatusBar(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.White);
        c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 1), Rule);

        c.F.Small.Draw(c.R, L.T("sheet.ready"), bar.X + 8,
                       bar.CenterY - c.F.Small.Height * 0.5f, InkDim);

        // The sum of what is selected, which is what that bar was for.
        int c0 = Math.Min(_curCol, _selCol), c1 = Math.Max(_curCol, _selCol);
        int r0 = Math.Min(_curRow, _selRow), r1 = Math.Max(_curRow, _selRow);

        int count = 0;
        double sum = 0;
        for (int col = c0; col <= c1; col++)
            for (int row = r0; row <= r1; row++)
                if (IsNumeric(col, row)) { sum += Value(col, row); count++; }

        string right = count > 1
            ? L.F("sheet.sum_average", Fmt(sum), Fmt(sum / count), count)
            : L.F("sheet.selected_0", $"{ColName(_curCol)}{_curRow + 1}");

        c.F.Small.DrawRight(c.R, right, new Rect(bar.X, bar.CenterY - c.F.Small.Height * 0.5f,
                                                 bar.W - 10, c.F.Small.Height), InkDim);
    }

    // ---- editing ---------------------------------------------------------

    void BeginEdit(bool keepExisting)
    {
        _editing = true;
        _editBuffer = keepExisting ? (At(_curCol, _curRow)?.Raw ?? "") : "";
    }

    void CommitEdit()
    {
        if (!_editing) return;
        Set(_curCol, _curRow, _editBuffer);
        _editing = false;
        _editBuffer = null;
    }

    void MoveCursor(int dc, int dr, bool extend)
    {
        _curCol = Math.Clamp(_curCol + dc, 0, Cols - 1);
        _curRow = Math.Clamp(_curRow + dr, 0, Rows - 1);
        if (!extend) { _selCol = _curCol; _selRow = _curRow; }
        EnsureVisible();
    }

    void EnsureVisible()
    {
        float x = 0;
        for (int i = 0; i < _curCol; i++) x += _colWidth[i];
        if (x < _scrollX) _scrollX = x;
        // The exact viewport width is not known here; a generous window is enough
        // to keep the cursor on screen after keyboard navigation.
        else if (x + _colWidth[_curCol] > _scrollX + 400) _scrollX = x + _colWidth[_curCol] - 400;

        float y = _curRow * RowHeight;
        if (y < _scrollY) _scrollY = y;
        else if (y + RowHeight > _scrollY + 300) _scrollY = y + RowHeight - 300;

        if (_scrollX < 0) _scrollX = 0;
        if (_scrollY < 0) _scrollY = 0;
    }

    void HandleKeys(UiContext c, Rect cellsArea)
    {
        if (c.KeyboardHandled) return;
        var input = c.In;

        if (_editing)
        {
            foreach (char ch in input.TypedChars)
            {
                if (ch == '\r') { CommitEdit(); MoveCursor(0, 1, false); }
                else if (ch >= ' ') _editBuffer += ch;
                c.KeyboardHandled = true;
            }
            if (input.KeyPressed(Keys.Back) && _editBuffer.Length > 0)
            {
                _editBuffer = _editBuffer[..^1];
                c.KeyboardHandled = true;
            }
            else if (input.KeyPressed(Keys.Escape)) { _editing = false; _editBuffer = null; c.KeyboardHandled = true; }
            else if (input.KeyPressed(Keys.Tab)) { CommitEdit(); MoveCursor(1, 0, false); c.KeyboardHandled = true; }
            return;
        }

        bool shift = input.Shift;
        if (input.KeyPressed(Keys.Left)) { MoveCursor(-1, 0, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.Right) || input.KeyPressed(Keys.Tab)) { MoveCursor(1, 0, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.Up)) { MoveCursor(0, -1, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.Down) || input.KeyPressed(Keys.Enter)) { MoveCursor(0, 1, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.PageUp)) { MoveCursor(0, -15, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.PageDown)) { MoveCursor(0, 15, shift); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.Home)) { _curCol = 0; _selCol = 0; EnsureVisible(); c.KeyboardHandled = true; }
        else if (input.KeyPressed(Keys.Delete))
        {
            for (int col = Math.Min(_curCol, _selCol); col <= Math.Max(_curCol, _selCol); col++)
                for (int row = Math.Min(_curRow, _selRow); row <= Math.Max(_curRow, _selRow); row++)
                    _cells.Remove(Key(col, row));
            _cache.Clear();
            c.KeyboardHandled = true;
        }
        else if (input.KeyPressed(Keys.F2)) { BeginEdit(true); c.KeyboardHandled = true; }
        else if (input.Ctrl && input.KeyPressed(Keys.C))
        {
            Clipboard.SetText(Display(_curCol, _curRow));
            c.KeyboardHandled = true;
        }
        else if (input.TypedChars.Count > 0)
        {
            char first = input.TypedChars[0];
            if (first >= ' ')
            {
                BeginEdit(false);
                foreach (char ch in input.TypedChars) if (ch >= ' ') _editBuffer += ch;
                c.KeyboardHandled = true;
            }
        }
    }
}
