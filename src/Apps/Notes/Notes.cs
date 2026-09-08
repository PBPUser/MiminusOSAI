using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Заметки» — coloured notes on a board, full screen.
///
/// A note is a rectangle of one colour with text typed straight onto it; the
/// app bar adds one, recolours the selected one and throws it away. Notes are
/// written into the virtual filesystem as text files under «Мои документы», so
/// they survive the program being closed and show up in the folder window and
/// in Notepad like anything else — which is the point of having a filesystem.</summary>
public sealed class NotesWindow : OsWindow
{
    public override string Title => L.T("notes.title");
    public override float MinWidth => 480;
    public override float MinHeight => 340;

    public NotesWindow()
    {
        Icon = IconId.TextFile;
        Bounds = new Rect(0, 0, 820, 540);
        Immersive = true;
    }

    /// <summary>One note: the file it lives in and the colour it wears. The
    /// colour is the first line of the file, so it survives a restart without
    /// anywhere else to record it.</summary>
    sealed class Note
    {
        public VNode File;
        public int Colour;
        public string Text = "";
    }

    readonly List<Note> _notes = new();
    int _selected = -1;
    VNode _folder;

    const string Prefix = "note-";

    public override void OnOpened(UiContext c)
    {
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
        _folder = Shell.Fs.MyDocuments;
        Load();
    }

    void Load()
    {
        _notes.Clear();
        foreach (var file in _folder.Entries)
        {
            if (file.Kind != NodeKind.TextFile) continue;
            if (!file.Name.StartsWith(Prefix, StringComparison.Ordinal)) continue;

            string text = file.Text ?? "";
            int colour = 0;
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                int nl = text.IndexOf('\n');
                if (nl > 0 && int.TryParse(text[1..nl].Trim(), out int parsed))
                {
                    colour = parsed;
                    text = text[(nl + 1)..];
                }
            }
            _notes.Add(new Note { File = file, Colour = colour, Text = text });
        }

        if (_notes.Count == 0) Add(0, L.T("notes.first_note"));
    }

    void Add(int colour, string text)
    {
        var file = Shell.Fs.CreateChild(_folder, Prefix + (_notes.Count + 1) + ".txt",
                                        NodeKind.TextFile, IconId.TextFile);
        var note = new Note { File = file, Colour = colour, Text = text };
        _notes.Add(note);
        Save(note);
        _selected = _notes.Count - 1;
    }

    static void Save(Note note)
    {
        if (note.File != null) note.File.Text = "#" + note.Colour + "\n" + note.Text;
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        c.R.FillRect(client, Color.Rgb(0x24262B));

        var head = client.CutTop(70);
        c.F.Big.Draw(c.R, L.T("notes.title"), head.X + 40, head.CenterY - c.F.Big.Height * 0.5f,
                     Color.White);
        c.F.Ui.Draw(c.R, L.F("notes.count", _notes.Count),
                    head.X + 56 + c.F.Big.Measure(L.T("notes.title")), head.CenterY - 2,
                    Color.Rgba(0xFFFFFF, 150));

        var bar = client.CutBottom(64);
        var board = client.Deflate(36, 6, 36, 10);

        DrawBoard(c, board);
        DrawAppBar(c, bar);
        HandleTyping(c);
    }

    void DrawBoard(UiContext c, Rect board)
    {
        const float w = 190, h = 150, gap = 14;
        int cols = Math.Max(1, (int)((board.W + gap) / (w + gap)));

        for (int i = 0; i < _notes.Count; i++)
        {
            var note = _notes[i];
            var r = new Rect(board.X + (i % cols) * (w + gap),
                             board.Y + (i / cols) * (h + gap), w, h);
            if (r.Bottom > board.Bottom) break;

            var colour = Theme.TileColors[note.Colour % Theme.TileColors.Length];
            bool sel = i == _selected;

            c.R.FillRect(r.Offset(3, 3), Color.Rgba(0x000000, 70));
            c.R.FillRect(r, colour);
            if (sel) c.R.DrawRect(r, Color.White, 2);
            else if (c.Hovering(r)) c.R.DrawRect(r, Color.Rgba(0xFFFFFF, 140));

            c.R.PushClip(r.Deflate(8));
            float ty = r.Y + 8;
            foreach (string line in c.F.Ui.Wrap(note.Text, r.W - 16))
            {
                if (ty + c.F.Ui.Height > r.Bottom - 22) break;
                c.F.Ui.Draw(c.R, line, r.X + 8, ty, Color.White);
                ty += c.F.Ui.Height + 2;
            }
            c.R.PopClip();

            // A caret on the selected note, because typing goes into it.
            if (sel && (int)(c.Time * 2) % 2 == 0)
                c.R.FillRect(new Rect(r.X + 8, ty, 1.6f, c.F.Ui.Height), Color.White);

            c.F.Small.Draw(c.R, note.File?.Name ?? "", r.X + 8, r.Bottom - 16,
                           Color.Rgba(0xFFFFFF, 150));

            if (c.Clicked(r)) { _selected = i; c.SoundAt(Sfx.Click, r, 0.4f); }
        }
    }

    void DrawAppBar(UiContext c, Rect bar)
    {
        c.R.FillRect(bar, Color.Rgb(0x1B1B1B));

        bool has = _selected >= 0 && _selected < _notes.Count;

        // The palette: clicking a colour recolours the selected note, or makes
        // a new one in that colour when nothing is selected.
        float x = bar.X + 36;
        for (int i = 0; i < 6; i++)
        {
            var swatch = new Rect(x, bar.CenterY - 14, 28, 28);
            c.R.FillRect(swatch, Theme.TileColors[i]);
            if (c.Hovering(swatch)) c.R.DrawRect(swatch, Color.White, 2);

            if (c.Clicked(swatch))
            {
                if (has) { _notes[_selected].Colour = i; Save(_notes[_selected]); }
                else Add(i, "");
                c.SoundAt(Sfx.Click, swatch, 0.45f);
            }
            x += 34;
        }

        float bx = bar.Right - 120;
        if (BarButton(c, new Rect(bx, bar.Y + 8, 100, bar.H - 16), IconId.RecycleBin, "notes.delete", has))
        {
            var note = _notes[_selected];
            if (note.File != null) Shell.Fs.Delete(note.File);
            _notes.RemoveAt(_selected);
            _selected = -1;
            c.Sound(Sfx.Trash, 0.6f);
        }

        bx -= 110;
        if (BarButton(c, new Rect(bx, bar.Y + 8, 100, bar.H - 16), IconId.TextFile, "notes.new", true))
        {
            Add(_notes.Count % Theme.TileColors.Length, "");
            c.Sound(Sfx.Click, 0.5f);
        }
    }

    bool BarButton(UiContext c, Rect r, IconId icon, string key, bool enabled)
    {
        bool hot = enabled && c.Hovering(r);
        if (hot) c.R.FillRect(r, Color.Rgba(0xFFFFFF, 35));

        var ring = new Rect(r.CenterX - 12, r.Y + 2, 24, 24);
        Color ink = enabled ? Color.White : Color.Rgba(0xFFFFFF, 90);
        c.R.DrawCircle(ring.CenterX, ring.CenterY, 12, ink, 1.5f);
        Icons.Draw(c.R, icon, ring.Deflate(5));

        string label = L.T(key);
        float w = c.F.Small.Measure(label);
        c.F.Small.Draw(c.R, label, r.CenterX - w * 0.5f, r.Bottom - c.F.Small.Height - 2, ink);

        return enabled && c.Clicked(r);
    }

    /// <summary>Typing goes into the selected note. There is no text box: the
    /// note is the text box.</summary>
    void HandleTyping(UiContext c)
    {
        if (c.KeyboardHandled || _selected < 0 || _selected >= _notes.Count) return;

        var note = _notes[_selected];
        bool changed = false;

        if (c.In.KeyPressed(Keys.Back) && note.Text.Length > 0)
        {
            note.Text = note.Text[..^1];
            changed = true;
        }
        else if (c.In.KeyPressed(Keys.Escape)) _selected = -1;

        foreach (char ch in c.In.TypedChars)
        {
            if (ch == '\b') continue;
            if (ch == '\r') { note.Text += '\n'; changed = true; continue; }
            if (ch < ' ') continue;
            note.Text += ch;
            changed = true;
        }

        if (changed) { Save(note); c.KeyboardHandled = true; }
    }
}
