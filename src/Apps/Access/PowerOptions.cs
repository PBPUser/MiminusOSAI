using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Электропитание» — the Control Panel page, laid out the way seven
/// laid it out: the plans as a list of radio buttons with a line of explanation
/// each, and the two timers under them.
///
/// The plans are not decoration. Power in a machine like this one is the frame
/// rate and the backlight, so that is what a plan sets: the saver holds the loop
/// to thirty frames and dims the picture to seven tenths, high performance takes
/// the cap off altogether, and balanced is sixty frames at full brightness. The
/// timers are real too — the screen goes black after the first, and the machine
/// puts up the lock screen after the second.</summary>
public sealed class PowerOptionsWindow : OsWindow
{
    public override string Title => L.T("power.title");
    public override float MinWidth => 520;
    public override float MinHeight => 380;

    public PowerOptionsWindow()
    {
        Icon = IconId.Power;
        Bounds = new Rect(0, 0, 620, 470);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    /// <summary>The minutes the two combos offer. Nought is «никогда», which
    /// is the answer everybody picked.</summary>
    static readonly int[] Minutes = { 1, 2, 5, 10, 15, 20, 30, 45, 60, 0 };

    static string MinutesLabel(int m) => m == 0 ? L.T("power.never") : L.F("power.minutes", m);

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        var s = Shell.Settings;
        c.R.FillRect(client, Color.White);

        var area = client.Deflate(20, 16, 20, 14);

        c.F.Caption.Draw(c.R, L.T("power.heading"), area.X, area.Y, Color.Rgb(0x1E4E79));
        area.CutTop(c.F.Caption.Height + 4);
        Note(c, ref area, "power.heading_note");
        area.CutTop(14);

        // ---- the plans --------------------------------------------------------
        (string key, string noteKey)[] plans =
        {
            ("power.balanced", "power.balanced_note"),
            ("power.performance", "power.performance_note"),
            ("power.saver", "power.saver_note"),
        };

        for (int i = 0; i < plans.Length; i++)
        {
            var row = area.CutTop(46);
            bool picked = s.PowerPlan == i;

            if (picked) c.R.FillRect(row, Color.Rgb(0xEAF3FB));
            else if (c.Hovering(row)) c.R.FillRect(row, Color.Rgb(0xF3F8FC));

            if (W.Radio(c, Id + ".plan" + i, new Rect(row.X + 6, row.Y + 4, 300, 20),
                        L.T(plans[i].key), picked))
            {
                s.PowerPlan = i;
                c.SoundAt(Sfx.Click, row, 0.45f);
            }

            c.F.Small.Draw(c.R, L.T(plans[i].noteKey), row.X + 30, row.Y + 24, Color.Rgb(0x707070));

            // What the plan is doing, on the right, so it is never a mystery.
            string effect = i switch
            {
                1 => L.T("power.effect_performance"),
                2 => L.T("power.effect_saver"),
                _ => L.T("power.effect_balanced"),
            };
            c.F.Small.DrawRight(c.R, effect, new Rect(row.X, row.Y + 12, row.W - 10, 16),
                                picked ? c.Theme.Accent : Color.Rgb(0x909090));
        }

        area.CutTop(16);

        // ---- the timers -------------------------------------------------------
        c.F.UiBold.Draw(c.R, L.T("power.timers"), area.X, area.Y, Color.Rgb(0x1E4E79));
        c.R.FillRect(new Rect(area.X, area.Y + c.F.UiBold.Height + 3, area.W, 1), Color.Rgb(0xE0E0E0));
        area.CutTop(c.F.UiBold.Height + 14);

        var names = Minutes.Select(MinutesLabel).ToList();

        var line = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("power.display_off"), line.X, line.Y + 4, t.Text);
        int display = Math.Max(0, Array.IndexOf(Minutes, s.DisplayOffMinutes));
        if (W.ComboBox(c, Id + ".display", new Rect(line.X + 280, line.Y, 160, 24), names, ref display))
            s.DisplayOffMinutes = Minutes[display];

        line = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("power.sleep_after"), line.X, line.Y + 4, t.Text);
        int sleep = Math.Max(0, Array.IndexOf(Minutes, s.SleepMinutes));
        if (W.ComboBox(c, Id + ".sleep", new Rect(line.X + 280, line.Y, 160, 24), names, ref sleep))
            s.SleepMinutes = Minutes[sleep];

        area.CutTop(10);
        Note(c, ref area, "power.timers_note");
        area.CutTop(14);

        // ---- the button -------------------------------------------------------
        c.F.UiBold.Draw(c.R, L.T("power.button_heading"), area.X, area.Y, Color.Rgb(0x1E4E79));
        c.R.FillRect(new Rect(area.X, area.Y + c.F.UiBold.Height + 3, area.W, 1), Color.Rgb(0xE0E0E0));
        area.CutTop(c.F.UiBold.Height + 14);

        line = area.CutTop(30);
        c.F.Ui.Draw(c.R, L.T("power.button_action"), line.X, line.Y + 4, t.Text);

        var actions = new List<string>
        {
            L.T("charm.shutdown"), L.T("charm.sleep"), L.T("start.log_off"),
        };
        int action = Math.Clamp(s.PowerButtonAction, 0, 2);
        if (W.ComboBox(c, Id + ".action", new Rect(line.X + 280, line.Y, 160, 24), actions, ref action))
            s.PowerButtonAction = action;

        area.CutTop(16);

        var now = area.CutTop(30);
        if (W.Button(c, Id + ".sleepnow", new Rect(now.X, now.Y, 170, 26),
                     L.T("power.sleep_now"), true, IconId.Lock))
            Shell.LockScreenNow(c);

        if (W.Button(c, Id + ".off", new Rect(now.X + 180, now.Y, 170, 26),
                     L.T("charm.shutdown"), true, IconId.Shutdown))
            Shell.BeginShutdown(c);
    }

    static void Note(UiContext c, ref Rect area, string key)
    {
        foreach (string line in c.F.Small.Wrap(L.T(key), MathF.Min(area.W, 540)))
        {
            if (area.H < c.F.Small.Height) return;
            var row = area.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X, row.Y, Color.Rgb(0x707070));
        }
    }
}
