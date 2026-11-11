namespace PlaceholderArt;

/// <summary>Which face of a voxel box a pixel belongs to.</summary>
public enum Face
{
    Top,

    /// <summary>Face towards +Y (south), seen on the lower left.</summary>
    South,

    /// <summary>Face towards +X (east), seen on the lower right.</summary>
    East,
}

/// <summary>
/// Colour of a face at face-local coordinates in voxels: u runs along the face (west→east for
/// top and south faces, north→south for east faces), v runs upwards (or north→south on the top).
/// </summary>
public delegate Rgba Material(Face face, float u, float v);

/// <summary>
/// Isometric drawing into a 64 × 80 tile frame, using the game's projection: one voxel is 2 pixels
/// across and 2 pixels high, a tile is 16 × 16 × 24 voxels and the north corner of the floor
/// diamond sits at pixel (32, 48) — the anchor declared in assets.json.
/// </summary>
public static class IsoDraw
{
    public const int FrameWidth = 64;
    public const int FrameHeight = 80;
    public const float AnchorX = 32f;
    public const float AnchorY = 48f;

    public static (float X, float Y) Project(float vx, float vy, float vz) =>
        (AnchorX + (vx - vy) * 2f, AnchorY + (vx + vy) - vz * 2f);

    /// <summary>Draws the visible faces of a box [x0,x1) × [y0,y1) × [z0,z1) in voxels.</summary>
    public static void Box(Canvas c, float x0, float y0, float z0, float x1, float y1, float z1, Material material, bool top = true, bool sides = true)
    {
        if (sides)
        {
            Quad(c, Face.South, material, (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1), y1, x1);
            Quad(c, Face.East, material, (x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1), y1, x1);
        }

        if (top) Quad(c, Face.Top, material, (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1), y1, x1, z1);
    }

    /// <summary>Flat floor diamond (the top of the tile at height <paramref name="z"/>).</summary>
    public static void Floor(Canvas c, Material material, float z = 0f) =>
        Quad(c, Face.Top, material, (0, 0, z), (16, 0, z), (16, 16, z), (0, 16, z), 16, 16, z);

    private static void Quad(Canvas c, Face face, Material material,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) d, (float X, float Y, float Z) e,
        float faceY, float faceX, float faceZ = 0f)
    {
        var pa = Project(a.X, a.Y, a.Z);
        var pb = Project(b.X, b.Y, b.Z);
        var pd = Project(d.X, d.Y, d.Z);
        var pe = Project(e.X, e.Y, e.Z);
        Shape shape = Shape.Polygon(pa, pb, pd, pe);
        c.Fill(shape, (px, py) =>
        {
            // Back-project the pixel onto the face plane to get face-local voxel coordinates.
            float dx = (px - AnchorX) / 2f;
            switch (face)
            {
                case Face.South:
                {
                    float vx = dx + faceY;
                    float vz = (AnchorY + vx + faceY - py) / 2f;
                    return material(face, vx, vz);
                }
                case Face.East:
                {
                    float vy = faceX - dx;
                    float vz = (AnchorY + faceX + vy - py) / 2f;
                    return material(face, vy, vz);
                }
                default:
                {
                    float sum = py - AnchorY + faceZ * 2f;
                    float vx = (sum + dx) / 2f;
                    float vy = (sum - dx) / 2f;
                    return material(face, vx, vy);
                }
            }
        });
    }

    /// <summary>Solid material with the usual light: top brightest, south face medium, east face dark.</summary>
    public static Material Solid(Rgba color, float grain = 0.06f, int seed = 1) => (face, u, v) =>
    {
        float n = (Noise.Hash((int)MathF.Floor(u * 2f), (int)MathF.Floor(v * 2f), seed + (int)face) - 0.5f) * grain;
        return color.Tone(Light(face) + n);
    };

    public static float Light(Face face) => face switch
    {
        Face.Top => 1.08f,
        Face.South => 0.82f,
        _ => 0.62f,
    };

    /// <summary>Running-bond bricks or stones on the sides, plain top.</summary>
    public static Material Bricks(Rgba brick, Rgba mortar, float brickLength = 6f, float rowHeight = 3f, int seed = 3) => (face, u, v) =>
    {
        if (face == Face.Top) return brick.Tone(Light(face) * 0.95f);
        int row = (int)MathF.Floor(v / rowHeight);
        float offset = (row & 1) * brickLength * 0.5f;
        float along = u + offset;
        int column = (int)MathF.Floor(along / brickLength);
        bool joint = v - row * rowHeight < 0.5f || along - column * brickLength < 0.5f;
        if (joint) return mortar.Tone(Light(face));
        float n = (Noise.Hash(column, row, seed) - 0.5f) * 0.18f;
        return brick.Tone(Light(face) + n);
    };

    /// <summary>Planks running along u.</summary>
    public static Material Planks(Rgba wood, float width = 3f, int seed = 5) => (face, u, v) =>
    {
        float across = face == Face.Top ? v : v;
        int plank = (int)MathF.Floor(across / width);
        bool joint = across - plank * width < 0.35f;
        float grain = Noise.Value(u * 0.7f, plank * 3.1f, 64, 64, seed) * 0.16f - 0.08f;
        float n = (Noise.Hash(plank, 0, seed) - 0.5f) * 0.16f;
        return wood.Tone(Light(face) + n + grain - (joint ? 0.25f : 0f));
    };

    /// <summary>Square tiles on top (floors), solid sides.</summary>
    public static Material Tiles(Rgba color, Rgba grout, float size = 4f, int seed = 7) => (face, u, v) =>
    {
        int cx = (int)MathF.Floor(u / size), cy = (int)MathF.Floor(v / size);
        bool joint = u - cx * size < 0.3f || v - cy * size < 0.3f;
        if (joint) return grout.Tone(Light(face));
        float n = (Noise.Hash(cx, cy, seed) - 0.5f) * 0.12f;
        return color.Tone(Light(face) + n);
    };

    /// <summary>Organic noisy surface (grass, dirt): two colours mixed by fractal noise.</summary>
    public static Material Organic(Rgba a, Rgba b, float scale = 1f, int seed = 9, float speckle = 0.1f) => (face, u, v) =>
    {
        float n = Noise.Value(u * 0.45f * scale, v * 0.45f * scale, 1024, 1024, seed) * 0.65f
                + Noise.Value(u * 1.3f * scale, v * 1.3f * scale, 1024, 1024, seed + 1) * 0.35f;
        float s = (Noise.Hash((int)MathF.Floor(u * 2f), (int)MathF.Floor(v * 2f), seed + 2) - 0.5f) * speckle;
        return a.Lerp(b, Math.Clamp(n, 0f, 1f)).Tone(Light(face) + s);
    };

    /// <summary>Panels with seams (metal hull).</summary>
    public static Material Panels(Rgba color, Rgba seam, float size = 8f, int seed = 11) => (face, u, v) =>
    {
        int cx = (int)MathF.Floor(u / size), cy = (int)MathF.Floor(v / size);
        bool joint = u - cx * size < 0.4f || v - cy * size < 0.4f;
        float n = (Noise.Hash(cx, cy, seed) - 0.5f) * 0.08f;
        return (joint ? seam : color).Tone(Light(face) + n);
    };

    /// <summary>Glass: transparent tint, brighter streak.</summary>
    public static Material Glass(Rgba tint) => (face, u, v) =>
    {
        float streak = MathF.Abs(((u + v) % 6f) - 3f) < 0.6f ? 0.25f : 0f;
        return tint.Tone(Light(face) + streak);
    };
}
