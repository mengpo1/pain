using Game.Core.Content;

namespace Game.Core.Definitions;

/// <summary>Sides of a unit's armour; the side hit depends on where the attack comes from.</summary>
public enum ArmorSide
{
    Front,
    Left,
    Right,
    Rear,
    Under,
}

/// <summary>Body parts that can carry fatal wounds (OpenXcom order).</summary>
public enum BodyPart
{
    Head,
    Torso,
    RightArm,
    LeftArm,
    RightLeg,
    LeftLeg,
}

/// <summary>
/// Protection and appearance of a unit (OpenXcom "armor": soldiers wear one, aliens have their
/// own). Damage modifiers multiply the damage of each damage type (100 = normal).
/// </summary>
public sealed class ArmorDefinition : Definition
{
    public string NameKey { get; init; } = "";

    public int Front { get; init; }
    public int Side { get; init; }
    public int Rear { get; init; }
    public int Under { get; init; }

    /// <summary>Damage type id → percentage applied to incoming damage (missing = 100).</summary>
    public IReadOnlyDictionary<string, int> DamageModifiers { get; init; } = new Dictionary<string, int>();

    /// <summary>Added to the wearer's statistics (powered suits, heavy plating with negative TU…).</summary>
    public IReadOnlyDictionary<string, int> Stats { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Sprite sheet id of the wearer. Frames 0–7: standing, by direction; 8–15: kneeling;
    /// 16: lying (unconscious or dead).
    /// </summary>
    public string Sprite { get; init; } = "";

    /// <summary>Height of the unit in voxels, standing and kneeling (a level is 24 voxels).</summary>
    public int StandHeight { get; init; } = 22;
    public int KneelHeight { get; init; } = 14;

    /// <summary>Eye level is this many voxels below the top of the head.</summary>
    public int EyeOffset { get; init; } = 2;

    /// <summary>Item left on the floor when the wearer dies or is knocked out.</summary>
    public string? Corpse { get; init; }

    public bool CanKneel { get; init; } = true;

    /// <summary>Can receive fatal wounds; null = OXCE default (only the player's soldiers bleed).</summary>
    public bool? Woundable { get; init; }

    /// <summary>Tiles at which the wearer sees units standing in the dark; 0 = side default (9 for the player, 20 for hostiles).</summary>
    public int VisibilityAtDark { get; init; }

    /// <summary>Light carried by the wearer (0–15); OXCE gives player units 15 and others none.</summary>
    public int? PersonalLight { get; init; }

    public int ArmorFor(ArmorSide side) => side switch
    {
        ArmorSide.Front => Front,
        ArmorSide.Left or ArmorSide.Right => Side,
        ArmorSide.Rear => Rear,
        _ => Under,
    };
}

/// <summary>Behaviour settings of an AI-controlled unit.</summary>
public sealed class AiDefinition
{
    /// <summary>0 (cautious) to 3 (reckless): how readily the unit attacks rather than takes cover.</summary>
    public int Aggression { get; init; } = 1;

    /// <summary>Turns during which an enemy seen once stays known to this unit's side.</summary>
    public int Intelligence { get; init; } = 2;

    /// <summary>Below this health percentage the unit prefers to retreat and hide.</summary>
    public int RetreatHealthPercent { get; init; } = 30;
}

/// <summary>An item a unit starts the battle with, and where it is carried.</summary>
public sealed class LoadoutEntry
{
    public string Item { get; init; } = "";

    /// <summary>Inventory slot id; null = first slot with room (hands first).</summary>
    public string? Slot { get; init; }

    /// <summary>Firearms: ammo item loaded at start.</summary>
    public string? Ammo { get; init; }
}

/// <summary>A kind of unit: base statistics, armour, starting equipment and behaviour.</summary>
public sealed class UnitDefinition : Definition
{
    public string NameKey { get; init; } = "";

    /// <summary>Stat id → value (see <c>stats/</c>).</summary>
    public IReadOnlyDictionary<string, int> Stats { get; init; } = new Dictionary<string, int>();

    public string Armor { get; init; } = "";

    public IReadOnlyList<LoadoutEntry> Loadout { get; init; } = [];

    public AiDefinition Ai { get; init; } = new();

    /// <summary>Score lost by the owner when this unit dies (mission rating).</summary>
    public int Value { get; init; } = 10;

    public string? DeathSound { get; init; }
}

public enum InventorySlotKind
{
    /// <summary>Holds one item of any size (right and left hands).</summary>
    Hand,

    /// <summary>A grid of cells; items occupy their width × height.</summary>
    Grid,

    /// <summary>The floor of the unit's tile.</summary>
    Ground,
}

/// <summary>
/// A place where a unit carries items (OpenXcom "invs"), with the TU cost of moving an item to
/// each other slot. Screen placement is for the inventory view.
/// </summary>
public sealed class InventorySlotDefinition : Definition
{
    public string NameKey { get; init; } = "";
    public InventorySlotKind Kind { get; init; } = InventorySlotKind.Grid;

    /// <summary>Grid size in cells (hands and ground ignore it).</summary>
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;

    /// <summary>Top-left corner on the inventory screen, in cells.</summary>
    public int[] ScreenPosition { get; init; } = [0, 0];

    /// <summary>Target slot id → TU to move an item there from this slot.</summary>
    public IReadOnlyDictionary<string, int> Costs { get; init; } = new Dictionary<string, int>();

    /// <summary>Order in which loadout items without a slot fill the slots (lower first).</summary>
    public int FillOrder { get; init; }
}
