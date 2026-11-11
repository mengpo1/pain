using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Movement;

/// <summary>One walking step: where it ends, what it costs, and the door it opens on the way.</summary>
public readonly record struct Step(Position To, int Direction, int Tu, int Energy, (Position Tile, TilePartSlot Slot)? Door);

/// <summary>
/// Which single walking steps are possible and what they cost, following OpenXcom Extended's
/// Pathfinding::getTUCost (see docs/mechanics.md). All numbers come from the tile parts and
/// <see cref="BattleRules"/>.
/// <list type="bullet">
/// <item>Cost = floor TU + object TU of the destination (an object one stands on, such as stairs,
/// is not counted), 4 if that sum is 0, +1 when climbing, ×1.5 on diagonals, then plus the average
/// TU of the walls crossed.</item>
/// <item>A step rises at most 8 voxels. From a surface at least 16 high (top of stairs) a unit steps
/// onto the floor of the level above; onto a tile without floor it goes down a level if the
/// surface below is at least 12 high.</item>
/// <item>A diagonal step is blocked if any wall of the 2 × 2 corner square blocks; objects and units
/// beside the corner do not block it.</item>
/// </list>
/// </summary>
public static class MovementRules
{
    public static bool TryStep(BattleMap map, BattleRules rules, Position from, int direction, out Step step)
    {
        step = default;
        (int dx, int dy) = Directions.Delta(direction);
        int x = from.X + dx, y = from.Y + dy;
        if (!map.InBounds(x, y, from.Z)) return false;

        int startSurface = map.StandElevation(from);
        Tile sameLevel = map[x, y, from.Z]!;
        Tile? above = map[x, y, from.Z + 1];
        Tile? below = map[x, y, from.Z - 1];
        Position to;
        int crossingLevel = from.Z;
        bool climbing = false;

        if (startSurface >= rules.ClimbHeight && above?.Floor is not null && sameLevel.Floor is null)
        {
            to = new Position(x, y, from.Z + 1);
            crossingLevel = from.Z + 1;
            climbing = true;
        }
        else if (sameLevel.Floor is not null)
        {
            to = new Position(x, y, from.Z);
            if (sameLevel.SurfaceHeight - startSurface > rules.MaxStepUp) return false;
        }
        else if (below is not null && below.SurfaceHeight >= rules.DescendHeight)
        {
            to = below.Position;
        }
        else
        {
            return false;
        }

        Tile destination = map[to]!;
        if (destination.Object is { TuWalk: TilePartDefinition.Impassable }) return false;

        var crossFrom = new Position(from.X, from.Y, crossingLevel);
        if (!TryCrossEdges(map, crossFrom, direction, out int wallTu, out (Position, TilePartSlot)? door)) return false;

        int cost = (destination.Floor?.TuWalk ?? 0) + (destination.Object is { StandHeight: 0 } obj ? obj.TuWalk : 0);
        if (destination.Floor is null) cost += rules.MinStepTu;
        if (cost == 0) cost = rules.MinStepTu;
        if (climbing) cost += rules.ClimbTu;
        if (Directions.IsDiagonal(direction)) cost = cost * rules.DiagonalCostPercent / 100;
        cost += wallTu;
        if (cost >= TilePartDefinition.Impassable) return false;

        step = new Step(to, direction, cost, cost * rules.StepEnergyPercent / 100, door);
        return true;
    }

    /// <summary>
    /// Checks the tile edges crossed by a step on one level. Orthogonal steps cross one edge, where
    /// a closed door is opened (reported in <paramref name="door"/>). Diagonal steps must find all
    /// four edges around the corner open; doors are never opened diagonally.
    /// <paramref name="wallTu"/> is the average TU of the walkable walls crossed (OXCE averages the
    /// walls with a non-zero cost).
    /// </summary>
    public static bool TryCrossEdges(BattleMap map, Position from, int direction, out int wallTu, out (Position Tile, TilePartSlot Slot)? door)
    {
        wallTu = 0;
        door = null;
        if (!Directions.IsDiagonal(direction))
        {
            TilePartDefinition? wall = map.EdgeWall(from, direction);
            if (wall is null) return true;
            if (wall.Door != DoorKind.None)
            {
                (Tile? tile, TilePartSlot slot) = map.EdgeWallLocation(from, direction);
                door = (tile!.Position, slot);
            }
            else if (!wall.Walkable)
            {
                return false;
            }

            wallTu = wall.TuWalk;
            return true;
        }

        int first = Directions.Normalize(direction - 1), second = Directions.Normalize(direction + 1);
        Position sideA = from.Step(first), sideB = from.Step(second);
        if (!map.InBounds(sideA) || !map.InBounds(sideB)) return false;
        TilePartDefinition?[] walls =
        [
            map.EdgeWall(from, first), map.EdgeWall(from, second),
            map.EdgeWall(sideA, second), map.EdgeWall(sideB, first),
        ];
        if (walls.Any(w => w is not null && !w.Walkable)) return false;

        // Edges actually crossed: one on the start tile's side, one on the destination's side.
        int[] costs = new[] { map.EdgeWall(from, first), map.EdgeWall(sideA, second) }
            .Where(w => w is { TuWalk: > 0 }).Select(w => w!.TuWalk).ToArray();
        wallTu = costs.Length == 0 ? 0 : costs.Sum() / costs.Length;
        return true;
    }
}
