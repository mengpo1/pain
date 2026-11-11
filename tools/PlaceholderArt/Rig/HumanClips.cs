using System.Numerics;

namespace PlaceholderArt.Rig;

/// <summary>What a clip draws: the upper body (legs are a separate strip turned with the movement), the whole figure, or the legs.</summary>
public enum ClipKind { Upper, Full, Legs }

/// <summary>One drawn frame: a pose, how long it shows, the held weapon's angle (degrees, null = along the forearm)
/// and side of the body, an event, and the blood pool under a dead body.</summary>
public sealed record RigFrame(HumanPose Pose, float Duration, float? WeaponAngle = null, bool WeaponOver = true, string? Event = null, float Pool = 0f);

public sealed record RigClip(string Name, ClipKind Kind, bool Loop, List<RigFrame> Frames);

/// <summary>
/// The human animations, authored as key poses of the rig. Timings follow the weapons' data: each attack marks
/// the frame of the blow with an "impact" event, which the game lines up with the moment the hit is applied.
/// Figures face +X; falls start facing -X (towards whoever hit them) and end on the back, head towards +X,
/// which is how the game lays a body along the direction of the blow.
/// </summary>
public static class HumanClips
{
    const float Pi = MathF.PI;

    public const int WalkFrames = 8;

    /// <summary>Animation time of one walk cycle (the game plays it with the distance walked: 48 px per second).</summary>
    public const float WalkCycle = 1f;

    static HumanPose P(Action<HumanPose>? edit = null)
    {
        var pose = new HumanPose();
        edit?.Invoke(pose);
        return pose;
    }

    static HumanPose With(this HumanPose pose, Action<HumanPose> edit)
    {
        HumanPose copy = pose.Copy();
        edit(copy);
        return copy;
    }

    static Vector3 V(float x, float y, float z) => new(x, y, z);

    // ------------------------------------------------------------------ stances (one per weapon family)

    public static readonly HumanPose Unarmed = P(p =>
    {
        p.HandRight = V(4.6f, 9.4f, 0.5f);
        p.HandLeft = V(4.6f, -9.4f, 0.5f);
    });

    public static readonly HumanPose Club = P(p =>
    {
        p.Twist = 0.12f;
        p.HandRight = V(7f, 6.4f, 4f);
        p.HandLeft = V(4.4f, -9.4f, 0.5f);
    });

    public static readonly HumanPose Blade = P(p =>
    {
        p.Twist = -0.1f;
        p.HandRight = V(8.6f, 6.8f, 4.5f);
        p.HandLeft = V(5.2f, -8.6f, 3f);
    });

    public static readonly HumanPose Pistol = P(p =>
    {
        p.Twist = -0.08f;
        p.HandRight = V(8.8f, 1.7f, 9.2f);
        p.HandLeft = V(8.1f, 0.1f, 8.8f);
        p.ElbowRight = V(-0.2f, 1f, -0.6f);
        p.ElbowLeft = V(-0.2f, -1f, -0.6f);
    });

    public static readonly HumanPose Long = P(p =>
    {
        p.Twist = 0.18f;
        p.HandRight = V(4.6f, 4.4f, 8.6f);
        p.HandLeft = V(11.4f, 0.4f, 8.8f);
        p.ElbowRight = V(-0.4f, 1f, -0.5f);
        p.ElbowLeft = V(-0.1f, -1f, -0.7f);
    });

    /// <summary>Stances: the held weapon's angle, and whether it goes over the body (blades and clubs held out)
    /// or under it (guns, gripped by hands drawn over them).</summary>
    static readonly (string Name, HumanPose Pose, float Angle, bool Over)[] Stances =
    [
        ("unarmed", Unarmed, 0f, true), ("club", Club, -28f, true), ("blade", Blade, 12f, true), ("pistol", Pistol, 0f, false), ("long", Long, -9f, false),
    ];

    /// <summary>Lying on the back, head towards +X, one arm flung above the head, one knee bent outwards.</summary>
    public static readonly HumanPose Lying = P(p =>
    {
        p.Turn = Pi;
        p.Lean = -Pi / 2f;
        p.Pelvis = V(-3.5f, 0f, 3.7f);
        p.HeadTurn = 0.45f;
        p.HandRight = V(-1.6f, 11f, 14.5f);
        p.HandLeft = V(-1.6f, -9.6f, -2.5f);
        p.ElbowRight = V(-1f, 1f, 0f);
        p.ElbowLeft = V(-1f, -1f, 0f);
        p.FootSpace = Space.World;
        p.FootRight = V(-18.4f, -3.9f, 1.9f);
        p.FootLeft = V(-16.4f, 6.8f, 1.9f);
        p.KneeRight = V(1f, 0f, 0f);
        p.KneeLeft = V(0.5f, -1f, 0f);
        p.FeetAlongShins = true;
        p.FootTurnLeft = -0.5f;
        p.OpenRight = p.OpenLeft = true;
    });

    /// <summary>Lying face down, head towards +X (wounded, crawling).</summary>
    static HumanPose Prone(float phase)
    {
        float s = MathF.Sin(phase), c = MathF.Cos(phase);
        return P(p =>
        {
            p.Lean = Pi / 2f;
            p.Pelvis = V(-4f, 0f, 3.6f);
            p.HeadNod = -0.55f;
            p.HeadTurn = 0.1f * s;
            // Hands on the floor, reaching above the head and pulling the body in turn.
            p.HandRight = V(1.5f, 7.5f + 1.5f * s, 12f + 5.5f * s);
            p.HandLeft = V(1.5f, -7.5f + 1.5f * s, 12f - 5.5f * s);
            p.ElbowRight = V(-1f, 1f, 0f);
            p.ElbowLeft = V(-1f, -1f, 0f);
            p.FootSpace = Space.World;
            p.FootRight = V(-18.6f + 2.5f * MathF.Max(0f, -s), 3.6f + 1.2f * MathF.Max(0f, -s), 1.8f);
            p.FootLeft = V(-18.6f + 2.5f * MathF.Max(0f, s), -3.6f - 1.2f * MathF.Max(0f, s), 1.8f);
            p.KneeRight = V(0f, 1f, 0f);
            p.KneeLeft = V(0f, -1f, 0f);
            p.FeetAlongShins = true;
            p.Twist = 0.08f * c;
        });
    }

    /// <summary>Every clip of a human (the heavy set adds the brute's double-fist smash).</summary>
    public static IEnumerable<RigClip> All(bool heavy)
    {
        foreach (var (name, pose, angle, over) in Stances)
        {
            yield return Idle($"idle.{name}", pose, angle, over);
            yield return Walk($"walk.{name}", name, pose, angle, over);
        }
        yield return Idle("idle", Unarmed, 0f, true);
        yield return Walk("walk", "unarmed", Unarmed, 0f, true);
        yield return Legs();

        yield return Punch(false);
        yield return Punch(true);
        yield return Swing(false);
        yield return Swing(true);
        yield return Slash();
        yield return Stab();
        if (heavy) yield return Smash();
        yield return FirePistol();
        yield return FireLong();

        yield return Hurt();
        yield return Knockdown();
        yield return GetUp();
        yield return DeathShot();
        yield return DeathCut();
        yield return DeathBlunt();
        yield return Crawl();

        yield return ExecuteHeadBash();
        yield return ExecuteStab();
        yield return ExecuteStomp();
        yield return ExecuteSmash("execute.smash", -28f);
        yield return ExecuteSmash("execute.behead", 12f);
        yield return ExecuteTear("execute.tearlegs", legs: true);
        yield return ExecuteTear("execute.tearhead", legs: false);
    }

    // ------------------------------------------------------------------ idle and walk

    static RigClip Idle(string name, HumanPose stance, float angle, bool over) => new(name, ClipKind.Upper, true,
    [
        new RigFrame(stance, 0.55f, angle, over),
        new RigFrame(stance.With(p =>
        {
            p.Twist += 0.03f;
            p.HandRight += V(0f, 0.3f, 0.6f);
            p.HandLeft += V(0f, -0.3f, 0.6f);
            p.HeadNod -= 0.04f;
        }), 0.55f, angle + 1.5f, over),
    ]);

    static RigClip Walk(string name, string family, HumanPose stance, float angle, bool over)
    {
        var frames = new List<RigFrame>();
        for (int i = 0; i < WalkFrames; i++)
        {
            float phase = i * 2f * Pi / WalkFrames;
            float s = MathF.Sin(phase), c = MathF.Cos(2f * phase);
            HumanPose pose = stance.With(p =>
            {
                // Right leg forward at s = 1: right shoulder back, left arm forward.
                switch (family)
                {
                    case "unarmed":
                        p.Twist += 0.07f * s;
                        p.HandRight += V(-5.4f * s, 0.4f * s, 1.4f * MathF.Abs(s));
                        p.HandLeft += V(5.4f * s, 0.4f * s, 1.4f * MathF.Abs(s));
                        break;
                    case "club":
                    case "blade":
                        p.Twist += 0.05f * s;
                        p.HandRight += V(-0.8f * s, 0f, 0.4f * c);
                        p.HandLeft += V(5f * s, 0.3f * s, 1.2f * MathF.Abs(s));
                        break;
                    default:
                        // Aiming while walking: the arms stay up, the shoulders sway a little.
                        p.Twist += 0.045f * s;
                        p.HandRight += V(0f, 0.25f * s, 0.25f * c);
                        p.HandLeft += V(0f, 0.25f * s, 0.25f * c);
                        break;
                }
                p.HeadTurn = -0.05f * s;
                p.Pelvis += V(0f, 0f, -0.3f * c);
            });
            float sway = family is "club" or "blade" ? 4f * s : 0.8f * s;
            frames.Add(new RigFrame(pose, WalkCycle / WalkFrames, angle + sway, over, i % 4 == 2 ? "footstep" : null));
        }
        return new RigClip(name, ClipKind.Upper, true, frames);
    }

    /// <summary>The legs' walk cycle, drawn under the body and turned to the direction of movement.</summary>
    static RigClip Legs()
    {
        var frames = new List<RigFrame>();
        for (int i = 0; i < WalkFrames; i++)
        {
            float phase = i * 2f * Pi / WalkFrames;
            float s = MathF.Sin(phase), c = MathF.Cos(phase);
            HumanPose pose = P(p =>
            {
                p.Pelvis = V(0f, 0f, 16.6f - 0.5f * MathF.Abs(s));
                p.FootRight = V(0.4f + 7.2f * s, 3.2f, -14.8f + 2.6f * MathF.Max(0f, c) + 0.5f * MathF.Abs(s));
                p.FootLeft = V(0.4f - 7.2f * s, -3.2f, -14.8f + 2.6f * MathF.Max(0f, -c) + 0.5f * MathF.Abs(s));
                p.FootPointRight = -0.35f * s;
                p.FootPointLeft = 0.35f * s;
            });
            frames.Add(new RigFrame(pose, WalkCycle / WalkFrames, Event: i % 4 == 2 ? "footstep" : null));
        }
        return new RigClip("legs", ClipKind.Legs, true, frames);
    }

    // ------------------------------------------------------------------ melee

    static HumanPose Mirror(HumanPose pose) => pose.With(p =>
    {
        p.Twist = -pose.Twist;
        p.Side = -pose.Side;
        p.HeadTurn = -pose.HeadTurn;
        p.HandRight = pose.HandLeft * new Vector3(1f, -1f, 1f);
        p.HandLeft = pose.HandRight * new Vector3(1f, -1f, 1f);
        p.ElbowRight = pose.ElbowLeft * new Vector3(1f, -1f, 1f);
        p.ElbowLeft = pose.ElbowRight * new Vector3(1f, -1f, 1f);
    });

    /// <summary>A cross with the right fist (the alternate one is the left jab): wind-up, extension, impact, retraction, guard.</summary>
    static RigClip Punch(bool alternate)
    {
        var frames = new List<RigFrame>
        {
            new(Unarmed.With(p => { p.Twist = 0.32f; p.HandRight = V(1.6f, 8.6f, 7f); p.HandLeft = V(7.6f, -4.8f, 7f); p.HeadNod = 0.05f; }), 0.025f),
            new(Unarmed.With(p => { p.Twist = -0.1f; p.HandRight = V(9.5f, 4.4f, 8.6f); p.HandLeft = V(6.2f, -6.5f, 6.5f); }), 0.02f),
            new(Unarmed.With(p =>
            {
                p.Twist = -0.45f; p.Bend = 0.16f; p.Pelvis = V(1.3f, 0f, 16.8f);
                p.HandRight = V(15.6f, 1.6f, 9f); p.HandLeft = V(3.6f, -8.6f, 6f); p.ElbowRight = V(0f, 1f, -0.3f);
            }), 0.045f, Event: "impact"),
            new(Unarmed.With(p => { p.Twist = -0.22f; p.Bend = 0.06f; p.Pelvis = V(0.6f, 0f, 17f); p.HandRight = V(10.4f, 4.2f, 8f); p.HandLeft = V(5f, -8f, 5f); }), 0.035f),
            new(Unarmed.With(p => { p.Twist = -0.05f; p.HandRight = V(6.4f, 8.4f, 4f); p.HandLeft = V(5.4f, -8.8f, 3f); }), 0.04f),
        };
        if (alternate) frames = frames.Select(f => f with { Pose = Mirror(f.Pose) }).ToList();
        return new RigClip(alternate ? "attack.unarmed.alt" : "attack.unarmed", ClipKind.Upper, false, frames);
    }

    /// <summary>Two-handed bat swing across the front (the alternate one is the backhand on the way back).</summary>
    static RigClip Swing(bool backhand)
    {
        (float twist, Vector3 hand, float angle, bool over)[] keys = backhand
            ? [(-0.62f, V(5f, -7.4f, 6f), -100f, true), (-0.25f, V(10.2f, -3.2f, 7f), -48f, true), (0.28f, V(11.6f, 3.4f, 6.5f), 8f, true),
               (0.6f, V(5.2f, 9.6f, 6f), 78f, true), (0.25f, V(7.4f, 6.8f, 4.2f), 20f, true)]
            : [(0.58f, V(-0.8f, 10f, 6.5f), 128f, false), (0.16f, V(6.2f, 9.2f, 7f), 62f, true), (-0.36f, V(11.2f, 2.6f, 6.5f), -10f, true),
               (-0.72f, V(7f, -6.2f, 6f), -78f, true), (-0.3f, V(8f, 3f, 4.2f), -42f, true)];
        float[] durations = [0.03f, 0.025f, 0.045f, 0.05f, 0.06f];
        var frames = new List<RigFrame>();
        for (int i = 0; i < keys.Length; i++)
        {
            var (twist, hand, angle, over) = keys[i];
            HumanPose pose = Club.With(p =>
            {
                p.Twist = twist;
                p.HandRight = hand;
                // The left hand holds the handle just above the right one.
                float a = angle * Pi / 180f;
                p.HandLeft = hand + V(MathF.Cos(a) * 2.4f, MathF.Sin(a) * 2.4f - 1.4f, 0.4f);
                p.ElbowLeft = V(-0.4f, -1f, -0.4f);
                if (i == 2) { p.Pelvis = V(1f, 0f, 16.8f); p.Bend = 0.12f; }
                if (i == 0) p.HeadTurn = backhand ? -0.15f : 0.15f;
            });
            frames.Add(new RigFrame(pose, durations[i], angle, over, i == 2 ? "impact" : null));
        }
        return new RigClip(backhand ? "attack.club.alt" : "attack.club", ClipKind.Upper, false, frames);
    }

    static RigClip Slash() => new("attack.blade", ClipKind.Upper, false,
    [
        new RigFrame(Blade.With(p => { p.Twist = 0.38f; p.HandRight = V(4f, 10f, 8f); p.HandLeft = V(6f, -7.4f, 5f); }), 0.025f, 82f),
        new RigFrame(Blade.With(p => { p.Twist = -0.12f; p.Pelvis = V(0.8f, 0f, 16.9f); p.HandRight = V(12.6f, 3f, 7f); p.HandLeft = V(4.4f, -8.6f, 4f); }), 0.04f, 6f, Event: "impact"),
        new RigFrame(Blade.With(p => { p.Twist = -0.48f; p.HandRight = V(8.2f, -6.2f, 6f); p.HandLeft = V(3.6f, -9.4f, 3f); }), 0.04f, -72f),
        new RigFrame(Blade.With(p => { p.Twist = -0.16f; p.HandRight = V(8.6f, 4.8f, 5f); }), 0.06f, 4f),
    ]);

    static RigClip Stab() => new("attack.blade.alt", ClipKind.Upper, false,
    [
        new RigFrame(Blade.With(p => { p.Twist = 0.26f; p.HandRight = V(3f, 8.4f, 7.6f); p.HandLeft = V(7f, -6f, 5f); }), 0.025f, 8f),
        new RigFrame(Blade.With(p =>
        {
            p.Twist = -0.38f; p.Bend = 0.14f; p.Pelvis = V(1.3f, 0f, 16.8f);
            p.HandRight = V(15.2f, 2f, 8f); p.HandLeft = V(3.8f, -8.8f, 4f); p.ElbowRight = V(0f, 1f, -0.3f);
        }), 0.045f, 0f, Event: "impact"),
        new RigFrame(Blade.With(p => { p.Twist = -0.16f; p.HandRight = V(10.2f, 4.6f, 7f); }), 0.04f, 4f),
        new RigFrame(Blade, 0.05f, 12f),
    ]);

    /// <summary>The brute's double-fist hammer blow: a long, readable wind-up overhead.</summary>
    static RigClip Smash() => new("attack.heavy", ClipKind.Upper, false,
    [
        new RigFrame(Unarmed.With(p => { p.Lean = -0.12f; p.Bend = -0.18f; p.HandRight = V(0f, 3.2f, 19f); p.HandLeft = V(0f, -3.2f, 19f); p.HeadNod = -0.15f; p.OpenRight = p.OpenLeft = false; }), 0.1f),
        new RigFrame(Unarmed.With(p => { p.Lean = -0.18f; p.Bend = -0.3f; p.HandRight = V(-2.4f, 2.6f, 21f); p.HandLeft = V(-2.4f, -2.6f, 21f); p.HeadNod = -0.2f; }), 0.1f),
        new RigFrame(Unarmed.With(p => { p.Bend = 0.6f; p.Pelvis = V(1.6f, 0f, 16.4f); p.HandRight = V(14.5f, 1.6f, 0.5f); p.HandLeft = V(14.5f, -1.6f, 0.5f); p.HeadNod = 0.2f; }), 0.07f, Event: "impact"),
        new RigFrame(Unarmed.With(p => { p.Bend = 0.45f; p.Pelvis = V(1.2f, 0f, 16.6f); p.HandRight = V(12.5f, 2.2f, 1.5f); p.HandLeft = V(12.5f, -2.2f, 1.5f); }), 0.08f),
        new RigFrame(Unarmed.With(p => { p.Bend = 0.15f; p.HandRight = V(7f, 7.5f, 2.5f); p.HandLeft = V(7f, -7.5f, 2.5f); }), 0.12f),
    ]);

    // ------------------------------------------------------------------ firearms

    static RigClip FirePistol() => new("fire.pistol", ClipKind.Upper, false,
    [
        new RigFrame(Pistol.With(p => { p.Twist = -0.04f; p.HandRight = V(6.8f, 1.6f, 10.2f); p.HandLeft = V(6.2f, 0.1f, 9.8f); p.HeadNod = -0.06f; p.Pelvis = V(-0.4f, 0f, 17f); }), 0.035f, -5f, false),
        new RigFrame(Pistol.With(p => { p.HandRight = V(7.9f, 1.6f, 9.6f); p.HandLeft = V(7.2f, 0.1f, 9.2f); }), 0.05f, -2f, false),
        new RigFrame(Pistol, 0.2f, 0f, false),
    ]);

    static RigClip FireLong() => new("fire.long", ClipKind.Upper, false,
    [
        new RigFrame(Long.With(p => { p.Twist = 0.26f; p.Pelvis = V(-1.3f, 0f, 17f); p.HandRight = V(3.4f, 4.6f, 8.8f); p.HandLeft = V(10f, 0.6f, 9.2f); p.HeadNod = -0.08f; }), 0.045f, -12f, false),
        new RigFrame(Long.With(p => { p.Twist = 0.22f; p.Pelvis = V(-0.6f, 0f, 17f); p.HandRight = V(4f, 4.5f, 8.7f); p.HandLeft = V(10.7f, 0.5f, 9f); }), 0.07f, -10f, false),
        new RigFrame(Long, 0.2f, -9f, false),
    ]);

    // ------------------------------------------------------------------ reactions, falls, deaths

    static RigClip Hurt() => new("hurt", ClipKind.Upper, false,
    [
        new RigFrame(Unarmed.With(p =>
        {
            p.Pelvis = V(-1.4f, 0f, 16.8f); p.Lean = -0.18f; p.HeadNod = -0.38f; p.HeadTurn = 0.3f; p.Twist = 0.28f;
            p.HandRight = V(-1f, 11f, 5f); p.HandLeft = V(2.5f, -11f, 7f); p.OpenRight = p.OpenLeft = true;
        }), 0.06f),
        new RigFrame(Unarmed.With(p => { p.Pelvis = V(-0.8f, 0f, 17f); p.Lean = -0.08f; p.HeadNod = -0.1f; p.Twist = 0.12f; p.HandRight = V(2f, 10f, 2f); p.HandLeft = V(3.5f, -10f, 3f); }), 0.08f),
        new RigFrame(Unarmed.With(p => { p.HeadNod = 0.12f; p.Bend = 0.08f; }), 0.2f),
    ]);

    /// <summary>World-space feet sliding from under a figure facing -X to the lying position.</summary>
    static HumanPose Falling(float lean, Vector3 pelvis, float feetX, float feetSpread, float feetZ, Vector3 handRight, Vector3 handLeft,
        float headNod = 0f, float twist = 0f, float bend = 0f) => P(p =>
    {
        p.Turn = Pi;
        p.Lean = lean;
        p.Pelvis = pelvis;
        p.Twist = twist;
        p.Bend = bend;
        p.HeadNod = headNod;
        p.HandRight = handRight;
        p.HandLeft = handLeft;
        p.ElbowRight = V(-1f, 1f, 0f);
        p.ElbowLeft = V(-1f, -1f, 0f);
        p.FootSpace = Space.World;
        p.FootRight = V(feetX, -feetSpread, feetZ);
        p.FootLeft = V(feetX + 1.2f, feetSpread + 0.6f, feetZ);
        p.KneeRight = V(1f, 0.2f, 0f);
        p.KneeLeft = V(1f, -0.4f, 0f);
        p.OpenRight = p.OpenLeft = true;
    });

    /// <summary>Thrown backwards: stagger, fold at the knees, hit the floor, bounce, lie still.</summary>
    static RigClip Knockdown() => new("knockdown", ClipKind.Full, false,
    [
        new RigFrame(Falling(-0.24f, V(0.8f, 0f, 16.8f), 1.2f, 3.4f, 1.8f, V(8f, 9f, 8f), V(8f, -9f, 9f), headNod: -0.45f), 0.05f),
        new RigFrame(Falling(-0.62f, V(2.8f, 0f, 14f), -2f, 3.6f, 1.9f, V(6f, 10f, 10f), V(6.5f, -10f, 11f), headNod: -0.3f), 0.05f),
        new RigFrame(Falling(-1.08f, V(3f, 0f, 9f), -6.8f, 3.8f, 2.2f, V(2f, 11f, 12f), V(2.5f, -11f, 12.5f), headNod: -0.15f), 0.05f),
        new RigFrame(Falling(-1.46f, V(-0.6f, 0f, 5f), -12.4f, 4.2f, 2.6f, V(-1f, 12f, 11f), V(-1f, -11.5f, 6f)), 0.05f, Event: "thud"),
        new RigFrame(Lying.With(p => { p.Lean = -1.5f; p.Pelvis = V(-2.8f, 0f, 4.4f); p.HeadNod = 0.25f; p.HeadTurn = 0.2f; }), 0.06f),
        new RigFrame(Lying, 0.3f),
    ]);

    /// <summary>Sits up, gets on one knee, stands and turns back to face the way the character was facing.</summary>
    static RigClip GetUp() => new("getup", ClipKind.Full, false,
    [
        new RigFrame(Lying.With(p => { p.Lean = -1.3f; p.Pelvis = V(-3.5f, 0f, 4.2f); p.HeadNod = 0.45f; p.HeadTurn = 0f; p.HandRight = V(0f, 10f, 8f); p.HandLeft = V(0f, -10f, 2f); }), 0.08f),
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Lean = -0.5f; p.Bend = 0.25f; p.Pelvis = V(-3.4f, 0f, 4.4f); p.HeadNod = 0.25f;
            p.HandSpace = Space.World; p.HandRight = V(3f, -8f, 2.2f); p.HandLeft = V(3f, 8f, 2.2f);
            p.FootSpace = Space.World; p.FootRight = V(-12.5f, -4.2f, 1.8f); p.FootLeft = V(-12f, 4.4f, 1.8f);
            p.KneeRight = V(0.6f, 0f, 1f); p.KneeLeft = V(0.6f, 0f, 1f); p.OpenRight = p.OpenLeft = true;
        }), 0.08f),
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Lean = 0.12f; p.Bend = 0.35f; p.Pelvis = V(-2.6f, 0f, 9.6f);
            p.HandRight = V(6f, 6f, -2f); p.HandLeft = V(5f, -8f, -3f);
            p.FootSpace = Space.World; p.FootRight = V(-9.5f, -3.6f, 1.8f); p.FootLeft = V(2.8f, 3.6f, 1.2f);
            p.KneeRight = V(1f, 0f, 0.5f); p.KneeLeft = V(1f, 0f, -1f);
        }), 0.08f),
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Bend = 0.15f; p.Pelvis = V(-1.5f, 0f, 15.4f);
            p.HandRight = V(4f, 9f, 0f); p.HandLeft = V(4f, -9f, 0f);
            p.FootSpace = Space.World; p.FootRight = V(-2.5f, -3.4f, 1.8f); p.FootLeft = V(0f, 3.4f, 1.7f);
        }), 0.07f),
        new RigFrame(Unarmed.With(p => { p.Turn = Pi / 2f; p.Pelvis = V(-0.6f, 0f, 17f); }), 0.045f),
        new RigFrame(Unarmed, 0.2f),
    ]);

    /// <summary>The death throes end on the lying pose, the blood pool spreading over the next frames.</summary>
    static IEnumerable<RigFrame> Bleed(HumanPose lying) =>
    [
        new RigFrame(lying, 0.12f, Pool: 0.35f),
        new RigFrame(lying, 0.3f, Pool: 0.65f),
        new RigFrame(lying, 0.5f, Pool: 0.88f),
        new RigFrame(lying, 0.5f, Pool: 1f),
    ];

    /// <summary>Shot: knocked straight back, arms thrown up, hard landing.</summary>
    static RigClip DeathShot() => new("death.shot", ClipKind.Full, false,
    [
        new RigFrame(Falling(-0.36f, V(1.6f, 0f, 16.4f), 1.4f, 3.4f, 1.8f, V(-2f, 11f, 13f), V(-1f, -11f, 12f), headNod: -0.6f, twist: 0.3f), 0.04f),
        new RigFrame(Falling(-0.86f, V(3.6f, 0f, 12.4f), -3f, 3.6f, 2f, V(-3f, 11.5f, 14f), V(-2f, -11.5f, 14f), headNod: -0.5f, twist: 0.2f), 0.05f),
        new RigFrame(Falling(-1.3f, V(2f, 0f, 7f), -9f, 4f, 2.2f, V(-2.5f, 12f, 14f), V(-2f, -11.5f, 13f), headNod: -0.3f), 0.05f),
        new RigFrame(Lying.With(p => { p.Lean = -1.52f; p.Pelvis = V(-2.6f, 0f, 4.2f); p.HeadNod = 0.2f; }), 0.06f, Event: "thud"),
        ..Bleed(Lying),
    ]);

    /// <summary>Cut: doubles over the wound, knees give way, falls back.</summary>
    static RigClip DeathCut() => new("death.cut", ClipKind.Full, false,
    [
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Bend = 0.5f; p.Pelvis = V(-0.6f, 0f, 16.2f); p.HeadNod = 0.4f; p.Twist = -0.2f;
            p.HandRight = V(6f, 3f, 4f); p.HandLeft = V(6.5f, -2.5f, 5f); p.ElbowRight = V(0f, 1f, -0.3f); p.ElbowLeft = V(0f, -1f, -0.3f);
        }), 0.08f),
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Bend = 0.55f; p.Pelvis = V(0.2f, 0f, 10.6f); p.HeadNod = 0.5f; p.Twist = -0.3f;
            p.HandRight = V(6f, 3.5f, 4f); p.HandLeft = V(6f, -3f, 4.5f);
            p.FootSpace = Space.World; p.FootRight = V(4.5f, -3.6f, 1.6f); p.FootLeft = V(5f, 3.6f, 1.6f);
            p.KneeRight = V(1f, 0f, 0f); p.KneeLeft = V(1f, 0f, 0f);
        }), 0.09f),
        new RigFrame(Falling(-0.85f, V(1f, 0f, 7f), -4f, 3.8f, 2f, V(5f, 4f, 6f), V(5f, -4f, 7f), headNod: 0.3f, bend: 0.25f), 0.07f),
        new RigFrame(Lying.With(p => { p.Lean = -1.5f; p.Pelvis = V(-2.8f, 0f, 4.3f); p.HandLeft = V(2f, -3f, 6f); }), 0.07f, Event: "thud"),
        ..Bleed(Lying),
    ]);

    /// <summary>Clubbed: the head snaps aside, the body drops where it stands, then tips over.</summary>
    static RigClip DeathBlunt() => new("death.blunt", ClipKind.Full, false,
    [
        new RigFrame(P(p =>
        {
            p.Turn = Pi; p.Lean = -0.12f; p.HeadNod = -0.55f; p.HeadTilt = 0.5f; p.HeadTurn = 0.4f; p.Twist = 0.35f;
            p.HandRight = V(0f, 11f, 2f); p.HandLeft = V(3f, -10f, 3f); p.OpenRight = p.OpenLeft = true;
        }), 0.05f),
        new RigFrame(Falling(-0.3f, V(0.4f, 0f, 11.4f), -1f, 3.6f, 1.8f, V(1f, 10.5f, 0f), V(2f, -10.5f, 1f), headNod: 0.4f, twist: 0.2f), 0.07f),
        new RigFrame(Falling(-1f, V(0.2f, 0f, 6.4f), -8f, 4f, 2f, V(-1f, 11f, 6f), V(0f, -11f, 4f), headNod: 0.3f), 0.06f),
        new RigFrame(Lying.With(p => { p.Lean = -1.5f; p.Pelvis = V(-2.8f, 0f, 4.4f); p.HeadTilt = 0.3f; }), 0.06f, Event: "thud"),
        ..Bleed(Lying.With(p => p.HeadTurn = 0.8f)),
    ]);

    static RigClip Crawl()
    {
        var frames = new List<RigFrame>();
        for (int i = 0; i < 4; i++) frames.Add(new RigFrame(Prone(i * Pi / 2f), 0.25f));
        return new RigClip("crawl", ClipKind.Full, true, frames);
    }

    // ------------------------------------------------------------------ executions

    /// <summary>Kneeling astride a body lying with its head towards +X, hands at the given world positions.</summary>
    static HumanPose Astride(float lean, Vector3 handRight, Vector3 handLeft, float headNod = 0.3f, float pelvisX = -1f, float pelvisZ = 9.8f) => P(p =>
    {
        p.Lean = lean;
        p.Pelvis = V(pelvisX, 0f, pelvisZ);
        p.HeadNod = headNod;
        p.HandSpace = Space.World;
        p.HandRight = handRight;
        p.HandLeft = handLeft;
        p.ElbowRight = V(-0.3f, 1f, 0f);
        p.ElbowLeft = V(-0.3f, -1f, 0f);
        p.FootSpace = Space.World;
        // Knees on the floor either side of the body, shins and feet behind.
        p.FootRight = V(pelvisX - 8.5f, 8.4f, 1.4f);
        p.FootLeft = V(pelvisX - 8.5f, -8.4f, 1.4f);
        p.KneeRight = V(1f, 0.6f, -0.2f);
        p.KneeLeft = V(1f, -0.6f, -0.2f);
        p.FootPointRight = p.FootPointLeft = 1.2f;
    });

    /// <summary>Astride, the head held in both hands and slammed against the floor (the victim's head is 10 px ahead).</summary>
    static RigClip ExecuteHeadBash()
    {
        HumanPose raised = Astride(-0.1f, V(9.2f, 4.6f, 12.5f), V(9.2f, -4.6f, 12.5f), headNod: 0.2f);
        return new RigClip("execute.headbash", ClipKind.Full, false,
        [
            new RigFrame(Astride(0.05f, V(10f, 4.4f, 8f), V(10f, -4.4f, 8f)), 0.04f),
            new RigFrame(raised, 0.05f),
            new RigFrame(Astride(0.42f, V(11.4f, 4.4f, 5.6f), V(11.4f, -4.4f, 5.6f), headNod: 0.45f, pelvisX: 0f), 0.06f, Event: "impact"),
            new RigFrame(Astride(0.32f, V(11.2f, 4.4f, 6f), V(11.2f, -4.4f, 6f), headNod: 0.4f, pelvisX: -0.4f), 0.06f),
            new RigFrame(Astride(0.1f, V(10f, 4.5f, 9.5f), V(10f, -4.5f, 9.5f)), 0.07f),
            new RigFrame(raised, 0.3f),
        ]);
    }

    /// <summary>Astride, the knife driven down into the chest with the right hand, the left hand pinning.</summary>
    static RigClip ExecuteStab()
    {
        HumanPose raised = Astride(-0.08f, V(2f, 5f, 17f), V(8f, -3.5f, 8f), headNod: 0.2f, pelvisX: -1.5f);
        return new RigClip("execute.stab", ClipKind.Full, false,
        [
            new RigFrame(raised, 0.08f, -20f),
            new RigFrame(Astride(0.4f, V(7.5f, 1.4f, 7.5f), V(8.5f, -4f, 7.5f), headNod: 0.45f, pelvisX: -0.8f), 0.06f, 0f, Event: "impact"),
            new RigFrame(Astride(0.32f, V(7f, 1.6f, 8f), V(8.5f, -4f, 7.5f), headNod: 0.4f, pelvisX: -1f), 0.07f, 0f),
            new RigFrame(Astride(0.1f, V(4f, 3.6f, 13f), V(8f, -3.6f, 8f), pelvisX: -1.3f), 0.07f, -12f),
            new RigFrame(raised, 0.3f, -20f),
        ]);
    }

    /// <summary>Standing beside the head (13 px ahead), the right foot raised and brought down on it.</summary>
    static RigClip ExecuteStomp()
    {
        HumanPose Leg(Vector3 foot, float lean, float bend) => Unarmed.With(p =>
        {
            p.Lean = lean; p.Bend = bend; p.HeadNod = 0.35f;
            p.FootSpace = Space.World;
            p.FootRight = foot;
            p.FootLeft = V(-1.2f, -3.6f, 1.7f);
            p.KneeRight = V(1f, 0.3f, 0.4f);
            p.HandRight = V(2f, 10.5f, 3f); p.HandLeft = V(3f, -10.5f, 4f);
        });
        HumanPose raised = Leg(V(7f, 2.2f, 10.5f), -0.12f, 0.1f);
        return new RigClip("execute.stomp", ClipKind.Full, false,
        [
            new RigFrame(Leg(V(3f, 3.2f, 5f), -0.04f, 0.1f), 0.05f),
            new RigFrame(raised, 0.06f),
            new RigFrame(Leg(V(11f, 0.8f, 5.4f), 0.1f, 0.3f).With(p => p.Pelvis = V(1.4f, 0f, 16f)), 0.06f, Event: "impact"),
            new RigFrame(Leg(V(10.6f, 1f, 5.6f), 0.06f, 0.25f).With(p => p.Pelvis = V(1f, 0f, 16.2f)), 0.06f),
            new RigFrame(Leg(V(6f, 2.4f, 8.5f), -0.06f, 0.15f), 0.07f),
            new RigFrame(raised, 0.3f),
        ]);
    }

    /// <summary>Standing over the head (20 px ahead), the weapon raised overhead in both hands and brought down.</summary>
    static RigClip ExecuteSmash(string name, float restAngle)
    {
        HumanPose Hands(Vector3 hand, float bend, float lean, Vector3 pelvis) => Club.With(p =>
        {
            p.Twist = 0f; p.Bend = bend; p.Lean = lean; p.Pelvis = pelvis; p.HeadNod = 0.3f + bend * 0.4f;
            p.HandRight = hand;
            p.HandLeft = hand + V(-0.8f, -2.6f, 0.2f);
            p.ElbowRight = V(-0.2f, 1f, -0.2f);
            p.ElbowLeft = V(-0.2f, -1f, -0.2f);
        });
        HumanPose raised = Hands(V(-0.5f, 2f, 21f), -0.25f, -0.06f, V(-0.5f, 0f, 17f));
        return new RigClip(name, ClipKind.Upper, false,
        [
            new RigFrame(Hands(V(4f, 3f, 14f), -0.1f, 0f, V(0f, 0f, 17f)), 0.04f, restAngle - 60f, false),
            new RigFrame(raised, 0.06f, 180f, false),
            new RigFrame(Hands(V(12.5f, 1.4f, -1f), 0.65f, 0.1f, V(1.8f, 0f, 16f)), 0.06f, 0f, true, "impact"),
            new RigFrame(Hands(V(12f, 1.4f, -0.5f), 0.55f, 0.08f, V(1.4f, 0f, 16.2f)), 0.07f, 2f, true),
            new RigFrame(Hands(V(6f, 2.6f, 12f), 0.05f, 0f, V(0.4f, 0f, 16.8f)), 0.07f, -120f, false),
            new RigFrame(raised, 0.3f, 180f, false),
        ]);
    }

    /// <summary>Astride a dog, pulling a hind leg or twisting the head off (strain, then the tear).</summary>
    static RigClip ExecuteTear(string name, bool legs)
    {
        float x = legs ? -2f : 9.5f;
        HumanPose Grip(float pull, float lean, float spread) => Astride(lean,
            V(x - pull, 3.2f + spread, 5f + pull * 0.8f), V(x - pull, -3.2f - spread, 5f + pull * 0.8f), headNod: 0.35f, pelvisX: legs ? -4f : -1f, pelvisZ: 9f);
        return new RigClip(name, ClipKind.Full, false,
        [
            new RigFrame(Grip(0f, 0.3f, 0f), 0.06f),
            new RigFrame(Grip(1.2f, 0.1f, 0.4f), 0.06f),
            new RigFrame(Grip(3.8f, -0.25f, 1.6f), 0.07f, Event: "impact"),
            new RigFrame(Grip(5f, -0.3f, 2.4f), 0.1f),
            new RigFrame(Grip(2f, 0.05f, 0.8f), 0.3f),
        ]);
    }
}
