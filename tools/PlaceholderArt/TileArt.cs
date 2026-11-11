namespace PlaceholderArt;

/// <summary>
/// Map tile sprites built from voxel boxes. Boxes use the same numbers as the pack's
/// shapes/shapes.json, so what you see is what bullets hit. "W" sprites stand on the west edge of
/// the tile (upper left on screen), "N" sprites on the north edge (upper right).
/// </summary>
public static class TileArt
{
    public static Canvas New() => new(IsoDraw.FrameWidth, IsoDraw.FrameHeight);

    public static Canvas Floor(Material material, float z = 0f)
    {
        Canvas c = New();
        IsoDraw.Floor(c, material, z);
        return c;
    }

    /// <summary>A full-height wall slab on one edge.</summary>
    public static Canvas Wall(bool west, Material material, float height = 24f, float thickness = 2f)
    {
        Canvas c = New();
        EdgeBox(c, west, 0, 16, 0, height, thickness, material);
        return c;
    }

    /// <summary>Box along an edge: <paramref name="from"/>–<paramref name="to"/> along the edge, z0–z1 high.</summary>
    public static void EdgeBox(Canvas c, bool west, float from, float to, float z0, float z1, float thickness, Material material)
    {
        if (west) IsoDraw.Box(c, 0, from, z0, thickness, to, z1, material);
        else IsoDraw.Box(c, from, 0, z0, to, thickness, z1, material);
    }

    /// <summary>Wall remains: a low, uneven stub.</summary>
    public static Canvas Ruin(bool west, Material material)
    {
        Canvas c = New();
        float[] heights = [5, 3, 6, 2, 4, 5, 3, 6];
        for (int i = 0; i < 8; i++) EdgeBox(c, west, i * 2, i * 2 + 2, 0, heights[i], 2, material);
        return c;
    }

    /// <summary>Window: sill, lintel, two posts and (unless broken) a pane.</summary>
    public static Canvas Window(bool west, Material frame, Material? glass)
    {
        Canvas c = New();
        EdgeBox(c, west, 0, 16, 0, 9, 2, frame);
        EdgeBox(c, west, 0, 2, 9, 19, 2, frame);
        if (glass is not null) EdgeBox(c, west, 2, 14, 9, 19, 1, glass);
        else
        {
            // A few shards left in the frame.
            EdgeBox(c, west, 2, 4, 9, 12, 1, IsoDraw.Glass(new Rgba(0.7f, 0.85f, 0.95f, 0.5f)));
            EdgeBox(c, west, 11, 14, 15, 19, 1, IsoDraw.Glass(new Rgba(0.7f, 0.85f, 0.95f, 0.5f)));
        }

        EdgeBox(c, west, 14, 16, 9, 19, 2, frame);
        EdgeBox(c, west, 0, 16, 19, 24, 2, frame);
        return c;
    }

    /// <summary>Door: closed leaf between posts, or open (frame only, leaf pushed aside).</summary>
    public static Canvas Door(bool west, Material frame, Material leaf, bool open)
    {
        Canvas c = New();
        EdgeBox(c, west, 0, 2, 0, 24, 2, frame);
        if (!open) EdgeBox(c, west, 2, 14, 0, 21, 1.5f, leaf);
        else if (west) IsoDraw.Box(c, 1, 2, 0, 8, 3.5f, 21, leaf);
        else IsoDraw.Box(c, 2, 1, 0, 3.5f, 8, 21, leaf);
        EdgeBox(c, west, 14, 16, 0, 24, 2, frame);
        EdgeBox(c, west, 2, 14, 21, 24, 2, frame);
        return c;
    }

    /// <summary>Sliding door: closed panel, or open (frame only).</summary>
    public static Canvas Hatch(bool west, Material frame, Material panel, bool open)
    {
        Canvas c = New();
        EdgeBox(c, west, 0, 2, 0, 24, 2, frame);
        if (!open) EdgeBox(c, west, 2, 14, 0, 20, 2, panel);
        EdgeBox(c, west, 14, 16, 0, 24, 2, frame);
        EdgeBox(c, west, 2, 14, 20, 24, 2, frame);
        return c;
    }

    /// <summary>Low fence: posts and two rails.</summary>
    public static Canvas Fence(bool west, Material material, float height = 10f)
    {
        Canvas c = New();
        for (int post = 0; post < 3; post++) EdgeBox(c, west, post * 7, post * 7 + 2, 0, height, 1.5f, material);
        EdgeBox(c, west, 0, 16, height * 0.45f, height * 0.6f, 1f, material);
        EdgeBox(c, west, 0, 16, height * 0.85f, height, 1f, material);
        return c;
    }

    public static Canvas Block(Material material, float x0, float y0, float z0, float x1, float y1, float z1)
    {
        Canvas c = New();
        IsoDraw.Box(c, x0, y0, z0, x1, y1, z1, material);
        return c;
    }

    /// <summary>
    /// One tile of a two-tile staircase rising towards the north (or the west): the low tile goes
    /// from 4 to 8 voxels, the high one from 12 to 16 (shapes stepLow*/stepHigh*).
    /// </summary>
    public static Canvas Stairs(bool west, bool high, Material material)
    {
        Canvas c = New();
        float baseZ = high ? 8 : 0;
        if (high) Box(c, west, 0, 16, 0, 8, material);
        Box(c, west, 0, 8, baseZ, baseZ + 8, material);   // back half, higher step
        Box(c, west, 8, 16, baseZ, baseZ + 4, material);  // front half, lower step
        return c;

        // Along the rising axis: from/to measured from the high (north or west) edge.
        static void Box(Canvas c, bool west, float from, float to, float z0, float z1, Material m)
        {
            if (west) IsoDraw.Box(c, from, 0, z0, to, 16, z1, m);
            else IsoDraw.Box(c, 0, from, z0, 16, to, z1, m);
        }
    }

    public static Canvas Crate(Material material)
    {
        Canvas c = Block(material, 2, 2, 0, 14, 14, 12);
        // Cross braces on the visible faces.
        IsoDraw.Box(c, 2, 13.6f, 5.5f, 14, 14.2f, 6.5f, IsoDraw.Solid(new Rgba(0.25f, 0.18f, 0.1f)), top: false);
        IsoDraw.Box(c, 13.6f, 2, 5.5f, 14.2f, 14, 6.5f, IsoDraw.Solid(new Rgba(0.25f, 0.18f, 0.1f)), top: false);
        return c;
    }

    /// <summary>Upright cylinder drawn with ellipses (barrels, wells).</summary>
    public static Canvas Barrel(Rgba color, Rgba band, float radius = 4f, float height = 14f)
    {
        Canvas c = New();
        (float cx, float cy) = IsoDraw.Project(8, 8, 0);
        float rx = radius * 2.8f, ry = radius * 1.4f, h = height * 2f;
        c.Fill(Shape.Ellipse(cx, cy, rx, ry), color.Shade(0.6f));
        c.Fill(Shape.Rect(cx - rx, cy - h, rx * 2f, h), (x, _) => color.Tone(0.65f + 0.45f * (1f - (x - (cx - rx)) / (rx * 2f))));
        c.Fill(Shape.Rect(cx - rx, cy - h * 0.35f, rx * 2f, 2f), band.Shade(0.8f));
        c.Fill(Shape.Rect(cx - rx, cy - h * 0.75f, rx * 2f, 2f), band.Shade(0.8f));
        c.Fill(Shape.Ellipse(cx, cy - h, rx, ry), color.Tone(1.1f));
        c.Fill(Shape.Ellipse(cx, cy - h, rx * 0.7f, ry * 0.7f), color.Shade(0.75f));
        c.Outline(Rgba.Black.WithAlpha(0.5f), 0.7f);
        return c;
    }

    /// <summary>Trunk and a round crown (deciduous) or stacked cones (conifer).</summary>
    public static Canvas Tree(Rgba bark, Rgba leaves, bool conifer, int seed)
    {
        Canvas c = New();
        (float cx, float cy) = IsoDraw.Project(8, 8, 0);
        c.Fill(Shape.Ellipse(cx, cy, 14f, 7f), Rgba.Black.WithAlpha(0.3f));
        c.Fill(Shape.Rect(cx - 2.5f, cy - 30f, 5f, 30f), (x, _) => bark.Tone(x < cx ? 0.95f : 0.7f));
        if (conifer)
        {
            for (int i = 0; i < 3; i++)
            {
                float baseY = cy - 14f - i * 12f, half = 16f - i * 4f;
                c.Fill(Shape.Polygon((cx - half, baseY), (cx + half, baseY), (cx, baseY - 20f)), (x, y) =>
                    leaves.Tone(0.75f + 0.35f * (1f - (x - cx + half) / (2f * half)) + (Noise.Hash((int)x, (int)y, seed) - 0.5f) * 0.15f));
            }
        }
        else
        {
            c.Fill(Shape.Blob(cx, cy - 36f, 20f, a => 15f + 3f * MathF.Sin(a * 5f + seed)), (x, y) =>
                leaves.Tone(0.7f + 0.4f * Math.Clamp((cy - 20f - y) / 34f, 0f, 1f) + (Noise.Hash((int)x / 2, (int)y / 2, seed) - 0.5f) * 0.22f));
        }

        c.Outline(Rgba.Black.WithAlpha(0.45f), 0.7f);
        return c;
    }

    public static Canvas Bush(Rgba leaves, int seed)
    {
        Canvas c = New();
        (float cx, float cy) = IsoDraw.Project(8, 8, 0);
        c.Fill(Shape.Blob(cx, cy - 7f, 15f, a => 11f + 2.5f * MathF.Sin(a * 6f + seed)), (x, y) =>
            leaves.Tone(0.7f + 0.45f * Math.Clamp((cy - y) / 16f, 0f, 1f) + (Noise.Hash((int)x / 2, (int)y / 2, seed) - 0.5f) * 0.25f));
        c.Outline(Rgba.Black.WithAlpha(0.4f), 0.7f);
        return c;
    }

    public static Canvas Rock(Rgba stone, int seed)
    {
        Canvas c = New();
        IsoDraw.Box(c, 3, 4, 0, 13, 13, 7, IsoDraw.Solid(stone, 0.12f, seed));
        IsoDraw.Box(c, 5, 5, 7, 11, 11, 10, IsoDraw.Solid(stone.Tone(1.05f), 0.12f, seed + 1));
        return c;
    }

    public static Canvas Table(Material top, Material legs)
    {
        Canvas c = New();
        foreach ((float x, float y) in new[] { (2f, 4f), (13f, 4f), (2f, 11f), (13f, 11f) })
            IsoDraw.Box(c, x, y, 0, x + 1, y + 1, 8, legs);
        IsoDraw.Box(c, 1, 3, 8, 15, 13, 10, top);
        return c;
    }

    public static Canvas Console(Material body, Rgba screen)
    {
        Canvas c = Block(body, 2, 2, 0, 14, 6, 14);
        IsoDraw.Box(c, 3, 5.8f, 7, 13, 6.2f, 12, IsoDraw.Solid(screen, 0.02f), top: false);
        return c;
    }

    public static Canvas Debris(Rgba color, int seed)
    {
        Canvas c = New();
        var random = new Random(seed);
        for (int i = 0; i < 9; i++)
        {
            float x = 2 + random.Next(0, 10), y = 2 + random.Next(0, 10);
            float w = 2 + random.Next(0, 3), h = 1 + random.Next(0, 3);
            IsoDraw.Box(c, x, y, 0, x + w, y + w, h, IsoDraw.Solid(color.Tone(0.8f + (float)random.NextDouble() * 0.4f), 0.1f, seed + i));
        }

        return c;
    }
}
