namespace PlaceholderArt;

/// <summary>NoctPrototype sounds, on top of the shared horror set (<see cref="NoctSounds"/>).</summary>
public static class NoctPrototypeSounds
{
    public static void Generate(Pack p)
    {
        p.Wav("audio/ambience_facility.wav", FacilityAmbience(), peak: 0.32, loop: true);
        p.Wav("audio/transmission.wav", Transmission(), peak: 0.6);
        p.Wav("audio/reload.wav", Reload(), peak: 0.7);
        p.Wav("audio/heal.wav", Heal(), peak: 0.6);
        p.Wav("audio/leaper_screech.wav", LeaperScreech(), peak: 0.8);
        p.Wav("audio/watcher_breath.wav", WatcherBreath(), peak: 0.55);
        p.Wav("audio/scavenger_growl.wav", ScavengerGrowl(), peak: 0.8);
    }

    /// <summary>
    /// Abandoned facility at night, 24 s loop: gusts of wind, a low electrical hum, a far metal
    /// creak and a loose sheet banging now and then.
    /// </summary>
    static float[] FacilityAmbience()
    {
        const double length = 24;
        var rng = new Random(401);
        var loop = new Loop(length);
        int n = Dsp.Len(length);
        loop.AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate;
            return 0.05 * Math.Sin(Math.Tau * 50 * t) + 0.025 * Math.Sin(Math.Tau * 100 * t + 0.4);
        });
        var wind = Dsp.Noise(length, rng).LowPass(t => 260 + 220 * Math.Sin(Math.Tau * t / length * 3) + 120 * Math.Sin(Math.Tau * t / length * 7))
            .Env(t => 0.55 + 0.45 * Math.Sin(Math.Tau * t / length * 2 + 1) * Math.Sin(Math.Tau * t / length * 5));
        loop.Add(wind, 0, 0.9);
        foreach (double at in new[] { 5.5, 17.2 })
            loop.Add(Dsp.Osc(1.6, t => 180 + 60 * Math.Sin(t * 9) - 40 * t, Wave.Saw).BandPass(t => 700 + 300 * Math.Sin(t * 6), 6).Env(Dsp.Ahr(0.3, 0.8, 0.5)), at, 0.18);
        foreach (double at in new[] { 9.8, 10.15, 21.4 })
            loop.Add(Dsp.Noise(0.4, rng).LowPass(900).Env(Dsp.Perc(0.002, 0.18)).Mix(Dsp.Osc(0.4, 95).Env(Dsp.Perc(0.002, 0.12)), 0, 0.6), at, 0.25);
        return loop.Apply(x => x.Reverb(0.85, 0.5, 0.3)).Normalize(1).Take();
    }

    /// <summary>Radio squelch, two short tones, a tail of static.</summary>
    static float[] Transmission()
    {
        var rng = new Random(402);
        double n = 0.75;
        return Dsp.Silence(n)
            .Mix(Dsp.Noise(0.09, rng).BandPass(1800, 1.2).Env(Dsp.Perc(0.002, 0.07)), 0, 0.7)
            .Mix(Dsp.Osc(0.08, 1250, Wave.Square).LowPass(3000).Env(Dsp.Ahr(0.003, 0.06, 0.01)), 0.1, 0.35)
            .Mix(Dsp.Osc(0.1, 940, Wave.Square).LowPass(3000).Env(Dsp.Ahr(0.003, 0.08, 0.01)), 0.2, 0.35)
            .Mix(Dsp.Noise(0.4, rng).BandPass(2400, 0.8).Env(Dsp.Ahr(0.02, 0.15, 0.2)), 0.33, 0.3);
    }

    /// <summary>Magazine out, magazine in, slide racked.</summary>
    static float[] Reload()
    {
        var rng = new Random(403);
        float[] Click(double pitch, double gain) => Dsp.Noise(0.06, rng).BandPass(pitch, 3).Env(Dsp.Perc(0.0005, 0.02))
            .Mix(Dsp.Osc(0.06, pitch * 0.5).Env(Dsp.Perc(0.0005, 0.015)), 0, gain);
        return Dsp.Silence(0.8)
            .Mix(Click(2600, 0.4), 0.02, 0.8)
            .Mix(Dsp.Noise(0.12, rng).BandPass(t => 1800 - 4000 * t, 2).Env(Dsp.Perc(0.01, 0.06)), 0.1, 0.3)
            .Mix(Click(1500, 0.8), 0.38, 1)
            .Mix(Dsp.Noise(0.1, rng).BandPass(t => 1200 + 9000 * t, 2).Env(Dsp.Perc(0.005, 0.04)), 0.55, 0.5)
            .Mix(Click(3200, 0.5), 0.64, 0.9);
    }

    /// <summary>Velcro torn open, a pill box rattle.</summary>
    static float[] Heal()
    {
        var rng = new Random(404);
        var o = Dsp.Silence(0.8).Mix(Dsp.Noise(0.3, rng).BandPass(t => 1500 + 2500 * t / 0.3, 3).Env(Dsp.Ahr(0.01, 0.22, 0.05)), 0, 0.7);
        for (int i = 0; i < 9; i++)
            o.Mix(Dsp.Noise(0.03, rng).BandPass(3500 + rng.Next(2500), 6).Env(Dsp.Perc(0.0005, 0.01)), 0.42 + i * 0.03 + rng.NextDouble() * 0.01, 0.5);
        return o;
    }

    /// <summary>The leap warning: a sharp, rising shriek.</summary>
    static float[] LeaperScreech()
    {
        var rng = new Random(405);
        double n = 0.5;
        Func<double, double> pitch = t => 420 * Math.Pow(2, 1.6 * t / n) * (1 + 0.04 * Math.Sin(t * 70));
        var voice = Dsp.Osc(n, pitch, Wave.Saw).Mix(Dsp.Noise(n, rng).HighPass(2000), 0, 0.3);
        return ((float[])voice.Clone()).BandPass(t => 1600 + 2200 * t / n, 4)
            .Mix(((float[])voice.Clone()).BandPass(3300, 6), 0, 0.5)
            .Env(Dsp.Ahr(0.02, 0.35, 0.12))
            .SoftClip(2);
    }

    /// <summary>A slow, wet breath and a few clicks: something waits in the dark.</summary>
    static float[] WatcherBreath()
    {
        var rng = new Random(406);
        double n = 2.2;
        var o = Dsp.Silence(n)
            .Mix(Dsp.Noise(1.1, rng).BandPass(t => 500 + 300 * t, 1.5).Env(t => Math.Sin(Math.PI * Math.Min(1, t / 1.1)) * 0.8), 0, 1)
            .Mix(Dsp.Noise(0.8, rng).BandPass(t => 900 - 400 * t, 1.5).Env(t => Math.Sin(Math.PI * Math.Min(1, t / 0.8)) * 0.6), 1.25, 1);
        foreach (double at in new[] { 0.6, 0.66, 1.9 })
            o.Mix(Dsp.Noise(0.02, rng).BandPass(2400, 5).Env(Dsp.Perc(0.0003, 0.006)), at, 0.7);
        return o;
    }

    /// <summary>Low, ragged snarl of the scavenger.</summary>
    static float[] ScavengerGrowl()
    {
        var rng = new Random(407);
        double n = 0.9;
        Func<double, double> pitch = t => 70 + 25 * Math.Sin(t * 13) + 15 * Math.Sin(t * 37);
        var voice = Dsp.Osc(n, pitch, Wave.Saw).Mix(Dsp.Noise(n, rng).LowPass(1200), 0, 0.5);
        return ((float[])voice.Clone()).BandPass(t => 420 + 120 * Math.Sin(t * 5), 3)
            .Mix(((float[])voice.Clone()).LowPass(300), 0, 0.8)
            .Env(Dsp.Ahr(0.05, 0.6, 0.25))
            .SoftClip(2.2);
    }
}
