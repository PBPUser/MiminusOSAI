using Miminus.Audio;
using Miminus.Graphics;
using Miminus.Platform;

namespace Miminus.UI;

/// <summary>The typefaces the shell draws with. Two families are baked — Tahoma
/// for the XP themes and Segoe UI for "Миминус 7" and "Миминус 8" — and
/// <see cref="Use"/> picks between them when the theme changes.</summary>
public sealed class Fonts : IDisposable
{
    readonly Font _tahoma, _tahomaBold, _tahomaSmall;
    readonly Font _segoe, _segoeBold, _segoeSmall;
    readonly Font _caption, _captionSeven;

    public Font Mono { get; }
    public Font MonoSmall { get; }
    public Font Big { get; }
    public Font Huge { get; }
    public Font Led { get; }

    public Font Ui { get; private set; }
    public Font UiBold { get; private set; }
    public Font Small { get; private set; }
    public Font Caption { get; private set; }

    public Fonts()
    {
        _tahoma = new Font("Tahoma", 11);
        _tahomaBold = new Font("Tahoma", 11, bold: true);
        _tahomaSmall = new Font("Tahoma", 10);
        _caption = new Font("Trebuchet MS", 13, bold: true);

        _segoe = new Font("Segoe UI", 12);
        _segoeBold = new Font("Segoe UI", 12, bold: true);
        _segoeSmall = new Font("Segoe UI", 11);
        _captionSeven = new Font("Segoe UI", 13);

        Mono = new Font("Consolas", 13);
        MonoSmall = new Font("Consolas", 12);
        Big = new Font("Arial", 34, bold: true);
        Huge = new Font("Arial", 92, bold: true);
        Led = new Font("Consolas", 20, bold: true);

        Use(ThemeId.LunaBlue);
    }

    public void Use(ThemeId id)
    {
        bool seven = id is ThemeId.Seven or ThemeId.Metro;
        Ui = seven ? _segoe : _tahoma;
        UiBold = seven ? _segoeBold : _tahomaBold;
        Small = seven ? _segoeSmall : _tahomaSmall;
        Caption = seven ? _captionSeven : _caption;
    }

    public void Dispose()
    {
        _tahoma.Dispose(); _tahomaBold.Dispose(); _tahomaSmall.Dispose(); _caption.Dispose();
        _segoe.Dispose(); _segoeBold.Dispose(); _segoeSmall.Dispose(); _captionSeven.Dispose();
        Mono.Dispose(); MonoSmall.Dispose(); Big.Dispose(); Huge.Dispose(); Led.Dispose();
    }
}

/// <summary>Everything a widget or window needs for one frame.
///
/// The UI is immediate-mode: widgets are function calls that both draw and
/// report interaction. The little persistent state some of them need (caret
/// position, scroll offset, drag anchors) lives in <see cref="State{T}"/>,
/// keyed by a caller-supplied id, so nothing has to be constructed up front.
///
/// Layering is handled by <see cref="MouseHandled"/>: the shell updates from the
/// topmost layer down, and once a layer claims the pointer everything beneath it
/// stops responding.</summary>
public sealed class UiContext
{
    public Renderer2D R;
    public Fonts F;
    public InputState In;
    public AudioEngine Audio;
    public Theme Theme;

    /// <summary>The shell's popup host. Widgets that need to float above every
    /// window — drop-downs especially — open through this rather than drawing
    /// inline, so they are painted last and get first refusal on the pointer.</summary>
    public MenuHost Menus;

    public double Time;
    public float Dt;
    public int ScreenW, ScreenH;

    /// <summary>Set once a layer has taken the pointer this frame.</summary>
    public bool MouseHandled;

    /// <summary>Set once a layer has taken the keyboard this frame.</summary>
    public bool KeyboardHandled;

    /// <summary>Id of the control holding keyboard focus, or null.</summary>
    public string Focus;

    /// <summary>Id of the control currently being dragged, or null.</summary>
    public string ActiveDrag;

    /// <summary>Tooltip requested this frame; drawn last by the shell.</summary>
    public string TooltipText;
    public float TooltipX, TooltipY;

    /// <summary>Set by widgets that want a text caret rather than an arrow.</summary>
    public CursorShape Cursor;

    readonly Dictionary<string, object> _state = new();

    public float MouseX => In.MouseX;
    public float MouseY => In.MouseY;

    public T State<T>(string id) where T : new()
    {
        if (_state.TryGetValue(id, out object v) && v is T t) return t;
        var fresh = new T();
        _state[id] = fresh;
        return fresh;
    }

    public void ForgetState(string idPrefix)
    {
        var doomed = _state.Keys.Where(k => k.StartsWith(idPrefix, StringComparison.Ordinal)).ToList();
        foreach (string k in doomed) _state.Remove(k);
    }

    public bool Hovering(Rect r) => !MouseHandled && r.Contains(In.MouseX, In.MouseY);

    /// <summary>True on the frame the primary button goes down inside the rect.
    /// Claims the pointer so lower layers ignore the same click.</summary>
    public bool Clicked(Rect r)
    {
        if (MouseHandled || !r.Contains(In.MouseX, In.MouseY)) return false;
        if (!In.Pressed(MouseButton.Left)) return false;
        MouseHandled = true;
        return true;
    }

    public bool RightClicked(Rect r)
    {
        if (MouseHandled || !r.Contains(In.MouseX, In.MouseY)) return false;
        if (!In.Pressed(MouseButton.Right)) return false;
        MouseHandled = true;
        return true;
    }

    public bool DoubleClicked(Rect r)
    {
        if (MouseHandled || !r.Contains(In.MouseX, In.MouseY)) return false;
        if (!In.DoubleClicked(MouseButton.Left)) return false;
        MouseHandled = true;
        return true;
    }

    public void Tooltip(Rect anchor, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!anchor.Contains(In.MouseX, In.MouseY)) return;
        TooltipText = text;
        TooltipX = In.MouseX + 12;
        TooltipY = anchor.Bottom + 2;
    }

    public void Sound(Sfx id, float gain = 1f, float pitch = 1f) => Audio?.Play(id, gain, pitch);

    public void SoundAt(Sfx id, Rect where, float gain = 1f, float pitch = 1f)
        => Audio?.PlayAt(id, where.CenterX, where.CenterY, gain, pitch);
}

public enum CursorShape { Arrow, Text, SizeNS, SizeWE, SizeNWSE, SizeNESW, Hand, Wait, Cross, Move }
