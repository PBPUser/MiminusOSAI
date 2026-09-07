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
/// speed-dial grid.</summary>
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

    public override string Title
        => (Cur.Title.Length > 0 ? Cur.Title + " — " : "") + BrandName;

    public override float MinWidth => 560;
    public override float MinHeight => 380;

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
        BuildMenu();
    }

    void BuildMenu()
    {
        Menu = new MenuBar();
        Menu.Add(L.T("browser.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("browser.new_tab"), () => NewTab(_ctx), shortcut: "Ctrl+T"),
            MenuItem.Of(L.T("browser.close_tab"), () => CloseTab(_active, _ctx),
                        enabled: _tabs.Count > 1, shortcut: "Ctrl+W"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("browser.exit"), Close),
        });
        Menu.Add(L.T("browser.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("browser.copy_address"), () => Clipboard.SetText(Cur.Url)),
            MenuItem.Of(L.T("browser.paste_and_go"), () =>
                Navigate(Clipboard.GetText().Trim(), _ctx)),
        });
        Menu.Add(L.T("browser.view"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("browser.reload"), () => { Cur.Scroll = 0; _ctx.Sound(Sfx.Navigate, 0.5f); }, shortcut: "F5"),
            MenuItem.Of(L.T("browser.home"), () => Navigate("about:home", _ctx), shortcut: "Alt+Home"),
        });
        Menu.Add(L.T("browser.bookmarks"), () => new List<MenuItem>
        {
            MenuItem.Of("mozilla-europe.org", () => Navigate("http://www.mozilla-europe.org/ru/firefox/", _ctx), IconId.Firefox),
            MenuItem.Of("google.ru", () => Navigate("http://www.google.ru/", _ctx), IconId.Globe),
            MenuItem.Of(L.T("browser.sodly"), () => Navigate("http://www.sodly.google.com/", _ctx), IconId.Globe),
            MenuItem.Of(L.T("browser.speed_dial"), () => Navigate("about:home", _ctx), IconId.Star),
            MenuItem.Of("miminus-os.ru", () => Navigate("http://miminus-os.ru/", _ctx), IconId.Star),
        });
        Menu.Add(L.T("browser.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("browser.about"), () =>
                Shell.MessageBox(_ctx, BrandName,
                    L.T(_brand == BrowserBrand.Orega
                        ? "browser.firefox_web_browser_3_6_part_of_miminus_os_r"
                        : "browser.figefoch_about"),
                    MsgButtons.Ok, Icon, null, Sfx.Info), IconId.DlgInfo),
        });
    }

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
        DrawTabStrip(c, area.CutTop(26));
        DrawNavBar(c, area.CutTop(30));
        var status = area.CutBottom(20);

        c.R.FillRect(area, Color.White);
        c.R.PushClip(area);
        DrawPage(c, area);
        c.R.PopClip();
        c.R.DrawRect(area, c.Theme.ControlBorder);

        W.StatusBar(c, status, L.T("browser.done"), "", L.T("browser.no_connection_required"));

        if (!c.KeyboardHandled && c.In.Ctrl)
        {
            if (c.In.KeyPressed(Keys.T)) { NewTab(c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.W)) { CloseTab(_active, c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.L)) { c.Focus = Id + ".addr"; c.KeyboardHandled = true; }
        }
    }

    void DrawTabStrip(UiContext c, Rect strip)
    {
        var t = c.Theme;
        c.R.FillRectV(strip, Color.Rgb(0xD8DCE4), Color.Rgb(0xB8BEC8));

        float x = strip.X + 2;
        float tabW = MathF.Min(190, (strip.W - 40) / Math.Max(1, _tabs.Count));

        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            bool sel = i == _active;
            var r = new Rect(x, strip.Y + (sel ? 2 : 4), tabW, strip.H - (sel ? 2 : 4));

            c.R.RoundedRectV(r, 4, sel ? Color.White : Color.Rgb(0xE6E9EE),
                             sel ? Color.White : Color.Rgb(0xCED3DB), Color.Rgb(0x9AA2AE), 1);
            if (sel) c.R.FillRect(new Rect(r.X + 1, r.Bottom - 3, r.W - 2, 3), Color.White);

            Icons.Draw(c.R, IconId.Globe, new Rect(r.X + 5, r.CenterY - 7, 14, 14));
            c.R.PushClip(new Rect(r.X + 22, r.Y, r.W - 42, r.H));
            c.F.Small.Draw(c.R, tab.Title.Length > 0 ? tab.Title : L.T("browser.new_tab"),
                           r.X + 22, r.CenterY - c.F.Small.Height * 0.5f, t.Text);
            c.R.PopClip();

            var close = new Rect(r.Right - 18, r.CenterY - 7, 14, 14);
            if (c.Hovering(close)) c.R.RoundedRect(close, 2, Color.Rgba(0xD04040, 200));
            c.R.Line(close.X + 4, close.Y + 4, close.Right - 4, close.Bottom - 4,
                     c.Hovering(close) ? Color.White : Color.Rgb(0x606060), 1.5f);
            c.R.Line(close.Right - 4, close.Y + 4, close.X + 4, close.Bottom - 4,
                     c.Hovering(close) ? Color.White : Color.Rgb(0x606060), 1.5f);

            if (c.Clicked(close)) { CloseTab(i, c); return; }
            else if (c.Clicked(r)) { _active = i; _addressText = tab.Url; c.SoundAt(Sfx.Tick, r, 0.3f); }

            x += tabW + 2;
        }

        var plus = new Rect(x + 2, strip.Y + 5, 20, strip.H - 8);
        if (c.Hovering(plus)) c.R.RoundedRect(plus, 3, Color.Rgba(0xFFFFFF, 160));
        c.R.FillRect(new Rect(plus.CenterX - 5, plus.CenterY - 1, 10, 2), Color.Rgb(0x404040));
        c.R.FillRect(new Rect(plus.CenterX - 1, plus.CenterY - 5, 2, 10), Color.Rgb(0x404040));
        c.Tooltip(plus, L.T("browser.new_tab"));
        if (c.Clicked(plus)) NewTab(c);
    }

    void DrawNavBar(UiContext c, Rect bar)
    {
        var t = c.Theme;
        W.ToolbarBackground(c, bar);
        var tab = Cur;

        float x = bar.X + 4;
        float bs = bar.H - 8;

        if (NavButton(c, ".back", new Rect(x, bar.Y + 4, bs, bs), 3, tab.Back.Count > 0))
        {
            tab.Forward.Add(tab.Url);
            string url = tab.Back[^1];
            tab.Back.RemoveAt(tab.Back.Count - 1);
            tab.Url = url; tab.Title = TitleFor(url); tab.Scroll = 0; _addressText = url;
            c.Sound(Sfx.Navigate, 0.45f);
        }
        x += bs + 3;

        if (NavButton(c, ".fwd", new Rect(x, bar.Y + 4, bs, bs), 1, tab.Forward.Count > 0))
        {
            tab.Back.Add(tab.Url);
            string url = tab.Forward[^1];
            tab.Forward.RemoveAt(tab.Forward.Count - 1);
            tab.Url = url; tab.Title = TitleFor(url); tab.Scroll = 0; _addressText = url;
            c.Sound(Sfx.Navigate, 0.45f);
        }
        x += bs + 3;

        // Reload.
        var reload = new Rect(x, bar.Y + 4, bs, bs);
        if (c.Hovering(reload)) c.R.RoundedRect(reload, 3, t.Hot);
        DrawReloadGlyph(c, reload);
        if (c.Clicked(reload)) { tab.Scroll = 0; c.Sound(Sfx.Navigate, 0.5f); }
        x += bs + 3;

        var home = new Rect(x, bar.Y + 4, bs, bs);
        if (c.Hovering(home)) c.R.RoundedRect(home, 3, t.Hot);
        Icons.Draw(c.R, IconId.Star, home.Deflate(3));
        c.Tooltip(home, L.T("browser.speed_dial"));
        if (c.Clicked(home)) Navigate("about:home", c);
        x += bs + 6;

        // Address field + Go.
        var go = new Rect(bar.Right - 190, bar.Y + 4, 52, bs);
        var search = new Rect(go.X - 122, bar.Y + 4, 118, bs);
        var addr = new Rect(x, bar.Y + 4, search.X - x - 6, bs);

        _addressFocused = c.Focus == Id + ".addr";
        c.R.FillRect(addr, Color.White);
        c.R.DrawRect(addr, _addressFocused ? t.ControlBorderHot : t.FieldBorder);
        Icons.Draw(c.R, IconId.Globe, new Rect(addr.X + 3, addr.CenterY - 7, 14, 14));

        if (c.Hovering(addr)) c.Cursor = CursorShape.Text;
        if (c.Clicked(addr)) { c.Focus = Id + ".addr"; _addressText = tab.Url; }

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

        string shown = _addressFocused ? _addressText : tab.Url;
        c.R.PushClip(new Rect(addr.X + 20, addr.Y, addr.W - 24, addr.H));
        c.F.Ui.Draw(c.R, shown, addr.X + 20, addr.CenterY - c.F.Ui.Height * 0.5f, t.Text);
        if (_addressFocused && (c.Time % 1.06) < 0.53)
            c.R.FillRect(new Rect(addr.X + 20 + c.F.Ui.Measure(shown), addr.Y + 4, 1.4f, addr.H - 8), t.Text);
        c.R.PopClip();

        // Search box.
        c.R.FillRect(search, Color.White);
        c.R.DrawRect(search, c.Focus == Id + ".search" ? t.ControlBorderHot : t.FieldBorder);
        Icons.Draw(c.R, IconId.Search, new Rect(search.X + 3, search.CenterY - 7, 14, 14));
        if (c.Hovering(search)) c.Cursor = CursorShape.Text;
        if (c.Clicked(search)) c.Focus = Id + ".search";

        if (c.Focus == Id + ".search" && !c.KeyboardHandled)
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
        c.R.PushClip(new Rect(search.X + 20, search.Y, search.W - 24, search.H));
        c.F.Ui.Draw(c.R, _searchText.Length > 0 ? _searchText : L.T("browser.search"),
                    search.X + 20, search.CenterY - c.F.Ui.Height * 0.5f,
                    _searchText.Length > 0 ? t.Text : t.TextDisabled);
        c.R.PopClip();

        if (W.Button(c, Id + ".go", go, L.T("browser.go")))
            Navigate(_addressFocused ? _addressText : tab.Url, c);
    }

    bool NavButton(UiContext c, string id, Rect r, int arrow, bool enabled)
    {
        bool hover = enabled && c.Hovering(r);
        if (hover) c.R.RoundedRect(r, 3, c.Theme.Hot);
        c.R.FillCircle(r.CenterX, r.CenterY, r.W * 0.42f,
                       enabled ? Color.Rgb(0x2E8AF5) : Color.Rgb(0xC8CCD2));
        c.R.FillCircle(r.CenterX, r.CenterY - 1.5f, r.W * 0.35f,
                       enabled ? Color.Rgb(0x7FC0FF) : Color.Rgb(0xDCDFE4));
        W.Arrow(c, r, arrow, Color.White, 4f);
        return enabled && c.Clicked(r);
    }

    static void DrawReloadGlyph(UiContext c, Rect r)
    {
        float cx = r.CenterX, cy = r.CenterY, rad = r.W * 0.3f;
        for (int i = 0; i <= 14; i++)
        {
            float a0 = 0.6f + i / 14f * 5.0f;
            float a1 = 0.6f + (i + 1) / 14f * 5.0f;
            c.R.Line(cx + MathF.Cos(a0) * rad, cy + MathF.Sin(a0) * rad,
                     cx + MathF.Cos(a1) * rad, cy + MathF.Sin(a1) * rad, Color.Rgb(0x2E7D32), 2f);
        }
        c.R.FillTriangle(cx + rad * 0.5f, cy - rad * 1.15f, cx + rad * 1.5f, cy - rad * 0.75f,
                         cx + rad * 0.55f, cy - rad * 0.2f, Color.Rgb(0x2E7D32));
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

    /// <summary>Opera's Экспресс-панель: the 3×3 thumbnail grid from part 1.</summary>
    float DrawSpeedDial(UiContext c, Rect page)
    {
        var tab = Cur;
        float y = page.Y - tab.Scroll;

        c.R.FillRect(page, Color.White);

        // Yandex-style search line at the top.
        var searchRow = new Rect(page.CenterX - 190, y + 24, 380, 26);
        c.F.UiBold.Draw(c.R, "Я", searchRow.X - 22, searchRow.CenterY - c.F.UiBold.Height * 0.5f, Color.Rgb(0xC4302B));
        c.R.FillRect(searchRow, Color.White);
        c.R.DrawRect(searchRow, Color.Rgb(0xA0A0A0));
        c.F.Ui.Draw(c.R, _searchText.Length > 0 ? _searchText : L.T("browser.search_2"),
                    searchRow.X + 6, searchRow.CenterY - c.F.Ui.Height * 0.5f,
                    _searchText.Length > 0 ? Color.Black : Color.Rgb(0xA0A0A0));
        var searchBtn = new Rect(searchRow.Right + 6, searchRow.Y, 70, searchRow.H);
        if (W.Button(c, Id + ".sd.search", searchBtn, L.T("browser.search_3")))
            Navigate("search:" + (_searchText.Length > 0 ? _searchText : "скачать интернет"), c);
        if (c.Clicked(searchRow)) c.Focus = Id + ".search";

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

        float gridTop = searchRow.Bottom + 28;
        float cellW = MathF.Min(190, (page.W - 80) / 3);
        float cellH = cellW * 0.72f;
        float gridW = cellW * 3 + 24;
        float gx = page.CenterX - gridW * 0.5f;

        for (int i = 0; i < dials.Length; i++)
        {
            var (title, url, col) = dials[i];
            var cell = new Rect(gx + (i % 3) * (cellW + 12), gridTop + (i / 3) * (cellH + 34), cellW, cellH);
            bool hover = c.Hovering(cell);

            c.R.FillRect(cell, Color.Rgb(0xF4F4F4));
            c.R.DrawRect(cell, hover ? Color.Rgb(0x3C82C8) : Color.Rgb(0xC8C8C8), hover ? 2 : 1);

            // A tiny abstract "thumbnail" per site.
            c.R.FillRect(new Rect(cell.X + 1, cell.Y + 1, cell.W - 2, cell.H * 0.24f), col);
            for (int k = 0; k < 5; k++)
                c.R.FillRect(new Rect(cell.X + 10, cell.Y + cell.H * 0.34f + k * (cell.H * 0.1f),
                                      (cell.W - 20) * (0.9f - k * 0.13f), 3), Color.Rgb(0xD0D4DA));

            // Index badge, as Opera numbered them.
            c.F.Small.Draw(c.R, (i + 1).ToString(), cell.X + 4, cell.Y + 3, Color.White);

            string label = c.F.Small.Ellipsize(title, cell.W);
            float lw = c.F.Small.Measure(label);
            c.F.Small.Draw(c.R, label, cell.CenterX - lw * 0.5f, cell.Bottom + 6, Color.Rgb(0x303030));

            if (c.Clicked(cell)) Navigate(url, c);
        }

        float bottom = gridTop + 3 * (cellH + 34) + 12;
        // "Вот написано Копирайт Михаила Гревцова" (part 1, 01:36)
        string copyright = L.T("browser.copyright_grevtsov");
        c.F.Small.Draw(c.R, copyright, page.CenterX - c.F.Small.Measure(copyright) * 0.5f,
                       gridTop - 20, Color.Rgb(0x808080));

        string hint = L.T("browser.what_is_speed_dial");
        c.F.Ui.Draw(c.R, hint, page.X + 24, bottom, Color.Rgb(0x2255AA));
        string hide = L.T("browser.hide_speed_dial");
        c.F.Ui.Draw(c.R, hide, page.Right - c.F.Ui.Measure(hide) - 30, bottom, Color.Rgb(0x2255AA));

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
