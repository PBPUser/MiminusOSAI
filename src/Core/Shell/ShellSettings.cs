using Miminus.Graphics;

namespace Miminus.Shell;

/// <summary>The visual-effect switches behind Display Properties → Оформление →
/// Эффекты.
///
/// These are not decoration: each one changes how the shell actually draws.
/// Turning off "show window contents while dragging" really does fall back to
/// an outline drag, and the font smoothing setting re-rasterises every glyph.</summary>
public sealed class ShellSettings
{
    /// <summary>Menus and tooltips fade in rather than appearing instantly.</summary>
    public bool MenuTransition = true;

    /// <summary>Fade (true) or unroll (false) — the two options XP offered.</summary>
    public bool MenuFade = true;

    /// <summary>48px desktop icons instead of 32px.</summary>
    public bool LargeIcons;

    /// <summary>Menus cast a drop shadow.</summary>
    public bool MenuShadows = true;

    /// <summary>Drag the whole window, or just an outline of it.</summary>
    public bool ShowWindowContentsWhileDragging = true;

    /// <summary>Underline access keys only after Alt is pressed.</summary>
    public bool HideAccessKeys;

    public FontSmoothing Smoothing
    {
        get => Font.Smoothing;
        set => Font.Smoothing = value;
    }

    public float DesktopIconSize => LargeIcons ? 48 : 32;
    public float DesktopCellSize => LargeIcons ? 96 : 76;
}
