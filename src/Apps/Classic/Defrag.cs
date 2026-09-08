using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Shell;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Apps;

/// <summary>«Дефрагментация диска» — the utility everybody watched and nobody
/// understood.
///
/// Two bands of coloured blocks, an analysis that finds the disk fragmented, a
/// defragmentation that walks the blocks across from the top band to the bottom
/// one, and a report at the end. The disk it works on is the virtual filesystem,
/// which lives in memory and cannot be fragmented at all — so the report says
/// so, after doing the whole job anyway.</summary>
public sealed class DefragWindow : OsWindow
{
    /// <summary>What a block holds. The colours are the ones the original used,
    /// and mean the same things.</summary>
    enum Block { Free, Fragmented, Contiguous, Unmovable }

    const int Columns = 96;
    const int Rows = 6;

    readonly Block[] _before = new Block[Columns * Rows];
    readonly Block[] _after = new Block[Columns * Rows];

    enum Phase { Idle, Analysing, Defragmenting, Done }

    Phase _phase = Phase.Idle;
    double _started;
    float _progress;
    int _placed;
    int _drive;

    public override string Title => L.T("defrag.title");
    public override float MinWidth => 520;
    public override float MinHeight => 380;

    public DefragWindow()
    {
        Icon = IconId.DriveHdd;
        Bounds = new Rect(0, 0, 640, 460);
        Reset();
    }

    public override void OnOpened(UiContext c) => CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);

    /// <summary>Lays out a disk that looks used: a run of unmovable blocks at
    /// the front, then files scattered through the middle with holes between
    /// them, which is what fragmentation looks like drawn.</summary>
    void Reset()
    {
        int seed = 20100606 + _drive * 977;
        for (int i = 0; i < _before.Length; i++)
        {
            seed = seed * 1103515245 + 12345;
            int r = (seed >> 16) & 0x7FFF;

            _before[i] = i < 40 ? Block.Unmovable
                       : r % 100 < 44 ? Block.Fragmented
                       : r % 100 < 58 ? Block.Contiguous
                       : Block.Free;
            _after[i] = Block.Free;
        }
        for (int i = 0; i < 40; i++) _after[i] = Block.Unmovable;

        _phase = Phase.Idle;
        _progress = 0;
        _placed = 40;
    }

    /// <summary>How much of the disk is in pieces, which is the number the
    /// analysis reports and the only figure anyone ever read off this window.</summary>
    int FragmentPercent()
    {
        int used = 0, frag = 0;
        foreach (var b in _before)
        {
            if (b == Block.Free) continue;
            used++;
            if (b == Block.Fragmented) frag++;
        }
        return used == 0 ? 0 : frag * 100 / used;
    }

    public override void Tick(UiContext c, float dt)
    {
        if (_phase == Phase.Analysing)
        {
            _progress += dt * 0.55f;
            if (_progress >= 1) { _progress = 1; _phase = Phase.Idle; c.Sound(Sfx.ScanDone, 0.6f); }
            return;
        }

        if (_phase != Phase.Defragmenting) return;

        // Blocks are carried across a few at a time, so the lower band fills
        // from the left while the upper one empties.
        for (int n = 0; n < 3 && _placed < _after.Length; n++)
        {
            int source = -1;
            for (int i = 40; i < _before.Length; i++)
                if (_before[i] is Block.Fragmented or Block.Contiguous) { source = i; break; }

            if (source < 0) break;

            _before[source] = Block.Free;
            _after[_placed++] = Block.Contiguous;
        }

        int remaining = 0;
        foreach (var b in _before) if (b is Block.Fragmented or Block.Contiguous) remaining++;

        _progress = 1 - remaining / (float)MathF.Max(1, _after.Length - 40);

        if (remaining == 0)
        {
            _phase = Phase.Done;
            _progress = 1;
            c.Sound(Sfx.ScanDone, 0.8f);
        }
        else if ((int)(c.Time * 6) % 6 == 0) c.Sound(Sfx.DriveSpin, 0.12f);
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        var t = c.Theme;
        c.R.FillRect(client, t.Face);

        var area = client.Deflate(10);
        var buttons = area.CutBottom(34);
        var legend = area.CutBottom(56);

        // ---- the drive list, as short as this machine's drive list is ------
        var list = area.CutTop(70);
        W.SunkenField(c, list);
        c.R.FillRect(list.Deflate(1), t.FieldBack);

        string[] drives = { "C:", "D:" };
        var head = new Rect(list.X + 2, list.Y + 2, list.W - 4, 18);
        c.F.Ui.Draw(c.R, L.T("defrag.volume"), head.X + 6, head.Y + 2, t.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("defrag.session_status"), head.X + 90, head.Y + 2, t.TextDisabled);
        c.F.Ui.Draw(c.R, L.T("defrag.file_system"), head.X + 260, head.Y + 2, t.TextDisabled);
        c.R.FillRect(new Rect(head.X, head.Bottom, head.W, 1), t.ControlBorder);

        for (int i = 0; i < drives.Length; i++)
        {
            var row = new Rect(list.X + 2, head.Bottom + 2 + i * 20, list.W - 4, 20);
            bool sel = i == _drive;
            if (sel) c.R.FillRect(row, t.Selection);

            Color ink = sel ? t.SelectionText : t.Text;
            Icons.Draw(c.R, IconId.DriveHdd, new Rect(row.X + 4, row.CenterY - 8, 16, 16));
            c.F.Ui.Draw(c.R, drives[i], row.X + 24, row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.F.Ui.Draw(c.R, L.T(sel && _phase == Phase.Done ? "defrag.done"
                                 : sel && _phase != Phase.Idle ? "defrag.working"
                                 : "defrag.not_analysed"),
                        row.X + 90, row.CenterY - c.F.Ui.Height * 0.5f, ink);
            c.F.Ui.Draw(c.R, "MIMFS", row.X + 260, row.CenterY - c.F.Ui.Height * 0.5f, ink);

            if (c.Clicked(row) && i != _drive) { _drive = i; Reset(); }
        }

        // ---- the two bands -------------------------------------------------
        area.CutTop(8);
        float bandH = (area.H - 46) * 0.5f;

        BandLabel(c, ref area, "defrag.estimated_before");
        DrawBand(c, area.CutTop(bandH), _before);
        area.CutTop(8);
        BandLabel(c, ref area, "defrag.estimated_after");
        DrawBand(c, area.CutTop(bandH), _after);

        // ---- legend ---------------------------------------------------------
        DrawLegend(c, legend);

        // ---- buttons --------------------------------------------------------
        float bw = 108, gap = 8;
        float x = buttons.X;
        bool busy = _phase is Phase.Analysing or Phase.Defragmenting;

        if (W.Button(c, Id + ".analyse", new Rect(x, buttons.Y + 4, bw, 24),
                     L.T("defrag.analyse"), !busy))
        {
            _phase = Phase.Analysing;
            _progress = 0;
            _started = c.Time;
            c.Sound(Sfx.DriveSpin, 0.5f);
        }
        x += bw + gap;

        if (W.Button(c, Id + ".defrag", new Rect(x, buttons.Y + 4, bw, 24),
                     L.T("defrag.defragment"), !busy))
        {
            _phase = Phase.Defragmenting;
            _progress = 0;
            _started = c.Time;
            c.Sound(Sfx.DriveSpin, 0.6f);
        }
        x += bw + gap;

        if (W.Button(c, Id + ".stop", new Rect(x, buttons.Y + 4, bw, 24), L.T("defrag.stop"), busy))
        {
            _phase = Phase.Idle;
            c.Sound(Sfx.Click, 0.5f);
        }

        if (W.Button(c, Id + ".report", new Rect(buttons.Right - bw, buttons.Y + 4, bw, 24),
                     L.T("defrag.report"), _phase == Phase.Done || _progress >= 1))
            ShowReport(c);

        // A progress bar while it works, and the news when it stops.
        if (busy)
        {
            var bar = new Rect(legend.X, legend.Bottom - 14, legend.W, 12);
            W.ProgressBar(c, bar, _progress);
        }
    }

    static void BandLabel(UiContext c, ref Rect area, string key)
    {
        var row = area.CutTop(c.F.Small.Height + 3);
        c.F.Small.Draw(c.R, L.T(key), row.X, row.Y, c.Theme.Text);
    }

    void DrawBand(UiContext c, Rect r, Block[] blocks)
    {
        W.SunkenField(c, r);
        var inner = r.Deflate(2);
        c.R.FillRect(inner, Color.White);

        float bw = inner.W / Columns;
        float bh = inner.H / Rows;

        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            if (b == Block.Free) continue;

            var cell = new Rect(inner.X + (i % Columns) * bw, inner.Y + (i / Columns) * bh,
                                MathF.Max(1, bw - 0.5f), MathF.Max(1, bh - 0.5f));
            c.R.FillRect(cell, Colour(b));
        }
    }

    static Color Colour(Block b) => b switch
    {
        Block.Fragmented => Color.Rgb(0xC03030),
        Block.Contiguous => Color.Rgb(0x2E6FC4),
        Block.Unmovable => Color.Rgb(0x2E8B2E),
        _ => Color.White,
    };

    void DrawLegend(UiContext c, Rect r)
    {
        (Block block, string key)[] items =
        {
            (Block.Fragmented, "defrag.fragmented"),
            (Block.Contiguous, "defrag.contiguous"),
            (Block.Unmovable, "defrag.unmovable"),
            (Block.Free, "defrag.free_space"),
        };

        float x = r.X;
        foreach (var (block, key) in items)
        {
            var swatch = new Rect(x, r.Y + 2, 12, 12);
            c.R.FillRect(swatch, Colour(block));
            c.R.DrawRect(swatch, c.Theme.ControlBorder);
            c.F.Small.Draw(c.R, L.T(key), swatch.Right + 5, swatch.Y, c.Theme.Text);
            x = swatch.Right + 9 + c.F.Small.Measure(L.T(key)) + 16;
        }
    }

    void ShowReport(UiContext c)
    {
        Shell.MessageBox(c, L.T("defrag.title"),
            L.F("defrag.report_body", FragmentPercent()),
            MsgButtons.Ok, IconId.DriveHdd, null, Sfx.Info);
    }
}
