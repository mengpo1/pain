using System.Numerics;
using TopDown.Core.Definitions;
using TopDown.Core.Geometry;

namespace TopDown.Core.World;

/// <summary>Everything needed to apply an attack to a character.</summary>
public readonly record struct Hit(
    float Damage,
    string DamageType,
    bool Knockdown,
    float KnockdownDuration,
    Vector2 Direction,
    float Knockback,
    Actor? Source,
    HitMethod Method,
    WeaponDefinition? Weapon = null);

/// <summary>
/// Attacks, bullets, thrown weapons, pickups and executions. The same rules apply to the
/// player and to enemies.
/// </summary>
public sealed class CombatSystem
{
    /// <summary>Hard cap so a bullet hitting a chain of glass panes cannot loop forever.</summary>
    private const int MaxBulletPenetrations = 8;
    private const float EmptyClickCooldown = 0.3f;
    private const float ThrowCooldown = 0.15f;
    private const float InteractRadius = 28f;

    private readonly GameWorld _world;

    internal CombatSystem(GameWorld world) => _world = world;

    private GameRules Rules => _world.Rules;

    /// <summary>The weapon an actor fights with when its hands are empty.</summary>
    public WeaponDefinition NaturalWeapon(Actor actor) =>
        _world.Definitions.Weapon(actor.Definition.NaturalWeaponId ?? Rules.UnarmedWeaponId);

    public WeaponDefinition CurrentWeapon(Actor actor) => actor.Weapon?.Definition ?? NaturalWeapon(actor);

    public bool TryAttack(Actor attacker)
    {
        if (!attacker.CanAct || attacker.AttackCooldown > 0f) return false;
        return attacker.Weapon is { Definition.IsFirearm: true } firearm
            ? TryFire(attacker, firearm)
            : TryMelee(attacker, CurrentWeapon(attacker));
    }

    private bool TryFire(Actor shooter, WeaponInstance weapon)
    {
        if (shooter.State != ActorState.Active) return false;
        WeaponDefinition definition = weapon.Definition;
        bool infinite = !shooter.IsPlayer && Rules.EnemiesHaveInfiniteAmmo && !weapon.IsEmpty;
        if (!infinite && !weapon.TryConsumeAmmo())
        {
            shooter.AttackCooldown = EmptyClickCooldown;
            _world.AddEvent(new WeaponEmpty(shooter, definition));
            if (definition.WhenEmpty == EmptyBehavior.Throw) ThrowWeapon(shooter);
            else if (definition.WhenEmpty == EmptyBehavior.Drop) DropWeapon(shooter, Vector2.Zero);
            // The click (and the automatic throw or drop) is the action: the attack input is consumed.
            return true;
        }

        shooter.AttackCooldown = definition.Cooldown;
        shooter.LastAttackTime = _world.Time;
        float spread = Geo.DegToRad(definition.SpreadDegrees) * (shooter.IsPlayer ? _world.Modifier.Spread : 1f);
        for (int i = 0; i < Math.Max(1, definition.Pellets); i++)
        {
            float angle = shooter.Facing + ((float)_world.Random.NextDouble() - 0.5f) * spread;
            FireBullet(shooter, definition, angle);
        }

        Vector2 muzzle = shooter.Position + Geo.FromAngle(shooter.Facing) * definition.MuzzleDistance;
        _world.AddEvent(new ShotFired(shooter, definition, muzzle, shooter.Facing));
        _world.EmitNoise(shooter.Position, definition.NoiseRadius, shooter);
        return true;
    }

    /// <summary>
    /// The bullet path starts at the shooter's centre, not at the muzzle: a character standing
    /// against a wall can never shoot through it, and a target touching the barrel is still hit.
    /// </summary>
    private void FireBullet(Actor shooter, WeaponDefinition weapon, float angle)
    {
        Vector2 direction = Geo.FromAngle(angle);
        Vector2 muzzle = shooter.Position + direction * weapon.MuzzleDistance;
        if (TraceBullet(shooter, weapon, shooter.Position, muzzle, direction) is not null) return;

        float range = MathF.Max(0f, weapon.Range - weapon.MuzzleDistance);
        if (weapon.ProjectileSpeed <= 0f)
        {
            Vector2 end = muzzle + direction * range;
            Vector2 stop = TraceBullet(shooter, weapon, muzzle, end, direction) ?? end;
            _world.AddEvent(new BulletTrace(muzzle, stop));
        }
        else
        {
            _world.AddProjectile(new Projectile(shooter, weapon, muzzle, direction * weapon.ProjectileSpeed, range));
        }
    }

    /// <summary>Moves a bullet along a segment. Returns where it stopped, or null if it went through.</summary>
    private Vector2? TraceBullet(Actor? owner, WeaponDefinition weapon, Vector2 from, Vector2 to, Vector2 direction)
    {
        for (int i = 0; i < MaxBulletPenetrations; i++)
        {
            if (!_world.Physics.Raycast(from, to, Blocking.Projectiles, out RayHit hit, actor => CanBulletHit(owner, actor)))
                return null;

            switch (hit.Kind)
            {
                case HitKind.Actor:
                    ApplyHit(hit.Actor!, new Hit(weapon.Damage, weapon.DamageType, weapon.Knockdown, weapon.KnockdownDuration,
                        direction, weapon.Knockback, owner, HitMethod.Firearm, weapon));
                    return hit.Point;
                case HitKind.Tile when _world.Map.Get(hit.TileX, hit.TileY).Destructible:
                    BreakTile(hit.TileX, hit.TileY);
                    from = hit.Point + direction * 0.5f;
                    continue;
                default:
                    _world.AddEvent(new BulletImpact(hit.Point, hit.Normal, ToSurface(hit.Kind)));
                    return hit.Point;
            }
        }

        return from;
    }

    private bool CanBulletHit(Actor? owner, Actor target) =>
        target != owner && target.IsAlive && (Rules.FriendlyFire || owner is null || owner.IsHostileTo(target));

    private static SurfaceKind ToSurface(HitKind kind) => kind switch
    {
        HitKind.Door => SurfaceKind.Door,
        HitKind.Prop => SurfaceKind.Prop,
        HitKind.Actor => SurfaceKind.Actor,
        _ => SurfaceKind.Wall,
    };

    internal void UpdateProjectiles(float dt)
    {
        foreach (Projectile projectile in _world.Projectiles)
        {
            if (!projectile.Alive) continue;
            projectile.PreviousPosition = projectile.Position;
            Vector2 step = projectile.Velocity * dt;
            float length = step.Length();
            if (length > projectile.RemainingRange)
            {
                step *= projectile.RemainingRange / length;
                length = projectile.RemainingRange;
            }

            Vector2 direction = Geo.NormalizeOrZero(projectile.Velocity);
            Vector2 end = projectile.Position + step;
            Vector2? stop = TraceBullet(projectile.Owner, projectile.Weapon, projectile.Position, end, direction);
            projectile.Position = stop ?? end;
            projectile.RemainingRange -= length;
            if (stop is not null || projectile.RemainingRange <= 0f) projectile.Alive = false;
        }
    }

    private bool TryMelee(Actor attacker, WeaponDefinition weapon)
    {
        if (!attacker.BeginMeleeAttack(weapon, _world.Time)) return false;
        attacker.AttackCooldown = weapon.Cooldown;
        _world.AddEvent(new MeleeSwing(attacker, weapon));
        _world.EmitNoise(attacker.Position, weapon.NoiseRadius, attacker);
        return true;
    }

    /// <summary>Resolves melee swings once their hit delay has elapsed.</summary>
    internal void UpdateMeleeAttacks()
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Attacking || actor.AttackResolved || actor.CurrentAttack is null) continue;
            if (actor.AttackElapsed < actor.CurrentAttack.HitDelay) continue;
            ResolveMelee(actor, actor.CurrentAttack);
            actor.AttackResolved = true;
        }
    }

    /// <summary>
    /// A swing hits characters inside its arc and reach, nearest first, only if nothing solid
    /// stands between the attacker's centre and the target.
    /// </summary>
    internal void ResolveMelee(Actor attacker, WeaponDefinition weapon)
    {
        var targets = new List<(Actor Target, float Distance)>();
        float halfArc = Geo.DegToRad(weapon.ArcDegrees) * 0.5f;
        foreach (Actor target in _world.Actors)
        {
            if (target == attacker || !target.IsAlive) continue;
            if (!Rules.FriendlyFire && !attacker.IsHostileTo(target)) continue;
            Vector2 toTarget = target.Position - attacker.Position;
            float distance = toTarget.Length();
            if (distance > weapon.Range + target.Radius) continue;

            float tolerance = distance > target.Radius ? MathF.Asin(target.Radius / distance) : MathF.PI;
            if (MathF.Abs(Geo.AngleDifference(attacker.Facing, Geo.ToAngle(toTarget))) > halfArc + tolerance) continue;
            if (!ReachableInMelee(attacker.Position, target.Position, distance - target.Radius)) continue;
            targets.Add((target, distance));
        }

        // Standing threats first, then bodies on the floor; nearest first within each group.
        targets.Sort((a, b) => a.Target.IsDown != b.Target.IsDown ? a.Target.IsDown.CompareTo(b.Target.IsDown) : a.Distance.CompareTo(b.Distance));
        int count = Math.Min(Math.Max(1, weapon.MaxTargets), targets.Count);
        for (int i = 0; i < count; i++)
        {
            Actor target = targets[i].Target;
            Vector2 direction = Geo.NormalizeOrZero(target.Position - attacker.Position);
            ApplyHit(target, new Hit(weapon.Damage, weapon.DamageType, weapon.Knockdown, weapon.KnockdownDuration,
                direction, weapon.Knockback, attacker, HitMethod.Melee, weapon));
        }

        if (count == 0) BreakTileInFront(attacker, weapon.Range);
    }

    private bool ReachableInMelee(Vector2 from, Vector2 to, float clearDistance)
    {
        if (!_world.Physics.Raycast(from, to, Blocking.Projectiles, out RayHit hit)) return true;
        return hit.Fraction * Vector2.Distance(from, to) >= clearDistance;
    }

    private void BreakTileInFront(Actor attacker, float reach)
    {
        Vector2 end = attacker.Position + Geo.FromAngle(attacker.Facing) * (attacker.Radius + reach);
        if (_world.Map.Raycast(attacker.Position, end, Blocking.Projectiles, out TileHit hit)
            && _world.Map.Get(hit.X, hit.Y).Destructible)
        {
            BreakTile(hit.X, hit.Y);
        }
    }

    internal void BreakTile(int x, int y)
    {
        TileDefinition tile = _world.Map.Get(x, y);
        if (!tile.Destructible) return;
        TileDefinition replacement = tile.BreaksInto is { } id ? _world.Definitions.Tile(id) : _world.FallbackFloor;
        _world.Map.Set(x, y, replacement);
        Vector2 center = _world.Map.TileCenter(x, y);
        _world.AddEvent(new TileBroken(x, y, tile, center));
        _world.EmitNoise(center, tile.BreakNoiseRadius, null);
    }

    /// <summary>Applies damage, knock-down or stun according to the target's definition.</summary>
    public void ApplyHit(Actor target, in Hit hit)
    {
        if (!target.IsAlive) return;
        float multiplier = target.Definition.DamageMultipliers.GetValueOrDefault(hit.DamageType, 1f);
        if (target.IsPlayer) multiplier *= _world.Modifier.DamageTaken;
        float damage = hit.Damage * multiplier;

        _world.AddEvent(new ActorHit(target, hit.Direction, hit.Method, hit.DamageType, damage, hit.Weapon));

        if (damage > 0f) target.Health -= damage;
        if (target.Health <= 0f)
        {
            Kill(target, hit);
            return;
        }

        // Only survivors react (a silent kill alerts nobody).
        if (hit.Source is not null && target.Brain is not null) target.Brain.OnAttacked(hit.Source);

        if (hit.Knockdown && !target.Definition.KnockdownImmune)
        {
            target.KnockDown(hit.KnockdownDuration);
            FallInDirection(target, hit.Direction);
            target.Velocity += hit.Direction * hit.Knockback;
            if (Rules.DropWeaponOnKnockdown) DropWeapon(target, hit.Direction * 60f);
            _world.AddEvent(new ActorKnockedDown(target, hit.Method));
        }
        else if (damage > 0f)
        {
            target.Stun(target.Definition.HitStunDuration);
            target.Velocity += hit.Direction * hit.Knockback * 0.5f;
        }
    }

    private void Kill(Actor victim, in Hit hit)
    {
        victim.Kill();
        FallInDirection(victim, hit.Direction);
        victim.Velocity = hit.Direction * hit.Knockback;
        if (Rules.DropWeaponOnDeath) DropWeapon(victim, hit.Direction * 80f);
        _world.AddEvent(new ActorKilled(victim, hit.Source, hit.Direction, hit.Method, hit.DamageType));
        _world.OnActorKilled(victim, hit);
    }

    /// <summary>A body on the floor lies in the direction it was hit.</summary>
    private static void FallInDirection(Actor actor, Vector2 direction)
    {
        if (direction.LengthSquared() > Geo.Epsilon) actor.Facing = Geo.ToAngle(direction);
    }

    public void PickupOrThrow(Actor actor)
    {
        if (!actor.IsFreeToAct) return;
        WorldWeapon? nearest = FindPickup(actor);
        if (actor.Weapon is null)
        {
            if (nearest is not null) Equip(actor, nearest);
            return;
        }

        if (nearest is not null && Rules.PickupWhileArmed == PickupWhileArmed.Swap)
        {
            DropWeapon(actor, Vector2.Zero);
            Equip(actor, nearest);
            return;
        }

        ThrowWeapon(actor);
    }

    public WorldWeapon? FindPickup(Actor actor)
    {
        WorldWeapon? best = null;
        float bestDistance = Rules.PickupRadius + actor.Radius;
        foreach (WorldWeapon weapon in _world.Weapons)
        {
            if (weapon.IsFlying) continue;
            if (actor.IsPlayer && !_world.Modifier.AllowFirearms && weapon.Weapon.Definition.IsFirearm) continue;
            float distance = Vector2.Distance(weapon.Position, actor.Position);
            if (distance <= bestDistance && _world.Physics.HasLineOfSight(actor.Position, weapon.Position, Blocking.Projectiles))
            {
                best = weapon;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void Equip(Actor actor, WorldWeapon weapon)
    {
        _world.RemoveWeapon(weapon);
        actor.Weapon = weapon.Weapon;
        actor.AttackCooldown = MathF.Max(actor.AttackCooldown, 0.1f);
        _world.AddEvent(new WeaponPickedUp(actor, weapon.Weapon.Definition));
    }

    public void DropWeapon(Actor actor, Vector2 velocity)
    {
        if (actor.Weapon is not { Definition.CanDrop: true } weapon) return;
        actor.Weapon = null;
        _world.AddWeapon(new WorldWeapon(weapon, actor.Position, actor.Facing)
        {
            Velocity = velocity,
            Spin = ((float)_world.Random.NextDouble() - 0.5f) * 10f,
        });
    }

    public void ThrowWeapon(Actor actor)
    {
        if (!actor.IsFreeToAct || actor.Weapon is not { Definition.CanDrop: true } weapon) return;
        actor.Weapon = null;
        actor.AttackCooldown = MathF.Max(actor.AttackCooldown, ThrowCooldown);
        ThrowDefinition throwDefinition = weapon.Definition.Throw;
        Vector2 direction = Geo.FromAngle(actor.Facing);
        _world.AddWeapon(new WorldWeapon(weapon, actor.Position, actor.Facing)
        {
            Velocity = direction * throwDefinition.Speed + actor.Velocity * 0.3f,
            Spin = throwDefinition.Spin,
            Thrower = actor,
        });
        _world.AddEvent(new WeaponThrown(actor, weapon.Definition));
        _world.EmitNoise(actor.Position, 60f, actor);
    }

    internal void UpdateWorldWeapons(float dt)
    {
        foreach (WorldWeapon weapon in _world.Weapons)
        {
            weapon.PreviousPosition = weapon.Position;
            if (weapon.Velocity.LengthSquared() < 1f && !weapon.IsFlying)
            {
                weapon.Velocity = Vector2.Zero;
                continue;
            }

            MoveWorldWeapon(weapon, dt);

            float speed = weapon.Velocity.Length();
            if (weapon.IsFlying)
            {
                weapon.Velocity *= MathF.Exp(-0.6f * dt);
                if (speed < Rules.ThrownHarmSpeed) weapon.Thrower = null;
            }
            else
            {
                float slowed = MathF.Max(0f, speed - 900f * dt);
                weapon.Velocity = speed > Geo.Epsilon ? weapon.Velocity / speed * slowed : Vector2.Zero;
                weapon.Spin *= MathF.Exp(-4f * dt);
            }

            weapon.Rotation += weapon.Spin * dt;
        }
    }

    private void MoveWorldWeapon(WorldWeapon weapon, float dt)
    {
        Vector2 delta = weapon.Velocity * dt;
        Vector2 direction = Geo.NormalizeOrZero(delta);
        // In flight a weapon passes over low furniture and through broken windows, like a bullet.
        Blocking mask = weapon.IsFlying ? Blocking.Projectiles : Blocking.Movement;
        bool blocked = _world.Physics.Raycast(weapon.Position, weapon.Position + delta, mask, out RayHit hit);

        if (weapon.IsFlying && FindThrowTarget(weapon, delta) is { } victim && (!blocked || victim.Fraction < hit.Fraction)
            && !_world.Physics.Raycast(weapon.Position + delta * victim.Fraction, victim.Actor.Position, mask, out _))
        {
            // The weapon is a small disc: the last check stops it from "touching" someone through a door.
            ThrowDefinition definition = weapon.Weapon.Definition.Throw;
            ApplyHit(victim.Actor, new Hit(definition.Damage, definition.DamageType, definition.Knockdown,
                definition.KnockdownDuration, direction, 180f, weapon.Thrower, HitMethod.Thrown, weapon.Weapon.Definition));
            weapon.Position += delta * victim.Fraction;
            weapon.Velocity *= -0.2f;
            weapon.Thrower = null;
            _world.AddEvent(new ThrownWeaponBounced(weapon.Position, weapon.Weapon.Definition));
            return;
        }

        if (blocked)
        {
            if (hit.Kind == HitKind.Tile && weapon.IsFlying && _world.Map.Get(hit.TileX, hit.TileY).Destructible)
            {
                BreakTile(hit.TileX, hit.TileY);
                weapon.Velocity *= 0.7f;
                weapon.Position += delta;
                return;
            }

            Vector2 velocity = weapon.Velocity;
            Vector2 reflected = velocity - 2f * Vector2.Dot(velocity, hit.Normal) * hit.Normal;
            weapon.Position = hit.Point + hit.Normal * WorldWeapon.CollisionRadius;
            weapon.Velocity = reflected * 0.3f;
            if (weapon.IsFlying) _world.AddEvent(new ThrownWeaponBounced(weapon.Position, weapon.Weapon.Definition));
            weapon.Thrower = null;
            return;
        }

        weapon.Position += delta;
    }

    private (Actor Actor, float Fraction)? FindThrowTarget(WorldWeapon weapon, Vector2 delta)
    {
        (Actor, float)? best = null;
        float bestFraction = float.MaxValue;
        foreach (Actor actor in _world.Actors)
        {
            if (actor == weapon.Thrower || !actor.IsAlive || actor.IsDown) continue;
            if (!Rules.FriendlyFire && weapon.Thrower is not null && !weapon.Thrower.IsHostileTo(actor)) continue;
            if (Geo.SegmentCircle(weapon.Position, delta, actor.Position, actor.Radius + WorldWeapon.CollisionRadius, out float t)
                && t < bestFraction)
            {
                bestFraction = t;
                best = (actor, t);
            }
        }

        return best;
    }

    /// <summary>Starts an execution on a nearby knocked-down enemy, or uses a nearby interactable.</summary>
    public bool TryInteract(Actor actor)
    {
        if (!actor.IsFreeToAct) return false;
        if (Rules.Execution.Enabled && FindExecutionTarget(actor) is { } victim)
        {
            float speed = actor.IsPlayer ? MathF.Max(0.05f, _world.Modifier.ExecutionSpeed) : 1f;
            float duration = CurrentWeapon(actor).ExecutionDuration * Rules.Execution.DurationMultiplier / speed;
            if (!actor.BeginExecution(victim, duration)) return false;
            actor.Facing = Geo.ToAngle(victim.Position - actor.Position);
            _world.AddEvent(new ExecutionStarted(actor, victim));
            return true;
        }

        if (actor.IsPlayer && FindInteractable(actor) is { } interactable)
        {
            _world.Activate(interactable);
            return true;
        }

        return false;
    }

    public Actor? FindExecutionTarget(Actor actor)
    {
        Actor? best = null;
        float bestGap = Rules.Execution.Range;
        foreach (Actor other in _world.Actors)
        {
            if (other.State != ActorState.KnockedDown || !actor.IsHostileTo(other)) continue;
            if (other.Definition.DamageMultipliers.GetValueOrDefault("execution", 1f) <= 0f) continue;
            float gap = Vector2.Distance(other.Position, actor.Position) - other.Radius - actor.Radius;
            if (gap <= bestGap && _world.Physics.HasLineOfSight(actor.Position, other.Position, Blocking.Movement))
            {
                best = other;
                bestGap = gap;
            }
        }

        return best;
    }

    public Interactable? FindInteractable(Actor actor)
    {
        foreach (Interactable interactable in _world.Interactables)
        {
            if (interactable.Available && Vector2.Distance(interactable.Position, actor.Position) <= InteractRadius + actor.Radius)
                return interactable;
        }

        return null;
    }

    internal void UpdateExecutions()
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Executing) continue;
            Actor? victim = actor.ExecutionTarget;
            if (victim is null || victim.State != ActorState.KnockedDown)
            {
                actor.EndExecution();
                continue;
            }

            Vector2 toVictim = victim.Position - actor.Position;
            actor.Facing = Geo.ToAngle(toVictim);
            if (actor.ExecutionElapsed < actor.ExecutionDuration) continue;

            if (actor.Weapon is { Definition.IsFirearm: true } gun && gun.TryConsumeAmmo())
            {
                _world.AddEvent(new ShotFired(actor, gun.Definition, victim.Position, actor.Facing));
                _world.EmitNoise(actor.Position, gun.Definition.NoiseRadius, actor);
            }

            actor.EndExecution();
            ApplyHit(victim, new Hit(1_000_000f, "execution", false, 0f, Geo.NormalizeOrZero(toVictim), 20f, actor, HitMethod.Execution, CurrentWeapon(actor)));
        }
    }
}
