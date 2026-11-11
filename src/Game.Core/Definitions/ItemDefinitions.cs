using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>What an item does in battle (OpenXcom battleType).</summary>
public enum ItemKind
{
    /// <summary>Carried, no battle use (loot, artefacts).</summary>
    None,

    /// <summary>Fires projectiles; uses ammo items unless it has its own <see cref="ItemDefinition.Damage"/>.</summary>
    Firearm,

    /// <summary>Clip or magazine loaded into a firearm; carries the damage of the shots.</summary>
    Ammo,

    /// <summary>Hits an adjacent tile.</summary>
    Melee,

    /// <summary>Thrown, explodes at the end of the turn its fuse runs out.</summary>
    Grenade,

    /// <summary>Remains of a unit (recovered after the mission).</summary>
    Corpse,
}

/// <summary>Kinds of attack, each with its own accuracy and TU cost on a weapon.</summary>
public enum AttackMode
{
    Snap,
    Aimed,
    Auto,
    Melee,
    Throw,
}

/// <summary>Accuracy and cost of one attack mode of a weapon.</summary>
public sealed class AttackModeDefinition
{
    /// <summary>Percentage multiplied with the attacker's skill (firing, melee or throwing).</summary>
    public int Accuracy { get; init; }

    /// <summary>Percentage of the unit's maximum TU, or a flat amount when the item has <see cref="ItemDefinition.FlatTu"/>.</summary>
    public int Tu { get; init; }

    /// <summary>Projectiles per use (auto fire); 1 otherwise.</summary>
    public int Shots { get; init; } = 1;
}

/// <summary>Damage dealt by a projectile, a blow or an explosion.</summary>
public sealed class DamageDefinition
{
    public int Power { get; init; }

    /// <summary>Damage type id (see <c>damageTypes/</c>).</summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Explosion radius in tiles. Null = explosive types use their default (power / 10 in
    /// OpenXcom), other types do not explode.
    /// </summary>
    public int? Radius { get; init; }
}

/// <summary>Distances (in tiles) over which accuracy is kept, then reduced (OXCE ranged accuracy).</summary>
public sealed class RangeDefinition
{
    /// <summary>Beyond this, aimed shots lose <see cref="Dropoff"/> accuracy points per tile.</summary>
    public int Aimed { get; init; } = 200;

    /// <summary>Same for snap shots.</summary>
    public int Snap { get; init; } = 15;

    /// <summary>Same for auto fire.</summary>
    public int Auto { get; init; } = 7;

    /// <summary>Closer than this, accuracy is also reduced.</summary>
    public int Min { get; init; }

    /// <summary>Accuracy points lost per tile outside the ranges above.</summary>
    public int Dropoff { get; init; } = 2;

    /// <summary>Targets further away cannot be fired at.</summary>
    public int Max { get; init; } = 200;
}

public sealed class ItemDefinition : Definition
{
    public string NameKey { get; init; } = "";
    public ItemKind Kind { get; init; }

    public int Weight { get; init; } = 1;

    /// <summary>Size in the inventory grid (cells).</summary>
    public int InventoryWidth { get; init; } = 1;
    public int InventoryHeight { get; init; } = 1;

    /// <summary>Accuracy penalty when the other hand also holds something.</summary>
    public bool TwoHanded { get; init; }

    /// <summary>Attack costs are flat TU amounts instead of percentages of the unit's TU.</summary>
    public bool FlatTu { get; init; }

    public AttackModeDefinition? Snap { get; init; }
    public AttackModeDefinition? Aimed { get; init; }
    public AttackModeDefinition? Auto { get; init; }
    public AttackModeDefinition? Melee { get; init; }

    public RangeDefinition Range { get; init; } = new();

    /// <summary>Firearms: ammo item ids that can be loaded.</summary>
    public IReadOnlyList<string> Ammo { get; init; } = [];

    /// <summary>Ammo: rounds in a full clip. Firearms with their own damage: rounds (0 = unlimited).</summary>
    public int Rounds { get; init; }

    /// <summary>TU to load a clip into this weapon.</summary>
    public int ReloadTu { get; init; } = 15;

    /// <summary>
    /// Damage of the projectile or blow. On ammo: damage of each round. On a firearm: only for
    /// weapons without ammo items (built-in power source).
    /// </summary>
    public DamageDefinition? Damage { get; init; }

    /// <summary>Grenades: TU to prime (percentage or flat like the attack modes).</summary>
    public int PrimeTu { get; init; } = 50;

    /// <summary>Grenades: true if the fuse delay is chosen when priming (0 = explodes at end of this turn).</summary>
    public bool ChooseFuse { get; init; } = true;

    /// <summary>Logical sprite ids: on the map floor, in the inventory, and of the projectile.</summary>
    public string FloorSprite { get; init; } = "";
    public string InventorySprite { get; init; } = "";
    public string? ProjectileSprite { get; init; }

    public string? FireSound { get; init; }
    public string? HitSound { get; init; }

    public AttackModeDefinition? Mode(AttackMode mode) => mode switch
    {
        AttackMode.Snap => Snap,
        AttackMode.Aimed => Aimed,
        AttackMode.Auto => Auto,
        AttackMode.Melee => Melee,
        _ => null,
    };
}
