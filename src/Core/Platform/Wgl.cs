using System.Runtime.InteropServices;

namespace Miminus.Platform;

/// <summary>WGL bootstrap: builds a real OpenGL 3.3 core-profile context.
/// Windows only hands out a legacy context from a plain wglCreateContext, so we
/// spin up a throwaway window first, grab wglCreateContextAttribsARB through it,
/// then build the context we actually want on the real window.</summary>
internal static unsafe class Wgl
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PIXELFORMATDESCRIPTOR
    {
        public ushort nSize;
        public ushort nVersion;
        public uint dwFlags;
        public byte iPixelType;
        public byte cColorBits;
        public byte cRedBits, cRedShift, cGreenBits, cGreenShift, cBlueBits, cBlueShift;
        public byte cAlphaBits, cAlphaShift;
        public byte cAccumBits, cAccumRedBits, cAccumGreenBits, cAccumBlueBits, cAccumAlphaBits;
        public byte cDepthBits, cStencilBits, cAuxBuffers;
        public byte iLayerType;
        public byte bReserved;
        public uint dwLayerMask, dwVisibleMask, dwDamageMask;
    }

    const uint PFD_DRAW_TO_WINDOW = 0x00000004;
    const uint PFD_SUPPORT_OPENGL = 0x00000020;
    const uint PFD_DOUBLEBUFFER = 0x00000001;
    const byte PFD_TYPE_RGBA = 0;

    [DllImport("gdi32.dll")]
    static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll")]
    static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR ppfd);

    [DllImport("gdi32.dll")]
    public static extern bool SwapBuffers(IntPtr hdc);

    [DllImport("opengl32.dll")]
    static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll")]
    static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

    [DllImport("opengl32.dll")]
    static extern bool wglDeleteContext(IntPtr hglrc);

    [DllImport("opengl32.dll")]
    static extern IntPtr wglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);

    static IntPtr _opengl32;

    /// <summary>Resolves a GL entry point: extensions come from wglGetProcAddress,
    /// the GL 1.1 core lives in opengl32.dll itself.</summary>
    public static IntPtr GetProc(string name)
    {
        IntPtr p = wglGetProcAddress(name);
        // wglGetProcAddress returns these sentinels for "not found" on some drivers.
        if (p == IntPtr.Zero || p == (IntPtr)1 || p == (IntPtr)2 || p == (IntPtr)3 || p == (IntPtr)(-1))
        {
            if (_opengl32 == IntPtr.Zero) _opengl32 = Win32.LoadLibraryW("opengl32.dll");
            p = Win32.GetProcAddress(_opengl32, name);
        }
        return p;
    }

    const int WGL_CONTEXT_MAJOR_VERSION_ARB = 0x2091;
    const int WGL_CONTEXT_MINOR_VERSION_ARB = 0x2092;
    const int WGL_CONTEXT_PROFILE_MASK_ARB = 0x9126;
    const int WGL_CONTEXT_FLAGS_ARB = 0x2094;
    const int WGL_CONTEXT_CORE_PROFILE_BIT_ARB = 0x00000001;
    const int WGL_CONTEXT_FORWARD_COMPATIBLE_BIT_ARB = 0x0002;

    const int WGL_DRAW_TO_WINDOW_ARB = 0x2001;
    const int WGL_SUPPORT_OPENGL_ARB = 0x2010;
    const int WGL_DOUBLE_BUFFER_ARB = 0x2011;
    const int WGL_PIXEL_TYPE_ARB = 0x2013;
    const int WGL_TYPE_RGBA_ARB = 0x202B;
    const int WGL_COLOR_BITS_ARB = 0x2014;
    const int WGL_ALPHA_BITS_ARB = 0x201B;
    const int WGL_DEPTH_BITS_ARB = 0x2022;
    const int WGL_STENCIL_BITS_ARB = 0x2023;
    const int WGL_SAMPLE_BUFFERS_ARB = 0x2041;
    const int WGL_SAMPLES_ARB = 0x2042;

    static delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, IntPtr> s_createContextAttribs;
    static delegate* unmanaged[Stdcall]<IntPtr, int*, float*, uint, int*, uint*, int> s_choosePixelFormatARB;
    static delegate* unmanaged[Stdcall]<int, int> s_swapInterval;

    static bool s_bootstrapped;

    /// <summary>Creates the hidden window + legacy context needed to resolve the
    /// ARB context-creation entry points, then tears it all down again.</summary>
    static void Bootstrap()
    {
        if (s_bootstrapped) return;
        s_bootstrapped = true;

        IntPtr hInstance = Win32.GetModuleHandleW(IntPtr.Zero);
        string clsName = "MiminusWglBootstrap";
        IntPtr clsPtr = Marshal.StringToHGlobalUni(clsName);

        var dummyProc = new Win32.WndProc(Win32.DefWindowProcW);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)sizeof(Win32.WNDCLASSEXW),
            style = Win32.CS_OWNDC,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(dummyProc),
            hInstance = hInstance,
            lpszClassName = clsPtr,
        };
        Win32.RegisterClassExW(ref wc);

        IntPtr hwnd = Win32.CreateWindowExW(0, clsPtr, "bootstrap", Win32.WS_OVERLAPPEDWINDOW,
            0, 0, 8, 8, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        IntPtr hdc = Win32.GetDC(hwnd);

        var pfd = new PIXELFORMATDESCRIPTOR
        {
            nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR),
            nVersion = 1,
            dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
            iPixelType = PFD_TYPE_RGBA,
            cColorBits = 32,
            cAlphaBits = 8,
            cDepthBits = 24,
            cStencilBits = 8,
        };
        int fmt = ChoosePixelFormat(hdc, ref pfd);
        SetPixelFormat(hdc, fmt, ref pfd);

        IntPtr rc = wglCreateContext(hdc);
        wglMakeCurrent(hdc, rc);

        s_createContextAttribs = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int*, IntPtr>)
            wglGetProcAddress("wglCreateContextAttribsARB");
        s_choosePixelFormatARB = (delegate* unmanaged[Stdcall]<IntPtr, int*, float*, uint, int*, uint*, int>)
            wglGetProcAddress("wglChoosePixelFormatARB");
        s_swapInterval = (delegate* unmanaged[Stdcall]<int, int>)
            wglGetProcAddress("wglSwapIntervalEXT");

        wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
        wglDeleteContext(rc);
        Win32.ReleaseDC(hwnd, hdc);
        Win32.DestroyWindow(hwnd);
        GC.KeepAlive(dummyProc);
    }

    /// <summary>Picks a pixel format and creates a 3.3 core context on <paramref name="hdc"/>.
    /// Falls back to a legacy context if the ARB path is unavailable.</summary>
    public static IntPtr CreateContext(IntPtr hdc, int msaaSamples)
    {
        Bootstrap();

        bool formatSet = false;
        if (s_choosePixelFormatARB != null)
        {
            int* attribs = stackalloc int[]
            {
                WGL_DRAW_TO_WINDOW_ARB, 1,
                WGL_SUPPORT_OPENGL_ARB, 1,
                WGL_DOUBLE_BUFFER_ARB, 1,
                WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
                WGL_COLOR_BITS_ARB, 32,
                WGL_ALPHA_BITS_ARB, 8,
                WGL_DEPTH_BITS_ARB, 24,
                WGL_STENCIL_BITS_ARB, 8,
                WGL_SAMPLE_BUFFERS_ARB, msaaSamples > 1 ? 1 : 0,
                WGL_SAMPLES_ARB, msaaSamples > 1 ? msaaSamples : 0,
                0
            };
            int chosen;
            uint numFormats;
            if (s_choosePixelFormatARB(hdc, attribs, null, 1, &chosen, &numFormats) != 0 && numFormats > 0)
            {
                var pfd2 = new PIXELFORMATDESCRIPTOR { nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR), nVersion = 1 };
                SetPixelFormat(hdc, chosen, ref pfd2);
                formatSet = true;
            }
        }

        if (!formatSet)
        {
            var pfd = new PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)sizeof(PIXELFORMATDESCRIPTOR),
                nVersion = 1,
                dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                iPixelType = PFD_TYPE_RGBA,
                cColorBits = 32,
                cAlphaBits = 8,
                cDepthBits = 24,
                cStencilBits = 8,
            };
            int fmt = ChoosePixelFormat(hdc, ref pfd);
            SetPixelFormat(hdc, fmt, ref pfd);
        }

        IntPtr rc = IntPtr.Zero;
        if (s_createContextAttribs != null)
        {
            int* ctxAttribs = stackalloc int[]
            {
                WGL_CONTEXT_MAJOR_VERSION_ARB, 3,
                WGL_CONTEXT_MINOR_VERSION_ARB, 3,
                WGL_CONTEXT_PROFILE_MASK_ARB, WGL_CONTEXT_CORE_PROFILE_BIT_ARB,
                0
            };
            rc = s_createContextAttribs(hdc, IntPtr.Zero, ctxAttribs);
        }
        if (rc == IntPtr.Zero) rc = wglCreateContext(hdc);
        if (rc == IntPtr.Zero) throw new Exception("Failed to create an OpenGL context.");

        wglMakeCurrent(hdc, rc);
        return rc;
    }

    public static void MakeCurrent(IntPtr hdc, IntPtr rc) => wglMakeCurrent(hdc, rc);
    public static void DeleteContext(IntPtr rc) => wglDeleteContext(rc);

    public static void SetSwapInterval(int interval)
    {
        if (s_swapInterval != null) s_swapInterval(interval);
    }
}
