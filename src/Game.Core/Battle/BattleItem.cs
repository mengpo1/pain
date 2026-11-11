using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// One physical item in a battle: carried by a unit (in a slot) or lying on a tile. The
/// definition says what the item is; this object holds what changes (ammo left, fuse, place).
/// </summary>
public sealed class BattleItem
{
    internal BattleItem(int id, ItemDefinition definition)
    {
        Id = id;
        Definition = definition;
        Rounds = definition.Rounds;
    }

    public int Id { get; }
    public ItemDefinition Definition { get; }

    /// <summary>Unit carrying the item, or null when it lies on the ground.</summary>
    public BattleUnit? Owner { get; internal set; }

    /// <summary>Inventory slot id while carried.</summary>
    public string? Slot { get; internal set; }

    /// <summary>Cell of the top-left corner in a grid slot.</summary>
    public int SlotX { get; internal set; }
    public int SlotY { get; internal set; }

    /// <summary>Tile where the item lies, when on the ground.</summary>
    public Position? GroundPosition { get; internal set; }

    /// <summary>Clip loaded in a firearm.</summary>
    public BattleItem? LoadedAmmo { get; internal set; }

    /// <summary>Rounds left (ammo items, and firearms with their own power source).</summary>
    public int Rounds { get; internal set; }

    /// <summary>Primed grenade: turns before it explodes (0 = at the end of this turn); null = not primed.</summary>
    public int? Fuse { get; internal set; }

    /// <summary>Corpses and unconscious bodies: the unit they come from.</summary>
    public BattleUnit? Body { get; internal set; }

    public bool IsFirearm => Definition.Kind == ItemKind.Firearm;

    /// <summary>Weapons with their own damage (lasers, bows with infinite arrows) need no clip.</summary>
    public bool SelfPowered => IsFirearm && Definition.Damage is not null;

    /// <summary>The item whose damage a shot from this weapon deals: its clip, or itself.</summary>
    public BattleItem? AmmoSource => SelfPowered ? this : LoadedAmmo;

    /// <summary>True if the weapon can fire one more round.</summary>
    public bool HasAmmo => AmmoSource is { } source && (source.Rounds > 0 || (SelfPowered && Definition.Rounds == 0));

    public override string ToString() => $"{Definition.Id}#{Id}";
}
