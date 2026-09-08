using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;

namespace Miminus.UI;

/// <summary>The stock control set, drawn to match the active theme.
///
/// Everything here is immediate mode: one call draws the control and returns
/// what the user did to it. Controls needing memory between frames stash it in
/// <see cref="UiContext.State{T}"/> under the id string the caller passes.</summary>
public static class W
{
    // ---- bevels ----------------------------------------------------------

    /// <summary>Classic two-tone 3D edge. Luna uses it for status bars and
    /// group boxes; the Classic theme uses it for nearly everything.</summary>
    public static void Bevel(UiContext c, Rect r, bool raised, float thickness = 1)
    {
        Color hi = raised ? Color.White : Color.Rgb(0x808080);
        Color lo = raised ? Color.Rgb(0x808080) : Color.White;
        var g = c.R;
        g.FillRect(new Rect(r.X, r.Y, r.W, thickness), hi);
        g.FillRect(new Rect(r.X, r.Y, thickness, r.H), hi);
        g.FillRect(new Rect(r.X, r.Bottom - thickness, r.W, thickness), lo);
        g.FillRect(new Rect(r.Right - thickness, r.Y, thickness, r.H), lo);
    }

    public static void SunkenField(UiContext c, Rect r)
    {
        c.R.FillRect(r, c.Theme.FieldBack);
        c.R.DrawRect(r, c.Theme.FieldBorder);
    }

    // ---- text ------------------------------------------------------------

    public static void Label(UiContext c, Rect r, string text, bool enabled = true, Font font = null)
    {
        font ??= c.F.Ui;
        font.Draw(c.R, text, r.X, r.Y + (r.H - font.Height) * 0.5f,
                  enabled ? c.Theme.Text : c.Theme.TextDisabled);
    }

    public static void LabelCentered(UiContext c, Rect r, string text, bool enabled = true, Font font = null)
    {
        font ??= c.F.Ui;
        font.DrawCentered(c.R, text, r, enabled ? c.Theme.Text : c.Theme.TextDisabled);
    }

    /// <summary>Draws a label with the character after '&amp;' underlined, the way
    /// Windows renders access keys.</summary>
    public static void AccessLabel(UiContext c, float x, float y, string text, Color color, Font font = null)
    {
        font ??= c.F.Ui;
        float pen = x;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                float w = font.Measure(next.ToString());
                font.Draw(c.R, next.ToString(), pen, y, color);
                c.R.FillRect(new Rect(pen, y + font.Ascent + 1, w, 1), color);
                pen += w;
                i++;
                continue;
            }
            string s = text[i].ToString();
            font.Draw(c.R, s, pen, y, color);
            pen += font.Measure(s);
        }
    }

    public static string StripAccess(string text) => text.Replace("&", "");

    // ---- buttons ---------------------------------------------------------

    public static bool Button(UiContext c, string id, Rect r, string text, bool enabled = true,
                              IconId icon = IconId.None, bool defaultButton = false)
    {
        bool hover = enabled && c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left) && c.ActiveDrag == null;
        bool clicked = enabled && c.Clicked(r);

        DrawButtonFace(c, r, enabled, hover, held, defaultButton);

        float tw = c.F.Ui.Measure(StripAccess(text));
        float iw = icon != IconId.None ? r.H - 8 : 0;
        float total = tw + (iw > 0 ? iw + 4 : 0);
        float tx = r.X + (r.W - total) * 0.5f + (held ? 1 : 0);
        float ty = r.Y + (r.H - c.F.Ui.Height) * 0.5f + (held ? 1 : 0);

        if (iw > 0)
        {
            Icons.Draw(c.R, icon, new Rect(tx, r.Y + 4 + (held ? 1 : 0), iw, iw));
            tx += iw + 4;
        }
        AccessLabel(c, tx, ty, text, enabled ? c.Theme.Text : c.Theme.TextDisabled);

        if (clicked) c.SoundAt(Sfx.Click, r, 0.5f);
        return clicked;
    }

    public static void DrawButtonFace(UiContext c, Rect r, bool enabled, bool hover, bool held, bool defaultButton = false)
    {
        var t = c.Theme;
        var g = c.R;

        if (t.Id == ThemeId.Classic)
        {
            g.FillRect(r, t.Face);
            Bevel(c, r, !held);
            if (defaultButton) g.DrawRect(r.Inflate(1), Color.Black);
            return;
        }

        Color top = held ? t.FaceDark : t.FaceLight;
        Color bottom = held ? t.FaceLight : t.FaceDark;
        Color border = !enabled ? Color.Rgb(0xC0C0C0)
                     : hover ? t.ControlBorderHot
                     : defaultButton ? t.Accent
                     : t.ControlBorder;

        if (t.Id == ThemeId.Seven)
        {
            top = held ? Color.Rgb(0xC2E0F5) : hover ? Color.Rgb(0xEAF6FD) : t.FaceLight;
            bottom = held ? Color.Rgb(0xA8D4F0) : hover ? Color.Rgb(0xC4E5F6) : t.FaceDark;
        }
        else if (hover && enabled)
        {
            top = Color.Rgb(0xFFFFFF);
            bottom = Color.Rgb(0xFFE0A8);
        }

        g.RoundedRectV(r, 3, top, bottom, border, 1);
        if (!held && enabled && t.Id != ThemeId.Seven)
            g.FillRect(new Rect(r.X + 2, r.Y + 1, r.W - 4, 1), Color.Rgba(0xFFFFFF, 200));
    }

    /// <summary>A borderless button that only shows a frame on hover — toolbars
    /// and title-bar-adjacent controls use this.</summary>
    public static bool FlatButton(UiContext c, string id, Rect r, string text, bool enabled = true,
                                  IconId icon = IconId.None, bool active = false)
    {
        bool hover = enabled && c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);
        bool clicked = enabled && c.Clicked(r);

        if (active || held) { c.R.FillRect(r, c.Theme.Hot); c.R.DrawRect(r, c.Theme.ControlBorderHot); }
        else if (hover) { c.R.FillRect(r, c.Theme.Hot.WithAlpha((byte)90)); c.R.DrawRect(r, c.Theme.ControlBorderHot.WithAlpha((byte)160)); }

        float pen = r.X + 4;
        if (icon != IconId.None)
        {
            float s = MathF.Min(r.H - 4, 20);
            Icons.Draw(c.R, icon, new Rect(pen, r.Y + (r.H - s) * 0.5f, s, s));
            pen += s + 3;
        }
        if (!string.IsNullOrEmpty(text))
            c.F.Ui.Draw(c.R, text, pen, r.Y + (r.H - c.F.Ui.Height) * 0.5f,
                        enabled ? c.Theme.Text : c.Theme.TextDisabled);

        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);
        return clicked;
    }

    // ---- check and radio -------------------------------------------------

    public static bool CheckBox(UiContext c, string id, Rect r, string text, ref bool value, bool enabled = true)
    {
        bool clicked = enabled && c.Clicked(r);
        if (clicked) { value = !value; c.SoundAt(Sfx.Click, r, 0.45f); }

        float boxSize = 13;
        var box = new Rect(r.X, r.Y + (r.H - boxSize) * 0.5f, boxSize, boxSize);
        bool hover = enabled && c.Hovering(r);

        c.R.FillRect(box, enabled ? c.Theme.FieldBack : c.Theme.Face);
        c.R.DrawRect(box, hover ? c.Theme.ControlBorderHot : c.Theme.FieldBorder);

        if (value)
        {
            Color tick = enabled ? c.Theme.Text : c.Theme.TextDisabled;
            c.R.Line(box.X + 3, box.CenterY, box.X + 5.5f, box.Bottom - 3.5f, tick, 2f);
            c.R.Line(box.X + 5.5f, box.Bottom - 3.5f, box.Right - 2.5f, box.Y + 3, tick, 2f);
        }

        if (!string.IsNullOrEmpty(text))
            AccessLabel(c, box.Right + 5, r.Y + (r.H - c.F.Ui.Height) * 0.5f, text,
                        enabled ? c.Theme.Text : c.Theme.TextDisabled);
        return clicked;
    }

    public static bool Radio(UiContext c, string id, Rect r, string text, bool selected, bool enabled = true)
    {
        bool clicked = enabled && c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.45f);

        float d = 12;
        float cx = r.X + d * 0.5f, cy = r.Y + r.H * 0.5f;
        bool hover = enabled && c.Hovering(r);

        c.R.FillCircle(cx, cy, d * 0.5f, enabled ? c.Theme.FieldBack : c.Theme.Face);
        c.R.DrawCircle(cx, cy, d * 0.5f, hover ? c.Theme.ControlBorderHot : c.Theme.FieldBorder);
        if (selected) c.R.FillCircle(cx, cy, 3, enabled ? c.Theme.Text : c.Theme.TextDisabled);

        if (!string.IsNullOrEmpty(text))
            AccessLabel(c, r.X + d + 5, r.Y + (r.H - c.F.Ui.Height) * 0.5f, text,
                        enabled ? c.Theme.Text : c.Theme.TextDisabled);
        return clicked;
    }

    // ---- containers ------------------------------------------------------

    public static void GroupBox(UiContext c, Rect r, string title)
    {
        float top = r.Y + c.F.Ui.Height * 0.5f;
        var frame = new Rect(r.X, top, r.W, r.Bottom - top);
        c.R.DrawRect(frame, c.Theme.Id == ThemeId.Seven ? Color.Rgb(0xD5DFE7) : Color.Rgb(0xB4B0A4));

        if (!string.IsNullOrEmpty(title))
        {
            float w = c.F.Ui.Measure(title);
            c.R.FillRect(new Rect(r.X + 7, top - 1, w + 6, 2), c.Theme.Face);
            c.F.Ui.Draw(c.R, title, r.X + 10, r.Y, c.Theme.Text);
        }
    }

    public static void StatusBar(UiContext c, Rect r, params string[] panels)
    {
        var t = c.Theme;
        c.R.FillRect(r, t.Face);
        c.R.FillRect(new Rect(r.X, r.Y, r.W, 1), Color.Rgba(0xFFFFFF, 180));

        if (panels.Length == 0) return;
        float x = r.X + 2;
        // Last panel takes the remaining width; the others split evenly-ish.
        float unit = (r.W - 4) / Math.Max(1, panels.Length);
        for (int i = 0; i < panels.Length; i++)
        {
            float w = i == panels.Length - 1 ? r.Right - 2 - x : unit;
            var cell = new Rect(x, r.Y + 2, w - 2, r.H - 4);
            if (t.Id == ThemeId.Classic || t.Id != ThemeId.Seven) Bevel(c, cell, false);
            c.R.PushClip(cell.Deflate(3, 0, 3, 0));
            c.F.Ui.Draw(c.R, panels[i], cell.X + 3, cell.Y + (cell.H - c.F.Ui.Height) * 0.5f, t.Text);
            c.R.PopClip();
            x += w;
        }
    }

    // ---- scrollbars ------------------------------------------------------

    sealed class ScrollState { public float DragOffset; public bool Dragging; public double RepeatAt; }

    public const float ScrollBarSize = 16;

    /// <summary>Vertical scrollbar. Returns the updated offset in content pixels.</summary>
    public static float ScrollBarV(UiContext c, string id, Rect r, float offset, float contentH, float viewH)
    {
        var t = c.Theme;
        var st = c.State<ScrollState>(id);
        float maxOffset = MathF.Max(0, contentH - viewH);
        if (maxOffset <= 0)
        {
            c.R.FillRect(r, t.ScrollTrack);
            return 0;
        }

        var up = new Rect(r.X, r.Y, r.W, ScrollBarSize);
        var down = new Rect(r.X, r.Bottom - ScrollBarSize, r.W, ScrollBarSize);
        var track = new Rect(r.X, up.Bottom, r.W, down.Y - up.Bottom);

        c.R.FillRect(r, t.ScrollTrack);

        float thumbH = MathF.Max(18, track.H * (viewH / contentH));
        float thumbY = track.Y + (track.H - thumbH) * (offset / maxOffset);
        var thumb = new Rect(track.X + 1, thumbY, track.W - 2, thumbH);

        // Arrows
        if (RepeatButton(c, id + ".up", up, st)) offset -= 24;
        if (RepeatButton(c, id + ".down", down, st)) offset += 24;
        Arrow(c, up, 0);
        Arrow(c, down, 2);

        // Thumb drag
        if (c.Clicked(thumb))
        {
            st.Dragging = true;
            st.DragOffset = c.MouseY - thumb.Y;
            c.ActiveDrag = id;
        }
        else if (c.Clicked(track))
        {
            offset += c.MouseY < thumb.Y ? -viewH : viewH;
        }

        if (st.Dragging)
        {
            if (!c.In.IsDown(MouseButton.Left)) { st.Dragging = false; if (c.ActiveDrag == id) c.ActiveDrag = null; }
            else
            {
                float y = c.MouseY - st.DragOffset;
                float frac = track.H - thumbH <= 0 ? 0 : (y - track.Y) / (track.H - thumbH);
                offset = Math.Clamp(frac, 0, 1) * maxOffset;
                c.MouseHandled = true;
            }
        }

        bool hot = st.Dragging || c.Hovering(thumb);
        if (t.Id == ThemeId.Seven)
            c.R.FillRect(thumb.Deflate(2), hot ? t.ScrollThumbHot : t.ScrollThumb);
        else
        {
            c.R.FillRectH(thumb, t.FaceLight, hot ? t.ScrollThumbHot : t.ScrollThumb);
            c.R.DrawRect(thumb, t.ControlBorder);
        }

        return Math.Clamp(offset, 0, maxOffset);
    }

    /// <summary>Horizontal scrollbar. Returns the updated offset in content pixels.</summary>
    public static float ScrollBarH(UiContext c, string id, Rect r, float offset, float contentW, float viewW)
    {
        var t = c.Theme;
        var st = c.State<ScrollState>(id);
        float maxOffset = MathF.Max(0, contentW - viewW);
        if (maxOffset <= 0) { c.R.FillRect(r, t.ScrollTrack); return 0; }

        var left = new Rect(r.X, r.Y, ScrollBarSize, r.H);
        var right = new Rect(r.Right - ScrollBarSize, r.Y, ScrollBarSize, r.H);
        var track = new Rect(left.Right, r.Y, right.X - left.Right, r.H);

        c.R.FillRect(r, t.ScrollTrack);

        float thumbW = MathF.Max(18, track.W * (viewW / contentW));
        float thumbX = track.X + (track.W - thumbW) * (offset / maxOffset);
        var thumb = new Rect(thumbX, track.Y + 1, thumbW, track.H - 2);

        if (RepeatButton(c, id + ".left", left, st)) offset -= 24;
        if (RepeatButton(c, id + ".right", right, st)) offset += 24;
        Arrow(c, left, 3);
        Arrow(c, right, 1);

        if (c.Clicked(thumb)) { st.Dragging = true; st.DragOffset = c.MouseX - thumb.X; c.ActiveDrag = id; }
        else if (c.Clicked(track)) offset += c.MouseX < thumb.X ? -viewW : viewW;

        if (st.Dragging)
        {
            if (!c.In.IsDown(MouseButton.Left)) { st.Dragging = false; if (c.ActiveDrag == id) c.ActiveDrag = null; }
            else
            {
                float x = c.MouseX - st.DragOffset;
                float frac = track.W - thumbW <= 0 ? 0 : (x - track.X) / (track.W - thumbW);
                offset = Math.Clamp(frac, 0, 1) * maxOffset;
                c.MouseHandled = true;
            }
        }

        bool hot = st.Dragging || c.Hovering(thumb);
        if (t.Id == ThemeId.Seven)
            c.R.FillRect(thumb.Deflate(2), hot ? t.ScrollThumbHot : t.ScrollThumb);
        else
        {
            c.R.FillRectV(thumb, t.FaceLight, hot ? t.ScrollThumbHot : t.ScrollThumb);
            c.R.DrawRect(thumb, t.ControlBorder);
        }
        return Math.Clamp(offset, 0, maxOffset);
    }

    /// <summary>A button that fires once on press and then auto-repeats while held.</summary>
    static bool RepeatButton(UiContext c, string id, Rect r, ScrollState shared)
    {
        var t = c.Theme;
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);

        if (t.Id != ThemeId.Seven)
        {
            c.R.FillRectV(r, held ? t.FaceDark : t.FaceLight, held ? t.FaceLight : t.FaceDark);
            c.R.DrawRect(r, t.ControlBorder);
        }
        else if (hover) c.R.FillRect(r, t.ScrollThumb.WithAlpha((byte)120));

        bool fired = false;
        if (c.Clicked(r)) { fired = true; shared.RepeatAt = c.Time + 0.35; }
        else if (held && c.Time >= shared.RepeatAt) { fired = true; shared.RepeatAt = c.Time + 0.05; c.MouseHandled = true; }
        return fired;
    }

    /// <summary>Small solid triangle. Direction: 0 up, 1 right, 2 down, 3 left.</summary>
    public static void Arrow(UiContext c, Rect r, int direction, Color? color = null, float size = 3.5f)
    {
        Color col = color ?? c.Theme.Text;
        float cx = MathF.Round(r.CenterX), cy = MathF.Round(r.CenterY);
        switch (direction)
        {
            case 0: c.R.FillTriangle(cx - size, cy + size * 0.6f, cx + size, cy + size * 0.6f, cx, cy - size * 0.7f, col); break;
            case 1: c.R.FillTriangle(cx - size * 0.6f, cy - size, cx - size * 0.6f, cy + size, cx + size * 0.7f, cy, col); break;
            case 2: c.R.FillTriangle(cx - size, cy - size * 0.6f, cx + size, cy - size * 0.6f, cx, cy + size * 0.7f, col); break;
            default: c.R.FillTriangle(cx + size * 0.6f, cy - size, cx + size * 0.6f, cy + size, cx - size * 0.7f, cy, col); break;
        }
    }

    // ---- list box --------------------------------------------------------

    sealed class ListState { public float Scroll; }

    /// <summary>Simple single-selection list. Returns true when the selection
    /// changed; <paramref name="activated"/> reports a double-click.</summary>
    public static bool ListBox(UiContext c, string id, Rect r, IReadOnlyList<string> items,
                               ref int selected, out bool activated, Font font = null, float rowH = 0)
    {
        font ??= c.F.Ui;
        if (rowH <= 0) rowH = font.Height + 4;
        activated = false;

        var st = c.State<ListState>(id);
        SunkenField(c, r);

        float contentH = items.Count * rowH;
        bool needScroll = contentH > r.H - 2;
        var view = new Rect(r.X + 1, r.Y + 1, r.W - 2 - (needScroll ? ScrollBarSize : 0), r.H - 2);

        if (needScroll)
            st.Scroll = ScrollBarV(c, id + ".sb", new Rect(view.Right, r.Y + 1, ScrollBarSize, r.H - 2),
                                   st.Scroll, contentH, view.H);
        else st.Scroll = 0;

        if (c.Hovering(view) && c.In.WheelDelta != 0)
        {
            st.Scroll = Math.Clamp(st.Scroll - c.In.WheelDelta * rowH * 3, 0, MathF.Max(0, contentH - view.H));
            c.MouseHandled = true;
        }

        bool changed = false;
        c.R.PushClip(view);
        for (int i = 0; i < items.Count; i++)
        {
            var row = new Rect(view.X, view.Y - st.Scroll + i * rowH, view.W, rowH);
            if (row.Bottom < view.Y || row.Y > view.Bottom) continue;

            if (i == selected)
            {
                c.R.FillRect(row, c.Theme.Selection);
                font.Draw(c.R, items[i], row.X + 4, row.Y + (rowH - font.Height) * 0.5f, c.Theme.SelectionText);
            }
            else
            {
                if (c.Hovering(row)) c.R.FillRect(row, c.Theme.Hot.WithAlpha((byte)70));
                font.Draw(c.R, items[i], row.X + 4, row.Y + (rowH - font.Height) * 0.5f, c.Theme.Text);
            }

            if (c.DoubleClicked(row)) { selected = i; changed = true; activated = true; }
            else if (c.Clicked(row) && selected != i) { selected = i; changed = true; c.SoundAt(Sfx.Tick, row, 0.3f); }
        }
        c.R.PopClip();
        return changed;
    }

    // ---- combo box -------------------------------------------------------

    sealed class ComboState
    {
        /// <summary>Index chosen in the popup, applied on the next frame because
        /// the menu callback runs after this widget has already returned.</summary>
        public int Pending = -1;
        public bool Open;
    }

    /// <summary>Drop-down list.
    ///
    /// The list is not drawn inline: it is handed to <see cref="UiContext.Menus"/>,
    /// which paints after every window and claims input before them. Anything
    /// drawn inline here would be covered by the rest of the dialog.</summary>
    public static bool ComboBox(UiContext c, string id, Rect r, IReadOnlyList<string> items, ref int selected)
    {
        var t = c.Theme;
        var st = c.State<ComboState>(id);
        bool changed = false;

        // Apply a choice made in the popup last frame.
        if (st.Pending >= 0)
        {
            if (st.Pending != selected) { selected = st.Pending; changed = true; }
            st.Pending = -1;
        }

        st.Open = c.Menus != null && c.Menus.IsOpen && c.Menus.Owner as string == id;

        SunkenField(c, r);
        var btn = new Rect(r.Right - 17, r.Y + 1, 16, r.H - 2);

        if (selected >= 0 && selected < items.Count)
        {
            c.R.PushClip(new Rect(r.X + 2, r.Y, btn.X - r.X - 4, r.H));
            c.F.Ui.Draw(c.R, items[selected], r.X + 4, r.Y + (r.H - c.F.Ui.Height) * 0.5f, t.Text);
            c.R.PopClip();
        }

        if (t.Id != ThemeId.Seven)
        {
            c.R.FillRectV(btn, t.FaceLight, t.FaceDark);
            c.R.DrawRect(btn, t.ControlBorder);
        }
        Arrow(c, btn, 2);

        if (c.Clicked(r) && c.Menus != null)
        {
            if (st.Open) c.Menus.Close();
            else if (!c.Menus.JustClosed)
            {
                var menu = new List<MenuItem>();
                for (int i = 0; i < items.Count; i++)
                {
                    int index = i;
                    menu.Add(new MenuItem
                    {
                        Text = items[i],
                        IsRadio = true,
                        Checked = i == selected,
                        Click = () => st.Pending = index,
                    });
                }
                c.Menus.Open(menu, r.X, r.Bottom, id, c, minWidth: r.W);
            }
        }

        return changed;
    }

    public static bool ComboIsOpen(UiContext c, string id) => c.State<ComboState>(id).Open;

    /// <summary>Single-line text field, used by dialogs across the shell.</summary>
    public static bool TextField(UiContext c, string id, Rect r, ref string value)
    {
        var t = c.Theme;
        bool focused = c.Focus == id;
        c.R.FillRect(r, t.FieldBack);
        c.R.DrawRect(r, focused ? t.ControlBorderHot : t.FieldBorder);

        if (c.Hovering(r)) c.Cursor = CursorShape.Text;
        if (c.Clicked(r)) { c.Focus = id; focused = true; }

        bool changed = false;
        if (focused && !c.KeyboardHandled)
        {
            foreach (char ch in c.In.TypedChars)
            {
                if (ch == '\r' || ch == '\t') continue;
                value += ch;
                changed = true;
            }
            if (c.In.KeyPressed(Keys.Back) && value.Length > 0)
            {
                value = value[..^1];
                changed = true;
            }
            if (c.In.Ctrl && c.In.KeyPressed(Keys.V)) { value += Clipboard.GetText().Replace("\r", "").Replace("\n", ""); changed = true; }
            if (changed) c.KeyboardHandled = true;
        }

        c.R.PushClip(r.Deflate(3, 0, 3, 0));
        float tw = c.F.Ui.Measure(value);
        float offset = MathF.Max(0, tw - (r.W - 10));
        c.F.Ui.Draw(c.R, value, r.X + 4 - offset, r.Y + (r.H - c.F.Ui.Height) * 0.5f, t.Text);
        if (focused && (c.Time % 1.06) < 0.53)
            c.R.FillRect(new Rect(r.X + 4 - offset + tw, r.Y + 3, 1.4f, r.H - 6), t.Text);
        c.R.PopClip();

        return changed;
    }

    /// <summary>What an in-place rename box wants the caller to do next.</summary>
    public enum RenameResult { Editing, Commit, Cancel }

    sealed class RenameState { public bool SelectAll = true; public bool Claimed; }

    /// <summary>The box that appears over a name when a folder or file is
    /// renamed in place.
    ///
    /// It behaves the way the shell's own does: it opens with the whole name
    /// selected so the first character typed replaces it, Enter accepts,
    /// Escape abandons, and clicking anywhere else accepts as well. Keyboard
    /// input is claimed while it is open so Delete and Enter do not also reach
    /// the view underneath.</summary>
    public static RenameResult RenameBox(UiContext c, string id, Rect r, ref string value)
    {
        var t = c.Theme;
        var st = c.State<RenameState>(id);

        // The box takes focus the frame it appears, and keeps it.
        if (!st.Claimed) { st.Claimed = true; c.Focus = id; }

        c.R.FillRect(r, t.FieldBack);
        c.R.DrawRect(r, t.ControlBorderHot);
        if (c.Hovering(r)) c.Cursor = CursorShape.Text;

        float textY = r.Y + (r.H - c.F.Ui.Height) * 0.5f;
        var result = RenameResult.Editing;

        if (!c.KeyboardHandled)
        {
            foreach (char ch in c.In.TypedChars)
            {
                if (ch < ' ') continue;
                if (st.SelectAll) { value = ""; st.SelectAll = false; }
                value += ch;
            }

            if (c.In.KeyPressed(Keys.Back))
            {
                if (st.SelectAll) { value = ""; st.SelectAll = false; }
                else if (value.Length > 0) value = value[..^1];
            }

            if (c.In.Ctrl && c.In.KeyPressed(Keys.V))
            {
                if (st.SelectAll) { value = ""; st.SelectAll = false; }
                value += Clipboard.GetText().Replace("\r", "").Replace("\n", "");
            }

            if (c.In.KeyPressed(Keys.Enter)) result = RenameResult.Commit;
            else if (c.In.KeyPressed(Keys.Escape)) result = RenameResult.Cancel;

            c.KeyboardHandled = true;
        }

        // A click inside keeps editing and drops the selection; a click outside
        // finishes, which is what the shell does.
        if (c.Clicked(r)) st.SelectAll = false;
        else if (c.In.Pressed(MouseButton.Left) && !r.Contains(c.MouseX, c.MouseY))
            result = RenameResult.Commit;

        c.R.PushClip(r.Deflate(2, 0, 2, 0));
        float tw = c.F.Ui.Measure(value);
        float offset = MathF.Max(0, tw - (r.W - 8));
        float x = r.X + 3 - offset;

        if (st.SelectAll && value.Length > 0)
        {
            c.R.FillRect(new Rect(x, r.Y + 2, tw, r.H - 4), t.Selection);
            c.F.Ui.Draw(c.R, value, x, textY, t.SelectionText);
        }
        else
        {
            c.F.Ui.Draw(c.R, value, x, textY, t.Text);
            if ((c.Time % 1.06) < 0.53)
                c.R.FillRect(new Rect(x + tw, r.Y + 3, 1.4f, r.H - 6), t.Text);
        }
        c.R.PopClip();

        if (result != RenameResult.Editing) c.ForgetState(id);
        return result;
    }

    // ---- slider and progress --------------------------------------------

    sealed class SliderState { public bool Dragging; }

    public static bool Slider(UiContext c, string id, Rect r, ref float value, float min, float max, bool vertical = false)
    {
        var t = c.Theme;
        var st = c.State<SliderState>(id);
        bool changed = false;

        if (vertical)
        {
            var groove = new Rect(r.CenterX - 2, r.Y + 4, 4, r.H - 8);
            c.R.FillRect(groove, t.FaceDark);
            c.R.DrawRect(groove, t.ControlBorder);
        }
        else
        {
            var groove = new Rect(r.X + 4, r.CenterY - 2, r.W - 8, 4);
            c.R.FillRect(groove, t.FaceDark);
            c.R.DrawRect(groove, t.ControlBorder);
        }

        float frac = max - min <= 0 ? 0 : (value - min) / (max - min);
        frac = Math.Clamp(frac, 0, 1);

        Rect thumb = vertical
            ? new Rect(r.CenterX - 6, r.Y + 2 + (r.H - 20) * (1 - frac), 12, 16)
            : new Rect(r.X + 2 + (r.W - 16) * frac, r.CenterY - 8, 12, 16);

        // Where the pointer currently sits on the track, as a value.
        float AtPointer()
        {
            float f = vertical
                ? 1 - (c.MouseY - r.Y - 8) / MathF.Max(1, r.H - 16)
                : (c.MouseX - r.X - 6) / MathF.Max(1, r.W - 16);
            return min + Math.Clamp(f, 0, 1) * (max - min);
        }

        // Pressing anywhere on the track takes the thumb there straight away,
        // rather than only moving once the pointer does.
        if (c.Clicked(r))
        {
            st.Dragging = true;
            c.ActiveDrag = id;

            float pressed = AtPointer();
            if (MathF.Abs(pressed - value) > 1e-4f) { value = pressed; changed = true; }
        }

        if (st.Dragging)
        {
            if (!c.In.IsDown(MouseButton.Left)) { st.Dragging = false; if (c.ActiveDrag == id) c.ActiveDrag = null; }
            else
            {
                float nv = AtPointer();
                if (MathF.Abs(nv - value) > 1e-4f) { value = nv; changed = true; }
                c.MouseHandled = true;
            }
        }

        c.R.RoundedRectV(thumb, 2, t.FaceLight, t.FaceDark, t.ControlBorder, 1);
        return changed;
    }

    public static void ProgressBar(UiContext c, Rect r, float fraction)
    {
        var t = c.Theme;
        c.R.FillRect(r, t.FieldBack);
        c.R.DrawRect(r, t.ControlBorder);
        var inner = r.Deflate(2);
        float w = inner.W * Math.Clamp(fraction, 0, 1);
        if (t.Id == ThemeId.Seven)
            c.R.FillRectV(new Rect(inner.X, inner.Y, w, inner.H), Color.Rgb(0x2FE33F), t.ProgressFill);
        else
        {
            // Luna draws the bar as discrete blocks.
            float bw = 8;
            for (float x = inner.X; x < inner.X + w - 2; x += bw + 2)
                c.R.FillRectV(new Rect(x, inner.Y, MathF.Min(bw, inner.X + w - x), inner.H),
                              Color.Rgb(0x5CE05C), t.ProgressFill);
        }
    }

    // ---- tabs ------------------------------------------------------------

    /// <summary>Tab strip. Returns the (possibly changed) selected index and
    /// outputs the client rect below the tabs.</summary>
    public static int Tabs(UiContext c, string id, Rect r, IReadOnlyList<string> labels, int selected, out Rect body)
    {
        var t = c.Theme;
        float h = c.F.Ui.Height + 8;
        float x = r.X;

        body = new Rect(r.X, r.Y + h, r.W, r.H - h);
        c.R.FillRect(body, t.Face);
        c.R.DrawRect(body, t.ControlBorder);

        for (int i = 0; i < labels.Count; i++)
        {
            float w = c.F.Ui.Measure(labels[i]) + 20;
            bool sel = i == selected;
            var tab = new Rect(x, r.Y + (sel ? 0 : 2), w, h + (sel ? 1 : -2));
            bool hover = c.Hovering(tab);

            if (t.Id == ThemeId.Seven)
            {
                c.R.RoundedRectV(tab, 3, sel ? Color.White : hover ? Color.Rgb(0xEAF6FD) : t.FaceDark,
                                 sel ? Color.White : t.FaceDark, t.ControlBorder, 1);
            }
            else
            {
                c.R.RoundedRectV(tab, 3, sel ? Color.White : t.FaceLight,
                                 sel ? t.Face : t.FaceDark, t.ControlBorder, 1);
            }
            if (sel) c.R.FillRect(new Rect(tab.X + 1, tab.Bottom - 2, tab.W - 2, 3), t.Face);

            c.F.Ui.DrawCentered(c.R, labels[i], tab, t.Text);
            if (c.Clicked(tab)) { selected = i; c.SoundAt(Sfx.Click, tab, 0.4f); }
            x += w;
        }
        return selected;
    }

    // ---- toolbar ---------------------------------------------------------

    public static void ToolbarBackground(UiContext c, Rect r)
    {
        var t = c.Theme;
        if (t.Id == ThemeId.Seven) c.R.FillRectV(r, Color.Rgb(0xF8F8F8), Color.Rgb(0xE8E8E8));
        else c.R.FillRectV(r, Color.Rgb(0xFFFFFF), t.Face);
        c.R.FillRect(new Rect(r.X, r.Bottom - 1, r.W, 1), Color.Rgba(0x000000, 30));
    }

    public static void Separator(UiContext c, float x, float y, float h)
    {
        c.R.FillRect(new Rect(x, y, 1, h), Color.Rgba(0x000000, 45));
        c.R.FillRect(new Rect(x + 1, y, 1, h), Color.Rgba(0xFFFFFF, 160));
    }
}
