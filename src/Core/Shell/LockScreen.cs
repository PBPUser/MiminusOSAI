using Miminus.Graphics;
using Miminus.Platform;
using Miminus.Sys;
using Miminus.UI;

namespace Miminus.Shell;

/// <summary>Экран блокировки — what version 8 shows before it will admit there
/// is a computer behind it.
///
/// The arrangement is the original's, which is more particular than it looks: a
/// picture filling the screen, the time in enormous thin numerals a third of the
/// way down and a third of the way in, the date directly under it in a size that
/// looks like a mistake until you see the real one, and a row of small status
/// glyphs beneath both — network, unread mail, battery. Nothing is centred and
/// nothing is boxed.
///
/// It is not a login screen and asks for nothing; it only has to be got rid of,
/// by a key, a click, or by dragging it up out of the way — and when it goes, it
/// goes upward, taking the picture with it and leaving the logon screen
/// underneath.</summary>
public sealed class LockScreen
{
    /// <summary>How far the curtain has been lifted, in pixels. Dragged by the
    /// pointer, and thrown the rest of the way once it is high enough.</summary>
    float _lift;
    bool _dragging;
    float _dragFrom;
    bool _throwing;

    public void Reset()
    {
        _lift = 0;
        _dragging = false;
        _throwing = false;
    }

    /// <summary>Draws the curtain and moves it. Returns true once it is clear
    /// of the screen and the logon screen behind it should take over.</summary>
    public bool Draw(UiContext c, DateTime now, int unread)
    {
        float screenH = c.ScreenH;

        if (_throwing) _lift += c.Dt * MathF.Max(900, screenH * 1.6f);
        else if (!_dragging && _lift > 0) _lift = MathF.Max(0, _lift - c.Dt * 700);

        var curtain = new Rect(0, -_lift, c.ScreenW, screenH);
        DrawPicture(c, curtain);

        // ---- the time, the date, and the glyphs, in that column -------------
        float x = MathF.Max(56, c.ScreenW * 0.09f);
        float y = curtain.Y + screenH * 0.26f;

        string time = L.Time(now);
        c.F.Huge.Draw(c.R, time, x + 3, y + 3, Color.Rgba(0x000000, 70));
        c.F.Huge.Draw(c.R, time, x, y, Color.White);

        // The date sits under the numerals rather than under the line box: the
        // largest face carries a great deal of air below its baseline.
        float dateY = y + c.F.Huge.Height * 0.84f;
        string date = L.LongDate(now);
        c.F.Big.Draw(c.R, date, x + 5, dateY + 2, Color.Rgba(0x000000, 60));
        c.F.Big.Draw(c.R, date, x + 3, dateY, Color.Rgba(0xFFFFFF, 235));

        DrawStatus(c, x + 4, dateY + c.F.Big.Height + 22, unread);

        // ---- the hint, along the bottom -------------------------------------
        string hint = L.T("lock.hint");
        float hw = c.F.Ui.Measure(hint);
        float pulse = 0.55f + 0.45f * MathF.Abs(MathF.Sin((float)c.Time * 1.4f));
        c.F.Ui.Draw(c.R, hint, (c.ScreenW - hw) * 0.5f, curtain.Bottom - 46,
                    Color.Rgba(0xFFFFFF, (byte)(200 * pulse)));

        HandleInput(c, screenH);
        return _lift >= screenH;
    }

    /// <summary>The row under the date: how the network is, whether there is
    /// mail, and how much battery there is. Version 8 drew these small and grey
    /// and never explained them.</summary>
    static void DrawStatus(UiContext c, float x, float y, int unread)
    {
        var net = new Rect(x, y, 24, 24);
        Icons.Draw(c.R, IconId.TrayNetwork, net);

        var mail = new Rect(net.Right + 26, y, 24, 24);
        Icons.Draw(c.R, IconId.Mail, mail);
        if (unread > 0)
            c.F.Caption.Draw(c.R, unread.ToString(), mail.Right + 6, mail.Y + 3,
                             Color.Rgba(0xFFFFFF, 230));

        // The battery: a box, a nub, and a fill that is not quite full.
        var battery = new Rect(mail.Right + 58, y + 5, 36, 16);
        c.R.DrawRect(battery, Color.Rgba(0xFFFFFF, 210));
        c.R.FillRect(new Rect(battery.X + 2, battery.Y + 2, (battery.W - 4) * 0.82f, battery.H - 4),
                     Color.Rgba(0xFFFFFF, 210));
        c.R.FillRect(new Rect(battery.Right, battery.CenterY - 4, 3, 8), Color.Rgba(0xFFFFFF, 210));
        c.F.Small.Draw(c.R, "82%", battery.Right + 12, battery.Y + 1, Color.Rgba(0xFFFFFF, 190));
    }

    /// <summary>The picture behind it: a flat field with a skyline along the
    /// bottom, built the same way the wallpapers are and for the same reason —
    /// nothing in this system is loaded from disk.</summary>
    static void DrawPicture(UiContext c, Rect r)
    {
        var accent = Theme.MetroAccent;

        // The field takes its colour from the accent chosen during setup, so
        // the lock screen belongs to the same machine as everything else.
        c.R.FillRectV(r, accent.Shade(0.34f), accent.Shade(0.14f));

        // A wash of light from the upper left, which is all the depth there is.
        c.R.FillRectH(new Rect(r.X, r.Y, r.W * 0.6f, r.H),
                      Color.Rgba(0xFFFFFF, 22), Color.Transparent);

        float baseY = r.Bottom - r.H * 0.22f;
        int seed = 7;
        float x = r.X - 40;

        while (x < r.Right + 40)
        {
            seed = seed * 1103515245 + 12345;
            float w = 40 + ((seed >> 16) & 0x3F);
            seed = seed * 1103515245 + 12345;
            float h = 30 + ((seed >> 16) & 0x7F);

            c.R.FillRect(new Rect(x, baseY - h, w - 6, h + r.H), accent.Shade(0.18f));

            // A few lit windows, which is what makes it read as a city.
            for (float wy = baseY - h + 8; wy < baseY - 8; wy += 12)
                for (float wx = x + 6; wx < x + w - 14; wx += 10)
                {
                    seed = seed * 1103515245 + 12345;
                    if (((seed >> 20) & 7) > 4)
                        c.R.FillRect(new Rect(wx, wy, 4, 6), Color.Rgba(0xFFD98A, 150));
                }
            x += w;
        }
    }

    void HandleInput(UiContext c, float screenH)
    {
        if (_throwing) return;

        // Dragged up with the pointer, the way a curtain is.
        if (c.In.Pressed(MouseButton.Left))
        {
            _dragging = true;
            _dragFrom = c.MouseY + _lift;
        }
        else if (_dragging && c.In.IsDown(MouseButton.Left))
        {
            _lift = MathF.Max(0, _dragFrom - c.MouseY);
        }
        else if (_dragging)
        {
            _dragging = false;
            // Far enough up, and it goes the rest of the way on its own; a
            // click that never moved counts as far enough, because a click is
            // also how the original was dismissed.
            if (_lift > screenH * 0.14f || _lift < 4) _throwing = true;
        }

        if (c.In.TypedChars.Count > 0 || c.In.KeyPressed(Keys.Enter) ||
            c.In.KeyPressed(Keys.Space) || c.In.KeyPressed(Keys.Escape))
            _throwing = true;

        if (c.In.WheelDelta > 0.01f)
        {
            _lift += c.In.WheelDelta * 90;
            c.In.WheelDelta = 0;
            if (_lift > screenH * 0.14f) _throwing = true;
        }
    }
}
