namespace PlaceholderArt;

/// <summary>
/// NoctInspired mod sprites: a monochrome thermal-camera look. Warm living bodies are bright, softly
/// glowing silhouettes; the environment is cold dark greys with sensor grain; the creature is the horror
/// twist: it is cold, barely brighter than the floor, with a faint rim and a few bright points.
/// Geometry is shared with the base pack (Actors, Weapons, Tiles, Props); only colours and post-processing differ.
/// </summary>
public static class NoctArt
{
    static Rgba G(float v, float a = 1f) => Rgba.Grey(v, a);

    static readonly HumanLook Player = new()
    {
        Body = G(0.80f), BodyShade = G(0.66f), BodyLight = G(0.95f), Skin = G(0.98f), SkinShade = G(0.88f),
        Hair = G(0.84f), Pants = G(0.70f), Shoes = G(0.50f), Collar = G(0.73f),
    };

    static readonly HumanLook Guard = new()
    {
        Body = G(0.78f), BodyShade = G(0.64f), BodyLight = G(0.92f), Skin = G(0.98f), SkinShade = G(0.88f),
        Hair = G(0.82f), Pants = G(0.68f), Shoes = G(0.50f),
    };

    static readonly HumanLook Thug = new()
    {
        Body = G(0.81f), BodyShade = G(0.66f), BodyLight = G(0.94f), Skin = G(0.99f), SkinShade = G(0.90f),
        Hair = null, Pants = G(0.70f), Shoes = G(0.50f),
    };

    static readonly HumanLook Brute = new()
    {
        Body = G(0.86f), BodyShade = G(0.72f), BodyLight = G(0.97f), Skin = G(0.97f), SkinShade = G(0.85f),
        Hair = G(0.9f, 0.3f), Pants = G(0.70f), Shoes = G(0.50f), BareArms = true, Scale = 1.35f, Bulk = 1.08f,
    };

    static readonly CreatureLook Creature = new()
    {
        Body = G(0.23f), BodyDark = G(0.14f), Rim = G(0.4f), Limb = G(0.17f), LimbLight = G(0.32f),
        Claw = G(0.48f), Eye = G(1f), Maw = G(0.04f), Reach = 1.12f, Thickness = 0.85f,
    };

    public static void Generate(Pack pack)
    {
        Humans(pack, "player", Player, 32, 48, 201);
        Humans(pack, "guard", Guard, 32, 48, 202);
        Humans(pack, "thug", Thug, 32, 48, 203);
        Humans(pack, "brute", Brute, 40, 56, 204);
        Creatures(pack);

        var legs = new Canvas(128, 32);
        for (int f = 0; f < 4; f++) Actors.LegsFrame(legs, f, G(0.62f), G(0.44f));
        pack.Png("sprites/actors/legs.png", Warm(legs, 205, 0.3f));

        int seed = 210;
        foreach (var (name, sprite) in Weapons.All())
            pack.Png($"sprites/weapons/{name}.png", Cold(sprite, 0.2f, 0.55f, G(0.8f), 0.65f, seed++));

        Fx(pack);
        Tiles(pack);

        pack.Png("sprites/props/crate.png", Cold(Props.Crate(shadow: false), 0.2f, 0.46f, G(0.62f), 0.55f, 231));
        pack.Png("sprites/props/table.png", Cold(Props.Table(shadow: false), 0.2f, 0.44f, G(0.6f), 0.55f, 232));
        pack.Png("sprites/props/plant.png", DeadPlant());
        pack.Png("sprites/props/car.png", Cold(Props.Car(shadow: false), 0.1f, 0.3f, G(0.42f), 0.6f, 233));
        pack.Png("sprites/props/barrel.png", Barrel());
        pack.Png("sprites/props/generator.png", Generator());

        pack.Png("sprites/ui/crosshair.png", Crosshair());
        pack.Png("sprites/ui/arrow.png", Arrow());
    }

    // ------------------------------------------------------------------ thermal post-processing

    /// <summary>Additive per-pixel sensor noise on the visible pixels.</summary>
    static void Grain(Canvas c, int seed, float amount) =>
        c.Map((x, y, p) =>
        {
            if (p.A <= 0f) return p;
            float d = (Noise.Hash(x, y, seed) - 0.5f) * 2f * amount;
            return new Rgba(Math.Clamp(p.R + d, 0f, 1f), Math.Clamp(p.G + d, 0f, 1f), Math.Clamp(p.B + d, 0f, 1f), p.A);
        });

    /// <summary>Warm body: softened edges, a white heat halo and grain.</summary>
    static Canvas Warm(Canvas c, int seed, float halo = 0.5f)
    {
        c.Blur(0.4f);
        c.Glow(Rgba.White, 2f, halo);
        Grain(c, seed, 0.035f);
        return c;
    }

    /// <summary>Cold object: luminance remapped into [lo, hi], light rim, grain.</summary>
    static Canvas Cold(Canvas src, float lo, float hi, Rgba edge, float edgeStrength, int seed)
    {
        var c = src.Clone();
        c.Map((_, _, p) => G(lo + (hi - lo) * p.Luma, p.A));
        c.EdgeLight(edge, edgeStrength);
        Grain(c, seed, 0.025f);
        return c;
    }

    /// <summary>
    /// Re-grades a base tile into thermal greys: luminance is centred on <paramref name="target"/> with its
    /// contrast scaled, then softened (wrapping, so it stays seamless) and grained.
    /// </summary>
    static Canvas ThermalTile(Canvas tile, float target, float contrast, int seed)
    {
        float sum = 0f, weight = 0f;
        for (int y = 0; y < tile.Height; y++)
            for (int x = 0; x < tile.Width; x++)
            {
                var p = tile[x, y];
                sum += p.Luma * p.A;
                weight += p.A;
            }
        float mean = weight > 0f ? sum / weight : 0f;
        tile.Map((_, _, p) => G(Math.Clamp(target + (p.Luma - mean) * contrast, 0.06f, 0.6f), p.A));
        if (tile.Wrap) tile.Blur(0.45f);
        Grain(tile, seed, 0.03f);
        return tile;
    }

    /// <summary>Residual body heat on the floor: light grey, fading out towards the edges.</summary>
    static void HeatPool(Canvas c, float cx, float cy, float r, int seed)
    {
        var rng = new Random(seed);
        var layer = new Canvas(c.Width, c.Height);
        var fade = Paint.Radial(cx, cy, r * 1.25f, G(0.8f, 0.9f), G(0.6f, 0f), 1.5f);
        layer.Fill(Effects.WobblyBlob(cx, cy, r, r * 0.82f, 0.14f, rng), fade);
        for (int i = 0; i < 4; i++)
        {
            float a = rng.NextSingle() * 6.283f, d = r * (0.75f + rng.NextSingle() * 0.25f), lr = r * (0.22f + rng.NextSingle() * 0.14f);
            layer.Fill(Effects.WobblyBlob(cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, lr, lr, 0.12f, rng), fade);
        }
        layer.Blur(0.9f);
        Grain(layer, seed, 0.03f);
        c.Draw(layer);
    }

    // ------------------------------------------------------------------ actors

    static void Humans(Pack pack, string name, HumanLook look, int size, int downSize, int seed)
    {
        var stand = new Canvas(size, size);
        Actors.HumanStanding(stand, look, size / 2f, size / 2f);
        pack.Png($"sprites/actors/{name}.png", Warm(stand, seed));

        var down = new Canvas(downSize, downSize);
        Actors.HumanDown(down, look, downSize / 2f, downSize / 2f);
        var cooling = down.Clone();
        pack.Png($"sprites/actors/{name}_down.png", Warm(down, seed + 1));

        // dead: a warm pool on the floor and a body that has started to cool (dimmer, weaker halo)
        float k = downSize / 48f;
        var dead = new Canvas(downSize, downSize);
        HeatPool(dead, downSize / 2f + 6f * k, downSize / 2f + 1.5f * k, 15f * k, seed + 2);
        cooling.Map((_, _, p) => p.Shade(0.84f));
        dead.Draw(Warm(cooling, seed + 3, 0.25f));
        pack.Png($"sprites/actors/{name}_dead.png", dead);
    }

    static void Creatures(Pack pack)
    {
        var body = new Canvas(48, 48);
        var eyes = new Canvas(48, 48);
        Actors.Creature(body, eyes, Creature, 21f, 24f, 106);
        body.Outline(G(0.45f, 0.4f));
        Grain(body, 206, 0.03f);
        eyes.Glow(Rgba.White, 1.1f, 1.8f);
        body.Draw(eyes);
        pack.Png("sprites/actors/creature.png", body);

        var corpse = new Canvas(64, 64);
        Actors.CreatureDead(corpse, Creature with { Eye = G(0.5f) }, 32f, 32f, 107);
        corpse.Outline(G(0.42f, 0.35f));
        var dead = new Canvas(64, 64);
        var rng = new Random(207);
        dead.Fill(Effects.WobblyBlob(33f, 33f, 17f, 14f, 0.15f, rng), G(0.07f, 0.85f)); // cold, dark ichor
        dead.Outline(G(0.3f, 0.35f));
        dead.Draw(corpse);
        Grain(dead, 208, 0.03f);
        pack.Png("sprites/actors/creature_dead.png", dead);
    }

    // ------------------------------------------------------------------ fx, tiles

    static void Fx(Pack pack)
    {
        var blood = new BloodColors(G(0.62f), G(0.76f), G(0.95f));
        pack.Png("sprites/fx/bullet.png", Effects.Bullet(G(0.7f), Rgba.White, Rgba.White));
        pack.Png("sprites/fx/muzzle_flash.png", Effects.MuzzleFlash(G(0.82f), G(0.95f), Rgba.White));
        for (int i = 1; i <= 3; i++)
        {
            var splat = Effects.Splat(i, blood, 110 + i);
            splat.Blur(0.45f);
            Grain(splat, 220 + i, 0.03f);
            pack.Png($"sprites/fx/blood_{i}.png", splat);
        }
        pack.Png("sprites/fx/blood_drop.png", Effects.Drop(blood));
        pack.Png("sprites/fx/spark.png", Effects.Spark(Rgba.White, G(0.85f)));
        pack.Png("sprites/fx/shard.png", Effects.Shard(G(0.72f, 0.85f), G(1f, 0.95f)));
        pack.Png("sprites/fx/smoke.png", Effects.Smoke(G(0.55f)));
    }

    static void Tiles(Pack pack)
    {
        pack.Png("sprites/tiles/floor_wood.png", ThermalTile(PlaceholderArt.Tiles.FloorWood(), 0.22f, 0.35f, 241));
        pack.Png("sprites/tiles/floor_tile.png", ThermalTile(PlaceholderArt.Tiles.FloorTile(), 0.21f, 0.3f, 242));
        pack.Png("sprites/tiles/floor_concrete.png", ThermalTile(PlaceholderArt.Tiles.FloorConcrete(), 0.19f, 0.55f, 243));
        pack.Png("sprites/tiles/floor_carpet.png", ThermalTile(PlaceholderArt.Tiles.FloorCarpet(), 0.23f, 0.4f, 244));
        pack.Png("sprites/tiles/wall_brick.png", ThermalTile(PlaceholderArt.Tiles.WallBrick(), 0.3f, 0.45f, 245));
        pack.Png("sprites/tiles/wall_plaster.png", ThermalTile(PlaceholderArt.Tiles.WallPlaster(), 0.29f, 0.6f, 246));
        pack.Png("sprites/tiles/glass.png", ThermalTile(PlaceholderArt.Tiles.Glass(), 0.5f, 0.8f, 247));
        pack.Png("sprites/tiles/glass_broken.png", ThermalTile(PlaceholderArt.Tiles.GlassBroken(), 0.5f, 0.8f, 248));
        pack.Png("sprites/tiles/door_wood.png", ThermalTile(PlaceholderArt.Tiles.DoorWood(), 0.34f, 0.55f, 249));
        pack.Png("sprites/tiles/door_metal.png", ThermalTile(PlaceholderArt.Tiles.DoorMetal(), 0.28f, 0.6f, 250));
        pack.Png("sprites/tiles/floor_grate.png", FloorGrate());
        pack.Png("sprites/tiles/floor_dirt.png", FloorDirt());
        pack.Png("sprites/tiles/wall_rock.png", WallRock());
    }

    static float U(int px) => (px + 0.5f) / 32f;

    /// <summary>Bar grating: 2 px bearing bars every 4 px, cross rods every 8 px, a dark void below.</summary>
    static Canvas FloorGrate()
    {
        var c = Canvas.Generate(32, 32, (x, y) =>
        {
            int lx = x % 4, ly = y % 8;
            float v;
            if (ly == 0) v = 0.34f;
            else if (ly == 1 && lx >= 2) v = 0.12f;           // rod shadow in the holes
            else if (lx == 0) v = 0.32f;
            else if (lx == 1) v = 0.25f;
            else
            {
                v = 0.09f + 0.04f * Noise.Tile(U(x), U(y), 4, 4, 2, 261);
                if (y % 16 >= 9 && y % 16 <= 11) v += 0.05f;  // support beam far below
            }
            return G(v);
        }, wrap: true);
        Grain(c, 262, 0.025f);
        return c;
    }

    static Canvas FloorDirt()
    {
        var c = Canvas.Generate(32, 32, (x, y) =>
        {
            float n = Noise.Tile(U(x), U(y), 3, 3, 4, 271), m = Noise.Tile(U(x), U(y), 8, 8, 2, 272);
            return G(0.2f + (n - 0.5f) * 0.12f + (m - 0.5f) * 0.05f);
        }, wrap: true);
        var rng = new Random(273);
        for (int i = 0; i < 9; i++)
        {
            float x = rng.NextSingle() * 32f, y = rng.NextSingle() * 32f, r = 0.6f + rng.NextSingle() * 1.1f;
            c.Fill(Shape.Ellipse(x + 0.3f, y + 0.4f, r * 1.2f, r, rng.NextSingle() * 3f), G(0.12f, 0.6f));
            c.Fill(Shape.Ellipse(x, y, r * 1.2f, r, rng.NextSingle() * 3f), G(0.27f + rng.NextSingle() * 0.05f));
        }
        c.Blur(0.35f);
        Grain(c, 274, 0.035f);
        return c;
    }

    /// <summary>Rough rock: periodic cellular (Worley) noise gives lumpy stones split by dark cracks.</summary>
    static Canvas WallRock()
    {
        const int cells = 4;
        const float size = 32f / cells;
        var pts = new (float X, float Y)[cells, cells];
        for (int j = 0; j < cells; j++)
            for (int i = 0; i < cells; i++)
                pts[i, j] = ((i + 0.15f + 0.7f * Noise.Hash(i, j, 281)) * size, (j + 0.15f + 0.7f * Noise.Hash(i, j, 282)) * size);

        var c = Canvas.Generate(32, 32, (x, y) =>
        {
            float px = x + 0.5f, py = y + 0.5f, f1 = float.MaxValue, f2 = float.MaxValue, nx = 0f, ny = 0f;
            int ci = (int)(px / size), cj = (int)(py / size);
            for (int dj = -2; dj <= 2; dj++)
                for (int di = -2; di <= 2; di++)
                {
                    int i = ci + di, j = cj + dj;
                    var p = pts[((i % cells) + cells) % cells, ((j % cells) + cells) % cells];
                    float fx = p.X + MathF.Floor(i / (float)cells) * 32f, fy = p.Y + MathF.Floor(j / (float)cells) * 32f;
                    float d = MathF.Sqrt((px - fx) * (px - fx) + (py - fy) * (py - fy));
                    if (d < f1) { f2 = f1; f1 = d; nx = px - fx; ny = py - fy; }
                    else if (d < f2) f2 = d;
                }
            float detail = Noise.Tile(U(x), U(y), 8, 8, 2, 283);
            if (f2 - f1 < 1.1f) return G(0.13f + detail * 0.04f);                  // crack
            float lit = -(nx + ny) / (f1 + 0.001f) * 0.035f;                         // lit from the top-left
            float dome = 0.05f * (1f - Math.Clamp(f1 / size, 0f, 1f));
            return G(0.27f + dome + lit + (detail - 0.5f) * 0.08f);
        }, wrap: true);
        Grain(c, 284, 0.03f);
        return c;
    }

    // ------------------------------------------------------------------ props, ui

    /// <summary>Potted plant that has long since died: crooked, forking dry stems and a few shrivelled leaves.</summary>
    static Canvas DeadPlant()
    {
        var c = new Canvas(32, 32);
        c.Fill(Shape.Circle(16f, 16f, 8.2f), G(0.2f));
        c.Fill(Shape.Circle(16f, 16f, 7.6f), G(0.33f));
        c.Fill(Shape.Circle(16f, 16f, 6.4f), G(0.15f));
        var rng = new Random(291);
        foreach (float a in new[] { -2.5f, -1.2f, -0.4f, 0.9f, 2.2f })
        {
            float sx = 16f + (rng.NextSingle() - 0.5f) * 6f, sy = 16f + (rng.NextSingle() - 0.5f) * 6f;
            Stem(c, rng, sx, sy, a + (rng.NextSingle() - 0.5f) * 0.8f, 2.5f + rng.NextSingle() * 2.5f, 0.6f, 3);
        }
        foreach (var (x, y, a) in new[] { (5f, 24f, 0.5f), (27f, 7f, 2.1f), (26.5f, 26f, 1.2f), (13f, 12.5f, 2.6f) })
            c.Fill(Shape.Ellipse(x, y, 1.5f, 0.75f, a), G(0.27f));                       // fallen leaves
        c.EdgeLight(G(0.55f), 0.4f);
        Grain(c, 292, 0.03f);
        return c;
    }

    /// <summary>Recursive crooked stem: each segment bends a little and may fork; tips sometimes keep a dry leaf.</summary>
    static void Stem(Canvas c, Random rng, float x, float y, float angle, float len, float width, int depth)
    {
        float ex = x + MathF.Cos(angle) * len, ey = y + MathF.Sin(angle) * len;
        c.Fill(Shape.Capsule(x, y, ex, ey, width, width * 0.75f), G(0.38f + rng.NextSingle() * 0.06f));
        if (depth == 0)
        {
            if (rng.NextSingle() < 0.5f) c.Fill(Shape.Ellipse(ex, ey, 1.3f, 0.6f, angle + 1.3f), G(0.3f));
            return;
        }
        float bend = (rng.NextSingle() - 0.5f) * 1.3f;
        Stem(c, rng, ex, ey, angle + bend, len * 0.8f, width * 0.75f, depth - 1);
        if (rng.NextSingle() < 0.55f)
            Stem(c, rng, ex, ey, angle + bend + (rng.Next(2) == 0 ? -0.8f : 0.8f), len * 0.55f, width * 0.6f, Math.Max(0, depth - 2));
    }

    static Canvas Barrel()
    {
        var c = new Canvas(32, 32);
        c.Fill(Shape.Circle(16f, 16f, 12.5f), G(0.17f));
        c.Fill(Shape.Circle(16f, 16f, 11.8f), G(0.4f));                                  // rolled rim
        c.Fill(Shape.Circle(16f, 16f, 10.6f), G(0.25f));                                 // lid
        c.Fill(Shape.Circle(16f, 16f, 7.6f).Minus(Shape.Circle(16f, 16f, 6.9f)), G(0.32f));
        c.Fill(Shape.Circle(16f, 16f, 4.2f).Minus(Shape.Circle(16f, 16f, 3.6f)), G(0.3f));
        c.Fill(Shape.Circle(21.6f, 11.4f, 1.8f), G(0.37f));                              // bungs
        c.Fill(Shape.Circle(21.6f, 11.4f, 1.0f), G(0.18f));
        c.Fill(Shape.Circle(10.6f, 20.8f, 1.2f), G(0.35f));
        c.Fill(Shape.Ellipse(12f, 9.5f, 3.5f, 1.2f, -0.6f), G(0.34f, 0.6f));             // dent glint
        c.EdgeLight(G(0.55f), 0.5f);
        Grain(c, 293, 0.025f);
        return c;
    }

    /// <summary>Generator housing with cooling fins around a hot, glowing core.</summary>
    static Canvas Generator()
    {
        var c = new Canvas(32, 32);
        c.Fill(Shape.RoundRect(16f, 16f, 28f, 24f, 2f), G(0.2f));
        c.Fill(Shape.RoundRect(16f, 15.8f, 26.4f, 22.4f, 1.6f), G(0.3f));
        for (float x = 4.2f; x < 9.5f; x += 1.8f)
        {
            c.Fill(Shape.Rect(x, 7.5f, 0.9f, 17f), G(0.42f));
            c.Fill(Shape.Rect(31f - x - 0.9f, 7.5f, 0.9f, 17f), G(0.42f));
        }
        c.Fill(Shape.Circle(25.4f, 7f, 2.1f), G(0.58f));                                 // warm exhaust stub
        c.Fill(Shape.Circle(25.4f, 7f, 1.1f), G(0.35f));
        c.EdgeLight(G(0.55f), 0.45f);
        Grain(c, 294, 0.025f);

        var core = new Canvas(32, 32);
        core.Fill(Shape.Circle(16f, 16f, 5.4f), Paint.Radial(16f, 16f, 5.4f, G(1f), G(0.68f)));
        core.Fill(Shape.Circle(16f, 16f, 2.2f), Rgba.White);
        core.Glow(Rgba.White, 2.2f, 1.1f);
        c.Draw(core);
        return c;
    }

    /// <summary>Thin white corner brackets and a centre dot, like a camera focus reticle.</summary>
    static Canvas Crosshair()
    {
        var c = new Canvas(16, 16);
        foreach (var (x, y, sx, sy) in new[] { (2f, 2f, 1, 1), (14f, 2f, -1, 1), (2f, 14f, 1, -1), (14f, 14f, -1, -1) })
        {
            float x0 = sx > 0 ? x : x - 1f, y0 = sy > 0 ? y : y - 1f;
            c.Fill(Shape.Rect(sx > 0 ? x0 : x0 - 3f, y0, 4f, 1f), Rgba.White);
            c.Fill(Shape.Rect(x0, sy > 0 ? y0 : y0 - 3f, 1f, 4f), Rgba.White);
        }
        c.Fill(Shape.Rect(7f, 7f, 2f, 2f), Rgba.White);
        c.Outline(G(0.05f, 0.6f));
        return c;
    }

    static Canvas Arrow()
    {
        var c = new Canvas(16, 16);
        c.Fill(Shape.Rect(2f, 7.2f, 8f, 1.6f).Or(Shape.Polygon((7.5f, 3f), (14.5f, 8f), (7.5f, 13f), (9.6f, 8f))), Rgba.White);
        c.Outline(G(0.05f, 0.6f));
        return c;
    }
}
