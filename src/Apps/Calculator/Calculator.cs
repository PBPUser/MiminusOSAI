using System.Globalization;
using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Калькулятор Плюс — the calculator opened in part 3, with the standard
/// and scientific keypads plus the unit-conversion mode that gave "Plus" its name.</summary>
public sealed class CalculatorWindow : OsWindow
{
    enum Mode { Standard, Scientific, Conversion }

    Mode _mode = Mode.Standard;

    string _entry = "0";
    double _accumulator;
    string _pendingOp;
    bool _freshEntry = true;
    double _memory;
    string _statusOp = "";

    // Conversion mode
    int _category, _fromUnit, _toUnit;
    string _convInput = "1";

    public override string Title => L.T("calc.calculator_plus");
    public override float MinWidth => 260;
    public override float MinHeight => 260;

    public CalculatorWindow()
    {
        Icon = IconId.Calculator;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 300, 300);
        BuildMenu();
    }

    void BuildMenu()
    {
        Menu = new MenuBar();
        Menu.Add(L.T("calc.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("calc.copy"), () => Clipboard.SetText(_entry), shortcut: "Ctrl+C"),
            MenuItem.Of(L.T("calc.paste"), () =>
            {
                if (double.TryParse(Clipboard.GetText().Trim(), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out double v))
                { _entry = Format(v); _freshEntry = true; }
            }, shortcut: "Ctrl+V"),
        });

        Menu.Add(L.T("calc.view"), () => new List<MenuItem>
        {
            new() { Text = L.T("calc.standard"), IsRadio = true, Checked = _mode == Mode.Standard,
                    Click = () => SetMode(Mode.Standard) },
            new() { Text = L.T("calc.scientific"), IsRadio = true, Checked = _mode == Mode.Scientific,
                    Click = () => SetMode(Mode.Scientific) },
            new() { Text = L.T("calc.unit_conversion"), IsRadio = true,
                    Checked = _mode == Mode.Conversion, Click = () => SetMode(Mode.Conversion) },
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
        Bounds.W = m switch { Mode.Standard => 300, Mode.Scientific => 460, _ => 380 };
        Bounds.H = m switch { Mode.Standard => 300, Mode.Scientific => 340, _ => 260 };
    }

    UiContext _ctx;

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(8);

        // Display.
        var display = area.CutTop(30);
        W.SunkenField(c, display);
        c.R.PushClip(display.Deflate(3));
        string shown = _mode == Mode.Conversion ? _convInput : _entry;
        float w = c.F.Big.Measure(shown);
        c.F.Big.Draw(c.R, shown, display.Right - 6 - w, display.CenterY - c.F.Big.Height * 0.5f, c.Theme.Text);
        c.R.PopClip();

        // Memory / pending-operator indicators, as on the real thing.
        var flags = area.CutTop(18);
        if (_memory != 0)
        {
            var m = new Rect(flags.X, flags.Y, 26, 16);
            W.SunkenField(c, m);
            c.F.Small.DrawCentered(c.R, "M", m, c.Theme.Text);
        }
        if (_statusOp.Length > 0)
            c.F.Small.Draw(c.R, _statusOp, flags.X + 34, flags.Y + 2, c.Theme.TextDisabled);

        area.CutTop(4);

        switch (_mode)
        {
            case Mode.Scientific: DrawScientific(c, area); break;
            case Mode.Conversion: DrawConversion(c, area); break;
            default: DrawStandard(c, area); break;
        }

        HandleKeyboard(c);
    }

    // ---- keypads ---------------------------------------------------------

    void DrawStandard(UiContext c, Rect area)
    {
        string[][] rows =
        {
            new[] { "MC", "7", "8", "9", "/", "sqrt" },
            new[] { "MR", "4", "5", "6", "*", "%" },
            new[] { "MS", "1", "2", "3", "-", "1/x" },
            new[] { "M+", "0", "+/-", ".", "+", "=" },
        };
        var top = area.CutTop(28);
        float bw = (top.W - 3 * 4) / 4;
        string[] clears = { "Backspace", "CE", "C" };
        for (int i = 0; i < clears.Length; i++)
        {
            var r = new Rect(top.X + (i + 1) * (bw + 4), top.Y, bw, 24);
            if (KeyButton(c, clears[i], r, Color.Rgb(0xC02020))) Press(c, clears[i]);
        }
        DrawGrid(c, area, rows);
    }

    void DrawScientific(UiContext c, Rect area)
    {
        string[][] rows =
        {
            new[] { "MC", "7", "8", "9", "/", "sin", "cos", "tan" },
            new[] { "MR", "4", "5", "6", "*", "asin", "acos", "atan" },
            new[] { "MS", "1", "2", "3", "-", "ln", "log", "x^y" },
            new[] { "M+", "0", "+/-", ".", "+", "n!", "pi", "=" },
        };
        var top = area.CutTop(28);
        float bw = (top.W - 7 * 4) / 8;
        string[] clears = { "Backspace", "CE", "C", "sqrt", "1/x", "%", "x^2", "e" };
        for (int i = 0; i < clears.Length; i++)
        {
            var r = new Rect(top.X + i * (bw + 4), top.Y, bw, 24);
            if (KeyButton(c, clears[i], r, i < 3 ? Color.Rgb(0xC02020) : c.Theme.Text)) Press(c, clears[i]);
        }
        DrawGrid(c, area, rows);
    }

    void DrawGrid(UiContext c, Rect area, string[][] rows)
    {
        float gap = 4;
        int cols = rows[0].Length;
        float bw = (area.W - gap * (cols - 1)) / cols;
        float bh = (area.H - gap * (rows.Length - 1)) / rows.Length;

        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < rows[r].Length; i++)
            {
                string key = rows[r][i];
                var rect = new Rect(area.X + i * (bw + gap), area.Y + r * (bh + gap), bw, bh);
                Color ink = key switch
                {
                    "=" => Color.Rgb(0x0050C0),
                    "/" or "*" or "-" or "+" => Color.Rgb(0x0050C0),
                    "MC" or "MR" or "MS" or "M+" => Color.Rgb(0xC02020),
                    _ => c.Theme.Text,
                };
                if (KeyButton(c, key, rect, ink)) Press(c, key);
            }
    }

    bool KeyButton(UiContext c, string label, Rect r, Color ink)
    {
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);
        W.DrawButtonFace(c, r, true, hover, held, label == "=");

        string shown = label switch
        {
            "Backspace" => L.T("calc.back"),
            "sqrt" => "√",
            "pi" => "π",
            "x^2" => "x²",
            "x^y" => "xʸ",
            _ => label,
        };
        var font = r.W > 40 ? c.F.Ui : c.F.Small;
        float w = font.Measure(shown);
        font.Draw(c.R, shown, r.CenterX - w * 0.5f + (held ? 1 : 0),
                  r.CenterY - font.Height * 0.5f + (held ? 1 : 0), ink);

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f, 1.05f);
        return clicked;
    }

    // ---- engine ----------------------------------------------------------

    static string Format(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return L.T("calc.error");
        if (MathF.Abs((float)v) >= 1e15 || (v != 0 && MathF.Abs((float)v) < 1e-10))
            return v.ToString("G12", CultureInfo.InvariantCulture);
        string s = v.ToString("0.############", CultureInfo.InvariantCulture);
        return s.Length == 0 ? "0" : s;
    }

    double Value => double.TryParse(_entry, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;

    void Digit(char d)
    {
        if (_freshEntry) { _entry = d == '.' ? "0." : d.ToString(); _freshEntry = false; return; }
        if (d == '.' && _entry.Contains('.')) return;
        if (_entry == "0" && d != '.') _entry = d.ToString();
        else _entry += d;
    }

    void Press(UiContext c, string key)
    {
        if (_mode == Mode.Conversion)
        {
            PressConversion(key);
            return;
        }

        switch (key)
        {
            case "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or ".":
                Digit(key[0]);
                return;

            case "C": _entry = "0"; _accumulator = 0; _pendingOp = null; _statusOp = ""; _freshEntry = true; return;
            case "CE": _entry = "0"; _freshEntry = true; return;
            case "Backspace":
                if (_freshEntry) return;
                _entry = _entry.Length > 1 ? _entry[..^1] : "0";
                if (_entry == "-" || _entry.Length == 0) _entry = "0";
                return;

            case "+/-":
                _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
                return;

            case "MC": _memory = 0; return;
            case "MR": _entry = Format(_memory); _freshEntry = true; return;
            case "MS": _memory = Value; _freshEntry = true; return;
            case "M+": _memory += Value; _freshEntry = true; return;

            case "+" or "-" or "*" or "/" or "x^y":
                ApplyPending();
                _pendingOp = key;
                _statusOp = key == "x^y" ? "^" : key;
                _freshEntry = true;
                return;

            case "=":
                ApplyPending();
                _pendingOp = null;
                _statusOp = "";
                _freshEntry = true;
                return;

            case "sqrt": Unary(c, Math.Sqrt); return;
            case "x^2": Unary(c, v => v * v); return;
            case "1/x": Unary(c, v => v == 0 ? double.NaN : 1 / v); return;
            case "%": _entry = Format(_accumulator * Value / 100); _freshEntry = true; return;

            case "sin": Unary(c, Math.Sin); return;
            case "cos": Unary(c, Math.Cos); return;
            case "tan": Unary(c, Math.Tan); return;
            case "asin": Unary(c, Math.Asin); return;
            case "acos": Unary(c, Math.Acos); return;
            case "atan": Unary(c, Math.Atan); return;
            case "ln": Unary(c, v => v <= 0 ? double.NaN : Math.Log(v)); return;
            case "log": Unary(c, v => v <= 0 ? double.NaN : Math.Log10(v)); return;
            case "n!": Unary(c, Factorial); return;
            case "pi": _entry = Format(Math.PI); _freshEntry = true; return;
            case "e": _entry = Format(Math.E); _freshEntry = true; return;
        }
    }

    void Unary(UiContext c, Func<double, double> f)
    {
        double r = f(Value);
        if (double.IsNaN(r) || double.IsInfinity(r))
        {
            _entry = L.T("calc.error");
            c.Sound(Sfx.Error, 0.6f);
        }
        else _entry = Format(r);
        _freshEntry = true;
    }

    static double Factorial(double v)
    {
        if (v < 0 || v != Math.Floor(v) || v > 170) return double.NaN;
        double r = 1;
        for (int i = 2; i <= (int)v; i++) r *= i;
        return r;
    }

    void ApplyPending()
    {
        double rhs = Value;
        if (_pendingOp == null) { _accumulator = rhs; return; }

        _accumulator = _pendingOp switch
        {
            "+" => _accumulator + rhs,
            "-" => _accumulator - rhs,
            "*" => _accumulator * rhs,
            "/" => rhs == 0 ? double.NaN : _accumulator / rhs,
            "x^y" => Math.Pow(_accumulator, rhs),
            _ => rhs,
        };
        _entry = Format(_accumulator);
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

    // ---- conversion mode -------------------------------------------------

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
        c.F.Ui.Draw(c.R, L.T("calc.category"), row.X, row.Y + 4, t.Text);
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
        c.F.Ui.Draw(c.R, L.T("calc.from"), row.X, row.Y + 4, t.Text);
        W.ComboBox(c, Id + ".from", new Rect(row.X + 90, row.Y, row.W - 90, 22), names, ref _fromUnit);
        area.CutTop(6);

        row = area.CutTop(24);
        c.F.Ui.Draw(c.R, L.T("calc.to"), row.X, row.Y + 4, t.Text);
        W.ComboBox(c, Id + ".to", new Rect(row.X + 90, row.Y, row.W - 90, 22), names, ref _toUnit);
        area.CutTop(10);

        double input = double.TryParse(_convInput, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
        double result = input * units[_fromUnit].ToBase / units[_toUnit].ToBase;

        row = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("calc.result"), row.X, row.Y + 5, t.Text);
        var res = new Rect(row.X + 90, row.Y, row.W - 90, 24);
        W.SunkenField(c, res);
        c.R.PushClip(res.Deflate(3));
        c.F.UiBold.Draw(c.R, Format(result), res.X + 5, res.CenterY - c.F.UiBold.Height * 0.5f, t.Text);
        c.R.PopClip();

        // A compact numeric pad for the conversion input.
        area.CutTop(8);
        string[][] rows =
        {
            new[] { "7", "8", "9", "C" },
            new[] { "4", "5", "6", "Backspace" },
            new[] { "1", "2", "3", "." },
            new[] { "0", "+/-", "", "" },
        };
        float gap = 4, bw = (area.W - gap * 3) / 4, bh = MathF.Max(20, (area.H - gap * 3) / 4);
        for (int r = 0; r < rows.Length; r++)
            for (int i = 0; i < 4; i++)
            {
                string key = rows[r][i];
                if (key.Length == 0) continue;
                var rect = new Rect(area.X + i * (bw + gap), area.Y + r * (bh + gap), bw, bh);
                if (KeyButton(c, key, rect, key == "C" ? Color.Rgb(0xC02020) : t.Text)) Press(c, key);
            }
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
