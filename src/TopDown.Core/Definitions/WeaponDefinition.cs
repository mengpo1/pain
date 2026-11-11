using System.Numerics;

namespace TopDown.Core.Definitions;

public enum WeaponKind
{
    Melee,
    Firearm,
}

/// <summary>What happens when the owner attacks with an empty firearm.</summary>
public enum EmptyBehavior
{
    /// <summary>Nothing but an empty click.</summary>
    Click,
    /// <summary>The weapon is thrown automatically.</summary>
    Throw,
    /// <summary>The weapon is dropped at the owner's feet.</summary>
    Drop,
}

/// <summary>
/// Data describing a weapon. Every value comes from content files; the engine only
/// interprets them. Distances are in world units (pixels at zoom 1), times in seconds.
/// </summary>
public sealed class WeaponDefinition
{
    public string Id { get; set; } = "";
    public string NameKey { get; set; } = "";
    public WeaponKind Kind { get; set; } = WeaponKind.Melee;

    /// <summary>Sprite used on the ground, in flight and in the hands.</summary>
    public string SpriteId { get; set; } = "";

    /// <summary>False for natural weapons (fists, claws): never dropped, never thrown.</summary>
    public bool CanDrop { get; set; } = true;

    public float Damage { get; set; } = 100f;
    public string DamageType { get; set; } = "blunt";

    /// <summary>Melee reach, or maximum bullet travel distance for firearms.</summary>
    public float Range { get; set; } = 30f;

    /// <summary>Total angle of a melee swing.</summary>
    public float ArcDegrees { get; set; } = 90f;

    /// <summary>Duration of the melee attack (the attacker is in the Attacking state).</summary>
    public float AttackDuration { get; set; } = 0.25f;

    /// <summary>Delay between the start of a melee attack and the moment it hits.</summary>
    public float HitDelay { get; set; } = 0.06f;

    /// <summary>Attacks per second.</summary>
    public float FireRate { get; set; } = 3f;

    /// <summary>Keep attacking while the button is held.</summary>
    public bool Automatic { get; set; }

    /// <summary>Maximum number of targets hit by a single melee swing.</summary>
    public int MaxTargets { get; set; } = 1;

    public bool Knockdown { get; set; }
    public float KnockdownDuration { get; set; } = 2.5f;

    /// <summary>Leaves a standing target dazed for this long (0 = no stun).</summary>
    public float StunDuration { get; set; }

    /// <summary>Speed (units/s) given to the target when hit.</summary>
    public float Knockback { get; set; } = 140f;

    /// <summary>Radius in which enemies hear the attack. 0 = silent.</summary>
    public float NoiseRadius { get; set; }

    /// <summary>Ammunition for firearms. 0 for melee weapons.</summary>
    public int MagazineSize { get; set; }

    public int Pellets { get; set; } = 1;
    public float SpreadDegrees { get; set; }

    /// <summary>Bullet speed. 0 or less means an instant (hitscan) shot.</summary>
    public float ProjectileSpeed { get; set; } = 2400f;

    public string ProjectileSpriteId { get; set; } = "";

    /// <summary>Camera shake strength when the weapon is used.</summary>
    public float Recoil { get; set; }

    /// <summary>Distance from the owner's centre to the muzzle (or blade tip).</summary>
    public float MuzzleDistance { get; set; } = 18f;

    /// <summary>Weapon sprite position relative to the owner: X forward, Y to the right.</summary>
    public Vector2 HoldOffset { get; set; } = new(10f, 5f);

    public EmptyBehavior WhenEmpty { get; set; } = EmptyBehavior.Click;

    /// <summary>How this weapon finishes off a target on the floor.</summary>
    public ExecutionDefinition Execution { get; set; } = new();

    public ThrowDefinition Throw { get; set; } = new();
    public WeaponSounds Sounds { get; set; } = new();

    public bool IsFirearm => Kind == WeaponKind.Firearm;
    public float Cooldown => FireRate > 0f ? 1f / FireRate : 0.5f;
}

public enum ExecutionMode
{
    /// <summary>Blows (fists, blades, clubs… or a gun used as a club).</summary>
    Strike,
    /// <summary>Point-blank shot; falls back to strikes when the gun is empty.</summary>
    Shot,
}

/// <summary>
/// Generic finishing move, tuned per weapon: one heavy blow, several quick ones, or a shot.
/// The same sequence serves the player and the enemies.
/// </summary>
public sealed class ExecutionDefinition
{
    public ExecutionMode Mode { get; set; } = ExecutionMode.Strike;

    /// <summary>Blows needed; the last one kills.</summary>
    public int Strikes { get; set; } = 1;

    /// <summary>Delay between engaging and the first blow.</summary>
    public float WindUp { get; set; } = 0.12f;

    /// <summary>Shortest time between two blows.</summary>
    public float StrikeInterval { get; set; } = 0.16f;

    /// <summary>For the player: each blow needs an attack press (fists in the reference game).</summary>
    public bool StrikeOnInput { get; set; }

    /// <summary>Without input, a blow happens anyway after this delay, so an execution never stalls.</summary>
    public float AutoStrikeDelay { get; set; } = 0.45f;

    /// <summary>Time after the final blow before the executioner can act again.</summary>
    public float Recovery { get; set; } = 0.12f;

    /// <summary>Noise of each blow (shots use the weapon's noise).</summary>
    public float NoiseRadius { get; set; }
}

public sealed class ThrowDefinition
{
    public float Speed { get; set; } = 700f;
    public float Damage { get; set; }
    public string DamageType { get; set; } = "thrown";
    public bool Knockdown { get; set; } = true;
    public float KnockdownDuration { get; set; } = 2f;
    /// <summary>Spin of the weapon in flight (radians/s).</summary>
    public float Spin { get; set; } = 18f;
}

public sealed class WeaponSounds
{
    public string? Attack { get; set; }
    public string? Hit { get; set; }
    public string? Empty { get; set; }
    public string? Pickup { get; set; }
}
