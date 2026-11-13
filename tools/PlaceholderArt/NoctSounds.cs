namespace PlaceholderArt;

/// <summary>NoctInspired mod audio: quiet, unsettling loops and survival-horror one-shots.</summary>
public static class NoctSounds
{
    public static void Generate(Pack p)
    {
        p.Wav("audio/ambience_drone.wav", AmbienceDrone(), peak: 0.35, loop: true);
        p.Wav("audio/music_dread.wav", Music.Dread(), peak: 0.5, loop: true);
        p.Wav("audio/creature_growl.wav", Growl());
        p.Wav("audio/creature_screech.wav", Screech());
        p.Wav("audio/static.wav", Static());
        p.Wav("audio/heartbeat.wav", Heartbeat());
        p.Wav("audio/footstep.wav", Footstep(), peak: 0.7);
        p.Wav("audio/door.wav", Door());
        p.Wav("audio/alert.wav", Alert());
    }

    const double Tau = 2 * Math.PI;

    /// <summary>
    /// 24 s loop: a beating mains-like hum (every partial is a whole number of cycles per loop), wind made
    /// of periodic noise through a slowly wandering band-pass, and two or three distant metallic tones.
    /// </summary>
    static float[] AmbienceDrone()
    {
        const double len = 24;
        var rng = new Random(201);
        var hum = new Loop(len).AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate;
            double am = 1 + 0.2 * Math.Sin(Tau * 2 * t / len);
            return am * (Math.Sin(Tau * 55 * t) + 0.8 * Math.Sin(Tau * 55.25 * t + 1.0) + 0.35 * Math.Sin(Tau * 110.5 * t + 0.3)
                         + 0.18 * Math.Sin(Tau * 165.75 * t + 2.0) + 0.08 * Math.Sin(Tau * 221 * t));
        });
        hum.Apply(s => s.LowPass(400)).Normalize(1);

        var table = Dsp.Noise(len, rng);
        var wind = new Loop(len).AddPeriodic(i => table[i]);
        wind.Apply(s => s.BandPass(t => 380 + 200 * Math.Sin(Tau * 3 * t / len + 0.5) + 110 * Math.Sin(Tau * 7 * t / len), 0.9).LowPass(1200));
        wind.Apply(s => s.Env(t => 0.5 + 0.3 * Math.Sin(Tau * 2 * t / len + 1.1) + 0.2 * Math.Sin(Tau * 5 * t / len + 0.3))).Normalize(1);

        var metal = new Loop(len)
            .Add(MetalTone(rng, 173, 4.5), 4.3)
            .Add(MetalTone(rng, 131, 5.0), 15.2, 0.8)
            .Add(MetalTone(rng, 219, 3.5), 20.6, 0.45);
        metal.Apply(s => s.LowPass(1500).Reverb(0.95, 0.5, 0.7)).Normalize(1);

        return new Loop(len).Mix(hum, 0.45).Mix(wind, 0.5).Mix(metal, 0.35).Take();
    }

    /// <summary>Struck metal: inharmonic, slowly decaying partials plus a short scrape.</summary>
    static float[] MetalTone(Random rng, double f0, double len)
    {
        double[] ratios = [1, 2.756, 5.404, 8.933, 13.34];
        var o = Dsp.Silence(len);
        for (int k = 0; k < ratios.Length; k++)
        {
            double f = f0 * ratios[k] * (1 + (rng.NextDouble() - 0.5) * 0.01), decay = len * 0.3 / (1 + k * 0.7);
            o.Mix(Dsp.Osc(len, f, Wave.Sine, rng.NextDouble()).Env(t => Math.Min(1, t / 0.004) * Math.Exp(-t / decay)), 0, 1.0 / (1 + k));
        }
        return o.Mix(Dsp.Noise(len, rng).BandPass(f0 * 4, 3).Env(Dsp.Perc(0.01, 0.08)), 0, 0.3).FadeOut(0.3);
    }

    /// <summary>Low, rough growl: jittery saw/square through vowel-like band-passes with a fast "rattle".</summary>
    static float[] Growl()
    {
        var rng = new Random(211);
        double n = 1.5;
        var jitter = Dsp.Wander(rng, n, 18);
        var rattle = Dsp.Wander(rng, n, 60);
        Func<double, double> f0 = t => 62 + 14 * Math.Sin(Math.PI * t / n) + 7 * jitter(t);
        var src = Dsp.Osc(n, f0, Wave.Saw).Mix(Dsp.Osc(n, t => f0(t) * 0.5, Wave.Square), 0, 0.5)
            .Env(t => 0.55 + 0.45 * Math.Abs(rattle(t)));
        var breath = Dsp.Noise(n, rng).BandPass(700, 1).Env(t => 0.5 + 0.5 * Math.Abs(rattle(t * 0.7)));
        return Dsp.Silence(n)
            .Mix(src.BandPass(420, 2.5))
            .Mix(src.BandPass(950, 3), 0, 0.6)
            .Mix(src.LowPass(300), 0, 0.7)
            .Mix(breath, 0, 0.3)
            .Env(Dsp.Swell(n, 0.25, 0.45))
            .SoftClip(2.5)
            .Reverb(0.6, 0.5, 0.2);
    }

    /// <summary>Harsh, inhuman screech: detuned saws with vibrato, ring-modulated, distorted.</summary>
    static float[] Screech()
    {
        var rng = new Random(212);
        double n = 1.1;
        var wander = Dsp.Wander(rng, n, 25);
        Func<double, double> contour = t => t < 0.12 ? 900 + 600 * t / 0.12 : 1500 - 500 * (t - 0.12) / (n - 0.12);
        var o = Dsp.Silence(n);
        foreach (double det in new[] { 1.0, 1.043, 0.962, 1.51 })
            o.Mix(Dsp.Osc(n, t => contour(t) * det * (1 + 0.03 * Math.Sin(Tau * 7.3 * t) + 0.02 * wander(t)), Wave.Saw), 0, det > 1.4 ? 0.3 : 0.5);
        var ring = Dsp.Osc(n, 313);
        for (int i = 0; i < o.Length; i++) o[i] *= 0.6f + 0.4f * ring[i];
        return o.BandPass(2200, 0.8)
            .Mix(Dsp.Noise(n, rng).BandPass(3000, 1.5), 0, 0.25)
            .Env(t => Math.Min(1, t / 0.03) * (t < 0.2 ? 1 : Math.Exp(-(t - 0.2) / 0.35)))
            .SoftClip(3)
            .Reverb(0.5, 0.4, 0.25);
    }

    /// <summary>Radio static burst: band-limited hiss with fluttering level and crackles.</summary>
    static float[] Static()
    {
        var rng = new Random(213);
        double n = 0.55;
        var hiss = Dsp.Noise(n, rng).BandPass(1700, 0.45);
        var level = Dsp.Wander(rng, n, 70);
        hiss.Env(t => 0.55 + 0.45 * level(t));
        for (int i = 0; i < 40; i++)
        {
            int at = rng.Next(hiss.Length - 16);
            double amp = (rng.NextDouble() * 2 - 1) * 1.5;
            for (int k = 0; k < 12; k++) hiss[at + k] += (float)(amp * Math.Exp(-k / 2.0) * (k % 2 == 0 ? 1 : -1));
        }
        hiss.Mix(Dsp.Osc(n, 2900).Env(t => 0.5 + 0.5 * Math.Sin(Tau * 9 * t)), 0, 0.04);
        return hiss.HighPass(250).Env(t => Math.Min(1, t / 0.01) * Math.Min(1, (n - t) / 0.06)).SoftClip(1.5);
    }

    static float[] Heartbeat()
    {
        var rng = new Random(214);
        return Dsp.Silence(1.0).Mix(Thump(rng), 0.04).Mix(Thump(rng), 0.32, 0.75).LowPass(400);
    }

    static float[] Thump(Random rng)
    {
        double n = 0.4;
        return Dsp.Osc(n, t => 45 + 35 * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.008, 0.08))
            .Mix(Dsp.Noise(n, rng).LowPass(180).Env(Dsp.Perc(0.004, 0.05)), 0, 0.6)
            .FadeOut(0.1);
    }

    /// <summary>Hard step on concrete with a short slap-back echo of a corridor.</summary>
    static float[] Footstep()
    {
        var rng = new Random(215);
        return Dsp.Silence(0.4)
            .Mix(Dsp.Noise(0.06, rng).BandPass(1900, 1.2).Env(Dsp.Perc(0.0005, 0.012)))
            .Mix(Dsp.Osc(0.06, t => 110 + 60 * Math.Exp(-t / 0.008)).Env(Dsp.Perc(0.001, 0.02)).FadeOut(0.02), 0, 0.8)
            .Mix(Dsp.Noise(0.06, rng).HighPass(4000).Env(Dsp.Perc(0.0005, 0.004)), 0, 0.4)
            .Echo(0.075, 0.35, 0.4)
            .Reverb(0.6, 0.6, 0.12);
    }

    /// <summary>Heavy metal door: stick-slip hinge creak, then a ringing bang in a big room.</summary>
    static float[] Door()
    {
        var rng = new Random(216);
        double n = 2.0;
        var impulses = Dsp.Silence(0.75);
        for (double t = 0; t < 0.72;)
        {
            impulses[Dsp.Len(t)] += (float)(0.6 + 0.4 * rng.NextDouble());
            t += 1 / (28 + 50 * t + 10 * rng.NextDouble());
        }
        var creak = Dsp.Silence(0.75)
            .Mix(impulses.BandPass(t => 640 + 200 * t, 12))
            .Mix(impulses.BandPass(1450, 14), 0, 0.7)
            .Mix(impulses.BandPass(2350, 16), 0, 0.4)
            .Env(t => Math.Min(1, t / 0.08) * Math.Min(1, (0.75 - t) / 0.05))
            .Normalize(0.5);

        const double bangAt = 0.8;
        var bang = Dsp.Silence(1.2);
        double[] partials = [213, 541, 874, 1297, 1838, 2510];
        for (int k = 0; k < partials.Length; k++)
            bang.Mix(Dsp.Osc(1.2, partials[k] * (1 + 0.003 * rng.NextDouble()), Wave.Sine, rng.NextDouble()).Env(Dsp.Perc(0.0008, 0.5 / (1 + k * 0.6))), 0, 0.6 / (1 + k * 0.4));
        bang.Mix(Dsp.Osc(0.4, t => 60 + 60 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.1)).FadeOut(0.1))
            .Mix(Dsp.Noise(0.2, rng).LowPass(2500).Env(Dsp.Perc(0.0005, 0.02)).FadeOut(0.05), 0, 0.9)
            .FadeOut(0.2)
            .Normalize(1);

        return Dsp.Silence(n).Mix(creak).Mix(bang, bangAt).Reverb(0.85, 0.5, 0.35).SoftClip(1.6);
    }

    /// <summary>Low, dissonant swelling tone (minor second plus tritone).</summary>
    static float[] Alert()
    {
        double n = 1.1;
        var o = Dsp.Silence(n);
        foreach (var (f, g) in new[] { (98.0, 1.0), (103.8, 0.8), (138.6, 0.6), (201.9, 0.3) })
            o.Mix(Dsp.Osc(n, t => f * (1 - 0.03 * t), Wave.Saw), 0, g);
        return o.LowPass(t => 300 + 900 * Math.Exp(-t / 0.25), 1.5).Env(Dsp.Ahr(0.02, 0.25, 0.3)).Reverb(0.8, 0.5, 0.3).SoftClip(1.4);
    }
}
