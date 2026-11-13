namespace PlaceholderArt;

public enum Wave { Sine, Triangle, Saw, Square }

/// <summary>
/// Tiny DSP toolkit: oscillators, noise, envelopes, RBJ biquad filters and a few effects.
/// Signals are plain float arrays at <see cref="Rate"/> Hz; most helpers work in place and return the
/// array so calls can be chained.
/// </summary>
public static class Dsp
{
    public const int Rate = 22050;

    public static int Len(double seconds) => (int)Math.Round(seconds * Rate);

    public static float[] Silence(double seconds) => new float[Len(seconds)];

    public static double Midi(double note) => 440.0 * Math.Pow(2.0, (note - 69.0) / 12.0);

    // ---------------------------------------------------------------- sources

    /// <summary>Oscillator with a time-varying frequency; saw/square use PolyBLEP to limit aliasing.</summary>
    public static float[] Osc(double seconds, Func<double, double> freq, Wave wave = Wave.Sine, double phase = 0)
    {
        var o = new float[Len(seconds)];
        double ph = phase - Math.Floor(phase);
        for (int i = 0; i < o.Length; i++)
        {
            double dt = freq(i / (double)Rate) / Rate;
            o[i] = (float)Sample(wave, ph, dt);
            ph += dt;
            ph -= Math.Floor(ph);
        }
        return o;
    }

    public static float[] Osc(double seconds, double freq, Wave wave = Wave.Sine, double phase = 0) =>
        Osc(seconds, _ => freq, wave, phase);

    static double Sample(Wave wave, double ph, double dt) => wave switch
    {
        Wave.Sine => Math.Sin(2 * Math.PI * ph),
        Wave.Triangle => 4 * Math.Abs(ph - 0.5) - 1,
        Wave.Saw => 2 * ph - 1 - Blep(ph, dt),
        _ => (ph < 0.5 ? 1 : -1) + Blep(ph, dt) - Blep((ph + 0.5) % 1.0, dt),
    };

    static double Blep(double t, double dt)
    {
        if (dt <= 0) return 0;
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1;
        }
        if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return t * t + t + t + 1;
        }
        return 0;
    }

    public static float[] Noise(double seconds, Random rng)
    {
        var o = new float[Len(seconds)];
        for (int i = 0; i < o.Length; i++) o[i] = (float)(rng.NextDouble() * 2 - 1);
        return o;
    }

    /// <summary>Smoothly interpolated random values in [-1, 1], changing about <paramref name="rate"/> times a second.</summary>
    public static Func<double, double> Wander(Random rng, double seconds, double rate)
    {
        var points = new double[(int)(seconds * rate) + 3];
        for (int i = 0; i < points.Length; i++) points[i] = rng.NextDouble() * 2 - 1;
        return t =>
        {
            double x = Math.Clamp(t * rate, 0, points.Length - 2.0001);
            int i = (int)x;
            double f = (1 - Math.Cos(Math.PI * (x - i))) / 2;
            return points[i] + (points[i + 1] - points[i]) * f;
        };
    }

    // ---------------------------------------------------------------- envelopes

    /// <summary>Linear attack then exponential decay.</summary>
    public static Func<double, double> Perc(double attack, double decay) =>
        t => t < attack ? t / attack : Math.Exp(-(t - attack) / decay);

    /// <summary>Linear attack, hold at full level, exponential release.</summary>
    public static Func<double, double> Ahr(double attack, double hold, double release) =>
        t => t < attack ? t / attack : t < attack + hold ? 1 : Math.Exp(-(t - attack - hold) / release);

    /// <summary>Smooth sin² swell in and out, reaching exactly zero at 0 and at <paramref name="length"/>.</summary>
    public static Func<double, double> Swell(double length, double attack, double release) => t =>
    {
        if (t <= 0 || t >= length) return 0;
        if (t < attack) return Math.Pow(Math.Sin(Math.PI / 2 * t / attack), 2);
        if (t > length - release) return Math.Pow(Math.Sin(Math.PI / 2 * (length - t) / release), 2);
        return 1;
    };

    public static float[] Env(this float[] s, Func<double, double> env)
    {
        for (int i = 0; i < s.Length; i++) s[i] = (float)(s[i] * env(i / (double)Rate));
        return s;
    }

    public static float[] Gain(this float[] s, double g)
    {
        for (int i = 0; i < s.Length; i++) s[i] = (float)(s[i] * g);
        return s;
    }

    /// <summary>Adds <paramref name="src"/> into <paramref name="dst"/> starting at time <paramref name="at"/> (seconds).</summary>
    public static float[] Mix(this float[] dst, float[] src, double at = 0, double gain = 1)
    {
        int off = Len(at);
        for (int i = 0; i < src.Length && off + i < dst.Length; i++)
            if (off + i >= 0) dst[off + i] += (float)(src[i] * gain);
        return dst;
    }

    public static float[] Padded(this float[] s, double extraSeconds)
    {
        var o = new float[s.Length + Len(extraSeconds)];
        Array.Copy(s, o, s.Length);
        return o;
    }

    public static float Peak(float[] s)
    {
        float m = 0;
        foreach (float v in s) m = Math.Max(m, Math.Abs(v));
        return m;
    }

    /// <summary>Scales so the largest absolute sample equals <paramref name="peak"/>.</summary>
    public static float[] Normalize(this float[] s, double peak)
    {
        float m = Peak(s);
        return m > 0 ? s.Gain(peak / m) : s;
    }

    public static float[] FadeIn(this float[] s, double seconds)
    {
        int n = Math.Min(Len(seconds), s.Length);
        for (int i = 0; i < n; i++) s[i] *= i / (float)n;
        return s;
    }

    public static float[] FadeOut(this float[] s, double seconds)
    {
        int n = Math.Min(Len(seconds), s.Length);
        for (int i = 0; i < n; i++) s[s.Length - 1 - i] *= i / (float)n;
        return s;
    }

    // ---------------------------------------------------------------- filters

    public static float[] LowPass(this float[] s, double hz, double q = 0.7071) => Filter(s, FilterKind.LowPass, _ => hz, q);
    public static float[] LowPass(this float[] s, Func<double, double> hz, double q = 0.7071) => Filter(s, FilterKind.LowPass, hz, q);
    public static float[] HighPass(this float[] s, double hz, double q = 0.7071) => Filter(s, FilterKind.HighPass, _ => hz, q);
    public static float[] HighPass(this float[] s, Func<double, double> hz, double q = 0.7071) => Filter(s, FilterKind.HighPass, hz, q);
    /// <summary>Band-pass with 0 dB peak gain.</summary>
    public static float[] BandPass(this float[] s, double hz, double q = 1) => Filter(s, FilterKind.BandPass, _ => hz, q);
    public static float[] BandPass(this float[] s, Func<double, double> hz, double q = 1) => Filter(s, FilterKind.BandPass, hz, q);

    /// <summary>Filters into a new array; the cutoff function is re-evaluated every 16 samples.</summary>
    static float[] Filter(float[] s, FilterKind kind, Func<double, double> hz, double q)
    {
        var o = new float[s.Length];
        var bq = new Biquad();
        for (int i = 0; i < s.Length; i++)
        {
            if ((i & 15) == 0) bq.Set(kind, hz(i / (double)Rate), q);
            o[i] = bq.Process(s[i]);
        }
        return o;
    }

    enum FilterKind { LowPass, HighPass, BandPass }

    /// <summary>RBJ cookbook biquad, transposed direct form II.</summary>
    struct Biquad
    {
        double _b0, _b1, _b2, _a1, _a2, _z1, _z2;

        public void Set(FilterKind kind, double hz, double q)
        {
            hz = Math.Clamp(hz, 10, Rate * 0.45);
            double w = 2 * Math.PI * hz / Rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q);
            double b0, b1, b2;
            switch (kind)
            {
                case FilterKind.LowPass: b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = b0; break;
                case FilterKind.HighPass: b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = b0; break;
                default: b0 = alpha; b1 = 0; b2 = -alpha; break;
            }
            double a0 = 1 + alpha;
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = -2 * cos / a0;
            _a2 = (1 - alpha) / a0;
        }

        public float Process(float x)
        {
            double y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return (float)y;
        }
    }

    // ---------------------------------------------------------------- effects

    /// <summary>tanh saturation, normalised so a full-scale input stays full scale.</summary>
    public static float[] SoftClip(this float[] s, double drive)
    {
        double norm = Math.Tanh(drive);
        float peak = Peak(s);
        if (peak <= 0) return s;
        for (int i = 0; i < s.Length; i++) s[i] = (float)(Math.Tanh(drive * s[i] / peak) / norm * peak);
        return s;
    }

    /// <summary>Feedback echo; output keeps the input length (pad first for a tail).</summary>
    public static float[] Echo(this float[] s, double delay, double feedback, double wet)
    {
        int d = Math.Max(1, Len(delay));
        var line = new float[s.Length];
        var o = (float[])s.Clone();
        for (int i = d; i < s.Length; i++)
        {
            line[i] = (float)(s[i - d] + feedback * line[i - d]);
            o[i] += (float)(wet * line[i]);
        }
        return o;
    }

    /// <summary>
    /// Small Schroeder/Freeverb-style mono reverb (parallel damped combs into series all-passes).
    /// The wet signal is scaled to the dry RMS times <paramref name="wet"/>; output keeps the input length.
    /// </summary>
    public static float[] Reverb(this float[] s, double size, double damp, double wet)
    {
        int[] combs = [557, 593, 641, 677, 709, 757, 787, 823];
        int[] allPasses = [278, 216, 170, 113];
        double fb = 0.7 + 0.28 * Math.Clamp(size, 0, 1);
        var acc = new double[s.Length];
        foreach (int d in combs)
        {
            var buf = new double[d];
            int idx = 0;
            double lp = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double y = buf[idx];
                lp = y * (1 - damp) + lp * damp;
                buf[idx] = s[i] + lp * fb;
                acc[i] += y;
                if (++idx == d) idx = 0;
            }
        }
        foreach (int d in allPasses)
        {
            var buf = new double[d];
            int idx = 0;
            for (int i = 0; i < s.Length; i++)
            {
                double b = buf[idx], x = acc[i];
                buf[idx] = x + b * 0.5;
                acc[i] = b - x;
                if (++idx == d) idx = 0;
            }
        }
        double dryRms = Rms(s), wetRms = Math.Sqrt(acc.Sum(v => v * v) / Math.Max(1, acc.Length));
        double k = wetRms > 0 ? wet * dryRms / wetRms : 0;
        var o = new float[s.Length];
        for (int i = 0; i < s.Length; i++) o[i] = (float)(s[i] + k * acc[i]);
        return o;
    }

    public static double Rms(float[] s) => s.Length == 0 ? 0 : Math.Sqrt(s.Sum(v => (double)v * v) / s.Length);

    /// <summary>One-pole DC blocker (about 20 Hz).</summary>
    public static float[] DcBlock(this float[] s)
    {
        double r = 1 - 2 * Math.PI * 20 / Rate, x1 = 0, y1 = 0;
        for (int i = 0; i < s.Length; i++)
        {
            double y = s[i] - x1 + r * y1;
            x1 = s[i];
            y1 = y;
            s[i] = (float)y;
        }
        return s;
    }
}

/// <summary>
/// Seamless loop builder. Everything is rendered into three back-to-back copies of the loop and only the
/// middle copy is kept, so note tails, filter and reverb state that cross the loop point wrap exactly.
/// Clips added with <see cref="Add"/> are identical in every copy; continuous sources must be periodic
/// (use <see cref="AddPeriodic"/> and whole numbers of cycles per loop).
/// </summary>
public sealed class Loop
{
    public Loop(double seconds)
    {
        Length = Dsp.Len(seconds);
        Seconds = seconds;
        Data = new float[Length * 3];
    }

    public int Length { get; }
    public double Seconds { get; }
    public float[] Data { get; private set; }

    /// <summary>Adds a clip at time <paramref name="at"/> within the loop, repeated in all three copies.</summary>
    public Loop Add(float[] clip, double at, double gain = 1)
    {
        int off = ((Dsp.Len(at) % Length) + Length) % Length;
        for (int k = 0; k < 3; k++)
        {
            int o = off + k * Length;
            for (int i = 0; i < clip.Length && o + i < Data.Length; i++) Data[o + i] += (float)(clip[i] * gain);
        }
        return this;
    }

    /// <summary>Adds a periodic source given as a function of the sample index within the loop.</summary>
    public Loop AddPeriodic(Func<int, double> sample)
    {
        for (int i = 0; i < Data.Length; i++) Data[i] += (float)sample(i % Length);
        return this;
    }

    /// <summary>Runs an effect chain over the whole three-copy buffer.</summary>
    public Loop Apply(Func<float[], float[]> fx)
    {
        var r = fx(Data);
        if (r.Length != Data.Length) Array.Resize(ref r, Data.Length);
        Data = r;
        return this;
    }

    public Loop Mix(Loop other, double gain = 1)
    {
        for (int i = 0; i < Data.Length; i++) Data[i] += (float)(other.Data[i] * gain);
        return this;
    }

    public Loop Normalize(double peak)
    {
        Data.Normalize(peak);
        return this;
    }

    /// <summary>
    /// The finished loop: the middle copy. Verifies that what follows the middle copy (the start of the third)
    /// matches its own start, i.e. that playing it on repeat continues exactly as rendered.
    /// </summary>
    public float[] Take()
    {
        float peak = Math.Max(Dsp.Peak(Data), 1e-9f), worst = 0f;
        for (int i = 0; i < Math.Min(Length, Dsp.Rate); i++)
            worst = Math.Max(worst, Math.Abs(Data[Length + i] - Data[2 * Length + i]));
        if (worst > peak * 1e-3f)
            throw new InvalidOperationException($"Loop is not seamless (mismatch {worst / peak:P3} of peak).");
        return Data[Length..(2 * Length)];
    }
}
