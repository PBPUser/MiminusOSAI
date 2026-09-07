using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Всё в одном» — the universal utility from part 3.
///
/// In the video the narrator opens the calculator's unit-conversion page and
/// reads its drop-downs as a single tool that "is suitable for everything":
/// it shows months, degrees Celsius, the weather, and how to address people.
/// Two of the months he lists — бенабрь and нехабрь — do not exist, so they are
/// here too.</summary>
public sealed class AllInOneWindow : OsWindow
{
    int _month, _unit, _weather, _address;
    float _temperature = 21;
    string _spoken;            // last thing said, shown under the button
    bool _speechFailed;

    static readonly string[] MonthKeys =
    {
        "allinone.month_december", "allinone.month_october",
        "allinone.month_benaber", "allinone.month_nehaber",
    };

    static readonly string[] UnitKeys =
    {
        "allinone.unit_celsius", "allinone.unit_fahrenheit", "allinone.unit_kelvin",
    };

    static readonly string[] WeatherKeys =
    {
        "allinone.weather_hail", "allinone.weather_clear",
        "allinone.weather_rain", "allinone.weather_snow",
    };

    static readonly string[] AddressKeys =
    {
        "allinone.address_mister", "allinone.address_miss",
        "allinone.address_missis", "allinone.address_comrade",
    };

    public override string Title => L.T("allinone.title");
    public override float MinWidth => 440;
    public override float MinHeight => 560;

    public AllInOneWindow()
    {
        Icon = IconId.Settings;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 460, 604);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(14);

        var head = area.CutTop(38);
        Icons.Draw(c.R, IconId.Settings, new Rect(head.X, head.Y, 28, 28));
        c.F.UiBold.Draw(c.R, L.T("allinone.title"), head.X + 36, head.Y + 2, c.Theme.Text);
        c.F.Small.Draw(c.R, L.T("allinone.subtitle"), head.X + 36, head.Y + 4 + c.F.UiBold.Height,
                       c.Theme.TextDisabled);

        float labelW = 132;

        // ---- months ------------------------------------------------------
        W.GroupBox(c, new Rect(area.X, area.Y, area.W, 66), L.T("allinone.months"));
        var row = new Rect(area.X + 14, area.Y + 26, area.W - 28, 22);
        c.F.Ui.Draw(c.R, L.T("allinone.months"), row.X, row.Y + 4, c.Theme.Text);
        W.ComboBox(c, Id + ".month", new Rect(row.X + labelW, row.Y, row.W - labelW, 22),
                   MonthKeys.Select(L.T).ToList(), ref _month);
        c.F.Small.Draw(c.R, L.T("allinone.invented_months_note"), row.X, row.Y + 26,
                       c.Theme.TextDisabled);
        area.CutTop(76);

        // ---- temperature -------------------------------------------------
        W.GroupBox(c, new Rect(area.X, area.Y, area.W, 76), L.T("allinone.temperature"));
        row = new Rect(area.X + 14, area.Y + 26, area.W - 28, 22);
        c.F.Ui.Draw(c.R, L.T("allinone.temperature"), row.X, row.Y + 4, c.Theme.Text);
        W.ComboBox(c, Id + ".unit", new Rect(row.X + labelW, row.Y, row.W - labelW, 22),
                   UnitKeys.Select(L.T).ToList(), ref _unit);

        var slider = new Rect(row.X, row.Y + 28, row.W - 70, 20);
        W.Slider(c, Id + ".temp", slider, ref _temperature, -40, 60);
        c.F.UiBold.Draw(c.R, Converted(), slider.Right + 8, slider.Y + 2, c.Theme.Text);
        area.CutTop(86);

        // ---- weather -----------------------------------------------------
        W.GroupBox(c, new Rect(area.X, area.Y, area.W, 52), L.T("allinone.weather"));
        row = new Rect(area.X + 14, area.Y + 24, area.W - 28, 22);
        c.F.Ui.Draw(c.R, L.T("allinone.weather"), row.X, row.Y + 4, c.Theme.Text);
        W.ComboBox(c, Id + ".weather", new Rect(row.X + labelW, row.Y, row.W - labelW, 22),
                   WeatherKeys.Select(L.T).ToList(), ref _weather);
        area.CutTop(62);

        // ---- forms of address --------------------------------------------
        W.GroupBox(c, new Rect(area.X, area.Y, area.W, 66), L.T("allinone.address"));
        row = new Rect(area.X + 14, area.Y + 24, area.W - 28, 22);
        c.F.Ui.Draw(c.R, L.T("allinone.address_short"), row.X, row.Y + 4, c.Theme.Text);
        W.ComboBox(c, Id + ".address", new Rect(row.X + labelW, row.Y, row.W - labelW, 22),
                   AddressKeys.Select(L.T).ToList(), ref _address);
        c.F.Small.Draw(c.R, L.T("allinone.address_note"), row.X, row.Y + 26, c.Theme.TextDisabled);
        area.CutTop(76);

        DrawPronunciation(c, area);
    }

    // ---- «Произношение» ---------------------------------------------------

    /// <summary>The author of the antivirus, as the utility insists on writing
    /// him: stressed syllables in capitals, because a system with no font files
    /// has no combining accent to put over a vowel either.</summary>
    const string Name = "Михаил Гревцов";
    const string Stressed = "МихаИл ГревцОв";
    const string Syllables = "Ми-ха-Ил  Грев-цОв";
    const string Latin = "Mikhail Grevtsov";
    const string Heard = "[михаИл грефцОф]";

    /// <summary>Nominative through prepositional, the six cases a Soviet
    /// reference book would print.</summary>
    static readonly (string Case, string Form)[] Declension =
    {
        ("allinone.case_nominative",    "Михаил Гревцов"),
        ("allinone.case_genitive",      "Михаила Гревцова"),
        ("allinone.case_dative",        "Михаилу Гревцову"),
        ("allinone.case_accusative",    "Михаила Гревцова"),
        ("allinone.case_instrumental",  "Михаилом Гревцовым"),
        ("allinone.case_prepositional", "о Михаиле Гревцове"),
    };

    void DrawPronunciation(UiContext c, Rect area)
    {
        var t = c.Theme;
        var box = new Rect(area.X, area.Y, area.W, 194);
        W.GroupBox(c, box, L.T("allinone.pronunciation"));

        var inner = box.Deflate(14);
        inner.CutTop(10);

        // The name itself, with the button that says it out loud beside it.
        var head = inner.CutTop(24);
        var speak = new Rect(head.Right - 108, head.Y, 108, 22);
        c.F.UiBold.Draw(c.R, Stressed, head.X, head.Y + 3, t.Text);
        if (W.Button(c, Id + ".speak", speak, L.T("allinone.speak")))
            Speak(c);

        Row(c, ref inner, L.T("allinone.by_syllables"), Syllables);
        Row(c, ref inner, L.T("allinone.as_heard"), Heard);
        Row(c, ref inner, L.T("allinone.in_latin"), Latin);

        inner.CutTop(4);
        c.F.Small.Draw(c.R, L.T("allinone.declension"), inner.X, inner.Y, t.TextDisabled);
        inner.CutTop(c.F.Small.Height + 4);

        // Six cases in two columns, which is what fits without another scroll.
        float half = inner.W / 2;
        for (int i = 0; i < Declension.Length; i++)
        {
            var (key, form) = Declension[i];
            float x = inner.X + (i < 3 ? 0 : half);
            float y = inner.Y + (i % 3) * (c.F.Small.Height + 3);
            c.F.Small.Draw(c.R, L.T(key), x, y, t.TextDisabled);
            c.F.Small.Draw(c.R, form, x + 26, y, t.Text);
        }
        inner.CutTop(3 * (c.F.Small.Height + 3) + 4);

        if (_spoken != null)
            c.F.Small.Draw(c.R, _speechFailed ? L.T("allinone.speech_unavailable")
                                              : L.F("allinone.spoken_by", _spoken),
                           inner.X, inner.Y, t.TextDisabled);
    }

    void Speak(UiContext c)
    {
        // The name is given to the engine unstressed: the capitals are a
        // reading aid for the eye, not something to pronounce.
        _speechFailed = !Speech.SayName(Name, Latin);
        _spoken = Speech.VoiceName ?? Name;

        // Without a speech engine the system falls back on the only voice it
        // has — its own beeps, one per syllable.
        if (_speechFailed)
            for (int i = 0; i < 5; i++)
                c.Sound(Sfx.Tick, 0.5f, 0.85f + i * 0.09f);
        else
            c.Sound(Sfx.Click, 0.4f);
    }

    void Row(UiContext c, ref Rect area, string label, string value)
    {
        var row = area.CutTop(19);
        c.F.Ui.Draw(c.R, label, row.X, row.Y, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, value, row.X + 104, row.Y, c.Theme.Text);
    }

    string Converted()
    {
        float v = _unit switch
        {
            1 => _temperature * 9f / 5f + 32f,
            2 => _temperature + 273.15f,
            _ => _temperature,
        };
        string suffix = _unit switch { 1 => "°F", 2 => "K", _ => "°C" };
        return $"{v:0.#} {suffix}";
    }
}

/// <summary>«Распознавание голоса» — the speech recognition demonstrated, and
/// abandoned, in part 3: the command is given, the system thinks about it, and
/// then admits it is not finished.</summary>
public sealed class VoiceRecognitionWindow : OsWindow
{
    enum Stage { Idle, Listening, Working, Failed }

    Stage _stage = Stage.Idle;
    double _stageStart;
    float _level;

    public override string Title => L.T("voice.title");
    public override float MinWidth => 360;
    public override float MinHeight => 260;

    public VoiceRecognitionWindow()
    {
        Icon = IconId.Volume;
        Resizable = false;
        Maximizable = false;
        Bounds = new Rect(0, 0, 400, 290);
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    public override void Tick(UiContext c, float dt)
    {
        // A fake input level that only moves while "listening".
        float target = _stage == Stage.Listening
            ? 0.25f + MathF.Abs(MathF.Sin((float)c.Time * 7f)) * 0.6f
            : 0.03f;
        _level += (target - _level) * MathF.Min(1, dt * 10);

        double age = c.Time - _stageStart;
        if (_stage == Stage.Listening && age > 2.2) SetStage(c, Stage.Working);
        else if (_stage == Stage.Working && age > 2.6) SetStage(c, Stage.Failed);
    }

    void SetStage(UiContext c, Stage s)
    {
        _stage = s;
        _stageStart = c.Time;
        if (s == Stage.Working) c.Sound(Sfx.ScanBeep, 0.6f);
        if (s == Stage.Failed) c.Sound(Sfx.Error, 0.7f);
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var area = client.Deflate(16);

        var head = area.CutTop(40);
        Icons.Draw(c.R, IconId.Volume, new Rect(head.X, head.Y, 28, 28));
        c.F.UiBold.Draw(c.R, L.T("voice.title"), head.X + 38, head.Y + 6, c.Theme.Text);

        // Input level meter.
        var meter = area.CutTop(30);
        var bar = new Rect(meter.X, meter.Y + 6, meter.W, 18);
        c.R.FillRect(bar, Color.Rgb(0x101418));
        c.R.DrawRect(bar, c.Theme.ControlBorder);
        int blocks = (int)(bar.W / 8);
        int lit = (int)(blocks * _level);
        for (int i = 0; i < blocks; i++)
        {
            Color col = i < lit
                ? (i > blocks * 0.8f ? Color.Rgb(0xE04040)
                   : i > blocks * 0.6f ? Color.Rgb(0xE0C040) : Color.Rgb(0x40D040))
                : Color.Rgb(0x1E2A22);
            c.R.FillRect(new Rect(bar.X + 2 + i * 8, bar.Y + 3, 6, bar.H - 6), col);
        }
        area.CutTop(8);

        // Transcript panel.
        var panel = area.CutTop(96);
        W.SunkenField(c, panel);
        var inner = panel.Deflate(8);

        switch (_stage)
        {
            case Stage.Idle:
                c.F.Ui.Draw(c.R, L.T("voice.prompt"), inner.X, inner.Y, c.Theme.TextDisabled);
                break;

            case Stage.Listening:
                c.F.Ui.Draw(c.R, L.T("voice.prompt"), inner.X, inner.Y, c.Theme.TextDisabled);
                c.F.UiBold.Draw(c.R, "« " + L.T("voice.command") + " »", inner.X,
                                inner.Y + c.F.Ui.Height + 8, c.Theme.Text);
                break;

            case Stage.Working:
                c.F.UiBold.Draw(c.R, "« " + L.T("voice.command") + " »", inner.X, inner.Y, c.Theme.Text);
                c.F.Ui.Draw(c.R, L.T("voice.working"), inner.X, inner.Y + c.F.UiBold.Height + 8,
                            c.Theme.Text);
                W.ProgressBar(c, new Rect(inner.X, inner.Bottom - 18, inner.W, 14),
                              (float)Math.Clamp((c.Time - _stageStart) / 2.6, 0, 1));
                break;

            default:
                c.F.UiBold.Draw(c.R, "« " + L.T("voice.command") + " »", inner.X, inner.Y, c.Theme.Text);
                float fy = inner.Y + c.F.UiBold.Height + 6;
                foreach (string line in c.F.Ui.Wrap(L.T("voice.failed"), inner.W))
                {
                    c.F.Ui.Draw(c.R, line, inner.X, fy, Color.Rgb(0xB03030));
                    fy += c.F.Ui.Height + 2;
                }
                break;
        }

        var buttons = area.CutBottom(30);
        float bw = 130;
        if (W.Button(c, Id + ".speak", new Rect(buttons.X, buttons.Y, bw, 26),
                     L.T("voice.speak"), _stage is Stage.Idle or Stage.Failed, IconId.Volume))
            SetStage(c, Stage.Listening);

        if (W.Button(c, Id + ".close", new Rect(buttons.Right - 90, buttons.Y, 90, 26), L.T("dlg.cancel")))
            Close();
    }
}
