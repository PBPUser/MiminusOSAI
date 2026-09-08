using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>Блокнот — and, when it happens to have АНТИВИРУС ГРЕВЦОВА.txt open,
/// the entire antivirus product of МИМИНУС ОС.
///
/// That is the joke the reference videos are built on: the "antivirus" is a text
/// file. So the scan is implemented for real — it types its warnings into the
/// document one line at a time, beeps as it goes, and finishes by reporting how
/// many Popovs it found.</summary>
public sealed class NotepadWindow : OsWindow, IScannable
{
    readonly TextEditor _editor;
    VNode _file;
    string _untitledName;

    bool _statusBar = true;

    // Antivirus scan state.
    bool _scanning;
    int _scanStep;
    double _scanNext;
    int _foundCount;

    public override float MinWidth => 300;
    public override float MinHeight => 200;

    public override string Title
    {
        get
        {
            string name = _file?.Name ?? _untitledName;
            return $"{name} - {L.T("notepad.notepad")}";
        }
    }

    public bool IsAntivirus => _file != null &&
        (_file.Name.Contains("АНТИВИРУС", StringComparison.OrdinalIgnoreCase) ||
         _file.Name.Contains("ANTIVIRUS", StringComparison.OrdinalIgnoreCase));

    public NotepadWindow(VNode file)
    {
        _file = file is { Kind: NodeKind.TextFile } ? file : null;
        _untitledName = L.T("notepad.untitled");

        Icon = IsAntivirus ? IconId.Antivirus : IconId.Notepad;
        Bounds = new Rect(0, 0, 640, 460);

        _editor = new TextEditor(null) { WordWrap = false };
        if (_file != null) _editor.Text = _file.Text ?? "";

        BuildMenu();
    }

    /// <summary>There is exactly one thing in this window to type into, so the
    /// caret starts in the document and returns to it whenever the window is
    /// brought forward. Without this the antivirus opens with nothing listening
    /// and swallows every key until its paper is clicked on — and the antivirus
    /// is a text file, so being able to type in it is the whole product.</summary>
    bool _takeCaret = true;

    public override void OnActivated() => _takeCaret = true;

    public override void OnOpened(UiContext c)
    {
        _editor.Font = c.F.Mono;
        if (IsAntivirus) Bounds = new Rect(Bounds.X, Bounds.Y, 700, 420);
    }

    void BuildMenu()
    {
        Menu = new MenuBar();

        Menu.Add(L.T("notepad.file"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("notepad.new"), NewFile, shortcut: "Ctrl+N"),
            MenuItem.Of(L.T("notepad.open"), OpenFile, shortcut: "Ctrl+O"),
            MenuItem.Of(L.T("notepad.save"), SaveFile, shortcut: "Ctrl+S"),
            MenuItem.Of(L.T("notepad.save_as"), SaveFile),
            MenuItem.Sep(),
            MenuItem.Of(L.T("notepad.page_setup"), NotSupported),
            MenuItem.Of(L.T("notepad.print"), NotSupported, shortcut: "Ctrl+P"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("notepad.e_xit"), () => Wm.RequestClose(this, _ctx)),
        });

        Menu.Add(L.T("notepad.edit"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("notepad.undo"), () => _editor.Undo(), shortcut: "Ctrl+Z"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("notepad.cu_t"), () =>
            {
                if (_editor.HasSelection) { Clipboard.SetText(_editor.SelectedText); _editor.DeleteSelection(); _editor.MarkDirty(); }
            }, shortcut: "Ctrl+X"),
            MenuItem.Of(L.T("notepad.copy"), () =>
            {
                if (_editor.HasSelection) Clipboard.SetText(_editor.SelectedText);
            }, shortcut: "Ctrl+C"),
            MenuItem.Of(L.T("notepad.paste"), () => { _editor.Replace(Clipboard.GetText()); _editor.MarkDirty(); }, shortcut: "Ctrl+V"),
            MenuItem.Of(L.T("notepad.de_lete"), () => { _editor.DeleteSelection(); _editor.MarkDirty(); }, shortcut: "Del"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("notepad.find"), () => Wm.Open(new FindWindow(_editor), _ctx), shortcut: "Ctrl+F"),
            MenuItem.Of(L.T("notepad.replace"), () => Wm.Open(new FindWindow(_editor) { ReplaceMode = true }, _ctx), shortcut: "Ctrl+H"),
            MenuItem.Sep(),
            MenuItem.Of(L.T("notepad.select_all"), () => _editor.SelectAll(), shortcut: "Ctrl+A"),
            MenuItem.Of(L.T("notepad.time_date"), () =>
            {
                _editor.Replace(Shell.Now.ToString("HH:mm ") + L.ShortDate(Shell.Now));
                _editor.MarkDirty();
            }, shortcut: "F5"),
        });

        Menu.Add(L.T("notepad.f_ormat"), () => new List<MenuItem>
        {
            MenuItem.Check(L.T("notepad.word_wrap"), _editor.WordWrap, () =>
            {
                _editor.WordWrap = !_editor.WordWrap;
                _editor.MarkDirty();
            }),
            MenuItem.Of(L.T("notepad.font"), NotSupported),
        });

        Menu.Add(L.T("notepad.view"), () => new List<MenuItem>
        {
            MenuItem.Check(L.T("notepad.status_bar"), _statusBar, () => _statusBar = !_statusBar),
        });

        // The antivirus commands only exist when the antivirus "product" is open.
        if (IsAntivirus)
        {
            Menu.Add(L.T("notepad.antivirus"), () => new List<MenuItem>
            {
                MenuItem.Of(L.T("notepad.scan_for_viruses"), StartScan, IconId.Antivirus,
                            enabled: !_scanning),
                MenuItem.Of(L.T("notepad.update_definitions"), UpdateDefinitions, IconId.Shield),
                MenuItem.Sep(),
                MenuItem.Of(L.T("notepad.about"), AboutAntivirus, IconId.DlgInfo),
            });
        }

        Menu.Add(L.T("notepad.help"), () => new List<MenuItem>
        {
            MenuItem.Of(L.T("notepad.about_notepad"), AboutNotepad, IconId.DlgInfo),
        });
    }

    UiContext _ctx;

    public override void DrawClient(UiContext c, Rect client)
    {
        _ctx = c;
        _editor.Font = c.F.Mono;

        var area = client;
        if (_statusBar)
        {
            var status = new Rect(area.X, area.Bottom - 20, area.W, 20);
            area = new Rect(area.X, area.Y, area.W, area.H - 20);

            string pos = L.F("notepad.ln_0_col_1", _editor.CaretLine + 1, _editor.CaretCol + 1);
            string mode = _editor.WordWrap ? L.T("notepad.wrap") : "";
            string count = L.F("notepad.chars_0", _editor.TotalChars);
            W.StatusBar(c, status, _scanning ? L.T("notepad.scanning") : count, mode, pos);
        }

        if (_takeCaret) { c.Focus = Id + ".edit"; _takeCaret = false; }

        _editor.Draw(c, area, Id + ".edit");

        // Keyboard shortcuts that the menus advertise.
        if (!c.KeyboardHandled && c.In.Ctrl)
        {
            if (c.In.KeyPressed(Keys.S)) { SaveFile(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.N)) { NewFile(); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.F)) { Wm.Open(new FindWindow(_editor), c); c.KeyboardHandled = true; }
            else if (c.In.KeyPressed(Keys.H)) { Wm.Open(new FindWindow(_editor) { ReplaceMode = true }, c); c.KeyboardHandled = true; }
        }

        TickScan(c);
        CheckTypedCommand(c);
    }

    /// <summary>Watches for the antivirus command being typed into the document.
    ///
    /// Part 2 drives the "antivirus" by typing «ищи вирусы» into the text file and
    /// pressing Enter — the first attempt is rejected as a malformed command, and
    /// only the second, prefixed run actually starts the scan.</summary>
    void CheckTypedCommand(UiContext c)
    {
        if (!IsAntivirus || _scanning) return;
        if (c.Focus != Id + ".edit") return;
        if (!c.In.KeyPressed(Platform.Keys.Enter)) return;

        // The line just completed is the one above the caret.
        string line = _editor.Line(Math.Max(0, _editor.CaretLine - 1)).Trim();
        if (line.Length == 0) return;

        string command = L.T("notepad.scan_command");
        bool mentionsCommand = line.Contains(command, StringComparison.OrdinalIgnoreCase);
        if (!mentionsCommand) return;

        bool prefixed = line.StartsWith("то ", StringComparison.OrdinalIgnoreCase)
                     || line.StartsWith("script", StringComparison.OrdinalIgnoreCase)
                     || line.Contains("скрипт", StringComparison.OrdinalIgnoreCase);

        if (!prefixed && !_commandRejectedOnce)
        {
            // First attempt: reject it, exactly as the video does.
            _commandRejectedOnce = true;
            _editor.AppendLine(L.T("notepad.scan_wrong_command"));
            _editor.MarkDirty();
            c.Sound(Sfx.Error, 0.6f);
            return;
        }

        StartScan();
    }

    bool _commandRejectedOnce;

    // ---- the antivirus gag ----------------------------------------------

    /// <summary>Kicks off the scan from outside the menu (used by --open=scan).</summary>
    public void BeginScan() => StartScan();

    void StartScan()
    {
        _scanning = true;
        _scanStep = 0;
        _scanNext = 0;
        _foundCount = 0;
        _editor.MoveCaretToEnd();
    }

    static readonly string[] ScanTargets =
    {
        @"C:\WINDOWS\system32",
        @"C:\Program Files",
        @"C:\Documents and Settings\Admin\Рабочий стол",
        @"C:\Documents and Settings\Admin\Рабочий стол\Революционные дистрибутивы",
        @"C:\BOLGENOS",
    };

    void TickScan(UiContext c)
    {
        if (!_scanning) return;
        if (c.Time < _scanNext) return;

        // Phase 1: a few lines of plausible scanning.
        if (_scanStep < ScanTargets.Length)
        {
            _editor.AppendLine(L.F("notepad.scanning_0", ScanTargets[_scanStep]));
            c.Sound(Sfx.ScanBeep, 0.5f, 1f + _scanStep * 0.05f);
            _scanNext = c.Time + 0.42;
            _scanStep++;
            return;
        }

        // Phase 2: the wall of ОПАСНОСТЬ!, exactly as in part 3.
        int alarmStart = ScanTargets.Length;
        int alarmLines = 6;
        if (_scanStep < alarmStart + alarmLines)
        {
            string word = L.T("notepad.danger");
            _editor.AppendLine(string.Join(" ", Enumerable.Repeat(word, 6)));
            c.Sound(Sfx.ScanBeep, 0.75f, 1.35f);
            _scanNext = c.Time + 0.13;
            _scanStep++;
            return;
        }

        // Phase 3: the verdict.
        _scanning = false;
        _editor.AppendLine("");
        _editor.AppendLine(L.F("notepad.0_popovs_detected_on_your_computer", _foundCount));
        _editor.AppendLine("");
        _editor.AppendLine(L.T("notepad.no_viruses_yet"));
        _editor.AppendLine("");
        _editor.AppendLine(L.T("notepad.disinfect"));
        _editor.MarkDirty();

        c.Sound(Sfx.ScanDone, 0.9f);

        Shell.MessageBox(c,
            L.T("notepad.grevtsov_antivirus_2009"),
            L.F("notepad.scan_complete_popovs_found_0_bolgenoses_0_di", _foundCount),
            MsgButtons.Yes | MsgButtons.No, IconId.DlgQuestion, r =>
            {
                if (r != MsgResult.Yes) return;
                _editor.AppendLine("");
                _editor.AppendLine(L.T("notepad.disinfection_complete_there_was_nothing_to_d"));
                _editor.MarkDirty();
                Shell.Audio.Play(Sfx.ScanDone, 0.7f);
            }, Sfx.Question);
    }

    void UpdateDefinitions()
    {
        _editor.AppendLine(L.T("notepad.definitions_updated_06_06_2010_signatures_1"));
        _editor.MarkDirty();
        Shell.Audio.Play(Sfx.Info, 0.6f);
    }

    void AboutAntivirus()
    {
        Shell.MessageBox(_ctx,
            L.T("notepad.grevtsov_antivirus_2009"),
            L.T("notepad.grevtsov_antivirus_2009_version_1_0_text_edi"),
            MsgButtons.Ok, IconId.Antivirus, null, Sfx.Info);
    }

    void AboutNotepad()
    {
        Shell.MessageBox(_ctx, L.T("notepad.about_notepad"),
            L.T("notepad.miminus_notepad_version_5_1_text_editor_of_o"),
            MsgButtons.Ok, IconId.Notepad, null, Sfx.Info);
    }

    // ---- file commands ---------------------------------------------------

    void NewFile()
    {
        ConfirmDiscard(() =>
        {
            _file = null;
            _untitledName = L.T("notepad.untitled");
            _editor.Text = "";
            Icon = IconId.Notepad;
        });
    }

    void OpenFile()
    {
        var picker = new FilePickerWindow(Shell.Fs, L.T("notepad.open_2"), node =>
        {
            if (node is not { Kind: NodeKind.TextFile }) return;
            ConfirmDiscard(() =>
            {
                _file = node;
                _editor.Text = node.Text ?? "";
                Icon = IsAntivirus ? IconId.Antivirus : IconId.Notepad;
                BuildMenu();
            });
        });
        Wm.Open(picker, _ctx);
    }

    void SaveFile()
    {
        // A file that came from a mounted host folder goes back to disk, if the
        // mount was created writable.
        if (_file is { IsHosted: true })
        {
            if (Sys.HostMount.WriteText(_file, _editor.Text, out string error))
            {
                _editor.Modified = false;
                Shell.Audio.Play(Sfx.Click, 0.6f);
            }
            else
            {
                Shell.MessageBox(_ctx, L.T("mount.title"),
                    L.F("mount.save_failed", error), MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
            }
            return;
        }

        if (_file == null)
        {
            // Nowhere to save to yet: drop it on the desktop, like Notepad would.
            _file = Shell.Fs.CreateChild(Shell.Fs.Desktop,
                L.T("notepad.new_text_document_txt"), NodeKind.TextFile, IconId.TextFile);
        }
        _file.Text = _editor.Text;
        _file.Modified = Shell.Now;
        _editor.Modified = false;
        Shell.Audio.Play(Sfx.Click, 0.6f);
    }

    void ConfirmDiscard(Action then)
    {
        if (!_editor.Modified) { then(); return; }
        Shell.MessageBox(_ctx, L.T("notepad.notepad"),
            L.F("notepad.the_text_in_the_0_file_has_changed_do_you_wa", _file?.Name ?? _untitledName),
            MsgButtons.Yes | MsgButtons.No | MsgButtons.Cancel, IconId.DlgWarning, r =>
            {
                if (r == MsgResult.Cancel || r == MsgResult.None) return;
                if (r == MsgResult.Yes) SaveFile();
                then();
            }, Sfx.Warning);
    }

    void NotSupported()
    {
        Shell.MessageBox(_ctx, L.T("notepad.notepad"),
            L.T("notepad.this_feature_is_coming_in_the_next_version_o"),
            MsgButtons.Ok, IconId.DlgInfo, null, Sfx.Info);
    }

    bool _closeConfirmed;

    public override bool OnClosing(UiContext c)
    {
        if (_closeConfirmed || !_editor.Modified) return true;

        Shell.MessageBox(c, L.T("notepad.notepad"),
            L.F("notepad.the_text_in_the_0_file_has_changed_do_you_wa", _file?.Name ?? _untitledName),
            MsgButtons.Yes | MsgButtons.No | MsgButtons.Cancel, IconId.DlgWarning, r =>
            {
                if (r == MsgResult.Cancel || r == MsgResult.None) return;
                if (r == MsgResult.Yes) SaveFile();
                _closeConfirmed = true;
                Close();
            }, Sfx.Warning);

        return false;
    }
}

/// <summary>Notepad's Find / Find and Replace dialog.</summary>
public sealed class FindWindow : OsWindow
{
    readonly TextEditor _editor;
    string _needle = "";
    string _replacement = "";
    bool _matchCase;
    string _status = "";

    public bool ReplaceMode;

    public override string Title => ReplaceMode ? L.T("notepad.replace_2") : L.T("notepad.find_2");
    public override float MinWidth => 380;
    public override float MinHeight => 150;

    public FindWindow(TextEditor editor)
    {
        _editor = editor;
        Icon = IconId.Search;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 420, 0);
    }

    public override void OnOpened(UiContext c)
    {
        Bounds.H = c.Theme.CaptionHeight + (ReplaceMode ? 132 : 104);
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        c.Focus = Id + ".needle";
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, c.Theme.Face);
        var body = client.Deflate(12);

        float labelW = 108, rowH = 22;
        var row = body.CutTop(rowH);
        W.Label(c, new Rect(row.X, row.Y, labelW, rowH), L.T("notepad.find_what"));
        W.TextField(c, Id + ".needle", new Rect(row.X + labelW, row.Y, row.W - labelW - 92, rowH), ref _needle);

        var findBtn = new Rect(body.Right - 84, row.Y, 84, rowH);
        if (W.Button(c, Id + ".find", findBtn, L.T("notepad.find_next"), _needle.Length > 0, IconId.None, true))
            DoFind(c);

        body.CutTop(8);

        if (ReplaceMode)
        {
            row = body.CutTop(rowH);
            W.Label(c, new Rect(row.X, row.Y, labelW, rowH), L.T("notepad.replace_with"));
            W.TextField(c, Id + ".repl", new Rect(row.X + labelW, row.Y, row.W - labelW - 92, rowH), ref _replacement);

            if (W.Button(c, Id + ".rall", new Rect(body.Right - 84, row.Y, 84, rowH),
                         L.T("notepad.replace_all"), _needle.Length > 0))
            {
                int n = _editor.ReplaceAll(_needle, _replacement, _matchCase);
                _status = L.F("notepad.replaced_0", n);
                c.Sound(n > 0 ? Sfx.Click : Sfx.Error, 0.6f);
            }
            body.CutTop(8);
        }

        row = body.CutTop(rowH);
        W.CheckBox(c, Id + ".case", new Rect(row.X, row.Y, 180, rowH),
                   L.T("notepad.match_case"), ref _matchCase);

        if (W.Button(c, Id + ".close", new Rect(body.Right - 84, row.Y, 84, rowH), L.T("notepad.cancel")))
            Close();

        if (_status.Length > 0)
            c.F.Ui.Draw(c.R, _status, body.X, body.Bottom - c.F.Ui.Height, c.Theme.TextDisabled);

        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Enter)) DoFind(c);
        if (!c.KeyboardHandled && c.In.KeyPressed(Keys.Escape)) Close();
    }

    void DoFind(UiContext c)
    {
        if (_editor.Find(_needle, _matchCase))
        {
            _status = "";
            c.Sound(Sfx.Tick, 0.5f);
        }
        else
        {
            _status = L.F("notepad.cannot_find_0", _needle);
            c.Sound(Sfx.Error, 0.6f);
        }
    }

}
