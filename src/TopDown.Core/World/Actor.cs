using System.Numerics;
using TopDown.Core.Ai;
using TopDown.Core.Definitions;

namespace TopDown.Core.World;

public enum ActorState
{
    /// <summary>Free to move and fight.</summary>
    Active,
    /// <summary>Swinging a melee weapon; can still move.</summary>
    Attacking,
    /// <summary>Dazed on its feet: cannot act for a moment.</summary>
    Stunned,
    /// <summary>On the floor, alive, will get up after a delay.</summary>
    KnockedDown,
    /// <summary>Getting back up; still vulnerable.</summary>
    GettingUp,
    /// <summary>Finishing off a target.</summary>
    Executing,
    /// <summary>Pinned by an executioner: reserved, cannot get up.</summary>
    BeingExecuted,
    /// <summary>Badly hurt on the floor: crawls, never gets up, waits to be finished off.</summary>
    Wounded,
    /// <summary>Final: never acts or gets up again.</summary>
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

    /// <summary>Time spent in the current state (animations).</summary>
    public float StateElapsed { get; private set; }

    /// <summary>Number of melee attacks started (alternates the swing side).</summary>
    public int AttackCount { get; private set; }

    // --- Execution, executioner side ---
    public Actor? ExecutionTarget { get; private set; }
    public WeaponDefinition? ExecutionWeapon { get; private set; }
    public float ExecutionElapsed { get; private set; }
    public int ExecutionStrikesDone { get; internal set; }
    /// <summary>Blows this execution needs (the weapon's, or its variant against this victim).</summary>
    public int ExecutionStrikesNeeded { get; private set; } = 1;
    public ExecutionAnimation ExecutionAnimation { get; private set; }
    /// <summary>Side of the victim the executioner stands on (+1 its right, -1 its left).</summary>
    public int ExecutionSide { get; private set; } = 1;
    /// <summary>Earliest time (in <see cref="ExecutionElapsed"/>) of the next blow.</summary>
    public float NextStrikeAt { get; internal set; }
    /// <summary>Time (in <see cref="ExecutionElapsed"/>) of the last blow, 0 before the first.</summary>
    public float LastStrikeAt { get; internal set; }
    /// <summary>The player pressed attack during the execution: deliver the next blow.</summary>
    internal bool StrikeRequested { get; set; }
    /// <summary>Final blow delivered; waiting for the recovery to end.</summary>
    public bool ExecutionFinished { get; private set; }
    private float _recoveryLeft;

    // --- Execution, victim side ---
    /// <summary>Who is executing this character (the reservation), if anyone.</summary>
    public Actor? Executioner { get; private set; }
    private float _downTimeBeforeExecution;

    public EnemyBrain? Brain { get; internal set; }

    /// <summary>Distance travelled, used to animate legs.</summary>
    public float DistanceWalked { get; internal set; }

    public bool IsAlive => State != ActorState.Dead;
    /// <summary>On the floor (alive): knocked down, getting up or being executed.</summary>
    public bool IsDown => State is ActorState.KnockedDown or ActorState.GettingUp or ActorState.BeingExecuted or ActorState.Wounded;

    /// <summary>Wounded, or pinned while wounded: will never get up again.</summary>
    public bool IsWounded => State == ActorState.Wounded || (State == ActorState.BeingExecuted && _woundedBeforeExecution);

    /// <summary>The hit that wounded this character (credited if it bleeds out).</summary>
    internal Hit? WoundingHit { get; private set; }
    private bool _woundedBeforeExecution;
    public bool CanAct => State is ActorState.Active or ActorState.Attacking;
    public bool CanMove => CanAct;

    public bool IsHostileTo(Actor other) => other != this && other.Faction != Faction;

    internal bool BeginMeleeAttack(WeaponDefinition weapon, float time)
    {
        if (State != ActorState.Active) return false;
        SetState(ActorState.Attacking);
        CurrentAttack = weapon;
        AttackElapsed = 0f;
        AttackResolved = false;
        AttackCount++;
        StateTimer = MathF.Max(weapon.AttackDuration, weapon.HitDelay);
        LastAttackTime = time;
        return true;
    }

    internal void KnockDown(float duration)
    {
        if (!IsAlive || IsWounded) return;
        if (State == ActorState.BeingExecuted)
        {
            // Already pinned: the executioner decides when it ends.
            _downTimeBeforeExecution = MathF.Max(_downTimeBeforeExecution, duration);
            return;
        }

        CancelActions();
        SetState(ActorState.KnockedDown);
        StateTimer = duration;
    }

    internal void Stun(float duration)
    {
        if (duration <= 0f) return;
        if (State == ActorState.Stunned)
        {
            StateTimer = MathF.Max(StateTimer, duration);
            return;
        }

        if (!CanAct && State != ActorState.Executing) return;
        CancelActions();
        SetState(ActorState.Stunned);
        StateTimer = duration;
    }

    /// <summary>On the floor for good, crawling. Any further damage kills.</summary>
    internal void Wound(in Hit hit)
    {
        if (!IsAlive || IsWounded) return;
        CancelActions();
        Executioner = null;
        WoundingHit = hit;
        Health = 1f;
        SetState(ActorState.Wounded);
        StateTimer = 0f;
    }

    internal void Kill()
    {
        CancelActions();
        Executioner = null;
        SetState(ActorState.Dead);
        StateTimer = 0f;
        Health = MathF.Min(Health, 0f);
        MoveIntent = Vector2.Zero;
    }

    /// <summary>True when a new action can start (also during the recovery of a finished swing).</summary>
    internal bool IsFreeToAct => State == ActorState.Active || (State == ActorState.Attacking && AttackResolved);

    /// <summary>
    /// Starts finishing off <paramref name="target"/>. The target becomes <see cref="ActorState.BeingExecuted"/>,
    /// which reserves it: nobody else can execute it until this execution ends.
    /// </summary>
    internal bool BeginExecution(Actor target, WeaponDefinition weapon, float windUp,
        ExecutionAnimation animation = ExecutionAnimation.Smash, int strikes = 1, int side = 1)
    {
        if (!IsFreeToAct || target == this || !target.BecomeExecuted(this)) return false;
        CurrentAttack = null;
        AttackResolved = true;
        SetState(ActorState.Executing);
        ExecutionTarget = target;
        ExecutionWeapon = weapon;
        ExecutionAnimation = animation;
        ExecutionStrikesNeeded = Math.Max(1, strikes);
        ExecutionSide = side < 0 ? -1 : 1;
        ExecutionElapsed = 0f;
        ExecutionStrikesDone = 0;
        NextStrikeAt = windUp;
        LastStrikeAt = 0f;
        StrikeRequested = false;
        ExecutionFinished = false;
        MoveIntent = Vector2.Zero;
        return true;
    }

    /// <summary>The final blow landed: stay committed for a short recovery, then act again.</summary>
    internal void FinishExecution(float recovery)
    {
        if (State != ActorState.Executing) return;
        ExecutionFinished = true;
        ExecutionTarget = null;
        _recoveryLeft = recovery;
        if (recovery <= 0f) EndExecution();
    }

    /// <summary>Ends (or cancels) the execution and frees the target if it is still pinned.</summary>
    internal void EndExecution()
    {
        if (State != ActorState.Executing) return;
        ReleaseTarget();
        ExecutionWeapon = null;
        ExecutionFinished = false;
        SetState(ActorState.Active);
    }

    private bool BecomeExecuted(Actor executioner)
    {
        if (!IsAlive || Executioner is not null) return false;
        if (State is not (ActorState.KnockedDown or ActorState.GettingUp or ActorState.Stunned or ActorState.Wounded)) return false;
        // A getting-up or dazed target ends on the floor for a while if the execution is interrupted.
        _downTimeBeforeExecution = State == ActorState.KnockedDown ? StateTimer : 0f;
        _woundedBeforeExecution = State == ActorState.Wounded;
        Executioner = executioner;
        CancelActions();
        SetState(ActorState.BeingExecuted);
        return true;
    }

    /// <summary>Execution interrupted: back on the floor (never straight back up).</summary>
    private void ReleaseFromExecution()
    {
        if (State != ActorState.BeingExecuted) return;
        Executioner = null;
        if (_woundedBeforeExecution)
        {
            SetState(ActorState.Wounded);
            return;
        }

        SetState(ActorState.KnockedDown);
        StateTimer = MathF.Max(_downTimeBeforeExecution, MinimumDownTimeAfterRelease);
    }

    private void ReleaseTarget()
    {
        if (ExecutionTarget is { } target && target.Executioner == this) target.ReleaseFromExecution();
        ExecutionTarget = null;
    }

    /// <summary>Advances timers and timed states.</summary>
    internal void TickState(float dt)
    {
        AttackCooldown = MathF.Max(0f, AttackCooldown - dt);
        StateElapsed += dt;
        switch (State)
        {
            case ActorState.Attacking:
                AttackElapsed += dt;
                StateTimer -= dt;
                if (StateTimer <= 0f && AttackResolved)
                {
                    CurrentAttack = null;
                    SetState(ActorState.Active);
                }
                break;
            case ActorState.Stunned:
                StateTimer -= dt;
                if (StateTimer <= 0f) SetState(ActorState.Active);
                break;
            case ActorState.KnockedDown:
                StateTimer -= dt;
                if (StateTimer <= 0f)
                {
                    SetState(ActorState.GettingUp);
                    StateTimer = Definition.GetUpDuration;
                }
                break;
            case ActorState.GettingUp:
                StateTimer -= dt;
                if (StateTimer <= 0f) SetState(ActorState.Active);
                break;
            case ActorState.Executing:
                ExecutionElapsed += dt;
                if (ExecutionFinished)
                {
                    _recoveryLeft -= dt;
                    if (_recoveryLeft <= 0f) EndExecution();
                }
                break;
        }
    }

    /// <summary>Shortest time on the floor after an interrupted execution.</summary>
    private const float MinimumDownTimeAfterRelease = 0.6f;

    private void SetState(ActorState state)
    {
        State = state;
        StateElapsed = 0f;
    }

    private void CancelActions()
    {
        CurrentAttack = null;
        AttackResolved = true;
        ReleaseTarget();
        ExecutionWeapon = null;
        ExecutionFinished = false;
        MoveIntent = Vector2.Zero;
    }
}
