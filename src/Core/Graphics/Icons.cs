namespace Miminus.Graphics;

public enum IconId
{
    None = 0,
    Folder, FolderOpen, TextFile, ImageFile, UnknownFile, WordDoc, Spreadsheet,
    AudioFile, VideoFile, Archive, Program,
    MyComputer, MyDocuments, MyPictures, MyMusic, RecycleBin, RecycleBinFull,
    Network, ControlPanel, Printer, Search, Help, Run, Shutdown, Logoff, Settings,
    DriveHdd, DriveDvd, DriveUsb, Phone, Camera,
    Notepad, Paint, Minesweeper, Calculator, MediaPlayer, Firefox, Opera,
    Excel, Word, Torrent, Skype, Antivirus, Display, Terminal, Game, Registry,
    DlgInfo, DlgWarning, DlgError, DlgQuestion,
    Star, Globe, Shield, Mail, Clock, Volume, TrayNetwork, Flag,

    // Version 8: the Start screen, the charms and what they open.
    Tiles, Store, Share, Devices, Power, People, Weather, Lock, PcSettings, Access,
}

/// <summary>Which procedurally generated picture an image file contains.</summary>
public enum PictureId { None, Photo, Wallpaper }

/// <summary>Every icon in the OS, drawn live from primitives.
///
/// Nothing is loaded from disk: each icon is a short vector program evaluated in
/// a 32×32 design space and scaled to whatever rectangle the caller wants, so
/// the same code serves a 16px tray icon and a 48px desktop icon. Drawing goes
/// through <see cref="Renderer2D"/>, which means icons batch together with the
/// rest of the frame.</summary>
public static class Icons
{
    /// <summary>Maps design-space coordinates (0..32) onto the target rectangle.</summary>
    readonly struct C
    {
        readonly float _x, _y, _s;
        public C(Rect r) { _s = MathF.Min(r.W, r.H) / 32f; _x = r.X + (r.W - 32 * _s) * 0.5f; _y = r.Y + (r.H - 32 * _s) * 0.5f; }
        public float X(float v) => _x + v * _s;
        public float Y(float v) => _y + v * _s;
        public float S(float v) => v * _s;
        public Rect R(float x, float y, float w, float h) => new(X(x), Y(y), S(w), S(h));
    }

    // ---- the two house styles --------------------------------------------
    //
    // The icons in this system come in two kinds, and the difference is not
    // decoration: a program written for the desktop wears the seven look —
    // saturated, rounded, lit from the top with a gloss across its upper half
    // and a shadow under it — and a program written for the full screen wears
    // the eight look, which is one flat shape in one flat colour and nothing
    // else at all. <see cref="Gloss"/> and <see cref="Drop"/> are the seven
    // half; the flat ones simply never call them.

    /// <summary>The highlight seven laid over the top half of everything: white
    /// at the top edge, gone by the middle. It is what made those icons look
    /// like they were made of something.</summary>
    static void Gloss(Renderer2D r, C c, float x, float y, float w, float h, float radius = 0)
    {
        var top = new Rect(c.X(x), c.Y(y), c.S(w), c.S(h * 0.5f));
        if (radius > 0) r.RoundedRectV(top, c.S(radius), Color.Rgba(0xFFFFFF, 150),
                                       Color.Rgba(0xFFFFFF, 20));
        else r.FillRectV(top, Color.Rgba(0xFFFFFF, 150), Color.Rgba(0xFFFFFF, 20));
    }

    /// <summary>The soft shadow under a seven icon. Two washes rather than a
    /// blur: at this size nobody can tell, and it costs two quads.</summary>
    static void Drop(Renderer2D r, C c, float x, float y, float w, float h, float radius = 0)
    {
        for (int i = 2; i >= 1; i--)
        {
            var box = new Rect(c.X(x - i * 0.5f), c.Y(y + i * 0.6f),
                               c.S(w + i), c.S(h + i * 0.4f));
            if (radius > 0) r.RoundedRect(box, c.S(radius + i * 0.4f), Color.Rgba(0x000000, 26));
            else r.FillRect(box, Color.Rgba(0x000000, 26));
        }
    }

    // A compact XP-flavoured palette shared by the whole icon set.
    static readonly Color Manila = Color.Rgb(0xFCD16B);
    static readonly Color ManilaDark = Color.Rgb(0xE0A21E);
    static readonly Color ManilaEdge = Color.Rgb(0xA9741A);
    static readonly Color Paper = Color.Rgb(0xFFFFFF);
    static readonly Color PaperEdge = Color.Rgb(0x9098A8);
    static readonly Color PaperFold = Color.Rgb(0xD8DEE8);
    static readonly Color Ink = Color.Rgb(0x6B7285);
    static readonly Color SteelHi = Color.Rgb(0xEFF3F8);
    static readonly Color Steel = Color.Rgb(0xB9C2D0);
    static readonly Color SteelDark = Color.Rgb(0x6E7A8C);
    static readonly Color ScreenBlue = Color.Rgb(0x2E6FC4);
    static readonly Color ScreenDeep = Color.Rgb(0x123A70);

    public static void Draw(Renderer2D r, IconId id, Rect rect)
    {
        if (id == IconId.None || rect.W <= 0 || rect.H <= 0) return;
        var c = new C(rect);
        switch (id)
        {
            case IconId.Folder: Folder(r, c, false); break;
            case IconId.FolderOpen: Folder(r, c, true); break;
            case IconId.TextFile: TextFile(r, c); break;
            case IconId.ImageFile: ImageFile(r, c); break;
            case IconId.UnknownFile: UnknownFile(r, c); break;
            case IconId.WordDoc: OfficeDoc(r, c, Color.Rgb(0x2B579A), "W"); break;
            case IconId.Word: OfficeDoc(r, c, Color.Rgb(0x2B579A), "W"); break;
            case IconId.Spreadsheet: OfficeDoc(r, c, Color.Rgb(0x1D6F42), "X"); break;
            case IconId.Excel: OfficeDoc(r, c, Color.Rgb(0x1D6F42), "X"); break;
            case IconId.AudioFile: AudioFile(r, c); break;
            case IconId.VideoFile: VideoFile(r, c); break;
            case IconId.Archive: Archive(r, c); break;
            case IconId.Program: Program(r, c); break;

            case IconId.MyComputer: MyComputer(r, c); break;
            case IconId.MyDocuments: MyDocuments(r, c); break;
            case IconId.MyPictures: MyPictures(r, c); break;
            case IconId.MyMusic: MyMusic(r, c); break;
            case IconId.RecycleBin: RecycleBin(r, c, false); break;
            case IconId.RecycleBinFull: RecycleBin(r, c, true); break;
            case IconId.Network: Network(r, c); break;
            case IconId.ControlPanel: ControlPanel(r, c); break;
            case IconId.Printer: Printer(r, c); break;
            case IconId.Search: Search(r, c); break;
            case IconId.Help: Help(r, c); break;
            case IconId.Run: Run(r, c); break;
            case IconId.Shutdown: Shutdown(r, c); break;
            case IconId.Logoff: Logoff(r, c); break;
            case IconId.Settings: Settings(r, c); break;
            case IconId.Registry: RegistryIcon(r, c); break;

            case IconId.DriveHdd: DriveHdd(r, c); break;
            case IconId.DriveDvd: DriveDvd(r, c); break;
            case IconId.DriveUsb: DriveUsb(r, c); break;
            case IconId.Phone: Phone(r, c); break;
            case IconId.Camera: Camera(r, c); break;

            case IconId.Notepad: Notepad(r, c); break;
            case IconId.Paint: Paint(r, c); break;
            case IconId.Minesweeper: Minesweeper(r, c); break;
            case IconId.Calculator: Calculator(r, c); break;
            case IconId.MediaPlayer: MediaPlayer(r, c); break;
            case IconId.Firefox: Firefox(r, c); break;
            case IconId.Opera: Opera(r, c); break;
            case IconId.Torrent: Torrent(r, c); break;
            case IconId.Skype: Skype(r, c); break;
            case IconId.Antivirus: Antivirus(r, c); break;
            case IconId.Display: Display(r, c); break;
            case IconId.Terminal: Terminal(r, c); break;
            case IconId.Game: Game(r, c); break;

            case IconId.DlgInfo: Badge(r, c, Color.Rgb(0x2C6FC0), "i"); break;
            case IconId.DlgWarning: Warning(r, c); break;
            case IconId.DlgError: Badge(r, c, Color.Rgb(0xC4302B), "X"); break;
            case IconId.DlgQuestion: Badge(r, c, Color.Rgb(0x2C6FC0), "?"); break;

            case IconId.Star: Star(r, c, Color.Rgb(0xF5C518)); break;
            case IconId.Globe: Globe(r, c); break;
            case IconId.Shield: Shield(r, c, Color.Rgb(0x3A7D3A)); break;
            case IconId.Mail: Mail(r, c); break;
            case IconId.Clock: Clock(r, c); break;
            case IconId.Volume: Volume(r, c); break;
            case IconId.TrayNetwork: TrayNetwork(r, c); break;
            case IconId.Flag: Flag(r, c); break;

            case IconId.Tiles: Tiles(r, c); break;
            case IconId.Store: Store(r, c); break;
            case IconId.Share: Share(r, c); break;
            case IconId.Devices: Devices(r, c); break;
            case IconId.Power: Power(r, c); break;
            case IconId.People: People(r, c); break;
            case IconId.Weather: Weather(r, c); break;
            case IconId.Lock: Lock(r, c); break;
            case IconId.PcSettings: PcSettings(r, c); break;
            case IconId.Access: Access(r, c); break;
        }
    }

    /// <summary>Overlays the little shortcut arrow in the bottom-left corner.</summary>
    public static void DrawShortcutOverlay(Renderer2D r, Rect rect)
    {
        var c = new C(rect);
        r.FillRect(c.R(0, 22, 10, 10), Color.White);
        r.DrawRect(c.R(0, 22, 10, 10), Color.Rgb(0x808080));
        r.Line(c.X(2.5f), c.Y(29.5f), c.X(7.5f), c.Y(24.5f), Color.Rgb(0x202020), c.S(1.4f));
        r.FillTriangle(c.X(4.5f), c.Y(24), c.X(8), c.Y(24), c.X(8), c.Y(27.5f), Color.Rgb(0x202020));
    }

    // ---- files and folders ----------------------------------------------

    static void Folder(Renderer2D r, C c, bool open)
    {
        // Back tab
        Drop(r, c, 2, 9, 28, 17, 1.5f);
        r.FillRect(c.R(2, 7, 12, 4), ManilaDark);
        r.RoundedRectV(c.R(2, 9, 28, 17), c.S(1.5f), Manila, ManilaDark, ManilaEdge, c.S(1));
        if (!open) Gloss(r, c, 3, 10, 26, 10, 1.5f);
        if (open)
        {
            // Front flap skewed open
            r.FillTriangle(c.X(4), c.Y(26), c.X(30), c.Y(26), c.X(26), c.Y(14), Color.Rgb(0xFFE39B));
            r.FillTriangle(c.X(4), c.Y(26), c.X(26), c.Y(14), c.X(8), c.Y(14), Color.Rgb(0xFFE39B));
            r.Line(c.X(8), c.Y(14), c.X(26), c.Y(14), ManilaEdge, c.S(1));
        }
        else
        {
            r.FillRectV(c.R(3, 12, 26, 3), Color.Rgb(0xFFE9A8), Manila);
        }
    }

    static void PageBase(Renderer2D r, C c)
    {
        r.FillRect(c.R(6, 3, 20, 26), Paper);
        r.DrawRect(c.R(6, 3, 20, 26), PaperEdge);
        // folded corner
        r.FillTriangle(c.X(19), c.Y(3), c.X(26), c.Y(10), c.X(19), c.Y(10), PaperFold);
        r.Line(c.X(19), c.Y(3), c.X(19), c.Y(10), PaperEdge, c.S(1));
        r.Line(c.X(19), c.Y(10), c.X(26), c.Y(10), PaperEdge, c.S(1));
    }

    static void TextFile(Renderer2D r, C c)
    {
        PageBase(r, c);
        for (int i = 0; i < 6; i++)
            r.FillRect(c.R(9, 12 + i * 2.6f, i == 5 ? 8 : 14, 1), Ink);
    }

    static void UnknownFile(Renderer2D r, C c)
    {
        PageBase(r, c);
        r.FillRect(c.R(9, 13, 14, 1), Ink.WithAlpha((byte)120));
        r.FillRect(c.R(9, 17, 14, 1), Ink.WithAlpha((byte)120));
        r.FillRect(c.R(9, 21, 9, 1), Ink.WithAlpha((byte)120));
    }

    static void ImageFile(Renderer2D r, C c)
    {
        PageBase(r, c);
        var frame = c.R(9, 13, 14, 11);
        r.FillRectV(frame, Color.Rgb(0x8FC8F0), Color.Rgb(0xCFE9FB));
        r.FillTriangle(c.X(9), c.Y(24), c.X(15), c.Y(16), c.X(21), c.Y(24), Color.Rgb(0x4F9A4F));
        r.FillTriangle(c.X(14), c.Y(24), c.X(19), c.Y(18.5f), c.X(23), c.Y(24), Color.Rgb(0x6DB86D));
        r.FillCircle(c.X(12), c.Y(16), c.S(1.6f), Color.Rgb(0xFFDD55));
        r.DrawRect(frame, Color.Rgb(0x5E6B80));
    }

    static void OfficeDoc(Renderer2D r, C c, Color brand, string letter)
    {
        PageBase(r, c);
        var tab = c.R(4, 14, 16, 14);
        r.RoundedRect(tab, c.S(1.5f), brand);
        // Stylised letter built from strokes rather than text, so it stays crisp.
        if (letter == "X")
        {
            r.Line(c.X(7.5f), c.Y(17), c.X(16.5f), c.Y(25), Color.White, c.S(2.2f));
            r.Line(c.X(16.5f), c.Y(17), c.X(7.5f), c.Y(25), Color.White, c.S(2.2f));
        }
        else
        {
            r.Line(c.X(6.5f), c.Y(17), c.X(9.5f), c.Y(25), Color.White, c.S(2.0f));
            r.Line(c.X(9.5f), c.Y(25), c.X(12), c.Y(19), Color.White, c.S(2.0f));
            r.Line(c.X(12), c.Y(19), c.X(14.5f), c.Y(25), Color.White, c.S(2.0f));
            r.Line(c.X(14.5f), c.Y(25), c.X(17.5f), c.Y(17), Color.White, c.S(2.0f));
        }
    }

    static void AudioFile(Renderer2D r, C c)
    {
        PageBase(r, c);
        r.FillRect(c.R(18, 12, 1.6f, 10), Color.Rgb(0x2B4A8B));
        r.FillCircle(c.X(15.5f), c.Y(22), c.S(3.2f), Color.Rgb(0x2B4A8B));
        r.FillRect(c.R(12, 12, 7.6f, 2.4f), Color.Rgb(0x2B4A8B));
    }

    static void VideoFile(Renderer2D r, C c)
    {
        PageBase(r, c);
        var strip = c.R(8, 13, 16, 12);
        r.FillRect(strip, Color.Rgb(0x303842));
        for (int i = 0; i < 4; i++)
        {
            r.FillRect(c.R(9 + i * 3.6f, 14, 2.2f, 2), Color.Rgb(0xE8EDF4));
            r.FillRect(c.R(9 + i * 3.6f, 22, 2.2f, 2), Color.Rgb(0xE8EDF4));
        }
        r.FillTriangle(c.X(13.5f), c.Y(16.5f), c.X(13.5f), c.Y(21.5f), c.X(18.5f), c.Y(19), Color.White);
    }

    static void Archive(Renderer2D r, C c)
    {
        PageBase(r, c);
        r.FillRect(c.R(14, 3, 4, 18), Color.Rgb(0x8A6FBF));
        for (int i = 0; i < 5; i++)
            r.FillRect(c.R(14, 5 + i * 3.4f, 4, 1.6f), Color.Rgb(0x5E4A8A));
        r.FillRect(c.R(13, 21, 6, 6), Color.Rgb(0xB9A4E0));
        r.DrawRect(c.R(13, 21, 6, 6), Color.Rgb(0x5E4A8A));
    }

    static void Program(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(4, 5, 24, 22), c.S(2), Color.Rgb(0xE8EDF4), Color.Rgb(0xB6C2D2), SteelDark, c.S(1));
        r.FillRectV(c.R(5, 6, 22, 5), Color.Rgb(0x4A8BE0), Color.Rgb(0x2B5FA8));
        r.FillCircle(c.X(10), c.Y(18), c.S(4), Color.Rgb(0xF0A030));
        r.FillTriangle(c.X(16), c.Y(23), c.X(21), c.Y(14), c.X(26), c.Y(23), Color.Rgb(0x50A050));
    }

    // ---- shell places ----------------------------------------------------

    static void MonitorBody(Renderer2D r, C c, Color screenTop, Color screenBottom)
    {
        r.RoundedRectV(c.R(2, 4, 28, 20), c.S(2), SteelHi, Steel, SteelDark, c.S(1));
        var screen = c.R(4.5f, 6.5f, 23, 15);
        r.FillRectV(screen, screenTop, screenBottom);
        r.DrawRect(screen, Color.Rgb(0x334055));
        r.FillRect(c.R(12, 24, 8, 3), Steel);
        r.RoundedRect(c.R(8, 27, 16, 3), c.S(1.2f), SteelDark);
    }

    static void MyComputer(Renderer2D r, C c)
    {
        MonitorBody(r, c, ScreenBlue, ScreenDeep);
        // little tower peeking behind
        r.FillRectV(c.R(21, 12, 8, 14), Color.Rgb(0xD8DEE8), Color.Rgb(0xA8B2C2));
        r.DrawRect(c.R(21, 12, 8, 14), SteelDark);
        r.FillRect(c.R(22.5f, 14, 5, 1.2f), SteelDark);
        r.FillRect(c.R(22.5f, 16.5f, 5, 1.2f), SteelDark);
        r.FillCircle(c.X(25), c.Y(23), c.S(1.2f), Color.Rgb(0x50C050));
    }

    static void Display(Renderer2D r, C c)
    {
        MonitorBody(r, c, Color.Rgb(0x7FB2E8), Color.Rgb(0x2E6FC4));
        r.FillCircle(c.X(23), c.Y(11), c.S(2.2f), Color.Rgb(0xFFE08A));
    }

    static void MyDocuments(Renderer2D r, C c)
    {
        Folder(r, c, false);
        r.FillRect(c.R(10, 4, 13, 15), Paper);
        r.DrawRect(c.R(10, 4, 13, 15), PaperEdge);
        for (int i = 0; i < 4; i++)
            r.FillRect(c.R(12, 7 + i * 2.6f, 9, 1), Ink);
    }

    static void MyPictures(Renderer2D r, C c)
    {
        Folder(r, c, false);
        var frame = c.R(9, 4, 15, 12);
        r.FillRectV(frame, Color.Rgb(0xAEDCF8), Color.Rgb(0xE4F3FD));
        r.FillTriangle(c.X(9), c.Y(16), c.X(15), c.Y(8), c.X(21), c.Y(16), Color.Rgb(0x4F9A4F));
        r.FillCircle(c.X(12), c.Y(7.5f), c.S(1.5f), Color.Rgb(0xFFDD55));
        r.DrawRect(frame, Color.Rgb(0x5E6B80));
    }

    static void MyMusic(Renderer2D r, C c)
    {
        Folder(r, c, false);
        r.FillRect(c.R(20, 4, 1.8f, 11), Color.Rgb(0x2B4A8B));
        r.FillCircle(c.X(17.5f), c.Y(15), c.S(3), Color.Rgb(0x2B4A8B));
        r.FillRect(c.R(14, 4, 7.8f, 2.4f), Color.Rgb(0x2B4A8B));
        r.FillCircle(c.X(11.5f), c.Y(17), c.S(3), Color.Rgb(0x2B4A8B));
        r.FillRect(c.R(14, 6, 1.8f, 11), Color.Rgb(0x2B4A8B));
    }

    static void RecycleBin(Renderer2D r, C c, bool full)
    {
        // Translucent blue bin, slightly tapered.
        var body = c.R(8, 9, 16, 19);
        r.FillRectV(body, Color.Rgba(0x9FCBEA, 235), Color.Rgba(0x5E96C8, 235));
        r.DrawRect(body, Color.Rgb(0x3B6E96));
        for (int i = 0; i < 3; i++)
            r.FillRect(c.R(11 + i * 4, 12, 1.6f, 13), Color.Rgba(0xFFFFFF, 120));
        r.RoundedRect(c.R(6, 6, 20, 4), c.S(1.2f), Color.Rgb(0x7FB3DA), Color.Rgb(0x3B6E96), c.S(1));
        r.FillRect(c.R(13, 3.5f, 6, 2.5f), Color.Rgb(0x7FB3DA));
        if (full)
        {
            r.FillRect(c.R(11, 2, 5, 5), Paper);
            r.FillRect(c.R(16, 1, 5, 5), Color.Rgb(0xF3E39A));
        }
    }

    static void Network(Renderer2D r, C c)
    {
        r.FillRectV(c.R(3, 16, 11, 8), Color.Rgb(0xE0E6EF), Color.Rgb(0xA8B2C2));
        r.DrawRect(c.R(3, 16, 11, 8), SteelDark);
        r.FillRectV(c.R(18, 16, 11, 8), Color.Rgb(0xE0E6EF), Color.Rgb(0xA8B2C2));
        r.DrawRect(c.R(18, 16, 11, 8), SteelDark);
        r.Line(c.X(8.5f), c.Y(16), c.X(8.5f), c.Y(10), Color.Rgb(0x4A5568), c.S(1.4f));
        r.Line(c.X(23.5f), c.Y(16), c.X(23.5f), c.Y(10), Color.Rgb(0x4A5568), c.S(1.4f));
        r.Line(c.X(8.5f), c.Y(10), c.X(23.5f), c.Y(10), Color.Rgb(0x4A5568), c.S(1.4f));
        r.FillCircle(c.X(16), c.Y(7), c.S(3.5f), Color.Rgb(0x3C82C8));
    }

    static void ControlPanel(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(3, 6, 26, 20), c.S(2), Color.Rgb(0xEFF3F8), Color.Rgb(0xC3CDDA), SteelDark, c.S(1));
        Gear(r, c, 12, 16, 7, Color.Rgb(0x6E8BB5));
        r.FillCircle(c.X(22), c.Y(12), c.S(3), Color.Rgb(0xD05050));
        r.FillCircle(c.X(22), c.Y(20), c.S(3), Color.Rgb(0x50A050));
    }

    static void Gear(Renderer2D r, C c, float cx, float cy, float radius, Color col)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4f;
            float x = cx + MathF.Cos(a) * radius;
            float y = cy + MathF.Sin(a) * radius;
            r.FillRect(new Rect(c.X(x) - c.S(1.6f), c.Y(y) - c.S(1.6f), c.S(3.2f), c.S(3.2f)), col);
        }
        r.FillCircle(c.X(cx), c.Y(cy), c.S(radius - 1.2f), col);
        r.FillCircle(c.X(cx), c.Y(cy), c.S(radius - 4f), Color.Rgb(0xEFF3F8));
    }

    /// <summary>«Редактор реестра»: the three blue blocks the registry editor
    /// has worn since it stopped being a Windows 3 program, drawn in the seven
    /// style — rounded, lit from the top, with a shadow under the stack.</summary>
    static void RegistryIcon(Renderer2D r, C c)
    {
        Drop(r, c, 5, 5, 22, 22, 2);

        var face = Color.Rgb(0x3F7FD0);
        var edge = Color.Rgb(0x1F4E86);

        // Three stacked blocks, the middle one indented, which is the shape of
        // a tree of keys reduced to three rectangles.
        (float x, float y, float w)[] blocks = { (5, 5, 22), (9, 13.5f, 18), (13, 22, 14) };
        foreach (var (x, y, w) in blocks)
        {
            var box = c.R(x, y, w, 7);
            r.RoundedRectV(box, c.S(1.4f), face.Shade(1.22f), face, edge, c.S(1));
            r.FillRect(new Rect(box.X + c.S(2), box.Y + c.S(2.6f), c.S(w - 4), c.S(1)),
                       Color.Rgba(0xFFFFFF, 170));
        }

        Gloss(r, c, 5, 5, 22, 8, 1.4f);
    }

    static void Settings(Renderer2D r, C c) => Gear(r, c, 16, 16, 11, Color.Rgb(0x6E8BB5));

    static void Printer(Renderer2D r, C c)
    {
        r.FillRect(c.R(8, 4, 16, 8), Paper);
        r.DrawRect(c.R(8, 4, 16, 8), PaperEdge);
        r.RoundedRectV(c.R(4, 12, 24, 11), c.S(1.5f), Color.Rgb(0xE0E6EF), Color.Rgb(0x98A4B6), SteelDark, c.S(1));
        r.FillCircle(c.X(24), c.Y(15), c.S(1.2f), Color.Rgb(0x50C050));
        r.FillRect(c.R(9, 21, 14, 7), Paper);
        r.DrawRect(c.R(9, 21, 14, 7), PaperEdge);
    }

    static void Search(Renderer2D r, C c)
    {
        r.DrawCircle(c.X(14), c.Y(14), c.S(8), Color.Rgb(0x3C6FA8), c.S(2.4f));
        r.FillCircle(c.X(14), c.Y(14), c.S(6.4f), Color.Rgba(0xBFE0FA, 170));
        r.Line(c.X(19.5f), c.Y(19.5f), c.X(27), c.Y(27), Color.Rgb(0x3C6FA8), c.S(3));
    }

    static void Help(Renderer2D r, C c) => Badge(r, c, Color.Rgb(0x2C6FC0), "?");

    static void Run(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(3, 7, 26, 18), c.S(1.5f), Color.Rgb(0xF4F7FB), Color.Rgb(0xD4DCE8), SteelDark, c.S(1));
        r.FillRect(c.R(4, 8, 24, 4), Color.Rgb(0x3C82C8));
        r.FillRect(c.R(6, 14, 20, 8), Color.Black);
        r.FillRect(c.R(7.5f, 16, 5, 1.4f), Color.Rgb(0x50E050));
        r.FillRect(c.R(7.5f, 19, 8, 1.4f), Color.Rgb(0x50E050));
    }

    static void Shutdown(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(17), c.S(11), Color.Rgb(0xD24A3A));
        r.FillCircle(c.X(16), c.Y(17), c.S(9), Color.Rgb(0xF0705E));
        r.DrawCircle(c.X(16), c.Y(17.5f), c.S(5.5f), Color.White, c.S(2));
        r.FillRect(c.R(14.8f, 8.5f, 2.4f, 8), Color.Rgb(0xF0705E));
        r.FillRect(c.R(14.8f, 9.5f, 2.4f, 7), Color.White);
    }

    static void Logoff(Renderer2D r, C c)
    {
        r.RoundedRect(c.R(4, 5, 14, 22), c.S(1.5f), Color.Rgb(0xD8DEE8), SteelDark, c.S(1));
        r.FillCircle(c.X(15), c.Y(16), c.S(1.4f), SteelDark);
        r.Line(c.X(20), c.Y(16), c.X(29), c.Y(16), Color.Rgb(0x3C82C8), c.S(2.4f));
        r.FillTriangle(c.X(25), c.Y(11), c.X(30), c.Y(16), c.X(25), c.Y(21), Color.Rgb(0x3C82C8));
    }

    // ---- drives and devices ---------------------------------------------

    static void DriveHdd(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(3, 10, 26, 13), c.S(1.5f), Color.Rgb(0xEFF3F8), Color.Rgb(0xB6C2D2), SteelDark, c.S(1));
        r.FillRectV(c.R(4, 11, 24, 4), Color.Rgb(0xFFFFFF), Color.Rgb(0xD6DEEA));
        r.FillCircle(c.X(7), c.Y(19), c.S(1.4f), Color.Rgb(0x50C050));
        r.FillRect(c.R(11, 18, 14, 2), Color.Rgb(0x98A4B6));
        r.FillRect(c.R(11, 21, 9, 1.4f), Color.Rgb(0xC0C8D4));
    }

    static void DriveDvd(Renderer2D r, C c)
    {
        DriveHdd(r, c);
        r.FillCircle(c.X(21), c.Y(9), c.S(8), Color.Rgb(0xD8E4F0));
        r.FillCircle(c.X(21), c.Y(9), c.S(7.2f), Color.Rgb(0xA8C8E8));
        r.FillCircle(c.X(21), c.Y(9), c.S(2.4f), Color.Rgb(0xF4F8FC));
        r.DrawCircle(c.X(21), c.Y(9), c.S(7.6f), Color.Rgb(0x6E7A8C), c.S(1));
    }

    static void DriveUsb(Renderer2D r, C c)
    {
        r.RoundedRect(c.R(6, 11, 20, 10), c.S(1.5f), Color.Rgb(0x4A5568));
        r.FillRect(c.R(2, 13.5f, 5, 5), Color.Rgb(0xC0C8D4));
        r.DrawRect(c.R(2, 13.5f, 5, 5), SteelDark);
        r.FillCircle(c.X(22), c.Y(16), c.S(1.4f), Color.Rgb(0x50C050));
    }

    static void Phone(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(9, 2, 14, 28), c.S(2), Color.Rgb(0x4A5568), Color.Rgb(0x2B3442), Color.Rgb(0x1A2230), c.S(1));
        r.FillRectV(c.R(11, 5, 10, 14), ScreenBlue, ScreenDeep);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r.FillRect(c.R(11.5f + j * 3.2f, 21 + i * 2.6f, 2.2f, 1.6f), Color.Rgb(0x8A94A6));
    }

    static void Camera(Renderer2D r, C c)
    {
        r.RoundedRectV(c.R(3, 9, 26, 16), c.S(2), Color.Rgb(0x5A6474), Color.Rgb(0x333B48), Color.Rgb(0x1E2530), c.S(1));
        r.FillRect(c.R(11, 6, 10, 4), Color.Rgb(0x5A6474));
        r.FillCircle(c.X(16), c.Y(17), c.S(6), Color.Rgb(0x1E2530));
        r.FillCircle(c.X(16), c.Y(17), c.S(4.4f), Color.Rgb(0x3C82C8));
        r.FillCircle(c.X(14.4f), c.Y(15.4f), c.S(1.4f), Color.Rgba(0xFFFFFF, 200));
        r.FillCircle(c.X(25), c.Y(12.5f), c.S(1.2f), Color.Rgb(0xD05050));
    }

    // ---- applications ----------------------------------------------------

    static void Notepad(Renderer2D r, C c)
    {
        Drop(r, c, 5, 3, 22, 26, 1.5f);
        r.RoundedRectV(c.R(5, 3, 22, 26), c.S(1.5f), Color.White, Color.Rgb(0xDCE4EE),
                       Color.Rgb(0x6E7A8C), c.S(1));

        // The blue title strip, and the paper under it.
        r.FillRectV(c.R(6, 4, 20, 5), Color.Rgb(0x6FB0F5), Color.Rgb(0x2B5FA8));
        for (int i = 0; i < 6; i++)
            r.FillRect(c.R(8, 12 + i * 2.7f, i == 5 ? 8 : 16, 1), Ink);

        Gloss(r, c, 6, 4, 20, 11, 1);
    }

    static void Paint(Renderer2D r, C c)
    {
        // palette
        r.FillCircle(c.X(14), c.Y(18), c.S(11.4f), Color.Rgba(0x000000, 40));
        r.FillCircle(c.X(14), c.Y(17), c.S(11), Color.Rgb(0xF6EDD8));
        r.PushClip(new Rect(c.X(3), c.Y(6), c.S(22), c.S(11)));
        r.FillCircle(c.X(14), c.Y(16), c.S(10), Color.Rgba(0xFFFFFF, 110));
        r.PopClip();
        r.DrawCircle(c.X(14), c.Y(17), c.S(11), Color.Rgb(0xA88C60), c.S(1));
        r.FillCircle(c.X(18), c.Y(21), c.S(3), Color.Rgb(0xF0E2C8).Shade(0.75f));
        r.FillCircle(c.X(9), c.Y(13), c.S(2.2f), Color.Rgb(0xD03030));
        r.FillCircle(c.X(14), c.Y(10.5f), c.S(2.2f), Color.Rgb(0x3060D0));
        r.FillCircle(c.X(19), c.Y(12.5f), c.S(2.2f), Color.Rgb(0xF0C020));
        r.FillCircle(c.X(8), c.Y(19), c.S(2.2f), Color.Rgb(0x30A050));
        // brush
        r.Line(c.X(21), c.Y(26), c.X(29), c.Y(6), Color.Rgb(0xA0703C), c.S(2.4f));
        r.FillTriangle(c.X(19), c.Y(29), c.X(23.5f), c.Y(24.5f), c.X(21.5f), c.Y(27.5f), Color.Rgb(0xD03030));
    }

    static void Minesweeper(Renderer2D r, C c)
    {
        Drop(r, c, 2, 2, 28, 28, 2);
        r.RoundedRectV(c.R(2, 2, 28, 28), c.S(2), Color.Rgb(0xE8E8E8), Color.Rgb(0xA8A8A8),
                       Color.Rgb(0x707070), c.S(1));
        r.FillCircle(c.X(16), c.Y(18), c.S(8), Color.Black);
        r.FillCircle(c.X(13), c.Y(15), c.S(2.4f), Color.Rgba(0xFFFFFF, 210));
        // spikes
        r.FillRect(c.R(15, 6, 2, 24), Color.Black);
        r.FillRect(c.R(4, 17, 24, 2), Color.Black);
        r.Line(c.X(8), c.Y(10), c.X(24), c.Y(26), Color.Black, c.S(2));
        r.Line(c.X(24), c.Y(10), c.X(8), c.Y(26), Color.Black, c.S(2));
        r.FillRect(c.R(14, 4, 4, 3), Color.Rgb(0xD03030));
    }

    static void Calculator(Renderer2D r, C c)
    {
        Drop(r, c, 5, 2, 22, 28, 2);
        r.RoundedRectV(c.R(5, 2, 22, 28), c.S(2), Color.Rgb(0xF4F7FB), Color.Rgb(0x9EAEC2),
                       Color.Rgb(0x5A6678), c.S(1));

        // The display, sunk into the case.
        r.RoundedRectV(c.R(7.5f, 5, 17, 6), c.S(1), Color.Rgb(0x9FC088), Color.Rgb(0xD6E8C4),
                       Color.Rgb(0x5A6E4C), c.S(1));

        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                var key = c.R(7.5f + col * 4.4f, 13 + row * 4.2f, 3.6f, 3.4f);
                bool equals = row == 3 && col == 3;
                r.RoundedRectV(key, c.S(0.8f),
                               equals ? Color.Rgb(0xFFB060) : Color.Rgb(0xC6D0DC),
                               equals ? Color.Rgb(0xD06810) : Color.Rgb(0x76839A));
            }

        Gloss(r, c, 5, 2, 22, 16, 2);
    }

    static void MediaPlayer(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(17), c.S(13.4f), Color.Rgba(0x000000, 40));
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0xC24A10));
        r.FillCircle(c.X(16), c.Y(16), c.S(11.6f), Color.Rgb(0xF58A3C));

        // The lit upper half, which is what a seven orb was.
        r.PushClip(new Rect(c.X(4), c.Y(4), c.S(24), c.S(13)));
        r.FillCircle(c.X(16), c.Y(15), c.S(10.4f), Color.Rgba(0xFFFFFF, 90));
        r.PopClip();

        r.FillTriangle(c.X(13), c.Y(10), c.X(13), c.Y(22), c.X(23), c.Y(16), Color.White);
    }

    static void Firefox(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0x1B4A8F));
        r.FillCircle(c.X(16), c.Y(16), c.S(11.5f), Color.Rgb(0x2C6FC0));
        // stylised flame wrapping the globe
        r.FillCircle(c.X(17), c.Y(16), c.S(9), Color.Rgb(0xE8721C));
        r.FillCircle(c.X(19.5f), c.Y(13.5f), c.S(6), Color.Rgb(0xF5A623));
        r.FillCircle(c.X(13.5f), c.Y(17.5f), c.S(5), Color.Rgb(0x2C6FC0));
        r.FillTriangle(c.X(22), c.Y(6), c.X(29), c.Y(12), c.X(21), c.Y(13), Color.Rgb(0xF5C518));
    }

    static void Opera(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0xC4302B));
        r.FillCircle(c.X(16), c.Y(16), c.S(7), Color.White);
        r.FillCircle(c.X(16), c.Y(16), c.S(5.4f), Color.Rgb(0xC4302B));
    }

    static void Torrent(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0x50A050));
        r.FillCircle(c.X(16), c.Y(16), c.S(11), Color.Rgb(0x76C476));
        r.FillRect(c.R(14, 7, 4, 13), Color.White);
        r.FillTriangle(c.X(10), c.Y(18), c.X(22), c.Y(18), c.X(16), c.Y(26), Color.White);
    }

    static void Skype(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0x00AFF0));
        r.FillCircle(c.X(16), c.Y(16), c.S(10), Color.Rgb(0x2FC0F8));
        r.Line(c.X(12), c.Y(12), c.X(20), c.Y(12), Color.White, c.S(3));
        r.Line(c.X(12), c.Y(20), c.X(20), c.Y(20), Color.White, c.S(3));
        r.Line(c.X(12), c.Y(12), c.X(12), c.Y(16), Color.White, c.S(3));
        r.Line(c.X(20), c.Y(16), c.X(20), c.Y(20), Color.White, c.S(3));
        r.Line(c.X(12), c.Y(16), c.X(20), c.Y(16), Color.White, c.S(3));
    }

    static void Shield(Renderer2D r, C c, Color col)
    {
        r.FillTriangle(c.X(4), c.Y(6), c.X(28), c.Y(6), c.X(16), c.Y(30), col);
        r.FillRect(c.R(4, 4, 24, 8), col);
        r.FillTriangle(c.X(6.5f), c.Y(8), c.X(25.5f), c.Y(8), c.X(16), c.Y(26.5f), col.Shade(1.25f));
        r.FillRect(c.R(6.5f, 6.5f, 19, 4), col.Shade(1.25f));
    }

    static void Antivirus(Renderer2D r, C c)
    {
        Shield(r, c, Color.Rgb(0x2E7D32));
        r.Line(c.X(10.5f), c.Y(15), c.X(14.5f), c.Y(20), Color.White, c.S(3));
        r.Line(c.X(14.5f), c.Y(20), c.X(22), c.Y(10.5f), Color.White, c.S(3));
    }

    static void Terminal(Renderer2D r, C c)
    {
        Drop(r, c, 3, 5, 26, 22, 1.5f);
        r.RoundedRectV(c.R(3, 5, 26, 22), c.S(1.5f), Color.Rgb(0x1C242E), Color.Rgb(0x0A0E12),
                       Color.Rgb(0x6E7A8C), c.S(1));
        r.FillRectV(c.R(4, 6, 24, 4), Color.Rgb(0x4A5668), Color.Rgb(0x2A3240));
        r.Line(c.X(7), c.Y(14), c.X(11), c.Y(17), Color.Rgb(0x50E050), c.S(1.8f));
        r.Line(c.X(11), c.Y(17), c.X(7), c.Y(20), Color.Rgb(0x50E050), c.S(1.8f));
        r.FillRect(c.R(13, 19, 9, 1.6f), Color.Rgb(0x50E050));
    }

    static void Game(Renderer2D r, C c)
    {
        r.RoundedRect(c.R(2, 10, 28, 14), c.S(5), Color.Rgb(0x4A5568), Color.Rgb(0x2B3442), c.S(1));
        r.FillRect(c.R(7, 16, 7, 2), Color.White);
        r.FillRect(c.R(9.5f, 13.5f, 2, 7), Color.White);
        r.FillCircle(c.X(22), c.Y(15), c.S(1.8f), Color.Rgb(0xD05050));
        r.FillCircle(c.X(26), c.Y(19), c.S(1.8f), Color.Rgb(0x50A050));
    }

    // ---- dialog badges ---------------------------------------------------

    static void Badge(Renderer2D r, C c, Color col, string glyph)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(14), col.Shade(0.7f));
        r.FillCircle(c.X(16), c.Y(16), c.S(12.5f), col);
        r.FillCircle(c.X(12), c.Y(11), c.S(4), col.Shade(1.35f));
        switch (glyph)
        {
            case "i":
                r.FillCircle(c.X(16), c.Y(9.5f), c.S(2), Color.White);
                r.FillRect(c.R(14.4f, 13.5f, 3.2f, 10), Color.White);
                break;
            case "X":
                r.Line(c.X(10), c.Y(10), c.X(22), c.Y(22), Color.White, c.S(3.6f));
                r.Line(c.X(22), c.Y(10), c.X(10), c.Y(22), Color.White, c.S(3.6f));
                break;
            case "?":
                r.DrawCircle(c.X(16), c.Y(12.5f), c.S(4.2f), Color.White, c.S(3));
                r.FillRect(c.R(14.4f, 15, 3.2f, 4.5f), Color.White);
                r.FillCircle(c.X(16), c.Y(22.5f), c.S(2), Color.White);
                break;
        }
    }

    static void Warning(Renderer2D r, C c)
    {
        r.FillTriangle(c.X(16), c.Y(2), c.X(31), c.Y(29), c.X(1), c.Y(29), Color.Rgb(0xC9A227));
        r.FillTriangle(c.X(16), c.Y(5), c.X(28.2f), c.Y(27), c.X(3.8f), c.Y(27), Color.Rgb(0xF5C518));
        r.FillRect(c.R(14.4f, 11, 3.2f, 9), Color.Black);
        r.FillCircle(c.X(16), c.Y(23), c.S(1.9f), Color.Black);
    }

    // ---- small glyphs ----------------------------------------------------

    static void Star(Renderer2D r, C c, Color col)
    {
        float cx = 16, cy = 16, outer = 13, inner = 5.4f;
        for (int i = 0; i < 5; i++)
        {
            float a0 = -MathF.PI / 2 + i * MathF.PI * 2 / 5;
            float a1 = a0 + MathF.PI / 5;
            float a2 = a0 + MathF.PI * 2 / 5;
            r.FillTriangle(
                c.X(cx + MathF.Cos(a0) * outer), c.Y(cy + MathF.Sin(a0) * outer),
                c.X(cx + MathF.Cos(a1) * inner), c.Y(cy + MathF.Sin(a1) * inner),
                c.X(cx), c.Y(cy), col);
            r.FillTriangle(
                c.X(cx + MathF.Cos(a1) * inner), c.Y(cy + MathF.Sin(a1) * inner),
                c.X(cx + MathF.Cos(a2) * outer), c.Y(cy + MathF.Sin(a2) * outer),
                c.X(cx), c.Y(cy), col);
        }
    }

    static void Globe(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0x2C6FC0));
        r.FillCircle(c.X(16), c.Y(16), c.S(11.5f), Color.Rgb(0x4A9BE8));
        r.FillTriangle(c.X(8), c.Y(14), c.X(16), c.Y(9), c.X(15), c.Y(17), Color.Rgb(0x5DBB63));
        r.FillTriangle(c.X(17), c.Y(20), c.X(24), c.Y(16), c.X(22), c.Y(24), Color.Rgb(0x5DBB63));
        r.DrawCircle(c.X(16), c.Y(16), c.S(11.5f), Color.Rgb(0x1B4A8F), c.S(1));
        r.FillRect(c.R(4.5f, 15.4f, 23, 1.2f), Color.Rgba(0x1B4A8F, 150));
    }

    static void Mail(Renderer2D r, C c)
    {
        r.FillRect(c.R(3, 8, 26, 17), Paper);
        r.DrawRect(c.R(3, 8, 26, 17), SteelDark);
        r.Line(c.X(3), c.Y(8), c.X(16), c.Y(18), Color.Rgb(0x8894A6), c.S(1.4f));
        r.Line(c.X(29), c.Y(8), c.X(16), c.Y(18), Color.Rgb(0x8894A6), c.S(1.4f));
    }

    static void Clock(Renderer2D r, C c)
    {
        r.FillCircle(c.X(16), c.Y(16), c.S(13), Color.Rgb(0xD8DEE8));
        r.FillCircle(c.X(16), c.Y(16), c.S(11.5f), Paper);
        r.DrawCircle(c.X(16), c.Y(16), c.S(11.5f), SteelDark, c.S(1));
        r.Line(c.X(16), c.Y(16), c.X(16), c.Y(9), Color.Black, c.S(1.6f));
        r.Line(c.X(16), c.Y(16), c.X(21), c.Y(18), Color.Black, c.S(1.6f));
    }

    static void Volume(Renderer2D r, C c)
    {
        Color ink = Color.Rgb(0x40485A);
        r.FillRect(c.R(4, 13, 5, 6), ink);
        r.FillTriangle(c.X(9), c.Y(13), c.X(15), c.Y(6), c.X(15), c.Y(26), ink);
        r.FillTriangle(c.X(9), c.Y(19), c.X(9), c.Y(13), c.X(15), c.Y(26), ink);
        // Two sound arcs, stroked as segment fans so nothing wraps behind the cone.
        Arc(r, c, 15, 16, 6.5f, -60, 60, ink, 1.7f);
        Arc(r, c, 15, 16, 10.5f, -60, 60, ink, 1.7f);
    }

    /// <summary>Stroked arc from <paramref name="deg0"/> to <paramref name="deg1"/>
    /// (0° points right, angles increase clockwise on screen).</summary>
    static void Arc(Renderer2D r, C c, float cx, float cy, float radius,
                    float deg0, float deg1, Color col, float thickness)
    {
        const int Steps = 12;
        float a0 = deg0 * MathF.PI / 180f, a1 = deg1 * MathF.PI / 180f;
        float px = 0, py = 0;
        for (int i = 0; i <= Steps; i++)
        {
            float a = a0 + (a1 - a0) * i / Steps;
            float x = c.X(cx + MathF.Cos(a) * radius);
            float y = c.Y(cy + MathF.Sin(a) * radius);
            if (i > 0) r.Line(px, py, x, y, col, c.S(thickness));
            px = x; py = y;
        }
    }

    static void TrayNetwork(Renderer2D r, C c)
    {
        r.FillRect(c.R(3, 16, 10, 8), Color.Rgb(0xD8DEE8));
        r.DrawRect(c.R(3, 16, 10, 8), Color.Rgb(0x40485A));
        r.FillRect(c.R(17, 8, 10, 8), Color.Rgb(0xD8DEE8));
        r.DrawRect(c.R(17, 8, 10, 8), Color.Rgb(0x40485A));
        r.Line(c.X(12), c.Y(20), c.X(18), c.Y(12), Color.Rgb(0x40485A), c.S(1.4f));
        r.FillCircle(c.X(8), c.Y(20), c.S(1.2f), Color.Rgb(0x50C050));
        r.FillCircle(c.X(22), c.Y(12), c.S(1.2f), Color.Rgb(0x50C050));
    }

    static void Flag(Renderer2D r, C c)
    {
        r.FillRect(c.R(8, 4, 1.8f, 24), Color.Rgb(0x40485A));
        r.FillTriangle(c.X(10), c.Y(5), c.X(24), c.Y(10), c.X(10), c.Y(15), Color.Rgb(0xD03030));
    }

    // ---- version 8: Start screen and charms ------------------------------
    //
    // These are the other house style: one flat tile in one flat colour with a
    // white glyph cut out of it, no gradient, no gloss and no shadow. They are
    // deliberately plainer than the desktop icons above — that difference is
    // the whole visual argument version 8 was making, and reproducing it means
    // reproducing both halves of it.

    /// <summary>The flat square a modern icon is built on: the accent colour,
    /// edge to edge, with nothing on it yet.</summary>
    static void Tile(Renderer2D r, C c, Color colour)
        => r.FillRect(c.R(2, 2, 28, 28), colour);

    static readonly Color MetroBlue = Color.Rgb(0x2D89EF);

    /// <summary>The four-pane Start glyph, flat and square — no orb.</summary>
    static void Tiles(Renderer2D r, C c)
    {
        r.FillRect(c.R(4, 4, 11, 11), MetroBlue);
        r.FillRect(c.R(17, 4, 11, 11), MetroBlue);
        r.FillRect(c.R(4, 17, 11, 11), MetroBlue);
        r.FillRect(c.R(17, 17, 11, 11), MetroBlue);
    }

    /// <summary>«Магазин» — a shopping bag with the same four panes on it.</summary>
    static void Store(Renderer2D r, C c)
    {
        Tile(r, c, MetroBlue);

        // The bag: a white outline and the four panes inside it, flat.
        r.DrawRect(c.R(8, 12, 16, 14), Color.White, c.S(1.6f));
        r.DrawCircle(c.X(16), c.Y(12), c.S(4.5f), Color.White, c.S(1.6f));
        r.FillRect(c.R(8, 8, 16, 5), MetroBlue);

        r.FillRect(c.R(11, 16, 4, 4), Color.White);
        r.FillRect(c.R(16.5f, 16, 4, 4), Color.White);
        r.FillRect(c.R(11, 21.5f, 4, 4), Color.White);
        r.FillRect(c.R(16.5f, 21.5f, 4, 4), Color.White);
    }

    /// <summary>«Общий доступ» — an arrow leaving a box.</summary>
    static void Share(Renderer2D r, C c)
    {
        r.DrawRect(c.R(5, 13, 22, 15), Color.Rgb(0x3A3A3A), c.S(2.2f));
        r.FillRect(c.R(14.8f, 9, 2.4f, 13), Color.Rgb(0x3A3A3A));
        r.FillTriangle(c.X(16), c.Y(3), c.X(9.5f), c.Y(11), c.X(22.5f), c.Y(11),
                       Color.Rgb(0x3A3A3A));
    }

    /// <summary>«Устройства» — a monitor with a second screen behind it.</summary>
    static void Devices(Renderer2D r, C c)
    {
        // A monitor and a slab, both outlined and both flat.
        var ink = Color.Rgb(0x3A3A3A);
        r.DrawRect(c.R(3, 6, 18, 13), ink, c.S(2));
        r.FillRect(c.R(10, 19, 5, 3), ink);
        r.FillRect(c.R(7, 22, 11, 2), ink);

        r.DrawRect(c.R(21, 12, 8, 16), ink, c.S(2));
        r.FillRect(c.R(23.5f, 25, 3, 1.6f), ink);
    }

    /// <summary>The power symbol the Settings charm hangs its menu from.</summary>
    static void Power(Renderer2D r, C c)
    {
        var col = Color.Rgb(0x3A4250);

        // The ring is drawn as segments rather than a circle, so the gap the
        // bar rises through is simply a stretch that is never drawn.
        const int steps = 30;
        const float gap = 0.62f;
        float px = 0, py = 0;
        for (int i = 0; i <= steps; i++)
        {
            float a = -MathF.PI / 2 + gap + i * (MathF.PI * 2 - gap * 2) / steps;
            float x = c.X(16 + MathF.Cos(a) * 10), y = c.Y(18 + MathF.Sin(a) * 10);
            if (i > 0) r.Line(px, py, x, y, col, c.S(2.6f));
            px = x; py = y;
        }
        r.FillRect(c.R(14.6f, 4, 2.8f, 13), col);
    }

    /// <summary>«Люди» — two heads, for the contacts tile.</summary>
    static void People(Renderer2D r, C c)
    {
        r.FillCircle(c.X(11), c.Y(12), c.S(5.4f), Color.White);
        r.FillCircle(c.X(11), c.Y(26), c.S(9), Color.White);
        r.FillCircle(c.X(22), c.Y(13), c.S(4.4f), Color.Rgba(0xFFFFFF, 170));
        r.FillCircle(c.X(23), c.Y(26), c.S(7.4f), Color.Rgba(0xFFFFFF, 170));
    }

    /// <summary>«Погода» — sun behind a cloud, for the live tile.</summary>
    static void Weather(Renderer2D r, C c)
    {
        r.FillCircle(c.X(21), c.Y(11), c.S(6.5f), Color.Rgb(0xFFD24A));
        r.FillCircle(c.X(11), c.Y(21), c.S(7), Color.White);
        r.FillCircle(c.X(20), c.Y(21), c.S(6), Color.White);
        r.FillRect(c.R(11, 20, 9, 7), Color.White);
    }

    /// <summary>A padlock — the lock screen, and Win+L.</summary>
    static void Lock(Renderer2D r, C c)
    {
        // The padlock, flat: a shackle drawn as a ring and a body drawn as a
        // rectangle, in one colour.
        var ink = Color.Rgb(0x3A3A3A);
        r.DrawCircle(c.X(16), c.Y(12.5f), c.S(6), ink, c.S(2.4f));
        r.FillRect(c.R(8, 12, 16, 4), Color.Transparent);
        r.FillRect(c.R(6, 15, 20, 13), ink);
        r.FillCircle(c.X(16), c.Y(21), c.S(2.2f), Color.White);
    }

    /// <summary>Специальные возможности: the figure with its arms out, which
    /// is the mark accessibility has had since before any of this.</summary>
    static void Access(Renderer2D r, C c)
    {
        var col = MetroBlue;
        r.FillCircle(c.X(16), c.Y(16), c.S(13), col);
        r.FillCircle(c.X(16), c.Y(9.5f), c.S(2.6f), Color.White);
        // Arms.
        r.Line(c.X(9), c.Y(15), c.X(23), c.Y(15), Color.White, c.S(2.2f));
        // Body and legs.
        r.Line(c.X(16), c.Y(13), c.X(16), c.Y(19), Color.White, c.S(2.2f));
        r.Line(c.X(16), c.Y(19), c.X(12), c.Y(24), Color.White, c.S(2.2f));
        r.Line(c.X(16), c.Y(19), c.X(20), c.Y(24), Color.White, c.S(2.2f));
    }

    /// <summary>«Параметры компьютера» — a gear on a tile.</summary>
    static void PcSettings(Renderer2D r, C c)
    {
        Tile(r, c, MetroBlue);

        // A gear cut out of the tile in white: teeth as spokes, a ring, a hole.
        float cx = c.X(16), cy = c.Y(16);
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4;
            r.Line(cx + MathF.Cos(a) * c.S(6), cy + MathF.Sin(a) * c.S(6),
                   cx + MathF.Cos(a) * c.S(10), cy + MathF.Sin(a) * c.S(10),
                   Color.White, c.S(3));
        }
        r.FillCircle(cx, cy, c.S(7), Color.White);
        r.FillCircle(cx, cy, c.S(3), MetroBlue);
    }
}
