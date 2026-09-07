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

    // ---- taskbar and Start menu ------------------------------------------

    /// <summary>A locked taskbar hides its grab handles, exactly as XP's does.
    /// Nothing here can be dragged anyway, so the handles are the whole
    /// difference — which is also true of the original.</summary>
    public bool LockTaskbar = true;

    /// <summary>Slide the taskbar off the bottom until the pointer goes looking
    /// for it. Windows get the space back while it is away.</summary>
    public bool AutoHideTaskbar;

    /// <summary>Draw the taskbar above windows rather than beneath them.</summary>
    public bool TaskbarOnTop = true;

    /// <summary>Collapse several windows of one program into a single button
    /// once the bar runs short of room.</summary>
    public bool GroupSimilar = true;

    /// <summary>Show the quick launch row beside the Start button.</summary>
    public bool ShowQuickLaunch = true;

    /// <summary>Show the clock in the notification area.</summary>
    public bool ShowClock = true;

    /// <summary>Keep the tray icons behind a chevron until asked.</summary>
    public bool HideInactiveIcons;

    public float DesktopIconSize => LargeIcons ? 48 : 32;
    public float DesktopCellSize => LargeIcons ? 96 : 76;
}
