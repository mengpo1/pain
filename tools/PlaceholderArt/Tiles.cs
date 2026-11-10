namespace PlaceholderArt;

/// <summary>
/// 32x32 seamless tiles (base colours) plus the 32x6 door planks. Every texture uses periodic noise or
/// modular patterns, and shapes are drawn on wrapping canvases, so opposite edges always match.
/// </summary>
public static class Tiles
{
    const int S = 32;

    static float U(int px) => (px + 0.5f) / S;

    static Canvas Tile(Func<int, int, Rgba> pixel) => Canvas.Generate(S, S, pixel, wrap: true);

    public static Canvas FloorWood()
    {
        Rgba[] tones = [Rgba.Hex("#a8642f"), Rgba.Hex("#b87238"), Rgba.Hex("#9c5a2a"), Rgba.Hex("#b06a33")];
        int[] joint = [5, 21, 13, 28];
        var seam = Rgba.Hex("#4a2812");
        var c = Tile((x, y) =>
        {
            int row = y / 8, ly = y % 8;
            if (ly == 0) return seam;
            if (x == joint[row]) return seam.Lerp(tones[row], 0.3f);
            float grain = Noise.Tile(U(x), U(y), 2, 24, 3, 11 + row);
            float fine = Noise.Tile(U(x), U(y), 8, 32, 1, 40 + row);
            float k = 0.84f + 0.24f * grain + 0.06f * fine;
            if (ly == 1) k += 0.1f;
            else if (ly == 7) k -= 0.1f;
            if (x == (joint[row] + 1) % S) k += 0.06f;
            return tones[row].Tone(k);
        });
        c.Fill(Shape.Ellipse(24f, 19.5f, 2.2f, 1.2f), Rgba.Hex("#6e3c1a").Fade(0.55f));
        c.Fill(Shape.Ellipse(24f, 19.5f, 1.0f, 0.55f), seam.Fade(0.7f));
        return c;
    }

    /// <summary>Glossy pink / teal checker with dark grout, 2x2 squares of 16 px.</summary>
    public static Canvas FloorTile()
    {
        Rgba pink = Rgba.Hex("#e2629c"), teal = Rgba.Hex("#2cb3aa"), grout = Rgba.Hex("#2a1e36");
        return Tile((x, y) =>
        {
            int lx = x % 16, ly = y % 16;
            if (lx == 0 || ly == 0) return grout;
            var c = (x / 16 + y / 16) % 2 == 0 ? pink : teal;
            float k = 1f;
            if (lx == 1 || ly == 1) k = 1.22f;
            else if (lx == 15 || ly == 15) k = 0.8f;
            float sheen = MathF.Max(0f, 1f - MathF.Abs(lx + ly - 12f) / 3f) * 0.12f;
            return c.Tone(k + sheen + (Noise.Hash(x, y, 5) - 0.5f) * 0.05f);
        });
    }

    public static Canvas FloorConcrete() => Tile((x, y) =>
    {
        float n = Noise.Tile(U(x), U(y), 4, 4, 4, 21);
        float v = 0.46f + (n - 0.5f) * 0.14f + (Noise.Hash(x, y, 22) - 0.5f) * 0.05f;
        if (Noise.Hash(x, y, 23) > 0.985f) v -= 0.08f;
        return new Rgba(v * 0.98f, v, v * 1.05f);
    });

    /// <summary>Dark purple carpet with a diamond lattice and small magenta centres.</summary>
    public static Canvas FloorCarpet()
    {
        Rgba baseC = Rgba.Hex("#2e1a42"), line = Rgba.Hex("#4d2b6e"), accent = Rgba.Hex("#a8407a");
        return Tile((x, y) =>
        {
            int lx = x % 16, ly = y % 16;
            float d = MathF.Abs(lx - 7.5f) + MathF.Abs(ly - 7.5f);
            var c = MathF.Abs(d - 7.5f) < 0.6f ? line : d <= 2f ? accent : baseC;
            float fiber = (Noise.Hash(x, y, 31) - 0.5f) * 0.12f + (Noise.Tile(U(x), U(y), 4, 4, 2, 32) - 0.5f) * 0.1f;
            return c.Tone(1f + fiber);
        });
    }

    /// <summary>Running-bond bricks (16x8) in dark maroon-purple with a lit top bevel on each brick.</summary>
    public static Canvas WallBrick()
    {
        Rgba mortar = Rgba.Hex("#25101f"), brick = Rgba.Hex("#5c2343");
        return Tile((x, y) =>
        {
            int row = y / 8, by = y % 8, shifted = x + (row % 2) * 8, bx = shifted % 16;
            if (by == 7 || bx == 15) return mortar;
            int id = (shifted / 16) % 2 + row * 2;
            float k = 0.92f + Noise.Hash(id, 0, 41) * 0.16f;
            if (by == 0) k += 0.32f;
            else if (by == 1) k += 0.13f;
            else if (by == 6) k -= 0.14f;
            if (bx == 0) k += 0.08f;
            else if (bx == 14) k -= 0.08f;
            k += (Noise.Tile(U(x), U(y), 8, 8, 2, 42) - 0.5f) * 0.14f + (Noise.Hash(x, y, 43) - 0.5f) * 0.05f;
            return brick.Tone(k);
        });
    }

    public static Canvas WallPlaster()
    {
        var baseC = Rgba.Hex("#3a4456");
        var c = Tile((x, y) =>
        {
            float n = Noise.Tile(U(x), U(y), 3, 3, 4, 51);
            return baseC.Tone(1f + (n - 0.5f) * 0.24f + (Noise.Hash(x, y, 52) - 0.5f) * 0.05f);
        });
        var crack = Rgba.Hex("#222934").Fade(0.5f);
        c.Fill(Shape.Capsule(20f, -2f, 23f, 6f, 0.45f, 0.35f), crack);
        c.Fill(Shape.Capsule(23f, 6f, 21.5f, 11f, 0.35f, 0.15f), crack);
        c.Fill(Shape.Capsule(23f, 6f, 27f, 8.5f, 0.3f, 0.1f), crack);
        return c;
    }

    /// <summary>Semi-transparent cyan pane with soft diagonal highlights (diagonals of period 32 tile).</summary>
    public static Canvas Glass()
    {
        Rgba pane = new(0.62f, 0.9f, 1f, 0.45f), shine = new(0.9f, 0.98f, 1f, 0.72f);
        return Tile((x, y) =>
        {
            int u = (x + y) % S;
            float s = MathF.Max(0f, 1f - MathF.Abs(u - 7.5f) / 2.6f) + 0.7f * MathF.Max(0f, 1f - MathF.Abs(u - 14f) / 1.3f);
            return pane.Lerp(shine, Math.Clamp(s, 0f, 1f));
        });
    }

    /// <summary>Mostly-clear leftover of a broken pane: faint tint plus scattered shards and glints.</summary>
    public static Canvas GlassBroken()
    {
        var c = Tile((x, y) => new Rgba(0.6f, 0.88f, 1f, 0.13f + 0.08f * Noise.Tile(U(x), U(y), 4, 4, 2, 61)));
        var rng = new Random(62);
        for (int i = 0; i < 9; i++)
        {
            float cx = rng.NextSingle() * S, cy = rng.NextSingle() * S, r = 1.3f + rng.NextSingle() * 2.4f, a = rng.NextSingle() * 6.283f;
            var shard = Shape.Radial(cx, cy, (a, r), (a + 2.0f + rng.NextSingle() * 0.6f, r * (0.45f + rng.NextSingle() * 0.4f)),
                (a + 4.1f + rng.NextSingle() * 0.5f, r * (0.5f + rng.NextSingle() * 0.4f)));
            c.Fill(shard, new Rgba(0.72f, 0.94f, 1f, 0.55f + rng.NextSingle() * 0.2f));
            c.Fill(Shape.Capsule(cx, cy, cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, 0.3f), new Rgba(1f, 1f, 1f, 0.75f));
        }
        for (int i = 0; i < 5; i++) c.Blend(rng.Next(S), rng.Next(S), new Rgba(1f, 1f, 1f, 0.85f));
        return c;
    }

    /// <summary>32x6 wooden door plank; hinge at the left (x = 0), lit top and free edge.</summary>
    public static Canvas DoorWood()
    {
        Rgba wood = Rgba.Hex("#93592c"), dark = Rgba.Hex("#4c2a12"), light = Rgba.Hex("#cf9358"), metal = Rgba.Hex("#3c3c44");
        return Canvas.Generate(32, 6, (x, y) =>
        {
            if (x <= 2) return y == 0 || y == 5 ? dark : x == 1 && y >= 1 && y <= 4 ? Rgba.Hex("#8a8a96") : metal;
            if (y == 0 || y == 5 || x == 31) return dark;
            if (y == 1 || x == 30) return light;
            float grain = Noise.Tile((x + 0.5f) / 32f, (y + 0.5f) / 6f, 3, 12, 2, 71);
            return wood.Tone(0.85f + 0.25f * grain - (y == 4 ? 0.1f : 0f));
        });
    }

    /// <summary>32x6 steel door plank with rivets; hinge at the left.</summary>
    public static Canvas DoorMetal()
    {
        Rgba steel = Rgba.Hex("#6f7886"), dark = Rgba.Hex("#2e333b"), light = Rgba.Hex("#b2bac6"), hinge = Rgba.Hex("#2a2d33");
        var c = Canvas.Generate(32, 6, (x, y) =>
        {
            if (x <= 2) return y == 0 || y == 5 ? dark : x == 1 && y >= 1 && y <= 4 ? Rgba.Hex("#7c838e") : hinge;
            if (y == 0 || y == 5 || x == 31) return dark;
            if (y == 1 || x == 30) return light;
            float brushed = Noise.Tile((x + 0.5f) / 32f, (y + 0.5f) / 6f, 2, 6, 3, 81);
            return steel.Tone(0.9f + 0.18f * brushed - (y == 4 ? 0.12f : 0f));
        });
        foreach (float x in new[] { 8f, 16.5f, 25f })
        {
            c.Fill(Shape.Circle(x + 0.15f, 3.25f, 0.85f), dark.Fade(0.8f));
            c.Fill(Shape.Circle(x, 3f, 0.7f), light);
        }
        return c;
    }
}
