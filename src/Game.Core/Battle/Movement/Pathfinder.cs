using Game.Core.Common;

namespace Game.Core.Battle.Movement;

/// <summary>A walking route: its steps (start excluded) and total TU, including standing up first.</summary>
public sealed record PathResult(IReadOnlyList<Step> Steps, int TotalTu, int StandUpTu)
{
    public static readonly PathResult None = new([], 0, 0);

    public bool Found => Steps.Count > 0;

    /// <summary>TU spent once the given number of steps are walked (standing up included).</summary>
    public int CostAfter(int stepCount)
    {
        int total = StandUpTu;
        for (int i = 0; i < stepCount && i < Steps.Count; i++) total += Steps[i].Tu;
        return total;
    }
}

/// <summary>
/// Cheapest walking routes in TU (Dijkstra; OXCE uses A*, same result). Energy and the TU the
/// unit has left are ignored, as in OXCE: the walk simply stops at the last affordable tile.
/// Tiles occupied by units the walker's side knows about are avoided; unseen units are not.
/// </summary>
public static class Pathfinder
{
    /// <summary>Upper bound on a route's cost (OXCE maxTUCost).</summary>
    public const int MaxCost = 1000;

    public static PathResult Find(Battle battle, BattleUnit unit, Position goal)
    {
        if (goal == unit.Position || !battle.Map.InBounds(goal)) return PathResult.None;
        Dictionary<Position, Node> nodes = Search(battle, unit, MaxCost, goal);
        if (!nodes.TryGetValue(goal, out Node? end)) return PathResult.None;

        var steps = new List<Step>();
        for (Node? node = end; node?.Step is { } step; node = node.Previous) steps.Add(step);
        steps.Reverse();
        int standUp = unit.Kneeling ? battle.Rules.Battle.StandUpTu : 0;
        return new PathResult(steps, end.Cost + standUp, standUp);
    }

    /// <summary>Every tile reachable within <paramref name="maxTu"/>, with its cost (AI, range display).</summary>
    public static Dictionary<Position, int> Reachable(Battle battle, BattleUnit unit, int maxTu)
    {
        int standUp = unit.Kneeling ? battle.Rules.Battle.StandUpTu : 0;
        return Search(battle, unit, maxTu - standUp, null).ToDictionary(pair => pair.Key, pair => pair.Value.Cost + standUp);
    }

    private sealed record Node(int Cost, Node? Previous, Step? Step);

    private static Dictionary<Position, Node> Search(Battle battle, BattleUnit unit, int maxCost, Position? goal)
    {
        var best = new Dictionary<Position, Node> { [unit.Position] = new Node(0, null, null) };
        var queue = new PriorityQueue<Position, (int Cost, int Order)>();
        int order = 0;
        queue.Enqueue(unit.Position, (0, order++));
        var done = new HashSet<Position>();

        while (queue.TryDequeue(out Position current, out var priority))
        {
            if (!done.Add(current)) continue;
            if (goal is { } g && current == g) break;
            for (int direction = 0; direction < Directions.Count; direction++)
            {
                if (!MovementRules.TryStep(battle.Map, battle.Rules.Battle, current, direction, out Step step)) continue;
                if (IsKnownOccupied(battle, unit, step.To)) continue;
                int cost = priority.Cost + step.Tu;
                if (cost > maxCost) continue;
                if (best.TryGetValue(step.To, out Node? known) && known.Cost <= cost) continue;
                best[step.To] = new Node(cost, best[current], step);
                queue.Enqueue(step.To, (cost, order++));
            }
        }

        return best;
    }

    private static bool IsKnownOccupied(Battle battle, BattleUnit walker, Position p)
    {
        BattleUnit? occupant = battle.UnitAt(p);
        return occupant is not null && occupant != walker && (occupant.Faction == walker.Faction || battle.IsSpottedBy(walker.Faction, occupant));
    }
}
