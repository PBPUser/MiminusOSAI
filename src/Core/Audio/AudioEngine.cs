namespace Miminus.Audio;

public enum Sfx
{
    Startup, Shutdown, Logon, Logoff,
    Click, MenuOpen, MenuClose, WindowOpen, WindowClose, Minimize, Restore,
    Error, Warning, Info, Question,
    Balloon, Tick, Navigate, Trash,
    MineReveal, MineFlag, MineBoom, MineWin,
    ScanBeep, ScanDone, Typewriter, Shutter, DriveSpin,
}

/// <summary>All sound in the OS.
///
/// Buffers are synthesised once at startup and played through a small pool of
/// OpenAL sources. Sounds are positioned in 3D from the screen coordinate that
/// triggered them, so a menu opening on the left is audibly on the left — the
/// listener sits in front of the desktop plane and the panning falls out of
/// OpenAL's own attenuation rather than a hand-rolled pan law.</summary>
public sealed class AudioEngine : IDisposable
{
    const int SourceCount = 24;

    readonly Dictionary<Sfx, uint> _buffers = new();
    readonly uint[] _sources = new uint[SourceCount];
    int _next;
    bool _ok;

    uint _musicSource;
    uint _musicBuffer;

    float _master = 0.7f;
    public bool Muted { get; set; }

    public float MasterVolume
    {
        get => _master;
        set
        {
            _master = Math.Clamp(value, 0, 1);
            if (_ok) AL.ListenerGain(Muted ? 0 : _master);
        }
    }

    public bool Available => _ok;
    public string Status => _ok ? "OpenAL ready" : "silent (" + (AL.LoadError ?? "unavailable") + ")";

    /// <summary>Half the screen width, in world units, used to map pixels to
    /// listener-relative positions.</summary>
    float _halfW = 640, _halfH = 400;

    public AudioEngine()
    {
        _ok = AL.Init();
        if (!_ok) return;

        for (int i = 0; i < SourceCount; i++)
        {
            _sources[i] = AL.GenSource();
            AL.Sourcei(_sources[i], AL.AL_SOURCE_RELATIVE, AL.AL_TRUE);
            AL.Sourcef(_sources[i], AL.AL_REFERENCE_DISTANCE, 1.0f);
            AL.Sourcef(_sources[i], AL.AL_ROLLOFF_FACTOR, 0.22f);
            AL.Sourcef(_sources[i], AL.AL_MAX_DISTANCE, 6f);
        }

        _musicSource = AL.GenSource();
        AL.Sourcei(_musicSource, AL.AL_SOURCE_RELATIVE, AL.AL_TRUE);
        AL.Source3f(_musicSource, AL.AL_POSITION, 0, 0, -1);

        BuildAll();
        AL.ListenerGain(_master);
    }

    public void SetScreenSize(int w, int h)
    {
        _halfW = Math.Max(1, w * 0.5f);
        _halfH = Math.Max(1, h * 0.5f);
    }

    // ---- sound design ----------------------------------------------------

    void Add(Sfx id, Wave w)
    {
        uint buf = AL.GenBuffer();
        AL.BufferData(buf, w.Declick().ToPcm16(), 1, w.Rate);
        _buffers[id] = buf;
    }

    void BuildAll()
    {
        // Startup: a rising major triad with a soft bell timbre and a long tail.
        var start = new Wave(3.2);
        float[] chord = { Wave.Note(60), Wave.Note(64), Wave.Note(67), Wave.Note(72) };
        for (int i = 0; i < chord.Length; i++)
            start.Fm(chord[i], 2.01f, 3.2f, 0.30f, 0.10f * i, 2.6f - 0.1f * i, 0.02f, 1.6f);
        start.Sweep(180, 900, 0.10f, 0f, 0.55f, 0.06f, 1.2f);
        start.Delay(0.19f, 0.42f, 0.55f).LowPass(6500).Normalize(0.88f);
        Add(Sfx.Startup, start);

        // Shutdown: the same triad inverted and falling away.
        var stop = new Wave(2.4);
        stop.Fm(Wave.Note(67), 2.0f, 2.6f, 0.30f, 0.00f, 1.5f, 0.02f, 1.8f);
        stop.Fm(Wave.Note(60), 2.0f, 2.6f, 0.30f, 0.18f, 1.7f, 0.02f, 1.8f);
        stop.Fm(Wave.Note(53), 2.0f, 2.2f, 0.28f, 0.36f, 1.8f, 0.02f, 1.8f);
        stop.Sweep(700, 140, 0.12f, 0.05f, 1.1f, 0.04f, 1.4f);
        stop.Delay(0.17f, 0.36f, 0.5f).LowPass(5200).Normalize(0.85f);
        Add(Sfx.Shutdown, stop);

        var logon = new Wave(1.5);
        logon.Fm(Wave.Note(72), 1.5f, 2.0f, 0.32f, 0f, 0.9f, 0.01f, 2f);
        logon.Fm(Wave.Note(79), 1.5f, 2.0f, 0.26f, 0.14f, 0.9f, 0.01f, 2f);
        logon.Delay(0.13f, 0.3f, 0.4f).Normalize(0.8f);
        Add(Sfx.Logon, logon);

        var logoff = new Wave(1.2);
        logoff.Fm(Wave.Note(72), 1.5f, 2.0f, 0.30f, 0f, 0.7f, 0.01f, 2f);
        logoff.Fm(Wave.Note(65), 1.5f, 2.0f, 0.28f, 0.13f, 0.8f, 0.01f, 2f);
        logoff.Normalize(0.78f);
        Add(Sfx.Logoff, logoff);

        // Click: a tiny filtered noise transient plus a wooden thump.
        var click = new Wave(0.05);
        click.Noise(0.5f, 0, 0.02f, 0.0005f, 6f);
        click.Sine(1650, 0.25f, 0, 0.02f, 0.001f, 5f);
        click.HighPass(700).Normalize(0.42f);
        Add(Sfx.Click, click);

        var tick = new Wave(0.04);
        tick.Noise(0.4f, 0, 0.014f, 0.0004f, 7f);
        tick.Sine(2400, 0.2f, 0, 0.012f, 0.0006f, 6f);
        tick.HighPass(1200).Normalize(0.34f);
        Add(Sfx.Tick, tick);

        var menuOpen = new Wave(0.22);
        menuOpen.Sweep(520, 1150, 0.30f, 0, 0.13f, 0.006f, 2.2f);
        menuOpen.Noise(0.10f, 0, 0.06f, 0.001f, 5f);
        menuOpen.LowPass(7000).Normalize(0.48f);
        Add(Sfx.MenuOpen, menuOpen);

        var menuClose = new Wave(0.2);
        menuClose.Sweep(1000, 460, 0.28f, 0, 0.11f, 0.005f, 2.4f);
        menuClose.LowPass(6000).Normalize(0.42f);
        Add(Sfx.MenuClose, menuClose);

        var winOpen = new Wave(0.35);
        winOpen.Sweep(300, 820, 0.26f, 0, 0.18f, 0.01f, 2f);
        winOpen.Fm(700, 2f, 1.4f, 0.18f, 0.02f, 0.22f, 0.008f, 2.4f);
        winOpen.Noise(0.09f, 0, 0.09f, 0.002f, 4f);
        winOpen.LowPass(8000).Normalize(0.55f);
        Add(Sfx.WindowOpen, winOpen);

        var winClose = new Wave(0.32);
        winClose.Sweep(760, 240, 0.26f, 0, 0.17f, 0.008f, 2.2f);
        winClose.Noise(0.09f, 0, 0.08f, 0.002f, 4f);
        winClose.LowPass(6500).Normalize(0.52f);
        Add(Sfx.WindowClose, winClose);

        var minimize = new Wave(0.3);
        minimize.Sweep(900, 260, 0.3f, 0, 0.2f, 0.006f, 1.8f);
        minimize.LowPass(5000).Normalize(0.48f);
        Add(Sfx.Minimize, minimize);

        var restore = new Wave(0.3);
        restore.Sweep(260, 900, 0.3f, 0, 0.2f, 0.006f, 1.8f);
        restore.LowPass(7000).Normalize(0.48f);
        Add(Sfx.Restore, restore);

        // Dialog stings, in the spirit of the XP system sounds.
        var error = new Wave(1.0);
        error.Fm(Wave.Note(58), 1.41f, 4.0f, 0.34f, 0f, 0.45f, 0.004f, 2.2f);
        error.Fm(Wave.Note(53), 1.41f, 4.0f, 0.34f, 0.16f, 0.6f, 0.004f, 2.2f);
        error.Delay(0.1f, 0.25f, 0.35f).Normalize(0.8f);
        Add(Sfx.Error, error);

        var warn = new Wave(0.8);
        warn.Fm(Wave.Note(69), 2.0f, 3.0f, 0.32f, 0f, 0.35f, 0.004f, 2.2f);
        warn.Fm(Wave.Note(64), 2.0f, 3.0f, 0.30f, 0.13f, 0.45f, 0.004f, 2.2f);
        warn.Normalize(0.74f);
        Add(Sfx.Warning, warn);

        var info = new Wave(0.7);
        info.Fm(Wave.Note(76), 2.0f, 2.2f, 0.30f, 0f, 0.5f, 0.004f, 2.4f);
        info.Fm(Wave.Note(83), 2.0f, 2.0f, 0.18f, 0.05f, 0.4f, 0.004f, 2.6f);
        info.Delay(0.08f, 0.22f, 0.3f).Normalize(0.7f);
        Add(Sfx.Info, info);

        var question = new Wave(0.8);
        question.Fm(Wave.Note(69), 2.0f, 2.4f, 0.30f, 0f, 0.35f, 0.004f, 2.2f);
        question.Fm(Wave.Note(74), 2.0f, 2.4f, 0.30f, 0.14f, 0.45f, 0.004f, 2.2f);
        question.Normalize(0.72f);
        Add(Sfx.Question, question);

        var balloon = new Wave(0.45);
        balloon.Fm(Wave.Note(81), 3.0f, 1.6f, 0.26f, 0f, 0.3f, 0.003f, 3f);
        balloon.Fm(Wave.Note(88), 3.0f, 1.4f, 0.16f, 0.06f, 0.25f, 0.003f, 3f);
        balloon.Normalize(0.6f);
        Add(Sfx.Balloon, balloon);

        var navigate = new Wave(0.25);
        navigate.Noise(0.35f, 0, 0.12f, 0.004f, 3f);
        navigate.Sweep(400, 1400, 0.14f, 0, 0.1f, 0.004f, 2.5f);
        navigate.HighPass(500).LowPass(9000).Normalize(0.4f);
        Add(Sfx.Navigate, navigate);

        // Crumple for the recycle bin.
        var trash = new Wave(0.7);
        for (int i = 0; i < 14; i++)
            trash.Noise(0.30f, 0.02f * i + (i % 3) * 0.01f, 0.05f, 0.001f, 5f);
        trash.HighPass(900).LowPass(9000).Normalize(0.55f);
        Add(Sfx.Trash, trash);

        // Minesweeper.
        var reveal = new Wave(0.06);
        reveal.Sine(900, 0.3f, 0, 0.03f, 0.001f, 4f);
        reveal.Noise(0.15f, 0, 0.015f, 0.0005f, 6f);
        reveal.Normalize(0.35f);
        Add(Sfx.MineReveal, reveal);

        var flag = new Wave(0.12);
        flag.Sine(1400, 0.3f, 0, 0.05f, 0.001f, 4f);
        flag.Sine(2100, 0.2f, 0.03f, 0.05f, 0.001f, 4f);
        flag.Normalize(0.42f);
        Add(Sfx.MineFlag, flag);

        var boom = new Wave(1.4);
        boom.Noise(0.9f, 0, 0.9f, 0.002f, 2.2f);
        boom.Sweep(160, 30, 0.6f, 0, 0.8f, 0.004f, 1.6f);
        boom.LowPass(1400).Normalize(0.95f);
        Add(Sfx.MineBoom, boom);

        var win = new Wave(1.6);
        int[] fanfare = { 60, 64, 67, 72, 76 };
        for (int i = 0; i < fanfare.Length; i++)
            win.Triangle(Wave.Note(fanfare[i]), 0.26f, 0.11f * i, 0.55f, 0.005f, 1.6f);
        win.Delay(0.12f, 0.3f, 0.4f).Normalize(0.8f);
        Add(Sfx.MineWin, win);

        // Antivirus "scanning".
        var scan = new Wave(0.1);
        scan.Square(1800, 0.22f, 0, 0.045f, 0.5f, 0.001f, 3f);
        scan.LowPass(4000).Normalize(0.32f);
        Add(Sfx.ScanBeep, scan);

        var scanDone = new Wave(0.9);
        scanDone.Triangle(Wave.Note(72), 0.28f, 0f, 0.25f, 0.004f, 2f);
        scanDone.Triangle(Wave.Note(76), 0.28f, 0.14f, 0.25f, 0.004f, 2f);
        scanDone.Triangle(Wave.Note(79), 0.28f, 0.28f, 0.45f, 0.004f, 2f);
        scanDone.Normalize(0.72f);
        Add(Sfx.ScanDone, scanDone);

        var type = new Wave(0.035);
        type.Noise(0.45f, 0, 0.012f, 0.0003f, 7f);
        type.Sine(2900, 0.14f, 0, 0.008f, 0.0004f, 6f);
        type.HighPass(1500).Normalize(0.22f);
        Add(Sfx.Typewriter, type);

        var shutter = new Wave(0.25);
        shutter.Noise(0.6f, 0, 0.04f, 0.0006f, 5f);
        shutter.Noise(0.4f, 0.07f, 0.05f, 0.0008f, 4f);
        shutter.HighPass(800).Normalize(0.5f);
        Add(Sfx.Shutter, shutter);

        var spin = new Wave(1.1);
        spin.Noise(0.25f, 0, 1.0f, 0.15f, 0.6f);
        spin.Sine(120, 0.10f, 0, 1.0f, 0.15f, 0.6f);
        spin.LowPass(1100).Normalize(0.34f);
        Add(Sfx.DriveSpin, spin);
    }

    // ---- playback --------------------------------------------------------

    uint NextSource()
    {
        // Prefer an idle source; otherwise steal the oldest in rotation.
        for (int i = 0; i < SourceCount; i++)
        {
            uint s = _sources[(_next + i) % SourceCount];
            if (AL.GetSourcei(s, AL.AL_SOURCE_STATE) != AL.AL_PLAYING)
            {
                _next = (_next + i + 1) % SourceCount;
                return s;
            }
        }
        uint steal = _sources[_next];
        _next = (_next + 1) % SourceCount;
        AL.Stop(steal);
        return steal;
    }

    public void Play(Sfx id, float gain = 1f, float pitch = 1f)
        => PlayAt(id, float.NaN, float.NaN, gain, pitch);

    /// <summary>Plays positioned at a point on screen. Pixels map to a shallow
    /// plane in front of the listener, which OpenAL turns into stereo placement.</summary>
    public void PlayAt(Sfx id, float screenX, float screenY, float gain = 1f, float pitch = 1f)
    {
        if (!_ok || Muted || !_buffers.TryGetValue(id, out uint buf)) return;

        uint src = NextSource();
        AL.Sourcei(src, AL.AL_BUFFER, (int)buf);
        AL.Sourcef(src, AL.AL_GAIN, Math.Clamp(gain, 0f, 1f));
        AL.Sourcef(src, AL.AL_PITCH, Math.Clamp(pitch, 0.25f, 4f));

        if (float.IsNaN(screenX))
        {
            AL.Source3f(src, AL.AL_POSITION, 0, 0, -1f);
        }
        else
        {
            float x = (screenX - _halfW) / _halfW;          // -1 .. 1
            float y = -(screenY - _halfH) / _halfH * 0.35f;  // gentle vertical cue
            AL.Source3f(src, AL.AL_POSITION, x * 1.4f, y, -1f);
        }
        AL.Play(src);
    }

    /// <summary>Loops a generated tune, used by the media player.</summary>
    public void PlayMusic(short[] pcm, int rate, bool loop = true)
    {
        if (!_ok) return;
        StopMusic();
        _musicBuffer = AL.GenBuffer();
        AL.BufferData(_musicBuffer, pcm, 1, rate);
        AL.Sourcei(_musicSource, AL.AL_BUFFER, (int)_musicBuffer);
        AL.Sourcei(_musicSource, AL.AL_LOOPING, loop ? AL.AL_TRUE : AL.AL_FALSE);
        AL.Sourcef(_musicSource, AL.AL_GAIN, 0.55f);
        AL.Play(_musicSource);
    }

    public void PauseMusic() { if (_ok) AL.Pause(_musicSource); }
    public void ResumeMusic() { if (_ok) AL.Play(_musicSource); }
    public bool MusicPlaying => _ok && AL.GetSourcei(_musicSource, AL.AL_SOURCE_STATE) == AL.AL_PLAYING;

    public void SetMusicVolume(float v)
    {
        if (_ok) AL.Sourcef(_musicSource, AL.AL_GAIN, Math.Clamp(v, 0, 1));
    }

    public void StopMusic()
    {
        if (!_ok) return;
        AL.Stop(_musicSource);
        AL.Sourcei(_musicSource, AL.AL_BUFFER, 0);
        if (_musicBuffer != 0) { AL.DeleteBuffer(_musicBuffer); _musicBuffer = 0; }
    }

    public void StopAll()
    {
        if (!_ok) return;
        foreach (uint s in _sources) AL.Stop(s);
        StopMusic();
    }

    public void Dispose()
    {
        if (!_ok) return;
        StopAll();
        foreach (uint s in _sources) AL.DeleteSource(s);
        AL.DeleteSource(_musicSource);
        foreach (uint b in _buffers.Values) AL.DeleteBuffer(b);
        AL.Shutdown();
    }
}
