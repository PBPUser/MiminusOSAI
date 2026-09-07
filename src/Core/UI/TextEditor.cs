using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;

namespace Miminus.UI;

/// <summary>A multi-line plain-text editor: caret, selection, word wrap,
/// scrolling, clipboard and undo.
///
/// This is the component the joke antivirus is typed into, so it has to behave
/// like the real Notepad — the text is held as a list of lines and every edit
/// goes through <see cref="Replace"/>, which keeps the undo stack and the
/// modified flag honest.</summary>
public sealed class TextEditor
{
    readonly List<string> _lines = new() { "" };

    public int CaretLine, CaretCol;
    public int AnchorLine, AnchorCol;

    public float ScrollX, ScrollY;
    public bool WordWrap;
    public bool ReadOnly;
    public bool Modified;

    public Font Font;

    double _blinkBase;
    bool _dragging;

    readonly List<(string text, int line, int col)> _undo = new();
    const int MaxUndo = 64;

    public TextEditor(Font font) => Font = font;

    // ---- content ---------------------------------------------------------

    public string Text
    {
        get => string.Join("\r\n", _lines);
        set
        {
            _lines.Clear();
            foreach (string l in (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                _lines.Add(l);
            if (_lines.Count == 0) _lines.Add("");
            CaretLine = CaretCol = AnchorLine = AnchorCol = 0;
            ScrollX = ScrollY = 0;
            _undo.Clear();
            Modified = false;
            _contentStamp++;
        }
    }

    public int LineCount => _lines.Count;
    public string Line(int i) => i >= 0 && i < _lines.Count ? _lines[i] : "";
    public IReadOnlyList<string> Lines => _lines;

    public bool HasSelection => CaretLine != AnchorLine || CaretCol != AnchorCol;

    public string SelectedText
    {
        get
        {
            if (!HasSelection) return "";
            var (l0, c0, l1, c1) = OrderedSelection();
            if (l0 == l1) return _lines[l0][c0..c1];

            var sb = new System.Text.StringBuilder();
            sb.Append(_lines[l0][c0..]);
            for (int i = l0 + 1; i < l1; i++) { sb.Append("\r\n"); sb.Append(_lines[i]); }
            sb.Append("\r\n");
            sb.Append(_lines[l1][..c1]);
            return sb.ToString();
        }
    }

    (int l0, int c0, int l1, int c1) OrderedSelection()
    {
        if (AnchorLine < CaretLine || (AnchorLine == CaretLine && AnchorCol < CaretCol))
            return (AnchorLine, AnchorCol, CaretLine, CaretCol);
        return (CaretLine, CaretCol, AnchorLine, AnchorCol);
    }

    void ClampCaret()
    {
        CaretLine = Math.Clamp(CaretLine, 0, _lines.Count - 1);
        CaretCol = Math.Clamp(CaretCol, 0, _lines[CaretLine].Length);
        AnchorLine = Math.Clamp(AnchorLine, 0, _lines.Count - 1);
        AnchorCol = Math.Clamp(AnchorCol, 0, _lines[AnchorLine].Length);
    }

    void PushUndo()
    {
        _undo.Add((Text, CaretLine, CaretCol));
        if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var (text, line, col) = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        _lines.Clear();
        foreach (string l in text.Replace("\r\n", "\n").Split('\n')) _lines.Add(l);
        if (_lines.Count == 0) _lines.Add("");
        CaretLine = AnchorLine = line;
        CaretCol = AnchorCol = col;
        ClampCaret();
        Modified = true;
        _contentStamp++;
    }

    /// <summary>Replaces the current selection (or inserts at the caret).
    /// Every mutation funnels through here.</summary>
    public void Replace(string insert)
    {
        if (ReadOnly) return;
        PushUndo();

        if (HasSelection)
        {
            var (l0, c0, l1, c1) = OrderedSelection();
            string head = _lines[l0][..c0];
            string tail = _lines[l1][c1..];
            _lines.RemoveRange(l0, l1 - l0 + 1);
            _lines.Insert(l0, head + tail);
            CaretLine = l0;
            CaretCol = c0;
        }

        insert ??= "";
        if (insert.Length > 0)
        {
            string[] parts = insert.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string cur = _lines[CaretLine];
            string before = cur[..CaretCol];
            string after = cur[CaretCol..];

            if (parts.Length == 1)
            {
                _lines[CaretLine] = before + parts[0] + after;
                CaretCol += parts[0].Length;
            }
            else
            {
                _lines[CaretLine] = before + parts[0];
                for (int i = 1; i < parts.Length; i++)
                    _lines.Insert(CaretLine + i, parts[i]);
                CaretLine += parts.Length - 1;
                CaretCol = parts[^1].Length;
                _lines[CaretLine] += after;
            }
        }

        AnchorLine = CaretLine;
        AnchorCol = CaretCol;
        Modified = true;
        _contentStamp++;
        ClampCaret();
    }

    public void DeleteSelection()
    {
        if (!HasSelection) return;
        Replace("");
    }

    public void SelectAll()
    {
        AnchorLine = 0; AnchorCol = 0;
        CaretLine = _lines.Count - 1;
        CaretCol = _lines[CaretLine].Length;
    }

    public void MoveCaretToEnd()
    {
        CaretLine = _lines.Count - 1;
        CaretCol = _lines[CaretLine].Length;
        AnchorLine = CaretLine;
        AnchorCol = CaretCol;
    }

    /// <summary>Appends text at the very end, as the antivirus "scan" does.</summary>
    public void AppendLine(string text)
    {
        MoveCaretToEnd();
        Replace(text + "\r\n");
    }

    public int TotalChars => _lines.Sum(l => l.Length) + Math.Max(0, _lines.Count - 1) * 2;

    // ---- layout ----------------------------------------------------------

    float LineHeight => Font.Height + 2;

    /// <summary>Lines as displayed. With word wrap on, one logical line can map
    /// to several visual rows; the mapping is rebuilt whenever it is needed.</summary>
    readonly List<(int line, int start, int length)> _visual = new();
    float _visualWidth = -1;
    int _visualStamp = -1;
    int _contentStamp;

    void BuildVisual(float width)
    {
        if (!WordWrap)
        {
            if (_visual.Count == _lines.Count && _visualStamp == _contentStamp) return;
            _visual.Clear();
            for (int i = 0; i < _lines.Count; i++) _visual.Add((i, 0, _lines[i].Length));
            _visualStamp = _contentStamp;
            _visualWidth = -1;
            return;
        }

        if (MathF.Abs(width - _visualWidth) < 0.5f && _visualStamp == _contentStamp) return;
        _visual.Clear();
        _visualWidth = width;
        _visualStamp = _contentStamp;

        for (int i = 0; i < _lines.Count; i++)
        {
            string line = _lines[i];
            if (line.Length == 0) { _visual.Add((i, 0, 0)); continue; }

            int start = 0;
            while (start < line.Length)
            {
                int fit = FitCount(line, start, width);
                if (fit <= 0) fit = 1;

                // Prefer breaking at the last space inside the run.
                int len = fit;
                if (start + fit < line.Length)
                {
                    int space = line.LastIndexOf(' ', Math.Min(start + fit, line.Length - 1), fit);
                    if (space > start) len = space - start + 1;
                }
                _visual.Add((i, start, len));
                start += len;
            }
        }
    }

    int FitCount(string line, int start, float width)
    {
        float w = 0;
        int n = 0;
        for (int i = start; i < line.Length; i++)
        {
            float adv = Font.MeasureUpTo(line[i].ToString(), 1);
            if (w + adv > width) break;
            w += adv;
            n++;
        }
        return n;
    }

    int VisualRowOf(int line, int col)
    {
        for (int i = 0; i < _visual.Count; i++)
        {
            var v = _visual[i];
            if (v.line != line) continue;
            if (col >= v.start && col <= v.start + v.length) return i;
        }
        for (int i = _visual.Count - 1; i >= 0; i--)
            if (_visual[i].line == line) return i;
        return 0;
    }

    // ---- drawing and input ----------------------------------------------

    public void Draw(UiContext c, Rect area, string id)
    {
        var t = c.Theme;
        c.R.FillRect(area, ReadOnly ? t.Face : t.FieldBack);

        float lineH = LineHeight;
        bool vScroll, hScroll;
        var text = area.Deflate(2);

        BuildVisual(text.W - 4);

        float contentH = _visual.Count * lineH;
        float contentW = WordWrap ? 0 : MaxLineWidth() + 8;

        vScroll = contentH > text.H;
        hScroll = !WordWrap && contentW > text.W - (vScroll ? W.ScrollBarSize : 0);
        if (hScroll) vScroll = contentH > text.H - W.ScrollBarSize;

        var view = new Rect(text.X, text.Y,
                            text.W - (vScroll ? W.ScrollBarSize : 0),
                            text.H - (hScroll ? W.ScrollBarSize : 0));

        // Interaction first, so the caret shown is the one after this frame's input.
        HandleInput(c, view, id, lineH);

        // Editing above may have added or removed lines, which leaves the line
        // map built earlier pointing at rows that no longer exist. Rebuild before
        // anything reads it — the call is a no-op when nothing changed.
        BuildVisual(view.W - 4);
        contentH = _visual.Count * lineH;

        EnsureCaretVisible(view, lineH);

        c.R.PushClip(view);
        DrawText(c, view, lineH);
        c.R.PopClip();

        if (vScroll)
            ScrollY = W.ScrollBarV(c, id + ".vs", new Rect(view.Right, text.Y, W.ScrollBarSize, view.H),
                                   ScrollY, contentH, view.H);
        else ScrollY = 0;

        if (hScroll)
            ScrollX = W.ScrollBarH(c, id + ".hs", new Rect(text.X, view.Bottom, view.W, W.ScrollBarSize),
                                   ScrollX, contentW, view.W);
        else ScrollX = 0;

        if (vScroll && hScroll)
            c.R.FillRect(new Rect(view.Right, view.Bottom, W.ScrollBarSize, W.ScrollBarSize), t.ScrollTrack);

        c.R.DrawRect(area, t.FieldBorder);
    }

    float MaxLineWidth()
    {
        float max = 0;
        // Measuring every line each frame would be wasteful on big documents;
        // sampling the longest few by character count is close enough for a
        // horizontal scroll range.
        foreach (string l in _lines.OrderByDescending(l => l.Length).Take(24))
            max = MathF.Max(max, Font.Measure(l));
        return max;
    }

    void DrawText(UiContext c, Rect view, float lineH)
    {
        var t = c.Theme;
        int first = Math.Max(0, (int)(ScrollY / lineH));
        int last = Math.Min(_visual.Count - 1, first + (int)(view.H / lineH) + 1);

        bool hasSel = HasSelection;
        var (sl0, sc0, sl1, sc1) = hasSel ? OrderedSelection() : (0, 0, 0, 0);

        for (int vi = first; vi <= last; vi++)
        {
            var v = _visual[vi];
            // Belt and braces: never index past the document even if a caller
            // mutates the text between the rebuild and the paint.
            if (v.line < 0 || v.line >= _lines.Count) continue;

            string full = _lines[v.line];
            int segStart = Math.Clamp(v.start, 0, full.Length);
            string seg = full.Substring(segStart, Math.Clamp(v.length, 0, full.Length - segStart));

            float y = view.Y + vi * lineH - ScrollY;
            float x = view.X - ScrollX;

            // Selection highlight for the part of this row that is selected.
            if (hasSel && v.line >= sl0 && v.line <= sl1)
            {
                int rowStart = segStart, rowEnd = segStart + seg.Length;
                int a = v.line == sl0 ? Math.Max(rowStart, sc0) : rowStart;
                int b = v.line == sl1 ? Math.Min(rowEnd, sc1) : rowEnd;

                // Whole-line selections extend past the last character.
                bool trailing = v.line < sl1 && rowEnd == full.Length;

                if (b > a || trailing)
                {
                    float sx = x + Font.MeasureUpTo(full, Math.Max(a, rowStart)) - Font.MeasureUpTo(full, rowStart);
                    float sw = b > a
                        ? Font.MeasureUpTo(full, b) - Font.MeasureUpTo(full, a)
                        : 0;
                    if (trailing) sw += 5;
                    c.R.FillRect(new Rect(sx, y, MathF.Max(sw, 2), lineH), t.Selection);
                }
            }

            if (seg.Length > 0)
                Font.Draw(c.R, seg, x, y + 1, t.Text);
        }

        // Caret.
        if (!ReadOnly && c.Focus == _focusId && _visual.Count > 0 &&
            (c.Time - _blinkBase) % 1.06 < 0.53)
        {
            int row = Math.Clamp(VisualRowOf(CaretLine, CaretCol), 0, _visual.Count - 1);
            var v = _visual[row];
            if (v.line < 0 || v.line >= _lines.Count) return;
            string full = _lines[v.line];
            float cx = view.X - ScrollX
                     + Font.MeasureUpTo(full, CaretCol) - Font.MeasureUpTo(full, v.start);
            float cy = view.Y + row * lineH - ScrollY;
            c.R.FillRect(new Rect(MathF.Round(cx), cy + 1, 1.4f, lineH - 2), t.Text);
        }
    }

    void EnsureCaretVisible(Rect view, float lineH)
    {
        int row = VisualRowOf(CaretLine, CaretCol);
        float top = row * lineH;
        if (top < ScrollY) ScrollY = top;
        else if (top + lineH > ScrollY + view.H) ScrollY = top + lineH - view.H;
        if (ScrollY < 0) ScrollY = 0;

        if (!WordWrap)
        {
            float cx = Font.MeasureUpTo(_lines[CaretLine], CaretCol);
            if (cx < ScrollX + 4) ScrollX = MathF.Max(0, cx - 4);
            else if (cx > ScrollX + view.W - 12) ScrollX = cx - view.W + 12;
        }
    }

    string _focusId;

    void HandleInput(UiContext c, Rect view, string id, float lineH)
    {
        _focusId = id;

        // ---- mouse -------------------------------------------------------
        if (c.Hovering(view)) c.Cursor = CursorShape.Text;

        if (c.DoubleClicked(view))
        {
            c.Focus = id;
            SetCaretFromPoint(c.MouseX, c.MouseY, view, lineH);
            SelectWordAtCaret();
            _blinkBase = c.Time;
        }
        else if (c.Clicked(view))
        {
            c.Focus = id;
            SetCaretFromPoint(c.MouseX, c.MouseY, view, lineH);
            if (!c.In.Shift) { AnchorLine = CaretLine; AnchorCol = CaretCol; }
            _dragging = true;
            c.ActiveDrag = id;
            _blinkBase = c.Time;
        }

        if (_dragging)
        {
            if (!c.In.IsDown(MouseButton.Left)) { _dragging = false; if (c.ActiveDrag == id) c.ActiveDrag = null; }
            else
            {
                SetCaretFromPoint(c.MouseX, c.MouseY, view, lineH);
                c.MouseHandled = true;
            }
        }

        if (c.Hovering(view) && c.In.WheelDelta != 0)
        {
            ScrollY = MathF.Max(0, ScrollY - c.In.WheelDelta * lineH * 3);
            c.MouseHandled = true;
        }

        // ---- keyboard ----------------------------------------------------
        if (c.KeyboardHandled || c.Focus != id) return;

        var input = c.In;
        bool shift = input.Shift, ctrl = input.Ctrl;
        bool moved = false;

        void AfterMove()
        {
            if (!shift) { AnchorLine = CaretLine; AnchorCol = CaretCol; }
            _blinkBase = c.Time;
            moved = true;
        }

        if (input.KeyPressed(Keys.Left))
        {
            if (CaretCol > 0) CaretCol--;
            else if (CaretLine > 0) { CaretLine--; CaretCol = _lines[CaretLine].Length; }
            AfterMove();
        }
        else if (input.KeyPressed(Keys.Right))
        {
            if (CaretCol < _lines[CaretLine].Length) CaretCol++;
            else if (CaretLine < _lines.Count - 1) { CaretLine++; CaretCol = 0; }
            AfterMove();
        }
        else if (input.KeyPressed(Keys.Up))
        {
            if (CaretLine > 0) { CaretLine--; CaretCol = Math.Min(CaretCol, _lines[CaretLine].Length); }
            AfterMove();
        }
        else if (input.KeyPressed(Keys.Down))
        {
            if (CaretLine < _lines.Count - 1) { CaretLine++; CaretCol = Math.Min(CaretCol, _lines[CaretLine].Length); }
            AfterMove();
        }
        else if (input.KeyPressed(Keys.Home))
        {
            if (ctrl) { CaretLine = 0; CaretCol = 0; } else CaretCol = 0;
            AfterMove();
        }
        else if (input.KeyPressed(Keys.End))
        {
            if (ctrl) { CaretLine = _lines.Count - 1; CaretCol = _lines[CaretLine].Length; }
            else CaretCol = _lines[CaretLine].Length;
            AfterMove();
        }
        else if (input.KeyPressed(Keys.PageUp))
        {
            int step = Math.Max(1, (int)(view.H / lineH) - 1);
            CaretLine = Math.Max(0, CaretLine - step);
            CaretCol = Math.Min(CaretCol, _lines[CaretLine].Length);
            AfterMove();
        }
        else if (input.KeyPressed(Keys.PageDown))
        {
            int step = Math.Max(1, (int)(view.H / lineH) - 1);
            CaretLine = Math.Min(_lines.Count - 1, CaretLine + step);
            CaretCol = Math.Min(CaretCol, _lines[CaretLine].Length);
            AfterMove();
        }
        else if (ctrl && input.KeyPressed(Keys.A)) { SelectAll(); moved = true; }
        else if (ctrl && input.KeyPressed(Keys.C)) { if (HasSelection) Clipboard.SetText(SelectedText); moved = true; }
        else if (ctrl && input.KeyPressed(Keys.X))
        {
            if (HasSelection && !ReadOnly) { Clipboard.SetText(SelectedText); DeleteSelection(); }
            moved = true;
        }
        else if (ctrl && input.KeyPressed(Keys.V))
        {
            if (!ReadOnly) { Replace(Clipboard.GetText()); _contentStamp++; c.Sound(Sfx.Typewriter, 0.5f); }
            moved = true;
        }
        else if (ctrl && input.KeyPressed(Keys.Z)) { Undo(); _contentStamp++; moved = true; }
        else if (input.KeyPressed(Keys.Back) && !ReadOnly)
        {
            if (HasSelection) DeleteSelection();
            else if (CaretCol > 0) { CaretCol--; AnchorCol = CaretCol; AnchorLine = CaretLine; PushUndoAndRemoveChar(); }
            else if (CaretLine > 0)
            {
                PushUndo();
                int prevLen = _lines[CaretLine - 1].Length;
                _lines[CaretLine - 1] += _lines[CaretLine];
                _lines.RemoveAt(CaretLine);
                CaretLine--;
                CaretCol = prevLen;
                AnchorLine = CaretLine; AnchorCol = CaretCol;
                Modified = true;
                _contentStamp++;
            }
            _contentStamp++;
            moved = true;
            c.Sound(Sfx.Typewriter, 0.35f, 0.9f);
        }
        else if (input.KeyPressed(Keys.Delete) && !ReadOnly)
        {
            if (HasSelection) DeleteSelection();
            else if (CaretCol < _lines[CaretLine].Length) PushUndoAndRemoveChar();
            else if (CaretLine < _lines.Count - 1)
            {
                PushUndo();
                _lines[CaretLine] += _lines[CaretLine + 1];
                _lines.RemoveAt(CaretLine + 1);
                Modified = true;
                _contentStamp++;
            }
            _contentStamp++;
            moved = true;
        }

        // ---- typed characters --------------------------------------------
        if (!ReadOnly && input.TypedChars.Count > 0)
        {
            foreach (char ch in input.TypedChars)
            {
                if (ch == '\r') Replace("\n");
                else if (ch == '\t') Replace("    ");
                else if (ch >= ' ') Replace(ch.ToString());
            }
            _contentStamp++;
            _blinkBase = c.Time;
            moved = true;
            c.Sound(Sfx.Typewriter, 0.4f, 0.95f + (float)Random.Shared.NextDouble() * 0.12f);
        }

        if (moved) { ClampCaret(); c.KeyboardHandled = true; }
    }

    void PushUndoAndRemoveChar()
    {
        PushUndo();
        string l = _lines[CaretLine];
        if (CaretCol < l.Length) _lines[CaretLine] = l.Remove(CaretCol, 1);
        Modified = true;
        _contentStamp++;
    }

    void SetCaretFromPoint(float mx, float my, Rect view, float lineH)
    {
        int row = (int)MathF.Floor((my - view.Y + ScrollY) / lineH);
        row = Math.Clamp(row, 0, Math.Max(0, _visual.Count - 1));
        if (_visual.Count == 0) { CaretLine = CaretCol = 0; return; }

        var v = _visual[row];
        string full = _lines[v.line];
        float localX = mx - view.X + ScrollX + Font.MeasureUpTo(full, v.start);

        int col = Font.IndexAtX(full, localX);
        col = Math.Clamp(col, v.start, v.start + v.length);

        CaretLine = v.line;
        CaretCol = Math.Clamp(col, 0, full.Length);
    }

    void SelectWordAtCaret()
    {
        string l = _lines[CaretLine];
        if (l.Length == 0) return;

        int start = Math.Clamp(CaretCol, 0, l.Length - 1);
        while (start > 0 && !char.IsWhiteSpace(l[start - 1])) start--;
        int end = Math.Clamp(CaretCol, 0, l.Length);
        while (end < l.Length && !char.IsWhiteSpace(l[end])) end++;

        AnchorLine = CaretLine; AnchorCol = start;
        CaretCol = end;
    }

    /// <summary>Finds <paramref name="needle"/> and selects it, wrapping around.
    /// Backs the Find command and the antivirus's word-swap gag.</summary>
    public bool Find(string needle, bool matchCase)
    {
        if (string.IsNullOrEmpty(needle)) return false;
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (int pass = 0; pass < 2; pass++)
        {
            int startLine = pass == 0 ? CaretLine : 0;
            for (int i = startLine; i < _lines.Count; i++)
            {
                int from = (pass == 0 && i == CaretLine) ? CaretCol : 0;
                if (from > _lines[i].Length) continue;
                int idx = _lines[i].IndexOf(needle, from, cmp);
                if (idx >= 0)
                {
                    AnchorLine = i; AnchorCol = idx;
                    CaretLine = i; CaretCol = idx + needle.Length;
                    return true;
                }
            }
        }
        return false;
    }

    public int ReplaceAll(string needle, string replacement, bool matchCase)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        PushUndo();
        var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int count = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            int idx = 0;
            while ((idx = _lines[i].IndexOf(needle, idx, cmp)) >= 0)
            {
                _lines[i] = _lines[i].Remove(idx, needle.Length).Insert(idx, replacement);
                idx += replacement.Length;
                count++;
            }
        }
        if (count > 0) { Modified = true; _contentStamp++; ClampCaret(); }
        return count;
    }

    public void MarkDirty() => _contentStamp++;
}
