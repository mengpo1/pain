using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>
/// A swinging door: a segment rotating around its hinge. Characters push it open; a door
/// swinging fast enough knocks down whoever stands on the other side.
/// </summary>
public sealed class Door
{
    internal Door(DoorDefinition definition, Vector2 hinge, float closedAngle, float length, bool locked, string? levelId)
    {
        Definition = definition;
        Hinge = hinge;
        ClosedAngle = closedAngle;
        Length = length;
        Locked = locked;
        LevelId = levelId;
    }

    public DoorDefinition Definition { get; }
    public string? LevelId { get; }
    public Vector2 Hinge { get; }
    public float ClosedAngle { get; }
    public float Length { get; }
    public bool Locked { get; internal set; }

    /// <summary>Opening angle relative to the closed position (radians).</summary>
    public float Angle { get; internal set; }
    public float PreviousAngle { get; internal set; }

    /// <summary>Angular speed (radians/s). Positive values move the door toward its left normal.</summary>
    public float AngularVelocity { get; internal set; }

    /// <summary>Last actor that pushed the door: it cannot be hit by its own push.</summary>
    internal Actor? LastPusher { get; set; }

    public float HalfThickness => Definition.Thickness * 0.5f;
    public float WorldAngle => ClosedAngle + Angle;
    public Vector2 Direction => Geo.FromAngle(WorldAngle);
    public Vector2 Tip => Hinge + Direction * Length;
    public bool IsMoving => MathF.Abs(AngularVelocity) > 0.05f;

    public Blocking Blocking =>
        Blocking.Movement
        | (Definition.BlocksProjectiles ? Blocking.Projectiles : Blocking.None)
        | (Definition.BlocksVision ? Blocking.Vision : Blocking.None);

    /// <summary>+1 if the point is on the door's left-normal side, -1 otherwise.</summary>
    public int SideOf(Vector2 point) => Geo.Cross(Direction, point - Hinge) >= 0f ? 1 : -1;

    internal void Integrate(float dt)
    {
        PreviousAngle = Angle;
        if (Locked)
        {
            AngularVelocity = 0f;
            return;
        }

        if (Definition.CloseSpring > 0f) AngularVelocity -= Angle * Definition.CloseSpring * dt;
        AngularVelocity *= MathF.Exp(-Definition.Damping * dt);
        if (MathF.Abs(AngularVelocity) < 0.02f) AngularVelocity = 0f;

        Angle += AngularVelocity * dt;
        float max = Geo.DegToRad(Definition.MaxAngleDegrees);
        if (Angle > max || Angle < -max)
        {
            Angle = Math.Clamp(Angle, -max, max);
            AngularVelocity = -AngularVelocity * 0.25f;
        }

        if (!IsMoving) LastPusher = null;
    }

    /// <summary>
    /// Already open as far as it goes in the direction a character at <paramref name="position"/>
    /// would push it: pushing harder will not move it.
    /// </summary>
    internal bool CannotYieldTo(Vector2 position)
    {
        if (Locked) return true;
        float push = -SideOf(position);
        float max = Geo.DegToRad(Definition.MaxAngleDegrees);
        return MathF.Abs(Angle) >= max - 0.02f && MathF.Sign(Angle) == MathF.Sign(push);
    }
}
