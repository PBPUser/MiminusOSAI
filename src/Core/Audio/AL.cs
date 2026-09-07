using Miminus.Platform;

namespace Miminus.Audio;

/// <summary>OpenAL bound by hand.
///
/// The Windows SDK ships no AL headers, and OpenAL32.dll is a system component,
/// so the library is loaded at runtime and every entry point resolved through
/// GetProcAddress. That also means a machine without OpenAL simply runs the OS
/// silently instead of failing to start — see <see cref="Available"/>.</summary>
internal static unsafe class AL
{
    // ---- AL enums --------------------------------------------------------
    public const int AL_NONE = 0;
    public const int AL_FALSE = 0;
    public const int AL_TRUE = 1;

    public const int AL_SOURCE_RELATIVE = 0x202;
    public const int AL_PITCH = 0x1003;
    public const int AL_POSITION = 0x1004;
    public const int AL_VELOCITY = 0x1006;
    public const int AL_LOOPING = 0x1007;
    public const int AL_BUFFER = 0x1009;
    public const int AL_GAIN = 0x100A;
    public const int AL_MIN_GAIN = 0x100D;
    public const int AL_MAX_GAIN = 0x100E;
    public const int AL_ORIENTATION = 0x100F;
    public const int AL_SOURCE_STATE = 0x1010;
    public const int AL_INITIAL = 0x1011;
    public const int AL_PLAYING = 0x1012;
    public const int AL_PAUSED = 0x1013;
    public const int AL_STOPPED = 0x1014;
    public const int AL_BUFFERS_QUEUED = 0x1015;
    public const int AL_BUFFERS_PROCESSED = 0x1016;
    public const int AL_SEC_OFFSET = 0x1024;
    public const int AL_REFERENCE_DISTANCE = 0x1020;
    public const int AL_ROLLOFF_FACTOR = 0x1021;
    public const int AL_MAX_DISTANCE = 0x1023;

    public const int AL_FORMAT_MONO8 = 0x1100;
    public const int AL_FORMAT_MONO16 = 0x1101;
    public const int AL_FORMAT_STEREO8 = 0x1102;
    public const int AL_FORMAT_STEREO16 = 0x1103;

    public const int AL_NO_ERROR = 0;

    public const int AL_INVERSE_DISTANCE_CLAMPED = 0xD002;

    static IntPtr _lib;
    public static bool Available { get; private set; }
    public static string LoadError { get; private set; }

    // ---- ALC -------------------------------------------------------------
    static delegate* unmanaged[Cdecl]<byte*, IntPtr> alcOpenDevice;
    static delegate* unmanaged[Cdecl]<IntPtr, bool> alcCloseDevice;
    static delegate* unmanaged[Cdecl]<IntPtr, int*, IntPtr> alcCreateContext;
    static delegate* unmanaged[Cdecl]<IntPtr, bool> alcMakeContextCurrent;
    static delegate* unmanaged[Cdecl]<IntPtr, void> alcDestroyContext;
    static delegate* unmanaged[Cdecl]<IntPtr, int> alcGetError;

    // ---- AL --------------------------------------------------------------
    static delegate* unmanaged[Cdecl]<int, uint*, void> alGenBuffers;
    static delegate* unmanaged[Cdecl]<int, uint*, void> alDeleteBuffers;
    static delegate* unmanaged[Cdecl]<uint, int, void*, int, int, void> alBufferData;
    static delegate* unmanaged[Cdecl]<int, uint*, void> alGenSources;
    static delegate* unmanaged[Cdecl]<int, uint*, void> alDeleteSources;
    static delegate* unmanaged[Cdecl]<uint, int, int, void> alSourcei;
    static delegate* unmanaged[Cdecl]<uint, int, float, void> alSourcef;
    static delegate* unmanaged[Cdecl]<uint, int, float, float, float, void> alSource3f;
    static delegate* unmanaged[Cdecl]<uint, int, int*, void> alGetSourcei;
    static delegate* unmanaged[Cdecl]<uint, void> alSourcePlay;
    static delegate* unmanaged[Cdecl]<uint, void> alSourceStop;
    static delegate* unmanaged[Cdecl]<uint, void> alSourcePause;
    static delegate* unmanaged[Cdecl]<uint, void> alSourceRewind;
    static delegate* unmanaged[Cdecl]<int, float, float, float, void> alListener3f;
    static delegate* unmanaged[Cdecl]<int, float, void> alListenerf;
    static delegate* unmanaged[Cdecl]<int, float*, void> alListenerfv;
    static delegate* unmanaged[Cdecl]<int> alGetError;
    static delegate* unmanaged[Cdecl]<int, void> alDistanceModel;

    static IntPtr _device, _context;

    static IntPtr P(string name) => Win32.GetProcAddress(_lib, name);

    /// <summary>Loads OpenAL and opens the default device. Returns false (rather
    /// than throwing) when audio is unavailable.</summary>
    public static bool Init()
    {
        if (Available) return true;
        try
        {
            _lib = Win32.LoadLibraryW("OpenAL32.dll");
            if (_lib == IntPtr.Zero) _lib = Win32.LoadLibraryW("soft_oal.dll");
            if (_lib == IntPtr.Zero)
            {
                LoadError = "OpenAL32.dll not found";
                return false;
            }

            alcOpenDevice = (delegate* unmanaged[Cdecl]<byte*, IntPtr>)P("alcOpenDevice");
            alcCloseDevice = (delegate* unmanaged[Cdecl]<IntPtr, bool>)P("alcCloseDevice");
            alcCreateContext = (delegate* unmanaged[Cdecl]<IntPtr, int*, IntPtr>)P("alcCreateContext");
            alcMakeContextCurrent = (delegate* unmanaged[Cdecl]<IntPtr, bool>)P("alcMakeContextCurrent");
            alcDestroyContext = (delegate* unmanaged[Cdecl]<IntPtr, void>)P("alcDestroyContext");
            alcGetError = (delegate* unmanaged[Cdecl]<IntPtr, int>)P("alcGetError");

            alGenBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)P("alGenBuffers");
            alDeleteBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)P("alDeleteBuffers");
            alBufferData = (delegate* unmanaged[Cdecl]<uint, int, void*, int, int, void>)P("alBufferData");
            alGenSources = (delegate* unmanaged[Cdecl]<int, uint*, void>)P("alGenSources");
            alDeleteSources = (delegate* unmanaged[Cdecl]<int, uint*, void>)P("alDeleteSources");
            alSourcei = (delegate* unmanaged[Cdecl]<uint, int, int, void>)P("alSourcei");
            alSourcef = (delegate* unmanaged[Cdecl]<uint, int, float, void>)P("alSourcef");
            alSource3f = (delegate* unmanaged[Cdecl]<uint, int, float, float, float, void>)P("alSource3f");
            alGetSourcei = (delegate* unmanaged[Cdecl]<uint, int, int*, void>)P("alGetSourcei");
            alSourcePlay = (delegate* unmanaged[Cdecl]<uint, void>)P("alSourcePlay");
            alSourceStop = (delegate* unmanaged[Cdecl]<uint, void>)P("alSourceStop");
            alSourcePause = (delegate* unmanaged[Cdecl]<uint, void>)P("alSourcePause");
            alSourceRewind = (delegate* unmanaged[Cdecl]<uint, void>)P("alSourceRewind");
            alListener3f = (delegate* unmanaged[Cdecl]<int, float, float, float, void>)P("alListener3f");
            alListenerf = (delegate* unmanaged[Cdecl]<int, float, void>)P("alListenerf");
            alListenerfv = (delegate* unmanaged[Cdecl]<int, float*, void>)P("alListenerfv");
            alGetError = (delegate* unmanaged[Cdecl]<int>)P("alGetError");
            alDistanceModel = (delegate* unmanaged[Cdecl]<int, void>)P("alDistanceModel");

            if (alcOpenDevice == null || alGenSources == null)
            {
                LoadError = "OpenAL entry points missing";
                return false;
            }

            _device = alcOpenDevice(null);
            if (_device == IntPtr.Zero) { LoadError = "no OpenAL output device"; return false; }

            _context = alcCreateContext(_device, null);
            if (_context == IntPtr.Zero) { LoadError = "alcCreateContext failed"; return false; }

            alcMakeContextCurrent(_context);
            alDistanceModel(AL_INVERSE_DISTANCE_CLAMPED);

            // Listener sits one "screen depth" back, looking at the desktop plane.
            alListener3f(AL_POSITION, 0, 0, 0);
            alListener3f(AL_VELOCITY, 0, 0, 0);
            float* orient = stackalloc float[6] { 0, 0, -1, 0, 1, 0 };
            alListenerfv(AL_ORIENTATION, orient);
            alListenerf(AL_GAIN, 1f);

            Available = true;
            return true;
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            return false;
        }
    }

    public static void Shutdown()
    {
        if (!Available) return;
        alcMakeContextCurrent(IntPtr.Zero);
        if (_context != IntPtr.Zero) { alcDestroyContext(_context); _context = IntPtr.Zero; }
        if (_device != IntPtr.Zero) { alcCloseDevice(_device); _device = IntPtr.Zero; }
        Available = false;
    }

    public static uint GenBuffer() { uint b; alGenBuffers(1, &b); return b; }
    public static void DeleteBuffer(uint b) { alDeleteBuffers(1, &b); }
    public static uint GenSource() { uint s; alGenSources(1, &s); return s; }
    public static void DeleteSource(uint s) { alDeleteSources(1, &s); }

    public static void BufferData(uint buffer, short[] pcm, int channels, int sampleRate)
    {
        fixed (short* p = pcm)
            alBufferData(buffer, channels == 2 ? AL_FORMAT_STEREO16 : AL_FORMAT_MONO16,
                         p, pcm.Length * sizeof(short), sampleRate);
    }

    public static void Sourcei(uint s, int p, int v) => alSourcei(s, p, v);
    public static void Sourcef(uint s, int p, float v) => alSourcef(s, p, v);
    public static void Source3f(uint s, int p, float x, float y, float z) => alSource3f(s, p, x, y, z);
    public static void Play(uint s) => alSourcePlay(s);
    public static void Stop(uint s) => alSourceStop(s);
    public static void Pause(uint s) => alSourcePause(s);
    public static void Rewind(uint s) => alSourceRewind(s);

    public static int GetSourcei(uint s, int p) { int v; alGetSourcei(s, p, &v); return v; }
    public static int GetError() => alGetError();
    public static void ListenerGain(float g) => alListenerf(AL_GAIN, g);
}
