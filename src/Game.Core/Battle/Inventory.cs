using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// Where units carry items and reloading. Slot ids "rightHand", "leftHand" and "ground" are
/// known to the rules; every other slot (belt, backpack…) is defined by the content.
/// </summary>
public static class Inventory
{
    public const string RightHand = "rightHand";
    public const string LeftHand = "leftHand";
    public const string Ground = "ground";

    public static readonly IReadOnlyList<string> Hands = [RightHand, LeftHand];

    /// <summary>Puts an item in a slot (first free cell of a grid). Returns false if it does not fit.</summary>
    public static bool TryPlace(Battle battle, BattleUnit unit, BattleItem item, string slotId)
    {
        if (!battle.Rules.InventorySlots.TryGetValue(slotId, out InventorySlotDefinition? slot)) return false;
        if (slot.Kind == InventorySlotKind.Ground) return false;
        if (slot.Kind == InventorySlotKind.Hand)
        {
            if (unit.ItemInSlot(slotId) is not null) return false;
            Assign(unit, item, slotId, 0, 0);
            return true;
        }

        int width = item.Definition.InventoryWidth, height = item.Definition.InventoryHeight;
        for (int y = 0; y + height <= slot.Height; y++)
            for (int x = 0; x + width <= slot.Width; x++)
                if (IsFree(unit, slotId, x, y, width, height))
                {
                    Assign(unit, item, slotId, x, y);
                    return true;
                }

        return false;
    }

    /// <summary>Puts an item in the first slot with room, hands first then by fill order.</summary>
    public static bool TryPlaceAnywhere(Battle battle, BattleUnit unit, BattleItem item)
    {
        IEnumerable<InventorySlotDefinition> slots = battle.Rules.InventorySlots.Values
            .Where(s => s.Kind != InventorySlotKind.Ground)
            .OrderBy(s => s.FillOrder).ThenBy(s => s.Id, StringComparer.Ordinal);
        return slots.Any(slot => TryPlace(battle, unit, item, slot.Id));
    }

    private static bool IsFree(BattleUnit unit, string slotId, int x, int y, int width, int height)
    {
        foreach (BattleItem other in unit.Inventory)
        {
            if (other.Slot != slotId) continue;
            bool overlap = x < other.SlotX + other.Definition.InventoryWidth && other.SlotX < x + width
                && y < other.SlotY + other.Definition.InventoryHeight && other.SlotY < y + height;
            if (overlap) return false;
        }

        return true;
    }

    private static void Assign(BattleUnit unit, BattleItem item, string slotId, int x, int y)
    {
        item.Owner = unit;
        item.Slot = slotId;
        item.SlotX = x;
        item.SlotY = y;
        item.GroundPosition = null;
        unit.Inventory.Add(item);
    }

    /// <summary>
    /// TU to move an item from one slot to another (OXCE RuleInventory::getCost): the source
    /// slot's cost towards the target, 0 within the same slot, unknown pairs refused.
    /// </summary>
    public static int? MoveCost(Battle battle, string from, string to)
    {
        if (from == to) return 0;
        return battle.Rules.InventorySlots.TryGetValue(from, out InventorySlotDefinition? slot) && slot.Costs.TryGetValue(to, out int cost) ? cost : null;
    }

    /// <summary>Moves a carried item (or one lying on the unit's tile) to a slot, paying the slot cost.</summary>
    public static CommandResult Move(Battle battle, BattleUnit unit, int itemId, string target)
    {
        BattleItem? item = battle.GetItem(itemId);
        bool onGround = item?.GroundPosition == unit.Position;
        if (item is null || (item.Owner != unit && !onGround)) return CommandResult.Refused("error.item_out_of_reach");
        if (item.Body is not null) return CommandResult.Refused("error.item_too_heavy");
        string from = onGround ? Ground : item.Slot!;
        if (MoveCost(battle, from, target) is not { } cost) return CommandResult.Refused("error.cannot_move_there");
        if (unit.Tu < cost) return CommandResult.Refused("error.not_enough_tu", cost);

        if (target == Ground)
        {
            unit.Tu -= cost;
            battle.DropItem(item, unit.Position);
        }
        else
        {
            if (onGround) battle.Map[unit.Position]!.Items.Remove(item);
            else unit.Inventory.Remove(item);
            item.GroundPosition = null;
            if (!TryPlace(battle, unit, item, target))
            {
                // No room: put it back where it was.
                if (onGround) battle.DropItem(item, unit.Position);
                else Assign(unit, item, from, item.SlotX, item.SlotY);
                return CommandResult.Refused("error.no_room");
            }

            unit.Tu -= cost;
        }

        battle.Emit(new ItemMoved(unit.Id, item.Id, target));
        return CommandResult.Ok;
    }

    /// <summary>A clip the unit carries (not already loaded) that fits the weapon.</summary>
    public static BattleItem? FindAmmo(BattleUnit unit, BattleItem weapon) =>
        unit.Inventory.FirstOrDefault(i => i != weapon && i.Definition.Kind == ItemKind.Ammo && weapon.Definition.Ammo.Contains(i.Definition.Id) && i.Rounds > 0);

    /// <summary>
    /// Loads a carried clip into a weapon held in hand (OXCE tuLoad, 15 TU by default). An empty
    /// clip is thrown away.
    /// </summary>
    public static CommandResult Reload(Battle battle, BattleUnit unit, int weaponId)
    {
        BattleItem? weapon = battle.GetItem(weaponId);
        if (weapon is null || weapon.Owner != unit || !Hands.Contains(weapon.Slot)) return CommandResult.Refused("error.not_in_hand");
        if (!weapon.IsFirearm || weapon.SelfPowered) return CommandResult.Refused("error.cannot_reload");
        if (weapon.LoadedAmmo is { Rounds: > 0 }) return CommandResult.Refused("error.already_loaded");
        BattleItem? clip = FindAmmo(unit, weapon);
        if (clip is null) return CommandResult.Refused("error.no_ammo");
        int cost = weapon.Definition.ReloadTu;
        if (unit.Tu < cost) return CommandResult.Refused("error.not_enough_tu", cost);

        unit.Tu -= cost;
        if (weapon.LoadedAmmo is { } empty) battle.DestroyItem(empty);
        Load(unit, weapon, clip);
        battle.Emit(new WeaponReloaded(unit.Id, weapon.Id, clip.Id));
        return CommandResult.Ok;
    }

    /// <summary>
    /// Primes a grenade held in hand (OXCE: 50 % of the unit's maximum TU). Fuse 0 explodes at the
    /// end of the current turn, n at the end of the n-th following turn of the player or hostiles.
    /// </summary>
    public static CommandResult Prime(Battle battle, BattleUnit unit, int itemId, int fuse)
    {
        BattleItem? item = battle.GetItem(itemId);
        if (item is null || item.Owner != unit || !Hands.Contains(item.Slot)) return CommandResult.Refused("error.not_in_hand");
        if (item.Definition.Kind != ItemKind.Grenade) return CommandResult.Refused("error.not_a_grenade");
        if (item.Fuse is not null) return CommandResult.Refused("error.already_primed");
        int cost = Combat.CombatRules.Percent(unit.MaxTu, battle.Rules.Battle.PrimeTuPercent);
        if (unit.Tu < cost) return CommandResult.Refused("error.not_enough_tu", cost);
        unit.Tu -= cost;
        item.Fuse = Math.Max(0, fuse);
        item.PrimedBy = unit;
        battle.Emit(new GrenadePrimed(unit.Id, item.Id, item.Fuse.Value));
        return CommandResult.Ok;
    }

    /// <summary>Puts a clip into a weapon (no cost: used when equipping units).</summary>
    public static void Load(BattleUnit? unit, BattleItem weapon, BattleItem clip)
    {
        unit?.Inventory.Remove(clip);
        clip.Owner = null;
        clip.Slot = null;
        clip.GroundPosition = null;
        weapon.LoadedAmmo = clip;
    }

    /// <summary>The weapon a unit attacks with by default: right hand, then left hand.</summary>
    public static BattleItem? MainWeapon(BattleUnit unit) =>
        Hands.Select(unit.ItemInSlot).FirstOrDefault(i => i is not null && (i.IsFirearm || i.Definition.Melee is not null));
}
