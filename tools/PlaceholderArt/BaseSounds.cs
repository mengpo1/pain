namespace PlaceholderArt;

/// <summary>Base pack sound effects and the action music loop, all synthesised from oscillators and noise.</summary>
public static class BaseSounds
{
    public static void Generate(Pack p)
    {
        p.Wav("audio/pistol.wav", Gunshot(Pistol, 1));
        p.Wav("audio/revolver.wav", Gunshot(Revolver, 2));
        p.Wav("audio/shotgun.wav", Gunshot(Shotgun, 3));
        p.Wav("audio/smg.wav", Gunshot(Smg, 4));
        p.Wav("audio/rifle.wav", Gunshot(Rifle, 5));
        p.Wav("audio/flare.wav", Flare());
        p.Wav("audio/empty.wav", Empty());
        p.Wav("audio/swing.wav", Whoosh(0.3, 0.11, 350, 2100, 8));
        p.Wav("audio/hit_blunt.wav", HitBlunt());
        p.Wav("audio/hit_sharp.wav", HitSharp());
        p.Wav("audio/punch.wav", Punch());
        p.Wav("audio/crush.wav", Crush());
        p.Wav("audio/tear.wav", Tear());
        p.Wav("audio/throw.wav", Whoosh(0.24, 0.07, 600, 2800, 12, flutter: 0.6));
        p.Wav("audio/clank.wav", Clank());
        p.Wav("audio/pickup.wav", Pickup());
        p.Wav("audio/door.wav", Door());
        p.Wav("audio/glass.wav", Glass());
        p.Wav("audio/death.wav", Death());
        p.Wav("audio/alert.wav", Alert());
        p.Wav("audio/footstep.wav", Footstep(), peak: 0.7);
        p.Wav("audio/ui_move.wav", UiMove(), peak: 0.6);
        p.Wav("audio/ui_select.wav", UiSelect(), peak: 0.7);
        p.Wav("audio/complete.wav", Complete());
        p.Wav("audio/player_death.wav", PlayerDeath());
        p.Wav("audio/music_action.wav", Music.Action(), peak: 0.7, loop: true);
        p.Wav("audio/music_hunt.wav", Music.Hunt(), peak: 0.7, loop: true);
        p.Wav("audio/music_menu.wav", Music.Menu(), peak: 0.6, loop: true);
        p.Wav("audio/dog_bark.wav", DogBark());
    }

    // ------------------------------------------------------------------ guns

    /// <summary>Gunshot recipe: bright crack + noise body + pitch-dropping thump + room tail, saturated.</summary>
    sealed record Shot(double Length, double CrackTau, double CrackHp, double BodyTau, double BodyLp,
                       double ThumpFrom, double ThumpTo, double ThumpTau, double ThumpGain,
                       double TailTau, double TailLp, double TailGain, double Drive, double Slap = 0);

    static readonly Shot Pistol = new(0.32, 0.006, 1800, 0.03, 3800, 190, 70, 0.045, 0.8, 0.10, 2200, 0.25, 2.2);
    static readonly Shot Revolver = new(0.5, 0.008, 1400, 0.055, 3000, 150, 50, 0.08, 1.0, 0.18, 1600, 0.35, 2.8, Slap: 0.07);
    static readonly Shot Shotgun = new(0.55, 0.012, 800, 0.09, 1800, 120, 42, 0.11, 1.2, 0.22, 1100, 0.45, 3.2, Slap: 0.09);
    static readonly Shot Smg = new(0.2, 0.004, 2600, 0.018, 5000, 240, 110, 0.028, 0.55, 0.05, 2800, 0.18, 1.8);
    static readonly Shot Rifle = new(0.45, 0.005, 3200, 0.04, 5500, 170, 60, 0.055, 0.85, 0.22, 2400, 0.35, 2.6, Slap: 0.055);

    static float[] Gunshot(Shot s, int seed)
    {
        var rng = new Random(seed);
        double n = s.Length;
        var crack = Dsp.Noise(n, rng).HighPass(s.CrackHp).Env(t => Math.Exp(-t / s.CrackTau));
        var body = Dsp.Noise(n, rng).LowPass(s.BodyLp).Env(Dsp.Perc(0.0008, s.BodyTau));
        var thump = Dsp.Osc(n, t => s.ThumpTo + (s.ThumpFrom - s.ThumpTo) * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.001, s.ThumpTau));
        var tail = Dsp.Noise(n, rng).LowPass(t => 250 + s.TailLp * Math.Exp(-t / 0.15))
            .Env(t => (1 - Math.Exp(-t / 0.012)) * Math.Exp(-t / s.TailTau));
        var mix = Dsp.Silence(n).Mix(crack, 0, 0.9).Mix(body).Mix(thump, 0, s.ThumpGain).Mix(tail, 0, s.TailGain);
        if (s.Slap > 0) mix = mix.Echo(s.Slap, 0.2, 0.3);
        return mix.SoftClip(s.Drive);
    }

    /// <summary>Hollow pop followed by the fizz of the burning flare.</summary>
    static float[] Flare()
    {
        var rng = new Random(6);
        double n = 0.55;
        var pop = Dsp.Osc(n, t => 110 + 260 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.05));
        var puff = Dsp.Noise(n, rng).LowPass(1300).Env(Dsp.Perc(0.002, 0.045));
        var hiss = Dsp.Noise(n, rng).HighPass(2500).LowPass(t => 7000 - 4000 * t)
            .Env(t => Math.Min(1, t / 0.03) * Math.Exp(-t / 0.22) * (0.8 + 0.2 * Math.Sin(2 * Math.PI * 31 * t)));
        return Dsp.Silence(n).Mix(pop).Mix(puff, 0, 0.8).Mix(hiss, 0.01, 0.45).SoftClip(1.6);
    }

    /// <summary>Short noise tick plus two inharmonic metal resonances.</summary>
    static float[] Tick(Random rng, double ring, double decay, double ringGain = 0.5)
    {
        double n = decay * 7 + 0.01;
        var tick = Dsp.Noise(n, rng).HighPass(1800).Env(t => Math.Exp(-t / 0.0013));
        var res = Dsp.Osc(n, ring).Env(Dsp.Perc(0.0003, decay))
            .Mix(Dsp.Osc(n, ring * 1.61).Env(Dsp.Perc(0.0003, decay * 0.6)), 0, 0.7)
            .Mix(Dsp.Osc(n, ring * 2.37).Env(Dsp.Perc(0.0003, decay * 0.4)), 0, 0.4);
        return tick.Mix(res, 0, ringGain).FadeOut(0.004);
    }

    static float[] Empty()
    {
        var rng = new Random(7);
        return Dsp.Silence(0.12).Mix(Tick(rng, 3200, 0.006)).Mix(Tick(rng, 4400, 0.004), 0.028, 0.5);
    }

    static float[] Pickup()
    {
        var rng = new Random(14);
        return Dsp.Silence(0.32)
            .Mix(Tick(rng, 2300, 0.02, 0.45), 0, 0.7)
            .Mix(Dsp.Noise(0.09, rng).BandPass(t => 1500 + 15000 * t, 2).Env(t => Math.Sin(Math.PI * t / 0.09)), 0.02, 0.3)
            .Mix(Tick(rng, 1650, 0.035, 0.45), 0.13)
            .Mix(Dsp.Osc(0.08, t => 90 + 80 * Math.Exp(-t / 0.01)).Env(Dsp.Perc(0.0005, 0.02)).FadeOut(0.01), 0.13, 0.5);
    }

    // ------------------------------------------------------------------ melee & impacts

    /// <summary>Band-passed noise whose centre frequency rises and falls with the swing.</summary>
    static float[] Whoosh(double len, double peakAt, double fLow, double fHigh, int seed, double flutter = 0)
    {
        var rng = new Random(seed);
        Func<double, double> env = t =>
        {
            double a = t < peakAt ? t / peakAt : (len - t) / (len - peakAt);
            return Math.Pow(Math.Max(0, a), 2) * (1 - flutter * (0.5 + 0.5 * Math.Sin(2 * Math.PI * 28 * t)));
        };
        Func<double, double> cutoff = t => fLow + (fHigh - fLow) * Math.Pow(Math.Sin(Math.PI * Math.Clamp(t / len, 0, 1)), 1.5);
        return Dsp.Noise(len, rng).BandPass(cutoff, 1.3).Env(env).Mix(Dsp.Noise(len, rng).LowPass(cutoff).Env(env), 0, 0.3);
    }

    static float[] HitBlunt()
    {
        var rng = new Random(9);
        double n = 0.28;
        return Dsp.Silence(n)
            .Mix(Dsp.Osc(n, t => 55 + 70 * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.001, 0.06)))
            .Mix(Dsp.Noise(n, rng).LowPass(1600).Env(Dsp.Perc(0.0005, 0.025)), 0, 0.8)
            .Mix(Dsp.Noise(n, rng).BandPass(380, 1.5).Env(Dsp.Perc(0.001, 0.05)), 0, 0.9)
            .SoftClip(2);
    }

    static float[] HitSharp()
    {
        var rng = new Random(10);
        double n = 0.25;
        var ring = Dsp.Osc(n, 2650).Env(Dsp.Perc(0.0005, 0.05)).Mix(Dsp.Osc(n, 4120).Env(Dsp.Perc(0.0005, 0.035)), 0, 0.6);
        return Dsp.Silence(n)
            .Mix(Dsp.Noise(n, rng).HighPass(3500).Env(Dsp.Perc(0.0005, 0.018)), 0, 0.8)
            .Mix(ring, 0, 0.25)
            .Mix(Dsp.Noise(n, rng).LowPass(t => 1400 - 3000 * t).Env(Dsp.Perc(0.004, 0.05)), 0.008, 0.9)
            .Mix(Dsp.Osc(n, t => 90 + 60 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.04)), 0, 0.5)
            .SoftClip(1.5);
    }

    static float[] Punch()
    {
        var rng = new Random(11);
        double n = 0.2;
        return Dsp.Silence(n)
            .Mix(Dsp.Osc(n, t => 70 + 90 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.045)))
            .Mix(Dsp.Noise(n, rng).BandPass(900, 1.2).Env(Dsp.Perc(0.001, 0.03)), 0, 0.9)
            .Mix(Dsp.Noise(n, rng).HighPass(2000).Env(Dsp.Perc(0.0003, 0.008)), 0, 0.6)
            .SoftClip(2.2);
    }

    /// <summary>Skull giving way: heavy thud, a burst of cracks, then a wet squelch.</summary>
    static float[] Crush()
    {
        var rng = new Random(14);
        double n = 0.5;
        var o = Dsp.Silence(n)
            .Mix(Dsp.Osc(n, t => 42 + 80 * Math.Exp(-t / 0.025)).Env(Dsp.Perc(0.001, 0.09)), 0, 1.1)
            .Mix(Dsp.Noise(n, rng).LowPass(900).Env(Dsp.Perc(0.0005, 0.04)), 0, 0.9);
        for (int i = 0; i < 7; i++)
            o.Mix(Dsp.Noise(0.03, rng).HighPass(2200 + rng.Next(2500)).Env(Dsp.Perc(0.0002, 0.004 + rng.NextDouble() * 0.004)), 0.004 + i * 0.006 + rng.NextDouble() * 0.004, 0.7);
        o.Mix(Dsp.Noise(0.32, rng).BandPass(t => 700 + 500 * Math.Sin(t * 38) * Math.Exp(-t / 0.1), 2.5).Env(Dsp.Perc(0.01, 0.12)), 0.03, 0.8);
        return o.SoftClip(2.4);
    }

    /// <summary>Flesh ripping: crackling sinew over a rising, tearing hiss, ending with a wet snap.</summary>
    static float[] Tear()
    {
        var rng = new Random(15);
        double n = 0.45;
        var o = Dsp.Silence(n)
            .Mix(Dsp.Noise(0.3, rng).BandPass(t => 450 + 2600 * t / 0.3, 3).Env(Dsp.Ahr(0.04, 0.18, 0.08)), 0, 0.8);
        for (int i = 0; i < 16; i++)
            o.Mix(Dsp.Noise(0.02, rng).BandPass(1200 + rng.Next(3000), 4).Env(Dsp.Perc(0.0002, 0.003)), 0.01 + rng.NextDouble() * 0.27, 0.8);
        o.Mix(Dsp.Osc(0.1, t => 160 * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.001, 0.03)), 0.29, 0.8)
         .Mix(Dsp.Noise(0.15, rng).BandPass(800, 1.5).Env(Dsp.Perc(0.002, 0.05)), 0.29, 0.9);
        return o.SoftClip(2);
    }

    /// <summary>Metal object hitting a wall: inharmonic partials with different decays.</summary>
    static float[] Clank()
    {
        var rng = new Random(13);
        double n = 0.6;
        var o = Dsp.Silence(n);
        double[] freq = [612, 1373, 2207, 3164, 4031];
        double[] decay = [0.32, 0.22, 0.15, 0.1, 0.07];
        for (int i = 0; i < freq.Length; i++)
            o.Mix(Dsp.Osc(n, freq[i] * (1 + 0.004 * rng.NextDouble()), Wave.Sine, rng.NextDouble()).Env(Dsp.Perc(0.0005, decay[i])), 0, 1.0 / (1 + i * 0.5));
        o.Mix(Dsp.Noise(n, rng).HighPass(1200).Env(Dsp.Perc(0.0003, 0.006)), 0, 1.2);
        o.Mix(Dsp.Osc(n, t => 80 + 80 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.03)), 0, 0.6);
        return o;
    }

    /// <summary>Wooden door slammed open: low thump plus a few wood body modes and a short room.</summary>
    static float[] Door()
    {
        var rng = new Random(15);
        double n = 0.45;
        var o = Dsp.Silence(n).Mix(Dsp.Osc(n, t => 75 + 60 * Math.Exp(-t / 0.02)).Env(Dsp.Perc(0.001, 0.07)));
        double[] modes = [182, 287, 421, 613, 870];
        for (int i = 0; i < modes.Length; i++)
            o.Mix(Dsp.Osc(n, modes[i]).Env(Dsp.Perc(0.001, 0.09 / (1 + i * 0.4))), 0, 0.5 / (1 + i * 0.3));
        o.Mix(Dsp.Noise(n, rng).BandPass(1100, 0.9).Env(Dsp.Perc(0.0005, 0.012)), 0, 0.9);
        return o.Reverb(0.4, 0.5, 0.15).SoftClip(1.5);
    }

    /// <summary>Crash of noise followed by a shower of short high pings.</summary>
    static float[] Glass()
    {
        var rng = new Random(16);
        double n = 0.9;
        var o = Dsp.Silence(n)
            .Mix(Dsp.Noise(n, rng).HighPass(2200).Env(Dsp.Perc(0.0005, 0.05)))
            .Mix(Dsp.Noise(n, rng).BandPass(4500, 0.8).Env(Dsp.Perc(0.002, 0.12)), 0, 0.5);
        for (int i = 0; i < 46; i++)
        {
            double at = Math.Pow(rng.NextDouble(), 1.8) * 0.65, f = 2400 + rng.NextDouble() * 3600, d = 0.012 + rng.NextDouble() * 0.05;
            var ping = Dsp.Osc(d * 6, f).Env(Dsp.Perc(0.0005, d)).Mix(Dsp.Osc(d * 6, f * 1.47).Env(Dsp.Perc(0.0005, d * 0.7)), 0, 0.5).FadeOut(d);
            o.Mix(ping, at, 0.35 * (1 - at) * (0.4 + 0.6 * rng.NextDouble()));
        }
        return o;
    }

    /// <summary>Wet thud: low body hit plus resonant, falling "squelch" noise.</summary>
    static float[] Death()
    {
        var rng = new Random(17);
        double n = 0.5;
        return Dsp.Silence(n)
            .Mix(Dsp.Osc(n, t => 48 + 70 * Math.Exp(-t / 0.04)).Env(Dsp.Perc(0.002, 0.12)))
            .Mix(Dsp.Noise(n, rng).LowPass(700).Env(Dsp.Perc(0.002, 0.07)), 0, 0.8)
            .Mix(Dsp.Noise(n, rng).BandPass(t => 1300 * Math.Exp(-t / 0.08) + 250, 3).Env(Dsp.Perc(0.01, 0.09)), 0.02)
            .Mix(Dsp.Noise(n, rng).BandPass(t => 900 - 1500 * t, 4).Env(Dsp.Perc(0.01, 0.05)), 0.16, 0.5)
            .SoftClip(1.8);
    }

    static float[] Footstep()
    {
        var rng = new Random(19);
        double n = 0.08;
        return Dsp.Noise(n, rng).LowPass(1000).Env(Dsp.Perc(0.001, 0.012))
            .Mix(Dsp.Osc(n, t => 85 + 50 * Math.Exp(-t / 0.01)).Env(Dsp.Perc(0.001, 0.015)), 0, 0.7)
            .Mix(Dsp.Noise(n, rng).BandPass(2500, 1).Env(Dsp.Perc(0.001, 0.006)), 0, 0.25);
    }

    static float[] DogBark()
    {
        var rng = new Random(24);
        double n = 0.32;
        var src = Dsp.Osc(n, t => 290 + 300 * Math.Exp(-t / 0.05), Wave.Saw).Mix(Dsp.Noise(n, rng), 0, 0.35);
        return Dsp.Silence(n)
            .Mix(src.BandPass(t => 850 - 300 * t, 4))
            .Mix(src.BandPass(1650, 5), 0, 0.6)
            .Mix(src.BandPass(2900, 6), 0, 0.25)
            .Env(t => Math.Min(1, t / 0.008) * Math.Exp(-t / 0.07))
            .SoftClip(2.5)
            .Reverb(0.3, 0.5, 0.1);
    }

    // ------------------------------------------------------------------ ui & stings

    static float[] Alert()
    {
        double n = 0.2;
        Func<double, double> sweep = t => 520 + 900 * Math.Min(1, t / 0.12);
        var a = Dsp.Osc(n, sweep, Wave.Triangle);
        var b = Dsp.Osc(n, t => 2 * sweep(t));
        return a.Mix(b, 0, 0.35).Env(Dsp.Ahr(0.004, 0.12, 0.02));
    }

    static float[] UiMove() => Dsp.Osc(0.06, 1250, Wave.Triangle).Env(Dsp.Perc(0.001, 0.018));

    static float[] UiSelect() => Dsp.Silence(0.2)
        .Mix(Blip(880, 0.05)).Mix(Blip(1320, 0.08), 0.06);

    static float[] Blip(double f, double len) =>
        Dsp.Osc(len + 0.05, f, Wave.Square).LowPass(3000).Mix(Dsp.Osc(len + 0.05, f, Wave.Triangle)).Env(Dsp.Ahr(0.002, len, 0.012)).FadeOut(0.01);

    static float[] Lead(double f, double len) =>
        Dsp.Osc(len + 0.3, f, Wave.Square).Mix(Dsp.Osc(len + 0.3, f * 1.004, Wave.Saw), 0, 0.5)
            .LowPass(t => 1200 + 3000 * Math.Exp(-t / 0.08)).Env(Dsp.Ahr(0.004, len, 0.07)).FadeOut(0.05);

    /// <summary>Quick rising major arpeggio landing on a held chord.</summary>
    static float[] Complete()
    {
        var o = Dsp.Silence(1.15);
        double[] notes = [72, 76, 79, 84];
        for (int i = 0; i < notes.Length; i++)
            o.Mix(Lead(Dsp.Midi(notes[i]), i == 3 ? 0.6 : 0.12), i * 0.09, i == 3 ? 0.9 : 0.7);
        foreach (double m in new[] { 60.0, 64, 67 })
            o.Mix(Dsp.Osc(0.85, Dsp.Midi(m), Wave.Triangle).Env(Dsp.Swell(0.85, 0.05, 0.5)), 0.27, 0.25);
        return o.Echo(0.135, 0.3, 0.3).FadeOut(0.1);
    }

    /// <summary>Detuned saws sliding down two octaves under a closing filter, over a low hit.</summary>
    static float[] PlayerDeath()
    {
        var rng = new Random(23);
        double n = 1.15;
        Func<double, double> slide = t => Dsp.Midi(57 - 26 * Math.Pow(Math.Min(t / 0.95, 1), 0.8));
        var mix = Dsp.Osc(n, slide, Wave.Saw)
            .Mix(Dsp.Osc(n, t => slide(t) * 1.012, Wave.Saw), 0, 0.8)
            .Mix(Dsp.Osc(n, t => slide(t) * 1.059 * 0.5, Wave.Saw), 0, 0.5)
            .LowPass(t => 2400 * Math.Exp(-t / 0.35) + 150, 1.2)
            .Env(Dsp.Swell(n, 0.01, 0.4));
        mix.Mix(Dsp.Osc(n, t => 40 + 70 * Math.Exp(-t / 0.05)).Env(Dsp.Perc(0.002, 0.25)), 0, 1.2);
        mix.Mix(Dsp.Noise(n, rng).LowPass(500).Env(Dsp.Perc(0.002, 0.1)), 0, 0.5);
        return mix.Reverb(0.7, 0.4, 0.25).SoftClip(1.3);
    }
}
