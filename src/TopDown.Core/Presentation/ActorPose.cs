using TopDown.Core.Ai;
using TopDown.Core.Definitions;
using TopDown.Core.World;

namespace TopDown.Core.Presentation;

/// <summary>
/// Which body animation a character shows and how far into it: keys from the most specific to the
/// most generic (the renderer uses the first one the character's animation set defines) and the time
/// to read the frame at. Pure presentation: it only reads the simulation, which never depends on it.
/// With <paramref name="FromImpact"/>, the time is counted from the blow (negative before it): the renderer
/// lines it up with the frame the animation marks with an "impact" event, so the contact drawn is always
/// the moment the simulation applies the hit, whatever the weapon's timing.
/// </summary>
public readonly record struct Pose(IReadOnlyList<string> Keys, float Time, bool FromImpact = false);

public static class ActorPose
{
    /// <summary>World units walked per second of walk animation time.</summary>
    public const float WalkUnitsPerSecond = 64f;

    /// <summary>A character still shows its firing pose this long after a shot.</summary>
    public const float FireHold = 0.25f;

    /// <summary>A creature shows its "hit" reaction this long after being hurt.</summary>
    public const float HitHold = 0.22f;

    /// <summary>Time far past the end of any one-shot animation (its last frame).</summary>
    public const float Settled = 1000f;

    public static Pose Of(Actor actor, GameWorld world)
    {
        string pose = world.Combat.CurrentWeapon(actor).PoseName;
        switch (actor.State)
        {
            case ActorState.Dead:
            {
                string kind = DeathKind(actor.LastDamageType);
                float time = actor.DiedOnTheFloor ? Settled : actor.StateElapsed;
                return new Pose([$"death.{kind}", "death", "knockdown"], time);
            }
            case ActorState.KnockedDown:
                return new Pose(["knockdown"], actor.StateElapsed);
            case ActorState.BeingExecuted:
                return new Pose(["knockdown"], Settled);
            case ActorState.GettingUp:
                return new Pose(["getup", "knockdown"], actor.StateElapsed);
            case ActorState.Wounded:
                return new Pose(["crawl"], actor.DistanceWalked / 20f);
            case ActorState.Stunned:
                return new Pose([$"hurt.{pose}", "hurt", "hit", $"idle.{pose}", "idle"], actor.StateElapsed);
            case ActorState.Leaping:
                return actor.LeapPhase switch
                {
                    LeapPhase.Crouch => new Pose(["leap.crouch", "idle"], actor.LeapPhaseElapsed),
                    LeapPhase.Airborne => new Pose(["leap.air", "walk", "idle"], actor.LeapPhaseElapsed),
                    _ => new Pose(["leap.land", "idle"], actor.LeapPhaseElapsed),
                };
            case ActorState.Attacking when actor.CurrentAttack is { } attack:
            {
                string key = $"attack.{attack.PoseName}";
                // Alternating sides (left and right punches) when the set has both.
                string[] keys = actor.AttackCount % 2 == 0 ? [key + ".alt", key, "attack"] : [key, "attack"];
                return new Pose(keys, actor.AttackElapsed - attack.HitDelay, true);
            }
            case ActorState.Executing:
                return ExecutionPose(actor, pose);
        }

        if (actor.Brain is { State: AiState.Awakening } waking)
            return new Pose(["awaken", "idle"], waking.StateTime);

        float moving = actor.Velocity.Length();
        if (actor.Brain is { State: AiState.Ambush or AiState.Dormant } && moving < 20f)
            return new Pose(["rest", $"idle.{pose}", "idle"], world.Time);

        float sinceHurt = world.Time - actor.LastHurtTime;
        if (sinceHurt < HitHold && actor.IsAlive) return new Pose(["hit", $"idle.{pose}", "idle"], sinceHurt);

        float sinceShot = world.Time - actor.LastAttackTime;
        if (actor.Weapon is { Definition.IsFirearm: true } && sinceShot < FireHold)
            return new Pose([$"fire.{pose}", $"idle.{pose}", "idle"], sinceShot);

        if (moving > 20f)
            return new Pose([$"walk.{pose}", "walk", $"idle.{pose}", "idle"], actor.DistanceWalked / WalkUnitsPerSecond);

        return new Pose([$"idle.{pose}", "idle"], world.Time);
    }

    /// <summary>
    /// One cycle of the execution animation per blow, counted from the blow: the wind-up leads to the
    /// first impact, each later blow restarts at its impact frame. Shots replay the firing pose.
    /// </summary>
    private static Pose ExecutionPose(Actor actor, string pose)
    {
        float since = actor.ExecutionStrikesDone > 0 ? actor.ExecutionElapsed - actor.LastStrikeAt : -1f;
        if (actor.ExecutionAnimation == ExecutionAnimation.Shoot)
            return new Pose([$"fire.{pose}", $"idle.{pose}", "idle"], since < 0f ? Settled : since);
        float windUp = actor.ExecutionWeapon?.Execution.WindUp ?? 0f;
        float time = since < 0f ? actor.ExecutionElapsed - windUp : since;
        return new Pose([$"execute.{actor.ExecutionAnimation.ToString().ToLowerInvariant()}", $"attack.{pose}", $"idle.{pose}", "idle"], time, true);
    }

    /// <summary>How a character died, from the last damage it took: "shot", "cut" or "blunt".</summary>
    public static string DeathKind(string? damageType) => damageType switch
    {
        "bullet" or "fire" => "shot",
        "sharp" => "cut",
        _ => "blunt",
    };
}
