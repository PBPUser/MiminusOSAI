using System.Globalization;
using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Калькулятор Плюс — the calculator opened in part 3, wearing the face
/// Windows 7 gave the thing.
///
/// The seven look is specific and worth reproducing exactly: a white display
/// panel with the running expression in small grey type above the number, a
/// memory row across the top of the keypad, and square buttons that are almost
/// flat — a hairline border, the faintest of gradients, and a blue wash on
/// hover rather than the orange one XP used. The equals key is the only
/// coloured one. МС and MR are greyed out until there is something in memory,
/// which is the detail that gives the whole keypad away as a seven keypad.
///
/// It wears that face under every theme, the same way the folder window keeps
/// its ribbon: the calculator is the calculator whatever the desktop is doing.
/// The arithmetic is in <see cref="CalcEngine"/>, shared with the flat
/// full-screen calculator version 8 brought.</summary>
public sealed class CalculatorWindow : OsWindow
{
    enum Mode { Standard, Scientific, Conversion }

    readonly CalcEngine _calc = new();
    Mode _mode = Mode.Standard;

    // Conversion mode keeps its own number: it is not doing arithmetic.
    int _category, _fromUnit = 0, _toUnit = 1;
    string _convInput = "1";

    public override string Title => L.T("calc.calculator_plus");
    public override float MinWidth => 260;
    public override float MinHeight => 300;

    public CalculatorWindow()
    {
        Icon = IconId.Calculator;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 268, 336);
        BuildMenu();
    }

    // ---- the seven palette ---------------------------------------------------

    static readonly Color PanelFace = Color.Rgb(0xF0F0F0);
    static readonly Color DisplayBack = Color.Rgb(0xFFFFFF);
    static readonly Color DisplayEdge = Color.Rgb(0xA0A0A0);

    static readonly Color KeyTop = Color.Rgb(0xFDFDFD);
    static readonly Color KeyBottom = Color.Rgb(0xE9E9E9);
    static readonly Color KeyEdge = Color.Rgb(0xACACAC);

    static readonly Color HotTop = Color.Rgb(0xEAF6FD);
    static readonly Color HotBottom = Color.Rgb(0xC4E5F6);
    static readonly Color HotEdge = Color.Rgb(0x3C7FB1);

    static readonly Color HeldTop = Color.Rgb(0xC4E5F6);
    static readonly Color HeldBottom = Color.Rgb(0x98D1EF);
    static readonly Color HeldEdge = Color.Rgb(0x2C628B);

    static readonly Color EqualsTop = Color.Rgb(0x5D9BE0);
    static readonly Color EqualsBottom = Color.Rgb(0x2E6EB8);
    static readonly Color EqualsEdge = Color.Rgb(0x26538C);

    static readonly Color Ink = Color.Rgb(0x1A1A1A);
    static readonly Color InkOperator = Color.Rgb(0x1E3E6E);
    static readonly Color InkOff = Color.Rgb(0xA8A8A8);

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("calc.view"), () => new List<MenuItem>
        {
            new() { Text = L.T("calc.standard"), IsRadio = true, Checked = _mode == Mode.Standard,
                    Click = () => SetMode(Mode.Standard), Shortcut = "Alt+1" },
            new() { Text = L.T("calc.scientific"), IsRadio = true, Checked = _mode == Mode.Scientific,
                    Click = () => SetMode(Mode.Scientific), Shortcut = "Alt+2" },
            new() { Text = L.T("calc.unit_conversion"), IsRadio = true,
                    Checked = _mode == Mode.Conversion, Click = () => SetMode(Mode.Conversion),
                    Shortcut = "Alt+3" },
            MenuItem.Sep(),
            MenuItem.Of(L.T("calc.modern"), () => Shell.Launch(_ctx, "calculator8", null),
                        IconId.Calculator),
        });

        Menu.Add(L.T("calc.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("calc.copy"), () => Clipboard.SetText(_calc.Entry), shortcut: "Ctrl+C"),
            MenuItem.Of(L.T("calc.paste"), () =>
            {
                if (double.TryParse(Clipboard.GetText().Trim(), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out double v))
                    _calc.SetValue(v);
            }, shortcut: "Ctrl+V"),
        });

        Menu.Add(L.T("calc.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("calc.about"), () =>
                Shell.MessageBox(_ctx, L.T("calc.calculator_plus"),
                    L.T("calc.miminus_calculator_plus_version_1_0_calculat"),
                    MsgButtons.Ok, IconId.Calculator, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    void SetMode(Mode m)
    {
        _mode = m;
        Bounds.W = m switch { Mode.Standard => 268, Mode.Scientific => 480, _ => 380 };
        Bounds.H = m switch { Mode.Standard => 336, Mode.Scientific => 372, _ => 320 };
    }

    UiContext _ctx;

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        c.R.FillRect(client, PanelFace);

        var area = client.Deflate(7);
        DrawDisplay(c, area.CutTop(58));
        area.CutTop(7);

        switch (_mode)
        {
            case Mode.Scientific: DrawScientific(c, area); break;
            case Mode.Conversion: DrawConversion(c, area); break;
            default: DrawStandard(c, area); break;
        }

        HandleKeyboard(c);
    }

    /// <summary>The white panel: the expression in small grey type along the
    /// top, the number in large type along the bottom, both right-aligned, and
    /// the memory flag in the corner.</summary>
    void DrawDisplay(UiContext c, Rect r)
    {
        c.R.FillRect(r, DisplayBack);
        c.R.DrawRect(r, DisplayEdge);
        // The hairline of shadow seven put inside the top edge.
        c.R.FillRect(new Rect(r.X + 1, r.Y + 1, r.W - 2, 1), Color.Rgb(0xE0E0E0));

        c.R.PushClip(r.Deflate(4, 2, 4, 2));

        string expression = _mode == Mode.Conversion ? "" : _calc.Expression;
        if (expression.Length > 0)
        {
            float ew = c.F.Small.Measure(expression);
            c.F.Small.Draw(c.R, expression, r.Right - 8 - ew, r.Y + 5, Color.Rgb(0x808080));
        }

        string shown = _mode == Mode.Conversion ? _convInput : _calc.Entry;
        float w = c.F.Big.Measure(shown);
        c.F.Big.Draw(c.R, shown, r.Right - 8 - w, r.Bottom - c.F.Big.Height - 4, Ink);

        c.R.PopClip();

        if (_calc.HasMemory)
            c.F.Small.Draw(c.R, "M", r.X + 6, r.Bottom - c.F.Small.Height - 5, Color.Rgb(0x606060));
    }

    // ---- keypads -------------------------------------------------------------

    /// <summary>One key on the pad: where it sits in the grid, how many cells it
    /// takes, and what it is for.</summary>
    readonly record struct Key(string Label, int Col, int Row, int ColSpan = 1, int RowSpan = 1,
                               Kind Kind = Kind.Digit);

    enum Kind { Digit, Operator, Memory, Function, Equals }

    /// <summary>The standard pad, in seven's arrangement: memory across the top,
    /// then the clears, then the digits with the operators down the right and
    /// an equals key two rows tall.</summary>
    static readonly Key[] StandardKeys =
    {
        new("MC", 0, 0, Kind: Kind.Memory), new("MR", 1, 0, Kind: Kind.Memory),
        new("MS", 2, 0, Kind: Kind.Memory), new("M+", 3, 0, Kind: Kind.Memory),
        new("M-", 4, 0, Kind: Kind.Memory),

        new("Backspace", 0, 1, Kind: Kind.Function), new("CE", 1, 1, Kind: Kind.Function),
        new("C", 2, 1, Kind: Kind.Function), new("+/-", 3, 1, Kind: Kind.Function),
        new("sqrt", 4, 1, Kind: Kind.Function),

        new("7", 0, 2), new("8", 1, 2), new("9", 2, 2),
        new("/", 3, 2, Kind: Kind.Operator), new("%", 4, 2, Kind: Kind.Function),

        new("4", 0, 3), new("5", 1, 3), new("6", 2, 3),
        new("*", 3, 3, Kind: Kind.Operator), new("1/x", 4, 3, Kind: Kind.Function),

        new("1", 0, 4), new("2", 1, 4), new("3", 2, 4),
        new("-", 3, 4, Kind: Kind.Operator), new("=", 4, 4, RowSpan: 2, Kind: Kind.Equals),

        new("0", 0, 5, ColSpan: 2), new(".", 2, 5),
        new("+", 3, 5, Kind: Kind.Operator),
    };

    /// <summary>The scientific pad: the standard one with three columns of
    /// functions grafted onto its left, which is how seven grew it too.</summary>
    static readonly Key[] ScientificKeys =
    {
        new("sin", 0, 0, Kind: Kind.Function), new("cos", 1, 0, Kind: Kind.Function),
        new("tan", 2, 0, Kind: Kind.Function),
        new("MC", 3, 0, Kind: Kind.Memory), new("MR", 4, 0, Kind: Kind.Memory),
        new("MS", 5, 0, Kind: Kind.Memory), new("M+", 6, 0, Kind: Kind.Memory),
        new("M-", 7, 0, Kind: Kind.Memory),

        new("asin", 0, 1, Kind: Kind.Function), new("acos", 1, 1, Kind: Kind.Function),
        new("atan", 2, 1, Kind: Kind.Function),
        new("Backspace", 3, 1, Kind: Kind.Function), new("CE", 4, 1, Kind: Kind.Function),
        new("C", 5, 1, Kind: Kind.Function), new("+/-", 6, 1, Kind: Kind.Function),
        new("sqrt", 7, 1, Kind: Kind.Function),

        new("ln", 0, 2, Kind: Kind.Function), new("log", 1, 2, Kind: Kind.Function),
        new("n!", 2, 2, Kind: Kind.Function),
        new("7", 3, 2), new("8", 4, 2), new("9", 5, 2),
        new("/", 6, 2, Kind: Kind.Operator), new("%", 7, 2, Kind: Kind.Function),

        new("x^2", 0, 3, Kind: Kind.Function), new("x^y", 1, 3, Kind: Kind.Operator),
        new("pi", 2, 3, Kind: Kind.Function),
        new("4", 3, 3), new("5", 4, 3), new("6", 5, 3),
        new("*", 6, 3, Kind: Kind.Operator), new("1/x", 7, 3, Kind: Kind.Function),

        new("e", 0, 4, Kind: Kind.Function),
        new("1", 3, 4), new("2", 4, 4), new("3", 5, 4),
        new("-", 6, 4, Kind: Kind.Operator), new("=", 7, 4, RowSpan: 2, Kind: Kind.Equals),

        new("0", 3, 5, ColSpan: 2), new(".", 5, 5),
        new("+", 6, 5, Kind: Kind.Operator),
    };

    void DrawStandard(UiContext c, Rect area) => DrawPad(c, area, StandardKeys, 5, 6);

    void DrawScientific(UiContext c, Rect area) => DrawPad(c, area, ScientificKeys, 8, 6);

    void DrawPad(UiContext c, Rect area, Key[] keys, int cols, int rows)
    {
        const float gap = 4;
        float bw = (area.W - gap * (cols - 1)) / cols;
        float bh = (area.H - gap * (rows - 1)) / rows;

        foreach (var key in keys)
        {
            var r = new Rect(area.X + key.Col * (bw + gap), area.Y + key.Row * (bh + gap),
                             bw * key.ColSpan + gap * (key.ColSpan - 1),
                             bh * key.RowSpan + gap * (key.RowSpan - 1));

            // MC and MR do nothing until there is something to recall, and say
            // so by going grey — which is what the original did.
            bool enabled = key.Label is not ("MC" or "MR") || _calc.HasMemory;

            if (SevenButton(c, r, key.Label, key.Kind, enabled)) Press(c, key.Label);
        }
    }

    /// <summary>A seven key: a hairline border, almost no gradient, and a blue
    /// wash under the pointer.</summary>
    bool SevenButton(UiContext c, Rect r, string label, Kind kind, bool enabled = true)
    {
        bool hover = enabled && c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);

        Color top, bottom, edge;
        if (kind == Kind.Equals)
        {
            top = held ? EqualsBottom : hover ? EqualsTop.Shade(1.12f) : EqualsTop;
            bottom = held ? EqualsTop : EqualsBottom;
            edge = EqualsEdge;
        }
        else if (held) { top = HeldTop; bottom = HeldBottom; edge = HeldEdge; }
        else if (hover) { top = HotTop; bottom = HotBottom; edge = HotEdge; }
        else { top = KeyTop; bottom = KeyBottom; edge = KeyEdge; }

        c.R.RoundedRectV(r, 2, top, bottom, edge, 1);

        // The pale line seven ran just inside the top edge of every key.
        if (kind != Kind.Equals)
            c.R.FillRect(new Rect(r.X + 1.5f, r.Y + 1.5f, r.W - 3, 1), Color.Rgba(0xFFFFFF, 190));

        Color ink = !enabled ? InkOff
                  : kind == Kind.Equals ? Color.White
                  : kind == Kind.Operator ? InkOperator
                  : kind == Kind.Memory ? Color.Rgb(0x404040)
                  : Ink;

        string shown = Glyph(label);
        var font = kind == Kind.Digit || kind == Kind.Equals || shown.Length <= 2
            ? c.F.Ui : c.F.Small;

        float w = font.Measure(shown);
        font.Draw(c.R, shown, r.CenterX - w * 0.5f, r.CenterY - font.Height * 0.5f, ink);

        bool clicked = enabled && c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f, 1.05f);
        return clicked;
    }

    /// <summary>How a key is written on itself. The engine speaks in ASCII; the
    /// keypad does not have to.</summary>
    internal static string Glyph(string label) => label switch
    {
        "Backspace" => "←",
        "sqrt" => "√",
        "pi" => "π",
        "x^2" => "x²",
        "x^y" => "xʸ",
        "/" => "÷",
        "*" => "×",
        "1/x" => "1/x",
        "n!" => "n!",
        _ => label,
    };

    void Press(UiContext c, string key)
    {
        if (_mode == Mode.Conversion) { PressConversion(key); return; }
        if (_calc.Press(key)) c.Sound(Sfx.Error, 0.6f);
    }

    void HandleKeyboard(UiContext c)
    {
        if (c.KeyboardHandled) return;

        foreach (char ch in c.In.TypedChars)
        {
            if (char.IsDigit(ch)) { Press(c, ch.ToString()); c.KeyboardHandled = true; }
            else if (ch is '.' or ',') { Press(c, "."); c.KeyboardHandled = true; }
            else if (ch is '+' or '-' or '*' or '/') { Press(c, ch.ToString()); c.KeyboardHandled = true; }
            else if (ch is '=' or '\r') { Press(c, "="); c.KeyboardHandled = true; }
        }

        if (c.In.KeyPressed(Keys.Back)) { Press(c, "Backspace"); c.KeyboardHandled = true; }
        else if (c.In.KeyPressed(Keys.Escape)) { Press(c, "C"); c.KeyboardHandled = true; }
        else if (c.In.KeyPressed(Keys.Delete)) { Press(c, "CE"); c.KeyboardHandled = true; }
    }

    // ---- conversion mode -----------------------------------------------------

    sealed record UnitDef(string Key, double ToBase);

    static readonly (string key, UnitDef[] units)[] Categories =
    {
        ("unit.length", new[]
        {
            new UnitDef("unit.millimetre", 0.001),
            new UnitDef("unit.centimetre", 0.01),
            new UnitDef("unit.metre", 1),
            new UnitDef("unit.kilometre", 1000),
            new UnitDef("unit.inch", 0.0254),
            new UnitDef("unit.foot", 0.3048),
            new UnitDef("unit.mile", 1609.344),
        }),
        ("unit.mass", new[]
        {
            new UnitDef("unit.gram", 0.001),
            new UnitDef("unit.kilogram", 1),
            new UnitDef("unit.tonne", 1000),
            new UnitDef("unit.pound", 0.45359237),
            new UnitDef("unit.ounce", 0.028349523125),
        }),
        ("unit.volume", new[]
        {
            new UnitDef("unit.millilitre", 0.001),
            new UnitDef("unit.litre", 1),
            new UnitDef("unit.gallon_us", 3.785411784),
            new UnitDef("unit.pint_us", 0.473176473),
        }),
        ("unit.data", new[]
        {
            new UnitDef("unit.byte", 1),
            new UnitDef("unit.kilobyte", 1024),
            new UnitDef("unit.megabyte", 1024d * 1024),
            new UnitDef("unit.gigabyte", 1024d * 1024 * 1024),
        }),
    };

    void DrawConversion(UiContext c, Rect area)
    {
        var t = c.Theme;

        var row = area.CutTop(24);
        c.F.Ui.Draw(c.R, L.T("calc.category"), row.X, row.Y + 4, Ink);
        var catNames = Categories.Select(x => L.T(x.key)).ToList();
        if (W.ComboBox(c, Id + ".cat", new Rect(row.X + 90, row.Y, row.W - 90, 22), catNames, ref _category))
        {
            _fromUnit = 0;
            _toUnit = Math.Min(1, Categories[_category].units.Length - 1);
        }
        area.CutTop(8);

        var units = Categories[_category].units;
        var names = units.Select(u => L.T(u.Key)).ToList();
        _fromUnit = Math.Clamp(_fromUnit, 0, units.Length - 1);
        _toUnit = Math.Clamp(_toUnit, 0, units.Length - 1);

        row = area.CutTop(24);
        c.F.Ui.Draw(c.R, L.T("calc.from"), row.X, row.Y + 4, Ink);
        W.ComboBox(c, Id + ".from", new Rect(row.X + 90, row.Y, row.W - 90, 22), names, ref _fromUnit);
        area.CutTop(6);

        row = area.CutTop(24);
        c.F.Ui.Draw(c.R, L.T("calc.to"), row.X, row.Y + 4, Ink);
        W.ComboBox(c, Id + ".to", new Rect(row.X + 90, row.Y, row.W - 90, 22), names, ref _toUnit);
        area.CutTop(10);

        double input = double.TryParse(_convInput, NumberStyles.Any, CultureInfo.InvariantCulture,
                                       out double v) ? v : 0;
        double result = input * units[_fromUnit].ToBase / units[_toUnit].ToBase;

        row = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("calc.result"), row.X, row.Y + 5, Ink);
        var res = new Rect(row.X + 90, row.Y, row.W - 90, 24);
        c.R.FillRect(res, DisplayBack);
        c.R.DrawRect(res, DisplayEdge);
        c.R.PushClip(res.Deflate(3));
        c.F.UiBold.Draw(c.R, CalcEngine.Format(result), res.X + 5,
                        res.CenterY - c.F.UiBold.Height * 0.5f, Ink);
        c.R.PopClip();

        // A compact numeric pad for the number being converted.
        area.CutTop(8);
        Key[] pad =
        {
            new("7", 0, 0), new("8", 1, 0), new("9", 2, 0), new("C", 3, 0, Kind: Kind.Function),
            new("4", 0, 1), new("5", 1, 1), new("6", 2, 1),
            new("Backspace", 3, 1, Kind: Kind.Function),
            new("1", 0, 2), new("2", 1, 2), new("3", 2, 2), new(".", 3, 2),
            new("0", 0, 3, ColSpan: 2), new("+/-", 2, 3, Kind: Kind.Function),
        };
        DrawPad(c, area, pad, 4, 4);
    }

    void PressConversion(string key)
    {
        switch (key)
        {
            case "C": _convInput = "0"; return;
            case "Backspace":
                _convInput = _convInput.Length > 1 ? _convInput[..^1] : "0";
                if (_convInput == "-") _convInput = "0";
                return;
            case "+/-":
                _convInput = _convInput.StartsWith('-') ? _convInput[1..] : "-" + _convInput;
                return;
            case ".":
                if (!_convInput.Contains('.')) _convInput += ".";
                return;
            default:
                if (key.Length == 1 && char.IsDigit(key[0]))
                    _convInput = _convInput == "0" ? key : _convInput + key;
                return;
        }
    }
}
