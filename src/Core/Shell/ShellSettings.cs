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

    // ---- display -----------------------------------------------------------

    /// <summary>Dots per inch the interface is drawn at. 96 is the size every
    /// window in this system was laid out for; anything else scales the whole
    /// picture, so a dialog stays the same shape and simply gets bigger.</summary>
    public int Dpi = 96;

    /// <summary>How much larger than the 96-DPI layout everything is drawn.</summary>
    public float Scale => Dpi / 96f;

    /// <summary>Frames per second the window is held to. The monitor sheet
    /// calls it a refresh rate; it is a frame cap, and it really caps.</summary>
    public int RefreshHz = 60;

    /// <summary>Bits per pixel the picture is reduced to before it is shown.
    /// 32 leaves it alone; 16 bands the gradients the way the hardware of the
    /// period did.</summary>
    public int ColorDepth = 32;

    /// <summary>Levels per channel the renderer should quantise to, or 0 for
    /// no quantisation at all.</summary>
    public float ColorLevels => ColorDepth switch
    {
        8 => 6,        // 6 levels per channel: the 216-colour web palette
        16 => 32,      // 5 bits per channel, near enough to 5-6-5
        24 => 0,
        _ => 0,
    };

    public float DesktopIconSize => LargeIcons ? 48 : 32;
    public float DesktopCellSize => LargeIcons ? 96 : 76;
}
