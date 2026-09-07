using System.Runtime.InteropServices;

namespace Miminus.Platform;

/// <summary>Raw Win32 interop. This is the only OS surface the app uses: it
/// supplies a window, a message pump, and a GDI device context that font
/// glyphs are rasterised through. Everything visible is drawn by OpenGL.</summary>
internal static unsafe class Win32
{
    public const string User32 = "user32.dll";
    public const string Gdi32 = "gdi32.dll";
    public const string Kernel32 = "kernel32.dll";

    // ---- window styles -------------------------------------------------
    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const uint WS_CLIPSIBLINGS = 0x04000000;

    public const uint CS_OWNDC = 0x0020;
    public const uint CS_HREDRAW = 0x0002;
    public const uint CS_VREDRAW = 0x0001;
    public const uint CS_DBLCLKS = 0x0008;

    public const int SW_SHOW = 5;
    public const int SW_SHOWMAXIMIZED = 3;

    // ---- messages ------------------------------------------------------
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_ACTIVATEAPP = 0x001C;
    public const uint WM_SETCURSOR = 0x0020;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_LBUTTONDBLCLK = 0x0203;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_RBUTTONDBLCLK = 0x0206;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_SYSCOMMAND = 0x0112;
    public const uint WM_ERASEBKGND = 0x0014;

    public const int SC_KEYMENU = 0xF100;
    public const int SC_SCREENSAVE = 0xF140;
    public const int SC_MONITORPOWER = 0xF170;

    public const uint PM_REMOVE = 0x0001;

    public const int IDC_ARROW = 32512;

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int left, top, right, bottom; }

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // ---- low-level keyboard hook, used to keep the Windows key ------------

    public const int WH_KEYBOARD_LL = 13;
    public const int HC_ACTION = 0;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport(Kernel32, SetLastError = true)]
    public static extern IntPtr GetModuleHandleW(IntPtr lpModuleName);

    [DllImport(Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryW(string lpLibFileName);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowExW(
        uint dwExStyle, IntPtr lpClassName, string lpWindowName, uint dwStyle,
        int X, int Y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport(User32)]
    public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport(User32)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport(User32)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport(User32)]
    public static extern bool UpdateWindow(IntPtr hWnd);

    [DllImport(User32)]
    public static extern bool PeekMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport(User32)]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport(User32)]
    public static extern IntPtr DispatchMessageW(ref MSG lpMsg);

    [DllImport(User32)]
    public static extern void PostQuitMessage(int nExitCode);

    [DllImport(User32, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport(User32)]
    public static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport(User32)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport(User32)]
    public static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);

    [DllImport(User32)]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport(User32)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(User32)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(User32)]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport(User32, CharSet = CharSet.Unicode)]
    public static extern bool SetWindowTextW(IntPtr hWnd, string text);

    // ---- GDI (used only to rasterise glyphs into a bitmap) -------------
    public const uint DIB_RGB_COLORS = 0;
    public const int BI_RGB = 0;
    public const uint TRANSPARENT = 1;
    public const uint OPAQUE = 2;

    public const int FW_NORMAL = 400;
    public const int FW_BOLD = 700;
    public const uint DEFAULT_CHARSET = 1;
    public const uint RUSSIAN_CHARSET = 204;
    public const uint OUT_TT_PRECIS = 4;
    public const uint CLIP_DEFAULT_PRECIS = 0;
    public const uint ANTIALIASED_QUALITY = 4;
    public const uint NONANTIALIASED_QUALITY = 3;
    public const uint CLEARTYPE_QUALITY = 5;
    public const uint DEFAULT_PITCH = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors0;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    public struct TEXTMETRICW
    {
        public int tmHeight, tmAscent, tmDescent, tmInternalLeading, tmExternalLeading;
        public int tmAveCharWidth, tmMaxCharWidth, tmWeight, tmOverhang;
        public int tmDigitizedAspectX, tmDigitizedAspectY;
        public char tmFirstChar, tmLastChar, tmDefaultChar, tmBreakChar;
        public byte tmItalic, tmUnderlined, tmStruckOut, tmPitchAndFamily, tmCharSet;
    }

    [DllImport(Gdi32)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport(Gdi32)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport(Gdi32)]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport(Gdi32)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport(Gdi32)]
    public static extern bool DeleteObject(IntPtr ho);

    [DllImport(Gdi32, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFontW(
        int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight,
        uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet,
        uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily,
        string pszFaceName);

    [DllImport(Gdi32)]
    public static extern uint SetTextColor(IntPtr hdc, uint color);

    [DllImport(Gdi32)]
    public static extern uint SetBkColor(IntPtr hdc, uint color);

    [DllImport(Gdi32)]
    public static extern int SetBkMode(IntPtr hdc, uint mode);

    [DllImport(Gdi32, CharSet = CharSet.Unicode)]
    public static extern bool TextOutW(IntPtr hdc, int x, int y, string lpString, int c);

    [DllImport(Gdi32, CharSet = CharSet.Unicode)]
    public static extern bool GetTextExtentPoint32W(IntPtr hdc, string lpString, int c, out SIZE psizl);

    [DllImport(Gdi32, CharSet = CharSet.Unicode)]
    public static extern bool GetTextMetricsW(IntPtr hdc, out TEXTMETRICW lptm);

    [DllImport(Gdi32)]
    public static extern int GetDeviceCaps(IntPtr hdc, int index);

    [DllImport(Gdi32)]
    public static extern int FillRect(IntPtr hDC, ref RECT lprc, IntPtr hbr);

    [DllImport(Gdi32)]
    public static extern IntPtr CreateSolidBrush(uint color);

    [DllImport(Gdi32)]
    public static extern bool GdiFlush();
}
