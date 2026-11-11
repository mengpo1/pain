using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

public enum HitKind
{
    None,
    Tile,
    Door,
    Prop,
    Actor,
}

public readonly record struct RayHit(HitKind Kind, float Fraction, Vector2 Point, Vector2 Normal, int TileX, int TileY, Door? Door, Actor? Actor);

/// <summary>
/// Collision queries and character movement. Characters are circles; walls are tiles;
/// props are rectangles; doors are thick rotating segments.
/// </summary>
public sealed class Physics
{
    private const int ResolveIterations = 3;
    private readonly GameWorld _world;

    internal Physics(GameWorld world) => _world = world;

    private TileMap Map => _world.Map;

    /// <summary>
    /// First obstacle matching <paramref name="mask"/> on the segment. Actors are tested only
    /// when <paramref name="actorFilter"/> is given.
    /// </summary>
    public bool Raycast(Vector2 from, Vector2 to, Blocking mask, out RayHit hit, Func<Actor, bool>? actorFilter = null)
    {
        Vector2 delta = to - from;
        hit = new RayHit(HitKind.None, 1f, to, Vector2.Zero, 0, 0, null, null);

        if (Map.Raycast(from, to, mask, out TileHit tileHit))
            hit = new RayHit(HitKind.Tile, tileHit.Fraction, tileHit.Point, tileHit.Normal, tileHit.X, tileHit.Y, null, null);

        foreach (Door door in _world.Doors)
        {
            if ((door.Blocking & mask) == 0) continue;
            if (Geo.SegmentIntersect(from, delta, door.Hinge, door.Tip, out float t) && t < hit.Fraction)
            {
                Vector2 normal = Geo.Perpendicular(door.Direction) * door.SideOf(from);
                hit = new RayHit(HitKind.Door, t, from + delta * t, normal, 0, 0, door, null);
            }
        }

        foreach (Prop prop in _world.Props)
        {
            if ((prop.Blocking & mask) == 0) continue;
            if (Geo.SegmentRect(from, delta, prop.Bounds, out float t, out Vector2 normal) && t < hit.Fraction)
                hit = new RayHit(HitKind.Prop, t, from + delta * t, normal, 0, 0, null, null);
        }

        if (actorFilter is not null)
        {
            foreach (Actor actor in _world.Actors)
            {
                if (!actorFilter(actor)) continue;
                if (Geo.SegmentCircle(from, delta, actor.Position, actor.Radius, out float t) && t < hit.Fraction)
                {
                    Vector2 point = from + delta * t;
                    hit = new RayHit(HitKind.Actor, t, point, Geo.NormalizeOrZero(point - actor.Position), 0, 0, null, actor);
                }
            }
        }

        return hit.Kind != HitKind.None;
    }

    public bool HasLineOfSight(Vector2 from, Vector2 to, Blocking mask) => !Raycast(from, to, mask, out _);

    /// <summary>
    /// True if a circle of <paramref name="radius"/> can travel in a straight line between two points
    /// (checked with three parallel rays). Doors are ignored: characters push them open.
    /// </summary>
    public bool CanWalkStraight(Vector2 from, Vector2 to, float radius)
    {
        Vector2 side = Geo.Perpendicular(Geo.NormalizeOrZero(to - from)) * radius;
        return !Map.Raycast(from, to, Blocking.Movement, out _)
            && !Map.Raycast(from + side, to + side, Blocking.Movement, out _)
            && !Map.Raycast(from - side, to - side, Blocking.Movement, out _)
            && !PropsBlock(from, to, Blocking.Movement)
            && !LockedDoorBlocks(from, to, side);
    }

    private bool LockedDoorBlocks(Vector2 from, Vector2 to, Vector2 side)
    {
        foreach (Door door in _world.Doors)
        {
            if (!door.Locked) continue;
            if (Geo.SegmentIntersect(from, to - from, door.Hinge, door.Tip, out _)
                || Geo.SegmentIntersect(from + side, to - from, door.Hinge, door.Tip, out _)
                || Geo.SegmentIntersect(from - side, to - from, door.Hinge, door.Tip, out _))
                return true;
        }

        return false;
    }

    private bool PropsBlock(Vector2 from, Vector2 to, Blocking mask)
    {
        foreach (Prop prop in _world.Props)
        {
            if ((prop.Blocking & mask) != 0 && Geo.SegmentRect(from, to - from, prop.Bounds, out _, out _)) return true;
        }
        return false;
    }

    /// <summary>Accelerates the actor toward its intent, moves it and resolves collisions.</summary>
    internal void MoveActor(Actor actor, float dt)
    {
        bool crawling = actor.State == ActorState.Wounded;
        Vector2 target = actor.CanMove || crawling
            ? Geo.ClampLength(actor.MoveIntent, 1f) * actor.MoveSpeed * (crawling ? 1f : actor.SpeedMultiplier)
            : Vector2.Zero;
        Vector2 velocity = actor.Velocity;
        if (actor.LeapPhase == LeapPhase.Airborne && actor.Definition.Ai.Leap is { } leap)
        {
            // In the air: full speed in a straight line at once.
            velocity = actor.LeapDirection * leap.Speed;
        }
        else
        {
            float rate = target == Vector2.Zero ? actor.Definition.Friction : actor.Definition.Acceleration;
            velocity += Geo.ClampLength(target - velocity, rate * dt);
        }

        Vector2 start = actor.Position;
        Vector2 displacement = velocity * dt;
        int substeps = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / (actor.Radius * 0.5f)));
        for (int i = 0; i < substeps; i++)
        {
            actor.Position += displacement / substeps;
            velocity = ResolveStatic(actor, velocity);
        }

        actor.Velocity = velocity;
        actor.DistanceWalked += Vector2.Distance(start, actor.Position);
    }

    /// <summary>Pushes the actor out of walls, props and doors; removes velocity going into them.</summary>
    internal Vector2 ResolveStatic(Actor actor, Vector2 velocity)
    {
        for (int iteration = 0; iteration < ResolveIterations; iteration++)
        {
            bool pushed = false;
            float r = actor.Radius;
            (int minX, int minY) = Map.TileAt(actor.Position - new Vector2(r));
            (int maxX, int maxY) = Map.TileAt(actor.Position + new Vector2(r));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (!Map.Blocks(x, y, Blocking.Movement)) continue;
                    if (Geo.CircleRectPush(actor.Position, r, Map.TileRect(x, y), out Vector2 push))
                    {
                        actor.Position += push;
                        velocity = RemoveInward(velocity, push);
                        pushed = true;
                    }
                }
            }

            foreach (Prop prop in _world.Props)
            {
                if (!prop.Definition.BlocksMovement) continue;
                if (Geo.CircleRectPush(actor.Position, r, prop.Bounds, out Vector2 push))
                {
                    actor.Position += push;
                    velocity = RemoveInward(velocity, push);
                    pushed = true;
                }
            }

            if (actor.IsAlive && !actor.IsDown)
            {
                foreach (Door door in _world.Doors)
                {
                    if (Geo.CircleCapsulePush(actor.Position, r, door.Hinge, door.Tip, door.HalfThickness, out Vector2 push))
                    {
                        // A door hit adds knockback to actor.Velocity; keep it in the local velocity.
                        Vector2 before = actor.Velocity;
                        _world.DoorLogic.OnContact(door, actor, velocity);
                        velocity += actor.Velocity - before;
                        actor.Position += push;
                        float into = -Vector2.Dot(velocity, Geo.NormalizeOrZero(push));
                        velocity = RemoveInward(velocity, push);
                        // A door wide open against the push: walk round its free end instead of sticking to it.
                        if (into > 0f && !door.Locked && door.CannotYieldTo(actor.Position)) velocity += door.Direction * into;
                        pushed = true;
                    }
                }
            }

            if (!pushed) break;
        }

        return velocity;
    }

    /// <summary>Soft separation between standing characters.</summary>
    internal void SeparateActors()
    {
        IReadOnlyList<Actor> actors = _world.Actors;
        for (int i = 0; i < actors.Count; i++)
        {
            Actor a = actors[i];
            if (!a.IsAlive || a.IsDown) continue;
            for (int j = i + 1; j < actors.Count; j++)
            {
                Actor b = actors[j];
                if (!b.IsAlive || b.IsDown) continue;
                Vector2 offset = b.Position - a.Position;
                float minDistance = a.Radius + b.Radius;
                float distanceSquared = offset.LengthSquared();
                if (distanceSquared >= minDistance * minDistance) continue;
                float distance = MathF.Sqrt(distanceSquared);
                Vector2 normal = distance > Geo.Epsilon ? offset / distance : Vector2.UnitX;
                Vector2 correction = normal * ((minDistance - distance) * 0.5f);
                a.Position -= correction;
                b.Position += correction;
                a.Velocity = ResolveStatic(a, a.Velocity);
                b.Velocity = ResolveStatic(b, b.Velocity);
            }
        }
    }

    private static Vector2 RemoveInward(Vector2 velocity, Vector2 push)
    {
        Vector2 normal = Geo.NormalizeOrZero(push);
        float into = Vector2.Dot(velocity, normal);
        return into < 0f ? velocity - normal * into : velocity;
    }
}
