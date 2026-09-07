namespace Miminus.Audio;

/// <summary>A mono scratch buffer with just enough DSP to build UI sounds.
///
/// Every sound in the OS is generated here at startup — no WAV files ship with
/// the project. Partials are summed into a float buffer, shaped by an envelope,
/// optionally run through a one-pole filter and a feedback delay, then converted
/// to 16-bit PCM for OpenAL.</summary>
public sealed class Wave
{
    public readonly float[] Data;
    public readonly int Rate;

    public Wave(double seconds, int rate = 44100)
    {
        Rate = rate;
        Data = new float[Math.Max(1, (int)(seconds * rate))];
    }

    public double Length => Data.Length / (double)Rate;

    /// <summary>Attack/decay envelope with an exponential tail — the shape almost
    /// every short UI chirp wants.</summary>
    static float Env(float t, float dur, float attack, float curve)
    {
        if (t < 0 || t > dur) return 0;
        float a = attack <= 0 ? 1 : MathF.Min(1, t / attack);
        float rel = MathF.Max(0, 1 - (t - attack) / MathF.Max(0.0001f, dur - attack));
        return a * MathF.Pow(rel, curve);
    }

    public Wave Sine(float freq, float amp, float start, float dur, float attack = 0.005f, float curve = 2f, float phase = 0)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            Data[i] += MathF.Sin(2 * MathF.PI * freq * t + phase) * amp * Env(t, dur, attack, curve);
        }
        return this;
    }

    /// <summary>Frequency glide, used for whooshes and the shutdown fall.</summary>
    public Wave Sweep(float f0, float f1, float amp, float start, float dur, float attack = 0.01f, float curve = 2f)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        float ph = 0;
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            float f = f0 + (f1 - f0) * (t / dur);
            ph += 2 * MathF.PI * f / Rate;
            Data[i] += MathF.Sin(ph) * amp * Env(t, dur, attack, curve);
        }
        return this;
    }

    /// <summary>Two-operator FM — gives bell and metallic timbres from one line.</summary>
    public Wave Fm(float carrier, float ratio, float index, float amp, float start, float dur,
                   float attack = 0.005f, float curve = 2.5f)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            float e = Env(t, dur, attack, curve);
            float mod = MathF.Sin(2 * MathF.PI * carrier * ratio * t) * index * e;
            Data[i] += MathF.Sin(2 * MathF.PI * carrier * t + mod) * amp * e;
        }
        return this;
    }

    public Wave Square(float freq, float amp, float start, float dur, float duty = 0.5f, float attack = 0.003f, float curve = 1.5f)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            float ph = (freq * t) % 1f;
            Data[i] += (ph < duty ? 1f : -1f) * amp * Env(t, dur, attack, curve);
        }
        return this;
    }

    public Wave Triangle(float freq, float amp, float start, float dur, float attack = 0.003f, float curve = 1.5f)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            float ph = (freq * t) % 1f;
            float tri = 4 * MathF.Abs(ph - 0.5f) - 1;
            Data[i] += tri * amp * Env(t, dur, attack, curve);
        }
        return this;
    }

    static uint _rng = 0x1234567u;
    static float Rand()
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return (_rng & 0xFFFFFF) / (float)0x800000 - 1f;
    }

    public Wave Noise(float amp, float start, float dur, float attack = 0.001f, float curve = 3f)
    {
        int i0 = (int)(start * Rate);
        int i1 = Math.Min(Data.Length, (int)((start + dur) * Rate));
        for (int i = Math.Max(0, i0); i < i1; i++)
        {
            float t = (i - i0) / (float)Rate;
            Data[i] += Rand() * amp * Env(t, dur, attack, curve);
        }
        return this;
    }

    /// <summary>One-pole low-pass. Cheap, and enough to take the fizz off noise.</summary>
    public Wave LowPass(float cutoffHz)
    {
        float dt = 1f / Rate;
        float rc = 1f / (2 * MathF.PI * cutoffHz);
        float a = dt / (rc + dt);
        float prev = 0;
        for (int i = 0; i < Data.Length; i++)
        {
            prev += a * (Data[i] - prev);
            Data[i] = prev;
        }
        return this;
    }

    public Wave HighPass(float cutoffHz)
    {
        float dt = 1f / Rate;
        float rc = 1f / (2 * MathF.PI * cutoffHz);
        float a = rc / (rc + dt);
        float prevIn = 0, prevOut = 0;
        for (int i = 0; i < Data.Length; i++)
        {
            float x = Data[i];
            prevOut = a * (prevOut + x - prevIn);
            prevIn = x;
            Data[i] = prevOut;
        }
        return this;
    }

    /// <summary>Feedback delay — a poor man's reverb that makes the startup chime
    /// sound like it belongs in a room.</summary>
    public Wave Delay(float timeSec, float feedback, float mix)
    {
        int d = (int)(timeSec * Rate);
        if (d <= 0 || d >= Data.Length) return this;
        for (int i = d; i < Data.Length; i++)
            Data[i] += Data[i - d] * feedback * mix;
        return this;
    }

    /// <summary>Scales so the loudest sample sits at <paramref name="peak"/>.</summary>
    public Wave Normalize(float peak = 0.85f)
    {
        float max = 0;
        foreach (float v in Data) { float a = MathF.Abs(v); if (a > max) max = a; }
        if (max < 1e-6f) return this;
        float g = peak / max;
        for (int i = 0; i < Data.Length; i++) Data[i] *= g;
        return this;
    }

    /// <summary>Fades the very start and end so buffers never click.</summary>
    public Wave Declick(float seconds = 0.004f)
    {
        int n = Math.Min((int)(seconds * Rate), Data.Length / 2);
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)n;
            Data[i] *= f;
            Data[Data.Length - 1 - i] *= f;
        }
        return this;
    }

    public short[] ToPcm16()
    {
        var pcm = new short[Data.Length];
        for (int i = 0; i < Data.Length; i++)
        {
            float v = Math.Clamp(Data[i], -1f, 1f);
            pcm[i] = (short)(v * 32000);
        }
        return pcm;
    }

    // ---- note helpers ----------------------------------------------------

    /// <summary>MIDI note number to frequency. A4 (69) = 440 Hz.</summary>
    public static float Note(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);
}
