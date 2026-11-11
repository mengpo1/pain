using Game.Core.Battle.Combat;
using Game.Core.Battle.Movement;
using Game.Core.Common;

namespace Game.Core.Battle.Actions;

/// <summary>
/// Walks a unit along a path, one tile per step (OXCE UnitWalkBState): TU and energy are paid at
/// the start of each step; the walk stops on the last affordable tile, when the next tile turns
/// out to be occupied, when the side spots a new enemy (no reaction fire for that step), or when
/// reaction fire is triggered.
/// </summary>
internal sealed class MoveAction : BattleAction
{
    private readonly BattleUnit _unit;
    private readonly IReadOnlyList<Step> _steps;
    private int _next;

    private MoveAction(BattleUnit unit, IReadOnlyList<Step> steps)
    {
        _unit = unit;
        _steps = steps;
    }

    public static (CommandResult, BattleAction?) Create(Battle battle, BattleUnit unit, Position destination)
    {
        PathResult path = battle.FindPath(unit, destination);
        if (!path.Found) return (CommandResult.Refused("error.no_path"), null);
        int first = path.StandUpTu + path.Steps[0].Tu;
        if (unit.Tu < first) return (CommandResult.Refused("error.not_enough_tu", first), null);
        return (CommandResult.Ok, new MoveAction(unit, path.Steps));
    }

    /// <summary>A walk the unit does not choose (panic): null if there is no path.</summary>
    internal static MoveAction? Forced(Battle battle, BattleUnit unit, Position destination)
    {
        PathResult path = battle.FindPath(unit, destination);
        return path.Found ? new MoveAction(unit, path.Steps) : null;
    }

    public override bool Advance(Battle battle)
    {
        if (!_unit.IsActive || _next >= _steps.Count) return true;

        if (_unit.Kneeling)
        {
            // Walking starts by standing up (8 TU), which can itself trigger reaction fire;
            // any reaction stops the walk.
            int queued = battle.QueuedActions;
            CommandResult stand = TurnRules.ToggleKneel(battle, _unit);
            if (!stand.Accepted) return Interrupt(battle, stand.ReasonKey ?? "error.not_enough_tu");
            return battle.QueuedActions > queued;
        }

        Step planned = _steps[_next];
        if (!MovementRules.TryStep(battle.Map, battle.Rules.Battle, _unit.Position, planned.Direction, out Step step) || step.To != planned.To)
            return Interrupt(battle, "error.path_blocked");

        BattleUnit? occupant = battle.UnitAt(step.To);
        if (occupant is not null && occupant != _unit)
        {
            // Someone unseen stands there: face them and give up, without paying.
            TurnRules.Face(battle, _unit, step.Direction);
            battle.RefreshVision();
            return Interrupt(battle, "error.path_blocked");
        }

        if (step.Tu > _unit.Tu) return Interrupt(battle, "error.not_enough_tu");
        if (!battle.PlayerReserve.Allows(battle, _unit, step.Tu)) return Interrupt(battle, "error.tu_reserved");
        if (step.Energy > _unit.Energy) return Interrupt(battle, "error.not_enough_energy");

        _unit.Tu -= step.Tu;
        _unit.Energy -= step.Energy;
        TurnRules.Face(battle, _unit, step.Direction);
        if (step.Door is { } door) Doors.Open(battle, door.Tile, door.Slot);
        Position from = _unit.Position;
        battle.PlaceUnit(_unit, step.To);
        battle.Emit(new UnitMoved(_unit.Id, from, step.To));
        _next++;

        int spotted = battle.SpottedCount(_unit.Faction);
        battle.RefreshVision();
        if (battle.SpottedCount(_unit.Faction) > spotted) return Interrupt(battle, "info.enemy_spotted");

        if (ReactionFire.Check(battle, _unit)) return true;
        return _next >= _steps.Count;
    }

    private bool Interrupt(Battle battle, string reasonKey)
    {
        battle.Emit(new ActionInterrupted(_unit.Id, reasonKey));
        return true;
    }
}
