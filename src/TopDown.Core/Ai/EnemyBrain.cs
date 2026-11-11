using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;
using TopDown.Core.World;

namespace TopDown.Core.Ai;

public enum AiState
{
    Dormant,
    Idle,
    Patrol,
    Wander,
    Suspicious,
    Investigate,
    Chase,
    Search,
    Return,
    /// <summary>Lying in wait, blind and still, until the target comes close.</summary>
    Ambush,
}

/// <summary>
/// One state machine shared by every enemy. Definitions only change its parameters
/// (vision, hearing, speed, reaction time…) and its idle behaviour.
/// </summary>
public sealed class EnemyBrain
{
    private const float WaypointReach = 6f;
    private const float RepathInterval = 0.45f;
    private const float StuckCheckInterval = 0.8f;
    private const float MeleeFacingTolerance = 0.45f;
    private const float FirearmFacingTolerance = 0.2f;
    private const int MaxRepathAttempts = 3;
    /// <summary>A target hidden longer than this needs a new (shorter) reaction time when it reappears.</summary>
    private const float ReappearDelay = 0.3f;
    /// <summary>A gunman whose shot is blocked this long moves to get a clear line of fire.</summary>
    private const float BlockedShotPatience = 0.6f;
    /// <summary>Distance kept from a target an ally is executing.</summary>
    private const float WaitingDistance = 52f;
    /// <summary>Beyond this, a gunman shoots a downed target instead of walking to it.</summary>
    private const float ShootRatherThanExecuteDistance = 90f;
    private const float SpreadMargin = 14f;
    private const float SpreadStrength = 1.4f;

    private readonly Actor _self;
    private readonly GameWorld _world;
    private readonly AiDefinition _ai;
    private readonly AiBehavior _behavior;
    private readonly List<Vector2> _patrol;
    private readonly List<Vector2> _path = new();
    private readonly Vector2 _home;
    private readonly float _homeFacing;

    private int _patrolIndex;
    private int _pathIndex;
    private Vector2 _pathGoal = new(float.NaN);
    private float _repathTimer;
    private float _stuckTimer;
    private Vector2 _stuckCheckPosition;
    private int _repathAttempts;

    private float _stateTime;
    private float _reactionTimer;
    private float _pauseTimer;
    private float _lookTimer;
    private float _lookAngle;
    private Vector2 _pointOfInterest;
    private Vector2? _wanderGoal;
    private bool _wasIncapacitated;

    // Wounded: crawling rhythm and way around obstacles.
    private float _crawlTime;
    private float _stuckTime;
    private float _detourTime;
    private int _detourSide = 1;
    private float _blockedShotTime;

    internal EnemyBrain(Actor self, GameWorld world, AiBehavior behavior, List<Vector2> patrol)
    {
        _self = self;
        _world = world;
        _ai = self.Definition.Ai;
        _behavior = behavior;
        _patrol = patrol;
        _home = self.Position;
        _homeFacing = self.Facing;
        _lookAngle = self.Facing;
        State = behavior == AiBehavior.Dormant ? AiState.Dormant : RestingState();
    }

    public AiState State { get; private set; }
    public Actor? Target { get; private set; }
    public bool CanSeeTarget { get; private set; }
    public Vector2 LastKnownTargetPosition { get; private set; }
    public float TimeSinceTargetSeen { get; private set; } = float.PositiveInfinity;
    public IReadOnlyList<Vector2> Path => _path;
    public int PathIndex => _pathIndex;

    public float VisionRange => _ai.VisionRange * _world.Modifier.EnemyVision;
    public float VisionAngle => Geo.DegToRad(_ai.VisionAngleDegrees);
    public float CloseDetectionRadius => _ai.CloseDetectionRadius;

    /// <summary>Wakes up a dormant enemy (level trigger).</summary>
    public void Activate()
    {
        if (State == AiState.Dormant) Enter(_patrol.Count > 0 ? AiState.Patrol : AiState.Wander);
    }

    /// <summary>Makes the enemy hunt <paramref name="target"/> even without seeing it.</summary>
    public void AlertTo(Actor target)
    {
        if (!target.IsAlive || !_self.IsHostileTo(target)) return;
        Target = target;
        LastKnownTargetPosition = target.Position;
        TimeSinceTargetSeen = 0f;
        if (State != AiState.Chase) BecomeAlerted(target, shareAlert: false);
    }

    internal void OnNoise(Vector2 position, Actor? source)
    {
        if (State == AiState.Dormant) return;
        if (State == AiState.Ambush)
        {
            // Only a noise made close by its prey gives away the ambush.
            if (source is not null && _self.IsHostileTo(source) && Vector2.Distance(position, _self.Position) <= _ai.WakeRadius * 2f)
                WakeUp(source, position);
            return;
        }

        if (State == AiState.Chase)
        {
            if (!CanSeeTarget && source is not null && source == Target) LastKnownTargetPosition = position;
            return;
        }

        _pointOfInterest = position;
        if (State is AiState.Suspicious or AiState.Investigate) return;
        Enter(AiState.Suspicious);
    }

    internal void OnAttacked(Actor attacker)
    {
        if (!_self.IsHostileTo(attacker)) return;
        Target = attacker;
        LastKnownTargetPosition = attacker.Position;
        TimeSinceTargetSeen = 0f;
        if (State != AiState.Chase) BecomeAlerted(attacker, shareAlert: true);
    }

    internal void Update(float dt)
    {
        if (!_self.IsAlive)
        {
            _self.MoveIntent = Vector2.Zero;
            return;
        }

        if (_self.State == ActorState.Wounded)
        {
            Crawl(dt);
            return;
        }

        if (!_self.CanAct)
        {
            _self.MoveIntent = Vector2.Zero;
            // Executing and leaping are actions of their own, not an incapacity.
            if (_self.State is not (ActorState.Executing or ActorState.Leaping)) _wasIncapacitated = true;
            return;
        }

        if (_wasIncapacitated)
        {
            // Back on its feet: it knows something is going on.
            _wasIncapacitated = false;
            _reactionTimer = _ai.ReactionTime;
            if (Target is not null && State != AiState.Chase && State != AiState.Dormant) Enter(AiState.Chase);
        }

        _stateTime += dt;
        _repathTimer -= dt;
        if (State == AiState.Dormant)
        {
            _self.MoveIntent = Vector2.Zero;
            return;
        }

        if (State == AiState.Ambush)
        {
            Stop();
            if (PreyWithinWakeRadius() is { } prey) WakeUp(prey, prey.Position);
            return;
        }

        Perceive(dt);
        switch (State)
        {
            case AiState.Idle: UpdateIdle(dt); break;
            case AiState.Patrol: UpdatePatrol(dt); break;
            case AiState.Wander: UpdateWander(dt); break;
            case AiState.Suspicious: UpdateSuspicious(dt); break;
            case AiState.Investigate: UpdateInvestigate(dt); break;
            case AiState.Chase: UpdateChase(dt); break;
            case AiState.Search: UpdateSearch(dt); break;
            case AiState.Return: UpdateReturn(dt); break;
        }
    }

    /// <summary>
    /// Wounded: drags itself head first away from the player, in painful pulls with pauses in
    /// between, and turns along walls when stuck. It never attacks again.
    /// </summary>
    private void Crawl(float dt)
    {
        if (_self.Definition.Wound is not { } wound)
        {
            _self.MoveIntent = Vector2.Zero;
            return;
        }

        _crawlTime += dt;
        Vector2 away = _self.Position - _world.Player.Position;
        float wanted = away.LengthSquared() > 1f && away.Length() < CrawlFleeDistance ? Geo.ToAngle(away) : _self.Facing;

        float pull = MathF.Max(0f, MathF.Sin(_crawlTime * MathF.Tau / CrawlCycle));
        bool pulling = pull > 0.3f;
        if (_detourTime > 0f)
        {
            _detourTime -= dt;
            wanted = Geo.ToAngle(away) + _detourSide * MathF.PI / 2f;
        }
        else if (pulling && _self.Velocity.Length() < wound.CrawlSpeed * 0.15f)
        {
            _stuckTime += dt;
            if (_stuckTime > CrawlStuckTime)
            {
                _stuckTime = 0f;
                _detourTime = CrawlDetourTime;
                _detourSide = -_detourSide;
            }
        }
        else if (pulling)
        {
            _stuckTime = 0f;
        }

        _self.Facing = Geo.RotateTowards(_self.Facing, wanted, CrawlTurnSpeed * dt);
        _self.MoveIntent = Geo.FromAngle(_self.Facing);
        _self.MoveSpeed = wound.CrawlSpeed * (0.25f + 0.75f * pull);
    }

    /// <summary>Beyond this distance from the player, a wounded character keeps crawling straight on.</summary>
    private const float CrawlFleeDistance = 420f;
    private const float CrawlCycle = 0.9f;
    private const float CrawlTurnSpeed = 2.5f;
    private const float CrawlStuckTime = 0.5f;
    private const float CrawlDetourTime = 1.2f;

    /// <summary>Leaves the ambush and hunts <paramref name="prey"/>, last seen (or heard) at <paramref name="at"/>.</summary>
    private void WakeUp(Actor prey, Vector2 at)
    {
        Target = prey;
        LastKnownTargetPosition = at;
        TimeSinceTargetSeen = 0f;
        BecomeAlerted(prey, shareAlert: true);
    }

    /// <summary>The player, if it came within the wake radius in clear view.</summary>
    private Actor? PreyWithinWakeRadius()
    {
        Actor player = _world.Player;
        if (!player.IsAlive || !_self.IsHostileTo(player)) return null;
        if (Vector2.Distance(player.Position, _self.Position) > _ai.WakeRadius + player.Radius) return null;
        return _world.Physics.HasLineOfSight(_self.Position, player.Position, Blocking.Vision) ? player : null;
    }

    /// <summary>Distance, field of view and line of sight (walls and closed doors block vision).</summary>
    public bool CanSee(Actor target)
    {
        if (!target.IsAlive) return false;
        Vector2 toTarget = target.Position - _self.Position;
        float distance = toTarget.Length();
        if (distance > VisionRange + target.Radius) return false;
        if (distance > CloseDetectionRadius + target.Radius
            && MathF.Abs(Geo.AngleDifference(_self.Facing, Geo.ToAngle(toTarget))) > VisionAngle * 0.5f)
            return false;
        return _world.Physics.HasLineOfSight(_self.Position, target.Position, Blocking.Vision);
    }

    private void Perceive(float dt)
    {
        Actor player = _world.Player;
        bool sawBefore = CanSeeTarget;
        CanSeeTarget = _self.IsHostileTo(player) && CanSee(player);
        if (CanSeeTarget && !sawBefore && State == AiState.Chase && TimeSinceTargetSeen > ReappearDelay)
            _reactionTimer = _ai.ReactionTime * 0.6f;
        if (CanSeeTarget)
        {
            Target = player;
            LastKnownTargetPosition = player.Position;
            TimeSinceTargetSeen = 0f;
            if (State != AiState.Chase) BecomeAlerted(player, shareAlert: true);
        }
        else
        {
            TimeSinceTargetSeen += dt;
        }
    }

    private void BecomeAlerted(Actor target, bool shareAlert)
    {
        Target = target;
        _reactionTimer = _ai.ReactionTime;
        Enter(AiState.Chase);
        _world.AddEvent(new EnemyAlerted(_self));
        if (!shareAlert || _ai.AlertAlliesRadius <= 0f) return;
        foreach (Actor ally in _world.Actors)
        {
            if (ally == _self || ally.Brain is null || !ally.IsAlive || ally.Faction != _self.Faction) continue;
            if (Vector2.Distance(ally.Position, _self.Position) > _ai.AlertAlliesRadius) continue;
            // Allies only react to what they can see: no telepathy through walls.
            if (_world.Physics.HasLineOfSight(ally.Position, _self.Position, Blocking.Vision)) ally.Brain.AlertTo(target);
        }
    }

    private void UpdateIdle(float dt)
    {
        if (Vector2.Distance(_self.Position, _home) > WaypointReach * 2f)
        {
            Enter(AiState.Return);
            return;
        }

        Stop();
        LookAround(dt, _homeFacing, spread: 0.9f, interval: 2.5f, turnFactor: 0.4f);
    }

    private void UpdatePatrol(float dt)
    {
        if (_patrol.Count == 0)
        {
            Enter(AiState.Idle);
            return;
        }

        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            Stop();
            Vector2 next = _patrol[_patrolIndex];
            if (Vector2.DistanceSquared(next, _self.Position) > 1f) TurnTowards(Geo.ToAngle(next - _self.Position), dt, 0.5f);
            return;
        }

        if (MoveTo(_patrol[_patrolIndex], _self.Definition.Speed, dt))
        {
            _patrolIndex = (_patrolIndex + 1) % _patrol.Count;
            _pauseTimer = _ai.PatrolPause;
        }
    }

    private void UpdateWander(float dt)
    {
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            Stop();
            LookAround(dt, _self.Facing, spread: 1.2f, interval: 1.5f, turnFactor: 0.4f);
            return;
        }

        _wanderGoal ??= PickWanderGoal();
        if (_wanderGoal is null || MoveTo(_wanderGoal.Value, _self.Definition.Speed, dt))
        {
            _wanderGoal = null;
            _pauseTimer = _ai.PatrolPause * (0.5f + (float)_world.Random.NextDouble());
        }
    }

    private Vector2? PickWanderGoal()
    {
        float radius = _ai.WanderRadiusTiles * _world.Map.TileSize;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = (float)_world.Random.NextDouble() * MathF.Tau;
            float distance = radius * MathF.Sqrt((float)_world.Random.NextDouble());
            Vector2 candidate = _home + Geo.FromAngle(angle) * distance;
            (int x, int y) = _world.Map.TileAt(candidate);
            if (_world.Pathfinder.IsWalkable(x, y)) return _world.Map.TileCenter(x, y);
        }

        return null;
    }

    private void UpdateSuspicious(float dt)
    {
        Stop();
        TurnTowards(Geo.ToAngle(_pointOfInterest - _self.Position), dt, 0.7f);
        if (_stateTime >= _ai.SuspicionDelay) Enter(AiState.Investigate);
    }

    private void UpdateInvestigate(float dt)
    {
        if (MoveTo(_pointOfInterest, _self.Definition.RunSpeed, dt)) Enter(AiState.Search);
    }

    private void UpdateChase(float dt)
    {
        if (Target is null || !Target.IsAlive)
        {
            Target = null;
            Enter(AiState.Search);
            return;
        }

        if (!CanSeeTarget)
        {
            if (TimeSinceTargetSeen > _ai.LoseTargetTime || MoveTo(LastKnownTargetPosition, _self.Definition.RunSpeed, dt))
            {
                _pointOfInterest = LastKnownTargetPosition;
                Enter(AiState.Search);
            }

            return;
        }

        _reactionTimer -= dt;
        Vector2 toTarget = Target.Position - _self.Position;
        float distance = toTarget.Length();
        float targetAngle = Geo.ToAngle(toTarget);
        WeaponDefinition weapon = _world.Combat.CurrentWeapon(_self);

        if (Target.IsDown && UpdateDownedTarget(dt, weapon, distance, targetAngle)) return;

        if (weapon.IsFirearm && _self.Weapon is { } gun)
        {
            if (gun.IsEmpty)
            {
                // Out of ammunition: throw the gun at the target and fight bare-handed.
                _self.Facing = targetAngle;
                _world.Combat.ThrowWeapon(_self);
                return;
            }

            bool clear = LineOfFireClear(Target);
            _blockedShotTime = clear ? 0f : _blockedShotTime + dt;
            float preferred = weapon.Range * _ai.PreferredRangeFactor;
            if (distance > preferred || _blockedShotTime > BlockedShotPatience)
                MoveTo(Target.Position, _self.Definition.RunSpeed, dt, faceMovement: false);
            else Stop();

            TurnTowards(targetAngle, dt, 1f);
            bool aimed = MathF.Abs(Geo.AngleDifference(_self.Facing, targetAngle)) < FirearmFacingTolerance;
            if (_reactionTimer <= 0f && aimed && distance <= weapon.Range && clear)
                AttackWithAimError(targetAngle);
            return;
        }

        // Leapers pounce from mid range when the way is clear (the crouch warns the player).
        if (_ai.Leap is { } leap && _reactionTimer <= 0f && !Target.IsDown && distance >= leap.MinRange && distance <= leap.Range
            && _world.Physics.HasLineOfSight(_self.Position, Target.Position, Blocking.Movement) && _world.Combat.TryLeap(_self, Target))
            return;

        float reach = weapon.Range + Target.Radius;
        if (distance > reach * 0.8f) MoveTo(Target.Position, _self.Definition.RunSpeed, dt, faceMovement: false);
        else Stop();
        SpreadFromAllies();

        TurnTowards(targetAngle, dt, 1f);
        bool facing = MathF.Abs(Geo.AngleDifference(_self.Facing, targetAngle)) < MeleeFacingTolerance;
        if (_reactionTimer <= _ai.ReactionTime * 0.5f && facing && distance <= reach) _world.Combat.TryAttack(_self);
    }

    /// <summary>
    /// The target lies on the floor. If an ally is already finishing it, stay close and wait;
    /// otherwise finish it off when in reach (same execution system as the player). Gunmen far
    /// away keep shooting when bullets can hit people on the floor. Returns false to fall back to
    /// the normal attack logic (executions disabled).
    /// </summary>
    private bool UpdateDownedTarget(float dt, WeaponDefinition weapon, float distance, float targetAngle)
    {
        Actor target = Target!;
        if (target.State == ActorState.BeingExecuted)
        {
            if (distance > WaitingDistance) MoveTo(target.Position, _self.Definition.RunSpeed, dt, faceMovement: false);
            else Stop();
            SpreadFromAllies();
            TurnTowards(targetAngle, dt, 1f);
            return true;
        }

        if (!_world.Rules.Execution.Enabled || !_world.Rules.Execution.EnemiesCanExecute) return false;
        TurnTowards(targetAngle, dt, 1f);
        if (_world.Combat.TryExecute(_self)) return true;

        bool canShoot = weapon.IsFirearm && _self.Weapon is { IsEmpty: false } && _world.Rules.Combat.BulletsHitDowned;
        if (canShoot && distance > ShootRatherThanExecuteDistance) return false;

        MoveTo(target.Position, _self.Definition.RunSpeed, dt, faceMovement: false);
        SpreadFromAllies();
        return true;
    }

    /// <summary>
    /// Simple local avoidance: allies too close push each other aside, and an ally standing between
    /// this enemy and its target is walked around (sideways), so attackers surround instead of queueing.
    /// </summary>
    private void SpreadFromAllies()
    {
        Vector2 push = Vector2.Zero;
        Vector2 toTarget = Target is null ? Vector2.Zero : Geo.NormalizeOrZero(Target.Position - _self.Position);
        foreach (Actor ally in _world.Actors)
        {
            if (ally == _self || !ally.IsAlive || ally.IsDown || ally.Faction != _self.Faction) continue;
            Vector2 away = _self.Position - ally.Position;
            float distance = away.Length();
            float range = _self.Radius + ally.Radius + SpreadMargin;
            if (distance >= range) continue;
            // Perfectly stacked: pick a side from the ids so both do not choose the same one.
            Vector2 direction = distance > Geo.Epsilon ? away / distance : Geo.FromAngle(_self.Id * 2.4f);
            float weight = 1f - distance / range;
            push += direction * weight;

            // The ally is in the way to the target: step around it, on the side it is not on.
            if (toTarget != Vector2.Zero && Vector2.Dot(-direction, toTarget) > 0.5f)
            {
                float cross = Geo.Cross(toTarget, -direction);
                float side = MathF.Abs(cross) > 0.05f ? -MathF.Sign(cross) : (_self.Id % 2 == 0 ? 1f : -1f);
                push += Geo.Perpendicular(toTarget) * side * (0.6f + weight);
            }
        }

        if (push == Vector2.Zero) return;
        if (_self.MoveIntent == Vector2.Zero) _self.MoveSpeed = _self.Definition.Speed;
        _self.MoveIntent = Geo.ClampLength(_self.MoveIntent + push * SpreadStrength, 1f);
    }

    private void AttackWithAimError(float targetAngle)
    {
        float error = Geo.DegToRad(_ai.AimErrorDegrees);
        float aimed = targetAngle + ((float)_world.Random.NextDouble() * 2f - 1f) * error;
        float previous = _self.Facing;
        _self.Facing = aimed;
        bool fired = _world.Combat.TryAttack(_self);
        if (!fired) _self.Facing = previous;
    }

    /// <summary>Glass does not stop the shot (it breaks); walls, doors and allies do.</summary>
    private bool LineOfFireClear(Actor target)
    {
        Vector2 from = _self.Position;
        Vector2 direction = Geo.NormalizeOrZero(target.Position - from);
        for (int i = 0; i < 8; i++)
        {
            bool blocked = _world.Physics.Raycast(from, target.Position, Blocking.Projectiles, out RayHit hit,
                actor => actor != _self && actor != target && actor.IsAlive && !_self.IsHostileTo(actor)
                    && (_world.Rules.Combat.BulletsHitDowned || !actor.IsDown) && _world.Rules.FriendlyFire.Projectiles);
            if (!blocked) return true;
            if (hit.Kind == HitKind.Tile && _world.Map.Get(hit.TileX, hit.TileY).Destructible)
            {
                from = hit.Point + direction;
                continue;
            }

            return false;
        }

        return false;
    }

    private void UpdateSearch(float dt)
    {
        Stop();
        LookAround(dt, _self.Facing, spread: 1.6f, interval: 0.9f, turnFactor: 0.8f);
        if (_stateTime >= _ai.SearchDuration) Enter(AiState.Return);
    }

    private void UpdateReturn(float dt)
    {
        Vector2 destination = _patrol.Count > 0 && _behavior == AiBehavior.Patrol ? _patrol[_patrolIndex] : _home;
        if (MoveTo(destination, _self.Definition.Speed, dt))
        {
            AiState resting = RestingState();
            if (resting == AiState.Idle) _self.Facing = Geo.RotateTowards(_self.Facing, _homeFacing, MathF.PI);
            Enter(resting);
        }
    }

    private AiState RestingState() => _behavior switch
    {
        AiBehavior.Ambush => AiState.Ambush,
        AiBehavior.Patrol when _patrol.Count > 0 => AiState.Patrol,
        AiBehavior.Wander or AiBehavior.Dormant => AiState.Wander,
        _ => AiState.Idle,
    };

    private void Enter(AiState state)
    {
        State = state;
        _stateTime = 0f;
        _pauseTimer = 0f;
        _wanderGoal = null;
        _repathAttempts = 0;
        _path.Clear();
        _pathGoal = new Vector2(float.NaN);
        _lookTimer = 0f;
        _lookAngle = _self.Facing;
    }

    private void Stop() => _self.MoveIntent = Vector2.Zero;

    private void TurnTowards(float angle, float dt, float speedFactor) =>
        _self.Facing = Geo.RotateTowards(_self.Facing, angle, Geo.DegToRad(_ai.TurnSpeedDegrees) * speedFactor * dt);

    private void LookAround(float dt, float center, float spread, float interval, float turnFactor)
    {
        _lookTimer -= dt;
        if (_lookTimer <= 0f)
        {
            _lookTimer = interval * (0.6f + (float)_world.Random.NextDouble() * 0.8f);
            _lookAngle = center + ((float)_world.Random.NextDouble() * 2f - 1f) * spread;
        }

        TurnTowards(_lookAngle, dt, turnFactor);
    }

    /// <summary>
    /// Walks toward <paramref name="goal"/>, straight when possible, otherwise along an A* path.
    /// Paths are recomputed only when the goal moves or the actor gets stuck. Returns true on arrival.
    /// </summary>
    private bool MoveTo(Vector2 goal, float speed, float dt, bool faceMovement = true)
    {
        if (Vector2.Distance(_self.Position, goal) <= WaypointReach)
        {
            Stop();
            return true;
        }

        bool goalMoved = float.IsNaN(_pathGoal.X) || Vector2.Distance(goal, _pathGoal) > _world.Map.TileSize * 0.5f;
        // An empty path means "arrived" or "unreachable": only retry when the goal moves or after a delay.
        bool needPath = _path.Count == 0 ? goalMoved || _repathTimer <= 0f : goalMoved && _repathTimer <= 0f;
        if (needPath) Repath(goal);
        if (_path.Count == 0)
        {
            Stop();
            return true;
        }

        // Skip waypoints that can be reached in a straight line (smooths grid paths).
        if (_pathIndex < _path.Count - 1 && _world.Physics.CanWalkStraight(_self.Position, _path[_pathIndex + 1], _self.Radius))
            _pathIndex++;

        Vector2 waypoint = _path[_pathIndex];
        if (Vector2.Distance(_self.Position, waypoint) <= WaypointReach)
        {
            _pathIndex++;
            if (_pathIndex >= _path.Count)
            {
                // End of the path: as close as the grid allows (the goal itself may be inside a wall).
                _path.Clear();
                Stop();
                return true;
            }

            waypoint = _path[_pathIndex];
        }

        Vector2 direction = Geo.NormalizeOrZero(waypoint - _self.Position);
        _self.MoveIntent = direction;
        _self.MoveSpeed = speed;
        if (faceMovement && direction != Vector2.Zero) TurnTowards(Geo.ToAngle(direction), dt, 1f);

        return CheckStuck(dt, goal);
    }

    private void Repath(Vector2 goal)
    {
        _pathGoal = goal;
        _repathTimer = RepathInterval;
        _pathIndex = 0;
        if (_world.Physics.CanWalkStraight(_self.Position, goal, _self.Radius))
        {
            _path.Clear();
            _path.Add(goal);
        }
        else
        {
            _world.Pathfinder.FindPath(_self.Position, goal, _path);
        }
    }

    /// <summary>Gives up on a destination after several failed attempts to move toward it.</summary>
    private bool CheckStuck(float dt, Vector2 goal)
    {
        _stuckTimer += dt;
        if (_stuckTimer < StuckCheckInterval) return false;
        _stuckTimer = 0f;
        bool stuck = Vector2.Distance(_stuckCheckPosition, _self.Position) < 4f;
        _stuckCheckPosition = _self.Position;
        if (!stuck)
        {
            _repathAttempts = 0;
            return false;
        }

        if (++_repathAttempts >= MaxRepathAttempts) return true;
        Repath(goal);
        return false;
    }
}
