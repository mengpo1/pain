using System.Numerics;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>Door swinging and door/character contacts.</summary>
internal sealed class DoorSystem
{
    /// <summary>Speed at which a character walking into a door starts pushing it (units/s).</summary>
    private const float PushThreshold = 15f;
    private const float BounceFactor = -0.3f;
    private readonly GameWorld _world;

    public DoorSystem(GameWorld world) => _world = world;

    public void Update(float dt)
    {
        foreach (Door door in _world.Doors) door.Integrate(dt);
    }

    /// <summary>
    /// Called while a standing character overlaps a door. A character walking into a still door
    /// kicks it open; a door swinging fast toward a character knocks it down.
    /// </summary>
    public void OnContact(Door door, Actor actor, Vector2 velocity)
    {
        int side = door.SideOf(actor.Position);
        bool movingTowardActor = door.IsMoving && MathF.Sign(door.AngularVelocity) == side;

        if (movingTowardActor && door.LastPusher != actor)
        {
            float speed = MathF.Abs(door.AngularVelocity);
            if (_world.Rules.DoorsKnockDown && speed >= Geo.DegToRad(door.Definition.KnockdownSpeedDegrees)
                && actor.CanAct && !actor.Definition.KnockdownImmune)
            {
                Vector2 direction = Geo.Perpendicular(door.Direction) * side;
                _world.Combat.ApplyHit(actor, new Hit(0f, "door", true, door.Definition.KnockdownDuration,
                    direction, 160f, door.LastPusher, HitMethod.Door));
                _world.AddEvent(new DoorHit(door, speed));
                _world.EmitNoise(door.Hinge, door.Definition.HitNoiseRadius, door.LastPusher);
            }

            door.AngularVelocity *= BounceFactor;
            return;
        }

        if (door.Locked) return;
        Vector2 towardDoor = -Geo.Perpendicular(door.Direction) * side;
        if (Vector2.Dot(velocity, towardDoor) < PushThreshold) return;

        float push = -side * Geo.DegToRad(door.Definition.PushSpeedDegrees);
        bool wasStill = !door.IsMoving;
        if (MathF.Sign(door.AngularVelocity) != MathF.Sign(push) || MathF.Abs(door.AngularVelocity) < MathF.Abs(push))
            door.AngularVelocity = push;
        door.LastPusher = actor;
        if (wasStill) _world.AddEvent(new DoorPushed(door));
    }
}
