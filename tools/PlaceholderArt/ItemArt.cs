namespace PlaceholderArt;

/// <summary>Items lying on the floor, projectiles, explosions and map cursors.</summary>
public static class ItemArt
{
    private static (float X, float Y) Center => IsoDraw.Project(8, 8, 0);

    /// <summary>A long gun lying on the floor: stock, body and barrel along the iso axis.</summary>
    public static Canvas Gun(Rgba body, Rgba accent, float length = 22f, float thickness = 2.6f)
    {
        Canvas c = TileArt.New();
        (float cx, float cy) = Center;
        float dx = length * 0.5f, dy = length * 0.25f;
        c.Fill(Shape.Capsule(cx - dx, cy + dy, cx + dx, cy - dy, thickness), body);
        c.Fill(Shape.Capsule(cx - dx * 0.2f, cy + dy * 0.2f, cx + dx * 0.25f, cy - dy * 0.25f, thickness * 1.25f), accent);
        c.Fill(Shape.Capsule(cx - dx * 0.1f, cy + dy * 0.1f + 2f, cx - dx * 0.05f, cy + 6f, 1.2f), body.Shade(0.8f));
        c.Outline(Rgba.Black.WithAlpha(0.6f), 0.8f);
        return c;
    }

    public static Canvas Clip(Rgba color)
    {
        Canvas c = TileArt.New();
        IsoDraw.Box(c, 6, 7, 0, 10, 9, 3, IsoDraw.Solid(color));
        return c;
    }

    public static Canvas Grenade(Rgba body, Rgba cap)
    {
        Canvas c = TileArt.New();
        (float cx, float cy) = Center;
        c.Fill(Shape.Ellipse(cx, cy + 1f, 5f, 2.5f), Rgba.Black.WithAlpha(0.3f));
        c.Fill(Shape.Circle(cx, cy - 3f, 4f), (x, y) => body.Tone(1.1f - (x - cx + y - cy) * 0.03f));
        c.Fill(Shape.Rect(cx - 1.5f, cy - 9f, 3f, 3f), cap);
        c.Outline(Rgba.Black.WithAlpha(0.6f), 0.8f);
        return c;
    }

    /// <summary>A blade or a bow lying on the floor.</summary>
    public static Canvas Blade(Rgba metal, Rgba grip)
    {
        Canvas c = TileArt.New();
        (float cx, float cy) = Center;
        c.Fill(Shape.Capsule(cx - 10f, cy + 5f, cx + 6f, cy - 3f, 1.6f), metal);
        c.Fill(Shape.Capsule(cx - 4f, cy + 6f, cx - 2f, cy - 1f, 1.4f), grip.Shade(0.9f));
        c.Fill(Shape.Capsule(cx + 6f, cy - 3f, cx + 11f, cy - 5.5f, 1.4f), grip);
        c.Outline(Rgba.Black.WithAlpha(0.6f), 0.8f);
        return c;
    }

    public static Canvas Bow(Rgba wood, Rgba cord)
    {
        Canvas c = TileArt.New();
        (float cx, float cy) = Center;
        c.Fill(Shape.Capsule(cx - 12f, cy + 4f, cx + 12f, cy - 4f, 0.6f), cord);
        // The limb: an arc bulging away from the cord, drawn as short capsules.
        const int segments = 10;
        for (int i = 0; i < segments; i++)
        {
            float t0 = i / (float)segments, t1 = (i + 1) / (float)segments;
            (float X, float Y) Point(float t) => (cx - 12f + 24f * t, cy + 4f - 8f * t - MathF.Sin(t * MathF.PI) * 7f);
            var (x0, y0) = Point(t0);
            var (x1, y1) = Point(t1);
            c.Fill(Shape.Capsule(x0, y0, x1, y1, 1.4f), wood);
        }
        c.Outline(Rgba.Black.WithAlpha(0.6f), 0.8f);
        return c;
    }

    /// <summary>A thrown grenade in flight (16 × 16, anchor at its centre).</summary>
    public static Canvas SmallGrenade(Rgba body, Rgba cap)
    {
        var c = new Canvas(16, 16);
        c.Fill(Shape.Circle(8f, 9f, 4.5f), (x, y) => body.Tone(1.15f - (x + y - 16f) * 0.04f));
        c.Fill(Shape.Rect(6.5f, 2.5f, 3f, 3f), cap);
        c.Outline(Rgba.Black.WithAlpha(0.7f), 0.8f);
        return c;
    }

    /// <summary>Small glowing dot or streak used for projectiles (anchor at its centre).</summary>
    public static Canvas Projectile(Rgba core, Rgba glow, float length = 6f)
    {
        var c = new Canvas(16, 16);
        c.Fill(Shape.Capsule(8f - length / 2f, 8f, 8f + length / 2f, 8f, 1.6f), core);
        c.Glow(glow, 1.8f, 1.4f);
        return c;
    }

    /// <summary>Expanding blast, <paramref name="frames"/> frames of 64 × 64 in one row.</summary>
    public static Canvas Explosion(Rgba hot, Rgba cool, int frames = 6, int seed = 3)
    {
        var sheet = new Canvas(64 * frames, 64);
        for (int f = 0; f < frames; f++)
        {
            var c = new Canvas(64, 64);
            float t = (f + 1f) / frames;
            float radius = 8f + 20f * MathF.Sqrt(t);
            Rgba color = hot.Lerp(cool, t).Fade(1f - t * 0.55f);
            c.Fill(Shape.Blob(32f, 34f, radius + 4f, a => radius * (0.85f + 0.15f * Noise.Hash((int)(a * 10f), f, seed))), (x, y) =>
            {
                float d = MathF.Sqrt((x - 32f) * (x - 32f) + (y - 34f) * (y - 34f)) / radius;
                return color.Lerp(hot.Tone(1.3f), Math.Clamp(1f - d * 1.4f, 0f, 1f) * (1f - t));
            });
            c.Glow(hot.Fade(0.6f), 2.5f, 1.2f * (1f - t));
            sheet.Draw(c, f * 64);
        }

        return sheet;
    }

    /// <summary>Diamond outline on the floor of a tile (map cursor), with optional cross.</summary>
    public static Canvas Cursor(Rgba color, bool cross = false, float width = 1.6f)
    {
        Canvas c = TileArt.New();
        (float tx, float ty) = IsoDraw.Project(0, 0, 0);
        (float rx, float ry) = IsoDraw.Project(16, 0, 0);
        (float bx, float by) = IsoDraw.Project(16, 16, 0);
        (float lx, float ly) = IsoDraw.Project(0, 16, 0);
        Shape outer = Shape.Polygon((tx, ty), (rx, ry), (bx, by), (lx, ly));
        Shape inner = Shape.Polygon((tx, ty + width * 1.6f), (rx - width * 3.2f, ry), (bx, by - width * 1.6f), (lx + width * 3.2f, ly));
        c.Fill(outer.Minus(inner), color);
        if (cross)
        {
            (float cx, float cy) = Center;
            c.Fill(Shape.Capsule(cx - 9f, cy, cx + 9f, cy, 0.9f), color);
            c.Fill(Shape.Capsule(cx, cy - 5f, cx, cy + 5f, 0.9f), color);
        }

        return c;
    }

    /// <summary>Filled translucent diamond (path preview, zones).</summary>
    public static Canvas Marker(Rgba color, float scale = 0.35f)
    {
        Canvas c = TileArt.New();
        (float cx, float cy) = Center;
        float w = 32f * scale, h = 16f * scale;
        c.Fill(Shape.Polygon((cx, cy - h), (cx + w, cy), (cx, cy + h), (cx - w, cy)), color);
        return c;
    }

    /// <summary>Small arrow pointing down, drawn above the selected unit (anchor at its tip).</summary>
    public static Canvas Arrow(Rgba color)
    {
        var c = new Canvas(16, 16);
        c.Fill(Shape.Polygon((2f, 2f), (14f, 2f), (8f, 14f)), color);
        c.Outline(Rgba.Black.WithAlpha(0.8f), 1f);
        return c;
    }
}
