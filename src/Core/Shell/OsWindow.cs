using Miminus.Graphics;
using Miminus.UI;

namespace Miminus.Shell;

public enum WindowState { Normal, Minimized, Maximized }

/// <summary>Base class for every pseudo-window in the OS.
///
/// A window owns nothing but its rectangle and its content: the frame, caption,
/// buttons, resizing and z-order all belong to <see cref="WindowManager"/>.
/// Subclasses implement <see cref="DrawClient"/> and, if they want one, expose a
/// <see cref="Menu"/>.</summary>
public abstract class OsWindow
{
    static int _nextId = 1;

    public readonly string Id = "win" + _nextId++;

    public IconId Icon = IconId.Program;
    public Rect Bounds;
    public Rect RestoreBounds;
    public WindowState State = WindowState.Normal;

    public bool Resizable = true;
    public bool Minimizable = true;
    public bool Maximizable = true;
    public bool ShowInTaskbar = true;
    public bool Modal;
    public OsWindow Owner;

    /// <summary>Set to true to have the manager drop the window this frame.</summary>
    public bool Closed;

    public MenuBar Menu;

    /// <summary>Id of the program that created this window, set by the launcher.</summary>
    public string ProgramId;

    /// <summary>Name shown in Task Manager's process list.</summary>
    public virtual string ProcessName => (ProgramId ?? "miminus") + ".exe";

    public virtual float MinWidth => 200;
    public virtual float MinHeight => 120;

    /// <summary>Caption text. Implemented as a property so it can follow the
    /// document name and the current language.</summary>
    public abstract string Title { get; }

    /// <summary>Text for this window's taskbar button; defaults to the title.</summary>
    public virtual string TaskbarTitle => Title;

    public WindowManager Wm;
    public ShellHost Shell;

    public bool Active => Wm != null && Wm.Focused == this;

    /// <summary>Client-area painting and interaction.</summary>
    public abstract void DrawClient(UiContext c, Rect client);

    /// <summary>Runs every frame even when the window is minimised or behind
    /// others, for animation and background work.</summary>
    public virtual void Tick(UiContext c, float dt) { }

    /// <summary>Return false to veto closing (used by "save changes?" prompts).</summary>
    public virtual bool OnClosing(UiContext c) => true;

    public virtual void OnActivated() { }
    public virtual void OnClosed() { }

    /// <summary>Called after the window is placed, once its size is final.</summary>
    public virtual void OnOpened(UiContext c) { }

    public void Close() => Closed = true;

    public void CenterOn(int screenW, int screenH, float taskbarH)
    {
        Bounds.X = MathF.Round((screenW - Bounds.W) * 0.5f);
        Bounds.Y = MathF.Round((screenH - taskbarH - Bounds.H) * 0.5f);
        if (Bounds.Y < 0) Bounds.Y = 0;
    }
}

/// <summary>Buttons a message box can offer.</summary>
[Flags]
public enum MsgButtons { Ok = 1, Cancel = 2, Yes = 4, No = 8 }

public enum MsgResult { None, Ok, Cancel, Yes, No }

/// <summary>The standard modal message box: icon, wrapped text, a button row.
/// Used for the "save changes?" prompt, the antivirus results, error stings.</summary>
public sealed class MessageBoxWindow : OsWindow
{
    readonly string _title;
    readonly string _message;
    readonly MsgButtons _buttons;
    readonly IconId _badge;
    readonly Action<MsgResult> _onResult;
    readonly Audio.Sfx _sound;
    bool _played;

    public override string Title => _title;
    public override float MinWidth => 240;
    public override float MinHeight => 110;

    public MessageBoxWindow(string title, string message, MsgButtons buttons, IconId badge,
                            Action<MsgResult> onResult, Audio.Sfx sound = Audio.Sfx.Info)
    {
        _title = title;
        _message = message;
        _buttons = buttons;
        _badge = badge;
        _onResult = onResult;
        _sound = sound;

        Icon = badge;
        Modal = true;
        Resizable = false;
        Maximizable = false;
        Minimizable = false;
        ShowInTaskbar = false;
        Bounds = new Rect(0, 0, 380, 160);
    }

    public override void OnOpened(UiContext c)
    {
        // Size to the text, then centre.
        float textW = 300;
        var lines = c.F.Ui.Wrap(_message, textW);
        float h = MathF.Max(48, lines.Count * (c.F.Ui.Height + 3));
        Bounds.W = textW + 48 + 40;
        Bounds.H = c.Theme.CaptionHeight + h + 76;
        CenterOn(c.ScreenW, c.ScreenH, c.Theme.TaskbarHeight);
    }

    void Finish(UiContext c, MsgResult result)
    {
        Close();
        _onResult?.Invoke(result);
    }

    public override void DrawClient(UiContext c, Rect client)
    {
        if (!_played) { _played = true; c.Sound(_sound, 0.9f); }

        c.R.FillRect(client, c.Theme.Face);

        var body = client.Deflate(14, 14, 14, 52);
        var iconRect = new Rect(body.X, body.Y, 32, 32);
        Icons.Draw(c.R, _badge, iconRect);

        float tx = body.X + 48;
        var lines = c.F.Ui.Wrap(_message, body.Right - tx);
        float ty = body.Y;
        foreach (string line in lines)
        {
            c.F.Ui.Draw(c.R, line, tx, ty, c.Theme.Text);
            ty += c.F.Ui.Height + 3;
        }

        // Button row, right-aligned, in Windows order.
        var row = new Rect(client.X, client.Bottom - 42, client.W, 26);
        var order = new List<(MsgButtons flag, string label, MsgResult result)>
        {
            (MsgButtons.Ok, Sys.L.T("win.ok"), MsgResult.Ok),
            (MsgButtons.Yes, Sys.L.T("win.yes"), MsgResult.Yes),
            (MsgButtons.No, Sys.L.T("win.no"), MsgResult.No),
            (MsgButtons.Cancel, Sys.L.T("win.cancel"), MsgResult.Cancel),
        };
        var shown = order.Where(o => (_buttons & o.flag) != 0).ToList();

        float bw = 78, gap = 8;
        float x = row.Right - 14 - shown.Count * bw - (shown.Count - 1) * gap;
        bool first = true;
        foreach (var (_, label, result) in shown)
        {
            var br = new Rect(x, row.Y, bw, row.H);
            if (W.Button(c, Id + ".b" + label, br, label, true, IconId.None, first))
                Finish(c, result);
            x += bw + gap;
            first = false;
        }

        if (!c.KeyboardHandled)
        {
            if (c.In.KeyPressed(Platform.Keys.Escape))
            {
                var fallback = (_buttons & MsgButtons.Cancel) != 0 ? MsgResult.Cancel
                             : (_buttons & MsgButtons.No) != 0 ? MsgResult.No
                             : MsgResult.Ok;
                Finish(c, fallback);
            }
            else if (c.In.KeyPressed(Platform.Keys.Enter))
            {
                Finish(c, shown[0].result);
            }
        }
    }
}
