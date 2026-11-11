using System.Numerics;

namespace PlaceholderArt.Rig;

/// <summary>
/// Furniture and objects of the action pack, built from solid blocks and drawn by the same top-down renderer
/// as the characters (banded light from the top left, crisp pixels, seams where parts meet, ink outline).
/// Sprite space: X to the right of the image, Y down, Z up from the floor; origin at the image centre.
/// </summary>
public static class Furniture
{
    static readonly RenderStyle Style = new() { Light = Vector3.Normalize(new Vector3(-0.38f, -0.5f, 1f)), ContactDepth = 1.6f };

    static Palette Base() => new Palette()
        .Set(Mat.Wood, Rgba.Hex("#9a6235")).Set(Mat.WoodDark, Rgba.Hex("#5a3519"))
        .Set(Mat.Fabric, Rgba.Hex("#a3283f")).Set(Mat.Fabric2, Rgba.Hex("#c23a52"))
        .Set(Mat.Screen, Rgba.Hex("#1c2a3a")).Set(Mat.Felt, Rgba.Hex("#1f7a4a"))
        .Set(Mat.Ceramic, Rgba.Hex("#e4e6ea")).Set(Mat.Leaf, Rgba.Hex("#3f9a3a")).Set(Mat.Paper, Rgba.Hex("#efeadb"))
        .Set(Mat.Plastic, Rgba.Hex("#2c2a34")).Set(Mat.Chrome, Rgba.Hex("#b8c0cc")).Set(Mat.Rubber, Rgba.Hex("#1c1c20"))
        .Set(Mat.Metal, Rgba.Hex("#5d6a78")).Set(Mat.Dark, Rgba.Hex("#141218"))
        .Set(Mat.Light, Rgba.Hex("#ffe9a8"), Rgba.Hex("#ffe9a8"), Rgba.Hex("#fff2c8"), Rgba.Hex("#fffbe8"))
        .Set(Mat.Trim, Rgba.Hex("#e8c048")).Set(Mat.Top, Rgba.Hex("#3a7bd5")).Set(Mat.Shirt, Rgba.Hex("#e8742a"))
        .Set(Mat.Glow, Rgba.Hex("#ff4fa0"), Rgba.Hex("#ff4fa0"), Rgba.Hex("#ff7ab8"), Rgba.Hex("#ffc2dc"));

    static int _group;

    /// <summary>Each call starts a new hard-edged piece (pieces get a seam line where one rises over another).</summary>
    static T Piece<T>(T prim, Mat material, Func<Vector3, Mat>? paint = null) where T : Prim
    {
        prim.Group = 8 + (_group++ % 8);
        prim.Material = material;
        if (paint is not null)
        {
            prim.Paint = paint;
            if (prim is Block b) prim.Frame = FrameOf(b);
        }
        return prim;
    }

    static Basis FrameOf(Block b)
    {
        var (center, _, _) = b.Bounds;
        return Basis.Identity with { Origin = new Vector3(center.X, center.Y, 0f) };
    }

    static Block B(float x, float y, float w, float d, float h, Mat m, float round = 1f, float z0 = 0f, Func<Vector3, Mat>? paint = null)
    {
        Block block = Block.On(x, y, w, d, h, round, z0);
        return Piece(block, m, paint is null ? null : p => paint(p));
    }

    static Ball O(float x, float y, float z, float r, Mat m) => Piece(new Ball(new Vector3(x, y, z), r), m);

    static Egg E(float x, float y, float z, float rx, float ry, float rz, Mat m, float turn = 0f) =>
        Piece(new Egg((Basis.Identity with { Origin = new Vector3(x, y, z) }).Turn(turn), new Vector3(rx, ry, rz)), m);

    static Canvas Draw(int w, int h, Palette palette, params Prim[] prims) =>
        TopDownRenderer.Render(prims, palette, w, h, new Vector2(w / 2f, h / 2f), Style);

    public static IEnumerable<(string Name, Canvas Sprite)> All()
    {
        yield return ("sofa", Sofa(Base(), 60));
        yield return ("armchair", Sofa(Base(), 26));
        yield return ("sofa_blue", Sofa(Base().Set(Mat.Fabric, Rgba.Hex("#2b4f8f")).Set(Mat.Fabric2, Rgba.Hex("#3a68b4")), 60));
        yield return ("bed", Bed());
        yield return ("tv", Tv());
        yield return ("desk", Desk());
        yield return ("chair", Chair());
        yield return ("pool_table", PoolTable());
        yield return ("bar", Bar());
        yield return ("fridge", Fridge());
        yield return ("stove", Stove());
        yield return ("counter", Counter());
        yield return ("toilet", Toilet());
        yield return ("bathtub", Bathtub());
        yield return ("lamp", Lamp());
        yield return ("plant", Plant());
        yield return ("arcade", Arcade());
        yield return ("cabinet", Cabinet());
        yield return ("dumpster", Dumpster());
        yield return ("crate", Crate());
        yield return ("table", Table());
        yield return ("car", Car());
    }

    static Canvas Sofa(Palette p, float length)
    {
        float half = length / 2f;
        var prims = new List<Prim>
        {
            B(0, 1, length, 24, 7, Mat.Fabric, 2f),
            B(0, -9, length, 7, 15, Mat.Fabric, 2.5f),
            B(-half + 3.5f, 1, 7, 24, 11, Mat.Fabric, 2.5f),
            B(half - 3.5f, 1, 7, 24, 11, Mat.Fabric, 2.5f),
        };
        int seats = length > 40 ? 3 : 1;
        float seatW = (length - 7f) / seats;
        for (int i = 0; i < seats; i++)
        {
            float x = -half + 3.5f + seatW * (i + 0.5f);
            prims.Add(B(x, 3, seatW - 1f, 16, 10, Mat.Fabric2, 2.5f));
            prims.Add(B(x, -5.5f, seatW - 1.5f, 5, 15.5f, Mat.Fabric2, 2f));
        }
        return Draw((int)length + 6, 34, p, [.. prims]);
    }

    static Canvas Bed()
    {
        Palette p = Base().Set(Mat.Fabric2, Rgba.Hex("#d65a8c")).Set(Mat.Fabric, Rgba.Hex("#f1ece4"));
        return Draw(52, 76, p,
            B(0, 0, 46, 70, 6, Mat.WoodDark, 1f),
            B(0, -33, 46, 5, 14, Mat.WoodDark, 1.5f),
            B(0, 1, 42, 64, 11, Mat.Fabric, 2f),
            E(-9, -24, 12, 8, 4.5f, 3, Mat.Paper),
            E(9, -24, 12, 8, 4.5f, 3, Mat.Paper),
            B(0, 10, 44, 46, 12.5f, Mat.Fabric2, 2.5f, paint: q => q.Y < -20f ? Mat.Fabric : q.Y > 18 && q.Y < 20 ? Mat.Fabric : Mat.Fabric2));
    }

    static Canvas Tv() => Draw(40, 22, Base(),
        B(0, 2, 36, 13, 9, Mat.WoodDark, 1f),
        B(0, -1, 26, 6, 20, Mat.Plastic, 1f, paint: q => q.Y > 2.2f ? Mat.Screen : Mat.Plastic),
        B(12, 4, 6, 4, 11, Mat.Plastic, 0.8f));

    static Canvas Desk()
    {
        Palette p = Base();
        return Draw(68, 36, p,
            B(0, 0, 64, 30, 14, Mat.Wood, 1f, paint: q => MathF.Abs(q.Y % 7f) < 0.6f ? Mat.WoodDark : Mat.Wood),
            B(-14, -6, 16, 5, 26, Mat.Plastic, 1f),
            B(-14, -2, 14, 3, 24, Mat.Screen, 0.5f),
            B(-12, 6, 14, 5, 15.5f, Mat.Plastic, 0.6f),
            B(8, 2, 10, 13, 14.6f, Mat.Paper, 0.2f),
            B(10, 0, 9, 12, 15f, Mat.Paper, 0.2f),
            E(22, -6, 15.5f, 2.5f, 2.5f, 2, Mat.Ceramic),
            B(25, 8, 5, 5, 17, Mat.Metal, 1f));
    }

    static Canvas Chair() => Draw(22, 22, Base().Set(Mat.Fabric, Rgba.Hex("#2a2a34")),
        B(0, 1, 15, 14, 10, Mat.Fabric, 2f),
        B(0, -6, 15, 4, 18, Mat.Fabric, 1.5f));

    static Canvas PoolTable()
    {
        Palette p = Base();
        var prims = new List<Prim>
        {
            B(0, 0, 58, 104, 10, Mat.WoodDark, 2f),
            B(0, 0, 48, 94, 11, Mat.Felt, 0.5f),
            B(-26, 0, 6, 104, 13, Mat.Wood, 2f),
            B(26, 0, 6, 104, 13, Mat.Wood, 2f),
            B(0, -49, 58, 6, 13, Mat.Wood, 2f),
            B(0, 49, 58, 6, 13, Mat.Wood, 2f),
        };
        foreach (var (x, y) in new[] { (-24f, -47f), (24f, -47f), (-25f, 0f), (25f, 0f), (-24f, 47f), (24f, 47f) })
            prims.Add(O(x, y, 11.5f, 2.6f, Mat.Dark));
        (float, float, Mat)[] balls = [(0, 22, Mat.Ceramic), (-3, -18, Mat.Trim), (3, -18, Mat.Fabric2), (0, -23, Mat.Top), (-6, -23, Mat.Shirt), (6, -23, Mat.Dark), (10, 5, Mat.Felt)];
        foreach (var (x, y, m) in balls) prims.Add(O(x, y, 12.6f, 1.8f, m == Mat.Felt ? Mat.Trim : m));
        prims.Add(Piece(new Bone(new Vector3(-14, 36, 14), new Vector3(8, 62 - 40, 14), 0.9f, 0.6f), Mat.Wood));
        return Draw(64, 112, p, [.. prims]);
    }

    static Canvas Bar()
    {
        var prims = new List<Prim> { B(0, 0, 92, 22, 20, Mat.WoodDark, 1.5f), B(0, -2, 92, 16, 21, Mat.Wood, 1f, paint: q => q.Y > 6.5f ? Mat.WoodDark : Mat.Wood) };
        float[] bottles = [-38, -33, -12, 9, 30, 35];
        Mat[] colors = [Mat.Leaf, Mat.Fabric2, Mat.Trim, Mat.Leaf, Mat.Shirt, Mat.Top];
        for (int i = 0; i < bottles.Length; i++)
            prims.Add(Piece(new Bone(new Vector3(bottles[i], -4, 21), new Vector3(bottles[i], -4, 29), 1.7f, 0.9f), colors[i]));
        foreach (float x in new[] { -22f, 0f, 20f }) prims.Add(Piece(new Bone(new Vector3(x, 1, 21), new Vector3(x, 1, 24), 1.5f, 1.6f), Mat.Chrome));
        return Draw(98, 28, Base(), [.. prims]);
    }

    static Canvas Fridge() => Draw(30, 30, Base(),
        B(0, 0, 26, 24, 36, Mat.Ceramic, 2f, paint: q => MathF.Abs(q.Y + 2f) < 0.5f ? Mat.Chrome : Mat.Ceramic),
        B(9, 9, 2, 6, 37, Mat.Chrome, 0.8f));

    static Canvas Stove()
    {
        var prims = new List<Prim> { B(0, 0, 28, 26, 18, Mat.Ceramic, 1.5f), B(0, -11, 28, 4, 22, Mat.Metal, 1f) };
        foreach (var (x, y) in new[] { (-6f, -2f), (6f, -2f), (-6f, 8f), (6f, 8f) })
        {
            prims.Add(E(x, y, 18, 4.2f, 4.2f, 0.8f, Mat.Dark));
            prims.Add(E(x, y, 18.4f, 2f, 2f, 0.6f, Mat.Metal));
        }
        return Draw(32, 30, Base(), [.. prims]);
    }

    static Canvas Counter() => Draw(66, 30, Base(),
        B(0, 0, 62, 26, 18, Mat.WoodDark, 1f),
        B(0, 0, 62, 26, 19, Mat.Ceramic, 1f, paint: q => q.X > 4 && q.X < 22 && MathF.Abs(q.Y - 1f) < 7f ? (MathF.Abs(q.Y - 1f) < 5.5f && q.X > 5.5f && q.X < 20.5f ? Mat.Chrome : Mat.Metal) : Mat.Ceramic),
        Piece(new Bone(new Vector3(13, -9, 19), new Vector3(13, -3, 25), 1f, 0.8f), Mat.Chrome),
        B(-18, -2, 12, 9, 20, Mat.Wood, 0.5f),
        E(-4, 5, 20.5f, 3, 3, 1.5f, Mat.Ceramic));

    static Canvas Toilet() => Draw(22, 28, Base(),
        B(0, -8, 16, 6, 16, Mat.Ceramic, 1.5f),
        E(0, 2, 7, 6.5f, 8.5f, 7, Mat.Ceramic),
        E(0, 2.5f, 13.6f, 4.6f, 6.2f, 0.6f, Mat.Chrome));

    static Canvas Bathtub()
    {
        Palette p = Base().Set(Mat.Light, Rgba.Hex("#8fd4f0"));
        return Draw(36, 68, p,
            B(0, 0, 32, 64, 14, Mat.Ceramic, 3f),
            B(0, 2, 24, 54, 14.4f, Mat.Light, 3f),
            Piece(new Bone(new Vector3(0, -28, 15), new Vector3(0, -23, 18), 1.2f, 1f), Mat.Chrome));
    }

    static Canvas Lamp() => Draw(22, 22, Base(),
        E(0, 0, 4, 4, 4, 4, Mat.Metal),
        E(0, 0, 26, 8, 8, 3.5f, Mat.Light));

    static Canvas Plant()
    {
        var prims = new List<Prim> { Piece(new Bone(new Vector3(0, 0, 0), new Vector3(0, 0, 10), 7f, 8.5f), Mat.Shirt) };
        for (int i = 0; i < 7; i++)
        {
            float a = i * MathF.Tau / 7f + 0.3f;
            prims.Add(E(MathF.Cos(a) * 7f, MathF.Sin(a) * 7f, 18 + (i % 2) * 2, 7f, 2.6f, 1.5f, i % 2 == 0 ? Mat.Leaf : Mat.Felt, a));
        }
        prims.Add(E(0, 0, 22, 4, 4, 3, Mat.Leaf));
        return Draw(34, 34, Base(), [.. prims]);
    }

    static Canvas Arcade() => Draw(30, 30, Base().Set(Mat.Plastic, Rgba.Hex("#24123a")),
        B(0, -2, 26, 22, 34, Mat.Plastic, 1.5f, paint: q => q.Y < -8.5f ? Mat.Glow : Mat.Plastic),
        B(0, 9, 24, 8, 20, Mat.Plastic, 1f, paint: q => q.Y < -1f ? Mat.Screen : Mat.Plastic),
        O(-5, 9.5f, 21, 1.4f, Mat.Fabric2), O(4, 9.5f, 20.6f, 1.2f, Mat.Trim), O(7, 9.5f, 20.6f, 1.2f, Mat.Top));

    static Canvas Cabinet() => Draw(30, 22, Base(),
        B(0, 0, 26, 18, 30, Mat.Metal, 1f, paint: q => MathF.Abs(q.Y - 6f) < 0.6f ? Mat.Dark : Mat.Metal),
        B(-5, -2, 9, 11, 30.6f, Mat.Paper, 0.2f));

    static Canvas Dumpster()
    {
        Palette p = Base().Set(Mat.Metal, Rgba.Hex("#2f6b45"));
        return Draw(66, 36, p,
            B(0, 0, 62, 30, 26, Mat.Metal, 1.5f, paint: q => MathF.Abs(q.X) < 0.6f ? Mat.Dark : Mat.Metal),
            B(-15, -14, 28, 4, 27, Mat.Rubber, 0.5f),
            B(15, -14, 28, 4, 27, Mat.Rubber, 0.5f));
    }

    static Canvas Crate() => Draw(32, 32, Base(),
        B(0, 0, 28, 28, 22, Mat.Wood, 1f, paint: q => MathF.Abs(q.X) > 11.5f || MathF.Abs(q.Y) > 11.5f || MathF.Abs(q.X - q.Y) < 2f ? Mat.WoodDark : MathF.Abs(q.Y % 7f) < 0.5f ? Mat.WoodDark : Mat.Wood));

    static Canvas Table() => Draw(66, 34, Base(),
        B(0, 0, 60, 28, 14, Mat.Wood, 1.5f, paint: q => MathF.Abs(q.Y % 7f) < 0.5f ? Mat.WoodDark : Mat.Wood),
        B(-8, -2, 9, 12, 14.6f, Mat.Paper, 0.2f),
        E(16, -4, 15.5f, 2.6f, 2.6f, 2, Mat.Ceramic),
        B(6, 6, 6, 3, 15f, Mat.Dark, 0.5f));

    static Canvas Car()
    {
        Palette p = Base().Set(Mat.Fabric2, Rgba.Hex("#e0305f"));
        var prims = new List<Prim>
        {
            B(0, 0, 112, 50, 14, Mat.Fabric2, 6f, z0: 4f, paint: q => MathF.Abs(q.Y) < 2.4f && MathF.Abs(q.X) > 22 ? Mat.Ceramic : Mat.Fabric2),
            B(-4, 0, 50, 42, 24, Mat.Screen, 7f),
            B(-6, 0, 30, 40, 25.5f, Mat.Fabric2, 4f),
        };
        foreach (var (x, y) in new[] { (-36f, -25f), (36f, -25f), (-36f, 25f), (36f, 25f) }) prims.Add(B(x, y, 18, 6, 12, Mat.Rubber, 2f));
        prims.Add(B(55, -17, 3, 8, 13, Mat.Light, 1f));
        prims.Add(B(55, 17, 3, 8, 13, Mat.Light, 1f));
        prims.Add(B(-56, -18, 2, 8, 12, Mat.Glow, 1f));
        prims.Add(B(-56, 18, 2, 8, 12, Mat.Glow, 1f));
        return Draw(120, 64, p, [.. prims]);
    }
}
