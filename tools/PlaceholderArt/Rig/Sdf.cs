using System.Numerics;

namespace PlaceholderArt.Rig;

/// <summary>
/// An orthonormal frame placed in sprite space: X forward (the way the character faces), Y its right-hand side,
/// Z up towards the camera. Rotations are algebraic (about an axis by an angle), so the handedness of the
/// screen never enters the maths.
/// </summary>
public readonly record struct Basis(Vector3 Origin, Vector3 Forward, Vector3 Right, Vector3 Up)
{
    public static readonly Basis Identity = new(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

    /// <summary>A point given in this frame's local coordinates (x forward, y right, z up), in sprite space.</summary>
    public Vector3 At(float x, float y, float z) => Origin + Forward * x + Right * y + Up * z;

    public Vector3 At(Vector3 local) => At(local.X, local.Y, local.Z);

    /// <summary>A direction given in local coordinates.</summary>
    public Vector3 Dir(Vector3 local) => Forward * local.X + Right * local.Y + Up * local.Z;

    public Vector3 Local(Vector3 world)
    {
        Vector3 d = world - Origin;
        return new Vector3(Vector3.Dot(d, Forward), Vector3.Dot(d, Right), Vector3.Dot(d, Up));
    }

    public Basis Moved(float x, float y, float z) => this with { Origin = At(x, y, z) };

    Basis Rotated(Vector3 axis, float angle)
    {
        if (angle == 0f) return this;
        var q = Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle);
        return this with { Forward = Vector3.Transform(Forward, q), Right = Vector3.Transform(Right, q), Up = Vector3.Transform(Up, q) };
    }

    /// <summary>Turns towards the right-hand side (about the up axis).</summary>
    public Basis Turn(float angle) => Rotated(Up, angle);

    /// <summary>Tips the up axis towards the forward one (bowing, or falling forwards, when positive).</summary>
    public Basis Lean(float angle) => Rotated(Right, angle);

    /// <summary>Tips the up axis towards the right-hand side.</summary>
    public Basis Roll(float angle) => Rotated(Forward, -angle);
}

/// <summary>A solid part of a model: a signed distance, a body group and how its surface is painted.</summary>
public abstract class Prim
{
    /// <summary>Parts of one group melt into each other; different groups keep a crisp edge (and a contact line).</summary>
    public int Group { get; init; }

    /// <summary>Body part, for drawing a subset (severed limbs).</summary>
    public int Part { get; init; }

    public Mat Material { get; init; }

    /// <summary>Optional surface detail: the material at a point given in <see cref="Frame"/> coordinates.</summary>
    public Func<Vector3, Mat>? Paint { get; init; }

    public Basis Frame { get; init; } = Basis.Identity;

    public abstract float Distance(Vector3 p);

    /// <summary>Bounding circle of the part seen from above, and its highest point.</summary>
    public abstract (Vector2 Center, float Radius, float Top) Bounds { get; }

    public Mat MaterialAt(Vector3 p) => Paint is null ? Material : Paint(Frame.Local(p));
}

public sealed class Ball(Vector3 center, float radius) : Prim
{
    public override float Distance(Vector3 p) => Vector3.Distance(p, center) - radius;

    public override (Vector2, float, float) Bounds => (new Vector2(center.X, center.Y), radius, center.Z + radius);
}

/// <summary>Round cone: a capsule whose radius goes from <paramref name="ra"/> at a to <paramref name="rb"/> at b.</summary>
public sealed class Bone(Vector3 a, Vector3 b, float ra, float rb) : Prim
{
    public override float Distance(Vector3 p)
    {
        Vector3 ba = b - a;
        float l2 = Vector3.Dot(ba, ba);
        if (l2 < 1e-6f) return Vector3.Distance(p, a) - MathF.Max(ra, rb);
        float rr = ra - rb;
        float a2 = l2 - rr * rr;
        float il2 = 1f / l2;
        Vector3 pa = p - a;
        float y = Vector3.Dot(pa, ba);
        float z = y - l2;
        Vector3 xv = pa * l2 - ba * y;
        float x2 = Vector3.Dot(xv, xv);
        float y2 = y * y * l2;
        float z2 = z * z * l2;
        float k = MathF.Sign(rr) * rr * rr * x2;
        if (MathF.Sign(z) * a2 * z2 > k) return MathF.Sqrt(x2 + z2) * il2 - rb;
        if (MathF.Sign(y) * a2 * y2 < k) return MathF.Sqrt(x2 + y2) * il2 - ra;
        return (MathF.Sqrt(x2 * a2 * il2) + y * rr) * il2 - ra;
    }

    public override (Vector2, float, float) Bounds
    {
        get
        {
            var c = new Vector2((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
            float r = Vector2.Distance(c, new Vector2(a.X, a.Y)) + MathF.Max(ra, rb);
            return (c, r, MathF.Max(a.Z + ra, b.Z + rb));
        }
    }
}

/// <summary>Ellipsoid with its radii along the axes of <see cref="Prim.Frame"/> (approximate distance).</summary>
public sealed class Egg(Basis frame, Vector3 radii) : Prim
{
    readonly Basis _frame = frame;

    public override float Distance(Vector3 p)
    {
        Vector3 q = _frame.Local(p);
        float k0 = (q / radii).Length();
        float k1 = (q / (radii * radii)).Length();
        return k1 < 1e-6f ? -MathF.Min(radii.X, MathF.Min(radii.Y, radii.Z)) : k0 * (k0 - 1f) / k1;
    }

    public override (Vector2, float, float) Bounds
    {
        get
        {
            float r = MathF.Max(radii.X, MathF.Max(radii.Y, radii.Z));
            return (new Vector2(_frame.Origin.X, _frame.Origin.Y), r, _frame.Origin.Z + r);
        }
    }
}

/// <summary>Light, colour bands and line rules shared by every model.</summary>
public sealed record RenderStyle
{
    /// <summary>Direction towards the light in sprite space (x forward, y right, z up).</summary>
    public Vector3 Light { get; init; } = Vector3.Normalize(new Vector3(0.3f, -0.42f, 1f));

    /// <summary>Lit fraction above which a surface takes the light tone, and below which the shade tone.</summary>
    public float LightBand { get; init; } = 0.9f;
    public float ShadeBand { get; init; } = 0.55f;

    /// <summary>A part this much higher than its neighbour gets a dark contact line drawn on the lower one.</summary>
    public float ContactDepth { get; init; } = 2.2f;

    /// <summary>Silhouette colour; null leaves the silhouette without an outline.</summary>
    public Rgba? Ink { get; init; } = Rgba.Hex("#140c1c");

    /// <summary>Sub-samples per pixel side.</summary>
    public int Samples { get; init; } = 4;
}

/// <summary>A palette ramp per material: line, shade, main, light.</summary>
public sealed class Palette
{
    readonly Dictionary<Mat, Rgba[]> _ramps = new();

    public Palette Set(Mat material, Rgba main, float hueShift = 1f)
    {
        _ramps[material] = Ramp(main, hueShift);
        return this;
    }

    public Palette Set(Mat material, Rgba line, Rgba shade, Rgba main, Rgba light)
    {
        _ramps[material] = [line, shade, main, light];
        return this;
    }

    public Rgba Color(Mat material, int tone) =>
        _ramps.TryGetValue(material, out Rgba[]? ramp) ? ramp[tone] : _ramps.TryGetValue(Mat.Top, out ramp) ? ramp[tone] : Rgba.Hex("#ff00ff");

    public bool Has(Mat material) => _ramps.ContainsKey(material);

    /// <summary>
    /// Pixel-art ramp from one colour: darker tones lean towards blue-violet and lose some brightness, the
    /// light tone leans towards warm yellow (hue shifting instead of plain black and white mixing).
    /// </summary>
    public static Rgba[] Ramp(Rgba main, float hueShift = 1f)
    {
        var (h, s, v) = ToHsv(main);
        Rgba Tone(float dh, float ds, float dv) => FromHsv(ShiftHue(h, dh * hueShift), Math.Clamp(s * ds, 0f, 1f), Math.Clamp(v * dv, 0f, 1f));
        float lightV = v > 0.85f ? 1f : MathF.Min(1f, v * 1.22f + 0.06f);
        return
        [
            Tone(-0.06f, 1.15f, 0.42f),
            Tone(-0.025f, 1.08f, 0.72f),
            main,
            FromHsv(ShiftHue(h, 0.025f * hueShift), Math.Clamp(s * 0.82f, 0f, 1f), lightV),
        ];
    }

    /// <summary>Moves a hue towards blue (negative) or yellow (positive) by up to <paramref name="amount"/> of a turn.</summary>
    static float ShiftHue(float h, float amount)
    {
        if (amount == 0f) return h;
        float target = amount < 0f ? 0.67f : 0.14f;
        float d = target - h;
        if (d > 0.5f) d -= 1f;
        if (d < -0.5f) d += 1f;
        float step = MathF.Min(MathF.Abs(amount), MathF.Abs(d)) * MathF.Sign(d);
        return (h + step + 1f) % 1f;
    }

    static (float H, float S, float V) ToHsv(Rgba c)
    {
        float max = MathF.Max(c.R, MathF.Max(c.G, c.B)), min = MathF.Min(c.R, MathF.Min(c.G, c.B));
        float d = max - min, h = 0f;
        if (d > 1e-5f)
        {
            if (max == c.R) h = ((c.G - c.B) / d % 6f + 6f) % 6f;
            else if (max == c.G) h = (c.B - c.R) / d + 2f;
            else h = (c.R - c.G) / d + 4f;
            h /= 6f;
        }
        return (h, max <= 0f ? 0f : d / max, max);
    }

    static Rgba FromHsv(float h, float s, float v)
    {
        float c = v * s, x = c * (1f - MathF.Abs(h * 6f % 2f - 1f)), m = v - c;
        (float r, float g, float b) = (int)(h * 6f) switch
        {
            0 => (c, x, 0f), 1 => (x, c, 0f), 2 => (0f, c, x), 3 => (0f, x, c), 4 => (x, 0f, c), _ => (c, 0f, x),
        };
        return new Rgba(r + m, g + m, b + m);
    }
}

/// <summary>Surface materials; each look maps them to colour ramps.</summary>
public enum Mat : byte
{
    None, Skin, Hair, Top, Trim, Shirt, Pants, Belt, Shoes, Glove, Eye, Mouth, Dark, Metal, Fur, FurDark, Flesh, Bone, Claw, Glow,
}

/// <summary>
/// Renders a set of solid parts seen straight from above into crisp pixel art: one vertical ray per sub-sample,
/// banded lighting from the surface normal, every pixel snapped to one palette colour (no blending), dark
/// contact lines where a part lies over another and a one-pixel silhouette outline.
/// </summary>
public static class TopDownRenderer
{
    readonly record struct Hit(Mat Material, int Tone, float Depth, int Group);

    public static Canvas Render(IReadOnlyList<Prim> prims, Palette palette, int width, int height, Vector2 origin, RenderStyle? style = null,
        Func<Prim, bool>? include = null)
    {
        style ??= new RenderStyle();
        var parts = include is null ? prims : prims.Where(include).ToList();
        var canvas = new Canvas(width, height);
        if (parts.Count == 0) return canvas;

        var bounds = parts.Select(p => p.Bounds).ToArray();
        int n = style.Samples;
        var depth = new float[width * height];
        var group = new int[width * height];
        var mats = new Mat[width * height];
        var tones = new int[width * height];
        var opaque = new bool[width * height];
        var candidates = new List<int>(parts.Count);
        var counts = new Dictionary<(Mat, int), int>();

        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                counts.Clear();
                int hits = 0;
                float depthSum = 0f;
                var groups = new Dictionary<int, int>();
                for (int sy = 0; sy < n; sy++)
                {
                    for (int sx = 0; sx < n; sx++)
                    {
                        float x = px + (sx + 0.5f) / n - origin.X, y = py + (sy + 0.5f) / n - origin.Y;
                        candidates.Clear();
                        float top = float.NegativeInfinity;
                        for (int i = 0; i < parts.Count; i++)
                        {
                            var (c, r, t) = bounds[i];
                            if (Vector2.DistanceSquared(c, new Vector2(x, y)) <= (r + 1.5f) * (r + 1.5f))
                            {
                                candidates.Add(i);
                                top = MathF.Max(top, t);
                            }
                        }
                        if (candidates.Count == 0) continue;
                        if (Cast(parts, candidates, x, y, top + 1f, palette, style) is not { } hit) continue;
                        hits++;
                        depthSum += hit.Depth;
                        counts[(hit.Material, hit.Tone)] = counts.GetValueOrDefault((hit.Material, hit.Tone)) + 1;
                        groups[hit.Group] = groups.GetValueOrDefault(hit.Group) + 1;
                    }
                }

                if (hits * 2 < n * n) continue;
                int index = py * width + px;
                var best = counts.MaxBy(kv => kv.Value * 8 + kv.Key.Item2).Key;
                opaque[index] = true;
                mats[index] = best.Item1;
                tones[index] = best.Item2;
                depth[index] = depthSum / hits;
                group[index] = groups.MaxBy(kv => kv.Value).Key;
            }
        }

        // Contact lines: the lower pixel next to a clearly higher part of another group darkens.
        for (int py = 0; py < height; py++)
        {
            for (int px = 0; px < width; px++)
            {
                int i = py * width + px;
                if (!opaque[i]) continue;
                bool line = false;
                foreach (var (dx, dy) in Neighbours)
                {
                    int x = px + dx, y = py + dy;
                    if ((uint)x >= (uint)width || (uint)y >= (uint)height) continue;
                    int j = y * width + x;
                    if (opaque[j] && group[j] != group[i] && depth[j] > depth[i] + style.ContactDepth) line = true;
                }
                Rgba color = palette.Color(mats[i], line ? 0 : tones[i]);
                canvas[px, py] = color;
            }
        }

        if (style.Ink is { } ink) HardOutline(canvas, ink);
        return canvas;
    }

    static readonly (int, int)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Transparent pixels touching an opaque one (4-neighbourhood) take the ink colour.</summary>
    public static void HardOutline(Canvas canvas, Rgba ink)
    {
        var edge = new List<(int, int)>();
        for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
                if (canvas.AlphaAt(x, y) <= 0f && Neighbours.Any(n => canvas.AlphaAt(x + n.Item1, y + n.Item2) > 0.5f))
                    edge.Add((x, y));
        foreach (var (x, y) in edge) canvas[x, y] = ink;
    }

    static Hit? Cast(IReadOnlyList<Prim> parts, List<int> candidates, float x, float y, float top, Palette palette, RenderStyle style)
    {
        float z = top;
        for (int step = 0; step < 96 && z > -2f; step++)
        {
            var p = new Vector3(x, y, z);
            float d = Field(parts, candidates, p);
            if (d < 0.025f)
            {
                // Surface found: material of the closest part, banded light from the field gradient.
                int nearest = candidates[0];
                float best = float.PositiveInfinity;
                foreach (int i in candidates)
                {
                    float di = parts[i].Distance(p);
                    if (di < best) { best = di; nearest = i; }
                }
                Prim prim = parts[nearest];
                Vector3 normal = Gradient(parts, candidates, p);
                float lit = Vector3.Dot(normal, style.Light);
                Mat material = prim.MaterialAt(p);
                int tone = lit >= style.LightBand ? 3 : lit >= style.ShadeBand ? 2 : lit > -0.1f ? 1 : 0;
                if (material is Mat.Eye or Mat.Mouth or Mat.Dark) tone = Math.Min(tone, 1);
                if (material == Mat.Glow) tone = 3;
                return new Hit(material, tone, z, prim.Group);
            }
            z -= MathF.Max(d * 0.85f, 0.03f);
        }
        return null;
    }

    static float Field(IReadOnlyList<Prim> parts, List<int> candidates, Vector3 p)
    {
        // Smooth union inside a group, plain union between groups.
        float result = float.PositiveInfinity;
        Span<float> groupDistance = stackalloc float[16];
        groupDistance.Fill(float.PositiveInfinity);
        foreach (int i in candidates)
        {
            Prim prim = parts[i];
            float d = prim.Distance(p);
            int g = prim.Group & 15;
            groupDistance[g] = SmoothMin(groupDistance[g], d, BlendOf(prim.Group));
        }
        foreach (float d in groupDistance) result = MathF.Min(result, d);
        return result;
    }

    /// <summary>How far parts of a group melt together (groups 0-7 are soft bodies, 8 and up hard objects).</summary>
    static float BlendOf(int group) => group >= 8 ? 0.01f : 1.6f;

    static float SmoothMin(float a, float b, float k)
    {
        if (float.IsPositiveInfinity(a)) return b;
        float h = MathF.Max(k - MathF.Abs(a - b), 0f) / k;
        return MathF.Min(a, b) - h * h * k * 0.25f;
    }

    static Vector3 Gradient(IReadOnlyList<Prim> parts, List<int> candidates, Vector3 p)
    {
        const float e = 0.2f;
        float dx = Field(parts, candidates, p + new Vector3(e, 0, 0)) - Field(parts, candidates, p - new Vector3(e, 0, 0));
        float dy = Field(parts, candidates, p + new Vector3(0, e, 0)) - Field(parts, candidates, p - new Vector3(0, e, 0));
        float dz = Field(parts, candidates, p + new Vector3(0, 0, e)) - Field(parts, candidates, p - new Vector3(0, 0, e));
        var g = new Vector3(dx, dy, dz);
        return g.LengthSquared() < 1e-9f ? Vector3.UnitZ : Vector3.Normalize(g);
    }
}
