using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>
/// Victory and defeat. As in OpenXcom, losing every unit is a defeat and neutralising every
/// hostile is a victory whatever the mission; missions add their own objective on top.
/// </summary>
public static class Objectives
{
    public static BattleOutcome? Evaluate(Battle battle)
    {
        MissionDefinition mission = battle.Mission;
        if (!battle.ActiveUnitsOf(Faction.Player).Any())
            return new BattleOutcome(false, battle.Turn, "mission.defeat.all_down");
        if (battle.UnitsOf(Faction.Hostile).Any() && !battle.ActiveUnitsOf(Faction.Hostile).Any())
            return new BattleOutcome(true, battle.Turn, "mission.victory.eliminated");

        ObjectiveDefinition objective = mission.Objective;
        bool playerTurnStarted = battle.CurrentFaction == Faction.Player && !battle.IsBusy;
        switch (objective.Kind)
        {
            case ObjectiveKind.Survive when playerTurnStarted && battle.Turn > objective.Turns:
                return new BattleOutcome(true, battle.Turn, "mission.victory.survived");
            case ObjectiveKind.ReachZone when ZoneReached(battle, objective):
                return new BattleOutcome(true, battle.Turn, "mission.victory.zone");
        }

        if (mission.TurnLimit > 0 && playerTurnStarted && battle.Turn > mission.TurnLimit)
            return new BattleOutcome(false, battle.Turn, "mission.defeat.time");
        return null;
    }

    /// <summary>Player units standing in the objective zone, and how many are needed.</summary>
    public static (int Inside, int Needed) ZoneProgress(Battle battle, ObjectiveDefinition objective)
    {
        List<BattleUnit> active = battle.ActiveUnitsOf(Faction.Player).ToList();
        int inside = active.Count(u => battle.Map[u.Position]?.Zone == objective.Zone);
        int needed = objective.Units > 0 ? Math.Min(objective.Units, active.Count) : active.Count;
        return (inside, needed);
    }

    private static bool ZoneReached(Battle battle, ObjectiveDefinition objective)
    {
        (int inside, int needed) = ZoneProgress(battle, objective);
        return needed > 0 && inside >= needed;
    }
}
