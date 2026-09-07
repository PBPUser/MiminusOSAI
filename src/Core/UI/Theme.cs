using Miminus.Graphics;

namespace Miminus.UI;

public enum ThemeId { LunaBlue, LunaOlive, LunaSilver, Seven, Classic }

/// <summary>Colours and metrics for one visual style.
///
/// The reference videos span two looks: XP Luna in parts 1–2 and a Windows 7
/// pastiche ("Миминус 7") in part 3, so the shell is written against this
/// interface and the Display Properties applet swaps the instance live.</summary>
public sealed class Theme
{
    public ThemeId Id;
    public string NameKey;

    // ---- window chrome ---------------------------------------------------
    public Color CaptionActiveTop, CaptionActiveMid, CaptionActiveBottom;
    public Color CaptionInactiveTop, CaptionInactiveMid, CaptionInactiveBottom;
    public Color CaptionTextActive, CaptionTextInactive, CaptionTextShadow;
    public Color FrameOuter, FrameInner;
    public float CaptionHeight = 26;
    public float FrameThickness = 4;
    public float CornerRadius = 6;
    public bool GlassCaption;

    // ---- surfaces --------------------------------------------------------
    public Color Face;              // dialog/window background
    public Color FaceLight;         // top of a control gradient
    public Color FaceDark;          // bottom of a control gradient
    public Color ControlBorder;
    public Color ControlBorderHot;
    public Color FieldBack;         // text boxes, lists
    public Color FieldBorder;
    public Color Text;
    public Color TextDisabled;
    public Color TextInverted;

    // ---- selection and accents ------------------------------------------
    public Color Selection;
    public Color SelectionText;
    public Color SelectionInactive;
    public Color Hot;               // hover wash
    public Color Accent;

    // ---- menus -----------------------------------------------------------
    public Color MenuBack;
    public Color MenuGutter;
    public Color MenuBorder;
    public Color MenuHighlight;
    public Color MenuHighlightText;
    public Color MenuSeparator;

    // ---- taskbar and start ----------------------------------------------
    public Color TaskbarTop, TaskbarMid, TaskbarBottom;
    public Color TaskbarEdge;
    public Color TaskButtonFace, TaskButtonActive, TaskButtonBorder;
    public Color TaskbarText;
    public Color StartTop, StartMid, StartBottom, StartEdge;
    public Color TrayBack, TrayEdge;
    public float TaskbarHeight = 30;

    // ---- start menu ------------------------------------------------------
    public Color StartMenuHeaderTop, StartMenuHeaderBottom;
    public Color StartMenuLeft, StartMenuRight;
    public Color StartMenuFooterTop, StartMenuFooterBottom;
    public Color StartMenuBorder;

    // ---- misc ------------------------------------------------------------
    public Color Shadow;
    public Color TooltipBack, TooltipBorder, TooltipText;
    public Color ScrollTrack, ScrollThumb, ScrollThumbHot;
    public Color ProgressFill;
    public Color DesktopFallback;

    public string Name => Miminus.Sys.L.T(NameKey);

    public static Theme Create(ThemeId id) => id switch
    {
        ThemeId.LunaOlive => LunaOlive(),
        ThemeId.LunaSilver => LunaSilver(),
        ThemeId.Seven => Seven(),
        ThemeId.Classic => Classic(),
        _ => LunaBlue(),
    };

    /// <summary>Shared skeleton: everything that is identical across the three
    /// Luna colour schemes, so each variant only overrides its accent colours.</summary>
    static Theme LunaBase()
    {
        return new Theme
        {
            Face = Color.Rgb(0xECE9D8),
            FaceLight = Color.Rgb(0xFFFFFF),
            FaceDark = Color.Rgb(0xDDD8C8),
            ControlBorder = Color.Rgb(0x7F9DB9),
            ControlBorderHot = Color.Rgb(0xE08A2E),
            FieldBack = Color.Rgb(0xFFFFFF),
            FieldBorder = Color.Rgb(0x7F9DB9),
            Text = Color.Rgb(0x000000),
            TextDisabled = Color.Rgb(0xA0A0A0),
            TextInverted = Color.Rgb(0xFFFFFF),

            Selection = Color.Rgb(0x316AC5),
            SelectionText = Color.Rgb(0xFFFFFF),
            SelectionInactive = Color.Rgb(0xD4D0C8),
            Hot = Color.Rgba(0xFFD89B, 140),

            MenuBack = Color.Rgb(0xFFFFFF),
            MenuGutter = Color.Rgb(0xECE9D8),
            MenuBorder = Color.Rgb(0x8E8E8E),
            MenuHighlight = Color.Rgb(0x316AC5),
            MenuHighlightText = Color.Rgb(0xFFFFFF),
            MenuSeparator = Color.Rgb(0xC5C2B8),

            Shadow = Color.Rgba(0x000000, 60),
            TooltipBack = Color.Rgb(0xFFFFE1),
            TooltipBorder = Color.Rgb(0x000000),
            TooltipText = Color.Rgb(0x000000),

            ScrollTrack = Color.Rgb(0xF1EFE2),
            ScrollThumb = Color.Rgb(0xD4D0C8),
            ScrollThumbHot = Color.Rgb(0xE8E5DA),
            ProgressFill = Color.Rgb(0x2FA02F),

            CaptionHeight = 26,
            FrameThickness = 4,
            CornerRadius = 7,
            TaskbarHeight = 30,
        };
    }

    public static Theme LunaBlue()
    {
        var t = LunaBase();
        t.Id = ThemeId.LunaBlue;
        t.NameKey = "theme.miminus_blue";

        t.CaptionActiveTop = Color.Rgb(0x0A62D8);
        t.CaptionActiveMid = Color.Rgb(0x2E8AF5);
        t.CaptionActiveBottom = Color.Rgb(0x0A47B0);
        t.CaptionInactiveTop = Color.Rgb(0x7BA7E8);
        t.CaptionInactiveMid = Color.Rgb(0x9CC0F0);
        t.CaptionInactiveBottom = Color.Rgb(0x7098D0);
        t.CaptionTextActive = Color.White;
        t.CaptionTextInactive = Color.Rgb(0xE4EEFC);
        t.CaptionTextShadow = Color.Rgba(0x00204A, 170);
        t.FrameOuter = Color.Rgb(0x0A50C8);
        t.FrameInner = Color.Rgb(0x3A82E8);
        t.Accent = Color.Rgb(0x316AC5);

        t.TaskbarTop = Color.Rgb(0x3F8CF3);
        t.TaskbarMid = Color.Rgb(0x1E5FD8);
        t.TaskbarBottom = Color.Rgb(0x0B3FA8);
        t.TaskbarEdge = Color.Rgb(0x0A2E80);
        t.TaskbarText = Color.White;
        t.TaskButtonFace = Color.Rgb(0x3C82E0);
        t.TaskButtonActive = Color.Rgb(0x1B4FB0);
        t.TaskButtonBorder = Color.Rgb(0x1A47A0);
        t.TrayBack = Color.Rgb(0x1290E8);
        t.TrayEdge = Color.Rgb(0x0A63B8);

        t.StartTop = Color.Rgb(0x5CBF3C);
        t.StartMid = Color.Rgb(0x3D9C24);
        t.StartBottom = Color.Rgb(0x1F6B12);
        t.StartEdge = Color.Rgb(0x175410);

        t.StartMenuHeaderTop = Color.Rgb(0x2E8AF5);
        t.StartMenuHeaderBottom = Color.Rgb(0x0A47B0);
        t.StartMenuLeft = Color.Rgb(0xFFFFFF);
        t.StartMenuRight = Color.Rgb(0xD3E5FA);
        t.StartMenuFooterTop = Color.Rgb(0x2E8AF5);
        t.StartMenuFooterBottom = Color.Rgb(0x0A47B0);
        t.StartMenuBorder = Color.Rgb(0x0A50C8);

        t.DesktopFallback = Color.Rgb(0x3A6EA5);
        return t;
    }

    public static Theme LunaOlive()
    {
        var t = LunaBase();
        t.Id = ThemeId.LunaOlive;
        t.NameKey = "theme.miminus_olive";

        t.CaptionActiveTop = Color.Rgb(0x8FA84E);
        t.CaptionActiveMid = Color.Rgb(0xAEC46A);
        t.CaptionActiveBottom = Color.Rgb(0x6D8438);
        t.CaptionInactiveTop = Color.Rgb(0xC0CB9A);
        t.CaptionInactiveMid = Color.Rgb(0xD4DCB4);
        t.CaptionInactiveBottom = Color.Rgb(0xAAB588);
        t.CaptionTextActive = Color.White;
        t.CaptionTextInactive = Color.Rgb(0xF0F4E4);
        t.CaptionTextShadow = Color.Rgba(0x2A3410, 170);
        t.FrameOuter = Color.Rgb(0x7A9040);
        t.FrameInner = Color.Rgb(0xA2B862);
        t.Accent = Color.Rgb(0x8FA84E);

        t.TaskbarTop = Color.Rgb(0xAEC46A);
        t.TaskbarMid = Color.Rgb(0x8FA84E);
        t.TaskbarBottom = Color.Rgb(0x60762E);
        t.TaskbarEdge = Color.Rgb(0x4A5C22);
        t.TaskbarText = Color.White;
        t.TaskButtonFace = Color.Rgb(0x9CB25A);
        t.TaskButtonActive = Color.Rgb(0x6D8438);
        t.TaskButtonBorder = Color.Rgb(0x5A7028);
        t.TrayBack = Color.Rgb(0xA8C05E);
        t.TrayEdge = Color.Rgb(0x748C38);

        t.StartTop = Color.Rgb(0x5CBF3C);
        t.StartMid = Color.Rgb(0x3D9C24);
        t.StartBottom = Color.Rgb(0x1F6B12);
        t.StartEdge = Color.Rgb(0x175410);

        t.StartMenuHeaderTop = Color.Rgb(0xAEC46A);
        t.StartMenuHeaderBottom = Color.Rgb(0x6D8438);
        t.StartMenuLeft = Color.Rgb(0xFFFFFF);
        t.StartMenuRight = Color.Rgb(0xE8EDD4);
        t.StartMenuFooterTop = Color.Rgb(0xAEC46A);
        t.StartMenuFooterBottom = Color.Rgb(0x6D8438);
        t.StartMenuBorder = Color.Rgb(0x7A9040);

        t.DesktopFallback = Color.Rgb(0x6D8438);
        return t;
    }

    public static Theme LunaSilver()
    {
        var t = LunaBase();
        t.Id = ThemeId.LunaSilver;
        t.NameKey = "theme.miminus_silver";

        t.CaptionActiveTop = Color.Rgb(0xB6B8C8);
        t.CaptionActiveMid = Color.Rgb(0xD8DAE6);
        t.CaptionActiveBottom = Color.Rgb(0x9295A8);
        t.CaptionInactiveTop = Color.Rgb(0xD6D8E0);
        t.CaptionInactiveMid = Color.Rgb(0xE8EAF0);
        t.CaptionInactiveBottom = Color.Rgb(0xB8BAC6);
        t.CaptionTextActive = Color.Rgb(0x101018);
        t.CaptionTextInactive = Color.Rgb(0x585868);
        t.CaptionTextShadow = Color.Rgba(0xFFFFFF, 120);
        t.FrameOuter = Color.Rgb(0x9295A8);
        t.FrameInner = Color.Rgb(0xC8CAD8);
        t.Accent = Color.Rgb(0x6E7A9C);

        t.TaskbarTop = Color.Rgb(0xE0E2EA);
        t.TaskbarMid = Color.Rgb(0xC0C3D2);
        t.TaskbarBottom = Color.Rgb(0x9A9EB0);
        t.TaskbarEdge = Color.Rgb(0x7A7E90);
        t.TaskbarText = Color.Rgb(0x101018);
        t.TaskButtonFace = Color.Rgb(0xD2D5E0);
        t.TaskButtonActive = Color.Rgb(0xA8ACBC);
        t.TaskButtonBorder = Color.Rgb(0x8A8E9E);
        t.TrayBack = Color.Rgb(0xCACDD8);
        t.TrayEdge = Color.Rgb(0x9A9EB0);

        t.StartTop = Color.Rgb(0x5CBF3C);
        t.StartMid = Color.Rgb(0x3D9C24);
        t.StartBottom = Color.Rgb(0x1F6B12);
        t.StartEdge = Color.Rgb(0x175410);

        t.StartMenuHeaderTop = Color.Rgb(0xD8DAE6);
        t.StartMenuHeaderBottom = Color.Rgb(0x9295A8);
        t.StartMenuLeft = Color.Rgb(0xFFFFFF);
        t.StartMenuRight = Color.Rgb(0xE6E7EE);
        t.StartMenuFooterTop = Color.Rgb(0xD8DAE6);
        t.StartMenuFooterBottom = Color.Rgb(0x9295A8);
        t.StartMenuBorder = Color.Rgb(0x8A8E9E);
        t.CaptionTextActive = Color.Rgb(0x202028);

        t.DesktopFallback = Color.Rgb(0x808494);
        return t;
    }

    /// <summary>"Миминус 7" — the Aero pastiche from part 3.</summary>
    public static Theme Seven()
    {
        var t = new Theme
        {
            Id = ThemeId.Seven,
            NameKey = "theme.miminus_7",

            CaptionActiveTop = Color.Rgba(0xE8F2FC, 225),
            CaptionActiveMid = Color.Rgba(0xBBD6F0, 215),
            CaptionActiveBottom = Color.Rgba(0xD8E8F8, 225),
            CaptionInactiveTop = Color.Rgba(0xF2F5F8, 205),
            CaptionInactiveMid = Color.Rgba(0xE0E6EC, 200),
            CaptionInactiveBottom = Color.Rgba(0xEDF1F5, 205),
            CaptionTextActive = Color.Rgb(0x14324E),
            CaptionTextInactive = Color.Rgb(0x6A7A88),
            CaptionTextShadow = Color.Rgba(0xFFFFFF, 190),
            FrameOuter = Color.Rgba(0x6E96BE, 190),
            FrameInner = Color.Rgba(0xFFFFFF, 130),
            CaptionHeight = 30,
            FrameThickness = 5,
            CornerRadius = 8,
            GlassCaption = true,

            Face = Color.Rgb(0xF0F0F0),
            FaceLight = Color.Rgb(0xFDFDFD),
            FaceDark = Color.Rgb(0xE1E1E1),
            ControlBorder = Color.Rgb(0x9EA6B0),
            ControlBorderHot = Color.Rgb(0x3C93D8),
            FieldBack = Color.Rgb(0xFFFFFF),
            FieldBorder = Color.Rgb(0xABADB3),
            Text = Color.Rgb(0x101010),
            TextDisabled = Color.Rgb(0xA0A0A0),
            TextInverted = Color.Rgb(0xFFFFFF),

            Selection = Color.Rgb(0x3399FF),
            SelectionText = Color.Rgb(0xFFFFFF),
            SelectionInactive = Color.Rgb(0xD8D8D8),
            Hot = Color.Rgba(0xCCE8FF, 190),
            Accent = Color.Rgb(0x3C93D8),

            MenuBack = Color.Rgb(0xF2F2F2),
            MenuGutter = Color.Rgb(0xE8E8E8),
            MenuBorder = Color.Rgb(0x9EA6B0),
            MenuHighlight = Color.Rgb(0xCCE8FF),
            MenuHighlightText = Color.Rgb(0x101010),
            MenuSeparator = Color.Rgb(0xD5D5D5),

            TaskbarTop = Color.Rgba(0x4A5868, 232),
            TaskbarMid = Color.Rgba(0x1E2833, 236),
            TaskbarBottom = Color.Rgba(0x11181F, 240),
            TaskbarEdge = Color.Rgba(0x7FA8CC, 150),
            TaskbarText = Color.White,
            TaskButtonFace = Color.Rgba(0xFFFFFF, 30),
            TaskButtonActive = Color.Rgba(0xFFFFFF, 70),
            TaskButtonBorder = Color.Rgba(0xFFFFFF, 60),
            TrayBack = Color.Rgba(0xFFFFFF, 22),
            TrayEdge = Color.Rgba(0xFFFFFF, 40),
            TaskbarHeight = 40,

            StartTop = Color.Rgb(0x7FD0F5),
            StartMid = Color.Rgb(0x2E8AD0),
            StartBottom = Color.Rgb(0x15497C),
            StartEdge = Color.Rgb(0x0E3357),

            StartMenuHeaderTop = Color.Rgba(0x2E5C88, 240),
            StartMenuHeaderBottom = Color.Rgba(0x18395A, 240),
            StartMenuLeft = Color.Rgb(0xF6F6F6),
            StartMenuRight = Color.Rgb(0xE4EDF6),
            StartMenuFooterTop = Color.Rgba(0x2E5C88, 240),
            StartMenuFooterBottom = Color.Rgba(0x18395A, 240),
            StartMenuBorder = Color.Rgba(0x5A88B4, 220),

            Shadow = Color.Rgba(0x000000, 80),
            TooltipBack = Color.Rgb(0xFFFFFF),
            TooltipBorder = Color.Rgb(0x767676),
            TooltipText = Color.Rgb(0x101010),

            ScrollTrack = Color.Rgb(0xF0F0F0),
            ScrollThumb = Color.Rgb(0xCDCDCD),
            ScrollThumbHot = Color.Rgb(0xA6A6A6),
            ProgressFill = Color.Rgb(0x06B025),
            DesktopFallback = Color.Rgb(0x1C4B7C),
        };
        return t;
    }

    /// <summary>Windows Classic — flat 3D bevels, no gradients.</summary>
    public static Theme Classic()
    {
        var t = LunaBase();
        t.Id = ThemeId.Classic;
        t.NameKey = "theme.classic";

        t.CaptionActiveTop = t.CaptionActiveMid = Color.Rgb(0x0A246A);
        t.CaptionActiveBottom = Color.Rgb(0x1E4FA0);
        t.CaptionInactiveTop = t.CaptionInactiveMid = Color.Rgb(0x808080);
        t.CaptionInactiveBottom = Color.Rgb(0xA0A0A0);
        t.CaptionTextActive = Color.White;
        t.CaptionTextInactive = Color.Rgb(0xD8D8D8);
        t.CaptionTextShadow = Color.Transparent;
        t.FrameOuter = Color.Rgb(0x808080);
        t.FrameInner = Color.Rgb(0xD4D0C8);
        t.CornerRadius = 0;
        t.FrameThickness = 4;
        t.CaptionHeight = 22;

        t.Face = Color.Rgb(0xD4D0C8);
        t.FaceLight = Color.Rgb(0xFFFFFF);
        t.FaceDark = Color.Rgb(0xACA899);
        t.ControlBorder = Color.Rgb(0x808080);
        t.Accent = Color.Rgb(0x0A246A);
        t.Selection = Color.Rgb(0x0A246A);
        t.MenuBack = Color.Rgb(0xD4D0C8);
        t.MenuGutter = Color.Rgb(0xD4D0C8);
        t.MenuHighlight = Color.Rgb(0x0A246A);

        t.TaskbarTop = t.TaskbarMid = t.TaskbarBottom = Color.Rgb(0xD4D0C8);
        t.TaskbarEdge = Color.Rgb(0x808080);
        t.TaskbarText = Color.Black;
        t.TaskButtonFace = Color.Rgb(0xD4D0C8);
        t.TaskButtonActive = Color.Rgb(0xC0BCB4);
        t.TaskButtonBorder = Color.Rgb(0x808080);
        t.TrayBack = Color.Rgb(0xD4D0C8);
        t.TrayEdge = Color.Rgb(0x808080);
        t.StartTop = t.StartMid = t.StartBottom = Color.Rgb(0xD4D0C8);
        t.StartEdge = Color.Rgb(0x808080);
        t.StartMenuHeaderTop = t.StartMenuHeaderBottom = Color.Rgb(0x0A246A);
        t.StartMenuLeft = Color.Rgb(0xD4D0C8);
        t.StartMenuRight = Color.Rgb(0xD4D0C8);
        t.StartMenuFooterTop = t.StartMenuFooterBottom = Color.Rgb(0xD4D0C8);
        t.StartMenuBorder = Color.Rgb(0x808080);
        t.TaskbarHeight = 28;
        t.DesktopFallback = Color.Rgb(0x3A6EA5);
        return t;
    }
}
