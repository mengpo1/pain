using Game.Core.Battle.Combat;
using Game.Core.Battle.Movement;
using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Ai;

/// <summary>
/// Plays an AI side one command at a time, from information its side is allowed to have:
/// enemies it saw within its memory (unit "intelligence", in rounds) at the place it last saw
/// them. Decisions follow OXCE's AIModule in a reduced, readable form — retreat when hurt and
/// exposed, attack a target in sight (or move to a firing point), investigate where an enemy was
/// last seen, otherwise patrol — see docs/mechanics.md for what is simplified.
/// </summary>
public sealed class AiController
{
    /// <summary>Most decisions a unit takes per turn (OXCE: about two per selection, two passes).</summary>
    private const int MaxActionsPerTurn = 4;

    /// <summary>A unit with this many TU or fewer is done (OXCE).</summary>
    private const int MinUsefulTu = 5;

    /// <summary>Side of the square searched for a firing position (OXCE findFirePoint: 11 × 11).</summary>
    private const int FirePointRadius = 5;

    private readonly Battle _battle;
    private readonly Dictionary<BattleUnit, int> _actions = new();
    private readonly Dictionary<BattleUnit, Position> _patrolTargets = new();
    private int _turn = -1;
    private Faction _faction;

    public AiController(Battle battle) => _battle = battle;

    /// <summary>The next command for the side currently playing; EndTurn when every unit is done.</summary>
    public BattleCommand NextCommand()
    {
        if (_turn != _battle.Turn || _faction != _battle.CurrentFaction)
        {
            _turn = _battle.Turn;
            _faction = _battle.CurrentFaction;
            _actions.Clear();
        }

        foreach (BattleUnit unit in _battle.ActiveUnitsOf(_battle.CurrentFaction).OrderBy(u => u.Id))
        {
            if (unit.Done || unit.MoraleState != MoraleState.None) continue;
            int count = _actions.GetValueOrDefault(unit);
            if (count >= MaxActionsPerTurn || unit.Tu <= MinUsefulTu)
            {
                unit.Done = true;
                continue;
            }

            _actions[unit] = count + 1;
            BattleCommand? command = Think(unit);
            if (command is not null) return command;
            unit.Done = true;
        }

        return new EndTurnCommand();
    }

    /// <summary>Tells the controller a command was refused, so the unit does not try it forever.</summary>
    public void Refused(BattleCommand command)
    {
        if (_battle.GetUnit(command.UnitId) is { } unit) unit.Done = true;
    }

    private BattleCommand? Think(BattleUnit unit)
    {
        AiDefinition ai = unit.Definition.Ai;
        List<BattleUnit> known = _battle.KnownEnemies(unit.Faction, ai.Intelligence).ToList();
        // Like OXCE's selectNearestTarget, a known enemy in sight is a target even behind the unit.
        List<BattleUnit> targets = known.Where(e => VisionSystem.CanSee(_battle, unit, e)).ToList();
        BattleItem? weapon = Inventory.MainWeapon(unit);

        bool hurt = unit.Health * 100 < unit.MaxHealth * ai.RetreatHealthPercent;
        if (hurt && targets.Count > 0 && Retreat(unit, known) is { } retreat) return retreat;

        if (weapon is not null && weapon.IsFirearm && !weapon.HasAmmo && Inventory.FindAmmo(unit, weapon) is not null)
            return new ReloadCommand(unit.Id, weapon.Id);

        if (weapon is not null && targets.Count > 0)
        {
            BattleUnit target = targets.MinBy(t => unit.Position.HorizontalDistanceSquared(t.Position))!;
            if (Attack(unit, weapon, target) is { } attack) return attack;
            if (!weapon.IsFirearm && Charge(unit, weapon, target) is { } charge) return charge;
            if (weapon.IsFirearm && FirePoint(unit, weapon, target, known) is { } move) return move;
        }

        if (known.Count > 0)
        {
            BattleUnit nearest = known.MinBy(e => unit.Position.HorizontalDistanceSquared(unit.LastKnownPosition(e)))!;
            Position last = unit.LastKnownPosition(nearest);
            if (last != unit.Position && MoveTowards(unit, last, ReserveFor(unit, weapon)) is { } investigate) return investigate;
        }

        return Patrol(unit, weapon);
    }

    /// <summary>
    /// Picks a fire mode like OXCE's default rule (close: auto, far: aimed, otherwise snap),
    /// kneeling first for aimed or auto fire when the unit can.
    /// </summary>
    private BattleCommand? Attack(BattleUnit unit, BattleItem weapon, BattleUnit target)
    {
        if (weapon.Definition.Melee is not null && !weapon.IsFirearm)
        {
            AttackCommand blow = new(unit.Id, weapon.Id, AttackMode.Melee, target.Position);
            return AttackRules(unit, weapon, AttackMode.Melee, target.Position) ? blow : null;
        }

        if (!weapon.HasAmmo) return null;
        double distance = unit.Position.HorizontalDistance(target.Position);
        AttackMode[] preference = distance < 4 ? [AttackMode.Auto, AttackMode.Snap, AttackMode.Aimed]
            : distance > 12 ? [AttackMode.Aimed, AttackMode.Snap, AttackMode.Auto]
            : [AttackMode.Snap, AttackMode.Aimed, AttackMode.Auto];

        foreach (AttackMode mode in preference)
        {
            if (!AttackRules(unit, weapon, mode, target.Position)) continue;
            int cost = CombatRules.AttackTu(unit, weapon.Definition, mode);
            if (mode != AttackMode.Snap && unit.Armor.CanKneel && !unit.Kneeling && unit.Tu >= cost + _battle.Rules.Battle.KneelTu + TurnCost(unit, target.Position))
                return new KneelCommand(unit.Id);
            if (!HasLineOfFire(unit, target)) return null;
            return new AttackCommand(unit.Id, weapon.Id, mode, target.Position);
        }

        return null;
    }

    private bool AttackRules(BattleUnit unit, BattleItem weapon, AttackMode mode, Position target) =>
        Actions.AttackAction.Validate(_battle, unit, weapon, mode, target, out _).Accepted;

    private int TurnCost(BattleUnit unit, Position target) =>
        Directions.TurnSteps(unit.Direction, Directions.Towards(unit.Position, target)) * _battle.Rules.Battle.TurnTu;

    private bool HasLineOfFire(BattleUnit unit, BattleUnit target)
    {
        Voxel origin = Ballistics.Origin(_battle, unit, target.Position);
        return Ballistics.AimPoint(_battle, unit, origin, target.Position) is { } aim && Ballistics.HasLineOfFire(_battle, unit, origin, aim, target.Position);
    }

    /// <summary>
    /// OXCE findFirePoint: a reachable tile near the unit, keeping enough TU to shoot, with a line
    /// of fire to the target; score = 100 − 10 × enemies watching it + TU left + 10 if outside the
    /// target's view sector. Accepted above 70.
    /// </summary>
    private BattleCommand? FirePoint(BattleUnit unit, BattleItem weapon, BattleUnit target, List<BattleUnit> known)
    {
        int attackTu = CheapestAttack(unit, weapon);
        if (attackTu <= 0 || unit.Tu <= attackTu) return null;
        Dictionary<Position, int> reachable = Pathfinder.Reachable(_battle, unit, unit.Tu - attackTu);
        Position? best = null;
        int bestScore = 70;
        foreach ((Position tile, int cost) in reachable)
        {
            if (tile == unit.Position || Math.Abs(tile.X - unit.Position.X) > FirePointRadius || Math.Abs(tile.Y - unit.Position.Y) > FirePointRadius) continue;
            if (!LineOfFireFrom(unit, tile, target)) continue;
            int score = 100 - 10 * Watchers(tile, known) + (unit.Tu - cost - attackTu);
            if (!VisionSystem.InViewSector(target.Direction, target.Position, tile)) score += 10;
            if (score > bestScore)
            {
                bestScore = score;
                best = tile;
            }
        }

        return best is { } destination ? new MoveCommand(unit.Id, destination) : null;
    }

    /// <summary>
    /// OXCE meleeAction, reduced: walk to the cheapest free tile next to the target that leaves
    /// enough TU to strike; if none is reachable this turn, close in as far as possible.
    /// </summary>
    private BattleCommand? Charge(BattleUnit unit, BattleItem weapon, BattleUnit target)
    {
        int strike = CombatRules.AttackTu(unit, weapon.Definition, AttackMode.Melee);
        Dictionary<Position, int> reachable = Pathfinder.Reachable(_battle, unit, unit.Tu - strike);
        Position? best = null;
        int bestCost = int.MaxValue;
        for (int direction = 0; direction < Directions.Count; direction++)
        {
            Position next = target.Position.Step(direction);
            if (reachable.TryGetValue(next, out int cost) && cost < bestCost && _battle.UnitAt(next) is null)
            {
                best = next;
                bestCost = cost;
            }
        }

        if (best is { } spot) return spot == unit.Position ? null : new MoveCommand(unit.Id, spot);
        return MoveTowards(unit, target.Position, 0);
    }

    /// <summary>Retreat (OXCE escape, reduced): the reachable tile seen by the fewest known enemies, then the farthest from them.</summary>
    private BattleCommand? Retreat(BattleUnit unit, List<BattleUnit> known)
    {
        Dictionary<Position, int> reachable = Pathfinder.Reachable(_battle, unit, unit.Tu);
        int here = Watchers(unit.Position, known);
        Position? best = null;
        (int Watchers, int Distance) bestScore = (here, MinDistance(unit.Position, known, unit));
        foreach (Position tile in reachable.Keys)
        {
            (int, int) score = (Watchers(tile, known), MinDistance(tile, known, unit));
            if (score.Item1 < bestScore.Watchers || (score.Item1 == bestScore.Watchers && score.Item2 > bestScore.Distance))
            {
                bestScore = score;
                best = tile;
            }
        }

        return best is { } destination ? new MoveCommand(unit.Id, destination) : null;
    }

    /// <summary>Known enemies that would have a line of sight to a tile (from where they were last seen).</summary>
    private int Watchers(Position tile, List<BattleUnit> known)
    {
        int count = 0;
        foreach (BattleUnit enemy in known)
        {
            Position from = enemy.LastSeenPositionBy[(int)_battle.CurrentFaction];
            if (from.HorizontalDistanceSquared(tile) > _battle.Rules.Battle.MaxViewDistance * _battle.Rules.Battle.MaxViewDistance) continue;
            if (TileLine(from, tile, enemy)) count++;
        }

        return count;
    }

    private int MinDistance(Position tile, List<BattleUnit> known, BattleUnit unit) =>
        known.Count == 0 ? 0 : known.Min(e => tile.HorizontalDistanceSquared(unit.LastKnownPosition(e)));

    /// <summary>Rough line of sight between two tiles (eye heights), ignoring units: used to judge cover.</summary>
    private bool TileLine(Position from, Position to, BattleUnit viewer)
    {
        var a = Voxel.TileCenter(from, 20);
        var b = Voxel.TileCenter(to, 20);
        TraceHit hit = LineTracer.TraceVoxels(_battle, a, b, viewer, sightOnly: true);
        return !hit.IsBlocked || hit.Tile == to;
    }

    private bool LineOfFireFrom(BattleUnit unit, Position tile, BattleUnit target)
    {
        var origin = new Voxel(tile.X * Voxel.TileWidth + 8, tile.Y * Voxel.TileWidth + 8,
            tile.Z * Voxel.TileHeight + _battle.Map.StandElevation(tile) + unit.Armor.StandHeight - 4);
        var aim = new Voxel(target.Position.X * Voxel.TileWidth + 8, target.Position.Y * Voxel.TileWidth + 8,
            LineTracer.UnitBottom(_battle, target) + target.Height / 2);
        TraceHit hit = LineTracer.TraceVoxels(_battle, origin, aim, unit, sightOnly: false);
        return hit.Unit == target;
    }

    private int CheapestAttack(BattleUnit unit, BattleItem weapon)
    {
        IEnumerable<int> costs = new[] { AttackMode.Snap, AttackMode.Aimed, AttackMode.Auto, AttackMode.Melee }
            .Select(mode => CombatRules.AttackTu(unit, weapon.Definition, mode)).Where(c => c > 0);
        return costs.DefaultIfEmpty(0).Min();
    }

    /// <summary>
    /// TU kept for reaction fire while patrolling (OXCE): cautious units keep an aimed shot,
    /// average ones a burst, bold ones a snap shot.
    /// </summary>
    private int ReserveFor(BattleUnit unit, BattleItem? weapon)
    {
        if (weapon is null || !weapon.IsFirearm) return 0;
        int percent = unit.Definition.Ai.Aggression switch { 0 => 50, 1 => 40, _ => 33 };
        return unit.MaxTu * percent / 100;
    }

    private BattleCommand? MoveTowards(BattleUnit unit, Position goal, int reserve)
    {
        PathResult path = _battle.FindPath(unit, goal);
        if (!path.Found)
        {
            // Unreachable (occupied, walled off): aim for a reachable tile close to it.
            Dictionary<Position, int> reachable = Pathfinder.Reachable(_battle, unit, unit.Tu - reserve);
            if (reachable.Count <= 1) return null;
            Position closest = reachable.Keys.Where(p => p != unit.Position).MinBy(p => p.HorizontalDistanceSquared(goal) * 10 + Math.Abs(p.Z - goal.Z));
            return closest.HorizontalDistanceSquared(goal) < unit.Position.HorizontalDistanceSquared(goal) ? new MoveCommand(unit.Id, closest) : null;
        }

        // Walk as far as the TU allow while keeping the reserve.
        int budget = unit.Tu - reserve;
        Position stop = unit.Position;
        for (int i = 0; i < path.Steps.Count && path.CostAfter(i + 1) <= budget; i++) stop = path.Steps[i].To;
        return stop == unit.Position ? null : new MoveCommand(unit.Id, stop);
    }

    /// <summary>Patrol between the map's patrol points (or random reachable tiles), keeping reaction TU.</summary>
    private BattleCommand? Patrol(BattleUnit unit, BattleItem? weapon)
    {
        int reserve = ReserveFor(unit, weapon);
        if (!_patrolTargets.TryGetValue(unit, out Position target) || target == unit.Position || _battle.UnitAt(target) is not null)
        {
            List<Position> points = _battle.Map.PatrolPoints.Where(p => p != unit.Position && _battle.UnitAt(p) is null).ToList();
            if (points.Count == 0)
            {
                points = Pathfinder.Reachable(_battle, unit, unit.MaxTu).Keys.Where(p => p != unit.Position).ToList();
                if (points.Count == 0) return null;
            }

            target = _battle.Random.Pick(points);
            _patrolTargets[unit] = target;
        }

        return MoveTowards(unit, target, reserve);
    }
}
