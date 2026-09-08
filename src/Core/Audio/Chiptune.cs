namespace Miminus.Audio;

/// <summary>Builds the system's music: short looping chiptunes generated from a
/// seed, so anything that wants a tune has real audio without a file shipping
/// with it. The media player's playlist and the update tour both come from
/// here.</summary>
public static class Chiptune
{
    static readonly int[][] Progressions =
    {
        new[] { 57, 64, 60, 55 },   // Am  E   C   G
        new[] { 60, 67, 65, 62 },   // C   G   F   D
        new[] { 55, 62, 58, 53 },
        new[] { 62, 57, 60, 55 },
        new[] { 60, 60, 65, 67 },
    };

    /// <summary>One stretch of the logon track: how fast, how long, and how
    /// much of the kit is playing.</summary>
    readonly record struct Section(int Bpm, int Bars, int Drive);

    /// <summary>The song on the welcome screen.
    ///
    /// It refuses to settle on a tempo: eight sections, none of them longer
    /// than two bars, running from a half-time stomp at 96 up to a 240 blast
    /// and back down. The riff stays in A minor throughout so the tempo is the
    /// only thing that moves, which is what makes the changes land — the ear
    /// keeps the notes and loses the ground under them.
    ///
    /// <c>Drive</c> is how much of the kit joins in: 0 is bass and hats, 1 adds
    /// the backbeat, 2 puts a note on every eighth and doubles the lead an
    /// octave up.</summary>
    public static (short[] pcm, int rate) Welcome()
    {
        const int Rate = 22050;

        Section[] song =
        {
            new(150, 2, 0),      // in on the riff alone
            new(96,  1, 1),      // half time, everything lands hard
            new(176, 2, 1),
            new(240, 1, 2),      // the blast
            new(128, 2, 1),
            new(200, 2, 2),
            new(110, 1, 1),      // the floor drops out
            new(180, 2, 2),      // and back up to finish
        };

        // A minor: the riff is a root, a fifth, a flat seventh and back.
        int[] riff = { 57, 57, 64, 57, 60, 64, 67, 64 };

        double length = song.Sum(x => x.Bars * 4 * (60.0 / x.Bpm));
        var w = new Wave(length + 0.4, Rate);

        double t = 0;
        int step = 0;

        foreach (var section in song)
        {
            double beat = 60.0 / section.Bpm;
            int beats = section.Bars * 4;

            for (int i = 0; i < beats; i++, step++)
            {
                float now = (float)t;
                int note = riff[step % riff.Length];

                // Bass on the beat, an octave and a half down, short and hard.
                w.Square(Wave.Note(note - 24), 0.30f, now, (float)(beat * 0.85), 0.5f, 0.002f, 2.2f);

                // Kick under it: a low triangle with a click of noise on top.
                w.Triangle(Wave.Note(33), 0.42f, now, 0.11f, 0.001f, 7f);
                w.Noise(0.10f, now, 0.02f, 0.001f, 9f);

                // Backbeat once the section asks for it.
                if (section.Drive >= 1 && i % 2 == 1)
                    w.Noise(0.26f, now, 0.10f, 0.001f, 4.5f);

                // Hats: on every beat, and between them when it is driving.
                w.Noise(0.06f, now, 0.025f, 0.001f, 9f);
                if (section.Drive >= 1)
                    w.Noise(0.05f, (float)(t + beat * 0.5), 0.02f, 0.001f, 10f);

                // The riff itself, doubled up an octave at full drive.
                w.Square(Wave.Note(note), 0.13f, now, (float)(beat * 0.55), 0.35f, 0.002f, 2f);
                if (section.Drive >= 2)
                {
                    w.Square(Wave.Note(note + 12), 0.07f, (float)(t + beat * 0.5),
                             (float)(beat * 0.4), 0.25f, 0.002f, 2.4f);
                    w.Triangle(Wave.Note(note + 19), 0.05f, now, (float)(beat * 0.9), 0.004f, 1.8f);
                }

                t += beat;
            }

            // A noise sweep over the last beat of every section, so the tempo
            // change is announced rather than merely happening.
            w.Noise(0.16f, (float)(t - beat), (float)beat, (float)(beat * 0.8), 0.5f);
        }

        w.LowPass(9000).Delay(0.11f, 0.24f, 0.22f).Normalize(0.82f).Declick(0.008f);
        return (w.ToPcm16(), Rate);
    }

    /// <summary>The tune the «Что нового» tour plays: the same machinery, set
    /// slower and softer, so it sits under the reading rather than over it.
    ///
    /// A major progression that turns back on itself, a bass on the downbeat, a
    /// sparse bell line above it, and no percussion at all — the point is that
    /// the presentation has music, not that anyone notices it.</summary>
    public static (short[] pcm, int rate) Presentation()
    {
        const int Rate = 22050;
        const double Loop = 19.2;                 // 96 beats at the tempo below
        var w = new Wave(Loop, Rate);

        int[] chords = { 60, 55, 57, 53 };        // C  G  Am  F
        double beat = 0.4;
        int beats = (int)(Loop / beat);

        // A bell figure that walks the chord and comes back down.
        int[] figure = { 0, 7, 12, 16, 12, 7 };

        for (int i = 0; i < beats; i++)
        {
            double t = i * beat;
            int chord = chords[(i / 6) % chords.Length];

            if (i % 6 == 0)
                w.Triangle(Wave.Note(chord - 24), 0.22f, (float)t, (float)(beat * 5.4),
                           0.006f, 1.1f);

            int note = chord + figure[i % figure.Length];
            w.Triangle(Wave.Note(note + 12), 0.10f, (float)t, (float)(beat * 1.4),
                       0.008f, 1.5f);

            // A held fifth under every other bar, for something to sit on.
            if (i % 12 == 0)
                w.Square(Wave.Note(chord - 12), 0.05f, (float)t, (float)(beat * 11f),
                         0.5f, 0.05f, 0.8f);
        }

        w.LowPass(4200).Delay(0.32f, 0.28f, 0.4f).Normalize(0.45f).Declick(0.02f);
        return (w.ToPcm16(), Rate);
    }

    /// <summary>Returns a mono PCM loop of roughly <paramref name="seconds"/> and
    /// its sample rate.</summary>
    public static (short[] pcm, int rate) Build(int seed, double seconds)
    {
        const int Rate = 22050;
        double loop = Math.Min(seconds, 16);          // one bar-set, looped by OpenAL
        var w = new Wave(loop, Rate);

        int[] chords = Progressions[Math.Abs(seed) % Progressions.Length];
        var rng = new Random(seed * 7919 + 13);

        double beat = 0.25;
        int beats = (int)(loop / beat);

        for (int i = 0; i < beats; i++)
        {
            double t = i * beat;
            int chord = chords[(i / 8) % chords.Length];

            // Bass on every other beat.
            if (i % 2 == 0)
                w.Square(Wave.Note(chord - 24), 0.20f, (float)t, (float)(beat * 1.6), 0.5f, 0.004f, 1.2f);

            // Arpeggiated lead.
            int[] offsets = { 0, 4, 7, 12 };
            int note = chord + offsets[rng.Next(offsets.Length)];
            w.Triangle(Wave.Note(note), 0.16f, (float)t, (float)(beat * 0.9), 0.004f, 1.6f);

            // Counter-melody an octave up, sparsely.
            if (rng.NextDouble() < 0.35)
                w.Square(Wave.Note(note + 12), 0.07f, (float)(t + beat * 0.5), (float)(beat * 0.45),
                         0.25f, 0.003f, 2f);

            // Percussion.
            if (i % 4 == 0) w.Noise(0.16f, (float)t, 0.06f, 0.001f, 4f);
            if (i % 4 == 2) w.Noise(0.09f, (float)t, 0.04f, 0.001f, 5f);
        }

        w.LowPass(7000).Delay(0.18f, 0.22f, 0.35f).Normalize(0.7f).Declick(0.01f);
        return (w.ToPcm16(), Rate);
    }
}
