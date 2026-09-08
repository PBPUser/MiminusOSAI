using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>«Вас приветствует МИМИНУС ОС» — what happens the first time the
/// system is ever run here, in version 8's shape.
///
/// The out-of-box experience of version 8 was a screen of one flat colour with
/// one question on it: a heading in very large light type at the top left, a
/// short line under it, the controls in the middle of all that space, and a
/// single button in the bottom right corner. There is no step list, no wizard
/// frame and no window — the questions simply follow one another.
///
/// The colour is the question. Choosing one on the personalisation page repaints
/// the whole screen in it there and then, and it stays: it is the accent the
/// tiles, the taskbar and every «Миминус 8» window are built out of afterwards.
/// That was the one part of the original everybody remembered, and it is the
/// part that is easiest to mean literally.</summary>
public sealed class Setup
{
    readonly ShellHost _shell;

    public Setup(ShellHost shell) => _shell = shell;

    /// <summary>The pages, in order. Each one is a heading, a line of text and
    /// one thing to answer; the last has nothing to answer and waits instead.</summary>
    enum Page { Welcome, Language, Personalise, Look, Ready }

    Page _page = Page.Welcome;
    double _entered;

    /// <summary>The name being typed, which becomes the account name.</summary>
    public string UserName = "Admin";

    static readonly ThemeId[] Looks =
    {
        ThemeId.Metro, ThemeId.LunaBlue, ThemeId.LunaOlive, ThemeId.LunaSilver,
        ThemeId.Seven, ThemeId.Classic,
    };

    static readonly string[] LookKeys =
    {
        "setup.look_metro", "setup.look_blue", "setup.look_olive", "setup.look_silver",
        "setup.look_seven", "setup.look_classic",
    };

    int _look;
    int _accent;

    /// <summary>How far the page has slid in. Version 8 moved between questions
    /// rather than cutting, and the movement is the only chrome it had.</summary>
    float Entered(UiContext c) => (float)Math.Clamp((c.Time - _entered) / 0.28, 0, 1);

    public void Begin(UiContext c) => _entered = c.Time;

    /// <summary>Draws the current page and moves between them. Returns true
    /// when setup is finished and the system should carry on booting.</summary>
    public bool Draw(UiContext c)
    {
        var screen = new Rect(0, 0, c.ScreenW, c.ScreenH);
        DrawBackdrop(c, screen);

        float ease = Entered(c);
        float slide = (1 - ease) * 60;
        byte alpha = (byte)(255 * ease);

        var area = screen.Deflate(MathF.Max(56, c.ScreenW * 0.09f), 0,
                                  MathF.Max(56, c.ScreenW * 0.09f), 0);
        area.Y = MathF.Max(60, c.ScreenH * 0.18f);
        area.H = c.ScreenH - area.Y - 92;

        // ---- the heading, which is most of the design ----------------------
        var head = area.CutTop(c.F.Big.Height + 44);
        DrawBack(c, new Rect(head.X - 46, head.Y + 6, 34, 34), alpha);

        c.F.Big.Draw(c.R, L.T(HeadingKey), head.X - slide, head.Y,
                     Color.Rgba(0xFFFFFF, alpha));
        c.F.Caption.Draw(c.R, L.T(SubheadingKey), head.X + 2 - slide,
                         head.Y + c.F.Big.Height + 12,
                         Color.Rgba(0xFFFFFF, (byte)(alpha * 0.8f)));

        area.CutTop(18);
        var body = new Rect(area.X - slide, area.Y, area.W, area.H);

        switch (_page)
        {
            case Page.Welcome: DrawWelcomePage(c, body, alpha); break;
            case Page.Language: DrawLanguagePage(c, body, alpha); break;
            case Page.Personalise: DrawPersonalisePage(c, body, alpha); break;
            case Page.Look: DrawLookPage(c, body, alpha); break;
            default: DrawReadyPage(c, body, alpha); break;
        }

        return DrawNext(c, screen);
    }

    string HeadingKey => _page switch
    {
        Page.Welcome => "setup.welcome_heading",
        Page.Language => "setup.language_heading",
        Page.Personalise => "setup.personalise_heading",
        Page.Look => "setup.look_heading",
        _ => "setup.ready_heading",
    };

    string SubheadingKey => _page switch
    {
        Page.Welcome => "setup.welcome_sub",
        Page.Language => "setup.language_sub",
        Page.Personalise => "setup.personalise_sub",
        Page.Look => "setup.look_sub",
        _ => "setup.ready_sub",
    };

    // ---- the field it all sits on ----------------------------------------

    /// <summary>One flat colour, and the faint weave the tile board wears, so
    /// the two screens are recognisably the same system.</summary>
    void DrawBackdrop(UiContext c, Rect screen)
    {
        var accent = Theme.AccentPalette[_accent % Theme.AccentPalette.Length];

        c.R.FillRect(screen, accent);
        c.R.FillRectH(screen, accent.Shade(1.12f), accent.Shade(0.86f));

        var line = Color.Rgba(0xFFFFFF, 12);
        for (float x = -screen.H; x < screen.W; x += 30)
            c.R.Line(x, screen.Bottom, x + screen.H, screen.Y, line, 7);

        // The mark, small, in the corner opposite the button.
        var mark = new Rect(screen.X + 40, screen.Bottom - 62, 22, 22);
        for (int i = 0; i < 4; i++)
            c.R.FillRect(new Rect(mark.X + (i % 2) * 12, mark.Y + (i / 2) * 12, 10, 10),
                         Color.Rgba(0xFFFFFF, i % 3 == 0 ? (byte)230 : (byte)170));

        c.F.Ui.Draw(c.R, L.T("shell.miminus_os"), mark.Right + 12,
                    mark.CenterY - c.F.Ui.Height * 0.5f, Color.Rgba(0xFFFFFF, 200));
    }

    // ---- the pages --------------------------------------------------------

    void DrawWelcomePage(UiContext c, Rect body, byte alpha)
    {
        Paragraph(c, ref body, L.T("setup.welcome_body"), alpha);
        body.CutTop(12);
        Paragraph(c, ref body, L.T("setup.welcome_note"), alpha);
    }

    void DrawLanguagePage(UiContext c, Rect body, byte alpha)
    {
        Paragraph(c, ref body, L.T("setup.language_body"), alpha);
        body.CutTop(22);

        Tile(c, ref body, "Русский", L.T("setup.language_ru_note"), L.IsRu,
             () => L.Current = Lang.Ru, alpha);
        Tile(c, ref body, "English", L.T("setup.language_en_note"), !L.IsRu,
             () => L.Current = Lang.En, alpha);
    }

    /// <summary>The page the original was known for: a name, and a strip of
    /// colours that repaints the screen as it is used.</summary>
    void DrawPersonalisePage(UiContext c, Rect body, byte alpha)
    {
        Paragraph(c, ref body, L.T("setup.personalise_body"), alpha);
        body.CutTop(20);

        // ---- the colour strip ----------------------------------------------
        var strip = body.CutTop(46);
        float sw = 34, gap = 8;
        for (int i = 0; i < Theme.AccentPalette.Length; i++)
        {
            var r = new Rect(strip.X + i * (sw + gap), strip.Y, sw, sw);
            if (r.Right > strip.Right) break;

            bool picked = i == _accent;
            c.R.FillRect(r, Theme.AccentPalette[i]);

            if (picked)
            {
                c.R.DrawRect(r.Inflate(3), Color.Rgba(0xFFFFFF, alpha), 2);
                // The tick version 8 put on the chosen swatch.
                c.R.Line(r.X + 8, r.CenterY, r.CenterX - 1, r.Bottom - 9, Color.White, 2.4f);
                c.R.Line(r.CenterX - 1, r.Bottom - 9, r.Right - 7, r.Y + 8, Color.White, 2.4f);
            }
            else if (c.Hovering(r)) c.R.DrawRect(r.Inflate(2), Color.Rgba(0xFFFFFF, 160), 2);

            if (c.Clicked(r))
            {
                _accent = i;
                _shell.SetAccent(Theme.AccentPalette[i], c.F);
                c.SoundAt(Sfx.Click, r, 0.6f);
            }
        }

        body.CutTop(26);

        // ---- the name -------------------------------------------------------
        var row = body.CutTop(34);
        c.F.Ui.Draw(c.R, L.T("setup.your_name"), row.X, row.Y + 7, Color.Rgba(0xFFFFFF, alpha));

        // Version 8's fields were white boxes with no border and nothing else.
        var field = new Rect(row.X + 160, row.Y, MathF.Min(320, row.W - 170), 30);
        W.TextField(c, "setup.name", field, ref UserName);

        body.CutTop(16);
        Paragraph(c, ref body, L.T("setup.name_note"), alpha);
    }

    void DrawLookPage(UiContext c, Rect body, byte alpha)
    {
        Paragraph(c, ref body, L.T("setup.look_body"), alpha);
        body.CutTop(20);

        // The looks as a row of miniature windows, each painted in its own
        // colours: the only honest way to offer a theme is to show it.
        var row = body.CutTop(96);
        float w = MathF.Min(132, (row.W - 8 * (Looks.Length - 1)) / Looks.Length);

        for (int i = 0; i < Looks.Length; i++)
        {
            var r = new Rect(row.X + i * (w + 8), row.Y, w, 84);
            if (r.Right > row.Right) break;

            var preview = Theme.Create(Looks[i]);
            bool picked = i == _look;

            c.R.FillRect(r, preview.Face);
            c.R.FillRectV(new Rect(r.X, r.Y, r.W, 18), preview.CaptionActiveTop,
                          preview.CaptionActiveBottom);
            c.R.FillRect(new Rect(r.X + 8, r.Y + 28, r.W - 16, 10), preview.Accent);
            c.R.FillRect(new Rect(r.X + 8, r.Y + 44, r.W * 0.5f, 8), preview.ControlBorder);
            c.R.FillRect(new Rect(r.X, r.Bottom - 12, r.W, 12), preview.TaskbarMid);

            c.R.DrawRect(r, picked ? Color.White : Color.Rgba(0xFFFFFF, 90), picked ? 3 : 1);

            string name = c.F.Small.Ellipsize(L.T(LookKeys[i]), r.W);
            float nw = c.F.Small.Measure(name);
            c.F.Small.Draw(c.R, name, r.CenterX - nw * 0.5f, r.Bottom + 6,
                           Color.Rgba(0xFFFFFF, picked ? alpha : (byte)(alpha * 0.7f)));

            if (c.Clicked(r))
            {
                _look = i;
                _shell.SetTheme(Looks[i], c.F);
                c.SoundAt(Sfx.Navigate, r, 0.5f);
            }
        }

        body.CutTop(20);
        Paragraph(c, ref body, L.T("setup.look_note"), alpha);
    }

    /// <summary>The last page: the ring of dots from the loading screen and a
    /// line of text that changes every second and a half, which is the whole of
    /// what version 8 showed while it pretended to be doing something.</summary>
    void DrawReadyPage(UiContext c, Rect body, byte alpha)
    {
        Paragraph(c, ref body, L.F("setup.ready_body", UserName), alpha);
        body.CutTop(24);

        var ring = body.CutTop(64);
        float cx = ring.X + 26, cy = ring.CenterY;

        for (int i = 0; i < 5; i++)
        {
            double phase = (c.Time * 0.62 - i * 0.055) % 1.0;
            if (phase < 0) phase += 1;

            float eased = (float)(phase * phase * (3 - 2 * phase));
            float ang = -MathF.PI * 0.5f + eased * MathF.PI * 2;
            float fade = MathF.Min(1, MathF.Min((float)phase, 1 - (float)phase) * 8);

            c.R.FillCircle(cx + MathF.Cos(ang) * 20, cy + MathF.Sin(ang) * 20, 2.6f,
                           Color.Rgba(0xFFFFFF, (byte)(alpha * fade)));
        }

        string[] steps = { "setup.step_applying", "setup.step_installing", "setup.step_almost" };
        string line = L.T(steps[(int)((c.Time - _entered) / 1.6) % steps.Length]);
        c.F.Caption.Draw(c.R, line, ring.X + 62, cy - c.F.Caption.Height * 0.5f,
                         Color.Rgba(0xFFFFFF, alpha));

        body.CutTop(10);
        Paragraph(c, ref body, L.T("setup.ready_note"), alpha);
    }

    // ---- pieces -----------------------------------------------------------

    void Paragraph(UiContext c, ref Rect body, string text, byte alpha)
    {
        foreach (string line in c.F.Ui.Wrap(text, MathF.Min(body.W, 620)))
        {
            if (body.H < c.F.Ui.Height) return;
            var row = body.CutTop(c.F.Ui.Height + 6);
            c.F.Ui.Draw(c.R, line, row.X, row.Y, Color.Rgba(0xFFFFFF, (byte)(alpha * 0.88f)));
        }
    }

    /// <summary>One answer, drawn as version 8 drew a list item: a flat block
    /// that lightens under the pointer and keeps a white bar down its left when
    /// it is the chosen one. No radio buttons — there were none.</summary>
    void Tile(UiContext c, ref Rect body, string label, string note, bool selected,
              Action pick, byte alpha)
    {
        const float height = 54;
        if (body.H < height) return;

        var row = body.CutTop(height + 6);
        var hit = new Rect(row.X, row.Y, MathF.Min(row.W, 460), height);

        if (selected) c.R.FillRect(hit, Color.Rgba(0xFFFFFF, 55));
        else if (c.Hovering(hit)) c.R.FillRect(hit, Color.Rgba(0xFFFFFF, 30));
        if (selected) c.R.FillRect(new Rect(hit.X, hit.Y, 4, hit.H), Color.White);

        c.F.Caption.Draw(c.R, label, hit.X + 18, hit.Y + 9, Color.Rgba(0xFFFFFF, alpha));
        if (note != null)
            c.F.Small.Draw(c.R, note, hit.X + 19, hit.Y + 11 + c.F.Caption.Height,
                           Color.Rgba(0xFFFFFF, (byte)(alpha * 0.7f)));

        if (c.Clicked(hit))
        {
            pick();
            c.Sound(Sfx.Click, 0.6f);
        }
    }

    /// <summary>The circle with an arrow in it, to the left of the heading —
    /// which is where version 8 put going back, and the only place it was.</summary>
    void DrawBack(UiContext c, Rect r, byte alpha)
    {
        if (_page == Page.Welcome) return;

        bool hot = c.Hovering(r);
        c.R.DrawCircle(r.CenterX, r.CenterY, r.W * 0.5f,
                       Color.Rgba(0xFFFFFF, (byte)((hot ? 255 : 150) * alpha / 255f)), 2);
        W.Arrow(c, r, 3, Color.Rgba(0xFFFFFF, alpha), 5f);

        if (c.Clicked(r)) Go(c, -1);
    }

    /// <summary>The one button, bottom right: a flat white rectangle with the
    /// word and a chevron. Returns true when it is the last one.</summary>
    bool DrawNext(UiContext c, Rect screen)
    {
        bool last = _page == Page.Ready;
        string label = L.T(last ? "setup.finish" : "setup.next");

        var next = new Rect(screen.Right - 190, screen.Bottom - 76, 150, 40);
        bool hot = c.Hovering(next);
        bool held = hot && c.In.IsDown(MouseButton.Left);

        c.R.FillRect(next, held ? Color.Rgba(0xFFFFFF, 200)
                          : hot ? Color.White : Color.Rgba(0xFFFFFF, 235));

        var accent = Theme.AccentPalette[_accent % Theme.AccentPalette.Length];
        c.F.Caption.Draw(c.R, label, next.X + 20, next.CenterY - c.F.Caption.Height * 0.5f,
                         accent.Shade(0.7f));
        W.Arrow(c, new Rect(next.Right - 30, next.Y, 20, next.H), 1, accent.Shade(0.7f), 5f);

        bool go = c.Clicked(next);

        // Enter walks forward, which is how these were got through.
        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Enter))
        {
            c.KeyboardHandled = true;
            go = true;
        }

        if (!go) return false;
        if (last) return true;

        Go(c, 1);
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
