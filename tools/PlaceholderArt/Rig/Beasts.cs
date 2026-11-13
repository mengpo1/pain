using System.Numerics;

namespace PlaceholderArt.Rig;

/// <summary>Shape of a four-legged creature (pixels, for a body facing +X).</summary>
public sealed record BeastBuild
{
    public float Hip { get; init; } = 4.5f;
    public float Chest { get; init; } = 5.5f;
    public float Depth { get; init; } = 4f;
    public float Head { get; init; } = 3.6f;
    public float Snout { get; init; } = 3f;
    public float Arm { get; init; } = 6.5f;
    public float Forearm { get; init; } = 6.5f;
    public float Thigh { get; init; } = 6f;
    public float Shin { get; init; } = 6f;
    /// <summary>Hind legs standing on the toes (a third bone below the hock).</summary>
    public float Hock { get; init; }
    public float Limb { get; init; } = 2f;
    public int Ridge { get; init; } = 5;
    public int Tail { get; init; }
    public float TailLength { get; init; } = 3.5f;

    /// <summary>Materials of the eyes, the open mouth and the muzzle (hot spots for the thermal creatures).</summary>
    public Mat Eyes { get; init; } = Mat.Glow;
    public Mat Mouth { get; init; } = Mat.Glow;
    public Mat Muzzle { get; init; } = Mat.Flesh;

    /// <summary>Ear size (0 = none) and a nose at the tip of the muzzle (dogs).</summary>
    public float Ears { get; init; }
    public bool Nose { get; init; }

    /// <summary>Patches of the darker coat on the body.</summary>
    public bool Patches { get; init; } = true;

    /// <summary>Legs under the body (dogs): elbows bend back, knees forward, instead of sprawling sideways.</summary>
    public bool Upright { get; init; }
}

/// <summary>Body parts of a four-legged model (drawing subsets: a dead body torn to pieces).</summary>
public static class BeastPart
{
    public const int Head = 0, Torso = 1, FrontRight = 2, FrontLeft = 3, HindRight = 4, HindLeft = 5;
}

/// <summary>
/// A pose of a creature in sprite space: the spine runs from the pelvis to the chest, the head sits at the end
/// of the neck, hands and feet are targets (inverse kinematics), and a few extra numbers bend the rest.
/// </summary>
public sealed class BeastPose
{
    public Vector3 Pelvis = new(-5f, 0f, 7f), Chest = new(4f, 0f, 8f);
    public float Roll;
    public Vector3 Head = new(10f, 0f, 8.5f);
    public float HeadTurn, Jaw;
    public Vector3 HandRight = new(8f, 7f, 1.5f), HandLeft = new(8f, -7f, 1.5f), FootRight = new(-8f, 6f, 1.5f), FootLeft = new(-8f, -6f, 1.5f);
    public float TailLift, TailSwing;
    public float Breath;

    public BeastPose Copy() => (BeastPose)MemberwiseClone();
}

public sealed record BeastFrame(BeastPose Pose, float Duration, string? Event = null, float Heat = 1f, float Pool = 0f);

public sealed record BeastClip(string Name, bool Loop, List<BeastFrame> Frames);

/// <summary>Solid parts of a creature from its pose (same renderer and materials as the humans).</summary>
public static class BeastModel
{
    public static List<Prim> Build(BeastPose pose, BeastBuild b)
    {
        var prims = new List<Prim>();
        Vector3 spine = pose.Chest - pose.Pelvis;
        Vector3 fwd = Vector3.Normalize(spine);
        Vector3 up0 = Vector3.Normalize(Vector3.UnitZ - fwd * Vector3.Dot(Vector3.UnitZ, fwd));
        Vector3 right0 = Vector3.Cross(up0, fwd);
        var body = new Basis(pose.Pelvis, fwd, right0, up0).Roll(pose.Roll);
        Basis At(Vector3 origin) => body with { Origin = origin };

        // Torso: hips, belly and chest melt together; a ridge of vertebrae runs along the back.
        prims.Add(new Egg(At(pose.Pelvis), new Vector3(b.Hip, b.Hip * 0.95f, b.Depth * 0.9f)) { Group = 0, Part = BeastPart.Torso, Material = Mat.Flesh, Frame = At(pose.Pelvis), Paint = b.Patches ? Mottle : null });
        Vector3 mid = Vector3.Lerp(pose.Pelvis, pose.Chest, 0.5f) + body.Up * 0.5f;
        float waist = b.Upright ? MathF.Min(b.Hip, b.Chest) * 0.8f : (b.Hip + b.Chest) * 0.5f;
        prims.Add(new Egg(At(mid), new Vector3(spine.Length() * 0.45f + 1f, waist * (1f + pose.Breath * 0.08f), b.Depth)) { Group = 0, Part = BeastPart.Torso, Material = Mat.Flesh, Frame = At(mid), Paint = b.Patches ? Mottle : null });
        prims.Add(new Egg(At(pose.Chest), new Vector3(b.Chest * 0.8f, b.Chest * (1f + pose.Breath * 0.1f), b.Depth * 1.05f)) { Group = 0, Part = BeastPart.Torso, Material = Mat.Flesh, Frame = At(pose.Chest), Paint = b.Patches ? Mottle : null });
        for (int i = 0; i < b.Ridge; i++)
        {
            float t = (i + 0.5f) / b.Ridge;
            Vector3 p = Vector3.Lerp(pose.Pelvis, pose.Chest, t) + body.Up * (b.Depth * 0.95f);
            prims.Add(new Ball(p, 1.1f + 0.4f * MathF.Sin(t * MathF.PI)) { Group = 1, Part = BeastPart.Torso, Material = Mat.Bone });
        }

        // Neck and head: skull, snout, an opening jaw with a hot mouth, two hot eyes.
        Vector3 toHead = pose.Head - pose.Chest;
        Vector3 hf = Vector3.Normalize(new Vector3(toHead.X, toHead.Y, toHead.Z * 0.4f));
        var head = new Basis(pose.Head, hf, Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, hf)), Vector3.UnitZ).Turn(pose.HeadTurn);
        head = head with { Up = Vector3.Normalize(Vector3.Cross(head.Forward, head.Right)) };
        prims.Add(new Bone(pose.Chest + body.Up * 1f, pose.Head - head.Forward * b.Head * 0.5f, b.Limb * 1.3f, b.Limb * 1.1f) { Group = 0, Part = BeastPart.Head, Material = Mat.Flesh });
        prims.Add(new Egg(head, new Vector3(b.Head, b.Head * 0.8f, b.Head * 0.7f)) { Group = 2, Part = BeastPart.Head, Material = Mat.Flesh });
        Basis snout = head.Moved(b.Head * 0.8f, 0f, -0.4f).Lean(0.25f);
        prims.Add(new Egg(snout, new Vector3(b.Snout, b.Head * 0.55f, b.Head * 0.45f)) { Group = 2, Part = BeastPart.Head, Material = b.Muzzle });
        Basis jaw = head.Moved(b.Head * 0.5f, 0f, -b.Head * 0.45f).Lean(0.15f + pose.Jaw * 0.7f).Moved(b.Snout * 0.8f, 0f, 0f);
        prims.Add(new Egg(jaw, new Vector3(b.Snout * 0.95f, b.Head * 0.45f, b.Head * 0.3f)) { Group = 3, Part = BeastPart.Head, Material = b.Muzzle });
        if (pose.Jaw > 0.15f)
            prims.Add(new Ball(head.At(b.Head * 0.9f + b.Snout * 0.6f, 0f, -b.Head * 0.25f), b.Head * 0.35f * MathF.Min(1f, pose.Jaw * 1.5f)) { Group = 4, Part = BeastPart.Head, Material = b.Mouth });
        foreach (int side in new[] { -1, 1 })
            prims.Add(new Ball(head.At(b.Head * 0.55f, side * b.Head * 0.45f, b.Head * 0.45f), 0.8f) { Group = 4, Part = BeastPart.Head, Material = b.Eyes });
        if (b.Nose)
            prims.Add(new Ball(snout.At(b.Snout * 0.95f, 0f, b.Head * 0.12f), 1f) { Group = 4, Part = BeastPart.Head, Material = Mat.Dark });
        if (b.Ears > 0f)
            foreach (int side in new[] { -1, 1 })
            {
                // Floppy ears hanging off both sides of the skull, seen from above as two dark flaps.
                Basis ear = head.Moved(-b.Head * 0.2f, side * b.Head * 0.95f, b.Head * 0.25f).Turn(side * 0.9f).Roll(side * 0.5f);
                prims.Add(new Egg(ear, new Vector3(b.Ears, b.Ears * 0.6f, b.Ears * 0.3f)) { Group = 5 + (side > 0 ? 0 : 1), Part = BeastPart.Head, Material = Mat.Fur });
            }

        // Limbs.
        float width = b.Chest * (b.Upright ? 0.55f : 0.9f), hipWidth = b.Hip * (b.Upright ? 0.55f : 0.75f);
        float splay = b.Upright ? 0.45f : 1f;
        Vector3 front = b.Upright ? -body.Forward + body.Up * 0.2f : body.Up * 0.6f - body.Forward * 0.4f;
        Vector3 hind = b.Upright ? body.Forward : body.Forward;
        Limb(prims, body with { Origin = pose.Chest + body.Right * width }, pose.HandRight, b.Arm, b.Forearm, 0f, front + body.Right * splay, b, 5, true, BeastPart.FrontRight);
        Limb(prims, body with { Origin = pose.Chest - body.Right * width }, pose.HandLeft, b.Arm, b.Forearm, 0f, front - body.Right * splay, b, 6, true, BeastPart.FrontLeft);
        Limb(prims, body with { Origin = pose.Pelvis + body.Right * hipWidth }, pose.FootRight, b.Thigh, b.Shin, b.Hock, hind + body.Right * splay * 0.5f, b, 7, false, BeastPart.HindRight);
        Limb(prims, body with { Origin = pose.Pelvis - body.Right * hipWidth }, pose.FootLeft, b.Thigh, b.Shin, b.Hock, hind - body.Right * splay * 0.5f, b, 7, false, BeastPart.HindLeft);

        // Tail: a tapering chain that lifts and sways.
        if (b.Tail > 0)
        {
            Vector3 p = pose.Pelvis - body.Forward * b.Hip * 0.8f;
            Vector3 dir = -body.Forward;
            for (int i = 0; i < b.Tail; i++)
            {
                float t = i / (float)b.Tail;
                var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, pose.TailSwing * (0.4f + t)) * Quaternion.CreateFromAxisAngle(body.Right, -pose.TailLift * 0.35f);
                dir = Vector3.Normalize(Vector3.Transform(dir, q));
                Vector3 next = p + dir * b.TailLength;
                prims.Add(new Bone(p, next, b.Limb * (1f - t * 0.6f), b.Limb * (1f - (t + 1f / b.Tail) * 0.6f)) { Group = 1, Part = BeastPart.Torso, Material = Mat.Flesh });
                p = next;
            }
        }
        return prims;
    }

    static Mat Mottle(Vector3 p) => Noise.Value(p.X * 0.45f + 40f, p.Y * 0.45f + p.Z * 0.3f + 40f, 64, 64, 917) > 0.62f ? Mat.Fur : Mat.Flesh;

    static void Limb(List<Prim> prims, Basis root, Vector3 target, float a, float c, float hock, Vector3 pole, BeastBuild b, int group, bool claws, int part)
    {
        Vector3 end = target;
        Vector3 hockPoint = target;
        if (hock > 0f)
        {
            // Digitigrade: the hock sits behind and above the toes.
            hockPoint = target - root.Forward * hock * 0.55f + Vector3.UnitZ * hock * 0.8f;
            end = hockPoint;
        }
        (Vector3 knee, Vector3 foot) = HumanModel.Reach(root.Origin, end, a, c, Vector3.Normalize(pole));
        // Limbs cool towards the extremities (the thermal camera sees the shape of each segment).
        prims.Add(new Bone(root.Origin, knee, b.Limb * 1.25f, b.Limb) { Group = group, Part = part, Material = Mat.Flesh });
        prims.Add(new Bone(knee, foot, b.Limb, b.Limb * 0.8f) { Group = group, Part = part, Material = Mat.Fur });
        if (hock > 0f) prims.Add(new Bone(foot, target, b.Limb * 0.8f, b.Limb * 0.6f) { Group = group, Part = part, Material = Mat.Bone });
        Vector3 tip = hock > 0f ? target : foot;
        Vector3 along = Vector3.Normalize(new Vector3(tip.X - knee.X, tip.Y - knee.Y, 0f) + root.Forward * 0.5f);
        prims.Add(new Ball(tip, b.Limb * 0.95f) { Group = group, Part = part, Material = Mat.Bone });
        if (claws)
            foreach (float spread in new[] { -0.45f, 0f, 0.45f })
            {
                Vector3 d = Vector3.Transform(along, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, spread));
                prims.Add(new Bone(tip, tip + d * 2.6f * (b.Nose ? 0.4f : 1f), 0.55f, 0.25f) { Group = group, Part = part, Material = Mat.Claw });
            }
    }
}

/// <summary>The creature animations of the NoctPrototype pack.</summary>
public static class CreatureClips
{
    const float Pi = MathF.PI;

    static BeastPose B(Action<BeastPose> edit)
    {
        var p = new BeastPose();
        edit(p);
        return p;
    }

    static BeastPose With(this BeastPose pose, Action<BeastPose> edit)
    {
        BeastPose copy = pose.Copy();
        edit(copy);
        return copy;
    }

    static Vector3 V(float x, float y, float z) => new(x, y, z);

    /// <summary>Diagonal-pair gait: the left hand moves with the right foot. <paramref name="stride"/> is the reach of a step.</summary>
    static BeastPose Gait(BeastPose stance, float phase, float stride, float lift, float bob, float sway)
    {
        float s = MathF.Sin(phase), c = MathF.Cos(phase);
        return stance.With(p =>
        {
            p.HandLeft += V(stride * s, 0f, lift * MathF.Max(0f, c));
            p.FootRight += V(stride * s, 0f, lift * MathF.Max(0f, c));
            p.HandRight += V(-stride * s, 0f, lift * MathF.Max(0f, -c));
            p.FootLeft += V(-stride * s, 0f, lift * MathF.Max(0f, -c));
            p.Chest += V(0f, sway * s, bob * MathF.Abs(s));
            p.Pelvis += V(0f, -sway * s, bob * MathF.Abs(c));
            p.Head += V(0f, sway * 1.4f * s, bob * MathF.Abs(s));
            p.TailSwing = 0.35f * s;
        });
    }

    static List<BeastFrame> Cycle(BeastPose stance, int frames, float duration, float stride, float lift, float bob, float sway, bool steps = true)
    {
        var list = new List<BeastFrame>();
        for (int i = 0; i < frames; i++)
            list.Add(new BeastFrame(Gait(stance, i * 2f * Pi / frames, stride, lift, bob, sway), duration / frames, steps && i % (frames / 2) == frames / 4 ? "footstep" : null));
        return list;
    }

    /// <summary>Dies on its side and cools: the last frames lose heat.</summary>
    static IEnumerable<BeastFrame> Cooling(BeastPose dead) =>
    [
        new BeastFrame(dead, 0.3f, Heat: 0.85f, Pool: 0.4f),
        new BeastFrame(dead, 0.6f, Heat: 0.7f, Pool: 0.7f),
        new BeastFrame(dead, 1.2f, Heat: 0.55f, Pool: 0.9f),
        new BeastFrame(dead, 1f, Heat: 0.45f, Pool: 1f),
    ];

    // ------------------------------------------------------------------ scavenger: low, hunched, on all fours

    public static readonly BeastBuild ScavengerBuild = new()
    {
        Hip = 4.4f, Chest = 5.8f, Depth = 4.2f, Head = 3.8f, Snout = 3.2f, Arm = 7f, Forearm = 7f, Thigh = 6f, Shin = 6f, Limb = 2.1f, Ridge = 6,
    };

    static readonly BeastPose ScavengerStance = B(p =>
    {
        p.Pelvis = V(-6f, 0f, 7.5f); p.Chest = V(3f, 0f, 9f); p.Head = V(10.5f, 0f, 8f);
        p.HandRight = V(8f, 8f, 1.4f); p.HandLeft = V(8f, -8f, 1.4f); p.FootRight = V(-9f, 6.5f, 1.4f); p.FootLeft = V(-9f, -6.5f, 1.4f);
    });

    public static IEnumerable<BeastClip> Scavenger()
    {
        BeastPose s = ScavengerStance;
        yield return new BeastClip("idle", true,
        [
            new BeastFrame(s, 0.4f), new BeastFrame(s.With(p => { p.Breath = 1f; p.Head += V(0.5f, 1f, -0.5f); p.HeadTurn = 0.25f; }), 0.35f),
            new BeastFrame(s.With(p => { p.Head += V(1f, -0.5f, -1f); p.HeadTurn = -0.3f; p.Jaw = 0.25f; }), 0.3f), new BeastFrame(s.With(p => p.Breath = 1f), 0.35f),
        ]);
        yield return new BeastClip("walk", true, Cycle(s.With(p => { p.Head += V(1f, 0f, -0.8f); p.Chest += V(0.5f, 0f, -0.5f); }), 8, 1f, 5.5f, 2.2f, 0.7f, 0.9f));
        yield return new BeastClip("rest", true,
        [
            new BeastFrame(s.With(p => { p.Pelvis = V(-6f, 0f, 4f); p.Chest = V(3f, 0f, 4.5f); p.Head = V(10f, 2f, 3f); p.HeadTurn = 0.4f; }), 0.9f, Heat: 0.8f),
            new BeastFrame(s.With(p => { p.Pelvis = V(-6f, 0f, 4.3f); p.Chest = V(3f, 0f, 5f); p.Head = V(10f, 2f, 3f); p.HeadTurn = 0.4f; p.Breath = 1f; }), 0.9f, Heat: 0.82f),
        ]);
        // Lunging bite: rear up a little, drop the head, snap forward with the jaws wide.
        yield return new BeastClip("attack", false,
        [
            new BeastFrame(s.With(p => { p.Pelvis += V(-1.5f, 0f, -1f); p.Chest += V(-2f, 0f, 2f); p.Head = V(7f, 0f, 11f); p.Jaw = 0.4f; p.HandRight += V(-2f, 1f, 2f); p.HandLeft += V(-2f, -1f, 2f); }), 0.1f),
            new BeastFrame(s.With(p => { p.Chest += V(-1f, 0f, 1.5f); p.Head = V(8f, 0f, 10f); p.Jaw = 0.8f; p.HandRight += V(-1f, 1.5f, 3f); p.HandLeft += V(-1f, -1.5f, 3f); }), 0.1f),
            new BeastFrame(s.With(p => { p.Pelvis += V(2f, 0f, 0f); p.Chest += V(4f, 0f, -1f); p.Head = V(17f, 0f, 6f); p.Jaw = 1f; p.HandRight += V(5f, 2f, 0f); p.HandLeft += V(5f, -2f, 0f); }), 0.09f, "impact"),
            new BeastFrame(s.With(p => { p.Pelvis += V(1f, 0f, 0f); p.Chest += V(2f, 0f, 0f); p.Head = V(14f, 1f, 6.5f); p.Jaw = 0.3f; p.HeadTurn = 0.3f; p.HandRight += V(3f, 1f, 0f); p.HandLeft += V(3f, -1f, 0f); }), 0.1f),
            new BeastFrame(s, 0.1f),
        ]);
        yield return new BeastClip("hit", false,
        [
            new BeastFrame(s.With(p => { p.Pelvis += V(-2f, 0f, 1f); p.Chest += V(-3f, 1f, 2.5f); p.Head = V(5f, 3f, 12f); p.HeadTurn = 0.8f; p.Jaw = 0.9f; p.Roll = 0.3f; }), 0.08f),
            new BeastFrame(s.With(p => { p.Pelvis += V(-1f, 0f, 0f); p.Chest += V(-1.5f, 0.5f, 1f); p.Head = V(8f, 1.5f, 9f); p.HeadTurn = 0.4f; p.Jaw = 0.4f; p.Roll = 0.15f; }), 0.12f),
            new BeastFrame(s, 0.2f),
        ]);
        BeastPose down = s.With(p =>
        {
            p.Roll = 1.25f; p.Pelvis = V(-6f, 2f, 4f); p.Chest = V(3f, 2.5f, 4.5f); p.Head = V(10f, 5f, 3f); p.HeadTurn = 0.6f; p.Jaw = 0.5f;
            p.HandRight = V(6f, 12f, 1.5f); p.HandLeft = V(9f, 9f, 1.5f); p.FootRight = V(-10f, 11f, 1.5f); p.FootLeft = V(-12f, 6f, 1.5f);
        });
        yield return new BeastClip("knockdown", false, [new BeastFrame(s.With(p => { p.Roll = 0.6f; p.Chest += V(-1f, 1f, 0f); }), 0.07f), new BeastFrame(down, 0.3f)]);
        yield return new BeastClip("getup", false, [new BeastFrame(s.With(p => { p.Roll = 0.6f; p.Pelvis += V(0f, 1f, -2f); p.Chest += V(0f, 1f, -2f); }), 0.25f), new BeastFrame(s, 0.35f)]);
        yield return new BeastClip("death", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-2f, 0f, 3f); p.Head = V(6f, 2f, 13f); p.Jaw = 1f; p.HeadTurn = 0.9f; }), 0.12f),
            new BeastFrame(s.With(p => { p.Roll = 0.7f; p.Pelvis += V(0f, 1f, -2f); p.Chest += V(0f, 1.5f, -2f); p.Head = V(9f, 4f, 5f); p.Jaw = 0.8f; }), 0.12f),
            new BeastFrame(down, 0.12f, "thud"),
            ..Cooling(down),
        ]);
    }

    // ------------------------------------------------------------------ leaper: lean, long hind legs, a tail

    public static readonly BeastBuild LeaperBuild = new()
    {
        Hip = 3.6f, Chest = 4.2f, Depth = 3.2f, Head = 2.9f, Snout = 3f, Arm = 5.5f, Forearm = 5.5f, Thigh = 6.5f, Shin = 6.5f, Hock = 4f,
        Limb = 1.6f, Ridge = 4, Tail = 6, TailLength = 3f,
    };

    static readonly BeastPose LeaperStance = B(p =>
    {
        p.Pelvis = V(-5f, 0f, 9f); p.Chest = V(3f, 0f, 7.5f); p.Head = V(9f, 0f, 8f);
        p.HandRight = V(7f, 5.5f, 1.2f); p.HandLeft = V(7f, -5.5f, 1.2f); p.FootRight = V(-6f, 5f, 1.2f); p.FootLeft = V(-6f, -5f, 1.2f);
        p.TailLift = 0.6f;
    });

    public static IEnumerable<BeastClip> Leaper()
    {
        BeastPose s = LeaperStance;
        yield return new BeastClip("idle", true,
        [
            new BeastFrame(s, 0.25f), new BeastFrame(s.With(p => { p.HeadTurn = 0.5f; p.Head += V(0f, 1f, 0.5f); p.TailSwing = 0.3f; }), 0.15f),
            new BeastFrame(s.With(p => { p.HeadTurn = 0.45f; p.Head += V(0f, 1f, 0.5f); p.TailSwing = 0.4f; p.Breath = 1f; }), 0.3f),
            new BeastFrame(s.With(p => { p.HeadTurn = -0.5f; p.Head += V(0f, -1f, 0.5f); p.TailSwing = -0.3f; }), 0.15f),
        ]);
        // Bounding run: hind legs push together, then the forelegs land together.
        var run = new List<BeastFrame>();
        for (int i = 0; i < 8; i++)
        {
            float ph = i * 2f * Pi / 8f, sn = MathF.Sin(ph), cs = MathF.Cos(ph);
            run.Add(new BeastFrame(s.With(p =>
            {
                p.HandRight += V(4.5f * sn, 0.5f, 2f * MathF.Max(0f, cs)); p.HandLeft += V(4.5f * sn - 1f, -0.5f, 2f * MathF.Max(0f, cs));
                p.FootRight += V(-5f * sn, 0f, 2f * MathF.Max(0f, -cs)); p.FootLeft += V(-5f * sn + 1f, 0f, 2f * MathF.Max(0f, -cs));
                p.Chest += V(1.5f * sn, 0f, 1f * cs); p.Pelvis += V(-1.5f * sn, 0f, -1f * cs); p.Head += V(1.5f * sn, 0f, 0.6f * cs);
                p.TailLift = 0.3f + 0.3f * cs; p.TailSwing = 0.15f * sn;
            }), 1f / 8f, i % 4 == 1 ? "footstep" : null));
        }
        yield return new BeastClip("walk", true, run);
        // Crouch: folds down, head low and aimed, tail up and quivering (wind-up of the leap).
        BeastPose low = s.With(p =>
        {
            p.Pelvis = V(-6.5f, 0f, 6f); p.Chest = V(2f, 0f, 3.6f); p.Head = V(8.5f, 0f, 3.2f); p.Jaw = 0.35f;
            p.HandRight = V(6f, 7f, 1.2f); p.HandLeft = V(6f, -7f, 1.2f); p.FootRight = V(-4f, 6f, 1.2f); p.FootLeft = V(-4f, -6f, 1.2f); p.TailLift = 1.6f;
        });
        yield return new BeastClip("leap.crouch", false,
        [
            new BeastFrame(s.With(p => { p.Pelvis += V(-0.5f, 0f, -1.5f); p.Chest += V(-0.5f, 0f, -2f); p.Head += V(-0.5f, 0f, -2.5f); p.TailLift = 1f; }), 0.08f),
            new BeastFrame(low, 0.1f),
            new BeastFrame(low.With(p => { p.TailSwing = 0.25f; p.Chest += V(0f, 0.4f, 0f); p.Jaw = 0.6f; }), 0.06f),
            new BeastFrame(low.With(p => { p.TailSwing = -0.25f; p.Chest += V(0f, -0.4f, 0f); p.Jaw = 0.4f; }), 0.06f),
            new BeastFrame(low.With(p => { p.TailSwing = 0.25f; p.Chest += V(0f, 0.4f, -0.3f); p.Jaw = 0.7f; }), 0.06f),
            new BeastFrame(low.With(p => { p.TailSwing = -0.25f; p.Chest += V(0f, -0.4f, -0.3f); p.Jaw = 0.5f; }), 0.06f),
            new BeastFrame(low.With(p => { p.Pelvis += V(0f, 0f, -0.6f); p.Jaw = 0.8f; }), 0.1f),
        ]);
        // In the air: fully stretched, forelegs reaching, hind legs trailing, then reaching down to land.
        BeastPose stretched = s.With(p =>
        {
            p.Pelvis = V(-7f, 0f, 10f); p.Chest = V(3.5f, 0f, 11f); p.Head = V(10.5f, 0f, 11.5f); p.Jaw = 0.9f;
            p.HandRight = V(15f, 4f, 9f); p.HandLeft = V(15f, -4f, 9f); p.FootRight = V(-17f, 3f, 8f); p.FootLeft = V(-17f, -3f, 8f); p.TailLift = -0.2f;
        });
        yield return new BeastClip("leap.air", false,
        [
            new BeastFrame(s.With(p => { p.Pelvis = V(-6f, 0f, 9f); p.Chest = V(3f, 0f, 9.5f); p.Head = V(9.5f, 0f, 10f); p.Jaw = 0.6f; p.HandRight = V(11f, 5f, 6f); p.HandLeft = V(11f, -5f, 6f); p.FootRight = V(-13f, 4f, 3f); p.FootLeft = V(-13f, -4f, 3f); }), 0.06f),
            new BeastFrame(stretched, 0.2f),
            new BeastFrame(stretched.With(p => { p.HandRight = V(13f, 6f, 4f); p.HandLeft = V(13f, -6f, 4f); p.FootRight = V(-12f, 4f, 6f); p.FootLeft = V(-12f, -4f, 6f); p.Chest += V(0f, 0f, -2f); }), 0.2f),
        ]);
        // Landing: forelegs splayed wide, chest slammed low, then it gathers itself up again.
        yield return new BeastClip("leap.land", false,
        [
            new BeastFrame(s.With(p => { p.Pelvis = V(-5f, 0f, 6f); p.Chest = V(3.5f, 0f, 3f); p.Head = V(10f, 0f, 2.6f); p.Jaw = 0.5f; p.HandRight = V(9f, 9.5f, 1.2f); p.HandLeft = V(9f, -9.5f, 1.2f); p.FootRight = V(-4f, 7f, 1.2f); p.FootLeft = V(-4f, -7f, 1.2f); p.TailLift = 1.2f; }), 0.1f, "thud"),
            new BeastFrame(s.With(p => { p.Pelvis = V(-5.5f, 0f, 6.5f); p.Chest = V(2.5f, 0f, 4f); p.Head = V(9f, 1f, 3.5f); p.HeadTurn = 0.3f; p.HandRight = V(8f, 8.5f, 1.2f); p.HandLeft = V(8f, -8.5f, 1.2f); p.FootRight = V(-5f, 6f, 1.2f); p.FootLeft = V(-5f, -6f, 1.2f); }), 0.25f),
            new BeastFrame(s.With(p => { p.Pelvis += V(0f, 0f, -1.5f); p.Chest += V(0f, 0f, -2f); p.Head += V(0f, 0f, -2f); p.HeadTurn = -0.3f; p.Breath = 1f; }), 0.25f),
            new BeastFrame(s, 0.2f),
        ]);
        yield return new BeastClip("attack", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-1f, 0f, 2f); p.Head += V(-1f, 0f, 2f); p.HandRight = V(4f, 7f, 7f); p.Jaw = 0.5f; }), 0.08f),
            new BeastFrame(s.With(p => { p.Chest += V(2f, 0f, 0f); p.Head += V(2f, 0f, 0f); p.HandRight = V(14f, -1f, 3f); p.Jaw = 1f; }), 0.08f, "impact"),
            new BeastFrame(s.With(p => { p.Chest += V(1f, 0f, 0f); p.HandRight = V(10f, -5f, 2f); p.Jaw = 0.4f; }), 0.08f),
            new BeastFrame(s, 0.08f),
        ]);
        yield return new BeastClip("hit", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-2.5f, 1.5f, 2f); p.Head += V(-3f, 3f, 3f); p.HeadTurn = 0.9f; p.Jaw = 1f; p.Roll = 0.35f; p.TailSwing = 0.6f; }), 0.08f),
            new BeastFrame(s.With(p => { p.Chest += V(-1f, 0.5f, 1f); p.Head += V(-1f, 1f, 1f); p.HeadTurn = 0.4f; p.TailSwing = -0.3f; }), 0.12f),
            new BeastFrame(s, 0.15f),
        ]);
        BeastPose dead = s.With(p =>
        {
            p.Roll = 1.35f; p.Pelvis = V(-5f, 1.5f, 3.5f); p.Chest = V(3f, 2f, 3.5f); p.Head = V(9f, 5f, 2.5f); p.HeadTurn = 0.8f; p.Jaw = 0.7f;
            p.HandRight = V(7f, 10f, 1.2f); p.HandLeft = V(9f, 7f, 1.2f); p.FootRight = V(-9f, 10f, 1.2f); p.FootLeft = V(-12f, 6f, 1.2f); p.TailLift = -0.5f; p.TailSwing = 0.8f;
        });
        yield return new BeastClip("death", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-2f, 1f, 3f); p.Head += V(-3f, 3f, 4f); p.Jaw = 1f; p.Roll = 0.4f; }), 0.1f),
            new BeastFrame(s.With(p => { p.Roll = 0.9f; p.Pelvis += V(0f, 1f, -3f); p.Chest += V(0f, 1.5f, -3f); p.Head += V(0f, 3f, -4f); p.Jaw = 0.8f; }), 0.1f),
            new BeastFrame(dead, 0.12f, "thud"),
            ..Cooling(dead),
        ]);
    }

    // ------------------------------------------------------------------ dog (action pack): trots, lunges, bites

    public static readonly BeastBuild DogBuild = new()
    {
        Hip = 4.1f, Chest = 4.9f, Depth = 3.9f, Head = 3.8f, Snout = 3.4f, Arm = 4.6f, Forearm = 4.6f, Thigh = 4.8f, Shin = 4.8f,
        Limb = 1.6f, Ridge = 0, Tail = 4, TailLength = 2.5f, Eyes = Mat.Dark, Mouth = Mat.Mouth, Muzzle = Mat.Skin, Ears = 3.1f, Nose = true,
        Patches = false, Upright = true,
    };

    static readonly BeastPose DogStance = B(p =>
    {
        p.Pelvis = V(-5.5f, 0f, 9f); p.Chest = V(4f, 0f, 9.5f); p.Head = V(10f, 0f, 11.5f);
        p.HandRight = V(5.5f, 4.8f, 1.2f); p.HandLeft = V(5.5f, -4.8f, 1.2f); p.FootRight = V(-6.5f, 4.5f, 1.2f); p.FootLeft = V(-6.5f, -4.5f, 1.2f);
        p.TailLift = 0.8f;
    });

    /// <summary>Dead (or knocked out) on its side: legs splayed, head down, tail limp. Torn pieces are cut from this pose.</summary>
    public static readonly BeastPose DogDead = B(p =>
    {
        p.Roll = 1.3f; p.Pelvis = V(-5f, 1.5f, 3.4f); p.Chest = V(4f, 2f, 3.6f); p.Head = V(10.5f, 3f, 3f); p.HeadTurn = 0.3f; p.Jaw = 0.3f;
        p.HandRight = V(7f, 10f, 1.2f); p.HandLeft = V(9.5f, 7.5f, 1.2f); p.FootRight = V(-7f, 10f, 1.2f); p.FootLeft = V(-10.5f, 7f, 1.2f);
        p.TailLift = -0.6f; p.TailSwing = 0.4f;
    });

    public static IEnumerable<BeastClip> Dog()
    {
        BeastPose s = DogStance;
        yield return new BeastClip("idle", true,
        [
            new BeastFrame(s.With(p => p.TailSwing = 0.35f), 0.18f), new BeastFrame(s.With(p => { p.TailSwing = -0.35f; p.Breath = 1f; }), 0.18f),
            new BeastFrame(s.With(p => { p.TailSwing = 0.35f; p.HeadTurn = 0.35f; p.Head += V(0f, 1f, 0f); }), 0.22f),
            new BeastFrame(s.With(p => { p.TailSwing = -0.35f; p.HeadTurn = 0.3f; p.Head += V(0f, 1f, 0f); p.Jaw = 0.35f; }), 0.22f),
        ]);
        // Gallop: hind legs push together, the front legs reach together.
        var run = new List<BeastFrame>();
        for (int i = 0; i < 8; i++)
        {
            float ph = i * 2f * Pi / 8f, sn = MathF.Sin(ph), cs = MathF.Cos(ph);
            run.Add(new BeastFrame(s.With(p =>
            {
                p.HandRight += V(4.2f * sn, 0.3f, 1.8f * MathF.Max(0f, cs)); p.HandLeft += V(4.2f * sn - 1f, -0.3f, 1.8f * MathF.Max(0f, cs));
                p.FootRight += V(-4.2f * sn, 0f, 1.8f * MathF.Max(0f, -cs)); p.FootLeft += V(-4.2f * sn + 1f, 0f, 1.8f * MathF.Max(0f, -cs));
                p.Chest += V(1.2f * sn, 0f, 0.8f * cs); p.Pelvis += V(-1.2f * sn, 0f, -0.8f * cs); p.Head += V(1.2f * sn, 0f, 0.6f * cs);
                p.TailLift = 0.5f + 0.3f * cs; p.TailSwing = 0.2f * sn; p.Jaw = 0.25f;
            }), 1f / 8f, i % 4 == 1 ? "footstep" : null));
        }
        yield return new BeastClip("walk", true, run);
        // Lunging bite: gathers itself, springs forward jaws open, snaps shut, shakes.
        yield return new BeastClip("attack", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-1.5f, 0f, -1.5f); p.Pelvis += V(-1f, 0f, -1.5f); p.Head = V(8f, 0f, 8.5f); p.Jaw = 0.5f; }), 0.05f),
            new BeastFrame(s.With(p => { p.Chest += V(3f, 0f, 0.5f); p.Pelvis += V(1.5f, 0f, 0f); p.Head = V(15f, 0f, 9f); p.Jaw = 1f; p.HandRight += V(4f, 0f, 2f); p.HandLeft += V(4f, 0f, 2f); }), 0.04f),
            new BeastFrame(s.With(p => { p.Chest += V(3f, 0f, 0f); p.Pelvis += V(1.5f, 0f, 0f); p.Head = V(15.5f, 0f, 8f); p.Jaw = 0.15f; p.HandRight += V(3f, 0f, 0f); p.HandLeft += V(3f, 0f, 0f); }), 0.06f, "impact"),
            new BeastFrame(s.With(p => { p.Chest += V(2f, 0f, 0f); p.Head = V(14f, 1.5f, 8.5f); p.HeadTurn = 0.45f; p.Jaw = 0.15f; }), 0.05f),
            new BeastFrame(s.With(p => { p.Chest += V(1f, 0f, 0f); p.Head = V(12f, -1f, 10f); p.HeadTurn = -0.35f; p.Jaw = 0.3f; }), 0.06f),
        ]);
        yield return new BeastClip("hit", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-2f, 1f, 1.5f); p.Head = V(7f, 2.5f, 14f); p.HeadTurn = 0.7f; p.Jaw = 0.9f; p.Roll = 0.3f; p.TailLift = -0.4f; }), 0.08f),
            new BeastFrame(s.With(p => { p.Chest += V(-1f, 0.5f, 0.5f); p.Head = V(8.5f, 1f, 12f); p.HeadTurn = 0.3f; p.TailLift = 0f; }), 0.12f),
            new BeastFrame(s, 0.2f),
        ]);
        BeastPose dead = DogDead;
        yield return new BeastClip("knockdown", false,
        [
            new BeastFrame(s.With(p => { p.Roll = 0.6f; p.Chest += V(-1f, 1f, -2f); p.Head += V(-1f, 2f, -2f); p.Jaw = 0.8f; }), 0.07f),
            new BeastFrame(dead.With(p => p.Jaw = 0.6f), 0.08f, "thud"),
            new BeastFrame(dead.With(p => { p.HandRight += V(1f, 0f, 0f); p.FootRight += V(-1f, 0f, 0f); }), 0.15f),
            new BeastFrame(dead, 0.3f),
        ]);
        yield return new BeastClip("getup", false,
        [
            new BeastFrame(dead.With(p => { p.Roll = 0.9f; p.Head += V(0f, -1f, 2f); }), 0.15f),
            new BeastFrame(s.With(p => { p.Roll = 0.4f; p.Pelvis += V(0f, 1f, -3f); p.Chest += V(0f, 1f, -2f); }), 0.15f),
            new BeastFrame(s, 0.2f),
        ]);
        yield return new BeastClip("death", false,
        [
            new BeastFrame(s.With(p => { p.Chest += V(-2f, 1f, 2f); p.Head = V(6f, 2f, 15f); p.Jaw = 1f; p.HeadTurn = 0.8f; p.TailLift = -0.5f; }), 0.1f),
            new BeastFrame(s.With(p => { p.Roll = 0.8f; p.Pelvis += V(0f, 1f, -3.5f); p.Chest += V(0f, 1.5f, -3.5f); p.Head = V(9f, 3f, 6f); p.Jaw = 0.8f; }), 0.1f),
            new BeastFrame(dead, 0.12f, "thud"),
            new BeastFrame(dead.With(p => { p.HandRight += V(1.2f, 0f, 0f); p.FootLeft += V(-1f, -0.5f, 0f); }), 0.15f, Pool: 0.35f),
            new BeastFrame(dead, 0.3f, Pool: 0.65f),
            new BeastFrame(dead, 0.5f, Pool: 0.88f),
            new BeastFrame(dead, 0.5f, Pool: 1f),
        ]);
        // Under an execution: paddles, jerks at each blow, goes limp.
        yield return new BeastClip("executed", false,
        [
            new BeastFrame(dead.With(p => { p.HandRight += V(2f, 0f, 1f); p.FootRight += V(-2f, 0f, 1f); p.Jaw = 0.7f; p.Head += V(0f, 0f, 1f); }), 0.1f),
            new BeastFrame(dead.With(p => { p.HandLeft += V(2f, 0f, 1f); p.FootLeft += V(-2f, 0f, 1f); p.Jaw = 0.5f; }), 0.1f),
            new BeastFrame(dead.With(p => { p.Head += V(0.5f, -1.5f, 0f); p.HeadTurn = -0.3f; p.Jaw = 1f; p.HandRight += V(2.5f, 1f, 0f); p.FootRight += V(-2.5f, 1f, 0f); p.TailSwing = -0.3f; }), 0.06f, "impact"),
            new BeastFrame(dead.With(p => { p.HandRight += V(1f, 0f, 0f); p.FootRight += V(-1f, 0f, 0f); p.Jaw = 0.4f; }), 0.08f),
            new BeastFrame(dead, 0.3f),
        ]);
        // Finishing a body on the floor: the head plunges onto the victim's head (11 px ahead), bites and shakes.
        yield return new BeastClip("execute.bite", false,
        [
            new BeastFrame(s.With(p => { p.Head = V(9f, 0f, 13f); p.Jaw = 0.9f; p.Chest += V(-1f, 0f, 1f); }), 0.06f),
            new BeastFrame(s.With(p => { p.Head = V(12f, 0f, 7f); p.Jaw = 1f; p.Chest += V(1f, 0f, -1.5f); p.HandRight += V(1.5f, 0f, 0f); p.HandLeft += V(1.5f, 0f, 0f); }), 0.04f),
            new BeastFrame(s.With(p => { p.Head = V(12.5f, 0f, 4.5f); p.Jaw = 0.1f; p.Chest += V(1.5f, 0f, -2.5f); p.Pelvis += V(0f, 0f, 1f); p.HandRight += V(2f, 0f, 0f); p.HandLeft += V(2f, 0f, 0f); }), 0.08f, "impact"),
            new BeastFrame(s.With(p => { p.Head = V(12f, 1.8f, 5f); p.HeadTurn = 0.5f; p.Jaw = 0.1f; p.Chest += V(1f, 0f, -2f); p.TailSwing = 0.4f; }), 0.06f),
            new BeastFrame(s.With(p => { p.Head = V(12f, -1.8f, 5f); p.HeadTurn = -0.5f; p.Jaw = 0.1f; p.Chest += V(1f, 0f, -2f); p.TailSwing = -0.4f; }), 0.06f),
            new BeastFrame(s.With(p => { p.Head = V(10f, 0f, 10f); p.Jaw = 0.6f; }), 0.2f),
        ]);
    }

    // ------------------------------------------------------------------ watcher: tall, thin, very long arms (human rig)

    public static HumanBuild WatcherBuild(Palette palette) => new()
    {
        Palette = palette, Hair = HairStyle.Bald, Featureless = true, BareArms = true, Arms = 1.55f, Legs = 1.25f, Head = 0.82f, Bulk = 0.78f, Scale = 1.05f,
    };

    static HumanPose H(Action<HumanPose> edit)
    {
        var p = new HumanPose();
        edit(p);
        return p;
    }

    static HumanPose HW(this HumanPose pose, Action<HumanPose> edit)
    {
        HumanPose copy = pose.Copy();
        edit(copy);
        return copy;
    }

    /// <summary>Hunched forward, arms hanging long in front.</summary>
    static readonly HumanPose WatcherStance = H(p =>
    {
        p.Pelvis = V(-1f, 0f, 20f); p.Bend = 0.75f; p.HeadNod = -0.5f;
        p.HandRight = V(12f, 7.5f, -12f); p.HandLeft = V(12f, -7.5f, -12f);
        p.FootRight = V(0.5f, 4f, -18.2f); p.FootLeft = V(0.5f, -4f, -18.2f);
        p.OpenRight = p.OpenLeft = true;
    });

    /// <summary>Curled up on the floor, almost cold.</summary>
    static readonly HumanPose WatcherCurled = H(p =>
    {
        p.Pelvis = V(-3f, 0f, 5.5f); p.Lean = 1.2f; p.Bend = 1f; p.HeadNod = 0.9f; p.Turn = 0.3f;
        p.HandRight = V(4f, 6f, 6f); p.HandLeft = V(5f, -5f, 6f);
        p.FootSpace = Space.World; p.FootRight = V(2f, 6f, 1.8f); p.FootLeft = V(3f, -4f, 1.8f);
        p.KneeRight = V(0f, 0.3f, 1f); p.KneeLeft = V(0f, -0.3f, 1f);
        p.OpenRight = p.OpenLeft = true;
    });

    public static IEnumerable<RigClip> Watcher()
    {
        HumanPose s = WatcherStance;
        yield return new RigClip("rest", ClipKind.Full, true,
        [
            new RigFrame(WatcherCurled, 1.1f) { Heat = 0.3f },
            new RigFrame(WatcherCurled.HW(p => { p.Pelvis += V(0f, 0f, 0.6f); p.Bend = 0.95f; }), 1.1f) { Heat = 0.33f },
        ]);
        // Waking: the head comes up first, then it unfolds onto its knees and rises; heat floods back in.
        yield return new RigClip("awaken", ClipKind.Full, false,
        [
            new RigFrame(WatcherCurled.HW(p => { p.HeadNod = 0.2f; p.HeadTurn = 0.4f; }), 0.15f) { Heat = 0.45f },
            new RigFrame(WatcherCurled.HW(p => { p.Lean = 0.7f; p.Bend = 0.8f; p.HeadNod = -0.2f; p.Pelvis = V(-3f, 0f, 7f); p.HandRight = V(9f, 7f, -3f); p.HandLeft = V(9f, -7f, -3f); }), 0.15f) { Heat = 0.6f },
            new RigFrame(s.HW(p =>
            {
                p.Pelvis = V(-2f, 0f, 9f); p.Bend = 1f; p.HeadNod = -0.6f;
                p.FootSpace = Space.World; p.FootRight = V(-9f, 5f, 1.8f); p.FootLeft = V(-9f, -5f, 1.8f); p.KneeRight = V(1f, 0.2f, -0.5f); p.KneeLeft = V(1f, -0.2f, -0.5f);
                p.HandRight = V(12f, 9f, -6f); p.HandLeft = V(12f, -9f, -6f);
            }), 0.2f) { Heat = 0.75f },
            new RigFrame(s.HW(p => { p.Pelvis = V(-1.5f, 0f, 15f); p.Bend = 0.9f; p.HeadNod = -0.7f; p.HandRight = V(12f, 10f, -9f); p.HandLeft = V(12f, -10f, -9f); }), 0.2f) { Heat = 0.9f },
            new RigFrame(s.HW(p => { p.HeadNod = -0.75f; p.HandRight = V(10f, 12f, -6f); p.HandLeft = V(10f, -12f, -6f); }), 0.3f),
        ]);
        yield return new RigClip("idle", ClipKind.Full, true,
        [
            new RigFrame(s, 0.5f),
            new RigFrame(s.HW(p => { p.HeadTurn = 0.35f; p.HandRight += V(0f, 0.5f, 0.6f); }), 0.4f),
            new RigFrame(s.HW(p => { p.HeadTurn = -0.3f; p.HandLeft += V(0f, -0.5f, 0.6f); }), 0.4f),
        ]);
        // Stalking gait: long strides, arms swinging low.
        var walk = new List<RigFrame>();
        for (int i = 0; i < 8; i++)
        {
            float ph = i * 2f * Pi / 8f, sn = MathF.Sin(ph), cs = MathF.Cos(ph);
            walk.Add(new RigFrame(s.HW(p =>
            {
                p.FootRight += V(8f * sn, 0f, 3f * MathF.Max(0f, cs)); p.FootLeft += V(-8f * sn, 0f, 3f * MathF.Max(0f, -cs));
                p.HandRight += V(-5f * sn, 0f, 1.5f * MathF.Abs(sn)); p.HandLeft += V(5f * sn, 0f, 1.5f * MathF.Abs(sn));
                p.Twist = 0.12f * sn; p.Pelvis += V(0f, 0f, -0.8f * MathF.Abs(cs)); p.HeadTurn = -0.08f * sn;
            }), 1f / 8f, Event: i % 4 == 2 ? "footstep" : null));
        }
        yield return new RigClip("walk", ClipKind.Full, true, walk);
        // Wide double sweep with a long, readable wind-up (the hit lands at 0.28 s).
        yield return new RigClip("attack", ClipKind.Full, false,
        [
            new RigFrame(s.HW(p => { p.Bend = 0.4f; p.HeadNod = -0.3f; p.HandRight = V(2f, 17f, 2f); p.HandLeft = V(2f, -17f, 2f); }), 0.12f),
            new RigFrame(s.HW(p => { p.Bend = 0.3f; p.HeadNod = -0.2f; p.Lean = -0.1f; p.HandRight = V(-3f, 18f, 6f); p.HandLeft = V(-3f, -18f, 6f); }), 0.14f),
            new RigFrame(s.HW(p => { p.Bend = 1f; p.Pelvis += V(2f, 0f, -1f); p.HeadNod = -0.6f; p.HandRight = V(19f, 3f, -10f); p.HandLeft = V(19f, -3f, -10f); }), 0.08f, Event: "impact"),
            new RigFrame(s.HW(p => { p.Bend = 0.95f; p.Pelvis += V(1.5f, 0f, -1f); p.HandRight = V(16f, -4f, -12f); p.HandLeft = V(16f, 4f, -12f); }), 0.1f),
            new RigFrame(s, 0.15f),
        ]);
        yield return new RigClip("hit", ClipKind.Full, false,
        [
            new RigFrame(s.HW(p => { p.Pelvis += V(-2f, 0f, 0f); p.Bend = 0.4f; p.HeadNod = -1f; p.HeadTurn = 0.6f; p.HandRight = V(4f, 15f, -2f); p.HandLeft = V(6f, -14f, 0f); }), 0.08f),
            new RigFrame(s.HW(p => { p.Pelvis += V(-1f, 0f, 0f); p.Bend = 0.6f; p.HeadNod = -0.7f; p.HeadTurn = 0.3f; }), 0.12f),
        ]);
        HumanPose dead = H(p =>
        {
            p.Lean = Pi / 2f; p.Pelvis = V(-5f, 0f, 4f); p.HeadNod = -0.4f; p.HeadTurn = 0.5f;
            p.HandSpace = Space.World; p.HandRight = V(16f, 11f, 2f); p.HandLeft = V(10f, -14f, 2f);
            p.ElbowRight = V(-1f, 1f, 0f); p.ElbowLeft = V(-1f, -1f, 0f);
            p.FootSpace = Space.World; p.FootRight = V(-24f, 4f, 1.8f); p.FootLeft = V(-22f, -7f, 1.8f);
            p.KneeRight = V(0f, 1f, 0f); p.KneeLeft = V(0f, -1f, 0f); p.FeetAlongShins = true; p.OpenRight = p.OpenLeft = true;
        });
        yield return new RigClip("death", ClipKind.Full, false,
        [
            new RigFrame(s.HW(p => { p.Bend = 0.3f; p.HeadNod = -1f; p.HandRight = V(2f, 16f, 6f); p.HandLeft = V(2f, -16f, 6f); }), 0.12f),
            new RigFrame(s.HW(p => { p.Pelvis = V(0f, 0f, 13f); p.Lean = 0.4f; p.Bend = 0.9f; p.HandRight = V(14f, 10f, -8f); p.HandLeft = V(14f, -10f, -8f); }), 0.12f),
            new RigFrame(dead.HW(p => { p.Lean = 1.2f; p.Pelvis = V(-3f, 0f, 7f); }), 0.1f),
            new RigFrame(dead, 0.3f, Event: "thud") { Heat = 0.85f },
            new RigFrame(dead, 0.6f, Pool: 0.6f) { Heat = 0.7f },
            new RigFrame(dead, 1.2f, Pool: 0.9f) { Heat = 0.55f },
            new RigFrame(dead, 1f, Pool: 1f) { Heat = 0.45f },
        ]);
    }
}
