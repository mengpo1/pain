using System.Numerics;

namespace PlaceholderArt.Rig;

public enum HairStyle { Short, Slick, Bald, Buzz, Long, Cap }

/// <summary>Body parts of a human model (drawing subsets: upper body, legs, severed limbs).</summary>
public static class HumanPart
{
    public const int Head = 0, Torso = 1, ArmLeft = 2, ArmRight = 3, LegLeft = 4, LegRight = 5;

    public static readonly int[] Upper = [Head, Torso, ArmLeft, ArmRight];
    public static readonly int[] Legs = [LegLeft, LegRight];
}

/// <summary>Who a human is: colours (per material), proportions and clothes.</summary>
public sealed record HumanBuild
{
    public required Palette Palette { get; init; }
    public HairStyle Hair { get; init; } = HairStyle.Short;

    /// <summary>Tank top: bare shoulders and arms.</summary>
    public bool BareArms { get; init; }

    /// <summary>Sleeves down to the wrists (otherwise the forearms are bare).</summary>
    public bool Sleeves { get; init; } = true;

    /// <summary>Jacket worn open: the shirt shows down the front.</summary>
    public bool OpenJacket { get; init; }

    /// <summary>Stripes down the sleeves (track suit).</summary>
    public bool Stripes { get; init; }

    /// <summary>Overall size (1 = a regular 40 px tall figure).</summary>
    public float Scale { get; init; } = 1f;

    /// <summary>Extra width across the shoulders and the chest.</summary>
    public float Bulk { get; init; } = 1f;

    /// <summary>Limb lengths and head size against a regular figure (creatures: long arms, small head).</summary>
    public float Arms { get; init; } = 1f;
    public float Legs { get; init; } = 1f;
    public float Head { get; init; } = 1f;

    /// <summary>No ears, nose or hair: a smooth skull with eye and mouth hot spots.</summary>
    public bool Featureless { get; init; }
}

/// <summary>Where a hand or foot target is given.</summary>
public enum Space { Pelvis, Chest, World }

/// <summary>
/// A pose of the human rig, in pixels, for a figure facing +X. The pelvis is placed in sprite space and
/// oriented (turn, lean, roll); the chest bends and twists on it, the head on the chest. Hands and feet are
/// targets reached by two-bone inverse kinematics; the pole vectors say where elbows and knees point.
/// </summary>
public sealed class HumanPose
{
    public Vector3 Pelvis = new(0f, 0f, 17f);
    public float Turn, Lean, Roll;
    public float Bend, Twist, Side;
    public float HeadTurn, HeadNod, HeadTilt;

    public Vector3 HandRight = new(3.5f, 7.6f, -4.5f), HandLeft = new(3.5f, -7.6f, -4.5f);
    public Space HandSpace = Space.Pelvis;
    public Vector3 ElbowRight = new(-0.5f, 1f, -0.4f), ElbowLeft = new(-0.5f, -1f, -0.4f);
    public bool OpenRight, OpenLeft;

    public Vector3 FootRight = new(0.6f, 3.3f, -15.3f), FootLeft = new(0.6f, -3.3f, -15.3f);
    public Space FootSpace = Space.Pelvis;
    public Vector3 KneeRight = new(1f, 0.25f, 0f), KneeLeft = new(1f, -0.25f, 0f);
    /// <summary>Foot directions: turn from the pelvis forward, and toes pointing down (+) or up (-).</summary>
    public float FootTurnRight, FootTurnLeft, FootPointRight, FootPointLeft;
    /// <summary>Feet as seen lying down: the foot axis follows the shin instead of the pelvis.</summary>
    public bool FeetAlongShins;

    public HumanPose Copy() => (HumanPose)MemberwiseClone();

    /// <summary>Linear blend of every number of two poses (flags from the second past halfway).</summary>
    public static HumanPose Lerp(HumanPose a, HumanPose b, float t)
    {
        static float L(float x, float y, float k) => x + (y - x) * k;
        HumanPose r = (t < 0.5f ? a : b).Copy();
        r.Pelvis = Vector3.Lerp(a.Pelvis, b.Pelvis, t);
        r.Turn = L(a.Turn, b.Turn, t); r.Lean = L(a.Lean, b.Lean, t); r.Roll = L(a.Roll, b.Roll, t);
        r.Bend = L(a.Bend, b.Bend, t); r.Twist = L(a.Twist, b.Twist, t); r.Side = L(a.Side, b.Side, t);
        r.HeadTurn = L(a.HeadTurn, b.HeadTurn, t); r.HeadNod = L(a.HeadNod, b.HeadNod, t); r.HeadTilt = L(a.HeadTilt, b.HeadTilt, t);
        if (a.HandSpace == b.HandSpace)
        {
            r.HandRight = Vector3.Lerp(a.HandRight, b.HandRight, t);
            r.HandLeft = Vector3.Lerp(a.HandLeft, b.HandLeft, t);
        }
        r.ElbowRight = Vector3.Lerp(a.ElbowRight, b.ElbowRight, t);
        r.ElbowLeft = Vector3.Lerp(a.ElbowLeft, b.ElbowLeft, t);
        if (a.FootSpace == b.FootSpace)
        {
            r.FootRight = Vector3.Lerp(a.FootRight, b.FootRight, t);
            r.FootLeft = Vector3.Lerp(a.FootLeft, b.FootLeft, t);
        }
        r.KneeRight = Vector3.Lerp(a.KneeRight, b.KneeRight, t);
        r.KneeLeft = Vector3.Lerp(a.KneeLeft, b.KneeLeft, t);
        r.FootTurnRight = L(a.FootTurnRight, b.FootTurnRight, t); r.FootTurnLeft = L(a.FootTurnLeft, b.FootTurnLeft, t);
        r.FootPointRight = L(a.FootPointRight, b.FootPointRight, t); r.FootPointLeft = L(a.FootPointLeft, b.FootPointLeft, t);
        return r;
    }
}

/// <summary>Joint positions of a posed human, in sprite space.</summary>
public sealed class HumanSkeleton
{
    public Basis Pelvis, Waist, Chest, Head;
    public Vector3 ShoulderRight, ShoulderLeft, ElbowRight, ElbowLeft, WristRight, WristLeft, HandRight, HandLeft;
    public Vector3 HipRight, HipLeft, KneeRight, KneeLeft, AnkleRight, AnkleLeft;
    public Basis FootRight, FootLeft;
    public float HeadRadius;
}

/// <summary>Builds the skeleton and the solid parts of a human from a pose.</summary>
public static class HumanModel
{
    public const float UpperArm = 7.2f, Forearm = 6.5f, Thigh = 7.8f, Shin = 7.2f;

    public static HumanSkeleton Pose(HumanPose pose, HumanBuild build)
    {
        float s = build.Scale, w = build.Bulk;
        var k = new HumanSkeleton();
        Basis pelvis = (Basis.Identity with { Origin = pose.Pelvis * new Vector3(1f, 1f, 1f) })
            .Turn(pose.Turn).Lean(pose.Lean).Roll(pose.Roll);
        k.Pelvis = pelvis;
        // The spine bends over two segments, so a bow curves the back instead of folding it.
        Basis waist = pelvis.Moved(0f, 0f, 4.2f * s).Turn(pose.Twist * 0.45f).Lean(pose.Bend * 0.5f).Roll(pose.Side * 0.5f);
        k.Waist = waist;
        Basis chest = waist.Moved(0f, 0f, 4.6f * s).Turn(pose.Twist * 0.55f).Lean(pose.Bend * 0.5f).Roll(pose.Side * 0.5f);
        k.Chest = chest;
        k.HeadRadius = 5f * (1f + (s - 1f) * 0.45f) * build.Head;
        k.Head = chest.Moved(0.3f * s, 0f, 3.4f * s).Turn(pose.HeadTurn).Lean(pose.HeadNod).Roll(pose.HeadTilt).Moved(0.8f * s, 0f, k.HeadRadius - 0.4f);

        k.ShoulderRight = chest.At(-0.2f * s, 7.3f * s * w, 1.4f * s);
        k.ShoulderLeft = chest.At(-0.2f * s, -7.3f * s * w, 1.4f * s);
        Vector3 Target(Vector3 local, Space space) => space switch
        {
            Space.World => local,
            Space.Chest => chest.At(local * s),
            _ => pelvis.At(local * s),
        };

        float arm = s * build.Arms, leg = s * build.Legs;
        (k.ElbowRight, k.WristRight) = Reach(k.ShoulderRight, Target(pose.HandRight, pose.HandSpace), UpperArm * arm, Forearm * arm, pelvis.Dir(pose.ElbowRight));
        (k.ElbowLeft, k.WristLeft) = Reach(k.ShoulderLeft, Target(pose.HandLeft, pose.HandSpace), UpperArm * arm, Forearm * arm, pelvis.Dir(pose.ElbowLeft));
        k.HandRight = k.WristRight + Vector3.Normalize(k.WristRight - k.ElbowRight) * 1.3f * s;
        k.HandLeft = k.WristLeft + Vector3.Normalize(k.WristLeft - k.ElbowLeft) * 1.3f * s;

        float hip = 3.2f * s * MathF.Sqrt(w);
        k.HipRight = pelvis.At(0f, hip, -1.2f * s);
        k.HipLeft = pelvis.At(0f, -hip, -1.2f * s);
        (k.KneeRight, k.AnkleRight) = Reach(k.HipRight, Target(pose.FootRight, pose.FootSpace), Thigh * leg, Shin * leg, pelvis.Dir(pose.KneeRight));
        (k.KneeLeft, k.AnkleLeft) = Reach(k.HipLeft, Target(pose.FootLeft, pose.FootSpace), Thigh * leg, Shin * leg, pelvis.Dir(pose.KneeLeft));
        k.FootRight = FootFrame(pelvis, k.KneeRight, k.AnkleRight, pose.FootTurnRight, pose.FootPointRight, pose.FeetAlongShins);
        k.FootLeft = FootFrame(pelvis, k.KneeLeft, k.AnkleLeft, pose.FootTurnLeft, pose.FootPointLeft, pose.FeetAlongShins);
        return k;
    }

    static Basis FootFrame(Basis pelvis, Vector3 knee, Vector3 ankle, float turn, float point, bool alongShin)
    {
        Basis foot = pelvis with { Origin = ankle };
        if (alongShin)
        {
            // Lying: the foot stands up from the shin, toes towards the pelvis forward (the sky when on the back).
            Vector3 shin = Vector3.Normalize(ankle - knee);
            Vector3 forward = Vector3.Normalize(pelvis.Forward - shin * Vector3.Dot(pelvis.Forward, shin));
            foot = new Basis(ankle, forward, pelvis.Right, -shin);
        }
        return foot.Turn(turn).Lean(point);
    }

    /// <summary>Two-bone inverse kinematics: the middle joint bends towards the pole.</summary>
    public static (Vector3 Middle, Vector3 End) Reach(Vector3 root, Vector3 target, float a, float b, Vector3 pole)
    {
        Vector3 d = target - root;
        float length = d.Length();
        Vector3 dir = length < 1e-4f ? Vector3.UnitX : d / length;
        length = Math.Clamp(length, MathF.Abs(a - b) + 0.05f, a + b - 0.02f);
        Vector3 perp = pole - dir * Vector3.Dot(pole, dir);
        perp = perp.LengthSquared() < 1e-6f ? new Vector3(-dir.Z, 0f, dir.X) : Vector3.Normalize(perp);
        float cos = Math.Clamp((a * a + length * length - b * b) / (2f * a * length), -1f, 1f);
        float sin = MathF.Sqrt(1f - cos * cos);
        return (root + dir * (a * cos) + perp * (a * sin), root + dir * length);
    }

    public static List<Prim> Build(HumanPose pose, HumanBuild build) => Build(Pose(pose, build), pose, build);

    public static List<Prim> Build(HumanSkeleton k, HumanPose pose, HumanBuild build)
    {
        float s = build.Scale, w = build.Bulk;
        var prims = new List<Prim>();
        Mat sleeve = build.BareArms ? Mat.Skin : Mat.Top;
        Mat forearm = build.BareArms || !build.Sleeves ? Mat.Skin : Mat.Top;

        // Torso: hips, belly and chest melt into one volume; the shoulders round it off.
        prims.Add(new Egg(k.Pelvis, new Vector3(4.2f, 5.9f * w, 3.6f) * s)
        {
            Group = 0, Part = HumanPart.Torso, Material = Mat.Pants, Frame = k.Pelvis,
            Paint = p => p.Z > 1.5f * s ? Mat.Belt : p.Z > 2.6f * s ? Mat.Top : Mat.Pants,
        });
        prims.Add(new Egg(k.Waist.Moved(0f, 0f, 0.6f * s), new Vector3(4.2f, 6.3f * w, 3.9f) * s)
        {
            Group = 0, Part = HumanPart.Torso, Material = Mat.Top, Frame = k.Waist.Moved(0f, 0f, 0.6f * s),
            Paint = p => build.OpenJacket && p.X > 2.2f * s && MathF.Abs(p.Y) < 1.7f * s ? Mat.Shirt : Mat.Top,
        });
        Basis chestCenter = k.Chest.Moved(-0.2f * s, 0f, 0.3f * s);
        prims.Add(new Egg(chestCenter, new Vector3(4.7f, 8.0f * w, 4.6f) * s)
        {
            Group = 0, Part = HumanPart.Torso, Material = Mat.Top, Frame = chestCenter,
            Paint = p => ChestPaint(p, build),
        });
        foreach (Vector3 shoulder in new[] { k.ShoulderRight, k.ShoulderLeft })
            prims.Add(new Ball(shoulder, 3.05f * s) { Group = 0, Part = HumanPart.Torso, Material = sleeve });

        // Head: neck, skull, nose and ears.
        float r = k.HeadRadius;
        prims.Add(new Bone(k.Chest.At(0.2f * s, 0f, 2.2f * s), k.Head.At(-0.6f, 0f, -r * 0.55f), 2.3f * s, 2.1f * s)
            { Group = 1, Part = HumanPart.Head, Material = Mat.Skin });
        prims.Add(new Ball(k.Head.Origin, r) { Group = 1, Part = HumanPart.Head, Material = Mat.Skin, Frame = k.Head, Paint = p => HeadPaint(p, r, build) });
        if (!build.Featureless) prims.Add(new Ball(k.Head.At(r * 0.92f, 0f, -r * 0.18f), 1.05f) { Group = 1, Part = HumanPart.Head, Material = Mat.Skin });
        if (build.Hair != HairStyle.Long && !build.Featureless)
            foreach (int side in new[] { -1, 1 })
                prims.Add(new Ball(k.Head.At(-0.2f, side * r * 0.96f, -r * 0.12f), 1.15f) { Group = 1, Part = HumanPart.Head, Material = Mat.Skin });
        if (build.Hair == HairStyle.Cap)
            prims.Add(new Egg(k.Head.Moved(r * 0.85f, 0f, r * 0.45f).Lean(0.35f), new Vector3(3.2f, 4.2f, 0.7f)) { Group = 1, Part = HumanPart.Head, Material = Mat.Trim });

        // Arms.
        Arm(prims, k.ShoulderRight, k.ElbowRight, k.WristRight, k.HandRight, pose.OpenRight, HumanPart.ArmRight, 3, sleeve, forearm, build);
        Arm(prims, k.ShoulderLeft, k.ElbowLeft, k.WristLeft, k.HandLeft, pose.OpenLeft, HumanPart.ArmLeft, 2, sleeve, forearm, build);

        // Legs.
        Leg(prims, k.HipRight, k.KneeRight, k.AnkleRight, k.FootRight, HumanPart.LegRight, 5, s);
        Leg(prims, k.HipLeft, k.KneeLeft, k.AnkleLeft, k.FootLeft, HumanPart.LegLeft, 4, s);
        return prims;
    }

    static void Arm(List<Prim> prims, Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 hand, bool open, int part, int group,
        Mat sleeve, Mat forearm, HumanBuild build)
    {
        float s = build.Scale, thick = build.BareArms ? 1.12f : 1f;
        Func<Vector3, Mat>? stripes = build.Stripes ? p => MathF.Abs(p.Y) < 0.75f && p.Z > 0f ? Mat.Trim : Mat.Top : null;
        Basis along = ArmFrame(shoulder, elbow);
        prims.Add(new Bone(shoulder, elbow, 2.55f * s * thick, 2.25f * s * thick) { Group = group, Part = part, Material = sleeve, Frame = along, Paint = sleeve == Mat.Top ? stripes : null });
        Basis fore = ArmFrame(elbow, wrist);
        prims.Add(new Bone(elbow, wrist, 2.2f * s * thick, 1.85f * s * thick)
        {
            Group = group, Part = part, Material = forearm, Frame = fore,
            Paint = forearm == Mat.Top ? p => p.X > Vector3.Distance(elbow, wrist) - 1.1f * s ? Mat.Trim : stripes?.Invoke(p) ?? Mat.Top : null,
        });
        if (open)
        {
            Vector3 dir = Vector3.Normalize(wrist - elbow);
            prims.Add(new Bone(wrist, wrist + dir * 2.6f * s, 1.7f * s, 1.25f * s) { Group = group, Part = part, Material = Mat.Skin });
        }
        else
        {
            prims.Add(new Ball(hand, 2.05f * s * thick) { Group = group, Part = part, Material = Mat.Skin });
        }
    }

    /// <summary>Frame along a limb segment (X along the bone, Z as close to up as possible).</summary>
    static Basis ArmFrame(Vector3 a, Vector3 b)
    {
        Vector3 x = Vector3.Normalize(b - a);
        Vector3 up = MathF.Abs(x.Z) > 0.95f ? Vector3.UnitX : Vector3.UnitZ;
        Vector3 z = Vector3.Normalize(up - x * Vector3.Dot(up, x));
        Vector3 y = Vector3.Cross(z, x);
        return new Basis(a, x, y, z);
    }

    static void Leg(List<Prim> prims, Vector3 hip, Vector3 knee, Vector3 ankle, Basis foot, int part, int group, float s)
    {
        prims.Add(new Bone(hip, knee, 3.0f * s, 2.5f * s) { Group = group, Part = part, Material = Mat.Pants });
        prims.Add(new Bone(knee, ankle, 2.45f * s, 1.85f * s) { Group = group, Part = part, Material = Mat.Pants });
        Basis shoe = foot.Moved(1.7f * s, 0f, -0.7f * s);
        prims.Add(new Egg(shoe, new Vector3(3.2f, 1.95f, 1.45f) * s)
        {
            Group = group, Part = part, Material = Mat.Shoes, Frame = shoe,
            Paint = p => p.Z < -0.6f * s ? Mat.Dark : Mat.Shoes,
        });
    }

    static Mat ChestPaint(Vector3 p, HumanBuild build)
    {
        float s = build.Scale;
        float ay = MathF.Abs(p.Y);
        if (build.BareArms)
        {
            // Tank top: bare shoulders and a scooped neckline.
            if (ay > 5.4f * s * build.Bulk) return Mat.Skin;
            if (p.Z > 2.6f * s && ay < 3.2f * s && p.X > -1.5f * s) return Mat.Skin;
            return Mat.Top;
        }
        // Collar around the neck, shirt down the front of an open jacket.
        if (p.Z > 2.9f * s && ay < 3.9f * s && ay > 1.6f * s && p.X > -2.8f * s) return Mat.Trim;
        if (build.OpenJacket && p.X > 1.2f * s && ay < 2.3f * s - MathF.Max(0f, p.Z) * 0.25f) return Mat.Shirt;
        return Mat.Top;
    }

    /// <summary>Hair, face, eyes and mouth on the skull (local frame: X towards the face, Z the top of the head).</summary>
    static Mat HeadPaint(Vector3 p, float r, HumanBuild build)
    {
        Vector3 q = p / r;
        float ay = MathF.Abs(q.Y);
        if (build.Featureless)
        {
            // Two glowing eye slits high on the front of a smooth skull, a hot mouth below.
            if (q.X > 0.6f && ay > 0.18f && ay < 0.46f && q.Z > 0.05f && q.Z < 0.32f) return Mat.Glow;
            if (q.X > 0.75f && ay < 0.3f && q.Z < -0.2f && q.Z > -0.5f) return Mat.Mouth;
            return Mat.Skin;
        }
        // Face details first: they only show when the face is turned up (lying, looking up).
        if (q.X > 0.7f && ay > 0.2f && ay < 0.5f && q.Z > -0.02f && q.Z < 0.24f) return Mat.Eye;
        if (q.X > 0.82f && ay < 0.24f && q.Z < -0.38f && q.Z > -0.55f) return Mat.Mouth;
        bool hair = build.Hair switch
        {
            HairStyle.Bald => false,
            HairStyle.Buzz => q.X < 0.5f && q.Z > -0.15f || q.X < -0.2f && q.Z > -0.5f,
            HairStyle.Slick => q.X < 0.62f && q.Z > -0.05f || q.X < -0.1f && q.Z > -0.5f,
            HairStyle.Long => q.X < 0.55f && q.Z > -0.2f || q.X < 0.2f && q.Z > -0.95f,
            HairStyle.Cap => q.X < 0.62f && q.Z > 0.05f || q.X < -0.3f && q.Z > -0.45f,
            _ => q.X < 0.5f && q.Z > -0.1f || q.X < -0.15f && q.Z > -0.55f,
        };
        if (build.Hair == HairStyle.Cap && q.Z > 0.05f && q.X < 0.62f) return Mat.Trim;
        if (build.Hair == HairStyle.Slick && hair && q.Z > 0.55f && MathF.Abs(q.Y + 0.25f) < 0.07f) return Mat.Skin; // side parting
        return hair ? Mat.Hair : Mat.Skin;
    }

    /// <summary>Sprite-space position of the right hand (where a held weapon's grip goes).</summary>
    public static Vector2 Grip(HumanSkeleton k) => new(k.HandRight.X, k.HandRight.Y);
}
