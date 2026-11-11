using Game.Core.Battle.Combat;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Actions;

/// <summary>
/// A throw in two steps: the item leaves the hand (the interface shows its arc), then lands.
/// Thrown grenades explode at the end of the turn when their fuse reaches 0, as in OXCE.
/// </summary>
internal sealed class ThrowAction(BattleUnit thrower, BattleItem item, Position target, double arc) : BattleAction
{
    private Position? _landing;
    private Voxel _origin;

    public override bool Advance(Battle battle)
    {
        if (_landing is null)
        {
            if (!thrower.IsActive || item.Owner != thrower) return true;
            int turn = Directions.TurnSteps(thrower.Direction, Directions.Towards(thrower.Position, target)) * battle.Rules.Battle.TurnTu;
            thrower.Tu -= turn + ThrowCost(battle, thrower);
            TurnRules.Face(battle, thrower, Directions.Towards(thrower.Position, target));
            _origin = Ballistics.Origin(battle, thrower, target, thrown: true);
            Voxel aim = Throwing.LandingVoxel(battle, target);
            double accuracy = CombatRules.Accuracy(battle, thrower, item, AttackMode.Throw) / 100.0;
            Voxel deviated = Ballistics.Deviate(_origin, aim, accuracy, battle.Random) with { Z = aim.Z };
            _landing = Throwing.Fly(battle, thrower, _origin, deviated, arc);
            thrower.Inventory.Remove(item);
            item.Owner = null;
            item.Slot = null;
            battle.Emit(new ItemThrown(thrower.Id, item.Id, _origin, Throwing.LandingVoxel(battle, _landing.Value)));
            return false;
        }

        battle.DropItem(item, _landing.Value);
        battle.Emit(new ItemLanded(item.Id, _landing.Value));
        battle.RefreshVision();
        ReactionFire.Check(battle, thrower);
        return true;
    }

    public static int ThrowCost(Battle battle, BattleUnit unit) => CombatRules.Percent(unit.MaxTu, battle.Rules.Battle.ThrowTuPercent);

    public static (CommandResult, BattleAction?) Create(Battle battle, BattleUnit unit, ThrowCommand command)
    {
        BattleItem? item = battle.GetItem(command.ItemId);
        if (item is null || item.Owner != unit || !Inventory.Hands.Contains(item.Slot)) return (CommandResult.Refused("error.not_in_hand"), null);
        if (Throwing.Validate(battle, unit, item, command.Target, out double arc) is { } reason) return (CommandResult.Refused(reason), null);
        int cost = ThrowCost(battle, unit) + Directions.TurnSteps(unit.Direction, Directions.Towards(unit.Position, command.Target)) * battle.Rules.Battle.TurnTu;
        if (unit.Tu < cost) return (CommandResult.Refused("error.not_enough_tu", cost), null);
        return (CommandResult.Ok, new ThrowAction(unit, item, command.Target, arc));
    }
}

/// <summary>An explosion in two steps: the blast is announced (animation), then its damage is applied.</summary>
internal sealed class ExplosionAction(Position center, DamageDefinition damage, BattleUnit? attacker, BattleItem? source) : BattleAction
{
    private bool _announced;

    public override bool Advance(Battle battle)
    {
        if (!_announced)
        {
            _announced = true;
            DamageTypeDefinition? type = battle.Rules.DamageTypes.GetValueOrDefault(damage.Type);
            int radius = type is null ? 1 : Explosions.Radius(battle.Rules.Battle, damage, type);
            battle.Emit(new ExplosionStarted(Voxel.TileCenter(center, battle.Map.StandElevation(center) + 4), radius, damage.Type));
            return false;
        }

        if (source is not null) battle.DestroyItem(source);
        Explosions.Detonate(battle, center, damage, attacker);
        return true;
    }
}

/// <summary>
/// End of a side's turn (OXCE BattlescapeGame::endTurn): grenades lying on the ground with their
/// fuse at 0 explode (never at the end of the neutral turn), then fuses count down, then the next
/// side plays.
/// </summary>
internal sealed class EndTurnAction : BattleAction
{
    private bool _explosionsQueued;

    public override bool Advance(Battle battle)
    {
        if (!_explosionsQueued)
        {
            _explosionsQueued = true;
            if (battle.CurrentFaction == Faction.Neutral) return false;
            foreach (BattleItem grenade in battle.Items.Where(i => i.Fuse == 0 && i.GroundPosition is not null && i.Definition.Damage is not null).ToList())
                battle.Push(new ExplosionAction(grenade.GroundPosition!.Value, grenade.Definition.Damage!, grenade.PrimedBy, grenade));
            return false;
        }

        if (battle.CurrentFaction != Faction.Neutral)
            foreach (BattleItem item in battle.Items)
                if (item.Fuse > 0) item.Fuse--;
        battle.SwitchToNextFaction();
        return true;
    }
}
