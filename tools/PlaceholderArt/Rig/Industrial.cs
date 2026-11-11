using System.Numerics;

namespace PlaceholderArt.Rig;

/// <summary>
/// Outdoor and industrial objects of the thermal pack, built from blocks like the furniture: every material is a
/// temperature (cold metal and glass, lukewarm concrete, hot exhausts, lamps and hot-water pipes), seen from far above.
/// </summary>
public static class Industrial
{
    static Rgba G(float v) => Rgba.Grey(v);

    static Palette Temperatures()
    {
        var p = new Palette();
        (Mat, float)[] heat =
        [
            (Mat.Metal, 0.25f), (Mat.Dark, 0.13f), (Mat.Rubber, 0.17f), (Mat.Wood, 0.31f), (Mat.WoodDark, 0.24f), (Mat.Ceramic, 0.3f),
            (Mat.Paper, 0.34f), (Mat.Chrome, 0.32f), (Mat.Plastic, 0.27f), (Mat.Glow, 0.97f), (Mat.Light, 0.8f), (Mat.Felt, 0.11f),
            (Mat.Leaf, 0.33f), (Mat.Fabric, 0.3f), (Mat.Screen, 0.19f), (Mat.Trim, 0.5f), (Mat.Top, 0.52f),
        ];
        foreach (var (m, v) in heat) p.Set(m, G(v * 0.55f), G(v * 0.8f), G(v), G(MathF.Min(1f, v * 1.15f + 0.03f)));
        return p;
    }

    static readonly RenderStyle Style = new()
    {
        Light = Vector3.Normalize(new Vector3(-0.2f, -0.25f, 1f)), LightBand = 0.95f, ShadeBand = 0.7f, Ink = Rgba.Grey(0.07f), ContactDepth = 1.6f,
    };

    static int _group;

    static T P<T>(T prim, Mat m, Func<Vector3, Mat>? paint = null) where T : Prim
    {
        prim.Group = 8 + (_group++ % 8);
        prim.Material = m;
        if (paint is not null)
        {
            prim.Paint = paint;
            var (c, _, _) = prim.Bounds;
            prim.Frame = Basis.Identity with { Origin = new Vector3(c.X, c.Y, 0f) };
        }
        return prim;
    }

    static Block B(float x, float y, float w, float d, float h, Mat m, float round = 1f, float z0 = 0f, Func<Vector3, Mat>? paint = null, float turn = 0f)
    {
        var frame = (Basis.Identity with { Origin = new Vector3(x, y, z0 + h / 2f) }).Turn(turn);
        return P(new Block(frame, new Vector3(w / 2f, d / 2f, h / 2f), round), m, paint);
    }

    static Bone Cyl(Vector3 a, Vector3 b, float r, Mat m) => P(new Bone(a, b, r, r), m);

    static Canvas Draw(int w, int h, bool warm, int seed, params Prim[] prims)
    {
        Canvas c = TopDownRenderer.Render(prims, Temperatures(), w, h, new Vector2(w / 2f, h / 2f), Style);
        c.Blur(0.3f);
        if (warm) c.Glow(Rgba.White, 1.8f, 0.45f);
        NoctArt.Grain(c, seed, 0.03f);
        return c;
    }

    public static IEnumerable<(string Name, Canvas Sprite)> All()
    {
        yield return ("container", Container());
        yield return ("tank", Tank());
        yield return ("pipes", Pipes());
        yield return ("generator", Generator());
        yield return ("van", Van());
        yield return ("rubble", Rubble());
        yield return ("tires", Tires());
        yield return ("pallets", Pallets());
        yield return ("pole", Pole());
        yield return ("vent", Vent());
    }

    static Canvas Container() => Draw(70, 34, false, 501,
        B(0, 0, 64, 28, 26, Mat.Metal, 1f, paint: q => q.X > 29f ? (MathF.Abs(q.Y) < 0.6f ? Mat.Dark : Mat.Chrome) : MathF.Abs(q.X % 4f) < 0.9f ? Mat.Dark : Mat.Metal));

    static Canvas Tank()
    {
        var prims = new List<Prim>
        {
            Cyl(new Vector3(0, 0, 0), new Vector3(0, 0, 24), 22f, Mat.Metal),
            Cyl(new Vector3(0, 0, 24), new Vector3(0, 0, 26), 18f, Mat.Chrome),
            B(6, -6, 7, 7, 28, Mat.Dark, 1f),
            Cyl(new Vector3(-16, 16, 8), new Vector3(-26, 26, 8), 2.6f, Mat.Trim),
        };
        for (int i = 0; i < 6; i++) prims.Add(B(-21.5f, -3f + i * 2.4f, 2.5f, 1f, 25, Mat.Dark, 0.3f));
        return Draw(56, 56, false, 502, [.. prims]);
    }

    static Canvas Pipes()
    {
        var prims = new List<Prim>
        {
            Cyl(new Vector3(-48, -5, 10), new Vector3(48, -5, 10), 3.6f, Mat.Trim),
            Cyl(new Vector3(-48, 5, 9), new Vector3(48, 5, 9), 3f, Mat.Metal),
        };
        foreach (float x in new[] { -36f, 0f, 36f }) prims.Add(B(x, 0, 4, 18, 9, Mat.Dark, 0.5f));
        foreach (float x in new[] { -20f, 20f }) prims.Add(Cyl(new Vector3(x - 1.5f, -5, 10), new Vector3(x + 1.5f, -5, 10), 4.4f, Mat.Chrome));
        return Draw(104, 26, true, 503, [.. prims]);
    }

    static Canvas Generator()
    {
        var prims = new List<Prim>
        {
            B(0, 0, 38, 24, 18, Mat.Top, 2f, paint: q => q.X > 8f && MathF.Abs(q.Y % 3f) < 1f ? Mat.Glow : Mat.Top),
            B(-12, 0, 10, 20, 20, Mat.Metal, 1f),
            Cyl(new Vector3(-3, -8, 18), new Vector3(-3, -8, 26), 2.6f, Mat.Glow),
        };
        return Draw(46, 32, true, 504, [.. prims]);
    }

    static Canvas Van()
    {
        var prims = new List<Prim>
        {
            B(-8, 0, 76, 42, 30, Mat.Metal, 3f, paint: q => MathF.Abs(q.X % 9f) < 0.7f ? Mat.Dark : Mat.Metal),
            B(36, 0, 20, 40, 20, Mat.Metal, 4f, z0: 4f),
            B(33, 0, 8, 34, 26, Mat.Screen, 2f),
        };
        foreach (var (x, y) in new[] { (-30f, -22f), (-30f, 22f), (30f, -22f), (30f, 22f) }) prims.Add(B(x, y, 14, 5, 12, Mat.Rubber, 2f));
        prims.Add(B(-14, 0, 22, 14, 31, Mat.Chrome, 1f));
        return Draw(106, 54, false, 505, [.. prims]);
    }

    static Canvas Rubble()
    {
        var rng = new Random(506);
        var prims = new List<Prim>();
        for (int i = 0; i < 14; i++)
        {
            float x = (rng.NextSingle() - 0.5f) * 34f, y = (rng.NextSingle() - 0.5f) * 28f;
            float s = 3f + rng.NextSingle() * 6f;
            prims.Add(i % 4 == 0
                ? P(new Ball(new Vector3(x, y, s * 0.3f), s * 0.6f), Mat.Ceramic)
                : B(x, y, s * 1.3f, s, s * (0.5f + rng.NextSingle()), i % 5 == 0 ? Mat.Metal : Mat.Ceramic, 0.6f, turn: rng.NextSingle() * 3f));
        }
        prims.Add(Cyl(new Vector3(-14, 10, 2), new Vector3(10, 16, 3), 0.8f, Mat.Dark));
        return Draw(50, 46, false, 507, [.. prims]);
    }

    static Canvas Tires()
    {
        var prims = new List<Prim>();
        float z = 0f;
        foreach (var (x, y) in new[] { (-5f, -2f), (4f, 3f), (-2f, 1f) })
        {
            prims.Add(P(new Egg(Basis.Identity with { Origin = new Vector3(x, y, z + 3f) }, new Vector3(9f, 9f, 3.2f)), Mat.Rubber));
            prims.Add(P(new Egg(Basis.Identity with { Origin = new Vector3(x, y, z + 3.3f) }, new Vector3(4.5f, 4.5f, 3.1f)), Mat.Dark));
            z += 3.5f;
        }
        return Draw(38, 38, false, 508, [.. prims]);
    }

    static Canvas Pallets() => Draw(42, 42, false, 509,
        B(0, 0, 36, 36, 6, Mat.WoodDark, 0.5f),
        B(0, 0, 36, 36, 7, Mat.Wood, 0.5f, z0: 6f, paint: q => MathF.Abs(q.Y % 7f) < 1.6f ? Mat.WoodDark : Mat.Wood),
        B(2, 1, 34, 34, 6, Mat.Wood, 0.5f, z0: 13f, paint: q => MathF.Abs(q.X % 7f) < 1.6f ? Mat.WoodDark : Mat.Wood, turn: 0.08f));

    static Canvas Pole() => Draw(30, 30, true, 510,
        P(new Ball(new Vector3(-6, 0, 2), 3f), Mat.Metal),
        Cyl(new Vector3(-6, 0, 2), new Vector3(-6, 0, 40), 1.4f, Mat.Metal),
        Cyl(new Vector3(-6, 0, 40), new Vector3(5, 0, 41), 1.2f, Mat.Metal),
        P(new Egg(Basis.Identity with { Origin = new Vector3(6, 0, 40) }, new Vector3(4.5f, 3f, 1.6f)), Mat.Light));

    static Canvas Vent() => Draw(32, 32, true, 511,
        B(0, 0, 26, 26, 10, Mat.Metal, 1.5f),
        P(new Egg(Basis.Identity with { Origin = new Vector3(0, 0, 10) }, new Vector3(9f, 9f, 1.2f)), Mat.Dark),
        P(new Egg(Basis.Identity with { Origin = new Vector3(0, 0, 10.6f) }, new Vector3(4f, 4f, 1f)), Mat.Glow));
}
