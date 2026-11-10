namespace PlaceholderArt;

public sealed record BloodColors(Rgba Dark, Rgba Mid, Rgba Light);

/// <summary>Small effect sprites and blood. Colours are parameters so both packs share the shapes.</summary>
public static class Effects
{
    /// <summary>Organic outline: an ellipse whose radius is perturbed by a few random sines.</summary>
    public static Shape WobblyBlob(float cx, float cy, float rx, float ry, float wobble, Random rng)
    {
        float[] freq = [2f, 3f, 5f, 7f];
        float[] amp = [1f, 0.6f, 0.4f, 0.25f];
        var phase = freq.Select(_ => rng.NextSingle() * 6.283f).ToArray();
        float max = MathF.Max(rx, ry) * (1f + wobble * 2.3f);
        return Shape.Blob(cx, cy, max, a =>
        {
            float w = 1f;
            for (int i = 0; i < freq.Length; i++) w += wobble * amp[i] * MathF.Sin(freq[i] * a + phase[i]);
            return Shape.EllipseRadius(rx, ry, a) * w;
        });
    }

    /// <summary>Pool of blood under a body: main blob, lobes, a wet sheen and a few drops around it.</summary>
    public static void BloodPool(Canvas c, float cx, float cy, float radius, BloodColors col, int seed)
    {
        var rng = new Random(seed);
        c.Fill(WobblyBlob(cx, cy, radius, radius * 0.82f, 0.14f, rng), col.Dark);
        for (int i = 0; i < 4; i++)
        {
            float a = rng.NextSingle() * 6.283f, d = radius * (0.75f + rng.NextSingle() * 0.25f), r = radius * (0.22f + rng.NextSingle() * 0.14f);
            c.Fill(WobblyBlob(cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, r, r, 0.12f, rng), col.Dark);
        }
        c.Fill(WobblyBlob(cx - radius * 0.05f, cy - radius * 0.07f, radius * 0.78f, radius * 0.62f, 0.12f, rng), col.Mid);
        for (int i = 0; i < 3; i++)
        {
            float a = rng.NextSingle() * 6.283f, d = radius * rng.NextSingle() * 0.5f;
            c.Fill(Shape.Ellipse(cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, radius * 0.16f, radius * 0.07f, -0.6f), col.Light.Fade(0.45f));
        }
        for (int i = 0; i < 7; i++)
        {
            float a = rng.NextSingle() * 6.283f, d = radius * (1.1f + rng.NextSingle() * 0.35f);
            c.Fill(Shape.Circle(cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, 0.6f + rng.NextSingle() * 0.8f), col.Dark);
        }
    }

    /// <summary>32x32 blood splat. Variant 1: round splat; 2: directional spray towards +X; 3: cluster of blobs.</summary>
    public static Canvas Splat(int variant, BloodColors col, int seed)
    {
        var c = new Canvas(32, 32);
        var rng = new Random(seed);
        switch (variant)
        {
            case 1:
                for (int i = 0; i < 3; i++)
                {
                    float a = rng.NextSingle() * 6.283f, len = 9f + rng.NextSingle() * 4f;
                    c.Fill(Shape.Capsule(16, 16, 16 + MathF.Cos(a) * len, 16 + MathF.Sin(a) * len, 2.2f, 0.5f), col.Dark);
                }
                c.Fill(WobblyBlob(16, 16, 7.5f, 7f, 0.2f, rng), col.Dark);
                c.Fill(WobblyBlob(15.4f, 15.3f, 5.4f, 4.8f, 0.18f, rng), col.Mid);
                Drops(c, rng, 16, 16, 9.5f, 14f, 9, col.Dark);
                break;
            case 2:
                c.Fill(WobblyBlob(11f, 16f, 7.5f, 5f, 0.16f, rng), col.Dark);
                c.Fill(WobblyBlob(10.5f, 15.5f, 5f, 3.2f, 0.15f, rng), col.Mid);
                for (int i = 0; i < 5; i++)
                {
                    float y = 16f + (rng.NextSingle() - 0.5f) * 9f;
                    c.Fill(Shape.Capsule(14f, 16f + (y - 16f) * 0.3f, 22f + rng.NextSingle() * 7f, y, 1.6f, 0.4f), col.Dark);
                }
                for (int i = 0; i < 10; i++)
                {
                    float x = 18f + rng.NextSingle() * 12.5f, y = 16f + (rng.NextSingle() - 0.5f) * (x - 10f) * 0.9f;
                    c.Fill(Shape.Circle(x, y, 0.5f + rng.NextSingle() * 1.1f), col.Dark);
                }
                break;
            default:
                for (int i = 0; i < 4; i++)
                {
                    float a = i * 1.57f + rng.NextSingle(), d = 3f + rng.NextSingle() * 4.5f, r = 2.8f + rng.NextSingle() * 2.2f;
                    float x = 16 + MathF.Cos(a) * d, y = 16 + MathF.Sin(a) * d;
                    c.Fill(WobblyBlob(x, y, r, r * 0.85f, 0.18f, rng), col.Dark);
                    c.Fill(WobblyBlob(x - 0.4f, y - 0.4f, r * 0.62f, r * 0.5f, 0.15f, rng), col.Mid);
                }
                Drops(c, rng, 16, 16, 10f, 14.5f, 7, col.Dark);
                break;
        }
        // wet highlights
        for (int i = 0; i < 2; i++)
            c.Fill(Shape.Ellipse(13.5f + rng.NextSingle() * 4f, 13.5f + rng.NextSingle() * 4f, 1.4f, 0.6f, -0.6f), col.Light.Fade(0.5f));
        return c;
    }

    static void Drops(Canvas c, Random rng, float cx, float cy, float minR, float maxR, int count, Rgba color)
    {
        for (int i = 0; i < count; i++)
        {
            float a = rng.NextSingle() * 6.283f, d = minR + rng.NextSingle() * (maxR - minR);
            c.Fill(Shape.Circle(cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, 0.5f + rng.NextSingle() * 1.2f), color);
        }
    }

    public static Canvas Drop(BloodColors col)
    {
        var c = new Canvas(4, 4);
        c.Fill(Shape.Circle(2f, 2f, 1.65f), col.Dark);
        c.Fill(Shape.Circle(1.9f, 1.9f, 1.0f), col.Mid);
        c.Fill(Shape.Circle(1.4f, 1.4f, 0.5f), col.Light.Fade(0.7f));
        return c;
    }

    /// <summary>16x4 tracer, transparent tail at -X, bright head at +X.</summary>
    public static Canvas Bullet(Rgba tail, Rgba head, Rgba glow)
    {
        var c = new Canvas(16, 4);
        c.Fill(Shape.Capsule(0.5f, 2f, 14.6f, 2f, 0.35f, 1.35f), (x, _) =>
        {
            float t = Math.Clamp((x - 0.5f) / 14f, 0f, 1f);
            return tail.Lerp(head, t * t).WithAlpha(MathF.Pow(t, 1.4f));
        });
        c.Fill(Shape.Capsule(11f, 2f, 14.4f, 2f, 0.5f, 0.9f), head);
        c.Glow(glow, 0.7f, 0.8f);
        return c;
    }

    /// <summary>16x16 star burst, origin left of centre, long spike towards +X.</summary>
    public static Canvas MuzzleFlash(Rgba outer, Rgba inner, Rgba core)
    {
        var c = new Canvas(16, 16);
        (float, float)[] Star(float k) =>
        [
            (0f, 10f * k), (0.28f, 3f * k), (0.75f, 6.2f * k), (1.2f, 2.4f * k), (1.75f, 3.6f * k), (2.4f, 1.8f * k),
            (3.14f, 2.2f * k), (-2.4f, 1.8f * k), (-1.75f, 3.6f * k), (-1.2f, 2.4f * k), (-0.75f, 6.2f * k), (-0.28f, 3f * k),
        ];
        c.Fill(Shape.Radial(5f, 8f, Star(1f)), outer);
        c.Fill(Shape.Radial(5.2f, 8f, Star(0.62f)), inner);
        c.Fill(Shape.Circle(5.4f, 8f, 1.8f), core);
        c.Glow(outer, 1f, 0.7f);
        return c;
    }

    public static Canvas Spark(Rgba core, Rgba edge)
    {
        var c = new Canvas(4, 4);
        c.Fill(Shape.Circle(2f, 2f, 1.9f), Paint.Radial(2f, 2f, 1.9f, core, edge.Fade(0.3f)));
        return c;
    }

    public static Canvas Shard(Rgba fill, Rgba edge)
    {
        var c = new Canvas(6, 6);
        c.Fill(Shape.Polygon((0.6f, 5.3f), (2.7f, 0.5f), (5.5f, 3.6f)), fill);
        c.Fill(Shape.Capsule(0.9f, 5.0f, 2.7f, 0.8f, 0.4f), edge);
        return c;
    }

    /// <summary>16x16 soft puff made of blurred overlapping circles.</summary>
    public static Canvas Smoke(Rgba color)
    {
        var c = new Canvas(16, 16);
        foreach (var (x, y, r) in new[] { (8f, 8f, 4.4f), (6f, 6.6f, 3.3f), (10.2f, 7f, 3.1f), (7.4f, 10.2f, 3.1f), (10.2f, 10f, 2.7f) })
            c.Fill(Shape.Circle(x, y, r), color.Fade(0.4f));
        c.Blur(1.1f);
        return c;
    }
}
