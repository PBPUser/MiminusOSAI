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

    VNode _target;
    object _targetOwner;

    public bool Dragging => Node != null;

    /// <summary>Starts carrying a node. Nothing happens on screen until the
    /// pointer has actually moved; sources decide that for themselves.</summary>
    public void Begin(VNode node, IconId icon, string label, object source)
    {
        if (node == null || Dragging) return;

        Node = node;
        _icon = icon;
        _label = label;
        Source = source;
        _target = null;
        _targetOwner = null;
    }

    public void Cancel()
    {
        Node = null;
        Source = null;
        _target = null;
        _targetOwner = null;
    }

    /// <summary>Offers a folder as the destination while the pointer is inside
    /// <paramref name="area"/>. Returns true when this offer is the one being
    /// taken, so the caller can light itself up.</summary>
    public bool Offer(UiContext c, Rect area, VNode folder, object owner)
    {
        if (!Dragging || folder == null) return false;
        if (!area.Contains(c.MouseX, c.MouseY)) return false;

        // Dropping something where it already lives is not a move.
        if (folder == Node.Parent || folder == Node) return false;

        _target = folder;
        _targetOwner = owner;
        return true;
    }

    /// <summary>True when this owner holds the offer taken this frame, which is
    /// only known after every layer has offered.</summary>
    public bool IsTarget(object owner) => _targetOwner == owner;

    /// <summary>Draws what is being carried and settles the drop. Called by the
    /// shell after every layer, so the topmost offer is the one in hand.</summary>
    public void Resolve(UiContext c)
    {
        if (!Dragging) return;

        // The pointer says what will happen if the button goes now.
        c.Cursor = _target != null ? CursorShape.Move : CursorShape.Arrow;

        DrawGhost(c);

        if (c.In.IsDown(MouseButton.Left)) return;

        var node = Node;
        var target = _target;
        Cancel();

        if (target == null) return;

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
    void DrawGhost(UiContext c)
    {
        var t = c.Theme;
        float x = c.MouseX + 12, y = c.MouseY + 10;

        string caption = _target != null
            ? L.F("fs.move_to", _label, _target.Name)
            : _label;

        float w = c.F.Small.Measure(caption) + 30;
        var box = new Rect(x, y, w, 22);

        c.R.FillRect(box.Offset(2, 2), Color.Rgba(0x000000, 40));
        c.R.FillRect(box, t.TooltipBack.WithAlpha(230));
        c.R.DrawRect(box, t.TooltipBorder);

        Icons.Draw(c.R, _icon, new Rect(box.X + 3, box.Y + 3, 16, 16));
        c.F.Small.Draw(c.R, caption, box.X + 23, box.CenterY - c.F.Small.Height * 0.5f, t.TooltipText);
    }
}
