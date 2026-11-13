namespace PlaceholderArt;

/// <summary>
/// Original music loops. The action tracks follow the dark synth style of early-2010s top-down action
/// soundtracks (pounding four-on-the-floor kick, distorted 16th-note bass pumping against it,
/// phrygian/minor harmony with flat-second and tritone tension, aggressive detuned leads, noise
/// risers); the horror loop follows dark ambient practice (beating drones, dissonant clusters,
/// irregular pulses, an endlessly rising Shepard–Risset glissando). No melody, sample or sound comes
/// from an existing track.
/// </summary>
public static class Music
{
    const double Tau = 2 * Math.PI;
    const int Rest = 0;
    const int Hold = -1;

    /// <summary>One dark synth track: tempo, harmony per bar, bass pattern and lead motif.</summary>
    public sealed record Track(
        double Bpm,
        int[] Roots,          // bass root (MIDI) per bar
        int[][] Chords,       // pad chord (MIDI) per bar
        int[] Bass,           // 16 offsets (semitones) from the bar's root, one per 16th note
        int[] Lead,           // 32 steps (2 bars): MIDI note, Rest or Hold
        int LeadFromBar,
        int Seed,
        double BassDrive = 3,
        bool OffbeatStabs = false);

    /// <summary>Levels 1 and 3: hypnotic and aggressive, E phrygian, 125 BPM, 16 bars.</summary>
    public static float[] Action() => Darksynth(new Track(
        Bpm: 125,
        Roots: [40, 40, 41, 40, 40, 40, 36, 38, 40, 40, 41, 40, 40, 40, 36, 35],
        Chords:
        [
            [52, 55, 59], [52, 55, 59], [53, 57, 60], [52, 55, 59],
            [52, 55, 59], [52, 55, 59], [48, 52, 55], [50, 53, 57],
            [52, 55, 59], [52, 55, 59], [53, 57, 60], [52, 55, 59],
            [52, 55, 59], [52, 55, 59], [48, 52, 55], [47, 50, 53],
        ],
        Bass: [0, 0, 12, 0, 0, 0, 12, 0, 0, 0, 12, 0, 1, 0, 12, 10],
        Lead:
        [
            76, Hold, Hold, 79, Hold, Hold, 77, Hold, 76, Hold, 74, Hold, 76, Hold, Hold, Rest,
            76, Hold, Hold, 79, Hold, Hold, 82, Hold, 81, Hold, 79, Hold, 77, Hold, 76, Rest,
        ],
        LeadFromBar: 8,
        Seed: 501));

    /// <summary>Level 2: faster and frantic, C# minor with flat-second stabs, 138 BPM, 16 bars.</summary>
    public static float[] Hunt() => Darksynth(new Track(
        Bpm: 138,
        Roots: [37, 37, 38, 37, 37, 37, 33, 35, 37, 37, 38, 37, 37, 37, 33, 32],
        Chords:
        [
            [49, 52, 56], [49, 52, 56], [50, 54, 57], [49, 52, 56],
            [49, 52, 56], [49, 52, 56], [45, 49, 52], [47, 50, 54],
            [49, 52, 56], [49, 52, 56], [50, 54, 57], [49, 52, 56],
            [49, 52, 56], [49, 52, 56], [45, 49, 52], [44, 48, 51],
        ],
        Bass: [0, 12, 0, 12, 0, 12, 0, 13, 0, 12, 0, 12, 1, 13, 0, 12],
        Lead:
        [
            73, Hold, 73, 76, Hold, 73, 74, Hold, 73, Rest, 79, Hold, 78, Hold, 76, Hold,
            73, Hold, 73, 76, Hold, 73, 74, Hold, 73, Rest, 71, Hold, 72, Hold, 73, Rest,
        ],
        LeadFromBar: 4,
        Seed: 502,
        BassDrive: 4,
        OffbeatStabs: true));

    /// <summary>
    /// Builds a dark synth loop. Two halves of 8 bars: the second adds the lead and busier drums,
    /// each half ends with a snare roll and a noise riser, the next one starts with an impact.
    /// Bass and pads duck under every kick (side-chain pumping).
    /// </summary>
    public static float[] Darksynth(Track track)
    {
        int bars = track.Roots.Length;
        double beat = 60 / track.Bpm, step = beat / 4, bar = beat * 4, length = bar * bars;
        var rng = new Random(track.Seed);
        var drums = new Loop(length);
        var hats = new Loop(length);
        var snares = new Loop(length);
        var bass = new Loop(length);
        var pad = new Loop(length);
        var lead = new Loop(length);
        var fx = new Loop(length);
        float[] kick = Kick(rng), snare = Clap(rng), hat = Hat(rng, 0.02), openHat = Hat(rng, 0.12), stab = Stab(rng);
        int half = bars / 2;

        for (int s = 0; s < bars * 16; s++)
        {
            double t = s * step;
            int b = s / 16, i = s % 16;
            bool second = b >= half;
            bool lastBarOfHalf = b % half == half - 1;

            if (i % 4 == 0 && !(lastBarOfHalf && i == 12)) drums.Add(kick, t);
            if (i == 4 || i == 12) snares.Add(snare, t);
            if (second && i == 15) snares.Add(snare, t, 0.25);
            if (lastBarOfHalf && i >= 8) snares.Add(snare, t, 0.25 + 0.09 * (i - 8));

            bool open = second && i % 4 == 2;
            hats.Add(open ? openHat : hat, t, open ? 0.45 : i % 4 == 2 ? 0.5 : i % 2 == 1 ? 0.3 : 0.18);

            // The bass plays every 16th note (offsets from the bar's root).
            double velocity = i % 4 == 0 ? 1.0 : i % 2 == 0 ? 0.85 : 0.7;
            bass.Add(BassNote(Dsp.Midi(track.Roots[b] + track.Bass[i]), step * 0.92, velocity, track.BassDrive), t, velocity);

            if (track.OffbeatStabs && second && (i == 6 || i == 14))
                fx.Add(Stab(track.Chords[b], stab), t, 0.5);
        }

        for (int b = 0; b < bars; b++)
            foreach (int note in track.Chords[b])
                pad.Add(PadNote(Dsp.Midi(note), bar + 0.4), b * bar);

        for (int b = track.LeadFromBar; b < bars; b++)
            AddLead(lead, track.Lead, b, step);

        for (int h = 0; h < bars; h += half)
        {
            fx.Add(Riser(bar, rng), (h + half - 1) * bar, 0.5);
            fx.Add(Impact(rng), h * bar, 0.9);
        }

        Func<int, double> duck = SideChain(beat, length);
        bass.Apply(x => Duck(x, duck, 0.75));
        pad.Apply(x => Duck(x, duck, 0.85));
        snares.Apply(x => GatedReverb(x));
        drums.Apply(x => x.Reverb(0.35, 0.6, 0.06));
        hats.Apply(x => x.Echo(step * 3, 0.2, 0.2));
        lead.Apply(x => x.Echo(beat * 0.75, 0.35, 0.35).Reverb(0.7, 0.5, 0.25));
        pad.Apply(x => x.Reverb(0.85, 0.5, 0.4));
        fx.Apply(x => x.Reverb(0.8, 0.4, 0.3));
        foreach (var l in new[] { drums, hats, snares, bass, pad, lead, fx }) l.Normalize(1);

        return new Loop(length)
            .Mix(drums, 0.85).Mix(hats, 0.3).Mix(snares, 0.6).Mix(bass, 0.7).Mix(pad, 0.3).Mix(lead, 0.42).Mix(fx, 0.35)
            .Apply(x => x.SoftClip(1.5))
            .Take();
    }

    /// <summary>
    /// Menu: slow, ominous and unresolved. A pulsing eighth-note arpeggio over a pedal bass,
    /// a heartbeat-like kick on beats 1 and 3, and a tritone bell every four bars. 92 BPM, 16 bars.
    /// </summary>
    public static float[] Menu()
    {
        const double bpm = 92;
        double beat = 60 / bpm, eighth = beat / 2, bar = beat * 4;
        const int bars = 16;
        double length = bar * bars;
        var rng = new Random(503);
        var arp = new Loop(length);
        var bass = new Loop(length);
        var pad = new Loop(length);
        var pulse = new Loop(length);
        var bell = new Loop(length);
        int[][] chords = [[45, 48, 52, 53], [45, 48, 52, 56], [43, 46, 50, 51], [44, 48, 51, 55]];
        int[] roots = [33, 33, 31, 32];
        int[] order = [0, 1, 2, 3, 2, 1, 2, 3];
        float[] kick = Kick(rng);

        for (int e = 0; e < bars * 8; e++)
        {
            double t = e * eighth;
            int b = e / 8, block = b / 4 % chords.Length, i = e % 8;
            int note = chords[block][order[i]] + 12;
            arp.Add(ArpNote(Dsp.Midi(note), eighth * 0.85, 400 + 1800 * (0.5 + 0.5 * Math.Sin(Tau * t / (bar * 4)))), t, i == 0 ? 1.0 : 0.7);
            if (b >= 4 && (i == 0 || i == 4)) pulse.Add(kick, t, i == 0 ? 0.9 : 0.6);
        }

        for (int b = 0; b < bars; b += 4)
        {
            int block = b / 4 % chords.Length;
            bass.Add(BassNote(Dsp.Midi(roots[block]), bar * 4 - 0.1, 0.6, 2), b * bar);
            foreach (int note in chords[block]) pad.Add(PadNote(Dsp.Midi(note), bar * 4 + 1), b * bar);
            bell.Add(Bell(Dsp.Midi(roots[block] + 42), 3), b * bar + bar * 2);
            bell.Add(Bell(Dsp.Midi(roots[block] + 48), 3), b * bar + bar * 3.5, 0.6);
        }

        arp.Apply(x => x.Echo(beat * 0.75, 0.45, 0.4).Reverb(0.8, 0.5, 0.3));
        pad.Apply(x => x.Reverb(0.9, 0.5, 0.5));
        bell.Apply(x => x.Echo(beat, 0.5, 0.45).Reverb(0.95, 0.4, 0.6));
        pulse.Apply(x => x.LowPass(500).Reverb(0.7, 0.6, 0.25));
        foreach (var l in new[] { arp, bass, pad, pulse, bell }) l.Normalize(1);
        return new Loop(length).Mix(arp, 0.45).Mix(bass, 0.5).Mix(pad, 0.35).Mix(pulse, 0.55).Mix(bell, 0.22)
            .Apply(x => x.SoftClip(1.3))
            .Take();
    }

    /// <summary>
    /// Horror loop (40 s): a sub drone that beats against itself, a dissonant E-F-B♭ cluster swelling
    /// in and out, an irregular double pulse that sometimes misses a beat, metallic scrapes, a faint
    /// tinnitus whine, and under everything a Shepard–Risset glissando that rises forever (one octave
    /// per loop, so the loop is seamless).
    /// </summary>
    public static float[] Dread()
    {
        const double length = 40;
        var rng = new Random(504);
        var drone = new Loop(length);
        var cluster = new Loop(length);
        var pulse = new Loop(length);
        var scrape = new Loop(length);
        var shepard = new Loop(length);
        var whine = new Loop(length);

        // Sub drone: E1 against a slightly detuned copy (slow throb), plus a quiet F1 rub.
        double e1 = Dsp.Midi(28), f1 = Dsp.Midi(29);
        double beatA = Math.Round(e1 * 1.0045 * length) / length, baseE = Math.Round(e1 * length) / length, baseF = Math.Round(f1 * length) / length;
        drone.AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate;
            return 0.5 * Math.Sin(Tau * baseE * t) + 0.45 * Math.Sin(Tau * beatA * t) + 0.12 * Math.Sin(Tau * baseF * t)
                + 0.15 * Math.Sin(Tau * baseE * 2 * t) * (0.5 + 0.5 * Math.Sin(Tau * t / length * 3));
        });

        // Dissonant cluster swelling twice per loop.
        foreach ((double at, double dur) in new[] { (2.0, 16.0), (22.0, 15.0) })
            foreach (int note in new[] { 52, 53, 58, 64 })
                cluster.Add(ClusterVoice(Dsp.Midi(note), dur, rng), at, note == 64 ? 0.4 : 1);

        // Double pulse ("lub-dub") at 66 BPM; some beats are missing, which is worse than a steady one.
        double period = 60 / 66.0;
        int beats = (int)(length / period);
        float[] thump = Thump(rng);
        for (int k = 0; k < beats; k++)
        {
            if (k % 7 == 3 || k % 11 == 6) continue;
            double t = k * length / beats;
            pulse.Add(thump, t, 0.9).Add(thump, t + 0.21, 0.55);
        }

        foreach (double at in new[] { 9.0, 27.5, 35.0 }) scrape.Add(Scrape(rng), at, at > 30 ? 0.6 : 1);

        // Shepard–Risset glissando: octave-spaced sines under a fixed bell curve, rising one octave per
        // loop. Partial o has f(t) = base * 2^(o + t/L); its phase is base * 2^o * L/ln2 * (2^(t/L) - 1).
        // The base is chosen so that phase is a whole number of turns at t = L: no click at the loop point.
        double turns = Math.Round(55 * length / Math.Log(2));
        double baseFrequency = turns * Math.Log(2) / length;
        shepard.AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate, u = t / length, sum = 0;
            for (int o = 0; o < 6; o++)
            {
                double pos = (o + u) / 6;                      // 0..1 across six octaves
                double amp = Math.Exp(-Math.Pow((pos - 0.5) / 0.18, 2));
                double phase = baseFrequency * Math.Pow(2, o) * length / Math.Log(2) * (Math.Pow(2, u) - 1);
                sum += amp * Math.Sin(Tau * phase);
            }
            return sum;
        });

        whine.AddPeriodic(i =>
        {
            double t = i / (double)Dsp.Rate;
            double f = Math.Round(2900 * length) / length;
            return Math.Sin(Tau * f * t) * Math.Pow(0.5 - 0.5 * Math.Cos(Tau * t / length), 3);
        });

        cluster.Apply(x => x.Reverb(0.95, 0.5, 0.6));
        pulse.Apply(x => x.LowPass(220).Reverb(0.8, 0.6, 0.3));
        scrape.Apply(x => x.Reverb(0.95, 0.3, 0.7));
        foreach (var l in new[] { drone, cluster, pulse, scrape, shepard, whine }) l.Normalize(1);
        return new Loop(length)
            .Mix(drone, 0.45).Mix(cluster, 0.5).Mix(pulse, 0.6).Mix(scrape, 0.3).Mix(shepard, 0.16).Mix(whine, 0.035)
            .Take();
    }

    // ------------------------------------------------------------------ instruments

    static float[] Kick(Random rng)
    {
        double n = 0.42;
        return Dsp.Osc(n, t => 44 + 150 * Math.Exp(-t / 0.035)).Env(Dsp.Perc(0.001, 0.2))
            .Mix(Dsp.Noise(n, rng).HighPass(3000).Env(t => Math.Exp(-t / 0.002)), 0, 0.5)
            .SoftClip(2.6)
            .LowPass(6000)
            .FadeOut(0.08);
    }

    /// <summary>Clap/snare: three quick noise bursts and a tonal body.</summary>
    static float[] Clap(Random rng)
    {
        double n = 0.35;
        var o = Dsp.Silence(n);
        foreach (double at in new[] { 0.0, 0.011, 0.022 })
            o.Mix(Dsp.Noise(0.2, rng).BandPass(1600, 0.8).Env(Dsp.Perc(0.0005, at < 0.02 ? 0.006 : 0.08)), at);
        return o.Mix(Dsp.Osc(n, t => 190 + 60 * Math.Exp(-t / 0.01)).Env(Dsp.Perc(0.001, 0.06)), 0, 0.7)
            .Mix(Dsp.Noise(n, rng).HighPass(6000).Env(Dsp.Perc(0.001, 0.05)), 0, 0.4)
            .SoftClip(1.8)
            .FadeOut(0.05);
    }

    static float[] Hat(Random rng, double decay) =>
        Dsp.Noise(decay * 6 + 0.01, rng).HighPass(7500).Env(Dsp.Perc(0.0005, decay));

    /// <summary>Distorted saw bass with a resonant filter snap; the drive gives the grit.</summary>
    static float[] BassNote(double f, double len, double velocity, double drive)
    {
        double n = len + 0.08;
        return Dsp.Osc(n, f * 1.006, Wave.Saw).Mix(Dsp.Osc(n, f / 1.006, Wave.Saw)).Mix(Dsp.Osc(n, f * 0.5, Wave.Square), 0, 0.45)
            .LowPass(t => 130 + 2600 * velocity * Math.Exp(-t / 0.07), 2.4)
            .SoftClip(drive)
            .LowPass(3800)
            .Env(Dsp.Ahr(0.002, len, 0.02))
            .FadeOut(0.015);
    }

    /// <summary>Aggressive lead: three detuned saws, a sub square, a pitch scoop into each note.</summary>
    static float[] LeadNote(double f, double len)
    {
        double n = len + 0.15;
        Func<double, double> pitch = t => f * Math.Pow(2, -1.0 / 12 * Math.Exp(-t / 0.025));
        var o = Dsp.Silence(n);
        foreach (double detune in new[] { 0.993, 1.0, 1.007 })
            o.Mix(Dsp.Osc(n, t => pitch(t) * detune, Wave.Saw), 0, 0.33);
        return o.Mix(Dsp.Osc(n, t => pitch(t) * 0.5, Wave.Square), 0, 0.25)
            .LowPass(t => 1400 + 4200 * Math.Exp(-t / 0.12), 1.2)
            .SoftClip(1.8)
            .Env(Dsp.Ahr(0.004, len, 0.09))
            .FadeOut(0.03);
    }

    static void AddLead(Loop lead, int[] motif, int bar, double step)
    {
        int first = bar % 2 * 16;
        for (int k = first; k < first + 16; k++)
        {
            if (motif[k] <= 0) continue;
            int steps = 1;
            while (k + steps < motif.Length && motif[k + steps] == Hold) steps++;
            lead.Add(LeadNote(Dsp.Midi(motif[k]), steps * step * 0.95), (bar * 16 + k - first) * step);
        }
    }

    static float[] Stab(Random rng) => Dsp.Noise(0.05, rng).HighPass(3000).Env(Dsp.Perc(0.0005, 0.01));

    /// <summary>Short distorted chord stab on the off-beat.</summary>
    static float[] Stab(int[] chord, float[] click)
    {
        double n = 0.25;
        var o = Dsp.Silence(n);
        foreach (int note in chord)
            o.Mix(Dsp.Osc(n, Dsp.Midi(note + 12) * 1.004, Wave.Saw).Mix(Dsp.Osc(n, Dsp.Midi(note + 12) / 1.004, Wave.Saw)), 0, 0.3);
        return o.LowPass(t => 900 + 3000 * Math.Exp(-t / 0.03), 1.5).SoftClip(2).Env(Dsp.Perc(0.002, 0.07)).Mix(click, 0, 0.3).FadeOut(0.03);
    }

    static float[] PadNote(double f, double len)
    {
        var o = Dsp.Osc(len, f * 1.006, Wave.Saw, 0.2).Mix(Dsp.Osc(len, f / 1.006, Wave.Saw, 0.7)).Mix(Dsp.Osc(len, f * 0.5, Wave.Triangle), 0, 0.5);
        return o.LowPass(t => 500 + 700 * Math.Pow(Math.Sin(Math.PI * Math.Min(1, t / len)), 2), 0.9).Env(Dsp.Swell(len, 0.6, 0.8));
    }

    static float[] ArpNote(double f, double len, double cutoff) =>
        Dsp.Osc(len + 0.25, f, Wave.Square).Mix(Dsp.Osc(len + 0.25, f * 1.005, Wave.Saw), 0, 0.5)
            .LowPass(t => cutoff + 1500 * Math.Exp(-t / 0.04), 1.6)
            .Env(Dsp.Ahr(0.002, len * 0.4, 0.12))
            .FadeOut(0.04);

    static float[] Bell(double f, double len) =>
        Dsp.Osc(len, f).Mix(Dsp.Osc(len, f * 2.76), 0, 0.4).Mix(Dsp.Osc(len, f * 5.4), 0, 0.15).Env(Dsp.Perc(0.002, 0.9)).FadeOut(0.2);

    /// <summary>Rising white noise over one bar, with a climbing tone underneath.</summary>
    static float[] Riser(double len, Random rng)
    {
        return Dsp.Noise(len, rng).HighPass(t => 300 + 7000 * Math.Pow(t / len, 2), 1.2)
            .Mix(Dsp.Osc(len, t => 180 * Math.Pow(2, 2 * t / len), Wave.Saw).LowPass(1800), 0, 0.25)
            .Env(t => Math.Pow(t / len, 2.2))
            .FadeOut(0.01);
    }

    /// <summary>Low boom and noise crash at the start of a section.</summary>
    static float[] Impact(Random rng)
    {
        double n = 2.2;
        return Dsp.Osc(n, t => 30 + 60 * Math.Exp(-t / 0.08)).Env(Dsp.Perc(0.002, 0.7))
            .Mix(Dsp.Noise(n, rng).LowPass(t => 6000 * Math.Exp(-t / 0.6) + 300).Env(Dsp.Perc(0.002, 0.5)), 0, 0.6)
            .SoftClip(1.5)
            .FadeOut(0.3);
    }

    static float[] ClusterVoice(double f, double len, Random rng)
    {
        var o = Dsp.Silence(len);
        foreach (double detune in new[] { -0.012, 0.004, 0.015 })
            o.Mix(Dsp.Osc(len, t => f * (1 + detune) * (1 + 0.003 * Math.Sin(Tau * 0.21 * t + detune * 90)), Wave.Saw, rng.NextDouble()), 0, 0.33);
        return o.LowPass(t => 300 + 900 * Math.Pow(Math.Sin(Math.PI * t / len), 3), 1.1).Env(Dsp.Swell(len, len * 0.45, len * 0.4));
    }

    static float[] Thump(Random rng)
    {
        double n = 0.5;
        return Dsp.Osc(n, t => 38 + 50 * Math.Exp(-t / 0.03)).Env(Dsp.Perc(0.004, 0.16))
            .Mix(Dsp.Noise(n, rng).LowPass(400).Env(Dsp.Perc(0.002, 0.05)), 0, 0.3)
            .SoftClip(1.4)
            .FadeOut(0.1);
    }

    /// <summary>Metal dragged on concrete: noise through moving resonant band-passes.</summary>
    static float[] Scrape(Random rng)
    {
        double n = 3.5;
        var wobble = Dsp.Wander(rng, n, 7);
        var o = Dsp.Noise(n, rng);
        return Dsp.Silence(n)
            .Mix(((float[])o.Clone()).BandPass(t => 900 + 600 * t / n + 250 * wobble(t), 14))
            .Mix(((float[])o.Clone()).BandPass(t => 2300 + 400 * wobble(t * 1.3), 18), 0, 0.6)
            .Env(t => Dsp.Swell(n, 0.4, 1.6)(t) * (0.6 + 0.4 * Math.Abs(wobble(t * 3))))
            .SoftClip(1.6);
    }

    // ------------------------------------------------------------------ mixing helpers

    /// <summary>
    /// Gain curve dipping on every beat (where the kick hits) and recovering. It depends on the
    /// position within the loop, so every repetition of the loop is identical.
    /// </summary>
    static Func<int, double> SideChain(double beat, double length)
    {
        int loop = Dsp.Len(length);
        return i =>
        {
            double t = (i % loop) / (double)Dsp.Rate;
            double sinceBeat = t - Math.Floor(t / beat + 1e-9) * beat;
            return 1 - Math.Exp(-sinceBeat / 0.11);
        };
    }

    static float[] Duck(float[] signal, Func<int, double> curve, double depth)
    {
        for (int i = 0; i < signal.Length; i++) signal[i] *= (float)(1 - depth * (1 - curve(i)));
        return signal;
    }

    /// <summary>80s gated reverb: big reverb cut short after each hit.</summary>
    static float[] GatedReverb(float[] signal)
    {
        var wet = signal.Reverb(0.9, 0.3, 1.2);
        // Gate opens with the dry signal and closes ~180 ms later.
        double hold = 0;
        float peak = Dsp.Peak(signal);
        for (int i = 0; i < wet.Length; i++)
        {
            if (Math.Abs(signal[i]) > 0.3 * peak) hold = 0.18;
            else hold -= 1.0 / Dsp.Rate;
            double gate = hold > 0 ? 1 : Math.Max(0, 1 + hold / 0.03);
            wet[i] = (float)(signal[i] + (wet[i] - signal[i]) * gate);
        }
        return wet;
    }
}
