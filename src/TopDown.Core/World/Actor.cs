using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;

namespace TopDown.Core.World;

public enum ActorState
{
    Active,
    Attacking,
    Stunned,
    KnockedDown,
    GettingUp,
    Executing,
    Dead,
}

/// <summary>
/// A character: the player or an enemy. Both share the same rules; only the controller differs
/// (player commands or an <see cref="EnemyBrain"/>). State changes go through methods that keep
/// the state machine consistent (a dead actor cannot attack, a knocked-down one cannot move…).
/// </summary>
public sealed class Actor
{
    internal Actor(int id, ActorDefinition definition, bool isPlayer, Vector2 position, float facing)
    {
        Id = id;
        Definition = definition;
        IsPlayer = isPlayer;
        Position = position;
        PreviousPosition = position;
        Facing = facing;
        PreviousFacing = facing;
        Health = definition.Health;
    }

    public int Id { get; }
    public ActorDefinition Definition { get; }
    public bool IsPlayer { get; }
    public string Faction => Definition.Faction;
    public float Radius => Definition.Radius;

    /// <summary>Name given in the level file (for triggers), if any.</summary>
    public string? LevelId { get; internal set; }
    public string? Tag { get; internal set; }

    public Vector2 Position { get; internal set; }
    public Vector2 PreviousPosition { get; internal set; }
    public Vector2 Velocity { get; internal set; }

    /// <summary>Orientation in radians (0 = +X, clockwise on screen because Y points down).</summary>
    public float Facing { get; internal set; }
    public float PreviousFacing { get; internal set; }

    /// <summary>Wanted movement direction, length ≤ 1, set by the controller each step.</summary>
    public Vector2 MoveIntent { get; internal set; }

    /// <summary>Speed matching <see cref="MoveIntent"/>.</summary>
    public float MoveSpeed { get; internal set; }

    public float SpeedMultiplier { get; internal set; } = 1f;
    public float Health { get; internal set; }

    public ActorState State { get; private set; } = ActorState.Active;

    /// <summary>Time left in the current timed state.</summary>
    public float StateTimer { get; private set; }

    public WeaponInstance? Weapon { get; internal set; }
    public float AttackCooldown { get; internal set; }

    /// <summary>Melee attack in progress, if any.</summary>
    public WeaponDefinition? CurrentAttack { get; private set; }
    public float AttackElapsed { get; private set; }
    public bool AttackResolved { get; internal set; }

    /// <summary>World time of the last attack (for animations).</summary>
    public float LastAttackTime { get; internal set; } = float.NegativeInfinity;

    public Actor? ExecutionTarget { get; private set; }
    public float ExecutionElapsed { get; private set; }
    public float ExecutionDuration { get; private set; }

    public EnemyBrain? Brain { get; internal set; }

    /// <summary>Distance travelled, used to animate legs.</summary>
    public float DistanceWalked { get; internal set; }

    public bool IsAlive => State != ActorState.Dead;
    public bool IsDown => State is ActorState.KnockedDown or ActorState.GettingUp;
    public bool CanAct => State is ActorState.Active or ActorState.Attacking;
    public bool CanMove => CanAct;

    public bool IsHostileTo(Actor other) => other != this && other.Faction != Faction;

    internal bool BeginMeleeAttack(WeaponDefinition weapon, float time)
    {
        if (State != ActorState.Active) return false;
        State = ActorState.Attacking;
        CurrentAttack = weapon;
        AttackElapsed = 0f;
        AttackResolved = false;
        StateTimer = MathF.Max(weapon.AttackDuration, weapon.HitDelay);
        LastAttackTime = time;
        return true;
    }

    internal void KnockDown(float duration)
    {
        if (!IsAlive) return;
        CancelActions();
        State = ActorState.KnockedDown;
        StateTimer = duration;
    }

    internal void Stun(float duration)
    {
        if (!CanAct || duration <= 0f) return;
        CancelActions();
        State = ActorState.Stunned;
        StateTimer = duration;
    }

    internal void Kill()
    {
        CancelActions();
        State = ActorState.Dead;
        StateTimer = 0f;
        Health = MathF.Min(Health, 0f);
        MoveIntent = Vector2.Zero;
    }

    /// <summary>Keeps a knocked-down actor on the floor for at least <paramref name="duration"/>.</summary>
    internal void Pin(float duration)
    {
        if (State == ActorState.KnockedDown) StateTimer = MathF.Max(StateTimer, duration);
    }

    /// <summary>True when a new action can start (also during the recovery of a finished swing).</summary>
    internal bool IsFreeToAct => State == ActorState.Active || (State == ActorState.Attacking && AttackResolved);

    internal bool BeginExecution(Actor target, float duration)
    {
        if (!IsFreeToAct || target.State != ActorState.KnockedDown) return false;
        CurrentAttack = null;
        State = ActorState.Executing;
        ExecutionTarget = target;
        ExecutionElapsed = 0f;
        ExecutionDuration = duration;
        MoveIntent = Vector2.Zero;
        target.Pin(duration + 0.3f);
        return true;
    }

    internal void EndExecution()
    {
        if (State != ActorState.Executing) return;
        ExecutionTarget = null;
        State = ActorState.Active;
    }

    internal void Revive(float health)
    {
        Health = health;
        State = ActorState.Active;
    }

    /// <summary>Advances timers and timed states.</summary>
    internal void TickState(float dt)
    {
        AttackCooldown = MathF.Max(0f, AttackCooldown - dt);
        switch (State)
        {
            case ActorState.Attacking:
                AttackElapsed += dt;
                StateTimer -= dt;
                if (StateTimer <= 0f && AttackResolved)
                {
                    CurrentAttack = null;
                    State = ActorState.Active;
                }
                break;
            case ActorState.Stunned:
                StateTimer -= dt;
                if (StateTimer <= 0f) State = ActorState.Active;
                break;
            case ActorState.KnockedDown:
                StateTimer -= dt;
                if (StateTimer <= 0f)
                {
                    State = ActorState.GettingUp;
                    StateTimer = Definition.GetUpDuration;
                }
                break;
            case ActorState.GettingUp:
                StateTimer -= dt;
                if (StateTimer <= 0f) State = ActorState.Active;
                break;
            case ActorState.Executing:
                ExecutionElapsed += dt;
                break;
        }
    }

    private void CancelActions()
    {
        CurrentAttack = null;
        AttackResolved = true;
        ExecutionTarget = null;
        MoveIntent = Vector2.Zero;
    }
}
