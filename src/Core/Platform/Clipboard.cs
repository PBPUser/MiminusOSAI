using System.Runtime.InteropServices;

namespace Miminus.Platform;

/// <summary>Unicode text clipboard access, so Ctrl+C/V in our Notepad talks to
/// the real Windows clipboard rather than a private buffer.</summary>
public static class Clipboard
{
    const uint CF_UNICODETEXT = 13;
    const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr hMem);

    /// <summary>Local fallback used when the OS clipboard cannot be opened
    /// (another process holding it, for instance), so copy/paste never dies.</summary>
    static string _fallback = "";

    public static bool SetText(string text)
    {
        text ??= "";
        _fallback = text;

        if (!OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero) return false;

            IntPtr target = GlobalLock(hMem);
            if (target == IntPtr.Zero) { GlobalFree(hMem); return false; }
            try { Marshal.Copy(text.ToCharArray(), 0, target, text.Length); Marshal.WriteInt16(target, text.Length * 2, 0); }
            finally { GlobalUnlock(hMem); }

            // Ownership passes to the clipboard on success; do not free hMem.
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return false;
            }
            return true;
        }
        finally { CloseClipboard(); }
    }

    public static string GetText()
    {
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT)) return _fallback;
        if (!OpenClipboard(IntPtr.Zero)) return _fallback;
        try
        {
            IntPtr h = GetClipboardData(CF_UNICODETEXT);
            if (h == IntPtr.Zero) return _fallback;
            IntPtr p = GlobalLock(h);
            if (p == IntPtr.Zero) return _fallback;
            try { return Marshal.PtrToStringUni(p) ?? _fallback; }
            finally { GlobalUnlock(h); }
        }
        catch { return _fallback; }
        finally { CloseClipboard(); }
    }
}
