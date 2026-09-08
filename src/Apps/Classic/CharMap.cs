using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Таблица символов» — the grid of glyphs, the box you collect them
/// in, and the Copy button.
///
/// It is the one program in the system that shows the font machinery doing its
/// job: every cell here is a glyph rasterised through GDI into the same atlas
/// the rest of the interface draws from, so the Cyrillic block works for the
/// same reason «Пуск» does. The magnifier that follows the pointer is the one
/// detail everybody remembers about the original.</summary>
public sealed class CharMapWindow : OsWindow
{
    /// <summary>The blocks the picker offers, as first and last code point.</summary>
    static readonly (string key, int first, int last)[] Blocks =
    {
        ("charmap.block_latin", 0x0020, 0x007E),
        ("charmap.block_latin_supplement", 0x00A0, 0x00FF),
        ("charmap.block_cyrillic", 0x0400, 0x045F),
        ("charmap.block_punctuation", 0x2010, 0x203A),
        ("charmap.block_symbols", 0x2190, 0x21FF),
        ("charmap.block_box", 0x2500, 0x257F),
    };

    int _block = 2;          // Cyrillic: this is a Russian system
    int _selected = -1;
    string _collected = "";
    float _scroll;

    const int Columns = 20;

    public override string Title => L.T("charmap.title");
    public override float MinWidth => 460;
    public override float MinHeight => 360;

    public CharMapWindow()
    {
        Icon = IconId.TextFile;
        Bounds = new Rect(0, 0, 540, 430);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    (int first, int last) Range => (Blocks[_block].first, Blocks[_block].last);

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client.Deflate(10);

        // ---- the block picker ---------------------------------------------
        var top = area.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("charmap.block"), top.X, top.CenterY - c.F.Ui.Height * 0.5f, t.Text);

        var combo = new Rect(top.X + 70, top.Y, 240, 22);
        var names = Blocks.Select(b => L.T(b.key)).ToList();
        int block = _block;
        if (W.ComboBox(c, Id + ".block", combo, names, ref block) && block != _block)
        {
            _block = block;
            _selected = -1;
            _scroll = 0;
        }

        area.CutTop(8);

        // ---- the collected string and its buttons --------------------------
        var bottom = area.CutBottom(56);

        // ---- the grid --------------------------------------------------------
        DrawGrid(c, area);

        // ---- what has been picked up -----------------------------------------
        c.F.Ui.Draw(c.R, L.T("charmap.characters_to_copy"), bottom.X, bottom.Y + 4, t.Text);

        var field = new Rect(bottom.X + 160, bottom.Y, bottom.W - 160, 22);
        W.SunkenField(c, field);
        c.R.PushClip(field.Deflate(3));
        c.F.Ui.Draw(c.R, _collected, field.X + 4, field.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        c.R.PopClip();

        var row = new Rect(bottom.X, bottom.Bottom - 26, bottom.W, 24);
        float bw = 96;

        if (W.Button(c, Id + ".select", new Rect(row.X + 160, row.Y, bw, 24),
                     L.T("charmap.select"), _selected >= 0))
        {
            _collected += char.ConvertFromUtf32(Range.first + _selected);
            c.Sound(Sfx.Click, 0.5f);
        }

        if (W.Button(c, Id + ".copy", new Rect(row.X + 160 + bw + 8, row.Y, bw, 24),
                     L.T("charmap.copy"), _collected.Length > 0))
        {
            Clipboard.SetText(_collected);
            c.Sound(Sfx.Info, 0.6f);
        }

        if (W.Button(c, Id + ".clear", new Rect(row.X + 160 + (bw + 8) * 2, row.Y, bw, 24),
                     L.T("charmap.clear"), _collected.Length > 0))
        {
            _collected = "";
            c.Sound(Sfx.Click, 0.5f);
        }

        // The status line names the character the pointer is over.
        if (_selected >= 0)
        {
            int code = Range.first + _selected;
            c.F.Small.Draw(c.R, L.F("charmap.code_point", code.ToString("X4")),
                           row.X, row.Y + 6, t.TextDisabled);
        }
    }

    void DrawGrid(UiContext c, Rect area)
    {
        var t = c.Theme;
        W.SunkenField(c, area);
        var inner = area.Deflate(2);
        c.R.FillRect(inner, t.FieldBack);

        var (first, last) = Range;
        int count = last - first + 1;
        int rows = (count + Columns - 1) / Columns;

        float cell = inner.W / Columns;
        float contentH = rows * cell;

        if (contentH > inner.H)
        {
            var bar = new Rect(inner.Right - W.ScrollBarSize, inner.Y, W.ScrollBarSize, inner.H);
            _scroll = W.ScrollBarV(c, Id + ".scroll", bar, _scroll, contentH, inner.H);
            inner.W -= W.ScrollBarSize;
            cell = inner.W / Columns;
            contentH = rows * cell;
        }
        else _scroll = 0;

        if (c.Hovering(inner) && MathF.Abs(c.In.WheelDelta) > 0.01f)
        {
            _scroll = Math.Clamp(_scroll - c.In.WheelDelta * 40, 0, MathF.Max(0, contentH - inner.H));
            c.In.WheelDelta = 0;
        }

        c.R.PushClip(inner);
        int hovered = -1;

        for (int i = 0; i < count; i++)
        {
            var r = new Rect(inner.X + (i % Columns) * cell,
                             inner.Y - _scroll + (i / Columns) * cell, cell, cell);
            if (r.Bottom < inner.Y || r.Y > inner.Bottom) continue;

            bool hot = c.Hovering(r);
            if (hot) hovered = i;

            if (i == _selected) c.R.FillRect(r, t.Selection);
            else if (hot) c.R.FillRect(r, t.Hot);

            c.R.FillRect(new Rect(r.X, r.Bottom - 1, r.W, 1), Color.Rgb(0xE0E0E0));
            c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), Color.Rgb(0xE0E0E0));

            string glyph = char.ConvertFromUtf32(first + i);
            c.F.Ui.DrawCentered(c.R, glyph, r, i == _selected ? t.SelectionText : t.Text);

            if (c.Clicked(r)) { _selected = i; c.SoundAt(Sfx.Click, r, 0.35f); }
            else if (c.DoubleClicked(r))
            {
                _selected = i;
                _collected += glyph;
            }
        }
        c.R.PopClip();

        // The magnifier: the character under the pointer, four times over.
        if (hovered >= 0)
        {
            var big = new Rect(c.MouseX - 26, c.MouseY - 62, 52, 56);
            big.X = Math.Clamp(big.X, inner.X, inner.Right - big.W);
            big.Y = MathF.Max(inner.Y, big.Y);

            c.R.FillRect(big.Offset(2, 2), Color.Rgba(0x000000, 50));
            c.R.FillRect(big, Color.White);
            c.R.DrawRect(big, Color.Black);
            c.F.Big.DrawCentered(c.R, char.ConvertFromUtf32(first + hovered), big, Color.Black);
        }
    }
}
