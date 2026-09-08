using System.Runtime.InteropServices;

namespace Miminus.Platform;

public enum MouseButton { Left = 0, Right = 1, Middle = 2 }

/// <summary>Snapshot of input for one frame. The shell and every pseudo-window
/// read from this rather than touching Win32 messages directly.</summary>
public sealed class InputState
{
    public float MouseX, MouseY;

    /// <summary>DPI scale the picture is drawn at. Pointer positions arrive
    /// from Windows in real pixels and are divided by it, so the UI works in
    /// one coordinate space whatever the scale.</summary>
    public float PointerScale = 1;
    public float MouseDX, MouseDY;
    public float WheelDelta;

    readonly bool[] _down = new bool[3];
    readonly bool[] _prev = new bool[3];
    readonly bool[] _dbl = new bool[3];

    readonly HashSet<int> _keysDown = new();
    readonly HashSet<int> _keysPrev = new();
    public readonly List<char> TypedChars = new();

    public bool IsDown(MouseButton b) => _down[(int)b];
    public bool Pressed(MouseButton b) => _down[(int)b] && !_prev[(int)b];
    public bool Released(MouseButton b) => !_down[(int)b] && _prev[(int)b];
    public bool DoubleClicked(MouseButton b) => _dbl[(int)b];

    public bool KeyDown(int vk) => _keysDown.Contains(vk);
    public bool KeyPressed(int vk) => _keysDown.Contains(vk) && !_keysPrev.Contains(vk);
    public bool KeyReleased(int vk) => !_keysDown.Contains(vk) && _keysPrev.Contains(vk);

    public bool Ctrl => KeyDown(Keys.Control);
    public bool Shift => KeyDown(Keys.Shift);
    public bool Alt => KeyDown(Keys.Menu);
    public bool Win => KeyDown(Keys.LWin) || KeyDown(Keys.RWin);

    public void SetButton(MouseButton b, bool down) => _down[(int)b] = down;
    internal void SetDouble(MouseButton b) => _dbl[(int)b] = true;
    public void SetKey(int vk, bool down) { if (down) _keysDown.Add(vk); else _keysDown.Remove(vk); }

    /// <summary>Rolls "current" into "previous" and clears per-frame edges.
    /// Called once at the very end of a frame.</summary>
    internal void EndFrame()
    {
        for (int i = 0; i < 3; i++) { _prev[i] = _down[i]; _dbl[i] = false; }
        _keysPrev.Clear();
        foreach (int k in _keysDown) _keysPrev.Add(k);
        TypedChars.Clear();
        WheelDelta = 0;
        MouseDX = MouseDY = 0;
    }

    /// <summary>Swallows the rest of this frame's input. Used when a modal layer
    /// (menu, dialog) has consumed the click and nothing below should see it.</summary>
    public void ConsumeMouse()
    {
        for (int i = 0; i < 3; i++) { _prev[i] = _down[i]; _dbl[i] = false; }
        WheelDelta = 0;
    }

    public void ConsumeKeyboard()
    {
        _keysPrev.Clear();
        foreach (int k in _keysDown) _keysPrev.Add(k);
        TypedChars.Clear();
    }
}

public static class Keys
{
    public const int Back = 0x08, Tab = 0x09, Enter = 0x0D, Shift = 0x10, Control = 0x11,
        Menu = 0x12, Pause = 0x13, Capital = 0x14, Escape = 0x1B, Space = 0x20,
        PageUp = 0x21, PageDown = 0x22, End = 0x23, Home = 0x24,
        Left = 0x25, Up = 0x26, Right = 0x27, Down = 0x28,
        Insert = 0x2D, Delete = 0x2E,
        LWin = 0x5B, RWin = 0x5C,
        D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
        D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,
        A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45, F = 0x46, G = 0x47,
        H = 0x48, I = 0x49, J = 0x4A, K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E,
        O = 0x4F, P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54, U = 0x55,
        V = 0x56, W = 0x57, X = 0x58, Y = 0x59, Z = 0x5A,
        F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73, F5 = 0x74, F6 = 0x75,
        F7 = 0x76, F8 = 0x77, F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

        // The two version 8 used for snapping a full-screen program to a side.
        Period = 0xBE, Comma = 0xBC;
}

/// <summary>Owns the OS window, the GL context and the message pump.</summary>
public sealed unsafe class AppWindow : IDisposable
{
    IntPtr _hwnd, _hdc, _hglrc;
    Win32.WndProc _proc;           // kept alive: Win32 holds a raw pointer to it
    Win32.HookProc _hookProc;      // likewise
    IntPtr _keyboardHook;
    bool _shouldClose;
    IntPtr _cursor;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool Focused { get; private set; } = true;
    public readonly InputState Input = new();
    public IntPtr Handle => _hwnd;

    /// <summary>Raised on WM_SIZE so the renderer can resize its targets.</summary>
    public event Action<int, int> Resized;

    public AppWindow(string title, int width, int height, bool fullscreen)
    {
        IntPtr hInstance = Win32.GetModuleHandleW(IntPtr.Zero);
        _proc = WindowProc;
        IntPtr clsPtr = Marshal.StringToHGlobalUni("MiminusOSWindow");
        _cursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)Win32.IDC_ARROW);

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)sizeof(Win32.WNDCLASSEXW),
            style = Win32.CS_OWNDC | Win32.CS_HREDRAW | Win32.CS_VREDRAW | Win32.CS_DBLCLKS,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = hInstance,
            hCursor = _cursor,
            lpszClassName = clsPtr,
        };
        if (Win32.RegisterClassExW(ref wc) == 0)
            throw new Exception("RegisterClassEx failed: " + Marshal.GetLastWin32Error());

        int screenW = Win32.GetSystemMetrics(Win32.SM_CXSCREEN);
        int screenH = Win32.GetSystemMetrics(Win32.SM_CYSCREEN);

        uint style;
        int x, y, w, h;
        if (fullscreen)
        {
            style = Win32.WS_POPUP | Win32.WS_VISIBLE;
            x = 0; y = 0; w = screenW; h = screenH;
        }
        else
        {
            style = Win32.WS_OVERLAPPEDWINDOW | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS;
            var r = new Win32.RECT { left = 0, top = 0, right = width, bottom = height };
            Win32.AdjustWindowRect(ref r, style, false);
            w = r.right - r.left;
            h = r.bottom - r.top;
            x = (screenW - w) / 2;
            y = (screenH - h) / 2;
        }

        _hwnd = Win32.CreateWindowExW(0, clsPtr, title, style, x, y, w, h,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new Exception("CreateWindowEx failed: " + Marshal.GetLastWin32Error());

        _hdc = Win32.GetDC(_hwnd);
        _hglrc = Wgl.CreateContext(_hdc, 4);
        GL.Load();
        Wgl.SetSwapInterval(1);

        Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
        Win32.UpdateWindow(_hwnd);

        Win32.GetClientRect(_hwnd, out var cr);
        Width = cr.right - cr.left;
        Height = cr.bottom - cr.top;

        InstallKeyboardHook();
    }

    /// <summary>Claims the Windows key while this window has the focus.
    ///
    /// An OS pretending to be an OS needs its own Start key, and the host shell
    /// would otherwise open its Start menu over the top. A low-level hook is
    /// the only way to see the key before the shell does; it runs on this
    /// thread, only swallows the two Windows keys, and only while the window is
    /// focused, so the host is left usable the moment focus goes elsewhere. If
    /// the hook cannot be installed the key simply keeps its usual meaning.</summary>
    void InstallKeyboardHook()
    {
        _hookProc = KeyboardHook;
        _keyboardHook = Win32.SetWindowsHookExW(Win32.WH_KEYBOARD_LL, _hookProc,
                                                Win32.GetModuleHandleW(IntPtr.Zero), 0);
    }

    IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code == Win32.HC_ACTION && Focused)
        {
            var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
            if (info.vkCode is Keys.LWin or Keys.RWin)
            {
                uint msg = (uint)wParam;
                if (msg is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN) Input.SetKey((int)info.vkCode, true);
                else if (msg is Win32.WM_KEYUP or Win32.WM_SYSKEYUP) Input.SetKey((int)info.vkCode, false);

                // Non-zero swallows the key, so the host shell never sees it.
                return (IntPtr)1;
            }
        }

        return Win32.CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_CLOSE:
                _shouldClose = true;
                return IntPtr.Zero;

            case Win32.WM_DESTROY:
                Win32.PostQuitMessage(0);
                return IntPtr.Zero;

            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;   // GL owns the pixels; never let GDI flash the frame

            case Win32.WM_SIZE:
            {
                int w = LoWord(lParam), h = HiWord(lParam);
                if (w > 0 && h > 0 && (w != Width || h != Height))
                {
                    Width = w; Height = h;
                    Resized?.Invoke(w, h);
                }
                return IntPtr.Zero;
            }

            case Win32.WM_ACTIVATEAPP:
                Focused = wParam != IntPtr.Zero;
                return IntPtr.Zero;

            case Win32.WM_SETCURSOR:
                // We draw our own pointer, but keep the arrow outside the client area.
                if (LoWord(lParam) == 1) { Win32.SetCursor(IntPtr.Zero); return (IntPtr)1; }
                break;

            case Win32.WM_MOUSEMOVE:
            {
                float nx = (short)LoWord(lParam);
                float ny = (short)HiWord(lParam);
                Input.MouseDX += nx - Input.MouseX;
                Input.MouseDY += ny - Input.MouseY;
                Input.MouseX = nx / Input.PointerScale;
                Input.MouseY = ny / Input.PointerScale;
                return IntPtr.Zero;
            }

            case Win32.WM_LBUTTONDOWN: Input.SetButton(MouseButton.Left, true); return IntPtr.Zero;
            case Win32.WM_LBUTTONUP: Input.SetButton(MouseButton.Left, false); return IntPtr.Zero;
            case Win32.WM_RBUTTONDOWN: Input.SetButton(MouseButton.Right, true); return IntPtr.Zero;
            case Win32.WM_RBUTTONUP: Input.SetButton(MouseButton.Right, false); return IntPtr.Zero;
            case Win32.WM_MBUTTONDOWN: Input.SetButton(MouseButton.Middle, true); return IntPtr.Zero;
            case Win32.WM_MBUTTONUP: Input.SetButton(MouseButton.Middle, false); return IntPtr.Zero;

            case Win32.WM_LBUTTONDBLCLK:
                Input.SetButton(MouseButton.Left, true);
                Input.SetDouble(MouseButton.Left);
                return IntPtr.Zero;

            case Win32.WM_RBUTTONDBLCLK:
                Input.SetButton(MouseButton.Right, true);
                Input.SetDouble(MouseButton.Right);
                return IntPtr.Zero;

            case Win32.WM_MOUSEWHEEL:
                Input.WheelDelta += (short)HiWord(wParam) / 120f;
                return IntPtr.Zero;

            case Win32.WM_KEYDOWN:
            case Win32.WM_SYSKEYDOWN:
                Input.SetKey((int)wParam, true);
                if (msg == Win32.WM_SYSKEYDOWN) return IntPtr.Zero;
                return IntPtr.Zero;

            case Win32.WM_KEYUP:
            case Win32.WM_SYSKEYUP:
                Input.SetKey((int)wParam, false);
                return IntPtr.Zero;

            case Win32.WM_CHAR:
            {
                char c = (char)wParam;
                if (c >= ' ' || c == '\r' || c == '\t') Input.TypedChars.Add(c);
                return IntPtr.Zero;
            }

            case Win32.WM_SYSCOMMAND:
            {
                int sc = (int)wParam & 0xFFF0;
                // Alt alone must not open the (nonexistent) system menu, and the
                // screensaver must not fire over a full-screen desktop.
                if (sc == Win32.SC_KEYMENU || sc == Win32.SC_SCREENSAVE || sc == Win32.SC_MONITORPOWER)
                    return IntPtr.Zero;
                break;
            }
        }
        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    static int LoWord(IntPtr v) => (int)((long)v & 0xFFFF);
    static int HiWord(IntPtr v) => (int)(((long)v >> 16) & 0xFFFF);

    public bool ShouldClose => _shouldClose;
    public void RequestClose() => _shouldClose = true;

    public void PumpMessages()
    {
        while (Win32.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, Win32.PM_REMOVE))
        {
            if (msg.message == Win32.WM_QUIT) { _shouldClose = true; break; }
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessageW(ref msg);
        }
    }

    public void SwapBuffers() => Wgl.SwapBuffers(_hdc);
    public void EndFrame() => Input.EndFrame();

    /// <summary>Injects a pointer press at a screen position. Used by the
    /// scripted-click switch so interactive behaviour can be screenshotted.</summary>
    public void InjectClick(float x, float y)
    {
        Input.MouseX = x / Input.PointerScale;
        Input.MouseY = y / Input.PointerScale;
        Input.SetButton(MouseButton.Left, true);
    }

    /// <summary>The same, with the other button — which is the only way a
    /// scripted run can reach a context menu.</summary>
    public void InjectRightClick(float x, float y)
    {
        Input.MouseX = x / Input.PointerScale;
        Input.MouseY = y / Input.PointerScale;
        Input.SetButton(MouseButton.Right, true);
    }

    /// <summary>Moves the injected pointer without touching the button, which
    /// is what makes a scripted drag a drag rather than two clicks.</summary>
    public void InjectMove(float x, float y)
    {
        Input.MouseDX += x - Input.MouseX;
        Input.MouseDY += y - Input.MouseY;
        Input.MouseX = x / Input.PointerScale;
        Input.MouseY = y / Input.PointerScale;
    }

    public void InjectRelease() => Input.SetButton(MouseButton.Left, false);

    /// <summary>Injects a key press (and optionally a typed character) for the
    /// scripted-input switch.</summary>
    /// <summary>Injects a key press, and the character it types when it has one.
    /// Used by the scripted-input switch so keyboard behaviour can be captured.</summary>
    public void InjectKey(int vk, char? typed = null)
    {
        if (vk != 0) Input.SetKey(vk, true);
        if (typed.HasValue) Input.TypedChars.Add(typed.Value);
    }

    public void InjectKeyUp(int vk) { if (vk != 0) Input.SetKey(vk, false); }
    public void SetTitle(string t) => Win32.SetWindowTextW(_hwnd, t);

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            Win32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_hglrc != IntPtr.Zero)
        {
            Wgl.MakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Wgl.DeleteContext(_hglrc);
            _hglrc = IntPtr.Zero;
        }
        if (_hdc != IntPtr.Zero) { Win32.ReleaseDC(_hwnd, _hdc); _hdc = IntPtr.Zero; }
        if (_hwnd != IntPtr.Zero) { Win32.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
    }
}
