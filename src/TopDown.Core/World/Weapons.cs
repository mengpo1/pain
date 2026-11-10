using System.Numerics;
using TopDown.Core.Definitions;

namespace TopDown.Core.World;

/// <summary>A concrete weapon: its definition plus its remaining ammunition.</summary>
public sealed class WeaponInstance
{
    public WeaponInstance(WeaponDefinition definition, int? ammo = null)
    {
        Definition = definition;
        Ammo = definition.IsFirearm ? Math.Clamp(ammo ?? definition.MagazineSize, 0, definition.MagazineSize) : 0;
    }

    public WeaponDefinition Definition { get; }
    public int Ammo { get; private set; }
    public bool IsEmpty => Definition.IsFirearm && Ammo <= 0;

    public bool TryConsumeAmmo()
    {
        if (!Definition.IsFirearm) return true;
        if (Ammo <= 0) return false;
        Ammo--;
        return true;
    }
}

/// <summary>
/// A weapon lying in the world: either resting on the floor (can be picked up)
/// or flying after a throw (can hurt). A weapon held by a character is not a WorldWeapon.
/// </summary>
public sealed class WorldWeapon
{
    public const float CollisionRadius = 6f;

    internal WorldWeapon(WeaponInstance weapon, Vector2 position, float rotation)
    {
        Weapon = weapon;
        Position = position;
        PreviousPosition = position;
        Rotation = rotation;
    }

    public WeaponInstance Weapon { get; }
    public Vector2 Position { get; internal set; }
    public Vector2 PreviousPosition { get; internal set; }
    public Vector2 Velocity { get; internal set; }
    public float Rotation { get; internal set; }
    public float Spin { get; internal set; }

    /// <summary>Set while the weapon is a dangerous thrown projectile.</summary>
    public Actor? Thrower { get; internal set; }

    public bool IsFlying => Thrower is not null;
    public bool IsResting => Thrower is null && Velocity.LengthSquared() < 1f;
}

public sealed class Projectile
{
    internal Projectile(Actor? owner, WeaponDefinition weapon, Vector2 position, Vector2 velocity, float range)
    {
        Owner = owner;
        Weapon = weapon;
        Position = position;
        PreviousPosition = position;
        Velocity = velocity;
        RemainingRange = range;
    }

    public Actor? Owner { get; }
    public WeaponDefinition Weapon { get; }
    public Vector2 Position { get; internal set; }
    public Vector2 PreviousPosition { get; internal set; }
    public Vector2 Velocity { get; }
    public float RemainingRange { get; internal set; }
    public bool Alive { get; internal set; } = true;
}

public sealed class ItemPickup
{
    internal ItemPickup(LevelItem definition, Vector2 position)
    {
        Definition = definition;
        Position = position;
    }

    public LevelItem Definition { get; }
    public Vector2 Position { get; }
    public bool Collected { get; internal set; }
}

public sealed class Interactable
{
    internal Interactable(LevelInteractable definition, Vector2 position)
    {
        Definition = definition;
        Position = position;
    }

    public LevelInteractable Definition { get; }
    public Vector2 Position { get; }
    public bool Used { get; internal set; }
    public bool Available => !(Used && Definition.Once);
}

/// <summary>A static solid object (table, crate…) with its own collision box.</summary>
public sealed class Prop
{
    internal Prop(LevelProp definition, Geometry.RectF bounds)
    {
        Definition = definition;
        Bounds = bounds;
    }

    public LevelProp Definition { get; }
    public Geometry.RectF Bounds { get; }

    public Blocking Blocking =>
        (Definition.BlocksMovement ? Blocking.Movement : Blocking.None)
        | (Definition.BlocksProjectiles ? Blocking.Projectiles : Blocking.None)
        | (Definition.BlocksVision ? Blocking.Vision : Blocking.None);
}
