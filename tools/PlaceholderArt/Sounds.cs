namespace PlaceholderArt;

/// <summary>Sound effects synthesised from oscillators and noise (no samples).</summary>
public static class Sounds
{
    /// <summary>Gunshot recipe: bright crack + noise body + pitch-dropping thump + room tail, saturated.</summary>
    public sealed record Shot(double Length, double CrackTau, double CrackHp, double BodyTau, double BodyLp,
        double ThumpFrom, double ThumpTo, double ThumpTau, double ThumpGain, double TailTau, double TailLp, double TailGain, double Drive);

    public static readonly Shot Rifle = new(0.45, 0.005, 3200, 0.04, 5500, 170, 60, 0.055, 0.85, 0.22, 2400, 0.35, 2.6);
    public static readonly Shot Pistol = new(0.32, 0.006, 1800, 0.03, 3800, 190, 70, 0.045, 0.8, 0.10, 2200, 0.25, 2.2);

    public static float[] Gunshot(Shot s, int seed)
    {
        var rng = new Random(seed);
        double n = s.Length;
        var crack = Dsp.Noise(n, rng).HighPass(s.CrackHp).Env(t => Math.Exp(-t / s.CrackTau));
        var body = Dsp.Noise(n, rng).LowPass(s.BodyLp).Env(Dsp.Perc(0.0008, s.BodyTau));
        var thump = Dsp.Osc(n, t => s.ThumpTo + (s.ThumpFrom - s.ThumpTo) * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.001, s.ThumpTau));
        var tail = Dsp.Noise(n, rng).LowPass(t => 250 + s.TailLp * Math.Exp(-t / 0.15))
            .Env(t => (1 - Math.Exp(-t / 0.012)) * Math.Exp(-t / s.TailTau));
        return Dsp.Silence(n).Mix(crack, 0, 0.9).Mix(body).Mix(thump, 0, s.ThumpGain).Mix(tail, 0, s.TailGain).SoftClip(s.Drive);
    }

    /// <summary>Energy weapon: descending square-ish zap with a buzzing tail.</summary>
    public static float[] Zap(int seed)
    {
        var rng = new Random(seed);
        double n = 0.35;
        var zap = Dsp.Osc(n, t => 220 + 1600 * Math.Exp(-t / 0.05), Wave.Saw).LowPass(4000).Env(Dsp.Perc(0.002, 0.12));
        var sub = Dsp.Osc(n, t => 90 + 200 * Math.Exp(-t / 0.04)).Env(Dsp.Perc(0.002, 0.09));
        var hiss = Dsp.Noise(n, rng).HighPass(3000).Env(Dsp.Perc(0.001, 0.04));
        return Dsp.Silence(n).Mix(zap, 0, 0.7).Mix(sub, 0, 0.8).Mix(hiss, 0, 0.3).SoftClip(1.8);
    }

    public static float[] Explosion(int seed, double length = 1.4)
    {
        var rng = new Random(seed);
        var boom = Dsp.Osc(length, t => 40 + 90 * Math.Exp(-t / 0.08)).Env(Dsp.Perc(0.003, 0.35));
        var roar = Dsp.Noise(length, rng).LowPass(t => 200 + 3000 * Math.Exp(-t / 0.12)).Env(Dsp.Perc(0.004, 0.5));
        var crackle = Dsp.Noise(length, rng).HighPass(2000).Env(t => Math.Exp(-t / 0.05) + 0.1 * Math.Exp(-t / 0.4) * (rng.NextDouble() < 0.02 ? 1 : 0.2));
        return Dsp.Silence(length).Mix(boom, 0, 1.0).Mix(roar, 0, 0.9).Mix(crackle, 0, 0.3).Reverb(0.5, 0.5, 0.2).SoftClip(2.4);
    }

    public static float[] Footstep(int seed, double brightness = 1)
    {
        var rng = new Random(seed);
        double n = 0.12;
        return Dsp.Noise(n, rng).LowPass(900 * brightness).Env(Dsp.Perc(0.002, 0.025))
            .Mix(Dsp.Osc(n, t => 80 + 40 * Math.Exp(-t / 0.01)).Env(Dsp.Perc(0.001, 0.02)), 0, 0.6);
    }

    /// <summary>Door: wooden creak (<paramref name="creak"/>) or pneumatic hiss.</summary>
    public static float[] Door(int seed, bool creak)
    {
        var rng = new Random(seed);
        if (creak)
        {
            double n = 0.6;
            var squeak = Dsp.Osc(n, t => 300 + 120 * Math.Sin(t * 9) + 60 * t, Wave.Saw).BandPass(900, 4).Env(Dsp.Swell(n, 0.05, 0.2));
            var knock = Dsp.Noise(n, rng).LowPass(600).Env(t => t > 0.45 ? Math.Exp(-(t - 0.45) / 0.03) : 0);
            return Dsp.Silence(n).Mix(squeak, 0, 0.5).Mix(knock, 0, 0.9);
        }

        double m = 0.5;
        return Dsp.Noise(m, rng).BandPass(t => 3000 - 2000 * t, 1.2).Env(Dsp.Swell(m, 0.02, 0.25))
            .Mix(Dsp.Osc(m, 70).Env(Dsp.Perc(0.01, 0.1)), 0.38, 0.6);
    }

    /// <summary>Death cry: falling formant buzz (pitch sets human / creature).</summary>
    public static float[] Death(int seed, double pitch)
    {
        var rng = new Random(seed);
        double n = 0.7;
        var voice = Dsp.Osc(n, t => pitch * (1.2 - 0.5 * t), Wave.Saw).BandPass(t => 900 - 400 * t, 3).Env(Dsp.Swell(n, 0.02, 0.4));
        var breath = Dsp.Noise(n, rng).BandPass(1500, 2).Env(Dsp.Swell(n, 0.02, 0.3));
        var thud = Dsp.Noise(n, rng).LowPass(300).Env(t => t > 0.5 ? Math.Exp(-(t - 0.5) / 0.04) : 0);
        return Dsp.Silence(n).Mix(voice, 0, 0.8).Mix(breath, 0, 0.25).Mix(thud, 0, 0.9);
    }

    public static float[] Click(int seed)
    {
        var rng = new Random(seed);
        return Dsp.Noise(0.05, rng).HighPass(2500).Env(t => Math.Exp(-t / 0.004))
            .Mix(Dsp.Osc(0.05, 1800).Env(Dsp.Perc(0.0005, 0.01)), 0, 0.4);
    }

    /// <summary>Short sequence of tones (turn start, victory, defeat).</summary>
    public static float[] Jingle(double[] notes, double step, Wave wave, double decay = 0.25)
    {
        double n = notes.Length * step + decay * 3;
        var mix = Dsp.Silence(n);
        for (int i = 0; i < notes.Length; i++)
        {
            double f = Dsp.Midi(notes[i]);
            var tone = Dsp.Osc(decay * 3, f, wave).LowPass(3000).Env(Dsp.Perc(0.005, decay))
                .Mix(Dsp.Osc(decay * 3, f * 2).Env(Dsp.Perc(0.005, decay * 0.6)), 0, 0.25);
            mix.Mix(tone, i * step, 0.6);
        }

        return mix.Reverb(0.4, 0.4, 0.15);
    }

    /// <summary>Plucked string release (crossbow, bow) with a wooden knock.</summary>
    public static float[] Twang(int seed, double pitch, double knock)
    {
        var rng = new Random(seed);
        double n = 0.45;
        var twang = Dsp.Osc(n, t => pitch * (1 + 0.4 * Math.Exp(-t / 0.01)), Wave.Triangle).Env(Dsp.Perc(0.001, 0.12));
        var hit = Dsp.Noise(n, rng).LowPass(1200).Env(Dsp.Perc(0.001, 0.015));
        var whoosh = Dsp.Noise(n, rng).BandPass(t => 2500 - 1500 * t, 1.5).Env(Dsp.Swell(0.3, 0.02, 0.2));
        return Dsp.Silence(n).Mix(twang, 0, 0.7).Mix(hit, 0, knock).Mix(whoosh, 0.02, 0.3);
    }

    /// <summary>Metal blade hit: noise burst plus inharmonic ringing.</summary>
    public static float[] Clang(int seed)
    {
        var rng = new Random(seed);
        double n = 0.6;
        var hit = Dsp.Noise(n, rng).HighPass(1200).Env(t => Math.Exp(-t / 0.01));
        var ring = Dsp.Osc(n, 1250).Env(Dsp.Perc(0.001, 0.2))
            .Mix(Dsp.Osc(n, 1250 * 1.7).Env(Dsp.Perc(0.001, 0.12)), 0, 0.6)
            .Mix(Dsp.Osc(n, 1250 * 2.9).Env(Dsp.Perc(0.001, 0.08)), 0, 0.4);
        return Dsp.Silence(n).Mix(hit, 0, 0.8).Mix(ring, 0, 0.5);
    }

    /// <summary>Calm ambient loop: two detuned pads and slow noise wind.</summary>
    public static Loop Ambience(double seconds, double root, int seed)
    {
        var rng = new Random(seed);
        var loop = new Loop(seconds);
        loop.AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate;
            // Whole number of cycles per loop, so the wrap-around is seamless.
            double f = Math.Round(Dsp.Midi(root) * seconds) / seconds;
            double f2 = Math.Round(f * 1.5 * seconds) / seconds;
            return 0.18 * Math.Sin(2 * Math.PI * f * t) + 0.12 * Math.Sin(2 * Math.PI * f2 * t + Math.Sin(2 * Math.PI * t / seconds) * 2);
        });
        loop.Add(Dsp.Noise(seconds, rng).LowPass(500).Gain(0.15), 0);
        return loop;
    }
}
