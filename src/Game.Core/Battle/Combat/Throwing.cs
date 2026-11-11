using Game.Core.Battle.Vision;
using Game.Core.Common;
using Game.Core.Definitions;

namespace Game.Core.Battle.Combat;

/// <summary>
/// Throwing (OXCE validateThrow / calculateParabola, simplified): reach depends on strength and
/// weight; the item flies on an arc that must clear the terrain; throwing accuracy deviates the
/// landing point; the item falls to the floor where the arc ends.
/// </summary>
public static class Throwing
{
    /// <summary>Apex heights tried, as a fraction of the horizontal distance (OXCE raises the curve until it fits).</summary>
    private static readonly double[] ArcHeights = [0.25, 0.5, 0.9];

    /// <summary>
    /// Horizontal reach in voxels, by simulating a throw at 45° (OXCE getMaxThrowDistance): the
    /// vertical speed starts at 1 voxel per voxel and drops every 8 voxels by
    /// (50 × weight / strength) / 100, the division being on integers.
    /// </summary>
    public static int MaxDistanceVoxels(int weight, int strength, int heightAboveTarget)
    {
        double height = heightAboveTarget + 0.5, climb = 1.0;
        double drop = (50 * Math.Max(1, weight) / Math.Max(1, strength)) / 100.0;
        int travelled = 0;
        while (travelled < 4000)
        {
            travelled += 8;
            height += climb < -1 ? -8 : climb * 8;
            if (height < 0 && climb < 0)
            {
                travelled -= (int)(height / Math.Max(climb, -1.0));
                break;
            }

            climb -= drop;
            if (climb <= -2.0) break;
        }

        return travelled;
    }

    /// <summary>Checks a throw and finds the arc height that reaches the target. Null reason = possible.</summary>
    public static string? Validate(Battle battle, BattleUnit thrower, BattleItem item, Position target, out double arc)
    {
        arc = 0;
        if (!battle.Map.InBounds(target) || target == thrower.Position) return "error.invalid_target";
        if (battle.Map[target]!.Object is { TuWalk: TilePartDefinition.Impassable }) return "error.cannot_throw_there";
        Voxel origin = Ballistics.Origin(battle, thrower, target, thrown: true);
        Voxel landing = LandingVoxel(battle, target);
        int reach = MaxDistanceVoxels(item.Definition.Weight, thrower.Stats[StatIds.Strength], origin.Z - landing.Z);
        if (thrower.Position.HorizontalDistance(target) > (reach + 8) / 16.0) return "error.out_of_range";

        foreach (double height in ArcHeights)
        {
            if (Fly(battle, thrower, origin, landing, height) == target)
            {
                arc = height;
                return null;
            }
        }

        return "error.cannot_throw_there";
    }

    /// <summary>The voxel just above the floor (or surface) of a tile.</summary>
    public static Voxel LandingVoxel(Battle battle, Position tile) =>
        Voxel.TileCenter(tile, battle.Map.StandElevation(tile) + 2);

    /// <summary>
    /// Follows the arc from <paramref name="origin"/> to <paramref name="target"/> and returns the
    /// tile where the item comes to rest: the target, or the last free point before an obstacle,
    /// dropped to the first floor below.
    /// </summary>
    public static Position Fly(Battle battle, BattleUnit thrower, Voxel origin, Voxel target, double arcHeight)
    {
        double horizontal = Math.Sqrt(Math.Pow(target.X - origin.X, 2) + Math.Pow(target.Y - origin.Y, 2));
        double apex = Math.Max(8, horizontal * arcHeight);
        int samples = Math.Max(8, (int)(horizontal / 4));
        Voxel previous = origin;
        for (int i = 1; i <= samples; i++)
        {
            double t = i / (double)samples;
            var point = new Voxel(
                (int)Math.Round(origin.X + (target.X - origin.X) * t),
                (int)Math.Round(origin.Y + (target.Y - origin.Y) * t),
                (int)Math.Round(origin.Z + (target.Z - origin.Z) * t + apex * 4 * t * (1 - t)));
            TraceHit hit = LineTracer.TraceVoxels(battle, previous, point, thrower, sightOnly: false);
            if (hit.IsBlocked) return Settle(battle, previous.Tile);
            previous = point;
        }

        return Settle(battle, target.Tile);
    }

    /// <summary>Drops a tile position down to the first level with a floor.</summary>
    public static Position Settle(Battle battle, Position p)
    {
        if (!battle.Map.InBounds(p)) p = new Position(Math.Clamp(p.X, 0, battle.Map.Width - 1), Math.Clamp(p.Y, 0, battle.Map.Length - 1), Math.Clamp(p.Z, 0, battle.Map.Height - 1));
        while (p.Z > 0 && battle.Map[p]!.Floor is null) p = p.Offset(0, 0, -1);
        return p;
    }
}
