using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Часы и таймер» — the one program in this system that does not ship
/// with it.
///
/// It is built into <c>store/</c> rather than <c>apps/</c>, so a fresh
/// installation does not have it: the Start menu does not list it and nothing
/// can launch it. Installing it from «Магазин Миминус» copies the assembly into
/// <c>apps/</c> and the system picks it up on the spot — no restart, because the
/// program registry rescans and every menu is built from that registry every
/// time it is opened. Uninstalling puts it back and it disappears again.
///
/// That is what makes the store's «Установить» button real rather than a
/// picture of one, and it is why this program exists at all.
///
/// What it does is what a clock program does: a face with three hands that
/// really move, a stopwatch that really counts, and a countdown that really
/// goes off.</summary>
public sealed class ClockWindow : OsWindow
{
    public override string Title => L.T("clockapp.title");
    public override float MinWidth => 420;
    public override float MinHeight => 320;

    public ClockWindow()
    {
        Icon = IconId.Clock;
        Bounds = new Rect(0, 0, 520, 400);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    enum Page { Face, Stopwatch, Timer }
    Page _page = Page.Face;

    // ---- the stopwatch -----------------------------------------------------

    bool _running;
    double _elapsed;            // seconds already banked
    double _startedAt = -1;     // when the current run began
    readonly List<double> _laps = new();

    double Stopwatch(UiContext c)
        => _elapsed + (_running && _startedAt >= 0 ? c.Time - _startedAt : 0);

    // ---- the countdown -----------------------------------------------------

    int _setMinutes = 5;
    double _timerLeft;
    bool _timerRunning;
    double _timerAt = -1;
    bool _rang;

    public override void Tick(UiContext c, float dt)
    {
        if (!_timerRunning) return;

        _timerLeft = MathF.Max(0, (float)(_timerAt - c.Time));
        if (_timerLeft > 0) return;

        _timerRunning = false;
        if (_rang) return;

        _rang = true;
        c.Sound(Sfx.Balloon, 1f);
        Shell.MessageBox(c, L.T("clockapp.title"), L.T("clockapp.timer_done"),
                         MsgButtons.Ok, IconId.Clock, null, Sfx.Info);
    }

    // ---- painting ----------------------------------------------------------

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var tabs = client.CutTop(30);
        DrawTabs(c, tabs);

        var body = client.Deflate(16, 14, 16, 14);
        switch (_page)
        {
            case Page.Stopwatch: DrawStopwatch(c, body); break;
            case Page.Timer: DrawTimer(c, body); break;
            default: DrawFace(c, body); break;
        }
    }

    void DrawTabs(UiContext c, Rect bar)
    {
        var t = c.Theme;
        c.R.FillRect(bar, t.FaceDark);
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), t.ControlBorder);

        string[] keys = { "clockapp.tab_clock", "clockapp.tab_stopwatch", "clockapp.tab_timer" };
        float x = bar.X + 6;

        for (int i = 0; i < keys.Length; i++)
        {
            string label = L.T(keys[i]);
            var r = new Rect(x, bar.Y + 3, c.F.Ui.Measure(label) + 26, bar.H - 3);
            bool sel = (int)_page == i;

            if (sel)
            {
                c.R.FillRect(r, t.Face);
                c.R.FillRect(new Rect(r.X, r.Y, r.W, 2), t.Accent);
                c.R.FillRect(new Rect(r.X, r.Y, 1, r.H), t.ControlBorder);
                c.R.FillRect(new Rect(r.Right - 1, r.Y, 1, r.H), t.ControlBorder);
            }
            else if (c.Hovering(r)) c.R.FillRect(r, t.Hot);

            c.F.Ui.DrawCentered(c.R, label, r, sel ? t.Text : t.TextDisabled);
            if (c.Clicked(r)) { _page = (Page)i; c.SoundAt(Sfx.Tick, r, 0.3f); }

            x = r.Right + 2;
        }
    }

    /// <summary>The face: a dial, twelve marks, three hands and the date under
    /// it. The hands come off the system clock, so it is the machine's own
    /// time, second for second.</summary>
    void DrawFace(UiContext c, Rect body)
    {
        var now = Shell.Now;
        var t = c.Theme;

        float radius = MathF.Min(body.W, body.H - 40) * 0.5f - 6;
        float cx = body.CenterX, cy = body.Y + radius + 6;

        c.R.FillCircle(cx, cy, radius, Color.White);
        c.R.DrawCircle(cx, cy, radius, t.ControlBorder, 2);

        for (int i = 0; i < 60; i++)
        {
            float a = i * MathF.PI / 30 - MathF.PI * 0.5f;
            bool hour = i % 5 == 0;
            float inner = radius - (hour ? 12 : 5);
            c.R.Line(cx + MathF.Cos(a) * inner, cy + MathF.Sin(a) * inner,
                     cx + MathF.Cos(a) * (radius - 3), cy + MathF.Sin(a) * (radius - 3),
                     hour ? Color.Rgb(0x303030) : Color.Rgb(0xB0B0B0), hour ? 2.2f : 1);
        }

        double seconds = now.Second + now.Millisecond / 1000.0;
        double minutes = now.Minute + seconds / 60.0;
        double hours = now.Hour % 12 + minutes / 60.0;

        Hand(c, cx, cy, hours / 12.0, radius * 0.52f, 4.5f, Color.Rgb(0x202020));
        Hand(c, cx, cy, minutes / 60.0, radius * 0.76f, 3f, Color.Rgb(0x202020));
        Hand(c, cx, cy, seconds / 60.0, radius * 0.84f, 1.4f, Color.Rgb(0xC03028));

        c.R.FillCircle(cx, cy, 4, Color.Rgb(0x202020));

        string digital = L.Time(now);
        var line = new Rect(body.X, body.Bottom - c.F.Big.Height - c.F.Small.Height - 6,
                            body.W, c.F.Big.Height);
        c.F.Big.DrawCentered(c.R, digital, line, t.Text);
        c.F.Small.DrawCentered(c.R, L.LongDate(now),
            new Rect(body.X, line.Bottom + 2, body.W, c.F.Small.Height), t.TextDisabled);
    }

    static void Hand(UiContext c, float cx, float cy, double turn, float len, float w, Color col)
    {
        float a = (float)(turn * MathF.PI * 2) - MathF.PI * 0.5f;
        c.R.Line(cx - MathF.Cos(a) * len * 0.18f, cy - MathF.Sin(a) * len * 0.18f,
                 cx + MathF.Cos(a) * len, cy + MathF.Sin(a) * len, col, w);
    }

    void DrawStopwatch(UiContext c, Rect body)
    {
        var t = c.Theme;
        double value = Stopwatch(c);

        c.F.Huge.DrawCentered(c.R, Format(value),
            new Rect(body.X, body.Y + 6, body.W, c.F.Huge.Height), t.Text);

        var row = new Rect(body.X, body.Y + c.F.Huge.Height + 16, body.W, 28);
        float bw = 110;
        float bx = body.CenterX - (bw * 3 + 16) * 0.5f;

        if (W.Button(c, Id + ".sw.run", new Rect(bx, row.Y, bw, 26),
                     L.T(_running ? "clockapp.stop" : "clockapp.start"), true, IconId.None, true))
        {
            if (_running) { _elapsed = value; _running = false; _startedAt = -1; }
            else { _running = true; _startedAt = c.Time; }
            c.Sound(Sfx.Click, 0.5f);
        }

        if (W.Button(c, Id + ".sw.lap", new Rect(bx + bw + 8, row.Y, bw, 26),
                     L.T("clockapp.lap"), _running))
        {
            _laps.Insert(0, value);
            c.Sound(Sfx.Tick, 0.5f);
        }

        if (W.Button(c, Id + ".sw.reset", new Rect(bx + (bw + 8) * 2, row.Y, bw, 26),
                     L.T("clockapp.reset"), value > 0 || _laps.Count > 0))
        {
            _running = false;
            _startedAt = -1;
            _elapsed = 0;
            _laps.Clear();
            c.Sound(Sfx.Click, 0.5f);
        }

        // The laps, newest first, which is the order they are read in.
        var list = new Rect(body.X, row.Bottom + 12, body.W, body.Bottom - row.Bottom - 12);
        if (list.H < 20) return;

        c.R.FillRect(list, Color.White);
        c.R.DrawRect(list, t.FieldBorder);

        c.R.PushClip(list.Deflate(1));
        float y = list.Y + 4;
        for (int i = 0; i < _laps.Count && y < list.Bottom; i++)
        {
            c.F.Ui.Draw(c.R, L.F("clockapp.lap_n", _laps.Count - i), list.X + 8, y, t.TextDisabled);
            c.F.Ui.DrawRight(c.R, Format(_laps[i]),
                             new Rect(list.X, y, list.W - 10, c.F.Ui.Height), t.Text);
            y += c.F.Ui.Height + 4;
        }
        c.R.PopClip();
    }

    void DrawTimer(UiContext c, Rect body)
    {
        var t = c.Theme;
        double left = _timerRunning ? _timerLeft : _setMinutes * 60;

        c.F.Huge.DrawCentered(c.R, Format(left),
            new Rect(body.X, body.Y + 6, body.W, c.F.Huge.Height), _timerRunning ? t.Accent : t.Text);

        var chips = new Rect(body.X, body.Y + c.F.Huge.Height + 16, body.W, 28);
        int[] minutes = { 1, 3, 5, 10, 15, 30 };
        float cw = 62;
        float cxs = body.CenterX - (cw * minutes.Length + 5 * (minutes.Length - 1)) * 0.5f;

        for (int i = 0; i < minutes.Length; i++)
        {
            var r = new Rect(cxs + i * (cw + 5), chips.Y, cw, 26);
            bool picked = !_timerRunning && _setMinutes == minutes[i];
            bool hot = c.Hovering(r);

            c.R.FillRect(r, picked ? t.Accent : hot ? t.Hot : t.FaceDark);
            c.R.DrawRect(r, picked ? t.Accent : t.ControlBorder);
            c.F.Ui.DrawCentered(c.R, L.F("clockapp.minutes", minutes[i]), r,
                                picked ? Color.White : t.Text);

            if (!_timerRunning && c.Clicked(r))
            {
                _setMinutes = minutes[i];
                c.SoundAt(Sfx.Click, r, 0.4f);
            }
        }

        var row = new Rect(body.X, chips.Bottom + 16, body.W, 28);
        float bw = 140;

        if (W.Button(c, Id + ".t.run", new Rect(body.CenterX - bw - 6, row.Y, bw, 26),
                     L.T(_timerRunning ? "clockapp.stop" : "clockapp.start"), true, IconId.Clock, true))
        {
            if (_timerRunning) _timerRunning = false;
            else
            {
                _timerAt = c.Time + _setMinutes * 60;
                _timerLeft = _setMinutes * 60;
                _timerRunning = true;
                _rang = false;
            }
            c.Sound(Sfx.Click, 0.5f);
        }

        if (W.Button(c, Id + ".t.reset", new Rect(body.CenterX + 6, row.Y, bw, 26),
                     L.T("clockapp.reset"), _timerRunning || _rang))
        {
            _timerRunning = false;
            _rang = false;
            _timerLeft = 0;
            c.Sound(Sfx.Click, 0.5f);
        }

        foreach (string line in c.F.Small.Wrap(L.T("clockapp.timer_note"), MathF.Min(body.W, 420)))
        {
            var r = new Rect(body.X, row.Bottom + 14, body.W, c.F.Small.Height);
            if (r.Bottom > body.Bottom) break;
            c.F.Small.DrawCentered(c.R, line, r, t.TextDisabled);
            row = new Rect(row.X, row.Y + c.F.Small.Height + 2, row.W, row.H);
        }
    }

    /// <summary>mm:ss.d, which is what a stopwatch reads, and hh:mm:ss once it
    /// has been running long enough to need the hours.</summary>
    static string Format(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes:D2}:{span.Seconds:D2}.{span.Milliseconds / 100}";
    }
}
