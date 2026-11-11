using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>
/// Where an executioner stands and which way it faces, for each execution animation. Bodies lie
/// with the head towards their facing direction and their right side towards +90°.
/// </summary>
public static class ExecutionPose
{
    /// <summary>Distance from a lying body's centre to its head, in body radii.</summary>
    public const float HeadDistance = 1.3f;

    /// <summary>Head of a character lying on the floor.</summary>
    public static Vector2 HeadOf(Actor victim) => victim.Position + Geo.FromAngle(victim.Facing) * (victim.Radius * HeadDistance);

    /// <summary>Kneeling on the body rather than standing beside it.</summary>
    public static bool IsAstride(ExecutionAnimation animation) =>
        animation is ExecutionAnimation.HeadBash or ExecutionAnimation.Stab or ExecutionAnimation.TearLegs or ExecutionAnimation.TearHead;

    /// <summary>Side (+1 right, -1 left of the victim) the executioner is already on.</summary>
    public static int SideOf(Actor executioner, Actor victim)
    {
        Vector2 right = Geo.Perpendicular(Geo.FromAngle(victim.Facing));
        return Vector2.Dot(executioner.Position - victim.Position, right) < 0f ? -1 : 1;
    }

    public static (Vector2 Position, float Facing) For(Actor executioner, Actor victim, ExecutionAnimation animation, int side)
    {
        Vector2 forward = Geo.FromAngle(victim.Facing);
        Vector2 right = Geo.Perpendicular(forward);
        float radius = victim.Radius;
        Vector2 head = HeadOf(victim);
        switch (animation)
        {
            case ExecutionAnimation.HeadBash:
                return (victim.Position + forward * (radius * 0.3f), victim.Facing);
            case ExecutionAnimation.Stab:
                return (victim.Position + forward * (radius * 0.1f), victim.Facing);
            case ExecutionAnimation.TearHead:
                return (victim.Position, victim.Facing);
            case ExecutionAnimation.TearLegs:
                // Legs stick out on the right side of a beast lying on its flank.
                return (victim.Position - right * (radius * 0.25f), victim.Facing + MathF.PI / 2f);
            case ExecutionAnimation.Bite:
            {
                Vector2 above = head + forward * (executioner.Radius + 2f);
                return (above, Geo.ToAngle(head - above));
            }
        }

        float gap = animation switch
        {
            ExecutionAnimation.Stomp => 3f,
            ExecutionAnimation.Shoot => 8f,
            _ => 10f,
        };
        Vector2 position = head - forward * (radius * 0.2f) + right * (side * (executioner.Radius + gap));
        return (position, Geo.ToAngle(head - position));
    }
}
