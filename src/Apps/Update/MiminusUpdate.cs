using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Центр обновления МИМИНУС» — the update centre.
///
/// Laid out like the XP-era Windows Update applet: a coloured banner, a shield,
/// and one panel that changes with the state of the check. The version on offer
/// is not compiled in — it comes from a text file in the project's repository,
/// so the window is really a reader for whatever that file currently says.</summary>
public sealed class UpdateWindow : OsWindow
{
    readonly ShellHost _shell;
    bool _checkedOnOpen;

    public override string Title => L.T("update.title");
    public override float MinWidth => 460;
    public override float MinHeight => 380;

    public UpdateWindow(ShellHost shell)
    {
        _shell = shell;
        Icon = IconId.Shield;
        Bounds = new Rect(0, 0, 580, 470);
    }

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

        // Opening the centre is itself a request to check, unless the shell's
        // startup check already has an answer.
        if (!_checkedOnOpen && _shell.Updates.State == UpdateState.Idle)
        {
            _checkedOnOpen = true;
            _shell.Updates.BeginCheck();
        }
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        var updates = _shell.Updates;

        c.R.FillRect(client, t.Face);

        DrawBanner(c, client.CutTop(72));

        var area = client.Deflate(16);
        var footer = area.CutBottom(30);

        switch (updates.State)
        {
            case UpdateState.Checking: DrawChecking(c, area); break;
            case UpdateState.Available: DrawAvailable(c, area, updates.Latest); break;
            case UpdateState.UpToDate: DrawUpToDate(c, area, updates.Latest); break;
            case UpdateState.Downloading:
            case UpdateState.Verifying:
            case UpdateState.Extracting: DrawInstalling(c, area, updates); break;
            case UpdateState.ReadyToRestart: DrawReady(c, area, updates.Latest); break;
            case UpdateState.Failed: DrawFailed(c, area, updates); break;
            default: DrawIdle(c, area); break;
        }

        DrawFooter(c, footer, updates);
    }

    // ---- banner ----------------------------------------------------------

    void DrawBanner(UiContext c, Rect banner)
    {
        var t = c.Theme;
        c.R.FillRectV(banner, t.CaptionActiveTop, t.CaptionActiveBottom);
        c.R.FillRect(new Rect(banner.X, banner.Bottom - 1, banner.W, 1), t.Shadow);

        Icons.Draw(c.R, IconId.Shield, new Rect(banner.X + 12, banner.Y + 16, 36, 36));

        // The title is long in Russian and the window is resizable, so the
        // heading steps down a size rather than running off the banner.
        string title = L.T("update.title");
        float room = banner.W - 70;
        var heading = c.F.Big.Measure(title) <= room ? c.F.Big
                    : c.F.Caption.Measure(title) <= room ? c.F.Caption
                    : c.F.UiBold;

        heading.Draw(c.R, title, banner.X + 58, banner.Y + 12, t.CaptionTextActive);
        c.F.Small.Draw(c.R, L.F("update.installed_version", UpdateService.InstalledVersion),
                       banner.X + 58, banner.Y + 16 + heading.Height, t.CaptionTextActive);
    }

    // ---- the four states -------------------------------------------------

    void DrawIdle(UiContext c, Rect area)
    {
        Paragraph(c, area, L.T("update.idle_hint"));
    }

    void DrawChecking(UiContext c, Rect area)
    {
        var t = c.Theme;
        c.F.UiBold.Draw(c.R, L.T("update.checking"), area.X, area.Y, t.Text);

        float y = area.Y + c.F.UiBold.Height + 10;
        W.ProgressBar(c, new Rect(area.X, y, area.W, 18), _shell.Updates.Progress);

        c.F.Small.Draw(c.R, Truncate(c, _shell.Updates.Source, area.W), area.X, y + 26, t.TextDisabled);
    }

    void DrawAvailable(UiContext c, Rect area, UpdateInfo info)
    {
        var t = c.Theme;
        c.F.UiBold.Draw(c.R, L.T("update.available"), area.X, area.Y, t.Text);
        area.CutTop(c.F.UiBold.Height + 8);

        var box = area.CutTop(MathF.Min(146, area.H - 8));
        W.GroupBox(c, box, L.T("update.available_group"));
        var inner = box.Deflate(12);
        inner.CutTop(10);

        Field(c, ref inner, L.T("update.field_name"), info?.Name);
        Field(c, ref inner, L.T("update.field_version"), info?.Version);
        Field(c, ref inner, L.T("update.field_released"), info?.Released);
        Field(c, ref inner, L.T("update.field_size"), info?.Size);

        area.CutTop(10);
        Link(c, area.CutTop(20), info?.Url);
        area.CutTop(6);
        Notes(c, area, info?.Notes);
    }

    void DrawInstalling(UiContext c, Rect area, UpdateService updates)
    {
        var t = c.Theme;
        string headline = updates.State switch
        {
            UpdateState.Verifying => L.T("update.verifying"),
            UpdateState.Extracting => L.T("update.extracting"),
            _ => L.T("update.downloading"),
        };
        c.F.UiBold.Draw(c.R, headline, area.X, area.Y, t.Text);

        float y = area.Y + c.F.UiBold.Height + 10;
        W.ProgressBar(c, new Rect(area.X, y, area.W, 18), updates.Progress);

        // Bytes are only known while the package is arriving.
        if (updates.State == UpdateState.Downloading && updates.Total > 0)
            c.F.Small.Draw(c.R, L.F("update.bytes_of", L.FileSize(updates.Fetched),
                                    L.FileSize(updates.Total)),
                           area.X, y + 26, t.TextDisabled);

        c.F.Small.Draw(c.R, L.T("update.do_not_turn_off"), area.X, y + 46, t.TextDisabled);
    }

    void DrawReady(UiContext c, Rect area, UpdateInfo info)
    {
        var t = c.Theme;
        Icons.Draw(c.R, IconId.Shield, new Rect(area.X, area.Y, 32, 32));
        c.F.UiBold.Draw(c.R, L.T("update.ready"), area.X + 42, area.Y + 2, t.Text);
        c.F.Ui.Draw(c.R, L.F("update.ready_detail", info?.Version ?? ""),
                    area.X + 42, area.Y + 4 + c.F.UiBold.Height, t.TextDisabled);
        area.CutTop(52);

        c.F.Ui.Draw(c.R, L.T("update.restart_explains"), area.X, area.Y, t.Text);
        area.CutTop(c.F.Ui.Height + 10);
        Notes(c, area, info?.Notes);
    }

    void DrawUpToDate(UiContext c, Rect area, UpdateInfo info)
    {
        var t = c.Theme;
        Icons.Draw(c.R, IconId.Shield, new Rect(area.X, area.Y, 32, 32));
        c.F.UiBold.Draw(c.R, L.T("update.up_to_date"), area.X + 42, area.Y + 2, t.Text);
        c.F.Ui.Draw(c.R, L.F("update.up_to_date_detail", UpdateService.InstalledVersion),
                    area.X + 42, area.Y + 4 + c.F.UiBold.Height, t.TextDisabled);
        area.CutTop(52);

        Link(c, area.CutTop(20), info?.Url ?? UpdateService.RepositoryUrl);
        area.CutTop(6);
        Notes(c, area, info?.Notes);
    }

    void DrawFailed(UiContext c, Rect area, UpdateService updates)
    {
        var t = c.Theme;
        Icons.Draw(c.R, IconId.DlgWarning, new Rect(area.X, area.Y, 32, 32));
        c.F.UiBold.Draw(c.R, L.T("update.failed"), area.X + 42, area.Y + 2, t.Text);

        // The joke from part 1 is that the network is always "checked" and never
        // connected; the real error is kept below it so the window is still of
        // some use when the machine genuinely is offline.
        c.F.Ui.Draw(c.R, L.T("update.failed_detail"), area.X + 42,
                    area.Y + 4 + c.F.UiBold.Height, t.TextDisabled);
        area.CutTop(52);

        if (!string.IsNullOrEmpty(updates.Error))
        {
            var msg = area.CutTop(34);
            W.SunkenField(c, msg);
            c.F.Small.Draw(c.R, Truncate(c, updates.Error, msg.W - 12),
                           msg.X + 6, msg.Y + 8, t.TextDisabled);
            area.CutTop(8);
        }

        if (updates.Latest != null && updates.Latest.Local)
        {
            c.F.Small.Draw(c.R, L.T("update.local_copy"), area.X, area.Y, t.TextDisabled);
            area.CutTop(c.F.Small.Height + 6);
            Field(c, ref area, L.T("update.field_name"), updates.Latest.Name);
            Field(c, ref area, L.T("update.field_version"), updates.Latest.Version);
            area.CutTop(6);
        }

        Link(c, area.CutTop(20), updates.Latest?.Url ?? UpdateService.RepositoryUrl);
    }

    // ---- footer ----------------------------------------------------------

    void DrawFooter(UiContext c, Rect footer, UpdateService updates)
    {
        var t = c.Theme;

        if (updates.LastChecked != null)
            c.F.Small.Draw(c.R, L.F("update.last_checked", L.Time(updates.LastChecked.Value)),
                           footer.X, footer.Y + 8, t.TextDisabled);

        var close = new Rect(footer.Right - 84, footer.Y, 84, 24);
        if (W.Button(c, Id + ".close", close, L.T("update.close"))) Close();

        var check = new Rect(close.X - 122 - 8, footer.Y, 122, 24);
        if (W.Button(c, Id + ".check", check, L.T("update.check_now"), enabled: !updates.Busy))
            updates.BeginCheck();

        // One button carries the cycle: install, then restart into it.
        var action = new Rect(check.X - 170 - 8, footer.Y, 170, 24);
        switch (updates.State)
        {
            case UpdateState.ReadyToRestart:
                if (W.Button(c, Id + ".restart", action, L.T("update.restart_now"),
                             defaultButton: true))
                    _shell.RestartForUpdate(c);
                break;

            case UpdateState.Available:
                if (W.Button(c, Id + ".install", action, L.T("update.install"),
                             defaultButton: true))
                    Confirm(c, updates.Latest);
                break;

            default:
                W.Button(c, Id + ".install", action, L.T("update.install"), enabled: false);
                break;
        }
    }

    /// <summary>Downloading replaces the running program, so it is asked for
    /// rather than assumed.</summary>
    void Confirm(UiContext c, UpdateInfo info)
    {
        _shell.MessageBox(c, L.T("update.title"),
            L.F("update.confirm_install", info?.Name ?? "", info?.Size ?? "?"),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion,
            r => { if (r == MsgResult.Yes) _shell.Updates.BeginDownload(); },
            Sfx.Question);
    }

    // ---- small drawing helpers -------------------------------------------

    void Field(UiContext c, ref Rect area, string label, string value)
    {
        if (area.H < 20) return;
        var row = area.CutTop(20);
        c.F.Ui.Draw(c.R, label, row.X, row.Y + 2, c.Theme.TextDisabled);
        c.F.Ui.Draw(c.R, string.IsNullOrEmpty(value) ? "—" : value,
                    row.X + 132, row.Y + 2, c.Theme.Text);
    }

    /// <summary>Draws the repository address the way a hyperlink looked then:
    /// blue, underlined, and clickable.</summary>
    void Link(UiContext c, Rect row, string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        float w = c.F.Ui.Measure(url);
        var hit = new Rect(row.X, row.Y, MathF.Min(w, row.W), c.F.Ui.Height + 2);
        bool hover = c.Hovering(hit);

        var colour = Color.Rgb(0x1A3DA0);
        c.F.Ui.Draw(c.R, url, hit.X, hit.Y, colour);
        c.R.FillRect(new Rect(hit.X, hit.Y + c.F.Ui.Height, MathF.Min(w, row.W), 1), colour);

        if (hover) c.Cursor = CursorShape.Hand;
        if (c.Clicked(hit))
        {
            Clipboard.SetText(url);
            _shell.Launch(c, "browser", null);
            c.Sound(Sfx.Click, 0.7f);
        }
    }

    void Notes(UiContext c, Rect area, string notes)
    {
        if (string.IsNullOrEmpty(notes) || area.H < 40) return;

        W.GroupBox(c, area, L.T("update.notes"));
        var inner = area.Deflate(12);
        inner.CutTop(10);

        foreach (string line in notes.Split('\n'))
        {
            if (inner.H < c.F.Small.Height) break;
            var row = inner.CutTop(c.F.Small.Height + 3);
            c.F.Small.Draw(c.R, "• " + Truncate(c, line, row.W - 8), row.X, row.Y, c.Theme.Text);
        }
    }

    void Paragraph(UiContext c, Rect area, string text)
        => c.F.Ui.Draw(c.R, text, area.X, area.Y, c.Theme.Text);

    static string Truncate(UiContext c, string text, float width)
    {
        if (text == null) return "";
        if (c.F.Small.Measure(text) <= width) return text;

        while (text.Length > 1 && c.F.Small.Measure(text + "…") > width)
            text = text[..^1];
        return text + "…";
    }
}
