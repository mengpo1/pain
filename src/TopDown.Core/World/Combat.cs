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
    WeaponDefinition? Weapon = null,
    float StunDuration = 0f);

/// <summary>Kinds of attack, for friendly-fire rules.</summary>
public enum DamageKind
{
    Melee,
    Projectile,
    Thrown,
    Explosion,
    Door,
}

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
        if (shooter.State != ActorState.Active || shooter.IsReloading) return false;
        WeaponDefinition definition = weapon.Definition;
        bool infinite = !shooter.IsPlayer && Rules.EnemiesHaveInfiniteAmmo && !weapon.IsEmpty;
        // Trigger pulled on an empty magazine with spare rounds: reload instead of clicking.
        if (weapon.IsEmpty && !infinite && TryReload(shooter)) return true;
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
                        direction, weapon.Knockback, owner, HitMethod.Firearm, weapon, weapon.StunDuration));
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
        target != owner && target.IsAlive
        && (Rules.Combat.BulletsHitDowned || !target.IsDown)
        && CanDamage(owner, target, DamageKind.Projectile);

    /// <summary>
    /// Single targeting rule for every attack: enemies can always be hurt, allies only when the
    /// friendly-fire flag of that kind of attack is on. Allies still collide physically.
    /// </summary>
    public bool CanDamage(Actor? source, Actor target, DamageKind kind)
    {
        if (source is null) return true;
        if (source == target) return false;
        if (source.IsHostileTo(target)) return true;
        FriendlyFireRules friendlyFire = Rules.FriendlyFire;
        return kind switch
        {
            DamageKind.Melee => friendlyFire.Melee,
            DamageKind.Projectile => friendlyFire.Projectiles,
            DamageKind.Thrown => friendlyFire.Thrown,
            DamageKind.Explosion => friendlyFire.Explosions,
            DamageKind.Door => friendlyFire.Doors,
            _ => false,
        };
    }

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
            if (!CanDamage(attacker, target, DamageKind.Melee)) continue;
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
                direction, weapon.Knockback, attacker, HitMethod.Melee, weapon, weapon.StunDuration));
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

        _world.AddEvent(new ActorHit(target, hit.Source, hit.Direction, hit.Method, hit.DamageType, damage, hit.Weapon));
        if (damage > 0f)
        {
            target.LastHurtTime = _world.Time;
            target.LastDamageType = hit.DamageType;
        }

        if (damage > 0f) target.Health -= damage;
        if (target.Health <= 0f)
        {
            if (!TryWound(target, hit)) Kill(target, hit);
            return;
        }

        // Only survivors react (a silent kill alerts nobody).
        if (hit.Source is not null && target.Brain is not null) target.Brain.OnAttacked(hit.Source);

        bool immune = target.Definition.KnockdownImmune;
        // A dazed character goes down from any further hit (configurable).
        bool knockdown = hit.Knockdown || (target.State == ActorState.Stunned && Rules.Combat.HitOnStunnedKnocksDown);
        if (knockdown && !immune && target.State != ActorState.BeingExecuted && !target.IsWounded)
        {
            target.KnockDown(MathF.Max(hit.KnockdownDuration, MinimumKnockdown));
            FallInDirection(target, hit.Direction);
            target.Velocity += hit.Direction * hit.Knockback;
            if (Rules.DropWeaponOnKnockdown) DropWeapon(target, hit.Direction * 60f);
            _world.AddEvent(new ActorKnockedDown(target, hit.Source, hit.Method));
            return;
        }

        // Brief reaction for everything that does not floor the target: a shove and a stagger.
        float stun = MathF.Max(hit.StunDuration, damage > 0f ? target.Definition.HitStunDuration : 0f);
        if (hit.Knockdown && immune) stun = MathF.Max(stun, target.Definition.KnockdownResistStun);
        if (stun > 0f) target.Stun(stun);
        if (target.CanAct || target.State == ActorState.Stunned) target.Velocity += hit.Direction * hit.Knockback * 0.5f;
    }

    /// <summary>Shortest knock-down caused by hitting a dazed character.</summary>
    private const float MinimumKnockdown = 1.2f;

    /// <summary>
    /// A blow, bullet or thrown weapon that would kill may leave the target wounded instead (chance
    /// given by the weapon, only for characters that can be wounded). It falls, drops its weapon and
    /// crawls until finished off.
    /// </summary>
    private bool TryWound(Actor target, in Hit hit)
    {
        if (target.Definition.Wound is null || target.IsWounded || target.State == ActorState.BeingExecuted) return false;
        if (hit.Method is not (HitMethod.Melee or HitMethod.Firearm or HitMethod.Thrown)) return false;
        if (hit.Weapon is not { WoundChance: > 0f } weapon || _world.Random.NextDouble() >= weapon.WoundChance) return false;

        bool wasDown = target.IsDown;
        target.Wound(hit);
        if (!wasDown) FallInDirection(target, hit.Direction);
        target.Velocity = hit.Direction * hit.Knockback * 0.6f;
        DropWeapon(target, hit.Direction * 60f);
        _world.AddEvent(new ActorWounded(target, hit.Source, hit.Direction, hit.Method));
        return true;
    }

    /// <summary>Wounded characters that can bleed out die after their time, credited to whoever wounded them.</summary>
    internal void UpdateWounds()
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Wounded || actor.Definition.Wound is not { BleedOutTime: > 0f } wound) continue;
            if (actor.StateElapsed < wound.BleedOutTime || actor.WoundingHit is not { } hit) continue;
            Kill(actor, hit);
        }
    }

    private void Kill(Actor victim, in Hit hit)
    {
        IReadOnlyList<int> severed = ChooseSevered(victim, hit);
        // An executed body is already on the floor: it keeps its position and orientation.
        bool alreadyDown = victim.IsDown;
        victim.Kill();
        if (!alreadyDown)
        {
            FallInDirection(victim, hit.Direction);
            victim.Velocity = hit.Direction * hit.Knockback;
        }
        else
        {
            victim.Velocity = Vector2.Zero;
        }

        if (Rules.DropWeaponOnDeath) DropWeapon(victim, hit.Direction * 80f);
        _world.AddEvent(new ActorKilled(victim, hit.Source, hit.Direction, hit.Method, hit.DamageType, hit.Weapon, severed));
        _world.OnActorKilled(victim, hit);
    }

    /// <summary>
    /// Body parts a killing blow cuts off, following the weapon's <see cref="DismemberDefinition"/>.
    /// Not for executions (their animation decides) nor for crawlers lying face down.
    /// </summary>
    private IReadOnlyList<int> ChooseSevered(Actor victim, in Hit hit)
    {
        if (hit.Weapon?.Dismember is not { Chance: > 0f } dismember || hit.Method == HitMethod.Execution || victim.IsWounded) return [];
        List<BodyPart> parts = victim.Definition.Sprites.Parts;
        if (parts.Count == 0 || victim.Definition.Sprites.Torso is null) return [];
        if (dismember.MaxDistance > 0f && hit.Source is { } source && Vector2.Distance(source.Position, victim.Position) > dismember.MaxDistance) return [];
        if (_world.Random.NextDouble() >= dismember.Chance) return [];

        var candidates = Enumerable.Range(0, parts.Count).Where(i => dismember.Parts.Contains(parts[i].Id)).ToList();
        var severed = new List<int>();
        for (int n = 0; n < dismember.Count && candidates.Count > 0; n++)
        {
            int pick = _world.Random.Next(candidates.Count);
            severed.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }

        return severed;
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
            if (!CanDamage(weapon.Thrower, actor, DamageKind.Thrown)) continue;
            if (Geo.SegmentCircle(weapon.Position, delta, actor.Position, actor.Radius + WorldWeapon.CollisionRadius, out float t)
                && t < bestFraction)
            {
                bestFraction = t;
                best = (actor, t);
            }
        }

        return best;
    }

    /// <summary>
    /// Starts reloading the weapon in hand from the player's spare ammunition (weapons with an
    /// <c>ammoType</c> only). The weapon cannot fire until the reload ends.
    /// </summary>
    public bool TryReload(Actor actor)
    {
        if (!actor.IsPlayer || !actor.CanAct || actor.IsReloading) return false;
        if (actor.Weapon is not { Definition: { IsFirearm: true, AmmoType: { } type } } weapon || weapon.IsFull) return false;
        if (_world.Inventory.AmmoOf(type) <= 0) return false;
        actor.ReloadTimer = MathF.Max(0.01f, weapon.Definition.ReloadTime);
        actor.ReloadingWeapon = weapon;
        _world.AddEvent(new ReloadStarted(actor, weapon.Definition));
        return true;
    }

    /// <summary>Finishes reloads; a weapon dropped or thrown meanwhile cancels its reload.</summary>
    internal void UpdateReloads(float dt)
    {
        foreach (Actor actor in _world.Actors)
        {
            if (!actor.IsReloading) continue;
            if (actor.Weapon is not { } weapon || weapon != actor.ReloadingWeapon || !actor.IsAlive)
            {
                actor.ReloadTimer = 0f;
                actor.ReloadingWeapon = null;
                continue;
            }

            actor.ReloadTimer -= dt;
            if (actor.ReloadTimer > 0f) continue;
            actor.ReloadTimer = 0f;
            actor.ReloadingWeapon = null;
            string type = weapon.Definition.AmmoType!;
            int taken = _world.Inventory.TakeAmmo(type, weapon.Definition.MagazineSize - weapon.Ammo);
            int loaded = weapon.Refill(taken);
            _world.Inventory.AddAmmo(type, taken - loaded);
            _world.AddEvent(new ReloadFinished(actor, weapon.Definition, loaded));
        }
    }

    /// <summary>Starts a leap at <paramref name="target"/>: the crouch (warning) comes first.</summary>
    public bool TryLeap(Actor actor, Actor target)
    {
        if (actor.Definition.Ai.Leap is null || actor.LeapCooldown > 0f || !actor.BeginLeap(target)) return false;
        actor.Velocity = Vector2.Zero;
        _world.AddEvent(new LeapStarted(actor, target));
        return true;
    }

    /// <summary>
    /// Leap phases: crouch while turning to the target, then a straight jump in the direction the
    /// target was at the end of the crouch (a quick sidestep dodges it), then a vulnerable landing.
    /// </summary>
    internal void UpdateLeaps(float dt)
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Leaping) continue;
            if (actor.Definition.Ai.Leap is not { } leap)
            {
                actor.EndLeap(0f);
                continue;
            }

            actor.MoveIntent = Vector2.Zero;
            switch (actor.LeapPhase)
            {
                case LeapPhase.Crouch:
                    Vector2 toTarget = actor.LeapTarget is { IsAlive: true } target ? target.Position - actor.Position : Geo.FromAngle(actor.Facing);
                    if (toTarget.LengthSquared() > Geo.Epsilon) actor.Facing = Geo.RotateTowards(actor.Facing, Geo.ToAngle(toTarget), LeapAimTurnSpeed * dt);
                    if (actor.LeapPhaseElapsed >= leap.WindUp)
                    {
                        Vector2 direction = toTarget.LengthSquared() > Geo.Epsilon ? Vector2.Normalize(toTarget) : Geo.FromAngle(actor.Facing);
                        actor.Facing = Geo.ToAngle(direction);
                        actor.LaunchLeap(direction);
                        _world.AddEvent(new LeapLaunched(actor, direction));
                    }
                    break;
                case LeapPhase.Airborne:
                    if (actor.LeapPhaseElapsed >= leap.Distance / MathF.Max(1f, leap.Speed)) Land(actor, hit: false);
                    break;
                case LeapPhase.Landing:
                    if (actor.LeapPhaseElapsed >= leap.Recovery) actor.EndLeap(leap.Cooldown);
                    break;
            }
        }
    }

    /// <summary>After movement: a leaping creature hits the first hostile it touches, once; a wall stops it.</summary>
    internal void ResolveLeapContacts()
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Leaping || actor.LeapPhase != LeapPhase.Airborne || actor.Definition.Ai.Leap is not { } leap) continue;
            foreach (Actor other in _world.Actors)
            {
                if (actor.LeapHit || other == actor || !other.IsAlive || other.IsDown || !CanDamage(actor, other, DamageKind.Melee)) continue;
                if (Vector2.Distance(actor.Position, other.Position) > actor.Radius + other.Radius + LeapContactMargin) continue;
                WeaponDefinition weapon = CurrentWeapon(actor);
                ApplyHit(other, new Hit(weapon.Damage, weapon.DamageType, weapon.Knockdown, weapon.KnockdownDuration, actor.LeapDirection,
                    weapon.Knockback, actor, HitMethod.Melee, weapon, weapon.StunDuration));
                actor.LeapHit = true;
            }

            if (actor.LeapHit) Land(actor, hit: true);
            else if (actor.LeapPhaseElapsed > LeapBlockCheckDelay && actor.Velocity.Length() < leap.Speed * 0.3f) Land(actor, hit: false);
        }
    }

    private void Land(Actor actor, bool hit)
    {
        actor.LandLeap();
        actor.Velocity *= 0.2f;
        _world.AddEvent(new LeapLanded(actor, hit));
    }

    private const float LeapAimTurnSpeed = 14f;
    private const float LeapContactMargin = 3f;
    private const float LeapBlockCheckDelay = 0.04f;

    /// <summary>Starts an execution on a nearby vulnerable enemy, or uses a nearby interactable.</summary>
    public bool TryInteract(Actor actor)
    {
        if (!actor.IsFreeToAct) return false;
        if (TryExecute(actor)) return true;

        if (actor.IsPlayer && FindInteractable(actor) is { } interactable)
        {
            _world.Activate(interactable);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Engages the best execution target (same rules for the player and the AI). The target is
    /// reserved until the execution ends or is interrupted.
    /// </summary>
    public bool TryExecute(Actor actor)
    {
        if (!Rules.Execution.Enabled || !actor.IsFreeToAct) return false;
        if (!actor.IsPlayer && !Rules.Execution.EnemiesCanExecute) return false;
        if (FindExecutionTarget(actor) is not { } victim) return false;

        WeaponDefinition weapon = CurrentWeapon(actor);
        ExecutionDefinition execution = weapon.Execution;
        ExecutionVariant? variant = execution.Against.GetValueOrDefault(victim.Definition.Anatomy);
        int strikes = variant?.Strikes ?? execution.Strikes;
        ExecutionAnimation animation = ChooseAnimation(actor, weapon, variant?.Animations is { Count: > 0 } list ? list : execution.Animations);
        int side = ExecutionPose.SideOf(actor, victim);
        if (!actor.BeginExecution(victim, weapon, execution.WindUp * ExecutionTimeScale(actor), animation, strikes, side)) return false;
        actor.Velocity = Vector2.Zero;
        _world.AddEvent(new ExecutionStarted(actor, victim, animation));
        return true;
    }

    /// <summary>One of the listed animations; by default a shot if the gun can fire, a blow otherwise.</summary>
    private ExecutionAnimation ChooseAnimation(Actor actor, WeaponDefinition weapon, List<ExecutionAnimation> animations)
    {
        bool canShoot = weapon.Execution.Mode == ExecutionMode.Shot && actor.Weapon is { Definition.IsFirearm: true } gun
            && gun.Definition == weapon && (!gun.IsEmpty || (!actor.IsPlayer && Rules.EnemiesHaveInfiniteAmmo));
        var usable = animations.Where(a => a != ExecutionAnimation.Shoot || canShoot).ToList();
        if (usable.Count == 0) return canShoot ? ExecutionAnimation.Shoot : ExecutionAnimation.Smash;
        return usable.Count == 1 ? usable[0] : usable[_world.Random.Next(usable.Count)];
    }

    /// <summary>
    /// Vulnerable hostile characters in reach and in clear view: on the floor, getting up, or dazed
    /// when the rules allow it. Not reserved by another executioner, not immune. The choice is
    /// deterministic: nearest gap, with a penalty for targets the actor is not facing.
    /// </summary>
    public Actor? FindExecutionTarget(Actor actor)
    {
        Actor? best = null;
        float bestScore = float.MaxValue;
        foreach (Actor other in _world.Actors)
        {
            if (!IsExecutable(other) || !actor.IsHostileTo(other)) continue;
            Vector2 toOther = other.Position - actor.Position;
            float gap = toOther.Length() - other.Radius - actor.Radius;
            if (gap > Rules.Execution.Range) continue;
            if (!_world.Physics.HasLineOfSight(actor.Position, other.Position, Blocking.Movement)) continue;

            float angle = MathF.Abs(Geo.AngleDifference(actor.Facing, Geo.ToAngle(toOther)));
            float score = gap + angle * FacingPenaltyPerRadian;
            if (score < bestScore || (score == bestScore && best is not null && other.Id < best.Id))
            {
                best = other;
                bestScore = score;
            }
        }

        return best;
    }

    private bool IsExecutable(Actor target)
    {
        if (target.Executioner is not null || !target.IsAlive) return false;
        if (target.Definition.DamageMultipliers.GetValueOrDefault("execution", 1f) <= 0f) return false;
        return target.State is ActorState.KnockedDown or ActorState.GettingUp or ActorState.Wounded
            || (target.State == ActorState.Stunned && Rules.Execution.AllowOnStunned);
    }

    /// <summary>Scales execution timings: global multiplier, and the player's modifier.</summary>
    private float ExecutionTimeScale(Actor actor)
    {
        float speed = actor.IsPlayer ? MathF.Max(0.05f, _world.Modifier.ExecutionSpeed) : 1f;
        return Rules.Execution.DurationMultiplier / speed;
    }

    /// <summary>How much a target behind the executioner counts against it (units per radian).</summary>
    private const float FacingPenaltyPerRadian = 10f;

    /// <summary>Speed at which the executioner gets into position (units/s).</summary>
    private const float MountSpeed = 320f;

    /// <summary>Turn speed while getting into position (radians/s).</summary>
    private const float PoseTurnSpeed = 30f;

    public Interactable? FindInteractable(Actor actor)
    {
        foreach (Interactable interactable in _world.Interactables)
        {
            if (interactable.Available && Vector2.Distance(interactable.Position, actor.Position) <= InteractRadius + actor.Radius)
                return interactable;
        }

        return null;
    }

    /// <summary>
    /// Runs executions: close in, face the victim, deliver blows (on input for the player when the
    /// weapon asks for it, automatically otherwise), kill on the last blow, short recovery.
    /// </summary>
    internal void UpdateExecutions(float dt)
    {
        foreach (Actor actor in _world.Actors)
        {
            if (actor.State != ActorState.Executing || actor.ExecutionFinished) continue;
            Actor? victim = actor.ExecutionTarget;
            WeaponDefinition? weapon = actor.ExecutionWeapon;
            if (victim is null || weapon is null || victim.State != ActorState.BeingExecuted || victim.Executioner != actor)
            {
                actor.EndExecution();
                continue;
            }

            TakePose(actor, victim, dt);
            Vector2 toVictim = ExecutionPose.HeadOf(victim) - actor.Position;

            ExecutionDefinition execution = weapon.Execution;
            float scale = ExecutionTimeScale(actor);
            if (actor.ExecutionElapsed < actor.NextStrikeAt) continue;

            bool manual = actor.IsPlayer && execution.StrikeOnInput;
            bool idleTooLong = actor.ExecutionElapsed >= MathF.Max(actor.NextStrikeAt, actor.LastStrikeAt + execution.AutoStrikeDelay * scale);
            if (manual && !actor.StrikeRequested && !idleTooLong) continue;

            Strike(actor, victim, weapon, Geo.NormalizeOrZero(toVictim), scale);
        }
    }

    private void Strike(Actor actor, Actor victim, WeaponDefinition weapon, Vector2 direction, float scale)
    {
        ExecutionDefinition execution = weapon.Execution;
        actor.StrikeRequested = false;
        actor.ExecutionStrikesDone++;
        actor.LastStrikeAt = actor.ExecutionElapsed;
        actor.NextStrikeAt = actor.ExecutionElapsed + execution.StrikeInterval * scale;
        actor.LastAttackTime = _world.Time;
        bool final = actor.ExecutionStrikesDone >= actor.ExecutionStrikesNeeded;

        bool shot = execution.Mode == ExecutionMode.Shot && actor.Weapon is { Definition.IsFirearm: true } gun
            && gun.Definition == weapon && (gun.TryConsumeAmmo() || (!actor.IsPlayer && Rules.EnemiesHaveInfiniteAmmo));
        if (shot)
        {
            final = true;
            _world.AddEvent(new ShotFired(actor, weapon, victim.Position, actor.Facing));
            _world.EmitNoise(actor.Position, weapon.NoiseRadius, actor);
        }
        else
        {
            _world.EmitNoise(actor.Position, execution.NoiseRadius, actor);
        }

        _world.AddEvent(new ExecutionStrike(actor, victim, weapon, actor.ExecutionStrikesDone, final, shot, actor.ExecutionAnimation));
        if (!final) return;

        actor.FinishExecution(execution.Recovery * scale);
        ApplyHit(victim, new Hit(1_000_000f, "execution", false, 0f, direction, 40f, actor, HitMethod.Execution, weapon));
    }

    /// <summary>
    /// The executioner gets into position (astride the body or beside the head, see
    /// <see cref="ExecutionPose"/>) and turns the right way. Walls stop it like any movement.
    /// </summary>
    private void TakePose(Actor actor, Actor victim, float dt)
    {
        var (position, facing) = ExecutionPose.For(actor, victim, actor.ExecutionAnimation, actor.ExecutionSide);
        actor.Facing = Geo.RotateTowards(actor.Facing, facing, PoseTurnSpeed * dt);
        Vector2 offset = position - actor.Position;
        float distance = offset.Length();
        if (distance <= 0.5f) return;
        actor.Position += offset / distance * MathF.Min(distance, MountSpeed * dt);
        actor.Velocity = _world.Physics.ResolveStatic(actor, Vector2.Zero);
    }
}
