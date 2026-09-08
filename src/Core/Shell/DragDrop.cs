using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Carrying a file, folder or shortcut from one place to another.
///
/// The drag crosses windows — desktop to folder, folder to desktop, folder to
/// folder — so it cannot belong to any one of them. Sources call
/// <see cref="Begin"/>; anything that can receive a drop calls
/// <see cref="Offer"/> while the pointer is over it, and the last offer of the
/// frame wins, which is the topmost one because layers draw back to front. The
/// shell resolves the release once every layer has had its say.</summary>
public sealed class DragDropHost
{
    readonly ShellHost _shell;

    public DragDropHost(ShellHost shell) => _shell = shell;

    /// <summary>What is being carried, or null.</summary>
    public VNode Node { get; private set; }

    /// <summary>Where it came from, so a source can tell its own drag apart.</summary>
    public object Source { get; private set; }

    IconId _icon;
    string _label;

    /// <summary>The program the thing being carried starts, when it is a
    /// shortcut to one. The taskbar wants this rather than the file.</summary>
    public string Launch { get; private set; }

    VNode _target;
    object _targetOwner;

    /// <summary>Set while the pointer is over something that pins rather than
    /// moves — the taskbar. It outranks a folder offered in the same frame,
    /// because the bar is drawn over everything the desktop has.</summary>
    bool _pin;
    object _pinOwner;

    public bool Dragging => Node != null || Launch != null;

    /// <summary>Starts carrying a node, a program, or both.
    ///
    /// Most of what is dragged is a file, and a file is what gets moved. A
    /// desktop shortcut to a program has no file behind it at all — it is a
    /// name and an icon — and it can still be carried, because there is one
    /// place that wants the program rather than the file: the taskbar.
    ///
    /// Nothing happens on screen until the pointer has actually moved; sources
    /// decide that for themselves.</summary>
    public void Begin(VNode node, IconId icon, string label, object source,
                      string launch = null)
    {
        if ((node == null && string.IsNullOrEmpty(launch)) || Dragging) return;

        Node = node;
        _icon = icon;
        _label = label;
        Launch = launch;
        Source = source;
        _target = null;
        _targetOwner = null;
        _pin = false;
        _pinOwner = null;
    }

    public void Cancel()
    {
        Node = null;
        Source = null;
        Launch = null;
        _target = null;
        _targetOwner = null;
        _pin = false;
        _pinOwner = null;
    }

    /// <summary>Offers a folder as the destination while the pointer is inside
    /// <paramref name="area"/>. Returns true when this offer is the one being
    /// taken, so the caller can light itself up.</summary>
    public bool Offer(UiContext c, Rect area, VNode folder, object owner)
    {
        if (!Dragging || Node == null || folder == null || _pin) return false;
        if (!area.Contains(c.MouseX, c.MouseY)) return false;

        // Dropping something where it already lives is not a move.
        if (folder == Node.Parent || folder == Node) return false;

        _target = folder;
        _targetOwner = owner;
        return true;
    }

    /// <summary>Offers a strip that pins rather than receives — the taskbar,
    /// and nothing else so far. A pin offer beats any folder offered in the
    /// same frame: the bar is above everything the desktop is showing, so the
    /// pointer being on it means the pointer is on it.</summary>
    public bool OfferPin(UiContext c, Rect area, object owner)
    {
        if (!Dragging) return false;
        if (!area.Contains(c.MouseX, c.MouseY)) return false;

        _pin = true;
        _pinOwner = owner;
        _target = null;
        _targetOwner = null;
        return true;
    }

    /// <summary>True when this owner holds the offer taken this frame, which is
    /// only known after every layer has offered.</summary>
    public bool IsTarget(object owner) => _targetOwner == owner || _pinOwner == owner;

    /// <summary>Draws what is being carried and settles the drop. Called by the
    /// shell after every layer, so the topmost offer is the one in hand.</summary>
    public void Resolve(UiContext c)
    {
        if (!Dragging) return;

        // The offers are made afresh every frame, so this one is spent as soon
        // as it has been read.
        bool pin = _pin;
        _pin = false;

        // The pointer says what will happen if the button goes now.
        c.Cursor = pin || _target != null ? CursorShape.Move : CursorShape.Arrow;

        DrawGhost(c, pin);

        if (c.In.IsDown(MouseButton.Left)) return;

        var node = Node;
        var target = _target;
        string launch = Launch;
        Cancel();

        if (pin) { _shell.Taskbar.AcceptDrop(c, node, launch); return; }
        if (target == null || node == null) return;

        if (_shell.Fs.Move(node, target, out string error))
        {
            _shell.Desktop.NodeMoved(c, node);
            c.Sound(Sfx.Navigate, 0.5f);
            return;
        }

        if (error != null)
            _shell.MessageBox(c, L.T("fs.move_title"), error,
                              MsgButtons.Ok, IconId.DlgWarning, null, Sfx.Warning);
    }

    /// <summary>The half-transparent copy that follows the pointer, with the
    /// name of the folder it is about to go into.</summary>
    void DrawGhost(UiContext c, bool pin)
    {
        var t = c.Theme;
        float x = c.MouseX + 12, y = c.MouseY + 10;

        string caption = pin ? L.F("taskbar.pin_to_bar", _label)
            : _target != null ? L.F("fs.move_to", _label, _target.Name)
            : _label;

        float w = c.F.Small.Measure(caption) + 30;

        // Carried down to the taskbar, the label would fall off the bottom of
        // the screen, so it flips above the pointer instead — and it is kept
        // inside the right edge for the same reason.
        if (y + 22 > c.ScreenH - 2) y = c.MouseY - 30;
        if (x + w > c.ScreenW - 2) x = MathF.Max(2, c.MouseX - w - 12);

        var box = new Rect(x, y, w, 22);

        c.R.FillRect(box.Offset(2, 2), Color.Rgba(0x000000, 40));
        c.R.FillRect(box, t.TooltipBack.WithAlpha(230));
        c.R.DrawRect(box, t.TooltipBorder);

        Icons.Draw(c.R, _icon, new Rect(box.X + 3, box.Y + 3, 16, 16));
        c.F.Small.Draw(c.R, caption, box.X + 23, box.CenterY - c.F.Small.Height * 0.5f, t.TooltipText);
    }
}
