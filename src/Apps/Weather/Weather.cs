using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Погода» — the full-screen weather app version 8 came with.
///
/// It reports on Миминусоград, in the calendar «Всё в одном» invented: the year
/// there has бенабрь and нехабрь in it, and the forecast is arranged around them.
/// Nothing is fetched — the network has been checked — so the week is generated
/// from the date, which means it is the same week every time the same day comes
/// round, which is more than can be said for a real forecast.</summary>
public sealed class WeatherWindow : OsWindow
{
    public override string Title => L.T("weather.title");
    public override float MinWidth => 520;
    public override float MinHeight => 360;

    public WeatherWindow()
    {
        Icon = IconId.Weather;
        Bounds = new Rect(0, 0, 820, 520);
        Immersive = true;
    }

    /// <summary>What the sky is doing. Each one draws itself.</summary>
    enum Sky { Clear, Cloud, Rain, Snow, Hail }

    /// <summary>One day: which sky, and how warm. Derived from the date, so the
    /// week is stable while the window is open and honest about being made up.</summary>
    readonly record struct Day(string NameKey, Sky Sky, int High, int Low);

    static readonly string[] DayKeys =
    {
        "weather.day_1", "weather.day_2", "weather.day_3",
        "weather.day_4", "weather.day_5", "weather.day_6",
    };

    Day[] Week()
    {
        var days = new Day[DayKeys.Length];
        int seed = 606;

        for (int i = 0; i < days.Length; i++)
        {
            seed = seed * 1103515245 + 12345;
            int r = (seed >> 16) & 0x7FFF;

            var sky = (Sky)(r % 5);
            int high = 12 + r % 14;
            days[i] = new Day(DayKeys[i], sky, high, high - 6 - r % 4);
        }
        return days;
    }

    int _selected;

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        var week = Week();
        var today = week[Math.Clamp(_selected, 0, week.Length - 1)];

        // The sky is the background: the whole window is the weather.
        DrawSkyBackground(c, client, today.Sky);

        var area = client.Deflate(48, 36, 48, 24);
        var strip = area.CutBottom(140);

        // ---- today ----------------------------------------------------------
        c.F.Big.Draw(c.R, L.T("weather.place"), area.X, area.Y - 6, Color.White);
        c.F.Ui.Draw(c.R, L.T("weather.date"), area.X + 2, area.Y + c.F.Big.Height - 4,
                    Color.Rgba(0xFFFFFF, 200));

        var glyph = new Rect(area.X, area.Y + 70, 92, 92);
        DrawSky(c, glyph, today.Sky);

        c.F.Huge.Draw(c.R, today.High + "°", glyph.Right + 16, glyph.Y - c.F.Huge.Height * 0.28f,
                      Color.White);

        float ty = glyph.Y + 8;
        c.F.Caption.Draw(c.R, L.T(SkyKey(today.Sky)), glyph.Right + 260, ty, Color.White);
        ty += c.F.Caption.Height + 6;
        c.F.Ui.Draw(c.R, L.F("weather.high_low", today.High, today.Low), glyph.Right + 260, ty,
                    Color.Rgba(0xFFFFFF, 210));
        ty += c.F.Ui.Height + 4;
        c.F.Ui.Draw(c.R, L.T("weather.wind"), glyph.Right + 260, ty, Color.Rgba(0xFFFFFF, 210));
        ty += c.F.Ui.Height + 4;
        c.F.Ui.Draw(c.R, L.T("weather.checked"), glyph.Right + 260, ty, Color.Rgba(0xFFFFFF, 160));

        // ---- the week --------------------------------------------------------
        float cw = strip.W / week.Length;
        for (int i = 0; i < week.Length; i++)
        {
            var cell = new Rect(strip.X + i * cw, strip.Y, cw - 8, strip.H - 10);
            bool sel = i == _selected;
            bool hot = c.Hovering(cell);

            c.R.FillRect(cell, Color.Rgba(0x000000, sel ? (byte)90 : hot ? (byte)60 : (byte)40));

            c.F.Ui.DrawCentered(c.R, L.T(week[i].NameKey),
                                new Rect(cell.X, cell.Y + 8, cell.W, c.F.Ui.Height), Color.White);
            DrawSky(c, new Rect(cell.CenterX - 22, cell.Y + 32, 44, 44), week[i].Sky);
            c.F.Caption.DrawCentered(c.R, week[i].High + "° / " + week[i].Low + "°",
                                     new Rect(cell.X, cell.Bottom - 26, cell.W, c.F.Caption.Height),
                                     Color.Rgba(0xFFFFFF, 220));

            if (c.Clicked(cell)) { _selected = i; c.SoundAt(Sfx.Navigate, cell, 0.45f); }
        }
    }

    static string SkyKey(Sky s) => s switch
    {
        Sky.Clear => "allinone.weather_clear",
        Sky.Rain => "allinone.weather_rain",
        Sky.Snow => "allinone.weather_snow",
        Sky.Hail => "allinone.weather_hail",
        _ => "weather.cloudy",
    };

    /// <summary>The field behind everything, coloured by what the sky is doing.</summary>
    static void DrawSkyBackground(UiContext c, Rect r, Sky sky)
    {
        (Color top, Color bottom) = sky switch
        {
            Sky.Clear => (Color.Rgb(0x1E7FD0), Color.Rgb(0x7FC4F0)),
            Sky.Rain => (Color.Rgb(0x30414E), Color.Rgb(0x5A7182)),
            Sky.Snow => (Color.Rgb(0x5A6A80), Color.Rgb(0xA8B8C8)),
            Sky.Hail => (Color.Rgb(0x2A3440), Color.Rgb(0x4A5866)),
            _ => (Color.Rgb(0x40699A), Color.Rgb(0x88A8C4)),
        };
        c.R.FillRectV(r, top, bottom);
    }

    /// <summary>One weather glyph, drawn from circles and lines at any size.</summary>
    static void DrawSky(UiContext c, Rect r, Sky sky)
    {
        float u = MathF.Min(r.W, r.H) / 32f;
        float x = r.CenterX, y = r.CenterY;

        if (sky == Sky.Clear)
        {
            c.R.FillCircle(x, y, 9 * u, Color.Rgb(0xFFD24A));
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI / 4;
                c.R.Line(x + MathF.Cos(a) * 11 * u, y + MathF.Sin(a) * 11 * u,
                         x + MathF.Cos(a) * 14 * u, y + MathF.Sin(a) * 14 * u,
                         Color.Rgb(0xFFD24A), 2 * u);
            }
            return;
        }

        // Everything else has a cloud in it.
        c.R.FillCircle(x - 6 * u, y - 1 * u, 7 * u, Color.White);
        c.R.FillCircle(x + 4 * u, y - 3 * u, 9 * u, Color.White);
        c.R.FillRect(new Rect(x - 6 * u, y - 2 * u, 12 * u, 7 * u), Color.White);

        switch (sky)
        {
            case Sky.Rain:
                for (int i = 0; i < 4; i++)
                    c.R.Line(x - 7 * u + i * 5 * u, y + 7 * u,
                             x - 9 * u + i * 5 * u, y + 13 * u, Color.Rgb(0x9FD4FF), 1.8f * u);
                break;

            case Sky.Snow:
                for (int i = 0; i < 4; i++)
                    c.R.FillCircle(x - 7 * u + i * 5 * u, y + 10 * u, 1.6f * u, Color.White);
                break;

            case Sky.Hail:
                for (int i = 0; i < 4; i++)
                {
                    c.R.FillRect(new Rect(x - 8 * u + i * 5 * u, y + 8 * u, 3 * u, 3 * u),
                                 Color.Rgb(0xD8ECFF));
                }
                break;
        }
    }
}
