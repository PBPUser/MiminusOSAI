using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Калькулятор» — the same arithmetic with version 8's face on it.
///
/// It is the other half of the joke the two calculators make together: this one
/// fills the screen to do what the other one does in a window the size of a
/// postcard. There is no chrome, no menu bar and no border — the keys are flat
/// rectangles of one colour with a gap between them, the equals key is the
/// accent colour, and the mode switch is on an app bar along the bottom where
/// version 8 put every command it could not find room for.
///
/// The engine is <see cref="CalcEngine"/>, shared with «Калькулятор Плюс»: both
/// windows are keypads over the same accumulator.</summary>
public sealed class ModernCalculatorWindow : OsWindow
{
    readonly CalcEngine _calc = new();
    bool _scientific;

    public override string Title => L.T("calc8.title");
    public override float MinWidth => 420;
    public override float MinHeight => 420;

    public ModernCalculatorWindow()
    {
        Icon = IconId.Calculator;
        Bounds = new Rect(0, 0, 560, 640);
        Immersive = true;
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    // ---- the flat palette ----------------------------------------------------

    static readonly Color Back = Color.Rgb(0x1B1B1B);
    static readonly Color DigitFace = Color.Rgb(0x333333);
    static readonly Color FunctionFace = Color.Rgb(0x262626);
    static readonly Color MemoryFace = Color.Rgb(0x222222);
    static readonly Color Disabled = Color.Rgb(0x1F1F1F);

    /// <summary>One key: what it says, where it sits and what it counts as.</summary>
    readonly record struct Key(string Label, int Col, int Row, int ColSpan = 1, int RowSpan = 1,
                               Kind Kind = Kind.Digit);

    enum Kind { Digit, Operator, Memory, Function, Equals }

    static readonly Key[] StandardKeys =
    {
        new("MC", 0, 0, Kind: Kind.Memory), new("MR", 1, 0, Kind: Kind.Memory),
        new("MS", 2, 0, Kind: Kind.Memory), new("M+", 3, 0, Kind: Kind.Memory),

        new("%", 0, 1, Kind: Kind.Function), new("sqrt", 1, 1, Kind: Kind.Function),
        new("x^2", 2, 1, Kind: Kind.Function), new("1/x", 3, 1, Kind: Kind.Function),

        new("CE", 0, 2, Kind: Kind.Function), new("C", 1, 2, Kind: Kind.Function),
        new("Backspace", 2, 2, Kind: Kind.Function), new("/", 3, 2, Kind: Kind.Operator),

        new("7", 0, 3), new("8", 1, 3), new("9", 2, 3),
        new("*", 3, 3, Kind: Kind.Operator),

        new("4", 0, 4), new("5", 1, 4), new("6", 2, 4),
        new("-", 3, 4, Kind: Kind.Operator),

        new("1", 0, 5), new("2", 1, 5), new("3", 2, 5),
        new("+", 3, 5, Kind: Kind.Operator),

        new("+/-", 0, 6), new("0", 1, 6), new(".", 2, 6),
        new("=", 3, 6, Kind: Kind.Equals),
    };

    /// <summary>Three more columns of functions on the left, which is the only
    /// difference between the two modes.</summary>
    static readonly Key[] ScientificExtra =
    {
        new("sin", 0, 1, Kind: Kind.Function), new("cos", 1, 1, Kind: Kind.Function),
        new("tan", 2, 1, Kind: Kind.Function),
        new("asin", 0, 2, Kind: Kind.Function), new("acos", 1, 2, Kind: Kind.Function),
        new("atan", 2, 2, Kind: Kind.Function),
        new("ln", 0, 3, Kind: Kind.Function), new("log", 1, 3, Kind: Kind.Function),
        new("n!", 2, 3, Kind: Kind.Function),
        new("pi", 0, 4, Kind: Kind.Function), new("e", 1, 4, Kind: Kind.Function),
        new("x^y", 2, 4, Kind: Kind.Operator),
    };

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Back);

        var bar = client.CutBottom(64);
        var area = client.Deflate(28, 20, 28, 12);

        DrawDisplay(c, area.CutTop(MathF.Max(90, area.H * 0.22f)));
        area.CutTop(14);

        if (_scientific)
        {
            // The extra columns take a third of the width, and the standard pad
            // keeps the rest — so the digits stay where the hand expects them.
            var extra = area.CutLeft(area.W * 0.42f);
            DrawPad(c, extra.Deflate(0, 0, 10, 0), ScientificExtra, 3, 7);
        }

        DrawPad(c, area, StandardKeys, 4, 7);
        DrawAppBar(c, bar);
        HandleKeyboard(c);
    }

    /// <summary>The number, in the largest face the system has, right-aligned
    /// with the pending expression in grey above it — and nothing else.</summary>
    void DrawDisplay(UiContext c, Rect r)
    {
        c.R.PushClip(r);

        if (_calc.Expression.Length > 0)
        {
            float ew = c.F.Caption.Measure(_calc.Expression);
            c.F.Caption.Draw(c.R, _calc.Expression, r.Right - ew, r.Y, Color.Rgb(0x9A9A9A));
        }

        if (_calc.HasMemory)
            c.F.Caption.Draw(c.R, "M", r.X, r.Y, Theme.MetroAccent);

        // The largest face is 92pt with a lot of air in the line box, so the
        // number is laid against the bottom of the panel rather than centred.
        string shown = _calc.Entry;
        float w = c.F.Huge.Measure(shown);
        float scale = MathF.Min(1, (r.W - 8) / MathF.Max(1, w));

        if (scale >= 0.999f)
            c.F.Huge.Draw(c.R, shown, r.Right - w, r.Bottom - c.F.Huge.Height * 0.9f, Color.White);
        else
        {
            // Too long for the big face: fall back to the next one down rather
            // than letting it run off the side.
            float bw = c.F.Big.Measure(shown);
            c.F.Big.Draw(c.R, shown, r.Right - bw, r.Bottom - c.F.Big.Height - 6, Color.White);
        }

        c.R.PopClip();
    }

    void DrawPad(UiContext c, Rect area, Key[] keys, int cols, int rows)
    {
        const float gap = 6;
        float bw = (area.W - gap * (cols - 1)) / cols;
        float bh = (area.H - gap * (rows - 1)) / rows;

        foreach (var key in keys)
        {
            var r = new Rect(area.X + key.Col * (bw + gap), area.Y + key.Row * (bh + gap),
                             bw * key.ColSpan + gap * (key.ColSpan - 1),
                             bh * key.RowSpan + gap * (key.RowSpan - 1));

            bool enabled = key.Label is not ("MC" or "MR") || _calc.HasMemory;
            if (FlatKey(c, r, key.Label, key.Kind, enabled)) Press(c, key.Label);
        }
    }

    /// <summary>A flat rectangle that lightens under the pointer. No border, no
    /// gradient, no corner radius: the whole of version 8's idea of a button.</summary>
    bool FlatKey(UiContext c, Rect r, string label, Kind kind, bool enabled)
    {
        bool hover = enabled && c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);

        Color face = kind switch
        {
            Kind.Equals => Theme.MetroAccent,
            Kind.Digit => DigitFace,
            Kind.Memory => MemoryFace,
            _ => FunctionFace,
        };
        if (!enabled) face = Disabled;
        else if (held) face = face.Shade(0.78f);
        else if (hover) face = kind == Kind.Equals ? face.Shade(1.18f) : face.Shade(1.35f);

        c.R.FillRect(r, face);

        Color ink = !enabled ? Color.Rgb(0x5A5A5A)
                  : kind == Kind.Operator ? Color.Rgb(0xBFDCFF)
                  : kind == Kind.Memory ? Color.Rgb(0xBEBEBE)
                  : Color.White;

        string shown = CalculatorWindow.Glyph(label);
        var font = kind == Kind.Digit ? c.F.Caption : c.F.Ui;
        float w = font.Measure(shown);
        font.Draw(c.R, shown, r.CenterX - w * 0.5f, r.CenterY - font.Height * 0.5f, ink);

        bool clicked = enabled && c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f, 1.05f);
        return clicked;
    }

    void DrawAppBar(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.Rgb(0x121212));

        c.F.Ui.Draw(c.R, L.T(_scientific ? "calc.scientific" : "calc.standard"),
                    bar.X + 28, bar.CenterY - c.F.Ui.Height * 0.5f, Color.Rgba(0xFFFFFF, 160));

        float x = bar.Right - 120;
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.Calculator,
                      _scientific ? "calc.standard" : "calc.scientific"))
        {
            _scientific = !_scientific;
            c.Sound(Sfx.Navigate, 0.5f);
        }

        x -= 110;
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.TextFile, "calc.copy"))
        {
            Clipboard.SetText(_calc.Entry);
            c.Sound(Sfx.Info, 0.5f);
        }

        x -= 110;
        // The other calculator is one button away, which is the point of having
        // both of them.
        if (BarButton(c, new Rect(x, bar.Y + 8, 100, bar.H - 16), IconId.Display, "calc8.classic"))
        {
            Shell.Launch(c, "calculator", null);
            Close();
        }
    }

    bool BarButton(UiContext c, Rect r, IconId icon, string key)
    {
        bool hot = c.Hovering(r);
        if (hot) c.R.FillRect(r, Color.Rgba(0xFFFFFF, 35));

        var ring = new Rect(r.CenterX - 12, r.Y + 2, 24, 24);
        c.R.DrawCircle(ring.CenterX, ring.CenterY, 12, Color.White, 1.5f);
        Icons.Draw(c.R, icon, ring.Deflate(5));

        string label = c.F.Small.Ellipsize(L.T(key), r.W - 4);
        float w = c.F.Small.Measure(label);
        c.F.Small.Draw(c.R, label, r.CenterX - w * 0.5f, r.Bottom - c.F.Small.Height - 2, Color.White);
        return c.Clicked(r);
    }

    void Press(UiContext c, string key)
    {
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
}
