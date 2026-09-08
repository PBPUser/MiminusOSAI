using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Веб-браузер Firefox» — the renamed browser from part 2, with Opera's
/// Экспресс-панель from part 1 as its home page.
///
/// There is no network here: pages are small hand-drawn layouts keyed by URL,
/// which is enough to reproduce the three sites the videos actually visit —
/// the Mozilla download page, a Google search for «скачать интернет», and the
/// speed-dial grid.
///
/// The chrome around those pages is the one both browsers grew into. Neither
/// has a menu bar any more: the tabs sit at the top with a stripe of the brand
/// colour along the one that is showing, the address is a rounded pill with the
/// padlock inside it, the buttons are flat glyphs that only take a shape when
/// the pointer is on them, and everything that used to be «Файл», «Правка» and
/// the rest is behind one button on the right — three lines for Фигефох, the
/// round O for Орега. It is the same window underneath; it stopped looking
/// like 2009.</summary>
/// <summary>Which of the two browsers from the videos this window is.</summary>
public enum BrowserBrand
{
    /// <summary>Part 1: "Это браузер. Называется Орега."</summary>
    Orega,
    /// <summary>Part 2: the "Italian-Russian" replacement.</summary>
    Figefoch,
}

public sealed class BrowserWindow : OsWindow
{
    sealed class Tab
    {
        public string Url = "about:home";
        public string Title = "";
        public float Scroll;
        public readonly List<string> Back = new();
        public readonly List<string> Forward = new();
    }

    readonly List<Tab> _tabs = new();
    int _active;
    string _addressText = "";
    bool _addressFocused;
    string _searchText = "";

    readonly BrowserBrand _brand;

    string BrandName => L.T(_brand == BrowserBrand.Orega ? "browser.orega" : "browser.figefoch");

    /// <summary>Everything that differs between the two browsers, which by this
    /// point in their lives is a palette and one button glyph. The rest of the
    /// chrome is the same shape in both — that is what happened to browsers.</summary>
    readonly record struct Skin(Color Strip, Color TabActive, Color TabHover, Color Toolbar,
                                Color Line, Color Field, Color FieldEdge, Color Accent,
                                Color Ink, Color InkDim);

    /// <summary>Фигефох wears Photon: a grey tab strip, a near-white toolbar and
    /// a blue stripe over the tab that is showing. Орега wears its own red over
    /// the same shapes.</summary>
    Skin S => _brand == BrowserBrand.Orega
        ? new Skin(Color.Rgb(0xDCDCDE), Color.Rgb(0xFFFFFF), Color.Rgb(0xEBEBED),
                   Color.Rgb(0xFFFFFF), Color.Rgb(0xD2D2D4), Color.Rgb(0xF1F1F2),
                   Color.Rgb(0xD8D8DA), Color.Rgb(0xFF1B2D), Color.Rgb(0x2B2B2B),
                   Color.Rgb(0x8A8A8E))
        : new Skin(Color.Rgb(0xE3E4E6), Color.Rgb(0xF9F9FA), Color.Rgb(0xEDEDF0),
                   Color.Rgb(0xF9F9FA), Color.Rgb(0xD7D7DB), Color.Rgb(0xFFFFFF),
                   Color.Rgb(0xC5C5C8), Color.Rgb(0x0A84FF), Color.Rgb(0x0C0C0D),
                   Color.Rgb(0x737373));

    public override string Title
        => (Cur.Title.Length > 0 ? Cur.Title + " — " : "") + BrandName;

    public override float MinWidth => 560;
    public override float MinHeight => 380;

    /// <summary>The tabs live in the title bar. There is nowhere else left for
    /// them: both browsers reached the same conclusion within a year of each
    /// other, and it is the single change that dates a browser window fastest.
    /// A full-screen window has no caption to put them in, so that one keeps
    /// its own strip inside the client area.</summary>
    public override bool CaptionTabs => !Immersive;

    public override void DrawCaptionTabs(UiContext c, Rect strip)
    {
        _ctx = c;
        DrawTabStrip(c, strip, overCaption: true);
    }

    Tab Cur => _tabs[Math.Clamp(_active, 0, _tabs.Count - 1)];

    public BrowserWindow(BrowserBrand brand = BrowserBrand.Figefoch)
    {
        _brand = brand;
        Icon = brand == BrowserBrand.Orega ? IconId.Opera : IconId.Firefox;
        Bounds = new Rect(0, 0, 880, 600);
        _tabs.Add(new Tab());
        // Орега opens on its Express panel; the newer browser on the download page.
        Navigate(brand == BrowserBrand.Orega
            ? "about:home"
            : "http://www.mozilla-europe.org/ru/firefox/", null, record: false);
    }

    /// <summary>What used to be the menu bar, folded into the one button on the
    /// right of the toolbar — which is where both browsers put it when they
    /// stopped having a menu bar to fold it out of.</summary>
    List<MenuItem> AppMenu() => new()
    {
        MenuItem.Of(L.T("browser.new_tab"), () => NewTab(_ctx), IconId.Globe, "Ctrl+T"),
        MenuItem.Of(L.T("browser.close_tab"), () => CloseTab(_active, _ctx), IconId.None,
                    "Ctrl+W", _tabs.Count > 1),
        MenuItem.Sep(),
        MenuItem.Of(L.T("browser.reload"), () => { Cur.Scroll = 0; _ctx.Sound(Sfx.Navigate, 0.5f); },
                    IconId.None, "F5"),
        MenuItem.Of(L.T("browser.home"), () => Navigate("about:home", _ctx), IconId.Star, "Alt+Home"),
        MenuItem.Sep(),
        MenuItem.Sub(L.T("browser.bookmarks"), Bookmarks(), IconId.Star),
        MenuItem.Sub(L.T("browser.edit"), new List<MenuItem>
        {
            MenuItem.Of(L.T("browser.copy_address"), () => Clipboard.SetText(Cur.Url)),
            MenuItem.Of(L.T("browser.paste_and_go"), () => Navigate(Clipboard.GetText().Trim(), _ctx)),
        }),
        MenuItem.Sep(),
        MenuItem.Of(L.T("browser.about"), () =>
            Shell.MessageBox(_ctx, BrandName,
                L.T(_brand == BrowserBrand.Orega
                    ? "browser.firefox_web_browser_3_6_part_of_miminus_os_r"
                    : "browser.figefoch_about"),
                MsgButtons.Ok, Icon, null, Sfx.Info), IconId.DlgInfo),
        MenuItem.Of(L.T("browser.exit"), Close),
    };

    List<MenuItem> Bookmarks() => new()
    {
        MenuItem.Of("mozilla-europe.org",
                    () => Navigate("http://www.mozilla-europe.org/ru/firefox/", _ctx), IconId.Firefox),
        MenuItem.Of("google.ru", () => Navigate("http://www.google.ru/", _ctx), IconId.Globe),
        MenuItem.Of(L.T("browser.sodly"), () => Navigate("http://www.sodly.google.com/", _ctx), IconId.Globe),
        MenuItem.Of(L.T("browser.speed_dial"), () => Navigate("about:home", _ctx), IconId.Star),
        MenuItem.Of("miminus-os.ru", () => Navigate("http://miminus-os.ru/", _ctx), IconId.Star),
    };

    UiContext _ctx;

    void NewTab(UiContext c)
    {
        _tabs.Add(new Tab());
        _active = _tabs.Count - 1;
        Navigate("about:home", c, record: false);
    }

    void CloseTab(int index, UiContext c)
    {
        if (_tabs.Count <= 1) { Close(); return; }
        _tabs.RemoveAt(index);
        _active = Math.Clamp(_active, 0, _tabs.Count - 1);
        c.Sound(Sfx.WindowClose, 0.4f);
    }

    void Navigate(string url, UiContext c, bool record = true)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        url = url.Trim();

        // Anything that is not a URL becomes a search, like a real address bar.
        if (!url.StartsWith("about:") && !url.Contains('.') && !url.StartsWith("http"))
            url = "search:" + url;
        else if (!url.StartsWith("about:") && !url.StartsWith("search:") && !url.StartsWith("http"))
            url = "http://" + url;

        var tab = Cur;
        if (record && tab.Url != url)
        {
            tab.Back.Add(tab.Url);
            tab.Forward.Clear();
        }
        tab.Url = url;
        tab.Scroll = 0;
        tab.Title = TitleFor(url);
        _addressText = url;
        c?.Sound(Sfx.Navigate, 0.5f);
    }

    static string TitleFor(string url)
    {
        if (url == "about:home") return L.T("browser.speed_dial");
        if (url.StartsWith("search:")) return url[7..] + " — " + L.T("browser.google_search");
        if (url.Contains("mozilla")) return L.T("browser.firefox_download");
        if (url.Contains("sodly")) return L.T("browser.sodly");
        if (url.Contains("google")) return "Google";
        if (url.Contains("miminus")) return L.T("browser.miminus_os_official_site");
        return url;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        c.R.FillRect(client, c.Theme.Face);

        var area = client;
        // In a window the tabs are up in the caption; full screen there is no
        // caption, so they come back inside.
        if (Immersive) DrawTabStrip(c, area.CutTop(32), overCaption: false);
        DrawNavBar(c, area.CutTop(38));

        c.R.FillRect(area, Color.White);
        c.R.PushClip(area);
        DrawPage(c, area);

        // The status bar went away and came back as this: a small tab of text
        // in the bottom-left corner, over the page rather than under it.
        string note = L.T("browser.no_connection_required");
        var bubble = new Rect(area.X, area.Bottom - c.F.Small.Height - 6,
                              c.F.Small.Measure(note) + 16, c.F.Small.Height + 6);
        c.R.FillRect(bubble, Color.Rgb(0xF1F1F2));
        c.R.FillRect(new Rect(bubble.X, bubble.Y, bubble.W, 1), Color.Rgb(0xDCDCDE));
        c.R.FillRect(new Rect(bubble.Right - 1, bubble.Y, 1, bubble.H), Color.Rgb(0xDCDCDE));
        c.F.Small.Draw(c.R, note, bubble.X + 8, bubble.CenterY - c.F.Small.Height * 0.5f,
                       Color.Rgb(0x5A5A5E));

        c.R.PopClip();

        if (!c.KeyboardHandled && c.In.Ctrl)
        {
            if (c.In.KeyPressed(Keys.T)) { NewTab(c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.W)) { CloseTab(_active, c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.L)) { c.Focus = Id + ".addr"; c.KeyboardHandled = true; }
        }
    }

    /// <summary>The tab strip both browsers ended up with: square tabs that
    /// meet without a gap, no bevel anywhere, and a stripe of the brand colour
    /// along the top of the one that is showing.
    ///
    /// Over the caption it draws no background of its own — the title bar is
    /// already painted, and the idle tabs are a wash over it, which is how a
    /// browser's tabs sit in a title bar without looking pasted on.</summary>
    void DrawTabStrip(UiContext c, Rect strip, bool overCaption)
    {
        var k = S;
        if (!overCaption) c.R.FillRect(strip, k.Strip);

        float tabW = Math.Clamp((strip.W - 44) / Math.Max(1, _tabs.Count), 92, 236);
        float x = strip.X;

        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            bool sel = i == _active;
            var r = new Rect(x, strip.Y, tabW, strip.H);
            bool hot = c.Hovering(r);

            if (sel)
            {
                // The tab in front is painted in the toolbar's own colour, so
                // it and the bar below it read as one shape.
                c.R.FillRect(r, overCaption ? k.Toolbar : k.TabActive);
                c.R.FillRect(new Rect(r.X, r.Y, r.W, 2), k.Accent);
            }
            else if (hot) c.R.FillRect(r, overCaption ? Color.Rgba(0xFFFFFF, 60) : k.TabHover);
            else if (overCaption) c.R.FillRect(r.Deflate(0, 3, 0, 0), Color.Rgba(0xFFFFFF, 26));

            // The separator between two idle tabs, which is all that divides
            // them now that they have no edges of their own.
            if (!sel && i + 1 <= _tabs.Count - 1 && _active != i + 1)
                c.R.FillRect(new Rect(r.Right - 1, r.Y + 8, 1, r.H - 16),
                             overCaption ? Color.Rgba(0xFFFFFF, 60) : Color.Rgba(0x000000, 40));

            Icons.Draw(c.R, IconId.Globe, new Rect(r.X + 9, r.CenterY - 8, 16, 16));

            var close = new Rect(r.Right - 24, r.CenterY - 9, 18, 18);
            c.R.PushClip(new Rect(r.X + 31, r.Y, MathF.Max(0, close.X - r.X - 35), r.H));
            c.F.Small.Draw(c.R, tab.Title.Length > 0 ? tab.Title : L.T("browser.new_tab"),
                           r.X + 31, r.CenterY - c.F.Small.Height * 0.5f,
                           sel ? k.Ink : overCaption ? c.Theme.CaptionTextActive : k.InkDim);
            c.R.PopClip();

            // The close cross appears on the tab under the pointer and on the
            // one showing, and nowhere else.
            if (sel || hot)
            {
                bool overClose = c.Hovering(close);
                if (overClose) c.R.RoundedRect(close, 3, Color.Rgba(0x000000, 26));
                var ink = sel || overClose ? (overClose ? k.Ink : k.InkDim)
                        : overCaption ? c.Theme.CaptionTextActive : k.InkDim;
                c.R.Line(close.X + 6, close.Y + 6, close.Right - 6, close.Bottom - 6, ink, 1.4f);
                c.R.Line(close.Right - 6, close.Y + 6, close.X + 6, close.Bottom - 6, ink, 1.4f);
                if (c.Clicked(close)) { CloseTab(i, c); return; }
            }

            if (c.Clicked(r)) { _active = i; _addressText = tab.Url; c.SoundAt(Sfx.Tick, r, 0.3f); }
            x += tabW;
        }

        var plus = new Rect(x + 2, strip.CenterY - 11, 22, 22);
        if (c.Hovering(plus))
            c.R.RoundedRect(plus, 3, overCaption ? Color.Rgba(0xFFFFFF, 60) : Color.Rgba(0x000000, 26));
        var plusInk = overCaption ? c.Theme.CaptionTextActive : k.InkDim;
        c.R.FillRect(new Rect(plus.CenterX - 5.5f, plus.CenterY - 0.75f, 11, 1.5f), plusInk);
        c.R.FillRect(new Rect(plus.CenterX - 0.75f, plus.CenterY - 5.5f, 1.5f, 11), plusInk);
        c.Tooltip(plus, L.T("browser.new_tab"));
        if (c.Clicked(plus)) NewTab(c);
    }

    /// <summary>The toolbar: four flat glyphs, one rounded pill for the address
    /// with the padlock and the star inside it, a second pill for the search,
    /// and the button that holds the menu.</summary>
    void DrawNavBar(UiContext c, Rect bar)
    {
        var k = S;
        var tab = Cur;

        c.R.FillRect(bar, k.Toolbar);
        c.R.FillRect(new Rect(bar.X, bar.Bottom - 1, bar.W, 1), k.Line);

        float bs = 28;
        float y = bar.CenterY - bs * 0.5f;
        float x = bar.X + 4;

        if (Glyph(c, new Rect(x, y, bs, bs), tab.Back.Count > 0, GlyphKind.Back))
        {
            tab.Forward.Add(tab.Url);
            string url = tab.Back[^1];
            tab.Back.RemoveAt(tab.Back.Count - 1);
            tab.Url = url; tab.Title = TitleFor(url); tab.Scroll = 0; _addressText = url;
            c.Sound(Sfx.Navigate, 0.45f);
        }
        x += bs + 2;

        if (Glyph(c, new Rect(x, y, bs, bs), tab.Forward.Count > 0, GlyphKind.Forward))
        {
            tab.Back.Add(tab.Url);
            string url = tab.Forward[^1];
            tab.Forward.RemoveAt(tab.Forward.Count - 1);
            tab.Url = url; tab.Title = TitleFor(url); tab.Scroll = 0; _addressText = url;
            c.Sound(Sfx.Navigate, 0.45f);
        }
        x += bs + 2;

        if (Glyph(c, new Rect(x, y, bs, bs), true, GlyphKind.Reload))
        {
            tab.Scroll = 0;
            c.Sound(Sfx.Navigate, 0.5f);
        }
        x += bs + 2;

        var homeR = new Rect(x, y, bs, bs);
        if (Glyph(c, homeR, true, GlyphKind.Home)) Navigate("about:home", c);
        c.Tooltip(homeR, L.T("browser.speed_dial"));
        x += bs + 8;

        // ---- the button that holds what used to be the menu bar ---------------
        var menu = new Rect(bar.Right - bs - 6, y, bs, bs);
        if (Glyph(c, menu, true, _brand == BrowserBrand.Orega ? GlyphKind.Opera : GlyphKind.Burger))
            Shell.Menus.Open(AppMenu(), menu.X - 120, menu.Bottom + 2, this, c, 190);

        // ---- the two pills -----------------------------------------------------
        var search = new Rect(menu.X - 176, y + 2, 168, bs - 4);
        var addr = new Rect(x, y + 2, search.X - x - 8, bs - 4);

        DrawAddressPill(c, addr, tab, k);
        DrawSearchPill(c, search, k);
    }

    /// <summary>The address bar, which is a pill now: the padlock on the left,
    /// the text in the middle and the bookmark star on the right — all inside
    /// the one shape, the way both browsers ended up drawing it.</summary>
    void DrawAddressPill(UiContext c, Rect addr, Tab tab, Skin k)
    {
        _addressFocused = c.Focus == Id + ".addr";

        float rad = addr.H * 0.5f;
        c.R.RoundedRect(addr, rad, _addressFocused ? Color.White : k.Field,
                        _addressFocused ? k.Accent : k.FieldEdge, _addressFocused ? 1.6f : 1);

        // The padlock: green and shut for a site, grey and open for about: and
        // for a search, which is what the difference means.
        bool secure = tab.Url.StartsWith("http");
        Padlock(c, new Rect(addr.X + 8, addr.CenterY - 7, 14, 14),
                secure ? Color.Rgb(0x12BC00) : k.InkDim, secure);

        var star = new Rect(addr.Right - 26, addr.CenterY - 9, 18, 18);
        bool starHot = c.Hovering(star);
        if (starHot) c.R.RoundedRect(star, 3, Color.Rgba(0x000000, 24));
        Icons.Draw(c.R, IconId.Star, star.Deflate(2));
        c.Tooltip(star, L.T("browser.bookmarks"));
        if (c.Clicked(star)) Shell.Menus.Open(Bookmarks(), star.X - 80, star.Bottom + 4, this, c);

        var field = new Rect(addr.X + 28, addr.Y, star.X - addr.X - 32, addr.H);
        if (c.Hovering(field)) c.Cursor = CursorShape.Text;
        if (c.Clicked(field)) { c.Focus = Id + ".addr"; _addressText = tab.Url; }

        if (_addressFocused && !c.KeyboardHandled)
        {
            foreach (char ch in c.In.TypedChars)
            {
                if (ch == '\r') { Navigate(_addressText, c); c.Focus = null; }
                else if (ch >= ' ') _addressText += ch;
                c.KeyboardHandled = true;
            }
            if (c.In.KeyPressed(Keys.Back) && _addressText.Length > 0)
            {
                _addressText = _addressText[..^1];
                c.KeyboardHandled = true;
            }
            if (c.In.KeyPressed(Keys.Escape)) { _addressText = tab.Url; c.Focus = null; c.KeyboardHandled = true; }
        }

        // The host is drawn in full ink and the rest of the address in grey,
        // which is the one piece of typography the newer bars actually added.
        string shown = _addressFocused ? _addressText : tab.Url;
        c.R.PushClip(field);
        float tx = field.X;
        float ty = field.CenterY - c.F.Ui.Height * 0.5f;

        if (!_addressFocused && shown.StartsWith("http"))
        {
            int slashes = shown.IndexOf("//", StringComparison.Ordinal);
            int hostEnd = slashes < 0 ? -1 : shown.IndexOf('/', slashes + 2);
            string scheme = slashes < 0 ? "" : shown[..(slashes + 2)];
            string host = hostEnd < 0 ? shown[(slashes + 2)..] : shown[(slashes + 2)..hostEnd];
            string rest = hostEnd < 0 ? "" : shown[hostEnd..];

            c.F.Ui.Draw(c.R, scheme, tx, ty, k.InkDim); tx += c.F.Ui.Measure(scheme);
            c.F.Ui.Draw(c.R, host, tx, ty, k.Ink); tx += c.F.Ui.Measure(host);
            c.F.Ui.Draw(c.R, rest, tx, ty, k.InkDim);
        }
        else
        {
            c.F.Ui.Draw(c.R, shown, tx, ty, k.Ink);
            if (_addressFocused && (c.Time % 1.06) < 0.53)
                c.R.FillRect(new Rect(tx + c.F.Ui.Measure(shown), addr.Y + 5, 1.4f, addr.H - 10), k.Ink);
        }
        c.R.PopClip();
    }

    void DrawSearchPill(UiContext c, Rect search, Skin k)
    {
        bool focused = c.Focus == Id + ".search";
        float rad = search.H * 0.5f;
        c.R.RoundedRect(search, rad, focused ? Color.White : k.Field,
                        focused ? k.Accent : k.FieldEdge, focused ? 1.6f : 1);

        Icons.Draw(c.R, IconId.Search, new Rect(search.X + 8, search.CenterY - 7, 14, 14));
        if (c.Hovering(search)) c.Cursor = CursorShape.Text;
        if (c.Clicked(search)) c.Focus = Id + ".search";

        if (focused && !c.KeyboardHandled)
        {
            foreach (char ch in c.In.TypedChars)
            {
                if (ch == '\r') { Navigate("search:" + _searchText, c); _searchText = ""; c.Focus = null; }
                else if (ch >= ' ') _searchText += ch;
                c.KeyboardHandled = true;
            }
            if (c.In.KeyPressed(Keys.Back) && _searchText.Length > 0)
            {
                _searchText = _searchText[..^1];
                c.KeyboardHandled = true;
            }
        }

        c.R.PushClip(new Rect(search.X + 26, search.Y, search.W - 34, search.H));
        c.F.Ui.Draw(c.R, _searchText.Length > 0 ? _searchText : L.T("browser.search"),
                    search.X + 26, search.CenterY - c.F.Ui.Height * 0.5f,
                    _searchText.Length > 0 ? k.Ink : k.InkDim);
        if (focused && (c.Time % 1.06) < 0.53)
            c.R.FillRect(new Rect(search.X + 26 + c.F.Ui.Measure(_searchText), search.Y + 5,
                                  1.4f, search.H - 10), k.Ink);
        c.R.PopClip();
    }

    enum GlyphKind { Back, Forward, Reload, Home, Burger, Opera }

    /// <summary>A toolbar button with nothing around it until the pointer
    /// arrives — which is the single biggest difference between a browser of
    /// 2009 and one of five years later.</summary>
    bool Glyph(UiContext c, Rect r, bool enabled, GlyphKind kind)
    {
        var k = S;
        bool hover = enabled && c.Hovering(r);
        bool held = hover && c.In.IsDown(MouseButton.Left);

        if (held) c.R.RoundedRect(r, 4, Color.Rgba(0x000000, 44));
        else if (hover) c.R.RoundedRect(r, 4, Color.Rgba(0x000000, 26));

        var ink = enabled ? k.Ink : Color.Rgb(0xBFBFC3);
        float cx = MathF.Round(r.CenterX), cy = MathF.Round(r.CenterY);

        switch (kind)
        {
            case GlyphKind.Back:
            case GlyphKind.Forward:
            {
                // A chevron, not a filled triangle: two strokes meeting.
                float d = kind == GlyphKind.Back ? 1 : -1;
                c.R.Line(cx + d * 2.5f, cy - 5.5f, cx - d * 2.5f, cy, ink, 1.8f);
                c.R.Line(cx - d * 2.5f, cy, cx + d * 2.5f, cy + 5.5f, ink, 1.8f);
                break;
            }

            case GlyphKind.Reload:
            {
                // Three quarters of a ring and the arrowhead that closes it.
                for (int i = 0; i < 20; i++)
                {
                    float a0 = -1.0f + i / 20f * 5.0f;
                    float a1 = -1.0f + (i + 1) / 20f * 5.0f;
                    c.R.Line(cx + MathF.Cos(a0) * 6, cy + MathF.Sin(a0) * 6,
                             cx + MathF.Cos(a1) * 6, cy + MathF.Sin(a1) * 6, ink, 1.7f);
                }
                c.R.FillTriangle(cx + 3, cy - 8.5f, cx + 9, cy - 6.5f, cx + 3.4f, cy - 2.6f, ink);
                break;
            }

            case GlyphKind.Home:
            {
                c.R.FillTriangle(cx, cy - 7, cx - 8, cy, cx + 8, cy, ink);
                c.R.FillRect(new Rect(cx - 5.5f, cy - 0.5f, 11, 7.5f), ink);
                c.R.FillRect(new Rect(cx - 1.6f, cy + 2.5f, 3.2f, 4.5f), k.Toolbar);
                break;
            }

            case GlyphKind.Burger:
                for (int i = -1; i <= 1; i++)
                    c.R.FillRect(new Rect(cx - 7, cy + i * 5 - 0.9f, 14, 1.8f), ink);
                break;

            case GlyphKind.Opera:
                // The O, which is the whole of that browser's identity.
                c.R.DrawCircle(cx, cy, 8, k.Accent, 3f);
                c.R.DrawCircle(cx, cy, 3.4f, k.Accent, 2.4f);
                break;
        }

        return enabled && c.Clicked(r);
    }

    /// <summary>The padlock in the address bar, drawn small enough that the
    /// shackle is a half-ring and the body is a rectangle.</summary>
    static void Padlock(UiContext c, Rect r, Color col, bool shut)
    {
        float cx = r.CenterX;
        float top = r.Y + 1.5f;
        float bodyY = r.Y + 6.5f;

        for (int i = 0; i <= 10; i++)
        {
            float a0 = MathF.PI + i / 10f * MathF.PI;
            float a1 = MathF.PI + (i + 1) / 10f * MathF.PI;
            float ox = shut ? 0 : 2f;
            c.R.Line(cx + MathF.Cos(a0) * 3.2f + ox, top + 3.4f + MathF.Sin(a0) * 3.4f,
                     cx + MathF.Cos(a1) * 3.2f + ox, top + 3.4f + MathF.Sin(a1) * 3.4f, col, 1.4f);
        }

        c.R.RoundedRect(new Rect(cx - 4.6f, bodyY, 9.2f, 6.8f), 1.4f, col);
    }

    // ---- page rendering --------------------------------------------------

    void DrawPage(UiContext c, Rect page)
    {
        var tab = Cur;
        float contentH;

        if (tab.Url == "about:home") contentH = DrawSpeedDial(c, page);
        else if (tab.Url.StartsWith("search:")) contentH = DrawSearchResults(c, page, tab.Url[7..]);
        else if (tab.Url.Contains("mozilla")) contentH = DrawMozillaPage(c, page);
        else if (tab.Url.Contains("google")) contentH = DrawGoogleHome(c, page);
        else if (tab.Url.Contains("miminus")) contentH = DrawMiminusSite(c, page);
        else contentH = DrawNotFound(c, page);

        if (contentH > page.H)
        {
            tab.Scroll = W.ScrollBarV(c, Id + ".pagesb",
                new Rect(page.Right - W.ScrollBarSize, page.Y, W.ScrollBarSize, page.H),
                tab.Scroll, contentH, page.H);
            if (c.Hovering(page) && c.In.WheelDelta != 0)
            {
                tab.Scroll = Math.Clamp(tab.Scroll - c.In.WheelDelta * 48, 0, contentH - page.H);
                c.MouseHandled = true;
            }
        }
        else tab.Scroll = 0;
    }

    /// <summary>Экспресс-панель, as it looks once the browser has caught up:
    /// the tiles are rounded cards on a grey field, each one a flat block of
    /// its site's colour with the initial in it, the numbers are gone, and the
    /// search line is one pill in the middle of the page.
    ///
    /// The nine sites are still part 1's nine sites, and «Копирайт Михаила
    /// Гревцова» is still printed under the search box, because that is the
    /// joke — only the paint is newer.</summary>
    float DrawSpeedDial(UiContext c, Rect page)
    {
        var tab = Cur;
        var k = S;
        float y = page.Y - tab.Scroll;

        c.R.FillRect(page, Color.Rgb(0xF5F5F7));

        // ---- the search pill in the middle of the page ------------------------
        var searchRow = new Rect(page.CenterX - 220, y + 40, 440, 40);
        bool focused = c.Focus == Id + ".search";
        c.R.RoundedRect(searchRow, searchRow.H * 0.5f, Color.White,
                        focused ? k.Accent : Color.Rgb(0xDCDCDE), focused ? 1.6f : 1);
        Icons.Draw(c.R, IconId.Search, new Rect(searchRow.X + 12, searchRow.CenterY - 9, 18, 18));

        c.R.PushClip(new Rect(searchRow.X + 38, searchRow.Y, searchRow.W - 110, searchRow.H));
        c.F.Ui.Draw(c.R, _searchText.Length > 0 ? _searchText : L.T("browser.search_2"),
                    searchRow.X + 38, searchRow.CenterY - c.F.Ui.Height * 0.5f,
                    _searchText.Length > 0 ? k.Ink : k.InkDim);
        c.R.PopClip();

        var go = new Rect(searchRow.Right - 78, searchRow.Y + 5, 68, searchRow.H - 10);
        bool goHot = c.Hovering(go);
        c.R.RoundedRect(go, go.H * 0.5f, goHot ? k.Accent.Shade(1.12f) : k.Accent);
        c.F.Small.DrawCentered(c.R, L.T("browser.search_3"), go, Color.White);
        if (c.Clicked(go))
            Navigate("search:" + (_searchText.Length > 0 ? _searchText : "скачать интернет"), c);
        if (c.Clicked(searchRow)) c.Focus = Id + ".search";

        // "Вот написано Копирайт Михаила Гревцова" (part 1, 01:36)
        string copyright = L.T("browser.copyright_grevtsov");
        c.F.Small.Draw(c.R, copyright, page.CenterX - c.F.Small.Measure(copyright) * 0.5f,
                       searchRow.Bottom + 12, Color.Rgb(0x9A9A9E));

        (string title, string url, Color col)[] dials =
        {
            (L.T("browser.file_sharing"), "http://torrents.ru/", Color.Rgb(0x3C7DBF)),
            ("Яндекс", "http://yandex.ru/", Color.Rgb(0xC4302B)),
            ("Евроспорт.Ру", "http://eurosport.ru/", Color.Rgb(0x1E7A3C)),
            ("YouTube", "http://youtube.com/", Color.Rgb(0xC4302B)),
            ("Donolink Servers", "http://donolink.ru/", Color.Rgb(0x4A5568)),
            ("Facebook", "http://facebook.com/", Color.Rgb(0x3B5998)),
            (L.T("browser.vkontakte"), "http://vkontakte.ru/", Color.Rgb(0x4C75A3)),
            ("AMBK.RU", "http://ambk.ru/", Color.Rgb(0xE07020)),
            (L.T("browser.miminus_os"), "http://miminus-os.ru/", Color.Rgb(0xE8B800)),
        };

        float gridTop = searchRow.Bottom + 44;
        float cellW = MathF.Min(196, (page.W - 96) / 3);
        float cellH = cellW * 0.66f;
        float gridW = cellW * 3 + 40;
        float gx = page.CenterX - gridW * 0.5f;

        for (int i = 0; i < dials.Length; i++)
        {
            var (title, url, col) = dials[i];
            var cell = new Rect(gx + (i % 3) * (cellW + 20), gridTop + (i / 3) * (cellH + 46),
                                cellW, cellH);
            bool hover = c.Hovering(cell);

            // The card lifts a little under the pointer, which is the whole of
            // the animation a speed dial ever had.
            var card = hover ? cell.Offset(0, -2) : cell;
            if (hover) c.R.RoundedRect(card.Offset(0, 3), 5, Color.Rgba(0x000000, 34));
            c.R.RoundedRect(card, 5, Color.White, Color.Rgb(0xE2E2E4));

            // A flat block of the site's colour with its initial cut out of it,
            // which is what a browser draws when it has no screenshot to show.
            var face = new Rect(card.X + 1, card.Y + 1, card.W - 2, card.H - 2);
            c.R.RoundedRect(face, 4, col);
            string initial = title[..1].ToUpperInvariant();
            c.F.Big.DrawCentered(c.R, initial, face, Color.Rgba(0xFFFFFF, 235));

            // The strip along the foot of the card carries the name, inside the
            // card rather than under it.
            var strip = new Rect(card.X + 1, card.Bottom - 23, card.W - 2, 22);
            c.R.FillRect(strip, Color.Rgba(0xFFFFFF, 240));
            c.R.PushClip(strip);
            c.F.Small.DrawCentered(c.R, c.F.Small.Ellipsize(title, strip.W - 10), strip,
                                   Color.Rgb(0x303030));
            c.R.PopClip();

            if (hover) c.R.RoundedRect(card, 5, Color.Transparent, k.Accent, 2);
            if (c.Clicked(cell)) Navigate(url, c);
        }

        float bottom = gridTop + 3 * (cellH + 46) + 6;

        string hint = L.T("browser.what_is_speed_dial");
        string hide = L.T("browser.hide_speed_dial");
        var hintR = new Rect(page.X + 30, bottom, c.F.Ui.Measure(hint), c.F.Ui.Height + 4);
        var hideR = new Rect(page.Right - c.F.Ui.Measure(hide) - 36, bottom,
                             c.F.Ui.Measure(hide), c.F.Ui.Height + 4);
        c.F.Ui.Draw(c.R, hint, hintR.X, hintR.Y, c.Hovering(hintR) ? k.Accent : Color.Rgb(0x8A8A8E));
        c.F.Ui.Draw(c.R, hide, hideR.X, hideR.Y, c.Hovering(hideR) ? k.Accent : Color.Rgb(0x8A8A8E));

        return bottom + 40 - page.Y + tab.Scroll;
    }

    float DrawMozillaPage(UiContext c, Rect page)
    {
        float y = page.Y - Cur.Scroll;

        var header = new Rect(page.X, y, page.W, 46);
        c.R.FillRectV(header, Color.Rgb(0x3B5F8A), Color.Rgb(0x24405F));
        c.F.Big.Draw(c.R, "mozilla europe", header.X + 24, header.CenterY - c.F.Big.Height * 0.5f, Color.White);

        float mx = header.Right - 24;
        foreach (string item in new[]
                 {
                     L.T("browser.press"), L.T("browser.about_2"),
                     L.T("browser.support"), L.T("browser.add_ons"), L.T("browser.products"),
                 })
        {
            float w = c.F.Ui.Measure(item);
            c.F.Ui.Draw(c.R, item, mx - w, header.CenterY - c.F.Ui.Height * 0.5f, Color.Rgba(0xFFFFFF, 220));
            mx -= w + 22;
        }

        var hero = new Rect(page.X, header.Bottom, page.W, 210);
        c.R.FillRectV(hero, Color.Rgb(0xE8F1FA), Color.Rgb(0xCFE2F2));

        c.F.Big.Draw(c.R, L.T("browser.meet_the_world_s_best_browser"),
                     hero.X + 30, hero.Y + 22, Color.Rgb(0x143A5E));

        string blurb = L.T("browser.secure_stable_and_fast_firefox_is_simply_bui");
        float by = hero.Y + 22 + c.F.Big.Height + 8;
        foreach (string line in c.F.Ui.Wrap(blurb, hero.W * 0.5f))
        {
            c.F.Ui.Draw(c.R, line, hero.X + 32, by, Color.Rgb(0x2A4055));
            by += c.F.Ui.Height + 3;
        }

        // Download button.
        var dl = new Rect(hero.X + 32, by + 14, 210, 42);
        c.R.RoundedRectV(dl, 5, Color.Rgb(0x7EC64B), Color.Rgb(0x3F8A1E), Color.Rgb(0x2E6A14), 1);
        Icons.Draw(c.R, IconId.Firefox, new Rect(dl.X + 6, dl.CenterY - 15, 30, 30));
        c.F.UiBold.Draw(c.R, "Firefox 3.6", dl.X + 42, dl.Y + 6, Color.White);
        c.F.Small.Draw(c.R, L.T("browser.free_download"), dl.X + 42,
                       dl.Y + 8 + c.F.UiBold.Height, Color.Rgba(0xFFFFFF, 220));
        if (c.Clicked(dl))
            Shell.MessageBox(c, L.T("browser.download"),
                L.T("browser.no_download_required_the_browser_already_shi"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

        // Fox mark on the right of the hero.
        Icons.Draw(c.R, IconId.Firefox, new Rect(hero.Right - 190, hero.Y + 30, 140, 140));

        var section = new Rect(page.X + 24, hero.Bottom + 20, page.W - 48, 150);
        c.R.FillRect(section, Color.Rgb(0xF6F8FA));
        c.R.DrawRect(section, Color.Rgb(0xD8DEE4));
        c.F.Big.Draw(c.R, L.T("browser.what_makes_firefox_the_best"),
                     section.X + 16, section.Y + 12, Color.Rgb(0x143A5E));

        string[] tags =
        {
            L.T("browser.flexibility"), L.T("browser.security"),
            L.T("browser.performance"), L.T("browser.features"),
        };
        float tx = section.X + 18;
        float ty = section.Y + 18 + c.F.Big.Height;
        foreach (string tag in tags)
        {
            c.F.Ui.Draw(c.R, tag, tx, ty, Color.Rgb(0x2255AA));
            c.R.FillRect(new Rect(tx, ty + c.F.Ui.Ascent + 2, c.F.Ui.Measure(tag), 1), Color.Rgb(0x2255AA));
            tx += c.F.Ui.Measure(tag) + 22;
        }

        c.F.Ui.Draw(c.R, L.T("browser.a_personal_browser_for_everyone"),
                    section.X + 18, ty + 30, Color.Rgb(0x3A4A58));

        return section.Bottom + 40 - page.Y + Cur.Scroll;
    }

    float DrawGoogleHome(UiContext c, Rect page)
    {
        float y = page.Y - Cur.Scroll;
        DrawGoogleWordmark(c, page.CenterX, y + 90, 2f);

        var box = new Rect(page.CenterX - 190, y + 150, 380, 26);
        c.R.FillRect(box, Color.White);
        c.R.DrawRect(box, Color.Rgb(0xA0A0A0));
        c.F.Ui.Draw(c.R, _searchText.Length > 0 ? _searchText : L.T("browser.search_google"),
                    box.X + 6, box.CenterY - c.F.Ui.Height * 0.5f,
                    _searchText.Length > 0 ? Color.Black : Color.Rgb(0xA0A0A0));
        if (c.Clicked(box)) c.Focus = Id + ".search";

        if (W.Button(c, Id + ".g.search", new Rect(page.CenterX - 110, box.Bottom + 14, 100, 24),
                     L.T("browser.search_2")))
            Navigate("search:" + (_searchText.Length > 0 ? _searchText : "скачать интернет"), c);
        if (W.Button(c, Id + ".g.lucky", new Rect(page.CenterX + 10, box.Bottom + 14, 140, 24),
                     L.T("browser.i_m_feeling_lucky")))
            Navigate("http://miminus-os.ru/", c);

        return box.Bottom + 80 - page.Y + Cur.Scroll;
    }

    static void DrawGoogleWordmark(UiContext c, float cx, float y, float scale)
    {
        // Letters coloured the way the logo is, drawn with the display font.
        (string ch, Color col)[] letters =
        {
            ("G", Color.Rgb(0x4285F4)), ("o", Color.Rgb(0xEA4335)), ("o", Color.Rgb(0xFBBC05)),
            ("g", Color.Rgb(0x4285F4)), ("l", Color.Rgb(0x34A853)), ("e", Color.Rgb(0xEA4335)),
        };
        var f = c.F.Huge;
        float total = letters.Sum(l => f.Measure(l.ch));
        float x = cx - total * 0.5f;
        foreach (var (ch, col) in letters)
        {
            f.Draw(c.R, ch, x, y, col);
            x += f.Measure(ch);
        }
    }

    /// <summary>The «скачать интернет» results page from part 3.</summary>
    float DrawSearchResults(UiContext c, Rect page, string query)
    {
        float y = page.Y - Cur.Scroll;

        var head = new Rect(page.X, y, page.W, 54);
        c.R.FillRect(head, Color.White);
        DrawGoogleWordmark(c, head.X + 70, head.Y + 6, 0.6f);

        var box = new Rect(head.X + 160, head.Y + 14, 300, 24);
        c.R.FillRect(box, Color.White);
        c.R.DrawRect(box, Color.Rgb(0xA0A0A0));
        c.F.Ui.Draw(c.R, query, box.X + 6, box.CenterY - c.F.Ui.Height * 0.5f, Color.Black);
        W.Button(c, Id + ".re", new Rect(box.Right + 8, box.Y, 70, 24), L.T("browser.search_2"));

        c.R.FillRect(new Rect(page.X, head.Bottom, page.W, 1), Color.Rgb(0xD8D8D8));

        c.F.Small.Draw(c.R,
            L.T("browser.about_282_000_results_0_34_seconds"),
            page.X + 160, head.Bottom + 6, Color.Rgb(0x707070));

        (string title, string url, string snippet)[] results =
        {
            (L.T("browser.download_the_internet_download_master_icq_ex"),
             "www.download-master.ru/internet/",
             L.T("browser.download_the_internet_for_free_fast_no_regis")),

            (L.T("browser.internet_explorer_8_download_internet_explor"),
             "www.microsoft.com/rus/ie8/",
             L.T("browser.this_is_how_you_download_internet_explorer_i")),

            (L.T("browser.internet_russia_free_download"),
             "internet-rus.ru/download/",
             L.T("browser.strangely_enough_download_internet_explorer")),

            (L.T("browser.windows_internet_explorer_8_ie_8_review"),
             "ie8.softportal.ru/",
             L.T("browser.all_you_need_is_to_download_the_internet_and")),

            (L.T("browser.miminus_os_the_internet_is_already_inside"),
             "miminus-os.ru/",
             L.T("browser.no_need_to_download_the_internet_it_ships_wi")),
        };

        float ry = head.Bottom + 26;
        foreach (var (title, url, snippet) in results)
        {
            var titleRect = new Rect(page.X + 160, ry, page.W - 190, c.F.Ui.Height + 2);
            bool hover = c.Hovering(titleRect);
            string shown = c.F.Ui.Ellipsize(title, titleRect.W);
            c.F.Ui.Draw(c.R, shown, titleRect.X, titleRect.Y, Color.Rgb(0x1122CC));
            if (hover)
                c.R.FillRect(new Rect(titleRect.X, titleRect.Y + c.F.Ui.Ascent + 2,
                                      c.F.Ui.Measure(shown), 1), Color.Rgb(0x1122CC));
            if (c.Clicked(titleRect))
                Navigate(url.Contains("miminus") ? "http://miminus-os.ru/" : "http://" + url, c);

            ry += c.F.Ui.Height + 3;
            foreach (string line in c.F.Small.Wrap(snippet, page.W - 200).Take(2))
            {
                c.F.Small.Draw(c.R, line, page.X + 160, ry, Color.Rgb(0x303030));
                ry += c.F.Small.Height + 1;
            }
            c.F.Small.Draw(c.R, url, page.X + 160, ry, Color.Rgb(0x1A7F37));
            ry += c.F.Small.Height + 22;
        }

        return ry + 30 - page.Y + Cur.Scroll;
    }

    float DrawMiminusSite(UiContext c, Rect page)
    {
        float y = page.Y - Cur.Scroll;

        var banner = new Rect(page.X, y, page.W, 130);
        c.R.FillRect(banner, Color.Rgb(0xFFD200));
        float tw = c.F.Huge.Measure(L.T("browser.miminus_os"));
        c.F.Huge.Draw(c.R, L.T("browser.miminus_os"), banner.CenterX - tw * 0.5f, banner.Y + 14, Color.Black);
        string sub = L.T("browser.our_answer_to_bolgenos");
        c.F.Ui.Draw(c.R, sub, banner.CenterX - c.F.Ui.Measure(sub) * 0.5f, banner.Y + 18 + c.F.Huge.Height,
                    Color.Rgb(0x604800));

        float ry = banner.Bottom + 24;
        string[] bullets =
        {
            "site.entirely_our_own_development",
            "site.our_own_browser_antivirus_and_image_editor",
            "site.runs_faster_because_it_is_written_from_scrat",
            "site.the_internet_is_already_inside_no_download",
            "site.bolgenos_has_been_defeated",
        };
        foreach (string bullet in bullets)
        {
            c.R.FillCircle(page.X + 40, ry + c.F.Ui.Height * 0.5f, 3.5f, Color.Rgb(0xE0A020));
            c.F.Ui.Draw(c.R, L.T(bullet), page.X + 54, ry, Color.Rgb(0x202020));
            ry += c.F.Ui.Height + 12;
        }

        var dl = new Rect(page.X + 40, ry + 12, 220, 40);
        c.R.RoundedRectV(dl, 5, Color.Rgb(0xF0C020), Color.Rgb(0xC08000), Color.Rgb(0x806000), 1);
        c.F.UiBold.DrawCentered(c.R, L.T("browser.download_miminus_os"), dl, Color.Black);
        if (c.Clicked(dl))
            Shell.MessageBox(c, L.T("browser.download"),
                L.T("browser.you_are_already_running_miminus_os"),
                MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);

        return dl.Bottom + 40 - page.Y + Cur.Scroll;
    }

    float DrawNotFound(UiContext c, Rect page)
    {
        float y = page.Y + 60;
        Icons.Draw(c.R, IconId.DlgWarning, new Rect(page.X + 40, y, 48, 48));
        c.F.Big.Draw(c.R, L.T("browser.server_not_found"), page.X + 100, y, Color.Rgb(0x303030));

        foreach (string line in c.F.Ui.Wrap(
            L.F("browser.firefox_can_t_find_the_server_at_0_network_c", Cur.Url),
            page.W - 160))
        {
            y += c.F.Ui.Height + 4;
            c.F.Ui.Draw(c.R, line, page.X + 100, y + 24, Color.Rgb(0x404040));
        }

        if (W.Button(c, Id + ".retry", new Rect(page.X + 100, y + 70, 130, 26),
                     L.T("browser.try_again")))
            Navigate("about:home", c);

        return page.H;
    }
}
