using System.Runtime.InteropServices;
using System.Text;

namespace Miminus.Platform;

/// <summary>The slice of OpenGL 3.3 core this project needs, bound by hand.
/// Entry points are unmanaged function pointers resolved once through WGL, so
/// calls go straight to the driver with no marshalling layer in between.</summary>
internal static unsafe class GL
{
    // ---- enums ---------------------------------------------------------
    public const uint COLOR_BUFFER_BIT = 0x4000;
    public const uint DEPTH_BUFFER_BIT = 0x0100;
    public const uint STENCIL_BUFFER_BIT = 0x0400;

    public const uint TRIANGLES = 0x0004;
    public const uint TRIANGLE_STRIP = 0x0005;
    public const uint TRIANGLE_FAN = 0x0006;
    public const uint LINES = 0x0001;
    public const uint LINE_STRIP = 0x0003;
    public const uint POINTS = 0x0000;

    public const uint BLEND = 0x0BE2;
    public const uint SCISSOR_TEST = 0x0C11;
    public const uint DEPTH_TEST = 0x0B71;
    public const uint CULL_FACE = 0x0B44;
    public const uint MULTISAMPLE = 0x809D;
    public const uint FRAMEBUFFER_SRGB = 0x8DB9;

    public const uint SRC_ALPHA = 0x0302;
    public const uint ONE_MINUS_SRC_ALPHA = 0x0303;
    public const uint ONE = 1;
    public const uint ZERO = 0;
    public const uint DST_COLOR = 0x0306;
    public const uint ONE_MINUS_SRC_COLOR = 0x0301;

    public const uint ARRAY_BUFFER = 0x8892;
    public const uint ELEMENT_ARRAY_BUFFER = 0x8893;
    public const uint STATIC_DRAW = 0x88E4;
    public const uint DYNAMIC_DRAW = 0x88E8;
    public const uint STREAM_DRAW = 0x88E0;

    public const uint FLOAT = 0x1406;
    public const uint UNSIGNED_BYTE = 0x1401;
    public const uint UNSIGNED_SHORT = 0x1403;
    public const uint UNSIGNED_INT = 0x1405;
    public const uint INT = 0x1404;

    public const uint VERTEX_SHADER = 0x8B31;
    public const uint FRAGMENT_SHADER = 0x8B30;
    public const uint COMPILE_STATUS = 0x8B81;
    public const uint LINK_STATUS = 0x8B82;
    public const uint INFO_LOG_LENGTH = 0x8B84;

    public const uint TEXTURE_2D = 0x0DE1;
    public const uint TEXTURE0 = 0x84C0;
    public const uint TEXTURE_MIN_FILTER = 0x2801;
    public const uint TEXTURE_MAG_FILTER = 0x2800;
    public const uint TEXTURE_WRAP_S = 0x2802;
    public const uint TEXTURE_WRAP_T = 0x2803;
    public const uint NEAREST = 0x2600;
    public const uint LINEAR = 0x2601;
    public const uint LINEAR_MIPMAP_LINEAR = 0x2703;
    public const uint NEAREST_MIPMAP_NEAREST = 0x2700;
    public const uint CLAMP_TO_EDGE = 0x812F;
    public const uint REPEAT = 0x2901;
    public const uint MIRRORED_REPEAT = 0x8370;

    public const uint RGBA = 0x1908;
    public const uint RGB = 0x1907;
    public const uint RED = 0x1903;
    public const uint RGBA8 = 0x8058;
    public const uint R8 = 0x8229;
    public const uint BGRA = 0x80E1;

    public const uint UNPACK_ALIGNMENT = 0x0CF5;
    public const uint PACK_ALIGNMENT = 0x0D05;

    public const uint FRAMEBUFFER = 0x8D40;
    public const uint COLOR_ATTACHMENT0 = 0x8CE0;
    public const uint FRAMEBUFFER_COMPLETE = 0x8CD5;

    public const uint VENDOR = 0x1F00;
    public const uint RENDERER = 0x1F01;
    public const uint VERSION = 0x1F02;
    public const uint SHADING_LANGUAGE_VERSION = 0x8B8C;

    public const uint NO_ERROR = 0;

    // ---- entry points --------------------------------------------------
    static delegate* unmanaged[Stdcall]<float, float, float, float, void> glClearColor;
    static delegate* unmanaged[Stdcall]<uint, void> glClear;
    static delegate* unmanaged[Stdcall]<int, int, int, int, void> glViewport;
    static delegate* unmanaged[Stdcall]<int, int, int, int, void> glScissor;
    static delegate* unmanaged[Stdcall]<uint, void> glEnable;
    static delegate* unmanaged[Stdcall]<uint, void> glDisable;
    static delegate* unmanaged[Stdcall]<uint, uint, void> glBlendFunc;
    static delegate* unmanaged[Stdcall]<uint, uint, uint, uint, void> glBlendFuncSeparate;
    static delegate* unmanaged[Stdcall]<uint, int, int, void> glDrawArrays;
    static delegate* unmanaged[Stdcall]<uint, int, uint, void*, void> glDrawElements;
    static delegate* unmanaged[Stdcall]<uint, IntPtr> glGetString;
    static delegate* unmanaged[Stdcall]<uint> glGetError;
    static delegate* unmanaged[Stdcall]<uint, int, void> glPixelStorei;
    static delegate* unmanaged[Stdcall]<int, int, int, int, uint, uint, void*, void> glReadPixels;
    static delegate* unmanaged[Stdcall]<void> glFinish;
    static delegate* unmanaged[Stdcall]<int, uint*, void> glGenTextures;
    static delegate* unmanaged[Stdcall]<int, uint*, void> glDeleteTextures;
    static delegate* unmanaged[Stdcall]<uint, uint, void> glBindTexture;
    static delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void> glTexImage2D;
    static delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void> glTexSubImage2D;
    static delegate* unmanaged[Stdcall]<uint, uint, int, void> glTexParameteri;
    static delegate* unmanaged[Stdcall]<uint, void> glActiveTexture;
    static delegate* unmanaged[Stdcall]<uint, void> glGenerateMipmap;

    static delegate* unmanaged[Stdcall]<int, uint*, void> glGenBuffers;
    static delegate* unmanaged[Stdcall]<int, uint*, void> glDeleteBuffers;
    static delegate* unmanaged[Stdcall]<uint, uint, void> glBindBuffer;
    static delegate* unmanaged[Stdcall]<uint, IntPtr, void*, uint, void> glBufferData;
    static delegate* unmanaged[Stdcall]<uint, IntPtr, IntPtr, void*, void> glBufferSubData;

    static delegate* unmanaged[Stdcall]<int, uint*, void> glGenVertexArrays;
    static delegate* unmanaged[Stdcall]<int, uint*, void> glDeleteVertexArrays;
    static delegate* unmanaged[Stdcall]<uint, void> glBindVertexArray;
    static delegate* unmanaged[Stdcall]<uint, int, uint, byte, int, void*, void> glVertexAttribPointer;
    static delegate* unmanaged[Stdcall]<uint, void> glEnableVertexAttribArray;

    static delegate* unmanaged[Stdcall]<uint, uint> glCreateShader;
    static delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void> glShaderSource;
    static delegate* unmanaged[Stdcall]<uint, void> glCompileShader;
    static delegate* unmanaged[Stdcall]<uint, uint, int*, void> glGetShaderiv;
    static delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> glGetShaderInfoLog;
    static delegate* unmanaged[Stdcall]<uint, void> glDeleteShader;
    static delegate* unmanaged[Stdcall]<uint> glCreateProgram;
    static delegate* unmanaged[Stdcall]<uint, uint, void> glAttachShader;
    static delegate* unmanaged[Stdcall]<uint, void> glLinkProgram;
    static delegate* unmanaged[Stdcall]<uint, uint, int*, void> glGetProgramiv;
    static delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void> glGetProgramInfoLog;
    static delegate* unmanaged[Stdcall]<uint, void> glUseProgram;
    static delegate* unmanaged[Stdcall]<uint, void> glDeleteProgram;
    static delegate* unmanaged[Stdcall]<uint, byte*, int> glGetUniformLocation;
    static delegate* unmanaged[Stdcall]<int, int, void> glUniform1i;
    static delegate* unmanaged[Stdcall]<int, float, void> glUniform1f;
    static delegate* unmanaged[Stdcall]<int, float, float, void> glUniform2f;
    static delegate* unmanaged[Stdcall]<int, float, float, float, void> glUniform3f;
    static delegate* unmanaged[Stdcall]<int, float, float, float, float, void> glUniform4f;
    static delegate* unmanaged[Stdcall]<int, int, byte, float*, void> glUniformMatrix4fv;

    static delegate* unmanaged[Stdcall]<int, uint*, void> glGenFramebuffers;
    static delegate* unmanaged[Stdcall]<int, uint*, void> glDeleteFramebuffers;
    static delegate* unmanaged[Stdcall]<uint, uint, void> glBindFramebuffer;
    static delegate* unmanaged[Stdcall]<uint, uint, uint, uint, int, void> glFramebufferTexture2D;
    static delegate* unmanaged[Stdcall]<uint, uint> glCheckFramebufferStatus;

    static IntPtr Req(string name)
    {
        IntPtr p = Wgl.GetProc(name);
        if (p == IntPtr.Zero) throw new Exception("Missing OpenGL entry point: " + name);
        return p;
    }

    public static void Load()
    {
        glClearColor = (delegate* unmanaged[Stdcall]<float, float, float, float, void>)Req("glClearColor");
        glClear = (delegate* unmanaged[Stdcall]<uint, void>)Req("glClear");
        glViewport = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)Req("glViewport");
        glScissor = (delegate* unmanaged[Stdcall]<int, int, int, int, void>)Req("glScissor");
        glEnable = (delegate* unmanaged[Stdcall]<uint, void>)Req("glEnable");
        glDisable = (delegate* unmanaged[Stdcall]<uint, void>)Req("glDisable");
        glBlendFunc = (delegate* unmanaged[Stdcall]<uint, uint, void>)Req("glBlendFunc");
        glBlendFuncSeparate = (delegate* unmanaged[Stdcall]<uint, uint, uint, uint, void>)Req("glBlendFuncSeparate");
        glDrawArrays = (delegate* unmanaged[Stdcall]<uint, int, int, void>)Req("glDrawArrays");
        glDrawElements = (delegate* unmanaged[Stdcall]<uint, int, uint, void*, void>)Req("glDrawElements");
        glGetString = (delegate* unmanaged[Stdcall]<uint, IntPtr>)Req("glGetString");
        glGetError = (delegate* unmanaged[Stdcall]<uint>)Req("glGetError");
        glPixelStorei = (delegate* unmanaged[Stdcall]<uint, int, void>)Req("glPixelStorei");
        glReadPixels = (delegate* unmanaged[Stdcall]<int, int, int, int, uint, uint, void*, void>)Req("glReadPixels");
        glFinish = (delegate* unmanaged[Stdcall]<void>)Req("glFinish");
        glGenTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glGenTextures");
        glDeleteTextures = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glDeleteTextures");
        glBindTexture = (delegate* unmanaged[Stdcall]<uint, uint, void>)Req("glBindTexture");
        glTexImage2D = (delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void>)Req("glTexImage2D");
        glTexSubImage2D = (delegate* unmanaged[Stdcall]<uint, int, int, int, int, int, uint, uint, void*, void>)Req("glTexSubImage2D");
        glTexParameteri = (delegate* unmanaged[Stdcall]<uint, uint, int, void>)Req("glTexParameteri");
        glActiveTexture = (delegate* unmanaged[Stdcall]<uint, void>)Req("glActiveTexture");
        glGenerateMipmap = (delegate* unmanaged[Stdcall]<uint, void>)Req("glGenerateMipmap");

        glGenBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glGenBuffers");
        glDeleteBuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glDeleteBuffers");
        glBindBuffer = (delegate* unmanaged[Stdcall]<uint, uint, void>)Req("glBindBuffer");
        glBufferData = (delegate* unmanaged[Stdcall]<uint, IntPtr, void*, uint, void>)Req("glBufferData");
        glBufferSubData = (delegate* unmanaged[Stdcall]<uint, IntPtr, IntPtr, void*, void>)Req("glBufferSubData");

        glGenVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glGenVertexArrays");
        glDeleteVertexArrays = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glDeleteVertexArrays");
        glBindVertexArray = (delegate* unmanaged[Stdcall]<uint, void>)Req("glBindVertexArray");
        glVertexAttribPointer = (delegate* unmanaged[Stdcall]<uint, int, uint, byte, int, void*, void>)Req("glVertexAttribPointer");
        glEnableVertexAttribArray = (delegate* unmanaged[Stdcall]<uint, void>)Req("glEnableVertexAttribArray");

        glCreateShader = (delegate* unmanaged[Stdcall]<uint, uint>)Req("glCreateShader");
        glShaderSource = (delegate* unmanaged[Stdcall]<uint, int, byte**, int*, void>)Req("glShaderSource");
        glCompileShader = (delegate* unmanaged[Stdcall]<uint, void>)Req("glCompileShader");
        glGetShaderiv = (delegate* unmanaged[Stdcall]<uint, uint, int*, void>)Req("glGetShaderiv");
        glGetShaderInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)Req("glGetShaderInfoLog");
        glDeleteShader = (delegate* unmanaged[Stdcall]<uint, void>)Req("glDeleteShader");
        glCreateProgram = (delegate* unmanaged[Stdcall]<uint>)Req("glCreateProgram");
        glAttachShader = (delegate* unmanaged[Stdcall]<uint, uint, void>)Req("glAttachShader");
        glLinkProgram = (delegate* unmanaged[Stdcall]<uint, void>)Req("glLinkProgram");
        glGetProgramiv = (delegate* unmanaged[Stdcall]<uint, uint, int*, void>)Req("glGetProgramiv");
        glGetProgramInfoLog = (delegate* unmanaged[Stdcall]<uint, int, int*, byte*, void>)Req("glGetProgramInfoLog");
        glUseProgram = (delegate* unmanaged[Stdcall]<uint, void>)Req("glUseProgram");
        glDeleteProgram = (delegate* unmanaged[Stdcall]<uint, void>)Req("glDeleteProgram");
        glGetUniformLocation = (delegate* unmanaged[Stdcall]<uint, byte*, int>)Req("glGetUniformLocation");
        glUniform1i = (delegate* unmanaged[Stdcall]<int, int, void>)Req("glUniform1i");
        glUniform1f = (delegate* unmanaged[Stdcall]<int, float, void>)Req("glUniform1f");
        glUniform2f = (delegate* unmanaged[Stdcall]<int, float, float, void>)Req("glUniform2f");
        glUniform3f = (delegate* unmanaged[Stdcall]<int, float, float, float, void>)Req("glUniform3f");
        glUniform4f = (delegate* unmanaged[Stdcall]<int, float, float, float, float, void>)Req("glUniform4f");
        glUniformMatrix4fv = (delegate* unmanaged[Stdcall]<int, int, byte, float*, void>)Req("glUniformMatrix4fv");

        glGenFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glGenFramebuffers");
        glDeleteFramebuffers = (delegate* unmanaged[Stdcall]<int, uint*, void>)Req("glDeleteFramebuffers");
        glBindFramebuffer = (delegate* unmanaged[Stdcall]<uint, uint, void>)Req("glBindFramebuffer");
        glFramebufferTexture2D = (delegate* unmanaged[Stdcall]<uint, uint, uint, uint, int, void>)Req("glFramebufferTexture2D");
        glCheckFramebufferStatus = (delegate* unmanaged[Stdcall]<uint, uint>)Req("glCheckFramebufferStatus");
    }

    // ---- thin managed wrappers ----------------------------------------
    public static void ClearColor(float r, float g, float b, float a) => glClearColor(r, g, b, a);
    public static void Clear(uint mask) => glClear(mask);
    public static void Viewport(int x, int y, int w, int h) => glViewport(x, y, w, h);
    public static void Scissor(int x, int y, int w, int h) => glScissor(x, y, w, h);
    public static void Enable(uint cap) => glEnable(cap);
    public static void Disable(uint cap) => glDisable(cap);
    public static void BlendFunc(uint s, uint d) => glBlendFunc(s, d);
    public static void BlendFuncSeparate(uint sc, uint dc, uint sa, uint da) => glBlendFuncSeparate(sc, dc, sa, da);
    public static void DrawArrays(uint mode, int first, int count) => glDrawArrays(mode, first, count);
    public static void DrawElements(uint mode, int count, uint type, int offsetBytes)
        => glDrawElements(mode, count, type, (void*)(IntPtr)offsetBytes);
    public static uint GetError() => glGetError();
    public static void PixelStore(uint pname, int value) => glPixelStorei(pname, value);
    public static void Finish() => glFinish();

    public static void ReadPixels(int x, int y, int w, int h, uint format, uint type, void* dest)
        => glReadPixels(x, y, w, h, format, type, dest);

    public static string GetString(uint name)
    {
        IntPtr p = glGetString(name);
        return p == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(p);
    }

    public static uint GenTexture() { uint t; glGenTextures(1, &t); return t; }
    public static void DeleteTexture(uint t) { glDeleteTextures(1, &t); }
    public static void BindTexture(uint target, uint tex) => glBindTexture(target, tex);
    public static void ActiveTexture(uint unit) => glActiveTexture(unit);
    public static void TexParameter(uint target, uint pname, int value) => glTexParameteri(target, pname, value);
    public static void GenerateMipmap(uint target) => glGenerateMipmap(target);

    public static void TexImage2D(uint target, int level, int internalFormat, int w, int h,
        int border, uint format, uint type, void* pixels)
        => glTexImage2D(target, level, internalFormat, w, h, border, format, type, pixels);

    public static void TexSubImage2D(uint target, int level, int x, int y, int w, int h,
        uint format, uint type, void* pixels)
        => glTexSubImage2D(target, level, x, y, w, h, format, type, pixels);

    public static uint GenBuffer() { uint b; glGenBuffers(1, &b); return b; }
    public static void DeleteBuffer(uint b) { glDeleteBuffers(1, &b); }
    public static void BindBuffer(uint target, uint buf) => glBindBuffer(target, buf);
    public static void BufferData(uint target, int size, void* data, uint usage)
        => glBufferData(target, (IntPtr)size, data, usage);
    public static void BufferSubData(uint target, int offset, int size, void* data)
        => glBufferSubData(target, (IntPtr)offset, (IntPtr)size, data);

    public static uint GenVertexArray() { uint a; glGenVertexArrays(1, &a); return a; }
    public static void DeleteVertexArray(uint a) { glDeleteVertexArrays(1, &a); }
    public static void BindVertexArray(uint a) => glBindVertexArray(a);
    public static void VertexAttribPointer(uint index, int size, uint type, bool normalized, int stride, int offset)
        => glVertexAttribPointer(index, size, type, (byte)(normalized ? 1 : 0), stride, (void*)(IntPtr)offset);
    public static void EnableVertexAttribArray(uint index) => glEnableVertexAttribArray(index);

    public static uint CreateShader(uint type) => glCreateShader(type);
    public static void DeleteShader(uint s) => glDeleteShader(s);
    public static uint CreateProgram() => glCreateProgram();
    public static void DeleteProgram(uint p) => glDeleteProgram(p);
    public static void AttachShader(uint p, uint s) => glAttachShader(p, s);
    public static void LinkProgram(uint p) => glLinkProgram(p);
    public static void UseProgram(uint p) => glUseProgram(p);

    public static void ShaderSource(uint shader, string source)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(source);
        fixed (byte* pb = bytes)
        {
            byte* ptr = pb;
            int len = bytes.Length;
            glShaderSource(shader, 1, &ptr, &len);
        }
    }

    public static void CompileShader(uint shader, string label)
    {
        glCompileShader(shader);
        int status;
        glGetShaderiv(shader, COMPILE_STATUS, &status);
        if (status == 0)
        {
            int logLen;
            glGetShaderiv(shader, INFO_LOG_LENGTH, &logLen);
            byte[] log = new byte[Math.Max(logLen, 1)];
            fixed (byte* pl = log) glGetShaderInfoLog(shader, log.Length, null, pl);
            throw new Exception($"Shader compile failed [{label}]:\n{Encoding.UTF8.GetString(log).TrimEnd('\0')}");
        }
    }

    public static void CheckLink(uint program, string label)
    {
        int status;
        glGetProgramiv(program, LINK_STATUS, &status);
        if (status == 0)
        {
            int logLen;
            glGetProgramiv(program, INFO_LOG_LENGTH, &logLen);
            byte[] log = new byte[Math.Max(logLen, 1)];
            fixed (byte* pl = log) glGetProgramInfoLog(program, log.Length, null, pl);
            throw new Exception($"Program link failed [{label}]:\n{Encoding.UTF8.GetString(log).TrimEnd('\0')}");
        }
    }

    public static int GetUniformLocation(uint program, string name)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(name + "\0");
        fixed (byte* pb = bytes) return glGetUniformLocation(program, pb);
    }

    public static void Uniform1i(int loc, int v) => glUniform1i(loc, v);
    public static void Uniform1f(int loc, float v) => glUniform1f(loc, v);
    public static void Uniform2f(int loc, float a, float b) => glUniform2f(loc, a, b);
    public static void Uniform3f(int loc, float a, float b, float c) => glUniform3f(loc, a, b, c);
    public static void Uniform4f(int loc, float a, float b, float c, float d) => glUniform4f(loc, a, b, c, d);
    public static void UniformMatrix4(int loc, float* m) => glUniformMatrix4fv(loc, 1, 0, m);

    public static uint GenFramebuffer() { uint f; glGenFramebuffers(1, &f); return f; }
    public static void DeleteFramebuffer(uint f) { glDeleteFramebuffers(1, &f); }
    public static void BindFramebuffer(uint target, uint f) => glBindFramebuffer(target, f);
    public static void FramebufferTexture2D(uint target, uint attach, uint texTarget, uint tex, int level)
        => glFramebufferTexture2D(target, attach, texTarget, tex, level);
    public static uint CheckFramebufferStatus(uint target) => glCheckFramebufferStatus(target);
}
