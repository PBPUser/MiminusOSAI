using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;

namespace Miminus.UI;

public sealed class MenuItem
{
    public string Text;
    public string Shortcut;
    public IconId Icon = IconId.None;
    public bool Enabled = true;
    public bool Checked;
    public bool IsRadio;
    public bool Separator;
    public bool Bold;
    public List<MenuItem> Children;
    public Action Click;

    public static MenuItem Sep() => new() { Separator = true };

    public static MenuItem Of(string text, Action click = null, IconId icon = IconId.None,
                              string shortcut = null, bool enabled = true)
        => new() { Text = text, Click = click, Icon = icon, Shortcut = shortcut, Enabled = enabled };

    public static MenuItem Sub(string text, List<MenuItem> children, IconId icon = IconId.None)
        => new() { Text = text, Children = children, Icon = icon };

    public static MenuItem Check(string text, bool isChecked, Action click)
        => new() { Text = text, Checked = isChecked, Click = click };
}

/// <summary>Owns every open popup menu in the OS.
///
/// Popups have to paint over windows and dialogs, so they are not drawn where
/// they are opened from. Instead a menu is pushed onto this host and the shell
/// runs it near the end of the frame, after all windows. Submenus stack, and
/// clicking anywhere outside the chain closes it.</summary>
public sealed class MenuHost
{
    sealed class Level
    {
        public List<MenuItem> Items;
        public Rect Bounds;
        public int Hover = -1;
        public int OpenChild = -1;
        public Rect ParentItemRect;
    }

    readonly List<Level> _stack = new();

    /// <summary>Set from the shell's effect switches.</summary>
    public bool Shadows = true;
    public bool Fade = true;

    double _openedAt = -1;

    /// <summary>Identifies who opened the root menu, so a menu bar can tell
    /// whether its own popup is the one showing.</summary>
    public object Owner { get; private set; }

    public bool IsOpen => _stack.Count > 0;

    /// <summary>True on the frame a menu closed, so the opener does not
    /// immediately reopen it from the same click.</summary>
    public bool JustClosed { get; private set; }

    const float ItemH = 19;
    const float SepH = 7;
    const float Gutter = 22;
    const float PadX = 6;

    /// <summary>Opens a menu at a point. <paramref name="above"/> makes that
    /// point the menu's foot rather than its head, which is what a menu hanging
    /// off a taskbar button wants: the bar is at the bottom of the screen, so
    /// the menu has to go up from it rather than down over it.</summary>
    public void Open(List<MenuItem> items, float x, float y, object owner, UiContext c,
                     float minWidth = 0, bool above = false)
    {
        _stack.Clear();
        Owner = owner;
        _openedAt = c.Time;
        Push(items, x, y, default, c, minWidth, above);
        c.Sound(Sfx.MenuOpen, 0.5f);
    }

    public void Close()
    {
        if (_stack.Count > 0) JustClosed = true;
        _stack.Clear();
        Owner = null;
    }

    void Push(List<MenuItem> items, float x, float y, Rect parentItem, UiContext c,
              float minWidth = 0, bool above = false)
    {
        var size = Measure(items, c);
        float w = MathF.Max(size.X, minWidth), h = size.Y;

        // Growing upwards from the point given, when that is what was asked
        // for and there is room for it.
        if (above) y = MathF.Max(0, y - h);

        // Keep the popup on screen; flip rather than clamp when it would hang off.
        if (x + w > c.ScreenW) x = MathF.Max(0, parentItem.W > 0 ? parentItem.X - w : c.ScreenW - w);
        if (y + h > c.ScreenH) y = MathF.Max(0, c.ScreenH - h);

        _stack.Add(new Level
        {
            Items = items,
            Bounds = new Rect(MathF.Round(x), MathF.Round(y), w, h),
            ParentItemRect = parentItem,
        });
    }

    static System.Numerics.Vector2 Measure(List<MenuItem> items, UiContext c)
    {
        float textW = 0, shortW = 0, h = 2;
        bool anySub = false;
        foreach (var it in items)
        {
            if (it.Separator) { h += SepH; continue; }
            h += ItemH;
            textW = MathF.Max(textW, c.F.Ui.Measure(W.StripAccess(it.Text)));
            if (!string.IsNullOrEmpty(it.Shortcut)) shortW = MathF.Max(shortW, c.F.Ui.Measure(it.Shortcut));
            if (it.Children != null) anySub = true;
        }
        float w = Gutter + textW + PadX * 2 + (shortW > 0 ? shortW + 24 : 0) + (anySub ? 16 : 0);
        return new System.Numerics.Vector2(MathF.Max(w, 110), h);
    }

    /// <summary>Input, hit-testing and submenu expansion for the whole open chain.
    ///
    /// Runs before anything else in the frame, because an open menu sits above
    /// every window and must take the click first; <see cref="Draw"/> then paints
    /// it last, on top of everything.</summary>
    public void Update(UiContext c)
    {
        JustClosed = false;
        if (_stack.Count == 0) return;

        // Escape backs out one level at a time.
        if (c.In.KeyPressed(Keys.Escape))
        {
            if (_stack.Count > 1) _stack.RemoveAt(_stack.Count - 1);
            else Close();
            c.KeyboardHandled = true;
            c.Sound(Sfx.MenuClose, 0.4f);
            return;
        }

        // A press outside every level dismisses the chain.
        if (c.In.Pressed(MouseButton.Left) || c.In.Pressed(MouseButton.Right))
        {
            bool inside = _stack.Any(l => l.Bounds.Contains(c.MouseX, c.MouseY));
            if (!inside)
            {
                Close();
                c.MouseHandled = true;
                return;
            }
        }

        MenuItem invoked = null;

        for (int li = 0; li < _stack.Count; li++)
        {
            var lvl = _stack[li];
            bool topLevel = li == _stack.Count - 1;

            float y = lvl.Bounds.Y + 1;
            int hoverNow = -1;

            for (int i = 0; i < lvl.Items.Count; i++)
            {
                var it = lvl.Items[i];
                if (it.Separator) { y += SepH; continue; }

                var row = new Rect(lvl.Bounds.X + 1, y, lvl.Bounds.W - 2, ItemH);
                bool hot = row.Contains(c.MouseX, c.MouseY);
                if (hot) hoverNow = i;

                // Hovering an item with children opens the submenu straight away.
                if (hot && it.Enabled && it.Children != null && lvl.OpenChild != i)
                {
                    while (_stack.Count > li + 1) _stack.RemoveAt(_stack.Count - 1);
                    lvl.OpenChild = i;
                    Push(it.Children, row.Right - 3, row.Y - 1, row, c);
                }
                else if (hot && it.Enabled && it.Children == null && lvl.OpenChild != -1)
                {
                    while (_stack.Count > li + 1) _stack.RemoveAt(_stack.Count - 1);
                    lvl.OpenChild = -1;
                }

                if (hot && it.Enabled && it.Children == null && topLevel &&
                    (c.In.Pressed(MouseButton.Left) || c.In.Released(MouseButton.Left)))
                {
                    invoked = it;
                }

                y += ItemH;
            }

            lvl.Hover = hoverNow;
        }

        // The menu owns the pointer and the keyboard while it is up.
        c.MouseHandled = true;
        c.KeyboardHandled = true;

        if (invoked != null)
        {
            Close();
            c.Sound(Sfx.Click, 0.5f);
            invoked.Click?.Invoke();
        }
    }

    /// <summary>Paints the open chain. Called last in the frame so menus cover
    /// windows, the taskbar and the Start menu alike.</summary>
    public void Draw(UiContext c)
    {
        // Menu transition effect: a short fade as the popup appears.
        float alpha = 1f;
        if (Fade && _openedAt >= 0)
            alpha = (float)Math.Clamp((c.Time - _openedAt) / 0.12, 0.15, 1);

        foreach (var lvl in _stack)
        {
            if (Shadows)
                c.R.FillRect(lvl.Bounds.Offset(3, 3), c.Theme.Shadow.WithAlpha(alpha));
            c.R.FillRect(lvl.Bounds, c.Theme.MenuBack);
            c.R.DrawRect(lvl.Bounds, c.Theme.MenuBorder);
            if (!c.Theme.Flat)
                c.R.FillRect(new Rect(lvl.Bounds.X + 1, lvl.Bounds.Y + 1, Gutter - 2, lvl.Bounds.H - 2),
                             c.Theme.MenuGutter);

            float y = lvl.Bounds.Y + 1;
            for (int i = 0; i < lvl.Items.Count; i++)
            {
                var it = lvl.Items[i];
                if (it.Separator)
                {
                    float sy = MathF.Round(y + SepH * 0.5f);
                    c.R.FillRect(new Rect(lvl.Bounds.X + Gutter, sy, lvl.Bounds.W - Gutter - 4, 1),
                                 c.Theme.MenuSeparator);
                    y += SepH;
                    continue;
                }

                var row = new Rect(lvl.Bounds.X + 1, y, lvl.Bounds.W - 2, ItemH);
                bool highlight = i == lvl.Hover && it.Enabled;
                if (highlight)
                {
                    c.R.FillRect(row, c.Theme.MenuHighlight);
                    if (c.Theme.Id == ThemeId.Seven) c.R.DrawRect(row, Color.Rgb(0x7DA2CE));
                    else if (c.Theme.Id == ThemeId.Metro) c.R.FillRect(new Rect(row.X, row.Y, 3, row.H), Color.White);
                }

                Color fg = !it.Enabled ? c.Theme.TextDisabled
                         : highlight ? c.Theme.MenuHighlightText : c.Theme.Text;

                if (it.Icon != IconId.None)
                    Icons.Draw(c.R, it.Icon, new Rect(row.X + 2, row.Y + 1.5f, 16, 16));
                else if (it.Checked)
                {
                    if (it.IsRadio)
                        c.R.FillCircle(row.X + 10, row.CenterY, 3.2f, fg);
                    else
                    {
                        c.R.Line(row.X + 6, row.CenterY, row.X + 9, row.CenterY + 3.5f, fg, 2f);
                        c.R.Line(row.X + 9, row.CenterY + 3.5f, row.X + 15, row.CenterY - 4, fg, 2f);
                    }
                }

                var font = it.Bold ? c.F.UiBold : c.F.Ui;
                W.AccessLabel(c, row.X + Gutter, row.Y + (ItemH - font.Height) * 0.5f, it.Text, fg, font);

                if (!string.IsNullOrEmpty(it.Shortcut))
                    font.DrawRight(c.R, it.Shortcut,
                                   new Rect(row.X, row.Y, row.W - (it.Children != null ? 20 : 8), ItemH), fg);

                if (it.Children != null)
                    W.Arrow(c, new Rect(row.Right - 14, row.Y, 12, ItemH), 1, fg, 3f);

                y += ItemH;
            }
        }
    }

    /// <summary>True when the pointer is inside any level of the open chain.</summary>
    public bool HitTest(float x, float y) => _stack.Any(l => l.Bounds.Contains(x, y));
}

/// <summary>A horizontal menu bar (Файл / Правка / Вид …) wired to a
/// <see cref="MenuHost"/>. Once one menu is open, sliding across the bar
/// switches menus without clicking, exactly like Windows.</summary>
public sealed class MenuBar
{
    public readonly List<(string Text, Func<List<MenuItem>> Build)> Menus = new();

    public void Add(string text, Func<List<MenuItem>> build) => Menus.Add((text, build));

    public float Height(UiContext c) => c.F.Ui.Height + 6;

    public void Draw(UiContext c, Rect r, MenuHost host, object ownerKey)
    {
        var t = c.Theme;
        if (t.Flat) c.R.FillRect(r, Color.Rgb(0xF6F6F6));
        else c.R.FillRect(r, t.Face);

        float x = r.X + 2;
        for (int i = 0; i < Menus.Count; i++)
        {
            string label = Menus[i].Text;
            float w = c.F.Ui.Measure(W.StripAccess(label)) + 14;
            var item = new Rect(x, r.Y, w, r.H);

            bool isOpen = host.IsOpen && host.Owner is MenuOwner mo && mo.Key == ownerKey && mo.Index == i;
            bool hover = c.Hovering(item);

            if (isOpen || hover)
            {
                c.R.FillRect(item, isOpen ? t.MenuHighlight : t.Hot);
                if (!isOpen && !t.Flat) c.R.DrawRect(item, t.ControlBorderHot);
            }

            W.AccessLabel(c, item.X + 7, item.Y + (item.H - c.F.Ui.Height) * 0.5f, label,
                          isOpen ? t.MenuHighlightText : t.Text);

            if (c.Clicked(item))
            {
                if (isOpen) host.Close();
                else if (!host.JustClosed)
                    host.Open(Menus[i].Build(), item.X, item.Bottom, new MenuOwner(ownerKey, i), c);
            }
            else if (hover && host.IsOpen && !isOpen &&
                     host.Owner is MenuOwner other && other.Key == ownerKey)
            {
                // Slide-to-switch between top-level menus.
                host.Open(Menus[i].Build(), item.X, item.Bottom, new MenuOwner(ownerKey, i), c);
            }

            x += w;
        }
    }
}

/// <summary>Identifies which menu bar and which of its entries opened the popup.</summary>
public sealed class MenuOwner
{
    public readonly object Key;
    public readonly int Index;
    public MenuOwner(object key, int index) { Key = key; Index = index; }
}
