using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>«Вас приветствует МИМИНУС ОС» — what happens the first time the
/// system is ever run here.
///
/// The out-of-box experience of the period: a full-screen blue field, one
/// question at a time, a bar of steps down the left, and no way to reach the
/// desktop until it is finished. It runs once — the first start writes the
/// version record, and a machine with a record has been set up already.
///
/// The questions are the ones the system actually needs answered: which
/// language, what to call the user, and which of its own themes to wear. The
/// last page thanks the user for registering, which nobody is asked to do.</summary>
public sealed class Setup
{
    readonly ShellHost _shell;

    public Setup(ShellHost shell) => _shell = shell;

    /// <summary>The pages, in order. Each one is a heading, a question and a
    /// set of choices; the last has nothing to choose.</summary>
    enum Page { Welcome, Language, Name, Look, Ready }

    Page _page = Page.Welcome;
    double _entered;

    /// <summary>The name being typed, which becomes the account name.</summary>
    public string UserName = "Admin";

    static readonly ThemeId[] Looks =
    {
        ThemeId.LunaBlue, ThemeId.LunaOlive, ThemeId.LunaSilver, ThemeId.Seven, ThemeId.Classic,
    };

    static readonly string[] LookKeys =
    {
        "setup.look_blue", "setup.look_olive", "setup.look_silver",
        "setup.look_seven", "setup.look_classic",
    };

    int _look;

    public void Begin(UiContext c) => _entered = c.Time;

    /// <summary>Draws the current page and moves between them. Returns true
    /// when setup is finished and the system should carry on booting.</summary>
    public bool Draw(UiContext c)
    {
        DrawBackdrop(c);

        var panel = new Rect(c.ScreenW * 0.30f, c.ScreenH * 0.20f,
                             c.ScreenW * 0.62f, c.ScreenH * 0.62f);

        DrawSteps(c, new Rect(c.ScreenW * 0.06f, panel.Y, c.ScreenW * 0.20f, panel.H));

        var body = panel;
        var head = body.CutTop(70);

        c.F.Big.Draw(c.R, L.T(HeadingKey), head.X, head.Y, Color.White);
        c.F.Ui.Draw(c.R, L.T(SubheadingKey), head.X + 2, head.Y + c.F.Big.Height + 6,
                    Color.Rgba(0xFFFFFF, 200));

        var buttons = body.CutBottom(48);
        body.CutTop(10);

        switch (_page)
        {
            case Page.Welcome: DrawWelcomePage(c, body); break;
            case Page.Language: DrawLanguagePage(c, body); break;
            case Page.Name: DrawNamePage(c, body); break;
            case Page.Look: DrawLookPage(c, body); break;
            default: DrawReadyPage(c, body); break;
        }

        return DrawButtons(c, buttons);
    }

    string HeadingKey => _page switch
    {
        Page.Welcome => "setup.welcome_heading",
        Page.Language => "setup.language_heading",
        Page.Name => "setup.name_heading",
        Page.Look => "setup.look_heading",
        _ => "setup.ready_heading",
    };

    string SubheadingKey => _page switch
    {
        Page.Welcome => "setup.welcome_sub",
        Page.Language => "setup.language_sub",
        Page.Name => "setup.name_sub",
        Page.Look => "setup.look_sub",
        _ => "setup.ready_sub",
    };

    // ---- the field it all sits on ----------------------------------------

    void DrawBackdrop(UiContext c)
    {
        var top = new Rect(0, 0, c.ScreenW, c.ScreenH * 0.5f);
        var bottom = new Rect(0, top.Bottom, c.ScreenW, c.ScreenH - top.H);
        c.R.FillRectV(top, Color.Rgb(0x2E6FC4), Color.Rgb(0x14477E));
        c.R.FillRectV(bottom, Color.Rgb(0x14477E), Color.Rgb(0x07203F));

        // The brand, bottom right, where the installer of the period put it.
        string brand = L.T("shell.miminus_os");
        float bw = c.F.Big.Measure(brand);
        c.F.Big.Draw(c.R, brand, c.ScreenW - bw - 40,
                     c.ScreenH - c.F.Big.Height - 34, Color.Rgba(0xFFFFFF, 150));
        c.F.Small.Draw(c.R, L.T("dlg.copyright_popov"), c.ScreenW - bw - 38,
                       c.ScreenH - 30, Color.Rgba(0xFFFFFF, 110));
    }

    /// <summary>The list of steps down the left, with the one in progress lit.
    /// It is the only thing telling the user how much is left.</summary>
    void DrawSteps(UiContext c, Rect area)
    {
        string[] keys =
        {
            "setup.step_welcome", "setup.step_language",
            "setup.step_name", "setup.step_look", "setup.step_ready",
        };

        float y = area.Y + 74;
        for (int i = 0; i < keys.Length; i++)
        {
            bool done = i < (int)_page;
            bool current = i == (int)_page;

            var dot = new Rect(area.X, y + 4, 8, 8);
            c.R.FillRect(dot, current ? Color.White
                             : done ? Color.Rgba(0xFFFFFF, 150) : Color.Rgba(0xFFFFFF, 60));

            c.F.Ui.Draw(c.R, L.T(keys[i]), area.X + 18, y,
                        current ? Color.White
                        : done ? Color.Rgba(0xFFFFFF, 170) : Color.Rgba(0xFFFFFF, 90));
            y += c.F.Ui.Height + 14;
        }
    }

    // ---- the pages --------------------------------------------------------

    void DrawWelcomePage(UiContext c, Rect body)
    {
        Paragraph(c, ref body, L.T("setup.welcome_body"));
        body.CutTop(10);
        Paragraph(c, ref body, L.T("setup.welcome_note"));
    }

    void DrawLanguagePage(UiContext c, Rect body)
    {
        Paragraph(c, ref body, L.T("setup.language_body"));
        body.CutTop(14);

        Choice(c, ref body, "ru", "Русский", L.T("setup.language_ru_note"), L.IsRu,
               () => L.Current = Lang.Ru);
        Choice(c, ref body, "en", "English", L.T("setup.language_en_note"), !L.IsRu,
               () => L.Current = Lang.En);
    }

    void DrawNamePage(UiContext c, Rect body)
    {
        Paragraph(c, ref body, L.T("setup.name_body"));
        body.CutTop(16);

        var row = body.CutTop(26);
        c.F.Ui.Draw(c.R, L.T("setup.your_name"), row.X, row.Y + 4, Color.White);

        var field = new Rect(row.X + 150, row.Y, 260, 24);
        W.TextField(c, "setup.name", field, ref UserName);

        body.CutTop(14);
        Paragraph(c, ref body, L.T("setup.name_note"));
    }

    void DrawLookPage(UiContext c, Rect body)
    {
        Paragraph(c, ref body, L.T("setup.look_body"));
        body.CutTop(14);

        for (int i = 0; i < Looks.Length; i++)
        {
            int index = i;
            Choice(c, ref body, "look" + i, L.T(LookKeys[i]), null, _look == i, () =>
            {
                _look = index;
                _shell.SetTheme(Looks[index], c.F);
            });
        }
    }

    void DrawReadyPage(UiContext c, Rect body)
    {
        Paragraph(c, ref body, L.F("setup.ready_body", UserName));
        body.CutTop(10);
        Paragraph(c, ref body, L.T("setup.ready_note"));
    }

    // ---- pieces -----------------------------------------------------------

    void Paragraph(UiContext c, ref Rect body, string text)
    {
        foreach (string line in c.F.Ui.Wrap(text, body.W - 40))
        {
            if (body.H < c.F.Ui.Height) return;
            var row = body.CutTop(c.F.Ui.Height + 5);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, Color.Rgba(0xFFFFFF, 225));
        }
    }

    /// <summary>One selectable line, drawn as the installer drew them: no box,
    /// just a mark, the label, and a wash when the pointer is over it.</summary>
    void Choice(UiContext c, ref Rect body, string id, string label, string note,
                bool selected, Action pick)
    {
        float height = note == null ? 30 : 44;
        if (body.H < height) return;

        var row = body.CutTop(height);
        var hit = new Rect(row.X, row.Y, MathF.Min(row.W, 460), height - 6);

        if (c.Hovering(hit)) c.R.FillRect(hit, Color.Rgba(0xFFFFFF, 30));
        if (selected) c.R.FillRect(hit, Color.Rgba(0xFFFFFF, 45));

        var mark = new Rect(hit.X + 8, hit.Y + 7, 12, 12);
        c.R.FillCircle(mark.CenterX, mark.CenterY, 6, Color.Rgba(0x000000, 60));
        c.R.FillCircle(mark.CenterX, mark.CenterY, 5, Color.White);
        if (selected) c.R.FillCircle(mark.CenterX, mark.CenterY, 2.5f, Color.Rgb(0x1B5FAF));

        c.F.UiBold.Draw(c.R, label, mark.Right + 10, hit.Y + 4, Color.White);
        if (note != null)
            c.F.Small.Draw(c.R, note, mark.Right + 10, hit.Y + 6 + c.F.UiBold.Height,
                           Color.Rgba(0xFFFFFF, 170));

        if (c.Clicked(hit))
        {
            pick();
            c.Sound(Sfx.Click, 0.6f);
        }
    }

    /// <summary>Back and Next, bottom right. Returns true on the last Next,
    /// which is what ends setup.</summary>
    bool DrawButtons(UiContext c, Rect area)
    {
        bool last = _page == Page.Ready;

        var next = new Rect(area.Right - 130, area.Y + 8, 122, 28);
        if (W.Button(c, "setup.next", next,
                     L.T(last ? "setup.finish" : "setup.next"), defaultButton: true))
        {
            if (last) return true;
            Go(c, 1);
        }

        var back = new Rect(next.X - 110, area.Y + 8, 102, 28);
        if (W.Button(c, "setup.back", back, L.T("setup.back"), enabled: _page != Page.Welcome))
            Go(c, -1);

        // Enter walks forward, which is how these were got through.
        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Enter))
        {
            c.KeyboardHandled = true;
            if (last) return true;
            Go(c, 1);
        }

        return false;
    }

    void Go(UiContext c, int delta)
    {
        int next = Math.Clamp((int)_page + delta, 0, (int)Page.Ready);
        if (next == (int)_page) return;

        _page = (Page)next;
        _entered = c.Time;
        c.Sound(Sfx.Navigate, 0.5f);
    }
}
