using Miminus.Graphics;

namespace Miminus.Shell;

/// <summary>Which side of the screen the taskbar is against.
///
/// Every Windows since 95 let the bar be dragged to any of the four, and almost
/// nobody did it on purpose — which is exactly why it has to be here.</summary>
public enum TaskbarEdge { Bottom, Top, Left, Right }

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

    // Large icons are on by default since version 8: every icon in this system
    // is a vector program evaluated at the size it is asked for, so the large
    // ones cost nothing and are what the desktop was always meant to look like.
    // The switch itself lives further down, over the three-way size.

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

    /// <summary>A locked taskbar hides its grab handles and stays where it is.
    /// Unlocked, the handles come back and the bar can be carried to any edge
    /// of the screen, which is what they were always for.</summary>
    public bool LockTaskbar = true;

    /// <summary>Which edge the taskbar is against. Kept in settings.txt, so a
    /// bar moved up the side of the screen is still up the side of the screen
    /// after a restart.</summary>
    public TaskbarEdge TaskbarEdge = TaskbarEdge.Bottom;

    /// <summary>How thick the bar is: 0 small, 1 as the theme drew it, 2 large.
    /// It scales whichever way the bar is lying, so a standing bar gets wider
    /// rather than taller.</summary>
    public int TaskbarSize = 1;

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

    /// <summary>Keep the tray icons behind a chevron until asked. Ticking it
    /// tucks all of them away at once; after that they are moved one at a time,
    /// by dragging.</summary>
    public bool HideInactiveIcons
    {
        get => HiddenTrayIcons.Count > 0;
        set
        {
            HiddenTrayIcons.Clear();
            if (value) HiddenTrayIcons.UnionWith(new[] { "Network", "Volume", "Shield" });
        }
    }

    /// <summary>Which notification icons are behind the chevron rather than in
    /// the bar. Seven let each one be dragged between the two, and this is
    /// where that lands — one name per icon, kept in settings.txt.</summary>
    public readonly HashSet<string> HiddenTrayIcons = new(StringComparer.OrdinalIgnoreCase);

    // ---- version 8 ---------------------------------------------------------

    /// <summary>Which of the two the Start button and the Windows key open.
    ///
    /// Version 8 threw the menu away and gave everybody the board; this one
    /// keeps both. The menu answers the Start button unless this is set, and
    /// either one can always reach the other: the menu has «Начальный экран» in
    /// it, and the board has «Меню Пуск» on its app bar.</summary>
    public bool UseStartScreen;

    /// <summary>The corners summon the charms and the switcher. Turning this
    /// off leaves the keyboard shortcuts working and the edges quiet.</summary>
    public bool HotCorners = true;

    /// <summary>Show the lock screen before the logon screen.</summary>
    public bool ShowLockScreen = true;

    /// <summary>How bright the picture is, 0.35 to 1. The charms bar is the
    /// only place it can be set, and it really dims: a veil is drawn over the
    /// finished frame, the last thing before the pointer.</summary>
    public float Brightness = 1f;

    // ---- специальные возможности -------------------------------------------

    /// <summary>How much larger the pointer is drawn. The one accessibility
    /// setting that costs nothing: every cursor shape is drawn from primitives,
    /// so all of them scale.</summary>
    public float CursorScale = 1f;

    /// <summary>The docked magnifier along the top of the screen. It reads the
    /// frame back out of the framebuffer around the pointer and draws it again
    /// enlarged, so it magnifies whatever is actually there.</summary>
    public bool Magnifier;
    public float MagnifierZoom = 2f;

    /// <summary>The on-screen keyboard. What it types goes into the same
    /// character queue the real keyboard fills, so every text box in the system
    /// accepts it without knowing.</summary>
    public bool OnScreenKeyboard;

    /// <summary>Экранный диктор: reads window titles and dialogs aloud through
    /// the speech engine the machine already has.</summary>
    public bool Narrator;

    /// <summary>Windows opening, closing and minimising with movement. Off is
    /// an accessibility setting and a taste, and it is honoured everywhere the
    /// shell animates.</summary>
    public bool Animations = true;

    // ---- электропитание -----------------------------------------------------

    /// <summary>0 balanced, 1 high performance, 2 power saver. The plan really
    /// sets the frame cap and the brightness — there is nothing else in this
    /// machine that costs power.</summary>
    public int PowerPlan;

    /// <summary>Minutes of no input before the screen goes dark, and before the
    /// machine locks itself. 0 means never.</summary>
    public int DisplayOffMinutes = 10;
    public int SleepMinutes = 20;

    /// <summary>What the power buttons offer first: 0 shut down, 1 sleep,
    /// 2 sign out.</summary>
    public int PowerButtonAction;

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

    /// <summary>How big the desktop icons are drawn: 0 small, 1 ordinary,
    /// 2 large. Explorer's «Вид» has offered exactly these three since 95, and
    /// the desktop's own menu offers them too — which is where this is set.
    ///
    /// <see cref="LargeIcons"/> is what the old effects sheet switched, and it
    /// still works: it is this, at either end.</summary>
    public int DesktopIcons = 2;

    public bool LargeIcons
    {
        get => DesktopIcons >= 2;
        set => DesktopIcons = value ? 2 : 1;
    }

    public float DesktopIconSize => DesktopIcons switch { 0 => 24, 1 => 32, _ => 48 };
    public float DesktopCellSize => DesktopIcons switch { 0 => 64, 1 => 76, _ => 96 };

    /// <summary>How big the Start screen's tiles are: 0 small, 1 ordinary,
    /// 2 large. Version 8 sized a tile per tile; this sizes the board, which is
    /// the setting people actually wanted.</summary>
    public int TileSizeStep = 1;

    public float StartTileSize => TileSizeStep switch { 0 => 68, 1 => 88, _ => 112 };
}
