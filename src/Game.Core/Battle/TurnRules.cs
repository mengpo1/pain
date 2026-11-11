using Game.Core.Battle.Combat;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle;

/// <summary>Turning, kneeling and what happens at the start and end of a side's turn.</summary>
public static class TurnRules
{
    /// <summary>Turns on the spot: TU per eighth of a turn, by the shortest way. Never triggers reaction fire.</summary>
    public static CommandResult Turn(Battle battle, BattleUnit unit, int direction)
    {
        direction = Directions.Normalize(direction);
        int cost = Directions.TurnSteps(unit.Direction, direction) * battle.Rules.Battle.TurnTu;
        if (cost == 0) return CommandResult.Ok;
        if (unit.Tu < cost) return CommandResult.Refused("error.not_enough_tu", cost);
        unit.Tu -= cost;
        Face(battle, unit, direction);
        battle.RefreshVision();
        return CommandResult.Ok;
    }

    /// <summary>Changes facing without cost (while walking, or before a reaction shot).</summary>
    internal static void Face(Battle battle, BattleUnit unit, int direction)
    {
        direction = Directions.Normalize(direction);
        if (unit.Direction == direction) return;
        unit.Direction = direction;
        battle.Emit(new UnitTurned(unit.Id, direction));
    }

    /// <summary>Kneel (4 TU) or stand up (8 TU); both can trigger reaction fire.</summary>
    public static CommandResult ToggleKneel(Battle battle, BattleUnit unit)
    {
        if (!unit.Armor.CanKneel) return CommandResult.Refused("error.cannot_kneel");
        BattleRules rules = battle.Rules.Battle;
        int cost = unit.Kneeling ? rules.StandUpTu : rules.KneelTu;
        if (unit.Tu < cost) return CommandResult.Refused("error.not_enough_tu", cost);
        unit.Tu -= cost;
        unit.Kneeling = !unit.Kneeling;
        battle.Emit(new UnitKneeled(unit.Id, unit.Kneeling));
        battle.RefreshVision();
        ReactionFire.Check(battle, unit);
        return CommandResult.Ok;
    }

    /// <summary>
    /// OXCE BattleUnit::prepareNewTurn for the units of the side about to play: TU back to full
    /// (minus 10 % per leg wound), energy recovery (minus torso wounds), bleeding, stun recovery.
    /// </summary>
    internal static void StartFactionTurn(Battle battle, Faction faction)
    {
        BattleRules rules = battle.Rules.Battle;
        foreach (BattleUnit unit in battle.Units)
        {
            if (unit.Faction != faction || unit.Status == UnitStatus.Dead) continue;
            unit.HitByThisTurn.Clear();
            unit.Done = false;
            if (unit.IsActive)
            {
                int tu = unit.MaxTu;
                int legWounds = unit.Wounds(BodyPart.LeftLeg) + unit.Wounds(BodyPart.RightLeg);
                tu -= tu * (legWounds * rules.LegWoundTuPercent) / 100;
                unit.Tu = Math.Clamp(tu, 0, unit.MaxTu);

                int recovery = rules.EnergyRecovery - unit.Energy * (unit.Wounds(BodyPart.Torso) * 10) / 100;
                unit.Energy = Math.Clamp(unit.Energy + recovery, 0, unit.MaxEnergy);
            }

            int bleeding = unit.TotalWounds * rules.BleedPerWound;
            if (bleeding > 0)
            {
                unit.Health = Math.Max(-4 * unit.MaxHealth, unit.Health - bleeding);
                DamageRules.CheckCasualty(battle, unit, null);
            }

            unit.Stun = Math.Max(0, unit.Stun - rules.StunRecovery);
            if (unit.Status == UnitStatus.Unconscious && unit.Health > 0 && unit.Stun < unit.Health) WakeUp(battle, unit);
        }
    }

    /// <summary>End of a side's turn: sliding doors close if nobody stands in the doorway.</summary>
    internal static void EndFactionTurn(Battle battle, Faction faction) => Doors.CloseSlidingDoors(battle);

    /// <summary>An unconscious unit whose stun dropped below its health stands up again where its body lies.</summary>
    private static void WakeUp(Battle battle, BattleUnit unit)
    {
        BattleItem? body = battle.Items.FirstOrDefault(i => i.Body == unit && i.GroundPosition is not null);
        Position place = body?.GroundPosition ?? unit.Position;
        if (battle.UnitAt(place) is not null) return;
        if (body is not null) battle.DestroyItem(body);
        unit.Status = UnitStatus.Active;
        battle.PlaceUnit(unit, place);
        unit.Tu = 0;
    }
}
