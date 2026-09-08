using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Центр обновления МИМИНУС» — a page of the Control Panel rather
/// than a window of its own.
///
/// It was a window with a coloured banner across the top and a row of buttons
/// along the bottom, which is how the XP-era applet looked. Seven moved it into
/// the Control Panel frame and gave it the shape it has here: a heading, one
/// large panel with a shield in it and a single big button on the right, and a
/// short list of facts underneath — when it last looked, what is installed, and
/// where the answer comes from.
///
/// The version on offer is still not compiled in. It comes from a text file in
/// the project's repository, so the page is really a reader for whatever that
/// file currently says.</summary>
public sealed partial class ControlPanelWindow
{
    /// <summary>Opening the page is itself a request to check, unless the
    /// shell's startup check already has an answer. It happens once per window
    /// rather than once per visit, so walking back and forth over the trail
    /// does not hammer the repository.</summary>
    bool _checkedOnOpen;

    void DrawUpdatePage(UiContext c, Rect area)
    {
        var updates = Shell.Updates;

        if (!_checkedOnOpen && updates.State == UpdateState.Idle)
        {
            _checkedOnOpen = true;
            updates.BeginCheck();
        }

        c.F.Caption.Draw(c.R, L.T("update.title"), area.X, area.Y, Color.Rgb(0x1E4E79));
        area.CutTop(c.F.Caption.Height + 4);
        c.F.Small.Draw(c.R, L.T("update.page_note"), area.X + 1, area.Y, Color.Rgb(0x707070));
        area.CutTop(c.F.Small.Height + 14);

        // ---- the panel that says where we stand --------------------------------
        DrawUpdateState(c, area.CutTop(112), updates);
        area.CutTop(14);

        // ---- what is known about the machine ------------------------------------
        UpdateFact(c, ref area, "update.fact_last_checked",
                   updates.LastChecked is { } t ? L.Time(t) : L.T("update.never_checked"));
        UpdateFact(c, ref area, "update.fact_installed", UpdateService.InstalledVersion);
        UpdateFact(c, ref area, "update.fact_channel", UpdateService.Branch);

        area.CutTop(10);
        UpdateLink(c, area.CutTop(22), updates.Latest?.Url ?? UpdateService.RepositoryUrl);

        // ---- what is new in the version on offer --------------------------------
        area.CutTop(10);
        string notes = updates.Latest?.Notes;
        if (!string.IsNullOrEmpty(notes) && area.H > 46)
        {
            PageCaption(c, ref area, "update.notes");
            foreach (string line in notes.Split('\n'))
            {
                if (area.H < c.F.Small.Height + 4) break;
                var row = area.CutTop(c.F.Small.Height + 3);
                c.R.FillCircle(row.X + 3, row.CenterY, 2, Color.Rgb(0x909090));
                c.R.PushClip(row);
                c.F.Small.Draw(c.R, line, row.X + 12, row.Y, Color.Rgb(0x404040));
                c.R.PopClip();
            }
        }
    }

    /// <summary>The big panel: a shield, a headline, a line of detail, and one
    /// button that carries the whole cycle — check, install, restart.</summary>
    void DrawUpdateState(UiContext c, Rect panel, UpdateService updates)
    {
        c.R.FillRect(panel, Color.Rgb(0xF7F9FB));
        c.R.DrawRect(panel, Color.Rgb(0xDCE3EA));

        var inner = panel.Deflate(16, 14, 16, 12);
        var buttons = inner.CutRight(196);

        // The shield is green when there is nothing to do, yellow when there is,
        // and the warning triangle when the repository could not be reached.
        var badge = inner.CutLeft(56);
        Icons.Draw(c.R, updates.State == UpdateState.Failed ? IconId.DlgWarning : IconId.Shield,
                   new Rect(badge.X, badge.Y + 2, 44, 44));

        (string head, string detail) = updates.State switch
        {
            UpdateState.Checking => (L.T("update.checking"), updates.Source),
            UpdateState.Downloading => (L.T("update.downloading"),
                                        updates.Total > 0
                                            ? L.F("update.bytes_of", L.FileSize(updates.Fetched),
                                                  L.FileSize(updates.Total))
                                            : L.T("update.do_not_turn_off")),
            UpdateState.Verifying => (L.T("update.verifying"), L.T("update.do_not_turn_off")),
            UpdateState.Extracting => (L.T("update.extracting"), L.T("update.do_not_turn_off")),
            UpdateState.Available => (L.T("update.available"),
                                      L.F("update.available_detail",
                                          updates.Latest?.Name ?? "",
                                          updates.Latest?.Size ?? "?")),
            UpdateState.ReadyToRestart => (L.T("update.ready"),
                                           L.F("update.ready_detail", updates.Latest?.Version ?? "")),
            UpdateState.Failed => (L.T("update.failed"),
                                   string.IsNullOrEmpty(updates.Error)
                                       ? L.T("update.failed_detail") : updates.Error),
            UpdateState.UpToDate => (L.T("update.up_to_date"),
                                     L.F("update.up_to_date_detail", UpdateService.InstalledVersion)),
            _ => (L.T("update.idle"), L.T("update.idle_hint")),
        };

        inner.CutLeft(8);
        c.F.Caption.Draw(c.R, c.F.Caption.Ellipsize(head, inner.W), inner.X, inner.Y + 2,
                         Color.Rgb(0x1E4E79));

        var body = inner;
        body.CutTop(c.F.Caption.Height + 6);
        foreach (string line in c.F.Small.Wrap(detail, body.W).Take(2))
        {
            var row = body.CutTop(c.F.Small.Height + 2);
            c.F.Small.Draw(c.R, line, row.X, row.Y, Color.Rgb(0x606060));
        }

        // While something is happening the bar underneath says how far along it is.
        if (updates.Busy)
            W.ProgressBar(c, new Rect(inner.X, panel.Bottom - 26, inner.W, 14), updates.Progress);

        // ---- the buttons ------------------------------------------------------
        var big = new Rect(buttons.Right - 186, buttons.Y + 4, 186, 28);
        switch (updates.State)
        {
            case UpdateState.ReadyToRestart:
                if (W.Button(c, Id + ".restart", big, L.T("update.restart_now"), true,
                             IconId.Shutdown, defaultButton: true))
                    Shell.RestartForUpdate(c);
                break;

            case UpdateState.Available:
                if (W.Button(c, Id + ".install", big, L.T("update.install"), true,
                             IconId.Shield, defaultButton: true))
                    ConfirmUpdate(c, updates.Latest);
                break;

            default:
                if (W.Button(c, Id + ".check", big, L.T("update.check_now"), !updates.Busy,
                             IconId.Shield, defaultButton: !updates.Busy))
                    updates.BeginCheck();
                break;
        }

        // The second button is the one the state does not need, kept where the
        // real page kept it: a way to look again without leaving the page.
        if (updates.State is UpdateState.Available or UpdateState.ReadyToRestart)
        {
            var again = new Rect(big.X, big.Bottom + 8, big.W, 26);
            if (W.Button(c, Id + ".recheck", again, L.T("update.check_now"), !updates.Busy))
                updates.BeginCheck();
        }
    }

    /// <summary>Downloading replaces the running program, so it is asked for
    /// rather than assumed.</summary>
    void ConfirmUpdate(UiContext c, UpdateInfo info)
    {
        Shell.MessageBox(c, L.T("update.title"),
            L.F("update.confirm_install", info?.Name ?? "", info?.Size ?? "?"),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion,
            r => { if (r == MsgResult.Yes) Shell.Updates.BeginDownload(); },
            Sfx.Question);
    }

    void UpdateFact(UiContext c, ref Rect area, string key, string value)
    {
        if (area.H < 22) return;
        var row = area.CutTop(22);
        c.F.Ui.Draw(c.R, L.T(key), row.X, row.Y + 2, c.Theme.TextDisabled);
        c.R.PushClip(row);
        c.F.Ui.Draw(c.R, string.IsNullOrEmpty(value) ? "—" : value, row.X + 210, row.Y + 2,
                    c.Theme.Text);
        c.R.PopClip();
    }

    /// <summary>The repository address, drawn the way a hyperlink looked then:
    /// blue, underlined, and clickable.</summary>
    void UpdateLink(UiContext c, Rect row, string url)
    {
        if (string.IsNullOrEmpty(url)) return;

        float w = MathF.Min(c.F.Ui.Measure(url), row.W);
        var hit = new Rect(row.X, row.CenterY - c.F.Ui.Height * 0.5f, w, c.F.Ui.Height + 2);
        bool hover = c.Hovering(hit);

        var colour = hover ? c.Theme.Accent : Color.Rgb(0x1A3DA0);
        c.R.PushClip(row);
        c.F.Ui.Draw(c.R, url, hit.X, hit.Y, colour);
        c.R.PopClip();
        c.R.FillRect(new Rect(hit.X, hit.Y + c.F.Ui.Height, w, 1), colour);

        if (hover) c.Cursor = CursorShape.Hand;
        if (c.Clicked(hit))
        {
            Clipboard.SetText(url);
            Shell.Launch(c, "browser", null);
            c.Sound(Sfx.Click, 0.7f);
        }
    }
}
