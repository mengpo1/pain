using Microsoft.Xna.Framework;
using TopDown.Core.Definitions;
using TopDown.Core.World;
using NVector2 = System.Numerics.Vector2;

namespace TopDown.Desktop.Graphics;

/// <summary>What executions did to a body: crushed head, parts torn off. Presentation only.</summary>
public sealed class BodyDamage
{
    /// <summary>Indices (in the actor's sprite parts) of the parts torn off.</summary>
    public HashSet<int> Torn { get; } = new();

    /// <summary>0 = intact head, 1 = head burst.</summary>
    public float HeadWounds { get; set; }

    /// <summary>Wounded: lies face down (crawling, then dead in that position).</summary>
    public bool FaceDown { get; set; }

    /// <summary>Distance crawled when the last blood smear was left.</summary>
    public float TrailDistance { get; set; }

    /// <summary>Time of the next moan of a crawler.</summary>
    public float NextGroan { get; set; }
}

/// <summary>Kind of sound an execution blow makes, beyond the weapon's own hit sound.</summary>
public enum GoreSound { None, Crush, Tear }

/// <summary>
/// Blood, burst heads and torn-off parts of executions. The simulation only says which blow of
/// which animation landed; this layer remembers the damage so bodies keep it once dead.
/// </summary>
public sealed class GoreLayer
{
    private readonly Dictionary<Actor, BodyDamage> _bodies = new();

    public BodyDamage? Damage(Actor actor) => _bodies.GetValueOrDefault(actor);

    public void Clear() => _bodies.Clear();

    /// <summary>A lethal hit left the target wounded: it now lies face down and bleeds.</summary>
    public void Wounded(ActorWounded wounded, EffectsLayer effects, Color blood, float time)
    {
        BodyDamage damage = GetOrAdd(wounded.Target);
        damage.FaceDown = true;
        damage.TrailDistance = wounded.Target.DistanceWalked;
        damage.NextGroan = time + 1.6f;
        Vector2 position = ToXna(wounded.Target.Position);
        effects.BloodSpray(position, SafeNormalize(ToXna(wounded.Direction)), 14, blood);
        effects.BloodPool(position, blood, 0.7f);
    }

    /// <summary>
    /// A crawler leaves a smear of blood every few units and moans now and then. Returns true when
    /// it is time to play its moan.
    /// </summary>
    public bool Crawling(Actor actor, EffectsLayer effects, Color blood, float time)
    {
        BodyDamage damage = GetOrAdd(actor);
        damage.FaceDown = true;
        if (actor.DistanceWalked - damage.TrailDistance >= TrailSpacing)
        {
            damage.TrailDistance = actor.DistanceWalked;
            NVector2 back = -new NVector2(MathF.Cos(actor.Facing), MathF.Sin(actor.Facing)) * (actor.Radius * 0.4f);
            effects.BloodPool(ToXna(actor.Position + back), blood, 0.3f);
        }

        if (time < damage.NextGroan) return false;
        damage.NextGroan = time + 2.6f + (actor.Id % 5) * 0.35f;
        return true;
    }

    private const float TrailSpacing = 6f;

    /// <summary>Index of the next part an execution of this kind tears off, or -1.</summary>
    public int NextPart(Actor victim, ExecutionAnimation animation)
    {
        string? kind = animation switch
        {
            ExecutionAnimation.TearLegs => "leg",
            ExecutionAnimation.TearHead => "head",
            _ => null,
        };
        if (kind is null || victim.Definition.Sprites.Torso is null) return -1;
        BodyDamage? damage = Damage(victim);
        List<BodyPart> parts = victim.Definition.Sprites.Parts;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].Id == kind && damage?.Torn.Contains(i) != true) return i;
        }

        return -1;
    }

    /// <summary>Applies one execution blow: blood, gore, parts flying off. Returns the extra sound to play.</summary>
    public GoreSound Strike(ExecutionStrike strike, EffectsLayer effects, AssetCache assets, Color blood, Color bodyTint)
    {
        Actor victim = strike.Victim;
        BodyDamage damage = GetOrAdd(victim);
        NVector2 forward = new(MathF.Cos(victim.Facing), MathF.Sin(victim.Facing));
        Vector2 from = ToXna(strike.Executioner.Position);

        int part = NextPart(victim, strike.Animation);
        if (part >= 0)
        {
            bool tearsOff = strike.Animation == ExecutionAnimation.TearLegs || strike.Final;
            PartPlacement placement = Place(victim, part, assets, NVector2.Zero);
            Vector2 outward = SafeNormalize(placement.Center - placement.Joint);
            if (!tearsOff)
            {
                effects.BloodSpray(placement.Joint, outward, 6, blood);
                return GoreSound.None;
            }

            damage.Torn.Add(part);
            Vector2 throwDirection = SafeNormalize(outward * 1.4f + SafeNormalize(placement.Center - from));
            effects.FlyingPart(placement.Center, throwDirection * 170f, victim.Facing, victim.Definition.Sprites.Parts[part].Sprite,
                placement.Origin, bodyTint);
            effects.BloodSpray(placement.Joint, outward, 9, blood);
            effects.BloodSpray(placement.Joint, -throwDirection, 3, blood);
            effects.Chunks(placement.Joint, outward, 2, blood);
            effects.BloodPool(placement.Joint, blood, 0.45f);
            return GoreSound.Tear;
        }

        if (strike.Animation is ExecutionAnimation.Stab or ExecutionAnimation.Bite)
        {
            Vector2 throat = ToXna(victim.Position + forward * (victim.Radius * 0.85f));
            effects.BloodSpray(throat, SafeNormalize(throat - from), strike.Final ? 18 : 8, blood);
            if (strike.Final) effects.BloodPool(throat, blood, 0.8f);
            return GoreSound.None;
        }

        // Head blows: slammed into the floor, kicked, smashed or shot until it bursts.
        Vector2 head = ToXna(ExecutionPose.HeadOf(victim));
        int needed = Math.Max(1, strike.Executioner.ExecutionStrikesNeeded);
        damage.HeadWounds = strike.Final ? 1f : MathF.Max(damage.HeadWounds, MathF.Min(0.6f, (float)strike.Index / needed));
        Vector2 away = SafeNormalize(head - from);
        if (!strike.Final)
        {
            effects.BloodSpray(head, away, 9, blood);
            effects.BloodPool(head, blood, 0.45f);
            return GoreSound.None;
        }

        effects.BloodBurst(head, 16, blood, 0.55f);
        effects.BloodSpray(head, away, 10, blood);
        effects.Chunks(head, away, 6, blood);
        effects.BloodPool(head + away * 3f, blood, 1.1f);
        return GoreSound.Crush;
    }

    /// <summary>World placement of a body part, with an extra offset in body space (for pulls).</summary>
    public static PartPlacement Place(Actor victim, int part, AssetCache assets, NVector2 bodyOffset)
    {
        ActorSprites sprites = victim.Definition.Sprites;
        PartShape shape = assets.PartShape(sprites.Parts[part].Sprite, sprites.Torso ?? "");
        SpriteFrame frame = assets.Sprite(sprites.Parts[part].Sprite);
        Vector2 position = ToXna(victim.Position);
        Vector2 center = position + Rotate(shape.Center + ToXna(bodyOffset), victim.Facing);
        Vector2 joint = position + Rotate(shape.Joint + ToXna(bodyOffset), victim.Facing);
        return new PartPlacement(center, joint, frame.Origin + shape.Center);
    }

    private BodyDamage GetOrAdd(Actor actor)
    {
        if (!_bodies.TryGetValue(actor, out BodyDamage? damage)) _bodies[actor] = damage = new BodyDamage();
        return damage;
    }

    private static Vector2 ToXna(NVector2 v) => new(v.X, v.Y);

    private static Vector2 SafeNormalize(Vector2 v) => v.LengthSquared() > 0.0001f ? Vector2.Normalize(v) : Vector2.UnitX;

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }
}

/// <summary>A body part in the world: its centre, its joint, and its pivot in its image.</summary>
public readonly record struct PartPlacement(Vector2 Center, Vector2 Joint, Vector2 Origin);
