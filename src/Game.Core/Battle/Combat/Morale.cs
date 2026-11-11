using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Morale changes (OXCE BattleUnit::moraleChange, BattlescapeGame::checkForCasualties). Losses
/// are reduced by bravery: × (110 − bravery) / 100. Rank and leadership modifiers are not
/// implemented yet (they all count as 100 %).
/// </summary>
public static class Morale
{
    public const int Max = 100;

    public static void Change(BattleUnit unit, int delta) => unit.Morale = Math.Clamp(unit.Morale + delta, 0, Max);

    /// <summary>OXCE reduceByBravery.</summary>
    public static int ReduceByBravery(BattleUnit unit, int loss) => (110 - unit.Stats[StatIds.Bravery]) * loss / 100;

    /// <summary>
    /// A death shakes the victim's side (−12 at bravery 50 with default values) and cheers the
    /// others (+10); the killer gains 20 for an enemy and loses for a friend or a civilian.
    /// </summary>
    public static void OnDeath(Battle battle, BattleUnit victim, BattleUnit? killer)
    {
        if (victim.Faction == Faction.Neutral && killer is null) return;
        BattleRules rules = battle.Rules.Battle;
        if (victim.Faction != Faction.Neutral)
        {
            foreach (BattleUnit unit in battle.Units)
            {
                if (!unit.IsActive || unit == victim) continue;
                if (unit.Faction == victim.Faction)
                {
                    int bravery = (110 - unit.Stats[StatIds.Bravery]) * 10 / 100;
                    Change(unit, -(100 * rules.MoraleLossWhenKilled * 200 * bravery / 100 / 100 / 100));
                }
                else
                {
                    Change(unit, rules.MoraleGainOnEnemyDeath);
                }
            }
        }

        if (killer is null || !killer.IsActive) return;
        if (Factions.AreEnemies(killer.Faction, victim.Faction)) Change(killer, rules.MoraleGainForKiller);
        else if (killer.Faction == victim.Faction) Change(killer, -(2000 * rules.MoraleLossWhenKilled / 100 / 100));
        else Change(killer, -(1000 * rules.MoraleLossWhenKilled / 100 / 100));
    }
}
