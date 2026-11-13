using PlaceholderArt.Rig;

namespace PlaceholderArt;

/// <summary>
/// NoctPrototype pack sprites, on top of the shared thermal set (<see cref="NoctArt"/>): three
/// creatures with a mottled heat signature and hot spots (so they never read like a warm human),
/// searchable containers, pickups that stand out, outdoor ground and a chain-link fence.
/// </summary>
public static class NoctPrototypeArt
{
    static Rgba G(float v, float a = 1f) => Rgba.Grey(v, a);

    /// <summary>Grey varying with a smooth noise, for skin that is neither cold nor uniformly warm.</summary>
    static Func<float, float, Rgba> Mottled(float lo, float hi, int seed, float scale = 18f) =>
        (x, y) => G(lo + (hi - lo) * Noise.Tile(x / scale, y / scale, 64, 64, 2, seed));

    public static void Generate(Pack pack)
    {
        Sheet(pack, "scavenger", 40, frame => (c, x, y) => Scavenger(c, x, y, frame), halo: 0.3f, seed: 301);
        Sheet(pack, "leaper", 40, frame => (c, x, y) => Leaper(c, x, y, frame), halo: 0.4f, seed: 302);
        Sheet(pack, "watcher", 52, frame => (c, x, y) => Watcher(c, x, y, frame), halo: 0.12f, seed: 303);
        AnimatedCreatures(pack);

        pack.Png("sprites/props/locker.png", Locker(open: false));
        pack.Png("sprites/props/locker_open.png", Locker(open: true));
        pack.Png("sprites/props/supply_crate.png", SupplyCrate(open: false));
        pack.Png("sprites/props/supply_crate_open.png", SupplyCrate(open: true));
        pack.Png("sprites/props/radio_console.png", RadioConsole());
        pack.Png("sprites/props/barrier.png", Barrier());
        pack.Png("sprites/props/antenna.png", Antenna());
        pack.Png("sprites/props/landing_mark.png", LandingMark());

        pack.Png("sprites/items/medkit.png", Pickup(Medkit));
        pack.Png("sprites/items/ammo_9mm.png", Pickup(AmmoBox));
        pack.Png("sprites/items/ammo_shells.png", Pickup(ShellBox));
        pack.Png("sprites/items/battery.png", Battery());

        pack.Png("sprites/tiles/ground_gravel.png", Gravel());
        pack.Png("sprites/tiles/fence.png", Fence());

        // Structure seen from far above: slabs, asphalt, thick concrete walls, roofs; industrial objects.
        pack.Png("sprites/tiles/floor_slab.png", Slab());
        pack.Png("sprites/tiles/asphalt.png", Asphalt());
        pack.Png("sprites/tiles/wall_concrete.png", ConcreteWall());
        pack.Png("sprites/tiles/roof_flat.png", RoofFlat());
        pack.Png("sprites/tiles/roof_metal.png", RoofMetal());
        foreach (var (name, sprite) in Industrial.All()) pack.Png($"sprites/props/{name}.png", sprite);
    }

    // ------------------------------------------------------------------ creatures

    /// <summary>Four animation frames side by side, plus a cooled-down corpse on a fading heat pool.</summary>
    static void Sheet(Pack pack, string name, int size, Func<int, Action<Canvas, float, float>> draw, float halo, int seed)
    {
        var sheet = new Canvas(size * 4, size);
        for (int frame = 0; frame < 4; frame++)
        {
            var one = new Canvas(size, size);
            draw(frame)(one, size / 2f, size / 2f);
            Signature(one, seed + frame, halo);
            sheet.Draw(one, frame * size, 0);
        }
        pack.Png($"sprites/actors/{name}.png", sheet);

        var body = new Canvas(size, size);
        draw(0)(body, size / 2f, size / 2f);
        body.Map((_, _, p) => p.Shade(0.72f));
        Signature(body, seed + 9, halo * 0.3f);
        var dead = new Canvas(size, size);
        NoctArt.HeatPool(dead, size / 2f + 2f, size / 2f, size * 0.3f, seed + 10);
        dead.Draw(body);
        pack.Png($"sprites/actors/{name}_dead.png", dead);
    }

    /// <summary>Soft edges, a faint warm halo and sensor grain.</summary>
    static void Signature(Canvas c, int seed, float halo)
    {
        c.Blur(0.35f);
        if (halo > 0f) c.Glow(G(0.9f), 1.6f, halo);
        NoctArt.Grain(c, seed, 0.035f);
    }

    /// <summary>A hot spot that glows (mouth, heart, eyes).</summary>
    static void Hot(Canvas c, Shape shape, float glow = 0.6f)
    {
        var hot = new Canvas(c.Width, c.Height);
        hot.Fill(shape, G(0.98f));
        hot.Glow(G(1f), 1.3f, glow);
        c.Draw(hot);
    }

    /// <summary>Charognard: low, gaunt, on all fours; a hot open maw and a warm chest.</summary>
    public static void Scavenger(Canvas c, float cx, float cy, int frame)
    {
        float swing = new[] { 2.6f, 0.8f, -2.6f, -0.8f }[frame];
        foreach (int s in new[] { -1, 1 })
        {
            float phase = swing * s;
            Leg(c, (cx + 4.5f, cy + 4.4f * s), (cx + 7.5f + phase * 0.5f, cy + 8.5f * s), (cx + 9.5f + phase, cy + 9.5f * s), 2.3f, 1.4f);
            Leg(c, (cx - 7f, cy + 4.6f * s), (cx - 9.5f - phase * 0.4f, cy + 8.8f * s), (cx - 12.5f - phase, cy + 9.2f * s), 2.5f, 1.5f);
        }

        c.Fill(Shape.Ellipse(cx - 1.5f, cy, 10.8f, 6.4f), G(0.2f));
        c.Fill(Shape.Ellipse(cx - 1.6f, cy - 0.3f, 9.9f, 5.6f), Mottled(0.4f, 0.62f, 311));
        c.Fill(Shape.Capsule(cx - 10f, cy, cx + 5f, cy, 0.7f), G(0.28f, 0.8f));           // spine
        foreach (float x in new[] { -5f, -2f, 1f })
            foreach (int s in new[] { -1, 1 })
                c.Fill(Shape.Capsule(cx + x, cy + 1.2f * s, cx + x + 1.2f, cy + 4.6f * s, 0.45f), G(0.3f, 0.7f)); // ribs
        Hot(c, Shape.Ellipse(cx + 3f, cy, 3f, 2.4f), 0.25f);
        c.Fill(Shape.Ellipse(cx + 10f, cy, 4.8f, 3.8f), G(0.22f));
        c.Fill(Shape.Ellipse(cx + 9.9f, cy - 0.2f, 4.1f, 3.2f), Mottled(0.5f, 0.68f, 312));
        Hot(c, Shape.Ellipse(cx + 13.4f, cy, 1.7f, 2.3f), 0.5f);
    }

    /// <summary>Bondisseur: compact and hot, huge folded hind legs, long clawed forelimbs, spiked back.</summary>
    public static void Leaper(Canvas c, float cx, float cy, int frame)
    {
        float reach = new[] { 2f, 0.4f, -1.4f, 0.4f }[frame];
        foreach (int s in new[] { -1, 1 })
        {
            float r = reach * s;
            Leg(c, (cx - 4f, cy + 4f * s), (cx + 0.5f, cy + 10.5f * s), (cx - 8.5f, cy + 10f * s), 3.2f, 2f);
            Leg(c, (cx + 3.5f, cy + 3.8f * s), (cx + 9f + r, cy + 7.6f * s), (cx + 14f + r, cy + 3.6f * s), 1.8f, 1.1f);
            c.Fill(Shape.Capsule(cx + 14f + r, cy + 3.6f * s, cx + 16.4f + r, cy + 2.2f * s, 0.7f, 0.1f), G(0.85f));   // claw
        }

        c.Fill(Shape.Ellipse(cx - 1.5f, cy, 7.4f, 5.9f), G(0.22f));
        c.Fill(Shape.Ellipse(cx - 1.6f, cy - 0.3f, 6.6f, 5.1f), Mottled(0.52f, 0.74f, 321));
        for (int i = 0; i < 5; i++)
        {
            float x = cx - 7f + i * 2.4f, side = i % 2 == 0 ? -1f : 1f;
            c.Fill(Shape.Polygon((x - 1f, cy), (x + 1f, cy), (x - 0.6f, cy + 3.2f * side)), G(0.3f));      // spikes
        }
        Hot(c, Shape.Ellipse(cx + 0.5f, cy, 2.6f, 2.2f), 0.35f);
        c.Fill(Shape.Circle(cx + 7.4f, cy, 3.4f), G(0.24f));
        c.Fill(Shape.Circle(cx + 7.3f, cy - 0.2f, 2.8f), Mottled(0.55f, 0.72f, 322));
        foreach (int s in new[] { -1, 1 }) Hot(c, Shape.Circle(cx + 9.3f, cy + 1.3f * s, 0.75f), 0.4f);
    }

    /// <summary>Veilleur: long and thin, many spindly limbs, a cold body with a few faint hot eyes.</summary>
    public static void Watcher(Canvas c, float cx, float cy, int frame)
    {
        float twitch = new[] { 0f, 0.6f, 0f, -0.6f }[frame];
        (float X, float Y, float Joint, float Tip)[] limbs = [(4f, 3f, 0.6f, 1.3f), (0f, 4.4f, 1.6f, 2.3f), (-5f, 3.8f, 2.4f, 2.9f)];
        foreach (int s in new[] { -1, 1 })
        {
            foreach (var (x, y, joint, tip) in limbs)
            {
                float j = joint + twitch * 0.08f * s, t = tip + twitch * 0.12f * s;
                var root = (cx + x, cy + y * s);
                var knee = (cx + x + MathF.Cos(j) * 12f, cy + (y + MathF.Sin(j) * 12f) * s);
                var end = (knee.Item1 + MathF.Cos(t) * 11f, knee.Item2 + MathF.Sin(t) * 11f * s);
                Leg(c, root, knee, end, 1.5f, 0.8f, G(0.3f), G(0.4f));
            }
        }

        c.Fill(Shape.Ellipse(cx - 2f, cy, 8f, 5.4f), G(0.18f));
        c.Fill(Shape.Ellipse(cx - 2.1f, cy - 0.2f, 7.2f, 4.7f), Mottled(0.3f, 0.46f, 331));
        c.Fill(Shape.Ellipse(cx + 8.4f, cy, 4.6f, 2.6f), G(0.2f));
        c.Fill(Shape.Ellipse(cx + 8.3f, cy - 0.1f, 4f, 2f), Mottled(0.34f, 0.48f, 332));
        foreach (var (x, y) in new[] { (10.6f, -1f), (11.2f, 0.9f), (9.2f, 0.1f) }) Hot(c, Shape.Circle(cx + x, cy + y, 0.6f), 0.25f);
    }

    static void Leg(Canvas c, (float X, float Y) a, (float X, float Y) b, (float X, float Y) tip, float r1, float r2, Rgba? dark = null, Rgba? light = null)
    {
        Rgba d = dark ?? G(0.26f), l = light ?? G(0.42f);
        c.Fill(Shape.Capsule(a.X, a.Y, b.X, b.Y, r1, (r1 + r2) / 2f), d);
        c.Fill(Shape.Capsule(b.X, b.Y, tip.X, tip.Y, (r1 + r2) / 2f, r2), d);
        c.Fill(Shape.Capsule(a.X, a.Y - 0.3f, b.X, b.Y - 0.3f, MathF.Max(0.3f, r1 - 0.8f), MathF.Max(0.3f, (r1 + r2) / 2f - 0.8f)), l);
    }

    // ------------------------------------------------------------------ containers and props

    /// <summary>Metal locker seen from above; open: the door swung aside, a dark empty inside.</summary>
    static Canvas Locker(bool open)
    {
        var c = new Canvas(32, 32);
        c.Fill(Shape.RoundRect(16f, 16f, 24f, 18f, 1.5f), G(0.16f));
        c.Fill(Shape.RoundRect(16f, 16f, 22f, 16f, 1f), G(0.34f));
        if (open)
        {
            c.Fill(Shape.Rect(7f, 10f, 18f, 12f), G(0.06f));
            c.Fill(Shape.RoundRect(13f, 26.5f, 20f, 3f, 0.8f, 0.35f), G(0.42f));
        }
        else
        {
            foreach (float y in new[] { 11f, 13.5f, 16f }) c.Fill(Shape.Rect(8f, y, 7f, 0.9f), G(0.2f));
            c.Fill(Shape.Capsule(21.5f, 13f, 21.5f, 19f, 0.8f), G(0.62f));
            c.Fill(Shape.Rect(15.6f, 8f, 0.8f, 16f), G(0.22f));
        }
        c.EdgeLight(G(0.75f), 0.55f);
        NoctArt.Grain(c, open ? 341 : 342, 0.025f);
        return c;
    }

    static Canvas SupplyCrate(bool open)
    {
        var crate = NoctArt.Cold(Props.Crate(shadow: false), 0.22f, 0.5f, G(0.72f), 0.55f, 343);
        if (!open) return crate;
        crate.Fill(Shape.Rect(6f, 6f, 20f, 20f), G(0.05f));
        crate.Fill(Shape.RoundRect(25f, 27f, 18f, 4f, 0.8f, -0.3f), G(0.4f));
        return crate;
    }

    static Canvas RadioConsole()
    {
        var c = new Canvas(64, 32);
        c.Fill(Shape.RoundRect(32f, 16f, 58f, 22f, 2f), G(0.15f));
        c.Fill(Shape.RoundRect(32f, 16f, 56f, 20f, 1.5f), G(0.3f));
        foreach (float x in new[] { 14f, 30f }) c.Fill(Shape.RoundRect(x, 14f, 12f, 9f, 1f), G(0.12f));
        for (int i = 0; i < 6; i++) c.Fill(Shape.Circle(42f + i * 2.6f, 12f + (i % 2) * 5f, 0.9f), G(0.55f));
        c.EdgeLight(G(0.7f), 0.5f);
        foreach (var (x, y) in new[] { (12f, 21f), (48f, 21f) }) Hot(c, Shape.Circle(x, y, 0.8f), 0.5f);   // warm diodes
        NoctArt.Grain(c, 344, 0.025f);
        return c;
    }

    /// <summary>Concrete road barrier, two tiles long.</summary>
    static Canvas Barrier()
    {
        var c = new Canvas(64, 24);
        c.Fill(Shape.RoundRect(32f, 12f, 60f, 16f, 3f), G(0.2f));
        c.Fill(Shape.RoundRect(32f, 11.5f, 56f, 11f, 2.5f), Mottled(0.34f, 0.44f, 345, 10f));
        c.Fill(Shape.Rect(4f, 11f, 56f, 1f), G(0.48f, 0.6f));
        NoctArt.Grain(c, 345, 0.03f);
        return c;
    }

    static Canvas Antenna()
    {
        var c = new Canvas(32, 32);
        for (int i = 0; i < 4; i++)
        {
            float a = i * MathF.PI / 2f + 0.4f;
            c.Fill(Shape.Capsule(16f, 16f, 16f + MathF.Cos(a) * 13f, 16f + MathF.Sin(a) * 13f, 0.5f), G(0.38f));
        }
        c.Fill(Shape.Circle(16f, 16f, 4f), G(0.24f));
        c.Fill(Shape.Circle(16f, 16f, 2.6f), G(0.42f));
        Hot(c, Shape.Circle(16f, 16f, 0.9f), 0.5f);
        return c;
    }

    /// <summary>Faded landing mark painted on the ground (decor under the extraction zone).</summary>
    static Canvas LandingMark()
    {
        var c = new Canvas(96, 96);
        c.Fill(Shape.Circle(48f, 48f, 44f).Minus(Shape.Circle(48f, 48f, 40f)), G(0.36f, 0.7f));
        c.Fill(Shape.Rect(30f, 28f, 7f, 40f), G(0.36f, 0.7f));
        c.Fill(Shape.Rect(59f, 28f, 7f, 40f), G(0.36f, 0.7f));
        c.Fill(Shape.Rect(30f, 45f, 36f, 6f), G(0.36f, 0.7f));
        c.Map((x, y, p) => p.A <= 0f ? p : p.Fade(0.5f + 0.5f * Noise.Tile(x / 16f, y / 16f, 6, 6, 2, 346)));
        return c;
    }

    // ------------------------------------------------------------------ pickups

    /// <summary>Small object with a bright rim so it reads against any floor.</summary>
    static Canvas Pickup(Action<Canvas> draw)
    {
        var c = new Canvas(20, 20);
        draw(c);
        c.EdgeLight(G(1f), 0.8f);
        c.Glow(G(0.85f), 1.2f, 0.35f);
        return c;
    }

    static void Medkit(Canvas c)
    {
        c.Fill(Shape.RoundRect(10f, 10f, 15f, 12f, 1.5f), G(0.32f));
        c.Fill(Shape.RoundRect(10f, 10f, 13.5f, 10.5f, 1.2f), G(0.62f));
        c.Fill(Shape.Rect(8.6f, 6.2f, 2.8f, 7.6f), G(0.95f));
        c.Fill(Shape.Rect(6.2f, 8.6f, 7.6f, 2.8f), G(0.95f));
    }

    static void AmmoBox(Canvas c)
    {
        c.Fill(Shape.RoundRect(10f, 10f, 15f, 11f, 1f), G(0.3f));
        c.Fill(Shape.RoundRect(10f, 10f, 13.5f, 9.5f, 0.8f), G(0.48f));
        for (int i = 0; i < 4; i++) c.Fill(Shape.Circle(5.6f + i * 2.9f, 10f, 1.1f), G(0.88f));
    }

    static void ShellBox(Canvas c)
    {
        c.Fill(Shape.RoundRect(10f, 10f, 15f, 12f, 1f), G(0.3f));
        for (int i = 0; i < 3; i++)
        {
            c.Fill(Shape.Capsule(5.5f + i * 4.5f, 6.5f, 5.5f + i * 4.5f, 13f, 1.7f), G(0.55f));
            c.Fill(Shape.Circle(5.5f + i * 4.5f, 13f, 1.7f), G(0.9f));
        }
    }

    /// <summary>The radio battery: the mission item, warm, glowing.</summary>
    static Canvas Battery()
    {
        var c = new Canvas(24, 24);
        c.Fill(Shape.RoundRect(12f, 12.5f, 12f, 16f, 2f), G(0.3f));
        c.Fill(Shape.RoundRect(12f, 12.5f, 10f, 14f, 1.5f), G(0.7f));
        c.Fill(Shape.Rect(9.5f, 3f, 2f, 2.5f), G(0.85f));
        c.Fill(Shape.Rect(13f, 3f, 2f, 2.5f), G(0.85f));
        c.Fill(Shape.Rect(8f, 11f, 8f, 1.2f), G(0.4f));
        c.EdgeLight(G(1f), 0.9f);
        c.Glow(G(1f), 2f, 0.6f);
        return c;
    }

    // ------------------------------------------------------------------ outdoor tiles

    static Canvas Gravel()
    {
        var rng = new Random(351);
        var c = Canvas.Generate(32, 32, (x, y) => G(0.17f + 0.06f * Noise.Tile(x / 32f, y / 32f, 4, 4, 3, 351)), wrap: true);
        for (int i = 0; i < 26; i++)
        {
            float x = rng.NextSingle() * 32f, y = rng.NextSingle() * 32f, r = 0.5f + rng.NextSingle() * 1.1f;
            c.Fill(Shape.Circle(x, y, r), G(0.24f + rng.NextSingle() * 0.1f));
        }
        NoctArt.Grain(c, 352, 0.025f);
        return c;
    }

    /// <summary>Chain-link fence over the gravel: it blocks the way, not the view nor the bullets.</summary>
    static Canvas Fence()
    {
        var c = Gravel();
        for (int k = -4; k < 8; k++)
        {
            float o = k * 6f;
            c.Fill(Shape.Capsule(o, 0f, o + 32f, 32f, 0.45f), G(0.46f, 0.85f));
            c.Fill(Shape.Capsule(o + 32f, 0f, o, 32f, 0.45f), G(0.46f, 0.85f));
        }
        c.Fill(Shape.Rect(14.5f, 0f, 3f, 32f), G(0.34f));
        c.Fill(Shape.Rect(0f, 14.5f, 32f, 3f), G(0.34f));
        c.Fill(Shape.Circle(16f, 16f, 2.4f), G(0.5f));
        return c;
    }

    // ------------------------------------------------------------------ animated creatures (rig)

    static Palette CreatureHeat(float flesh, float fur, float bone, float claw, float heat)
    {
        // Body heat fades towards the floor's cold as the creature rests or dies; hot spots go first.
        float k = 0.35f + 0.65f * heat;
        var p = new Palette();
        NoctArt.Thermal(p, Mat.Flesh, flesh * k);
        NoctArt.Thermal(p, Mat.Fur, fur * k);
        NoctArt.Thermal(p, Mat.Bone, bone * k);
        NoctArt.Thermal(p, Mat.Claw, claw * k);
        NoctArt.Thermal(p, Mat.Glow, heat > 0.6f ? 1f : 0.35f + heat * 0.6f);
        NoctArt.Thermal(p, Mat.Mouth, heat > 0.6f ? 0.9f : 0.3f + heat * 0.5f);
        foreach (Mat m in new[] { Mat.Skin, Mat.Top, Mat.Trim, Mat.Shirt, Mat.Pants, Mat.Shoes, Mat.Belt, Mat.Hair, Mat.Dark, Mat.Eye })
            NoctArt.Thermal(p, m, flesh * k * (m is Mat.Shoes or Mat.Dark ? 0.85f : 1f));
        return p;
    }

    static readonly BloodColors CreatureBlood = new(G(0.3f), G(0.42f), G(0.52f));

    static void AnimatedCreatures(Pack pack)
    {
        RenderStyle style = NoctArt.ThermalStyle;
        RigBeasts.Generate(pack, "scavenger", CreatureClips.Scavenger(), CreatureClips.ScavengerBuild,
            h => CreatureHeat(0.68f, 0.5f, 0.38f, 0.3f, h), style, 48, CreatureBlood, (c, s) => NoctArt.Warm(c, s, 0.28f, 0.25f), 311);
        RigBeasts.Generate(pack, "leaper", CreatureClips.Leaper(), CreatureClips.LeaperBuild,
            h => CreatureHeat(0.76f, 0.55f, 0.42f, 0.34f, h), style, 48, CreatureBlood, (c, s) => NoctArt.Warm(c, s, 0.36f, 0.25f), 312);
        RigHumans.Generate(pack, pack.Rig, "watcher", CreatureClips.WatcherBuild(CreatureHeat(0.6f, 0.5f, 0.45f, 0.4f, 1f)), CreatureBlood, 313,
            style: style, upperSize: 60, fullSize: 64, heat: h => CreatureHeat(0.6f, 0.5f, 0.45f, 0.4f, h),
            finish: (c, s) => NoctArt.Warm(c, s, 0.12f, 0.25f), clips: CreatureClips.Watcher(), stills: false);
    }

    // ------------------------------------------------------------------ structure tiles (thermal, seamless 32 px)

    static Canvas Thermal32(Func<int, int, float> heat, int seed) => Canvas.Generate(32, 32, (x, y) =>
    {
        float g = heat(x, y) + (Noise.Hash(x, y, seed) - 0.5f) * 0.035f;
        return G(Math.Clamp(g, 0f, 1f));
    }, wrap: true);

    static float N(int x, int y, int period, int seed) => Noise.Tile((x + 0.5f) / 32f, (y + 0.5f) / 32f, period, period, 2, seed);

    /// <summary>Indoor concrete slabs: 16 px squares, darker joints, faint stains.</summary>
    static Canvas Slab() => Thermal32((x, y) =>
    {
        if (x % 16 == 0 || y % 16 == 0) return 0.2f;
        return 0.3f + N(x, y, 4, 601) * 0.06f - (N(x, y, 2, 602) > 0.7f ? 0.03f : 0f);
    }, 603);

    /// <summary>Cold asphalt with a painted line (paint reads a touch warmer).</summary>
    static Canvas Asphalt() => Thermal32((x, y) =>
    {
        float g = 0.22f + N(x, y, 8, 604) * 0.05f;
        if (y >= 15 && y <= 16 && x % 16 < 10) g = 0.38f;
        return Noise.Hash(x, y, 605) > 0.95f ? g + 0.05f : g;
    }, 606);

    /// <summary>Top of a thick concrete wall: lighter than the floors, a centre seam, rebar marks.</summary>
    static Canvas ConcreteWall() => Thermal32((x, y) =>
    {
        float g = 0.46f + N(x, y, 4, 607) * 0.05f;
        if (y == 15 || y == 16) g -= 0.06f;
        if (x % 8 == 3 && (y == 6 || y == 25)) g += 0.08f;
        return g;
    }, 608);

    /// <summary>Flat tar roof: dark membrane, lighter seams every 16 px, gravel specks.</summary>
    static Canvas RoofFlat() => Thermal32((x, y) =>
    {
        float g = 0.3f + N(x, y, 4, 609) * 0.05f;
        if (x % 16 == 0 || y % 16 == 0) g += 0.08f;
        return Noise.Hash(x, y, 610) > 0.93f ? g + 0.06f : g;
    }, 611);

    /// <summary>Corrugated metal roof: ribs every 4 px.</summary>
    static Canvas RoofMetal() => Thermal32((x, y) =>
    {
        int r = x % 4;
        float g = r == 0 ? 0.22f : r == 1 ? 0.38f : 0.31f;
        return g + N(x, y, 4, 612) * 0.03f;
    }, 613);
}
