using Miminus.Graphics;
using Miminus.UI;

namespace Miminus.Sys;

/// <summary>What the second monitor does when there is one.</summary>
public enum MultiMode
{
    /// <summary>One screen. The second monitor is plugged in and dark, which is
    /// the state most second monitors spend most of their lives in.</summary>
    Single,

    /// <summary>The desktop runs across both, and a window can be carried from
    /// one to the other.</summary>
    Extend,

    /// <summary>Both monitors show the same thing, so the second one is a
    /// picture of the first.</summary>
    Duplicate,
}

/// <summary>One display: where it is, and whether anything is on it.</summary>
public sealed class Display
{
    public int Index;
    public Rect Bounds;
    public bool Primary;
    public bool Enabled = true;

    /// <summary>What «Определить» prints on it.</summary>
    public string Label => (Index + 1).ToString();
}

/// <summary>The monitors this machine thinks it has.
///
/// It has one real window, so the second monitor is a region of that window
/// rather than a second piece of glass — but everything the system does with a
/// monitor it does with these: a maximised window fills the one it is on and not
/// the other, snapping snaps to that monitor's halves, the taskbar and the icons
/// stay on the primary, and the wallpaper is painted once per screen with a
/// seam drawn between them.
///
/// That is the whole of what multiple monitors are, as far as a shell is
/// concerned: the screen stops being one rectangle. Which of the two is on the
/// left, which one is primary, and whether the second is extended, duplicated or
/// dark are all real answers with real consequences, and they are chosen in
/// «Разрешение экрана» exactly as they were.</summary>
public sealed class DisplayLayout
{
    /// <summary>How the second monitor is being used.</summary>
    public MultiMode Mode = MultiMode.Single;

    /// <summary>True when the second monitor is the one on the left, which is
    /// the arrangement half of all desks end up in.</summary>
    public bool SecondOnLeft;

    /// <summary>Which of the two carries the taskbar and the icons.</summary>
    public int PrimaryIndex;

    /// <summary>When «Определить» was pressed, so the big numbers can fade.</summary>
    public double IdentifiedAt = -10;

    /// <summary>True while a second monitor is actually showing something.</summary>
    public bool Extended => Mode == MultiMode.Extend;

    /// <summary>How many screens the shell should treat itself as having.</summary>
    public int Count => Extended ? 2 : 1;

    readonly Display[] _monitors =
    {
        new() { Index = 0, Primary = true },
        new() { Index = 1 },
    };

    /// <summary>The monitors, with their rectangles worked out for the screen
    /// size given. Extended, the screen is cut in two down the middle; on one
    /// screen the second monitor has no rectangle at all.</summary>
    public IReadOnlyList<Display> All(int screenW, int screenH)
    {
        float half = MathF.Round(screenW * 0.5f);

        if (!Extended)
        {
            _monitors[0].Bounds = new Rect(0, 0, screenW, screenH);
            _monitors[1].Bounds = default;
            _monitors[1].Enabled = false;
        }
        else
        {
            // The second monitor sits on whichever side the arrangement says.
            var left = new Rect(0, 0, half, screenH);
            var right = new Rect(half, 0, screenW - half, screenH);

            _monitors[0].Bounds = SecondOnLeft ? right : left;
            _monitors[1].Bounds = SecondOnLeft ? left : right;
            _monitors[1].Enabled = true;
        }

        _monitors[0].Primary = PrimaryIndex == 0 || !Extended;
        _monitors[1].Primary = PrimaryIndex == 1 && Extended;
        return _monitors;
    }

    public Display Primary(int screenW, int screenH)
    {
        var all = All(screenW, screenH);
        return all.FirstOrDefault(m => m.Primary && m.Enabled) ?? all[0];
    }

    /// <summary>Which monitor a point is on. Off the end of everything, the
    /// primary answers — the shell always has somewhere to put a thing.</summary>
    public Display At(int screenW, int screenH, float x, float y)
    {
        foreach (var m in All(screenW, screenH))
            if (m.Enabled && m.Bounds.Contains(x, y)) return m;
        return Primary(screenW, screenH);
    }

    /// <summary>Which monitor a window is mostly on, which is the one it
    /// maximises onto and snaps inside.</summary>
    public Display Of(int screenW, int screenH, Rect window)
    {
        Display best = null;
        float bestArea = -1;

        foreach (var m in All(screenW, screenH))
        {
            if (!m.Enabled) continue;
            var hit = m.Bounds.Intersect(window);
            float area = MathF.Max(0, hit.W) * MathF.Max(0, hit.H);
            if (area > bestArea) { bestArea = area; best = m; }
        }

        return best ?? Primary(screenW, screenH);
    }
}
