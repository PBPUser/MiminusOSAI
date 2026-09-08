namespace Miminus.Audio;

public enum Sfx
{
    Startup, Shutdown, Logon, Logoff,
    Click, MenuOpen, MenuClose, WindowOpen, WindowClose, Minimize, Restore,
    Error, Warning, Info, Question,
    Balloon, Tick, Navigate, Trash,
    Charm, Tile, Lock, Unlock, Snap, Key,
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
        // Version 8 retuned the whole scheme: shorter than the XP one, higher,
        // and built out of struck bells rather than swelling pads. Nothing here
        // rings for three seconds any more — a sound that outlasts the thing it
        // announces is a sound from an older machine.

        // Startup: four bells up a major ninth, struck close together and gone
        // inside a second and a half.
        var start = new Wave(1.9);
        float[] chord = { Wave.Note(69), Wave.Note(76), Wave.Note(81), Wave.Note(88) };
        for (int i = 0; i < chord.Length; i++)
            start.Fm(chord[i], 3.01f, 2.4f, 0.28f, 0.055f * i, 1.5f - 0.12f * i, 0.004f, 2.4f);
        start.Fm(Wave.Note(57), 2.0f, 1.4f, 0.14f, 0f, 0.7f, 0.006f, 2.2f);
        start.Delay(0.11f, 0.24f, 0.38f).LowPass(11000).Normalize(0.86f);
        Add(Sfx.Startup, start);

        // Shutdown: the same bells, two of them, falling.
        var stop = new Wave(1.4);
        stop.Fm(Wave.Note(81), 3.0f, 2.2f, 0.28f, 0.00f, 0.9f, 0.004f, 2.4f);
        stop.Fm(Wave.Note(74), 3.0f, 2.2f, 0.28f, 0.10f, 1.0f, 0.004f, 2.4f);
        stop.Fm(Wave.Note(69), 3.0f, 2.0f, 0.24f, 0.20f, 1.1f, 0.004f, 2.4f);
        stop.Delay(0.10f, 0.22f, 0.34f).LowPass(9000).Normalize(0.82f);
        Add(Sfx.Shutdown, stop);

        // Logon: two bells a fifth apart, and nothing else.
        var logon = new Wave(1.0);
        logon.Fm(Wave.Note(81), 3.0f, 1.8f, 0.30f, 0f, 0.62f, 0.003f, 2.6f);
        logon.Fm(Wave.Note(88), 3.0f, 1.6f, 0.22f, 0.07f, 0.60f, 0.003f, 2.8f);
        logon.Delay(0.09f, 0.2f, 0.3f).Normalize(0.76f);
        Add(Sfx.Logon, logon);

        var logoff = new Wave(0.9);
        logoff.Fm(Wave.Note(88), 3.0f, 1.6f, 0.26f, 0f, 0.5f, 0.003f, 2.8f);
        logoff.Fm(Wave.Note(81), 3.0f, 1.6f, 0.26f, 0.08f, 0.6f, 0.003f, 2.8f);
        logoff.Normalize(0.72f);
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

        // Menus: a brushed rise and fall, shorter and airier than the XP pair.
        var menuOpen = new Wave(0.16);
        menuOpen.Sweep(760, 1650, 0.24f, 0, 0.09f, 0.003f, 2.6f);
        menuOpen.Noise(0.08f, 0, 0.045f, 0.0008f, 6f);
        menuOpen.HighPass(400).LowPass(9500).Normalize(0.42f);
        Add(Sfx.MenuOpen, menuOpen);

        var menuClose = new Wave(0.14);
        menuClose.Sweep(1500, 680, 0.22f, 0, 0.08f, 0.003f, 2.8f);
        menuClose.HighPass(400).LowPass(8000).Normalize(0.36f);
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

        // The dialog family: four two-note figures on one timbre, told apart by
        // their interval rather than by how unpleasant they are. Version 8
        // stopped shouting at people who clicked the wrong thing.
        var error = new Wave(0.85);
        error.Fm(Wave.Note(70), 2.0f, 2.6f, 0.30f, 0f, 0.42f, 0.003f, 2.4f);
        error.Fm(Wave.Note(65), 2.0f, 2.6f, 0.30f, 0.14f, 0.55f, 0.003f, 2.4f);
        error.Fm(Wave.Note(53), 2.0f, 1.6f, 0.12f, 0.14f, 0.5f, 0.004f, 2.2f);
        error.Delay(0.08f, 0.2f, 0.28f).LowPass(9000).Normalize(0.76f);
        Add(Sfx.Error, error);

        var warn = new Wave(0.7);
        warn.Fm(Wave.Note(76), 2.0f, 2.4f, 0.30f, 0f, 0.34f, 0.003f, 2.4f);
        warn.Fm(Wave.Note(71), 2.0f, 2.4f, 0.28f, 0.12f, 0.44f, 0.003f, 2.4f);
        warn.LowPass(10000).Normalize(0.7f);
        Add(Sfx.Warning, warn);

        var info = new Wave(0.6);
        info.Fm(Wave.Note(83), 3.0f, 1.8f, 0.28f, 0f, 0.38f, 0.003f, 2.8f);
        info.Fm(Wave.Note(88), 3.0f, 1.6f, 0.18f, 0.06f, 0.36f, 0.003f, 3f);
        info.Delay(0.07f, 0.18f, 0.24f).Normalize(0.66f);
        Add(Sfx.Info, info);

        var question = new Wave(0.7);
        question.Fm(Wave.Note(76), 2.0f, 2.2f, 0.28f, 0f, 0.32f, 0.003f, 2.4f);
        question.Fm(Wave.Note(83), 2.0f, 2.2f, 0.28f, 0.12f, 0.44f, 0.003f, 2.4f);
        question.Normalize(0.68f);
        Add(Sfx.Question, question);

        // The notification: two struck bars a fourth apart, which is the shape
        // every toast of that era took.
        var balloon = new Wave(0.7);
        balloon.Fm(Wave.Note(84), 4.01f, 1.2f, 0.26f, 0f, 0.34f, 0.002f, 3.2f);
        balloon.Fm(Wave.Note(89), 4.01f, 1.1f, 0.22f, 0.09f, 0.42f, 0.002f, 3.4f);
        balloon.Delay(0.06f, 0.16f, 0.26f).HighPass(300).Normalize(0.62f);
        Add(Sfx.Balloon, balloon);

        var navigate = new Wave(0.18);
        navigate.Noise(0.28f, 0, 0.08f, 0.002f, 4f);
        navigate.Sweep(700, 1900, 0.12f, 0, 0.07f, 0.002f, 3f);
        navigate.HighPass(700).LowPass(11000).Normalize(0.34f);
        Add(Sfx.Navigate, navigate);

        // ---- the six version 8 asked for -------------------------------------

        // A charm strip coming out of the edge: a brushed sweep with no pitch
        // in it to speak of, so it reads as movement rather than as a note.
        var charm = new Wave(0.3);
        charm.Noise(0.34f, 0, 0.2f, 0.03f, 2.2f);
        charm.Sweep(300, 2400, 0.10f, 0, 0.16f, 0.02f, 2.6f);
        charm.HighPass(600).LowPass(12000).Normalize(0.36f);
        Add(Sfx.Charm, charm);

        // A tile going down: a short wooden knock, lower than a click.
        var tile = new Wave(0.09);
        tile.Sine(420, 0.32f, 0, 0.05f, 0.001f, 4.5f);
        tile.Sine(840, 0.14f, 0, 0.03f, 0.001f, 5f);
        tile.Noise(0.16f, 0, 0.02f, 0.0006f, 6f);
        tile.LowPass(5000).Normalize(0.44f);
        Add(Sfx.Tile, tile);

        // Locking: two low bells, closing.
        var lockDown = new Wave(0.8);
        lockDown.Fm(Wave.Note(69), 2.0f, 1.8f, 0.28f, 0f, 0.4f, 0.003f, 2.6f);
        lockDown.Fm(Wave.Note(62), 2.0f, 1.8f, 0.28f, 0.1f, 0.55f, 0.003f, 2.6f);
        lockDown.LowPass(7000).Normalize(0.7f);
        Add(Sfx.Lock, lockDown);

        // Unlocking: the same two, the other way up, and brighter.
        var unlock = new Wave(0.7);
        unlock.Fm(Wave.Note(76), 3.0f, 1.6f, 0.26f, 0f, 0.36f, 0.003f, 2.8f);
        unlock.Fm(Wave.Note(83), 3.0f, 1.5f, 0.24f, 0.09f, 0.44f, 0.003f, 3f);
        unlock.Delay(0.07f, 0.18f, 0.24f).Normalize(0.68f);
        Add(Sfx.Unlock, unlock);

        // Snap: a short brushed thud, the sound of something meeting an edge.
        var snap = new Wave(0.16);
        snap.Noise(0.34f, 0, 0.07f, 0.0008f, 5f);
        snap.Sine(240, 0.28f, 0, 0.08f, 0.001f, 4f);
        snap.Sweep(1500, 500, 0.12f, 0, 0.06f, 0.001f, 4f);
        snap.LowPass(4200).Normalize(0.46f);
        Add(Sfx.Snap, snap);

        // A key on the on-screen keyboard: drier and duller than a mouse click,
        // because it is a key.
        var key = new Wave(0.05);
        key.Noise(0.4f, 0, 0.016f, 0.0004f, 7f);
        key.Sine(1200, 0.2f, 0, 0.014f, 0.0006f, 6f);
        key.LowPass(6000).Normalize(0.3f);
        Add(Sfx.Key, key);

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
