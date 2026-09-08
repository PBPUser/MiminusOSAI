using System.Globalization;
using Miminus.Sys;

namespace Miminus.Apps;

/// <summary>The arithmetic both calculators are made of.
///
/// Two windows share this: the one with the Windows 7 keypad and the flat
/// full-screen one version 8 brought. Neither of them knows how to add — they
/// draw buttons and hand the label of whichever was pressed to <see cref="Press"/>,
/// which is the whole of the interface between the two halves.
///
/// It is an accumulator machine, not an expression parser: a pending operator
/// and one number waiting for it, exactly as every calculator of this shape has
/// worked since they had gears in them. (The system does have a real parser —
/// it is in the spreadsheet, where it belongs.)</summary>
public sealed class CalcEngine
{
    string _entry = "0";
    double _accumulator;
    string _pendingOp;
    bool _fresh = true;
    double _memory;

    /// <summary>What is on the display.</summary>
    public string Entry => _entry;

    /// <summary>The line above it: the number already entered and the operator
    /// waiting for a second one. Empty when nothing is pending.</summary>
    public string Expression { get; private set; } = "";

    public double Memory => _memory;
    public bool HasMemory => _memory != 0;

    public double Value => double.TryParse(_entry, NumberStyles.Any,
                                           CultureInfo.InvariantCulture, out double v) ? v : 0;

    /// <summary>Twelve significant figures, then scientific notation — the
    /// range a calculator of this size admits to having.</summary>
    public static string Format(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return L.T("calc.error");
        if (MathF.Abs((float)v) >= 1e15 || (v != 0 && MathF.Abs((float)v) < 1e-10))
            return v.ToString("G12", CultureInfo.InvariantCulture);

        string s = v.ToString("0.############", CultureInfo.InvariantCulture);
        return s.Length == 0 ? "0" : s;
    }

    public void SetValue(double v)
    {
        _entry = Format(v);
        _fresh = true;
    }

    void Digit(char d)
    {
        if (_fresh) { _entry = d == '.' ? "0." : d.ToString(); _fresh = false; return; }
        if (d == '.' && _entry.Contains('.')) return;
        if (_entry == "0" && d != '.') _entry = d.ToString();
        else _entry += d;
    }

    /// <summary>Acts on one key. Returns true when the result was not a number,
    /// so the window can make the noise: the engine has no sound of its own.</summary>
    public bool Press(string key)
    {
        switch (key)
        {
            case "0" or "1" or "2" or "3" or "4" or "5" or "6" or "7" or "8" or "9" or ".":
                Digit(key[0]);
                return false;

            case "C":
                _entry = "0"; _accumulator = 0; _pendingOp = null; Expression = ""; _fresh = true;
                return false;

            case "CE": _entry = "0"; _fresh = true; return false;

            case "Backspace":
                if (_fresh) return false;
                _entry = _entry.Length > 1 ? _entry[..^1] : "0";
                if (_entry == "-" || _entry.Length == 0) _entry = "0";
                return false;

            case "+/-":
                _entry = _entry.StartsWith('-') ? _entry[1..] : "-" + _entry;
                return false;

            case "MC": _memory = 0; return false;
            case "MR": _entry = Format(_memory); _fresh = true; return false;
            case "MS": _memory = Value; _fresh = true; return false;
            case "M+": _memory += Value; _fresh = true; return false;
            case "M-": _memory -= Value; _fresh = true; return false;

            case "+" or "-" or "*" or "/" or "x^y":
                ApplyPending();
                _pendingOp = key;
                Expression = _entry + " " + Symbol(key);
                _fresh = true;
                return false;

            case "=":
                ApplyPending();
                _pendingOp = null;
                Expression = "";
                _fresh = true;
                return false;

            case "sqrt": return Unary(Math.Sqrt);
            case "x^2": return Unary(v => v * v);
            case "1/x": return Unary(v => v == 0 ? double.NaN : 1 / v);
            case "%": _entry = Format(_accumulator * Value / 100); _fresh = true; return false;

            case "sin": return Unary(Math.Sin);
            case "cos": return Unary(Math.Cos);
            case "tan": return Unary(Math.Tan);
            case "asin": return Unary(Math.Asin);
            case "acos": return Unary(Math.Acos);
            case "atan": return Unary(Math.Atan);
            case "ln": return Unary(v => v <= 0 ? double.NaN : Math.Log(v));
            case "log": return Unary(v => v <= 0 ? double.NaN : Math.Log10(v));
            case "n!": return Unary(Factorial);
            case "pi": _entry = Format(Math.PI); _fresh = true; return false;
            case "e": _entry = Format(Math.E); _fresh = true; return false;

            default: return false;
        }
    }

    /// <summary>How an operator is written on the line above the number, which
    /// is not always how it is written on its own key.</summary>
    public static string Symbol(string op) => op switch
    {
        "*" => "×",
        "/" => "÷",
        "x^y" => "^",
        _ => op,
    };

    bool Unary(Func<double, double> f)
    {
        double r = f(Value);
        _fresh = true;

        if (double.IsNaN(r) || double.IsInfinity(r))
        {
            _entry = L.T("calc.error");
            return true;
        }
        _entry = Format(r);
        return false;
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
}
