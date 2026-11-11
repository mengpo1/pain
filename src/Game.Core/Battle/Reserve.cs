using Game.Core.Battle.Combat;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>Attack mode the player keeps TU for during the turn (OXCE "reserve" buttons).</summary>
public enum ReserveMode
{
    None,
    Snap,
    Aimed,
    Auto,
}

/// <summary>
/// The player's TU reserve (OXCE checkReservedTU): walking, turning on the spot and kneeling are
/// refused if they would leave fewer TU than the reserved shot (+ 4 to kneel, if that reserve is
/// on). A unit that already cannot pay its reserve is not restricted, and shooting never is.
/// The AI keeps its own reserve when patrolling (see AiController).
/// </summary>
public sealed class Reserve
{
    public ReserveMode Mode { get; set; }

    /// <summary>Also keep the TU to kneel (standing units only).</summary>
    public bool Kneel { get; set; }

    /// <summary>
    /// TU reserved for a unit: the cost of its main weapon in the chosen mode, falling back
    /// auto → snap → aimed when the weapon lacks that mode.
    /// </summary>
    public int For(Battle battle, BattleUnit unit)
    {
        int reserved = 0;
        if (Mode != ReserveMode.None && Inventory.MainWeapon(unit) is { } weapon)
        {
            AttackMode first = Mode switch { ReserveMode.Snap => AttackMode.Snap, ReserveMode.Aimed => AttackMode.Aimed, _ => AttackMode.Auto };
            foreach (AttackMode mode in new[] { first, AttackMode.Snap, AttackMode.Aimed })
            {
                int cost = CombatRules.AttackTu(unit, weapon.Definition, mode);
                if (cost <= 0) continue;
                reserved = cost;
                break;
            }
        }

        if (Kneel && !unit.Kneeling && unit.Armor.CanKneel) reserved += battle.Rules.Battle.KneelTu;
        return reserved;
    }

    /// <summary>True if spending <paramref name="cost"/> TU keeps the reserve (or the reserve is already out of reach).</summary>
    public bool Allows(Battle battle, BattleUnit unit, int cost)
    {
        if (unit.Faction != Faction.Player) return true;
        int reserved = For(battle, unit);
        if (reserved == 0 || unit.Tu < reserved) return true;
        return unit.Tu - cost >= reserved;
    }
}
