using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Проигрыватель Миминус — the AIMP-style player from part 3, right down
/// to the clip it ends on: "BolgenOS on TV".
///
/// Audio tracks are synthesised chiptunes played through OpenAL; the video track
/// is a procedurally drawn news broadcast, so the whole thing ships without a
/// single media file.
///
/// Version 8 re-skinned it. Under «Миминус 8» the case is flat black, the
/// transport is a row of outlined circles with one accent-filled button in the
/// middle, and the seek bar is a hairline — the look players took on when they
/// stopped pretending to be hi-fi separates. Under the older themes it keeps
/// the brushed AIMP panel it had.
///
/// The flat skin is drawn in the theme's own accent rather than version 8's, so
/// under Luna it is a Luna player: the colour follows the window, and only the
/// full-screen programs are painted in the one fixed blue.</summary>
public sealed class MediaPlayerWindow : OsWindow
{
    sealed record Track(string Key, double Seconds, bool IsVideo, int Seed);

    static readonly Track[] Playlist =
    {
        new("track.bolgenos_on_tv_avi", 96, true, 0),
        new("track.songa_mp3", 74, false, 1),
        new("track.chudo_mp3", 62, false, 2),
        new("track.metro2033_mp3", 88, false, 3),
        new("track.miminus_os_anthem_mp3", 48, false, 4),
    };

    int _current;
    bool _playing;
    double _position;
    float _volume = 0.6f;
    bool _shuffle, _repeat = true;
    int _tab;   // 0 playlist, 1 equalizer, 2 about

    readonly float[] _spectrum = new float[28];
    readonly float[] _eq = new float[10];

    bool _seeking;

    public override string Title
        => L.T("player.miminus_media_player") + " — " + Current.Name;

    public override string TaskbarTitle => Current.Name;
    public override float MinWidth => 520;
    public override float MinHeight => 360;

    (string Name, Track T) Current
    {
        get
        {
            var t = Playlist[Math.Clamp(_current, 0, Playlist.Length - 1)];
            return (L.T(t.Key), t);
        }
    }

    public MediaPlayerWindow(VNode file)
    {
        Icon = IconId.MediaPlayer;
        Bounds = new Rect(0, 0, 700, 460);

        for (int i = 0; i < _eq.Length; i++) _eq[i] = 0.5f;

        // Opening a specific file selects the matching entry.
        if (file != null)
        {
            int idx = Array.FindIndex(Playlist, t =>
                L.T(t.Key).Equals(file.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _current = idx;
            else if (file.Kind == NodeKind.Video) _current = 0;
        }

        BuildMenu();
    }

    void BuildMenu()
    {
        Menu = new MenuBar();
        Menu.Add(L.T("player.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("player.open"), () =>
                Wm.Open(new FilePickerWindow(Shell.Fs, L.T("player.open_2"), n =>
                {
                    int idx = Array.FindIndex(Playlist, t => L.T(t.Key) == n.Name);
                    if (idx >= 0) { _current = idx; Play(_ctx); }
                }), _ctx)),
            MenuItem.Sep(),
            MenuItem.Of(L.T("player.exit"), Close),
        });
        Menu.Add(L.T("player.playback"), () => new List<MenuItem>
        {
            MenuItem.Of(_playing ? L.T("player.pause") : L.T("player.play"),
                        () => TogglePlay(_ctx), shortcut: "Space"),
            MenuItem.Of(L.T("player.stop"), () => Stop(_ctx)),
            MenuItem.Sep(),
            MenuItem.Of(L.T("player.previous"), () => Step(_ctx, -1)),
            MenuItem.Of(L.T("player.next"), () => Step(_ctx, 1)),
            MenuItem.Sep(),
            MenuItem.Check(L.T("player.shuffle"), _shuffle, () => _shuffle = !_shuffle),
            MenuItem.Check(L.T("player.repeat"), _repeat, () => _repeat = !_repeat),
        });
        Menu.Add(L.T("player.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("player.about"), () =>
                Shell.MessageBox(_ctx, L.T("player.miminus_media_player"),
                    L.T("player.miminus_media_player_2_61_audio_synthesised"),
                    MsgButtons.Ok, IconId.MediaPlayer, null, Sfx.Info), IconId.DlgInfo),
        });
    }

    UiContext _ctx;

    public override void Tick(UiContext c, float dt)
    {
        if (!_playing || _seeking) return;

        _position += dt;
        if (_position >= Current.T.Seconds)
        {
            _position = 0;
            if (_repeat || _current < Playlist.Length - 1) Step(c, 1);
            else Stop(c);
        }

        // Spectrum bars: a cheap animated stand-in driven by the clock, biased so
        // the low bands move more than the highs, like a real analyser.
        for (int i = 0; i < _spectrum.Length; i++)
        {
            float f = 0.5f + i * 0.35f;
            float target = MathF.Abs(MathF.Sin((float)c.Time * f + i * 0.7f)) *
                           MathF.Abs(MathF.Cos((float)c.Time * 0.8f + i * 0.23f));
            target *= 1f - i / (float)_spectrum.Length * 0.55f;
            target *= _volume;
            _spectrum[i] += (target - _spectrum[i]) * MathF.Min(1, dt * 12);
        }
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        if (c.Theme.Modern) c.R.FillRect(client, Color.Rgb(0x1B1B1B));
        else c.R.FillRectV(client, Color.Rgb(0x2A3038), Color.Rgb(0x171B21));

        var area = client;
        var side = area.CutRight(232);
        var transport = area.CutBottom(76);

        DrawStage(c, area.Deflate(8, 8, 4, 4));
        DrawTransport(c, transport.Deflate(8, 0, 4, 8));
        DrawSidePanel(c, side.Deflate(4, 8, 8, 8));

        if (!c.KeyboardHandled)
        {
            if (c.In.KeyPressed(Keys.Space)) { TogglePlay(c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.Right)) { _position = Math.Min(Current.T.Seconds, _position + 5); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.Left)) { _position = Math.Max(0, _position - 5); c.KeyboardHandled = true; }
        }
    }

    // ---- stage (video or visualiser) -------------------------------------

    void DrawStage(UiContext c, Rect r)
    {
        c.R.FillRect(r, Color.Black);
        c.R.DrawRect(r, c.Theme.Modern ? Color.Rgb(0x2E2E2E) : Color.Rgb(0x3A424C));
        c.R.PushClip(r);

        if (Current.T.IsVideo) DrawVideo(c, r.Deflate(1));
        else DrawVisualiser(c, r.Deflate(1));

        c.R.PopClip();
    }

    /// <summary>The BolgenOS TV news segment, drawn rather than decoded: a studio
    /// wall, a desk, a presenter, a monitor showing a yellow desktop, and the
    /// lower-third caption the clip is famous for.</summary>
    void DrawVideo(UiContext c, Rect r)
    {
        double t = _playing ? _position : _position;

        // Keep a 4:3 pillarboxed frame inside whatever the window gives us.
        float aspect = 4f / 3f;
        float w = MathF.Min(r.W, r.H * aspect);
        float h = w / aspect;
        var f = new Rect(r.CenterX - w * 0.5f, r.CenterY - h * 0.5f, w, h);

        // Studio backdrop.
        c.R.FillRectV(f, Color.Rgb(0x2E4A6E), Color.Rgb(0x15263C));
        for (int i = 0; i < 7; i++)
        {
            float x = f.X + f.W * (i / 7f);
            c.R.FillRectV(new Rect(x, f.Y, f.W / 14f, f.H * 0.62f),
                          Color.Rgba(0x6FA8D8, 40), Color.Rgba(0x6FA8D8, 5));
        }

        // Desk.
        var desk = new Rect(f.X, f.Y + f.H * 0.64f, f.W, f.H * 0.36f);
        c.R.FillRectV(desk, Color.Rgb(0x8C6A45), Color.Rgb(0x4A3722));
        c.R.FillRect(new Rect(desk.X, desk.Y, desk.W, 3), Color.Rgba(0xFFFFFF, 60));

        // Presenter: shoulders, head, a slight idle sway.
        float sway = MathF.Sin((float)t * 1.6f) * f.W * 0.006f;
        float px = f.CenterX - f.W * 0.14f + sway;
        float headR = f.H * 0.085f;
        float headY = f.Y + f.H * 0.40f;

        c.R.FillTriangle(px - f.W * 0.13f, desk.Y + 2, px + f.W * 0.13f, desk.Y + 2,
                         px, headY + headR * 0.5f, Color.Rgb(0x2A3550));
        c.R.FillRect(new Rect(px - f.W * 0.13f, headY + headR, f.W * 0.26f, desk.Y - headY - headR + 2),
                     Color.Rgb(0x2A3550));
        c.R.FillCircle(px, headY, headR, Color.Rgb(0xE0B48C));
        c.R.FillCircle(px, headY - headR * 0.55f, headR * 0.95f, Color.Rgb(0x5A3E28));
        // Eyes blink every few seconds.
        bool blink = (t % 4.0) < 0.12;
        float eyeH = blink ? 0.8f : 2.2f;
        c.R.FillRect(new Rect(px - headR * 0.42f, headY - headR * 0.08f, 3, eyeH), Color.Rgb(0x201810));
        c.R.FillRect(new Rect(px + headR * 0.22f, headY - headR * 0.08f, 3, eyeH), Color.Rgb(0x201810));

        // Monitor on the desk showing the yellow МИМИНУС desktop.
        var mon = new Rect(f.CenterX + f.W * 0.13f, f.Y + f.H * 0.40f, f.W * 0.26f, f.H * 0.22f);
        c.R.FillRect(mon.Inflate(3), Color.Rgb(0x30363E));
        c.R.FillRect(mon, Color.Rgb(0xFFD200));
        float capW = c.F.Small.Measure("МИМИНУС");
        c.F.Small.Draw(c.R, "МИМИНУС", mon.CenterX - capW * 0.5f, mon.CenterY - c.F.Small.Height * 0.5f, Color.Black);
        c.R.FillRect(new Rect(mon.CenterX - mon.W * 0.06f, mon.Bottom + 3, mon.W * 0.12f, f.H * 0.03f),
                     Color.Rgb(0x30363E));

        // Lower third.
        var band = new Rect(f.X + f.W * 0.06f, f.Y + f.H * 0.78f, f.W * 0.62f, f.H * 0.09f);
        c.R.FillRectH(band, Color.Rgba(0xC81A1A, 235), Color.Rgba(0xC81A1A, 120));
        c.R.FillRect(new Rect(band.X, band.Y, band.W * 0.02f, band.H), Color.White);
        var band2 = new Rect(band.X, band.Bottom + 2, band.W * 0.8f, band.H * 0.72f);
        c.R.FillRectH(band2, Color.Rgba(0x101820, 220), Color.Rgba(0x101820, 90));

        c.F.UiBold.Draw(c.R, L.T("player.bolgenos_an_operating_system_from_scratch"),
                        band.X + band.W * 0.04f, band.CenterY - c.F.UiBold.Height * 0.5f, Color.White);
        c.F.Small.Draw(c.R, L.T("player.our_answer_miminus_os"),
                       band2.X + band.W * 0.04f, band2.CenterY - c.F.Small.Height * 0.5f, Color.Rgb(0xFFD200));

        // Channel bug and clock.
        c.F.Small.Draw(c.R, "МИМИНУС ТВ", f.Right - c.F.Small.Measure("МИМИНУС ТВ") - 10, f.Y + 8,
                       Color.Rgba(0xFFFFFF, 190));
        string clock = TimeSpan.FromSeconds(_position).ToString(@"mm\:ss");
        c.F.Small.Draw(c.R, clock, f.Right - c.F.Small.Measure(clock) - 10, f.Y + 8 + c.F.Small.Height + 2,
                       Color.Rgba(0xFFFFFF, 140));

        // Analogue wobble: faint scanlines and a rolling bright band.
        for (float y = f.Y; y < f.Bottom; y += 3)
            c.R.FillRect(new Rect(f.X, y, f.W, 1), Color.Rgba(0x000000, 26));
        float rollY = f.Y + (float)((t * 40) % (f.H + 60)) - 30;
        c.R.FillRectV(new Rect(f.X, rollY, f.W, 30), Color.Rgba(0xFFFFFF, 0), Color.Rgba(0xFFFFFF, 14));

        if (!_playing)
        {
            c.R.FillRect(f, Color.Rgba(0x000000, 110));
            string paused = L.T("player.paused");
            float pw = c.F.Big.Measure(paused);
            c.F.Big.Draw(c.R, paused, f.CenterX - pw * 0.5f, f.CenterY - c.F.Big.Height * 0.5f,
                         Color.Rgba(0xFFFFFF, 200));
        }
    }

    void DrawVisualiser(UiContext c, Rect r)
    {
        // Album-art placeholder.
        bool modern = c.Theme.Modern;

        float artSize = MathF.Min(r.H * 0.62f, r.W * 0.3f);
        var art = new Rect(r.X + 24, r.CenterY - artSize * 0.5f, artSize, artSize);
        if (modern)
        {
            c.R.FillRect(art, c.Theme.Accent.Shade(0.55f));
            c.R.DrawRect(art, c.Theme.Accent);
        }
        else
        {
            c.R.FillRectV(art, Color.Rgb(0x3A4654), Color.Rgb(0x1E2630));
            c.R.DrawRect(art, Color.Rgb(0x55606E));
        }
        Icons.Draw(c.R, IconId.AudioFile, art.Deflate(artSize * 0.26f));

        string name = Current.Name;
        c.F.Big.Draw(c.R, c.F.Big.Ellipsize(name, r.Right - art.Right - 40), art.Right + 24,
                     art.Y + 6, Color.White);
        c.F.Ui.Draw(c.R, L.T("player.miminus_os_soundtrack"), art.Right + 26,
                    art.Y + 8 + c.F.Big.Height, Color.Rgba(0xFFFFFF, 160));

        // Spectrum.
        var bars = new Rect(art.Right + 24, r.Bottom - r.H * 0.42f, r.Right - art.Right - 40, r.H * 0.34f);
        float bw = bars.W / _spectrum.Length;
        for (int i = 0; i < _spectrum.Length; i++)
        {
            float h = MathF.Max(2, _spectrum[i] * bars.H);
            var bar = new Rect(bars.X + i * bw + 1, bars.Bottom - h, bw - 2, h);
            if (modern)
            {
                // One flat colour, and a cap that is simply a lighter shade.
                c.R.FillRect(bar, c.Theme.Accent);
                c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 2), Color.Rgb(0x9FD4FF));
            }
            else
            {
                c.R.FillRectV(bar, Color.Rgb(0x7FE0FF), Color.Rgb(0x1E70C0));
                c.R.FillRect(new Rect(bar.X, bar.Y, bar.W, 2), Color.White);
            }
        }
        c.R.FillRect(new Rect(bars.X, bars.Bottom, bars.W, 1), Color.Rgba(0xFFFFFF, 60));
    }

    // ---- transport -------------------------------------------------------

    void DrawTransport(UiContext c, Rect r)
    {
        var t = Current.T;

        bool modern = c.Theme.Modern;

        // Seek bar.
        var seek = new Rect(r.X, r.Y, r.W, 14);
        float frac = t.Seconds <= 0 ? 0 : (float)(_position / t.Seconds);

        if (modern)
        {
            // A hairline with a square handle: nothing else.
            var line = new Rect(seek.X, seek.CenterY - 2, seek.W, 4);
            c.R.FillRect(line, Color.Rgb(0x3A3A3A));
            c.R.FillRect(new Rect(line.X, line.Y, MathF.Max(0, line.W * frac), line.H),
                         c.Theme.Accent);
            c.R.FillRect(new Rect(seek.X + (seek.W - 6) * frac, seek.Y, 6, seek.H), Color.White);
        }
        else
        {
            c.R.RoundedRect(seek, 4, Color.Rgb(0x11161C), Color.Rgb(0x3A424C), 1);
            c.R.RoundedRectV(new Rect(seek.X + 2, seek.Y + 2, MathF.Max(0, (seek.W - 4) * frac), seek.H - 4), 3,
                             Color.Rgb(0x7FD0F5), Color.Rgb(0x2E8AD0));

            var knob = new Rect(seek.X + (seek.W - 10) * frac, seek.Y - 2, 10, seek.H + 4);
            c.R.RoundedRect(knob, 3, Color.Rgb(0xE8EEF4), Color.Rgb(0x9AA6B4), 1);
        }

        if (c.Clicked(seek) || (_seeking && c.In.IsDown(MouseButton.Left)))
        {
            _seeking = true;
            float f = Math.Clamp((c.MouseX - seek.X) / seek.W, 0, 1);
            _position = f * t.Seconds;
            c.MouseHandled = true;
        }
        if (_seeking && !c.In.IsDown(MouseButton.Left)) _seeking = false;

        // Times.
        c.F.Small.Draw(c.R, TimeSpan.FromSeconds(_position).ToString(@"mm\:ss"), r.X, seek.Bottom + 4,
                       Color.Rgba(0xFFFFFF, 190));
        string total = TimeSpan.FromSeconds(t.Seconds).ToString(@"mm\:ss");
        c.F.Small.Draw(c.R, total, r.Right - c.F.Small.Measure(total), seek.Bottom + 4,
                       Color.Rgba(0xFFFFFF, 190));

        // Buttons.
        var row = new Rect(r.X, seek.Bottom + 20, r.W, 34);
        float bs = 30, gap = 6;
        float x = row.CenterX - (bs * 5 + gap * 4) * 0.5f;

        if (RoundButton(c, new Rect(x, row.Y, bs, bs), Glyph.Prev)) Step(c, -1);
        x += bs + gap;
        if (RoundButton(c, new Rect(x, row.Y, bs, bs), _playing ? Glyph.Pause : Glyph.Play, true)) TogglePlay(c);
        x += bs + gap;
        if (RoundButton(c, new Rect(x, row.Y, bs, bs), Glyph.Stop)) Stop(c);
        x += bs + gap;
        if (RoundButton(c, new Rect(x, row.Y, bs, bs), Glyph.Next)) Step(c, 1);
        x += bs + gap;
        if (RoundButton(c, new Rect(x, row.Y, bs, bs), _shuffle ? Glyph.ShuffleOn : Glyph.Shuffle)) _shuffle = !_shuffle;

        // Volume.
        var vol = new Rect(row.Right - 120, row.Y + 8, 110, 18);
        Icons.Draw(c.R, IconId.Volume, new Rect(vol.X - 22, vol.Y, 18, 18));
        if (W.Slider(c, Id + ".vol", vol, ref _volume, 0, 1))
        {
            Shell.Audio.SetMusicVolume(_volume);
            c.MouseHandled = true;
        }
    }

    enum Glyph { Play, Pause, Stop, Prev, Next, Shuffle, ShuffleOn }

    bool RoundButton(UiContext c, Rect r, Glyph g, bool primary = false)
    {
        bool hover = c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);

        if (c.Theme.Modern)
        {
            // An outlined circle, filled only when it is the play button or
            // when the pointer is on it.
            Color fill = primary ? c.Theme.Accent
                       : held ? Color.Rgb(0x3A3A3A)
                       : hover ? Color.Rgb(0x2E2E2E) : Color.Transparent;
            if (primary && held) fill = c.Theme.Accent.Shade(0.8f);
            else if (primary && hover) fill = c.Theme.Accent.Shade(1.15f);

            if (fill.A > 0) c.R.FillCircle(r.CenterX, r.CenterY, r.W * 0.5f, fill);
            c.R.DrawCircle(r.CenterX, r.CenterY, r.W * 0.5f,
                           primary ? Color.Transparent : Color.Rgba(0xFFFFFF, hover ? (byte)200 : (byte)120),
                           1.4f);
        }
        else
        {
            Color top = primary ? Color.Rgb(0x4FA8E8) : Color.Rgb(0x475262);
            Color bot = primary ? Color.Rgb(0x1E6AB0) : Color.Rgb(0x2A323C);
            if (held) (top, bot) = (bot, top);
            else if (hover) { top = top.Shade(1.25f); bot = bot.Shade(1.2f); }

            c.R.FillCircle(r.CenterX, r.CenterY, r.W * 0.5f, bot);
            c.R.FillCircle(r.CenterX, r.CenterY - 1, r.W * 0.46f, top);
        }

        Color ink = Color.White;
        float s = r.W * 0.22f;
        float cx = r.CenterX, cy = r.CenterY;

        switch (g)
        {
            case Glyph.Play: c.R.FillTriangle(cx - s * 0.7f, cy - s, cx - s * 0.7f, cy + s, cx + s, cy, ink); break;
            case Glyph.Pause:
                c.R.FillRect(new Rect(cx - s * 0.8f, cy - s, s * 0.6f, s * 2), ink);
                c.R.FillRect(new Rect(cx + s * 0.2f, cy - s, s * 0.6f, s * 2), ink);
                break;
            case Glyph.Stop: c.R.FillRect(new Rect(cx - s * 0.8f, cy - s * 0.8f, s * 1.6f, s * 1.6f), ink); break;
            case Glyph.Prev:
                c.R.FillRect(new Rect(cx - s, cy - s, s * 0.35f, s * 2), ink);
                c.R.FillTriangle(cx + s, cy - s, cx + s, cy + s, cx - s * 0.5f, cy, ink);
                break;
            case Glyph.Next:
                c.R.FillRect(new Rect(cx + s * 0.65f, cy - s, s * 0.35f, s * 2), ink);
                c.R.FillTriangle(cx - s, cy - s, cx - s, cy + s, cx + s * 0.5f, cy, ink);
                break;
            default:
                Color col = g == Glyph.ShuffleOn ? Color.Rgb(0x7FE0A0) : ink;
                c.R.Line(cx - s, cy - s * 0.6f, cx + s, cy + s * 0.6f, col, 1.8f);
                c.R.Line(cx - s, cy + s * 0.6f, cx + s, cy - s * 0.6f, col, 1.8f);
                break;
        }

        bool clicked = c.Clicked(r);
        if (clicked) c.SoundAt(Sfx.Click, r, 0.5f);
        return clicked;
    }

    // ---- side panel ------------------------------------------------------

    void DrawSidePanel(UiContext c, Rect r)
    {
        c.R.FillRect(r, c.Theme.Modern ? Color.Rgb(0x161616) : Color.Rgb(0x1B2028));
        c.R.DrawRect(r, c.Theme.Modern ? Color.Rgb(0x2E2E2E) : Color.Rgb(0x3A424C));

        var tabsRow = r.CutTop(24);
        string[] names = { L.T("player.playlist"), L.T("player.equalizer"), L.T("player.info") };
        float tw = tabsRow.W / names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            var tab = new Rect(tabsRow.X + i * tw, tabsRow.Y, tw, tabsRow.H);
            bool sel = i == _tab;
            c.R.FillRectV(tab, sel ? Color.Rgb(0x2E3A48) : Color.Rgb(0x151A20),
                          sel ? Color.Rgb(0x1F2831) : Color.Rgb(0x101419));
            c.F.Small.DrawCentered(c.R, names[i], tab, sel ? Color.White : Color.Rgba(0xFFFFFF, 130));
            if (sel) c.R.FillRect(new Rect(tab.X, tab.Y, tab.W, 2), Color.Rgb(0x7FD0F5));
            if (c.Clicked(tab)) { _tab = i; c.SoundAt(Sfx.Tick, tab, 0.35f); }
        }

        var body = r.Deflate(4);
        switch (_tab)
        {
            case 1: DrawEqualizer(c, body); break;
            case 2: DrawInfo(c, body); break;
            default: DrawPlaylist(c, body); break;
        }
    }

    void DrawPlaylist(UiContext c, Rect r)
    {
        float rowH = 30;
        for (int i = 0; i < Playlist.Length; i++)
        {
            var row = new Rect(r.X, r.Y + i * rowH, r.W, rowH - 2);
            bool cur = i == _current;
            bool hover = c.Hovering(row);

            if (cur) c.R.FillRectH(row, Color.Rgba(0x2E8AD0, 200), Color.Rgba(0x2E8AD0, 40));
            else if (hover) c.R.FillRect(row, Color.Rgba(0xFFFFFF, 20));

            var track = Playlist[i];
            Icons.Draw(c.R, track.IsVideo ? IconId.VideoFile : IconId.AudioFile,
                       new Rect(row.X + 4, row.CenterY - 8, 16, 16));

            string title = L.T(track.Key);
            c.F.Ui.Draw(c.R, c.F.Ui.Ellipsize(title, row.W - 66), row.X + 24, row.Y + 3,
                        cur ? Color.White : Color.Rgba(0xFFFFFF, 190));
            c.F.Small.Draw(c.R, TimeSpan.FromSeconds(track.Seconds).ToString(@"mm\:ss"),
                           row.Right - 38, row.Y + 5, Color.Rgba(0xFFFFFF, 130));

            if (cur && _playing)
            {
                // Little animated bars marking the playing item.
                for (int b = 0; b < 3; b++)
                {
                    float h = 3 + MathF.Abs(MathF.Sin((float)c.Time * 6 + b)) * 7;
                    c.R.FillRect(new Rect(row.Right - 14 + b * 4, row.Bottom - 6 - h, 2.5f, h),
                                 Color.Rgb(0x7FE0FF));
                }
            }

            if (c.DoubleClicked(row)) { _current = i; _position = 0; Play(c); }
            else if (c.Clicked(row)) { _current = i; c.SoundAt(Sfx.Tick, row, 0.3f); }
        }
    }

    void DrawEqualizer(UiContext c, Rect r)
    {
        string[] bands = { "60", "170", "310", "600", "1k", "3k", "6k", "12k", "14k", "16k" };
        float bw = r.W / _eq.Length;
        for (int i = 0; i < _eq.Length; i++)
        {
            var col = new Rect(r.X + i * bw, r.Y + 8, bw, r.H - 40);
            W.Slider(c, Id + ".eq" + i, col, ref _eq[i], 0, 1, vertical: true);
            c.F.Small.DrawCentered(c.R, bands[i], new Rect(col.X, col.Bottom, col.W, 14),
                                   Color.Rgba(0xFFFFFF, 150));
        }
        if (W.Button(c, Id + ".eqreset", new Rect(r.X + 4, r.Bottom - 26, r.W - 8, 22),
                     L.T("player.reset")))
        {
            for (int i = 0; i < _eq.Length; i++) _eq[i] = 0.5f;
        }
    }

    void DrawInfo(UiContext c, Rect r)
    {
        var t = Current.T;
        (string key, string val)[] rows =
        {
            ("player.title", Current.Name),
            ("player.type", t.IsVideo ? L.T("player.video") : L.T("player.audio")),
            ("player.duration", TimeSpan.FromSeconds(t.Seconds).ToString(@"mm\:ss")),
            ("player.source", L.T("player.synthesised_live")),
            ("player.codec", t.IsVideo ? "MIMINUS-GL" : "MIMINUS-AL"),
            ("player.bitrate", t.IsVideo ? "—" : "44100 Hz / 16 bit"),
        };

        float y = r.Y + 6;
        foreach (var (key, val) in rows)
        {
            c.F.Small.Draw(c.R, L.T(key), r.X + 4, y, Color.Rgba(0xFFFFFF, 130));
            y += c.F.Small.Height + 1;
            c.R.PushClip(new Rect(r.X + 4, y, r.W - 8, c.F.Ui.Height + 2));
            c.F.Ui.Draw(c.R, val, r.X + 4, y, Color.White);
            c.R.PopClip();
            y += c.F.Ui.Height + 8;
        }
    }

    // ---- playback --------------------------------------------------------

    void TogglePlay(UiContext c)
    {
        if (_playing) Pause(c); else Play(c);
    }

    void Play(UiContext c)
    {
        _playing = true;
        var t = Current.T;
        if (!t.IsVideo)
        {
            var (pcm, rate) = Chiptune.Build(t.Seed, t.Seconds);
            Shell.Audio.PlayMusic(pcm, rate);
            Shell.Audio.SetMusicVolume(_volume);
        }
        else Shell.Audio.StopMusic();
        c.Sound(Sfx.Click, 0.5f);
    }

    void Pause(UiContext c)
    {
        _playing = false;
        Shell.Audio.PauseMusic();
        c.Sound(Sfx.Click, 0.5f);
    }

    void Stop(UiContext c)
    {
        _playing = false;
        _position = 0;
        Shell.Audio.StopMusic();
        Array.Clear(_spectrum);
        c.Sound(Sfx.Click, 0.5f);
    }

    void Step(UiContext c, int delta)
    {
        if (_shuffle) _current = Random.Shared.Next(Playlist.Length);
        else _current = (_current + delta + Playlist.Length) % Playlist.Length;
        _position = 0;
        if (_playing) Play(c);
    }

    public override void OnClosed() => Shell.Audio.StopMusic();
}
