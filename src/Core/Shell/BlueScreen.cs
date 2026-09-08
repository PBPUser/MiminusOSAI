using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>The stop screen.
///
/// When something in the system throws where nothing was expecting it, the OS
/// does what the systems it is imitating did: it stops, in white on blue, with
/// a made-up stop code and the real exception underneath it. The technical part
/// is genuine — the type, the message and the top of the stack — because a
/// parody of a crash screen is still the only report anyone gets.
///
/// Drawing it must never throw. It uses one font, one colour and no widgets.</summary>
public sealed class BlueScreen
{
    /// <summary>0x0000BEDA — «беда». Made up, in the manner of the real ones.</summary>
    public const string StopCode = "0x0000BEDA";

    static readonly Color Ground = Color.Rgb(0x0000AA);
    static readonly Color Ink = Color.Rgb(0xFFFFFF);

    readonly string _type;
    readonly string _message;
    readonly string[] _stack;
    readonly double _started;

    public BlueScreen(Exception ex, double time)
    {
        _type = ex?.GetType().FullName ?? "System.Exception";
        _message = ex?.Message ?? "";
        _started = time;

        // The top of the stack is what identifies the fault; the rest is the
        // shell calling itself and tells nobody anything.
        _stack = (ex?.StackTrace ?? "")
            .Split('\n')
            .Select(line => line.TrimEnd('\r').Trim())
            .Where(line => line.Length > 0)
            .Take(6)
            .ToArray();
    }

    /// <summary>True once the fake memory dump has run its course and a key
    /// press will restart the system.</summary>
    public bool DumpComplete(double time) => time - _started > 4.5;

    public void Draw(UiContext c, double time)
    {
        c.R.Clear(Ground);

        var font = c.F.Mono;
        float lineH = font.Height + 2;

        // 78 columns of text, centred, which is what the original looked like.
        float w = font.Measure(new string('M', 78));
        float x = MathF.Max(20, (c.ScreenW - w) * 0.5f);
        float y = MathF.Max(24, c.ScreenH * 0.16f);

        void Line(string text = "")
        {
            if (text != null) font.Draw(c.R, text, x, y, Ink);
            y += lineH;
        }

        Line(L.T("bsod.title"));
        Line();

        foreach (string line in Wrap(c, L.T("bsod.problem_detected"), w)) Line(line);
        Line();

        Line(L.T("bsod.stop_name"));
        Line();

        foreach (string line in Wrap(c, L.T("bsod.first_time"), w)) Line(line);
        Line();

        Line(L.T("bsod.technical_information"));
        Line();

        // The invented stop code, with the exception's own hash where a real
        // one would put the fault address.
        int address = _message.GetHashCode();
        Line($"*** STOP: {StopCode} ({address:X8}, 0x00000000, 0x00000000, 0x00000000)");
        Line();

        Line("*** " + _type);
        foreach (string line in Wrap(c, _message, w)) Line("    " + line);
        Line();

        foreach (string frame in _stack) Line("    " + Clip(c, frame, w));
        Line();

        // The dump that always ran whether or not anyone wanted it.
        double age = time - _started;
        int percent = (int)Math.Clamp(age / 4.0 * 100, 0, 100);
        Line(percent >= 100
            ? L.T("bsod.dump_complete")
            : L.F("bsod.dumping_memory", percent));

        if (DumpComplete(time))
        {
            Line();
            Line(L.T("bsod.press_any_key"));
        }
    }

    /// <summary>Wraps to the screen's column count. The font is fixed width, so
    /// this is arithmetic rather than measurement.</summary>
    static List<string> Wrap(UiContext c, string text, float width)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        float space = c.F.Mono.Measure(" ");
        int columns = Math.Max(20, (int)(width / MathF.Max(1, space)));

        foreach (string paragraph in text.Split('\n'))
        {
            string current = "";
            foreach (string word in paragraph.Split(' '))
            {
                if (current.Length == 0) { current = word; continue; }
                if (current.Length + 1 + word.Length <= columns) { current += " " + word; continue; }
                lines.Add(current);
                current = word;
            }
            lines.Add(current);
        }
        return lines;
    }

    static string Clip(UiContext c, string text, float width)
    {
        float space = c.F.Mono.Measure(" ");
        int columns = Math.Max(20, (int)(width / MathF.Max(1, space))) - 4;
        return text.Length <= columns ? text : text[..columns];
    }

    /// <summary>True when the viewer has asked for the restart.</summary>
    public static bool RestartRequested(UiContext c)
        => c.In.TypedChars.Count > 0
           || c.In.KeyPressed(Keys.Enter)
           || c.In.KeyPressed(Keys.Space)
           || c.In.KeyPressed(Keys.Escape)
           || c.In.Pressed(MouseButton.Left);
}
